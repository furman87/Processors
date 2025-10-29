using Processors.Interfaces;
using Processors.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Processors.Services;

public class ProcessorEngine : IProcessorEngine
{
    private readonly ConcurrentDictionary<string, ProcessorInstance> _processors = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcessorEngine> _logger;
    private readonly IMessageLogger _messageLogger;
    private readonly IProcessorConfigurationService _configurationService;

    public ProcessorEngine(
        IServiceProvider serviceProvider, 
        ILogger<ProcessorEngine> logger, 
        IMessageLogger messageLogger,
        IProcessorConfigurationService configurationService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _messageLogger = messageLogger;
        _configurationService = configurationService;
        
        // Subscribe to configuration changes for hot reload
        _configurationService.ConfigurationChanged += OnConfigurationChanged;
    }

    public async Task StartAsync(string processorName)
    {
        if (_processors.TryGetValue(processorName, out var existingProcessor))
        {
            if (existingProcessor.IsRunning)
            {
                _logger.LogWarning("Processor {ProcessorName} is already running", processorName);
                return;
            }
        }

        var config = await _configurationService.GetConfigurationAsync(processorName);
        if (config == null)
        {
            _logger.LogError("Configuration not found for processor {ProcessorName}", processorName);
            return;
        }

        var processor = new ProcessorInstance(config, _serviceProvider, _logger, _messageLogger);
        _processors.AddOrUpdate(processorName, processor, (key, old) => processor);

        await processor.StartAsync();
        _logger.LogInformation("Started processor {ProcessorName}", processorName);
    }

    public async Task StopAsync(string processorName)
    {
        if (_processors.TryGetValue(processorName, out var processor))
        {
            await processor.StopAsync();
            _logger.LogInformation("Stopped processor {ProcessorName}", processorName);
        }
    }

    public async Task<ProcessorStatistics> GetStatisticsAsync(string processorName)
    {
        if (_processors.TryGetValue(processorName, out var processor))
        {
            return await processor.GetStatisticsAsync();
        }

        // Check if the processor is configured but not started
        var config = await _configurationService.GetConfigurationAsync(processorName);
        if (config != null)
        {
            return new ProcessorStatistics
            {
                ProcessorName = processorName,
                IsRunning = false,
                MessagesPerMinute = 0,
                PendingMessages = 0,
                ErrorCount = 0,
                LastUpdated = DateTime.UtcNow,
                Uptime = TimeSpan.Zero,
                TotalProcessed = 0,
                Status = "Not Started"
            };
        }

        return new ProcessorStatistics
        {
            ProcessorName = processorName,
            IsRunning = false,
            MessagesPerMinute = 0,
            PendingMessages = 0,
            ErrorCount = 0,
            LastUpdated = DateTime.UtcNow,
            Uptime = TimeSpan.Zero,
            TotalProcessed = 0,
            Status = "Not Found"
        };
    }

    public async Task<IEnumerable<ProcessorStatistics>> GetAllStatisticsAsync()
    {
        // Get all configured processors
        var configurations = await _configurationService.GetAllConfigurationsAsync();
        var allStatistics = new List<ProcessorStatistics>();

        foreach (var config in configurations)
        {
            if (_processors.TryGetValue(config.Name, out var processor))
            {
                // Get statistics from running/instantiated processor
                var stats = await processor.GetStatisticsAsync();
                allStatistics.Add(stats);
            }
            else
            {
                // Create default statistics for processors that haven't been started
                var defaultStats = new ProcessorStatistics
                {
                    ProcessorName = config.Name,
                    IsRunning = false,
                    MessagesPerMinute = 0,
                    PendingMessages = 0,
                    ErrorCount = 0,
                    LastUpdated = DateTime.UtcNow,
                    Uptime = TimeSpan.Zero,
                    TotalProcessed = 0,
                    Status = "Not Started"
                };
                allStatistics.Add(defaultStats);
            }
        }

        return allStatistics;
    }

    public bool IsRunning(string processorName)
    {
        return _processors.TryGetValue(processorName, out var processor) && processor.IsRunning;
    }

    public async Task<IEnumerable<string>> GetConfiguredProcessorNamesAsync()
    {
        var configurations = await _configurationService.GetAllConfigurationsAsync();
        return configurations.Select(c => c.Name);
    }

