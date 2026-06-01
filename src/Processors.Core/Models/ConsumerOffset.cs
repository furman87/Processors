namespace Processors.Core.Models;

public sealed class ConsumerOffset
{
    public string ConsumerGroup { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public long LastProcessedId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
