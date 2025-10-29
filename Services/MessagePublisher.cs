using Processors.Interfaces;
using Processors.Models;
using Processors.MessageSources;
using Microsoft.Extensions.Logging;

namespace Processors.Services;

public class MessagePublisher : IMessagePublisher
{
    private readonly DatabaseMessageSource? _databaseSource;
    private readonly RedisMessageSource? _redisSource;
    private readonly FileSystemMessageSource? _fileSystemSource;
    private readonly HttpApiMessageSource? _httpApiSource;
    private readonly ILogger<MessagePublisher> _logger;

    public MessagePublisher(
        DatabaseMessageSource? databaseSource = null,
        RedisMessageSource? redisSource = null,
        FileSystemMessageSource? fileSystemSource = null,
        HttpApiMessageSource? httpApiSource = null,
        ILogger<MessagePublisher>? logger = null)
    {
        _databaseSource = databaseSource;
        _redisSource = redisSource;
        _fileSystemSource = fileSystemSource;
        _httpApiSource = httpApiSource;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(ProcessorMessage<T> message, string topic)
    {
        message.Topic = topic;
        message.MarkSentToNext();

        var tasks = new List<Task>();

        if (_databaseSource != null)
        {
            tasks.Add(_databaseSource.InsertMessageAsync(message));
        }

        if (_redisSource != null)
        {
            tasks.Add(_redisSource.PublishMessageAsync(message, topic));
        }

        if (_fileSystemSource != null)
        {
            tasks.Add(_fileSystemSource.WriteMessageAsync(message, topic));
        }

        if (_httpApiSource != null)
        {
            tasks.Add(_httpApiSource.PostMessageAsync(message, topic));
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Published message {MessageId} to topic {Topic}", message.Id, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId} to topic {Topic}", message.Id, topic);
            throw;
        }
    }

    public async Task PublishBatchAsync<T>(IEnumerable<ProcessorMessage<T>> messages, string topic)
    {
        var messagesToPublish = messages.ToList();
        foreach (var message in messagesToPublish)
        {
            message.Topic = topic;
            message.MarkSentToNext();
        }

        var tasks = new List<Task>();

        foreach (var message in messagesToPublish)
        {
            tasks.Add(PublishAsync(message, topic));
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Published {Count} messages to topic {Topic}", messagesToPublish.Count, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish batch of {Count} messages to topic {Topic}", messagesToPublish.Count, topic);
            throw;
        }
    }
}