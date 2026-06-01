namespace Processors.Core.Interfaces;

public interface ILockRepository
{
    /// <summary>
    /// Tries to acquire or renew the distributed lock for (consumerGroup, topic).
    /// Returns true if this instance now holds the lock.
    /// Expired locks are stolen automatically.
    /// </summary>
    Task<bool> TryAcquireOrRenewAsync(
        string consumerGroup,
        string topic,
        string lockId,
        int durationSeconds,
        CancellationToken cancellationToken);

    /// <summary>Release the lock held by this instance (called on graceful shutdown).</summary>
    Task ReleaseAsync(string consumerGroup, string topic, string lockId, CancellationToken cancellationToken);
}
