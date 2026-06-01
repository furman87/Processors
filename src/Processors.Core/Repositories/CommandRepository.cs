using System.Text.Json;
using Dapper;
using Npgsql;
using Processors.Core.Interfaces;

namespace Processors.Core.Repositories;

public sealed class CommandRepository(string connectionString) : ICommandRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<long> IssueCommandAsync(
        string processorName,
        string consumerGroup,
        string command,
        object? parameters = null,
        string? issuedBy = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var parametersJson = parameters is not null
            ? JsonSerializer.Serialize(parameters, JsonOpts)
            : null;

        return await conn.ExecuteScalarAsync<long>(
            """
            INSERT INTO processor_commands
                (processor_name, consumer_group, command, parameters, issued_by)
            VALUES
                (@ProcessorName, @ConsumerGroup, @Command, @Parameters::jsonb, @IssuedBy)
            RETURNING id
            """,
            new
            {
                ProcessorName = processorName,
                ConsumerGroup = consumerGroup,
                Command       = command,
                Parameters    = parametersJson,
                IssuedBy      = issuedBy,
            });
    }

    public async Task<IReadOnlyList<PendingCommand>> ClaimPendingAsync(
        string instanceId, int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        // Claim up to <limit> rows atomically using SKIP LOCKED
        var rows = await conn.QueryAsync<PendingCommand>(
            """
            WITH claimed AS (
                SELECT id
                FROM   processor_commands
                WHERE  processed_at IS NULL
                ORDER  BY created_at
                LIMIT  @Limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE processor_commands pc
            SET    processed_at = now(),
                   processed_by = @InstanceId
            FROM   claimed
            WHERE  pc.id = claimed.id
            RETURNING pc.id, pc.processor_name, pc.consumer_group, pc.command,
                      pc.parameters::text AS parameters
            """,
            new { Limit = limit, InstanceId = instanceId }, tx);

        await tx.CommitAsync(cancellationToken);
        return rows.AsList();
    }
}
