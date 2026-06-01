namespace Processors.Core.Models;

public sealed class ProcessorHeartbeat
{
    public string ProcessorName { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public long EventsProcessed { get; set; }
    public long ErrorCount { get; set; }
    public int ActiveThreads { get; set; }
    public int MaxThreads { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}
