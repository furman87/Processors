namespace Processors.Core.Interfaces;

/// <summary>Writes events into the event_log table.</summary>
public interface IEventProducer
{
    /// <summary>Produce a single event. Returns the assigned event ID (offset).</summary>
    Task<long> ProduceAsync(
        string topic,
        object payload,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>Produce multiple events in a single transaction. Returns assigned IDs in order.</summary>
    Task<IReadOnlyList<long>> ProduceBatchAsync(
        IEnumerable<ProduceRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed class ProduceRequest
{
    public string Topic { get; init; } = string.Empty;
    public required object Payload { get; init; }
    public string? PartitionKey { get; init; }
    public IDictionary<string, string>? Headers { get; init; }
    public DateTimeOffset? EventTime { get; init; }
}
