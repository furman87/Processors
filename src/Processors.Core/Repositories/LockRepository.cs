using Dapper;
using Npgsql;
using Processors.Core.Interfaces;

namespace Processors.Core.Repositories;

public sealed class LockRepository(string connectionString) : ILockRepository
{
    public async Task<bool> TryAcquireOrRenewAsync(
        string consumerGroup,
        string topic,
        string lockId,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        // Remove expired lock OR our own lock (so we can renew)
        await conn.ExecuteAsync(
            """
            DELETE FROM consumer_locks
            WHERE consumer_group = @ConsumerGroup
              AND topic          = @Topic
              AND (locked_until < now() OR lock_id = @LockId)
            """,
            new { ConsumerGroup = consumerGroup, Topic = topic, LockId = lockId }, tx);

        // Attempt insert; ON CONFLICT DO NOTHING means someone else already holds a valid lock
        var inserted = await conn.ExecuteAsync(
            """
            INSERT INTO consumer_locks (consumer_group, topic, lock_id, locked_until)
            VALUES (@ConsumerGroup, @Topic, @LockId,
                    now() + make_interval(secs => @Duration))
            ON CONFLICT DO NOTHING
            """,
            new { ConsumerGroup = consumerGroup, Topic = topic, LockId = lockId, Duration = durationSeconds },
            tx);

        await tx.CommitAsync(cancellationToken);
        return inserted > 0;
    }

    public async Task ReleaseAsync(
        string consumerGroup, string topic, string lockId, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(
            """
            DELETE FROM consumer_locks
            WHERE consumer_group = @ConsumerGroup
              AND topic          = @Topic
              AND lock_id        = @LockId
            """,
            new { ConsumerGroup = consumerGroup, Topic = topic, LockId = lockId });
    }
}
