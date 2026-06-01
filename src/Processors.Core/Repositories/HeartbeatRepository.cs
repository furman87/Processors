using Dapper;
using Npgsql;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Repositories;

public sealed class HeartbeatRepository(string connectionString) : IHeartbeatRepository
{
    public async Task UpsertAsync(ProcessorHeartbeat hb, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(
            """
            INSERT INTO processor_heartbeats
                (processor_name, consumer_group, instance_id, topic, state,
                 events_processed, error_count, active_threads, max_threads, last_error, updated_at)
            VALUES
                (@ProcessorName, @ConsumerGroup, @InstanceId, @Topic, @State,
                 @EventsProcessed, @ErrorCount, @ActiveThreads, @MaxThreads, @LastError, now())
            ON CONFLICT (processor_name, consumer_group, instance_id) DO UPDATE
                SET topic            = EXCLUDED.topic,
                    state            = EXCLUDED.state,
                    events_processed = EXCLUDED.events_processed,
                    error_count      = EXCLUDED.error_count,
                    active_threads   = EXCLUDED.active_threads,
                    max_threads      = EXCLUDED.max_threads,
                    last_error       = EXCLUDED.last_error,
                    updated_at       = EXCLUDED.updated_at
            """,
            new
            {
                hb.ProcessorName,
                hb.ConsumerGroup,
                hb.InstanceId,
                hb.Topic,
                hb.State,
                hb.EventsProcessed,
                hb.ErrorCount,
                hb.ActiveThreads,
                hb.MaxThreads,
                hb.LastError,
            });
    }

    public async Task<IReadOnlyList<ProcessorHeartbeat>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<ProcessorHeartbeat>(
            "SELECT * FROM processor_heartbeats ORDER BY processor_name, consumer_group");
        return rows.AsList();
    }

    public async Task<IReadOnlyList<ProcessorHeartbeat>> GetByProcessorAsync(
        string processorName, string consumerGroup, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<ProcessorHeartbeat>(
            """
            SELECT * FROM processor_heartbeats
            WHERE processor_name = @ProcessorName AND consumer_group = @ConsumerGroup
            """,
            new { ProcessorName = processorName, ConsumerGroup = consumerGroup });
        return rows.AsList();
    }
}
