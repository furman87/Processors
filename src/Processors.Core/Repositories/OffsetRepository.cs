using Dapper;
using Npgsql;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Repositories;

public sealed class OffsetRepository(string connectionString) : IOffsetRepository
{
    public async Task<long> GetOffsetAsync(
        string consumerGroup, string topic, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<long>(
            """
            SELECT COALESCE(last_processed_id, 0)
            FROM   consumer_offsets
            WHERE  consumer_group = @ConsumerGroup
              AND  topic = @Topic
            """,
            new { ConsumerGroup = consumerGroup, Topic = topic });
    }

    public async Task CommitOffsetAsync(
        string consumerGroup, string topic, long lastProcessedId, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);

        // Only advance the offset — never move it backward
        await conn.ExecuteAsync(
            """
            INSERT INTO consumer_offsets (consumer_group, topic, last_processed_id, updated_at)
            VALUES (@ConsumerGroup, @Topic, @LastProcessedId, now())
            ON CONFLICT (consumer_group, topic) DO UPDATE
                SET last_processed_id = EXCLUDED.last_processed_id,
                    updated_at        = EXCLUDED.updated_at
                WHERE consumer_offsets.last_processed_id < EXCLUDED.last_processed_id
            """,
            new { ConsumerGroup = consumerGroup, Topic = topic, LastProcessedId = lastProcessedId });
    }

    public async Task<IReadOnlyList<ConsumerOffset>> GetAllOffsetsAsync(CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<ConsumerOffset>(
            "SELECT consumer_group, topic, last_processed_id, updated_at FROM consumer_offsets");
        return rows.AsList();
    }
}
