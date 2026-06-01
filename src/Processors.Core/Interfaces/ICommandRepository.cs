namespace Processors.Core.Interfaces;

public interface ICommandRepository
{
    /// <summary>Issue a command targeting a processor. Returns the new command ID.</summary>
    Task<long> IssueCommandAsync(
        string processorName,
        string consumerGroup,
        string command,
        object? parameters = null,
        string? issuedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Claim and return up to <paramref name="limit"/> unprocessed commands for execution.</summary>
    Task<IReadOnlyList<PendingCommand>> ClaimPendingAsync(
        string instanceId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

public sealed class PendingCommand
{
    public long Id { get; set; }
    public string ProcessorName { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    /// <summary>Raw JSON parameters string (may be null).</summary>
    public string? Parameters { get; set; }
}
