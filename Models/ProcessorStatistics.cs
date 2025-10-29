namespace Processors.Models;

public class ProcessorStatistics
{
    public string ProcessorName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public double MessagesPerMinute { get; set; }
    public int PendingMessages { get; set; }
    public int ErrorCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; set; }
    public long TotalProcessed { get; set; }
    public string Status { get; set; } = "Stopped";
}

public class ProcessorConfig
{
    public string Name { get; set; } = string.Empty;
    public string InputTopic { get; set; } = string.Empty;
    public List<string> OutputTopics { get; set; } = new();
    public string ProcessorType { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    
    // Property for JSON serialization/deserialization
    public int PollingIntervalSeconds 
    { 
        get => (int)PollingInterval.TotalSeconds;
        set => PollingInterval = TimeSpan.FromSeconds(value);
    }
    
    public bool AutoStart { get; set; } = true;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}