using Processors.Core.Models;

namespace Processors.Core.Interfaces;

/// <summary>
/// Implement this interface to define event-processing logic for a topic.
/// Registered via <see cref="Extensions.ProcessorRegistry"/> in the worker host.
/// </summary>
public interface IEventProcessor
{
    Task ProcessAsync(EventContext context, CancellationToken cancellationToken);
}
