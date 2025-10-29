using StackExchange.Redis;
using Processors.Interfaces;
using Processors.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Processors.MessageSources;

public class RedisMessageSource : IMessageSource
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisMessageSource> _logger;

    public RedisMessageSource(IConnectionMultiplexer redis, ILogger<RedisMessageSource> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<IEnumerable<ProcessorMessage<T>>> PollMessagesAsync<T>(string topic, int maxCount = 10)
    {
        try
        {
            var queueKey = $"queue:{topic}";
            var processingKey = $"processing:{topic}";
            
            var messages = new List<ProcessorMessage<T>>();
            
            for (int i = 0; i < maxCount; i++)
            {
                var messageJson = await _database.ListRightPopLeftPushAsync(queueKey, processingKey);
                if (!messageJson.HasValue) break;

                try
                {
                    var message = JsonConvert.DeserializeObject<ProcessorMessage<T>>(messageJson!);
                    if (message != null)
                    {
                        message.MarkReceived();
                        messages.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message from Redis");
                }
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll messages from Redis for topic {Topic}", topic);
            return new List<ProcessorMessage<T>>();
        }
    }

    public async Task AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            // Remove from processing queue (in real implementation, you might want to store message ID mapping)
            // For simplicity, we'll just log the acknowledgment
            _logger.LogDebug("Message {MessageId} acknowledged", messageId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge message {MessageId}", messageId);
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            await _database.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task PublishMessageAsync<T>(ProcessorMessage<T> message, string topic)
    {
        try
        {
            var queueKey = $"queue:{topic}";
            var messageJson = JsonConvert.SerializeObject(message);
            await _database.ListLeftPushAsync(queueKey, messageJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Redis topic {Topic}", topic);
            throw;
        }
    }
}