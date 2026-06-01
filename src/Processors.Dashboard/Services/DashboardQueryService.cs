using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Dashboard.Services;

/// <summary>
/// Aggregates data from multiple DB tables to build the dashboard view:
/// heartbeats (state/metrics) + consumer_offsets (position) + event_log (lag).
/// </summary>
public sealed class DashboardQueryService(
    IHeartbeatRepository heartbeatRepo,
    IOffsetRepository offsetRepo,
    IEventRepository eventRepo,
    IDeadLetterRepository deadLetterRepo)
{
    /// <summary>Returns one <see cref="ProcessorStatusDto"/> per known processor/consumer-group pair.</summary>
    public async Task<IReadOnlyList<ProcessorStatusDto>> GetAllStatusesAsync(CancellationToken ct = default)
    {
        var heartbeats = await heartbeatRepo.GetAllAsync(ct);
        var offsets    = await offsetRepo.GetAllOffsetsAsync(ct);

        // Build a lookup of latest event IDs per topic (one DB call per unique topic)
        var topics    = heartbeats.Select(h => h.Topic).Distinct().ToList();
        var latestIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var topic in topics)
            latestIds[topic] = await eventRepo.GetLatestIdAsync(topic, ct);

        // Group heartbeats by (processor_name, consumer_group) and pick the most recent instance
        var grouped = heartbeats
            .GroupBy(h => (h.ProcessorName, h.ConsumerGroup))
            .Select(g =>
            {
                var latest = g.OrderByDescending(h => h.UpdatedAt).First();
                var offset = offsets.FirstOrDefault(o =>
                    o.ConsumerGroup == latest.ConsumerGroup &&
                    o.Topic == latest.Topic);

                latestIds.TryGetValue(latest.Topic, out var latestId);

                return new ProcessorStatusDto
                {
                    Name            = latest.ProcessorName,
                    Topic           = latest.Topic,
                    ConsumerGroup   = latest.ConsumerGroup,
                    State           = latest.State,
                    InstanceId      = latest.InstanceId,
                    LastProcessedId = offset?.LastProcessedId ?? 0,
                    LatestEventId   = latestId,
                    ActiveThreads   = latest.ActiveThreads,
                    MaxThreads      = latest.MaxThreads,
                    EventsProcessed = latest.EventsProcessed,
                    ErrorCount      = latest.ErrorCount,
                    LastHeartbeat   = latest.UpdatedAt,
                    LastError       = latest.LastError,
                };
            })
            .OrderBy(s => s.Name)
            .ToList();

        return grouped;
    }

    public async Task<ProcessorStatusDto?> GetStatusAsync(
        string processorName, string consumerGroup, CancellationToken ct = default)
    {
        var all = await GetAllStatusesAsync(ct);
        return all.FirstOrDefault(s =>
            s.Name.Equals(processorName, StringComparison.OrdinalIgnoreCase) &&
            s.ConsumerGroup.Equals(consumerGroup, StringComparison.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<DeadLetterEvent>> GetDeadLetterAsync(
        string topic, string consumerGroup, int count = 50, CancellationToken ct = default)
        => deadLetterRepo.GetRecentAsync(topic, consumerGroup, count, ct);
}
