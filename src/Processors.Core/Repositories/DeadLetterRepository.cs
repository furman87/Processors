using Dapper;
using Npgsql;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Repositories;

public sealed class DeadLetterRepository(string connectionString) : IDeadLetterRepository
{
    public async Task InsertAsync(DeadLetterEvent evt, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(
            """
            INSERT INTO dead_letter_events
                (original_event_id, topic, consumer_group, processor_name,
                 attempts, last_error, payload, headers)
            VALUES
                (@OriginalEventId, @Topic, @ConsumerGroup, @ProcessorName,
                 @Attempts, @LastError, @Payload::jsonb, @Headers::jsonb)
            """,
            new
            {
                evt.OriginalEventId,
                evt.Topic,
                evt.ConsumerGroup,
                evt.ProcessorName,
                evt.Attempts,
                evt.LastError,
                evt.Payload,
                evt.Headers,
            });
    }

    public async Task<IReadOnlyList<DeadLetterEvent>> GetRecentAsync(
        string topic, string consumerGroup, int count = 100, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<DeadLetterEvent>(
            """
            SELECT id, original_event_id, topic, consumer_group, processor_name,
                   failed_at, attempts, last_error,
                   payload::text AS payload,
                   headers::text AS headers
            FROM   dead_letter_events
            WHERE  topic          = @Topic
              AND  consumer_group = @ConsumerGroup
            ORDER  BY failed_at DESC
            LIMIT  @Count
            """,
            new { Topic = topic, ConsumerGroup = consumerGroup, Count = count });

        return rows.AsList();
    }
}
