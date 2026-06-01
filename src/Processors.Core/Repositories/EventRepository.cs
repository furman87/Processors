using Dapper;
using Npgsql;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Repositories;

public sealed class EventRepository(string connectionString) : IEventRepository
{
    public async Task<IReadOnlyList<EventRecord>> GetNextBatchAsync(
        string topic, long afterId, int batchSize, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        const string sql = """
            SELECT id,
                   topic,
                   partition_key,
                   event_time,
                   created_at,
                   payload::text  AS payload,
                   headers::text  AS headers
            FROM   event_log
            WHERE  topic = @Topic
              AND  id > @AfterId
            ORDER  BY id
            LIMIT  @BatchSize
            """;

        var rows = await conn.QueryAsync<EventRecord>(
            sql, new { Topic = topic, AfterId = afterId, BatchSize = batchSize });

        return rows.AsList();
    }

    public async Task<long> GetLatestIdAsync(string topic, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<long>(
            "SELECT COALESCE(MAX(id), 0) FROM event_log WHERE topic = @Topic",
            new { Topic = topic });
    }

    public async Task<long> InsertAsync(
        string topic,
        string payloadJson,
        string? partitionKey = null,
        string? headersJson = null,
        DateTime? eventTime = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        const string sql = """
            INSERT INTO event_log (topic, partition_key, event_time, payload, headers)
            VALUES (@Topic, @PartitionKey, @EventTime, @Payload::jsonb, @Headers::jsonb)
            RETURNING id
            """;

        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            Topic        = topic,
            PartitionKey = partitionKey,
            EventTime    = DateTime.SpecifyKind(eventTime ?? DateTime.UtcNow, DateTimeKind.Utc),
            Payload      = payloadJson,
            Headers      = headersJson,
        });
    }

    public async Task<IReadOnlyList<long>> InsertBatchAsync(
        IEnumerable<EventInsertRequest> requests,
        CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO event_log (topic, partition_key, event_time, payload, headers)
            VALUES (@Topic, @PartitionKey, @EventTime, @Payload::jsonb, @Headers::jsonb)
            RETURNING id
            """;

        var ids = new List<long>();
        foreach (var req in requests)
        {
            var id = await conn.ExecuteScalarAsync<long>(sql, new
            {
                req.Topic,
                req.PartitionKey,
                EventTime = DateTime.SpecifyKind(req.EventTime ?? DateTime.UtcNow, DateTimeKind.Utc),
                Payload   = req.PayloadJson,
                Headers   = req.HeadersJson,
            }, tx);
            ids.Add(id);
        }

        await tx.CommitAsync(cancellationToken);
        return ids;
    }
}
