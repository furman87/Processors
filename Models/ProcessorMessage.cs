using Newtonsoft.Json;

namespace Processors.Models;

public class ProcessorMessage<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = string.Empty;
    public DateTime DateTimeCreated { get; set; }
    public DateTime? DateTimeReceived { get; set; }
    public DateTime? DateTimeProcessingComplete { get; set; }
    public DateTime? DateTimeSentToNext { get; set; }
    public T? Payload { get; set; }
    
    [JsonIgnore]
    public string PayloadJson => JsonConvert.SerializeObject(Payload);
    
    public ProcessorMessage()
    {
        DateTimeCreated = DateTime.UtcNow;
    }
    
    public void MarkReceived()
    {
        DateTimeReceived = DateTime.UtcNow;
    }
    
    public void MarkProcessingComplete()
    {
        DateTimeProcessingComplete = DateTime.UtcNow;
    }
    
    public void MarkSentToNext()
    {
        DateTimeSentToNext = DateTime.UtcNow;
    }
}

public class ProcessorMessageMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime DateTimeCreated { get; set; }
    public DateTime? DateTimeReceived { get; set; }
    public DateTime? DateTimeProcessingComplete { get; set; }
    public DateTime? DateTimeSentToNext { get; set; }
    public string PayloadType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
}