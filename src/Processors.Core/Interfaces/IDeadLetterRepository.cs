using Processors.Core.Models;

namespace Processors.Core.Interfaces;

public interface IDeadLetterRepository
{
    Task InsertAsync(DeadLetterEvent evt, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeadLetterEvent>> GetRecentAsync(
        string topic,
        string consumerGroup,
        int count = 100,
        CancellationToken cancellationToken = default);
}
