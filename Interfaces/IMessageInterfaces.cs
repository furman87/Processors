using Processors.Models;

namespace Processors.Interfaces;

public interface IMessageSource
{
    Task<IEnumerable<ProcessorMessage<T>>> PollMessagesAsync<T>(string topic, int maxCount = 10);
    Task AcknowledgeMessageAsync(string messageId);
    Task<bool> IsHealthyAsync();
}

public interface IMessageProcessor<T>
{
    Task<ProcessorResult<T>> ProcessAsync(ProcessorMessage<T> message);
}

public interface IMessagePublisher
{
    Task PublishAsync<T>(ProcessorMessage<T> message, string topic);
    Task PublishBatchAsync<T>(IEnumerable<ProcessorMessage<T>> messages, string topic);
}

public interface IProcessorEngine
{
    Task StartAsync(string processorName);
    Task StopAsync(string processorName);
    Task<ProcessorStatistics> GetStatisticsAsync(string processorName);
    Task<IEnumerable<ProcessorStatistics>> GetAllStatisticsAsync();
    Task<IEnumerable<string>> GetConfiguredProcessorNamesAsync();
    Task StartAllAutoStartProcessorsAsync();
    bool IsRunning(string processorName);
}

public interface IMessageLogger
{
    Task LogMessageAsync<T>(ProcessorMessage<T> message, string status, string? errorMessage = null);
    Task LogStatisticsAsync(ProcessorStatistics statistics);
}

public class ProcessorResult<T>
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IEnumerable<ProcessorMessage<object>>? OutputMessages { get; set; }
    public ProcessorMessage<T>? ModifiedMessage { get; set; }
    
    public static ProcessorResult<T> SuccessResult(ProcessorMessage<T>? modifiedMessage = null, IEnumerable<ProcessorMessage<object>>? outputMessages = null)
    {
        return new ProcessorResult<T>
        {
            Success = true,
            ModifiedMessage = modifiedMessage,
            OutputMessages = outputMessages ?? new List<ProcessorMessage<object>>()
        };
    }
    
    public static ProcessorResult<T> ErrorResult(string errorMessage)
    {
        return new ProcessorResult<T>
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}