    public async Task StartAllAutoStartProcessorsAsync()
    {
        var configurations = await _configurationService.GetAllConfigurationsAsync();
        var autoStartProcessors = configurations.Where(c => c.AutoStart);
        
        foreach (var config in autoStartProcessors)
        {
            try
            {
                await StartAsync(config.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-start processor {ProcessorName}", config.Name);
            }
        }
    }

    private async void OnConfigurationChanged(object? sender, ProcessorConfigurationChangedEventArgs e)
    {
        try
        {
            switch (e.ChangeType)
            {
                case ConfigurationChangeType.Updated:
                    // Restart processor with new configuration if it's currently running
                    if (_processors.TryGetValue(e.ProcessorName, out var processor) && processor.IsRunning)
                    {
                        _logger.LogInformation("Restarting processor {ProcessorName} due to configuration change", e.ProcessorName);
                        await StopAsync(e.ProcessorName);
                        await StartAsync(e.ProcessorName);
                    }
                    break;
                    
                case ConfigurationChangeType.Deleted:
                    // Stop and remove processor
                    if (_processors.TryGetValue(e.ProcessorName, out var deletedProcessor))
                    {
                        _logger.LogInformation("Stopping processor {ProcessorName} due to configuration deletion", e.ProcessorName);
                        await StopAsync(e.ProcessorName);
                        _processors.TryRemove(e.ProcessorName, out _);
                    }
                    break;
                    
                case ConfigurationChangeType.Added:
                    // Auto-start if configured
                    if (e.Configuration?.AutoStart == true)
                    {
                        _logger.LogInformation("Auto-starting new processor {ProcessorName}", e.ProcessorName);
                        await StartAsync(e.ProcessorName);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling configuration change for processor {ProcessorName}", e.ProcessorName);
        }
    }
}

internal class ProcessorInstance
{
    private readonly ProcessorConfig _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IMessageLogger _messageLogger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ProcessorStatistics _statistics;
    private readonly DateTime _startTime;
    private Task? _processingTask;

    public bool IsRunning { get; private set; }

    public ProcessorInstance(ProcessorConfig config, IServiceProvider serviceProvider, ILogger logger, IMessageLogger messageLogger)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _messageLogger = messageLogger;
        _startTime = DateTime.UtcNow;
        _statistics = new ProcessorStatistics
        {
            ProcessorName = config.Name,
            Status = "Stopped"
        };
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        _statistics.IsRunning = true;
        _statistics.Status = "Running";

        _processingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cancellationTokenSource.Cancel();
        if (_processingTask != null)
        {
            await _processingTask;
        }

        IsRunning = false;
        _statistics.IsRunning = false;
        _statistics.Status = "Stopped";
    }

    public async Task<ProcessorStatistics> GetStatisticsAsync()
    {
        _statistics.LastUpdated = DateTime.UtcNow;
        _statistics.Uptime = _statistics.IsRunning ? DateTime.UtcNow - _startTime : TimeSpan.Zero;
        
        await _messageLogger.LogStatisticsAsync(_statistics);
        return _statistics;
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var messageSource = _serviceProvider.GetService<IMessageSource>();
        var messageProcessor = CreateMessageProcessor();
        var messagePublisher = _serviceProvider.GetService<IMessagePublisher>();

        if (messageSource == null || messageProcessor == null || messagePublisher == null)
        {
            _logger.LogError("Required services not found for processor {ProcessorName}", _config.Name);
            return;
        }

        var lastMinuteProcessed = new Queue<DateTime>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await messageSource.PollMessagesAsync<object>(_config.InputTopic, 10);
                _statistics.PendingMessages = messages.Count();

                var semaphore = new SemaphoreSlim(_config.MaxConcurrency);
                var tasks = messages.Select(async message =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await ProcessSingleMessageAsync(message, messageProcessor, messagePublisher, messageSource);
                        
                        lock (lastMinuteProcessed)
                        {
                            lastMinuteProcessed.Enqueue(DateTime.UtcNow);
                            _statistics.TotalProcessed++;
                            
                            // Remove messages older than 1 minute
                            while (lastMinuteProcessed.Count > 0 && 
                                   DateTime.UtcNow - lastMinuteProcessed.Peek() > TimeSpan.FromMinutes(1))
                            {
                                lastMinuteProcessed.Dequeue();
                            }
                            
                            _statistics.MessagesPerMinute = lastMinuteProcessed.Count;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
                await Task.Delay(_config.PollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in processing loop for {ProcessorName}", _config.Name);
                _statistics.ErrorCount++;
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    private async Task ProcessSingleMessageAsync<T>(
        ProcessorMessage<T> message,
        IMessageProcessor<T> processor,
        IMessagePublisher publisher,
        IMessageSource source)
    {
        try
        {
            await _messageLogger.LogMessageAsync(message, "Processing");

            var result = await processor.ProcessAsync(message);

            if (result.Success)
            {
                message.MarkProcessingComplete();
                await _messageLogger.LogMessageAsync(message, "Completed");

                // Publish output messages
                if (result.OutputMessages != null && result.OutputMessages.Any())
                {
                    foreach (var outputTopic in _config.OutputTopics)
                    {
                        await publisher.PublishBatchAsync(result.OutputMessages, outputTopic);
                    }
                }

                await source.AcknowledgeMessageAsync(message.Id);
            }
            else
            {
                _statistics.ErrorCount++;
                await _messageLogger.LogMessageAsync(message, "Failed", result.ErrorMessage);
                _logger.LogError("Failed to process message {MessageId}: {Error}", message.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _statistics.ErrorCount++;
            await _messageLogger.LogMessageAsync(message, "Error", ex.Message);
            _logger.LogError(ex, "Exception processing message {MessageId}", message.Id);
        }
    }

    private IMessageProcessor<object> CreateMessageProcessor()
    {
        // This would typically use reflection or factory pattern to create the appropriate processor
        // For demo, returning a default processor
        return new DefaultMessageProcessor();
    }
}

public class DefaultMessageProcessor : IMessageProcessor<object>
{
    public async Task<ProcessorResult<object>> ProcessAsync(ProcessorMessage<object> message)
    {
        // Default implementation - just passes through the message
        await Task.Delay(100); // Simulate some processing
        return ProcessorResult<object>.SuccessResult(message);
    }
}