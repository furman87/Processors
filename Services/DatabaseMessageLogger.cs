using Dapper;
using Npgsql;
using Processors.Interfaces;
using Processors.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Processors.Services;

public class DatabaseMessageLogger : IMessageLogger
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMessageLogger> _logger;

    public DatabaseMessageLogger(string connectionString, ILogger<DatabaseMessageLogger> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task LogMessageAsync<T>(ProcessorMessage<T> message, string status, string? errorMessage = null)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO message_logs 
                (message_id, topic, status, error_message, payload_type, payload_json, logged_at)
                VALUES (@MessageId, @Topic, @Status, @ErrorMessage, @PayloadType, @PayloadJson, @LoggedAt)
                ON CONFLICT (message_id, logged_at) DO UPDATE SET
                status = EXCLUDED.status,
                error_message = EXCLUDED.error_message";

            await connection.ExecuteAsync(sql, new
            {
                MessageId = message.Id,
                message.Topic,
                Status = status,
                ErrorMessage = errorMessage,
                PayloadType = typeof(T).Name,
                PayloadJson = message.PayloadJson,
                LoggedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log message {MessageId} to database", message.Id);
        }
    }

    public async Task LogStatisticsAsync(ProcessorStatistics statistics)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO processor_statistics 
                (processor_name, is_running, messages_per_minute, pending_messages, error_count, 
                 last_updated, uptime_seconds, total_processed, status)
                VALUES (@ProcessorName, @IsRunning, @MessagesPerMinute, @PendingMessages, @ErrorCount,
                        @LastUpdated, @UptimeSeconds, @TotalProcessed, @Status)";

            await connection.ExecuteAsync(sql, new
            {
                statistics.ProcessorName,
                statistics.IsRunning,
                statistics.MessagesPerMinute,
                statistics.PendingMessages,
                statistics.ErrorCount,
                statistics.LastUpdated,
                UptimeSeconds = (int)statistics.Uptime.TotalSeconds,
                statistics.TotalProcessed,
                statistics.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log statistics for processor {ProcessorName}", statistics.ProcessorName);
        }
    }
}