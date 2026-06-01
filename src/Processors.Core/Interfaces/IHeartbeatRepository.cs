using Processors.Core.Models;

namespace Processors.Core.Interfaces;

public interface IHeartbeatRepository
{
    Task UpsertAsync(ProcessorHeartbeat heartbeat, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessorHeartbeat>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessorHeartbeat>> GetByProcessorAsync(string processorName, string consumerGroup, CancellationToken cancellationToken);
}
