using Processors.Core.Models;

namespace Processors.Core.Interfaces;

public interface IOffsetRepository
{
    /// <summary>Returns the last committed event ID for the consumer group + topic (0 if none).</summary>
    Task<long> GetOffsetAsync(string consumerGroup, string topic, CancellationToken cancellationToken);

    /// <summary>Atomically upsert the offset (only advances; never moves backward).</summary>
    Task CommitOffsetAsync(string consumerGroup, string topic, long lastProcessedId, CancellationToken cancellationToken);

    /// <summary>Returns all known offsets (used by the dashboard).</summary>
    Task<IReadOnlyList<ConsumerOffset>> GetAllOffsetsAsync(CancellationToken cancellationToken);
}
