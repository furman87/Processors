namespace Processors.Core.Configuration;

public sealed class ProcessorConfig
{
    public string Name { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;

    /// <summary>Start this processor automatically when the worker host starts.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Maximum number of concurrent event-processing tasks per batch.</summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>Number of events fetched per polling iteration.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Milliseconds to sleep when no new events are available.</summary>
    public int PollingIntervalMs { get; set; } = 100;

    /// <summary>When true, events in a batch are processed sequentially to preserve order.</summary>
    public bool OrderingRequired { get; set; } = false;

    /// <summary>Maximum number of retry attempts before an event is dead-lettered.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay (ms) for exponential-backoff retries.</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>Distributed lock lease duration in seconds.</summary>
    public int LockTimeoutSeconds { get; set; } = 30;

    /// <summary>How often (seconds) the runner writes a heartbeat row.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 5;
}

public sealed class EventLogOptions
{
    public const string SectionName = "EventLog";

    /// <summary>Fallback connection string (can also use ConnectionStrings:Postgres).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public List<ProcessorConfig> Processors { get; set; } = [];
}
