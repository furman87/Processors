namespace Processors.Core.Models;

public enum ProcessorState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted,
}

/// <summary>Live snapshot of a processor's runtime status (in-process view).</summary>
public sealed class ProcessorStatus
{
    public string Name { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string ConsumerGroup { get; init; } = string.Empty;
    public ProcessorState State { get; init; }
    public long LastProcessedId { get; init; }
    public long LatestEventId { get; init; }

    /// <summary>Number of events that have not yet been processed (LatestEventId - LastProcessedId).</summary>
    public long Lag => Math.Max(0, LatestEventId - LastProcessedId);

    public int ActiveThreads { get; init; }
    public int MaxThreads { get; init; }
    public long EventsProcessed { get; init; }
    public long ErrorCount { get; init; }
    public DateTime? LastProcessedAt { get; init; }
    public string? LastError { get; init; }
}

/// <summary>Aggregated view built by the dashboard from DB tables.</summary>
public sealed class ProcessorStatusDto
{
    public string Name { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string ConsumerGroup { get; init; } = string.Empty;
    public string State { get; init; } = "Offline";
    public string InstanceId { get; init; } = string.Empty;
    public long LastProcessedId { get; init; }
    public long LatestEventId { get; init; }
    public long Lag => Math.Max(0, LatestEventId - LastProcessedId);
    public int ActiveThreads { get; init; }
    public int MaxThreads { get; init; }
    public long EventsProcessed { get; init; }
    public long ErrorCount { get; init; }
    public DateTime? LastHeartbeat { get; init; }
    public string? LastError { get; init; }

    /// <summary>True when a heartbeat has been received within the last 30 seconds.</summary>
    public bool IsOnline => LastHeartbeat.HasValue
        && (DateTime.UtcNow - LastHeartbeat.Value).TotalSeconds < 30;
}
