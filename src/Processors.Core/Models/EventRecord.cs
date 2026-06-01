namespace Processors.Core.Models;

/// <summary>Raw row returned by Dapper from the event_log table.</summary>
public sealed class EventRecord
{
    public long Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? PartitionKey { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>JSON string of the payload (read via ::text cast in SQL).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>JSON string of the headers (read via ::text cast in SQL). Null if no headers.</summary>
    public string? Headers { get; set; }
}
