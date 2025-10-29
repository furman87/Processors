using Processors.Interfaces;
using Processors.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Processors.MessageProcessors;

public class EmailMessageProcessor : IMessageProcessor<EmailMessage>
{
    private readonly ILogger<EmailMessageProcessor> _logger;

    public EmailMessageProcessor(ILogger<EmailMessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<EmailMessage>> ProcessAsync(ProcessorMessage<EmailMessage> message)
    {
        try
        {
            if (message.Payload == null)
            {
                return ProcessorResult<EmailMessage>.ErrorResult("Email payload is null");
            }

            // Simulate email processing
            await Task.Delay(Random.Shared.Next(100, 500));
            
            _logger.LogInformation("Processing email to {To} with subject {Subject}", 
                message.Payload.To, message.Payload.Subject);

            // Simulate potential failure
            if (string.IsNullOrWhiteSpace(message.Payload.To))
            {
                return ProcessorResult<EmailMessage>.ErrorResult("Email recipient is required");
            }

            // Create notification message
            var notification = new ProcessorMessage<object>
            {
                Topic = "notification_queue",
                Payload = new NotificationMessage
                {
                    Type = "EmailSent",
                    Message = $"Email sent to {message.Payload.To}",
                    Timestamp = DateTime.UtcNow
                }
            };

            var outputMessages = new List<ProcessorMessage<object>> { notification };

            return ProcessorResult<EmailMessage>.SuccessResult(message, outputMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email message {MessageId}", message.Id);
            return ProcessorResult<EmailMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
}

public class DataMessageProcessor : IMessageProcessor<DataMessage>
{
    private readonly ILogger<DataMessageProcessor> _logger;

    public DataMessageProcessor(ILogger<DataMessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<DataMessage>> ProcessAsync(ProcessorMessage<DataMessage> message)
    {
        try
        {
            if (message.Payload == null)
            {
                return ProcessorResult<DataMessage>.ErrorResult("Data payload is null");
            }

            // Simulate data processing
            await Task.Delay(Random.Shared.Next(200, 800));
            
            _logger.LogInformation("Processing data message of type {DataType} with {RecordCount} records", 
                message.Payload.DataType, message.Payload.Records?.Count ?? 0);

            var outputMessages = new List<ProcessorMessage<object>>();

            // Create analytics message
            var analyticsMessage = new ProcessorMessage<object>
            {
                Topic = "analytics_queue",
                Payload = new AnalyticsMessage
                {
                    Source = message.Payload.DataType,
                    RecordCount = message.Payload.Records?.Count ?? 0,
                    ProcessedAt = DateTime.UtcNow,
                    Metrics = new Dictionary<string, object>
                    {
                        ["processing_time_ms"] = Random.Shared.Next(200, 800),
                        ["success_rate"] = 0.95
                    }
                }
            };

            // Create reporting message
            var reportingMessage = new ProcessorMessage<object>
            {
                Topic = "reporting_queue",
                Payload = new ReportingMessage
                {
                    ReportType = "DataProcessingSummary",
                    Data = new { 
                        DataType = message.Payload.DataType,
                        ProcessedRecords = message.Payload.Records?.Count ?? 0,
                        ProcessedAt = DateTime.UtcNow
                    },
                    GeneratedAt = DateTime.UtcNow
                }
            };

            outputMessages.Add(analyticsMessage);
            outputMessages.Add(reportingMessage);

            return ProcessorResult<DataMessage>.SuccessResult(message, outputMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data message {MessageId}", message.Id);
            return ProcessorResult<DataMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
}

public class NotificationMessageProcessor : IMessageProcessor<NotificationMessage>
{
    private readonly ILogger<NotificationMessageProcessor> _logger;

    public NotificationMessageProcessor(ILogger<NotificationMessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<NotificationMessage>> ProcessAsync(ProcessorMessage<NotificationMessage> message)
    {
        try
        {
            if (message.Payload == null)
            {
                return ProcessorResult<NotificationMessage>.ErrorResult("Notification payload is null");
            }

            // Simulate notification sending
            await Task.Delay(Random.Shared.Next(50, 200));
            
            _logger.LogInformation("Sending notification of type {Type}: {Message}", 
                message.Payload.Type, message.Payload.Message);

            // No output messages for notification processor (end of chain)
            return ProcessorResult<NotificationMessage>.SuccessResult(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification message {MessageId}", message.Id);
            return ProcessorResult<NotificationMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
}

public class AnalyticsMessageProcessor : IMessageProcessor<AnalyticsMessage>
{
    private readonly ILogger<AnalyticsMessageProcessor> _logger;

    public AnalyticsMessageProcessor(ILogger<AnalyticsMessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<AnalyticsMessage>> ProcessAsync(ProcessorMessage<AnalyticsMessage> message)
    {
        try
        {
            if (message.Payload == null)
            {
                return ProcessorResult<AnalyticsMessage>.ErrorResult("Analytics payload is null");
            }

            // Simulate analytics processing
            await Task.Delay(Random.Shared.Next(300, 1000));
            
            _logger.LogInformation("Processing analytics from {Source} with {RecordCount} records", 
                message.Payload.Source, message.Payload.RecordCount);

            // Create reporting message
            var reportingMessage = new ProcessorMessage<object>
            {
                Topic = "reporting_queue",
                Payload = new ReportingMessage
                {
                    ReportType = "AnalyticsReport",
                    Data = new { 
                        Source = message.Payload.Source,
                        RecordCount = message.Payload.RecordCount,
                        ProcessedAt = message.Payload.ProcessedAt,
                        Metrics = message.Payload.Metrics
                    },
                    GeneratedAt = DateTime.UtcNow
                }
            };

            var outputMessages = new List<ProcessorMessage<object>> { reportingMessage };

            return ProcessorResult<AnalyticsMessage>.SuccessResult(message, outputMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics message {MessageId}", message.Id);
            return ProcessorResult<AnalyticsMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
}

public class ReportingMessageProcessor : IMessageProcessor<ReportingMessage>
{
    private readonly ILogger<ReportingMessageProcessor> _logger;

    public ReportingMessageProcessor(ILogger<ReportingMessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<ReportingMessage>> ProcessAsync(ProcessorMessage<ReportingMessage> message)
    {
        try
        {
            if (message.Payload == null)
            {
                return ProcessorResult<ReportingMessage>.ErrorResult("Reporting payload is null");
            }

            // Simulate report generation
            await Task.Delay(Random.Shared.Next(500, 1500));
            
            _logger.LogInformation("Generating report of type {ReportType}", 
                message.Payload.ReportType);

            // End of processing chain - no output messages
            return ProcessorResult<ReportingMessage>.SuccessResult(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reporting message {MessageId}", message.Id);
            return ProcessorResult<ReportingMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
}

// Message payload models
public class EmailMessage
{
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
}

public class DataMessage
{
    public string DataType { get; set; } = string.Empty;
    public List<Dictionary<string, object>>? Records { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class NotificationMessage
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AnalyticsMessage
{
    public string Source { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public Dictionary<string, object>? Metrics { get; set; }
}

public class ReportingMessage
{
    public string ReportType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime GeneratedAt { get; set; }
}

// Configuration loader
public static class ConfigurationLoader
{
    public static T LoadConfiguration<T>(string filePath)
    {
        try
        {
            var json = System.IO.File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(json, options) ?? throw new Exception("Deserialized configuration is null");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error loading configuration from {filePath}: {ex.Message}", ex);
        }
    }
}

// Processor settings and configurations
public class ProcessorSettings
{
    public Dictionary<string, string> Queues { get; set; } = new();
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public string DeadLetterQueue { get; set; } = string.Empty;
}

public class ProcessorConfiguration
{
    public ProcessorSettings Settings { get; set; } = new();
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
}