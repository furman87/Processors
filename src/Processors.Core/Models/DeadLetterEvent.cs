namespace Processors.Core.Models;

public sealed class DeadLetterEvent
{
    public long Id { get; set; }
    public long OriginalEventId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public string ProcessorName { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int Attempts { get; set; }
    public string LastError { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Headers { get; set; }
}
