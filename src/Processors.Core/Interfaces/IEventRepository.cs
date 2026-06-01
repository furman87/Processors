using Processors.Core.Models;

namespace Processors.Core.Interfaces;

public interface IEventRepository
{
    /// <summary>Fetch the next batch of events for a topic after a given offset.</summary>
    Task<IReadOnlyList<EventRecord>> GetNextBatchAsync(
        string topic, long afterId, int batchSize, CancellationToken cancellationToken);

    /// <summary>Returns the highest event ID currently in the log for a topic (0 if none).</summary>
    Task<long> GetLatestIdAsync(string topic, CancellationToken cancellationToken);

    /// <summary>Insert a single event. Returns the assigned ID.</summary>
    Task<long> InsertAsync(
        string topic,
        string payloadJson,
        string? partitionKey = null,
        string? headersJson = null,
        DateTime? eventTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>Insert multiple events in one transaction. Returns assigned IDs in order.</summary>
    Task<IReadOnlyList<long>> InsertBatchAsync(
        IEnumerable<EventInsertRequest> requests,
        CancellationToken cancellationToken);
}

public sealed class EventInsertRequest
{
    public string Topic { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string? PartitionKey { get; init; }
    public string? HeadersJson { get; init; }
    public DateTime? EventTime { get; init; }
}
