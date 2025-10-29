using Dapper;
using Npgsql;
using Processors.Interfaces;
using Processors.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Processors.MessageSources;

public class DatabaseMessageSource : IMessageSource
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMessageSource> _logger;

    public DatabaseMessageSource(string connectionString, ILogger<DatabaseMessageSource> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IEnumerable<ProcessorMessage<T>>> PollMessagesAsync<T>(string topic, int maxCount = 10)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT id, topic, datetime_created, datetime_received, datetime_processing_complete, 
                       datetime_sent_to_next, payload_type, payload_json, status
                FROM processor_messages 
                WHERE topic = @Topic AND status = 'Pending'
                ORDER BY datetime_created 
                LIMIT @MaxCount";

            var messages = await connection.QueryAsync<ProcessorMessageMetadata>(sql, new { Topic = topic, MaxCount = maxCount });
            
            var result = new List<ProcessorMessage<T>>();
            foreach (var metadata in messages)
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<T>(metadata.PayloadJson);
                    var message = new ProcessorMessage<T>
                    {
                        Id = metadata.Id,
                        Topic = metadata.Topic,
                        DateTimeCreated = metadata.DateTimeCreated,
                        DateTimeReceived = metadata.DateTimeReceived,
                        DateTimeProcessingComplete = metadata.DateTimeProcessingComplete,
                        DateTimeSentToNext = metadata.DateTimeSentToNext,
                        Payload = payload
                    };
                    message.MarkReceived();
                    result.Add(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message {MessageId}", metadata.Id);
                }
            }

            // Update status to 'Processing' for fetched messages
            if (result.Any())
            {
                var messageIds = result.Select(m => m.Id).ToArray();
                var updateSql = @"
                    UPDATE processor_messages 
                    SET status = 'Processing', datetime_received = @DateTimeReceived 
                    WHERE id = ANY(@MessageIds)";
                
                await connection.ExecuteAsync(updateSql, new { 
                    MessageIds = messageIds, 
                    DateTimeReceived = DateTime.UtcNow 
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll messages from database for topic {Topic}", topic);
            return new List<ProcessorMessage<T>>();
        }
    }

    public async Task AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE processor_messages 
                SET status = 'Completed', datetime_processing_complete = @DateTimeComplete 
                WHERE id = @MessageId";

            await connection.ExecuteAsync(sql, new { 
                MessageId = messageId, 
                DateTimeComplete = DateTime.UtcNow 
            });
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await connection.QuerySingleAsync<int>("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task InsertMessageAsync<T>(ProcessorMessage<T> message)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO processor_messages 
                (id, topic, datetime_created, payload_type, payload_json, status)
                VALUES (@Id, @Topic, @DateTimeCreated, @PayloadType, @PayloadJson, @Status)";

            await connection.ExecuteAsync(sql, new
            {
                message.Id,
                message.Topic,
                message.DateTimeCreated,
                PayloadType = typeof(T).Name,
                PayloadJson = message.PayloadJson,
                Status = "Pending"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert message {MessageId}", message.Id);
            throw;
        }
    }
}