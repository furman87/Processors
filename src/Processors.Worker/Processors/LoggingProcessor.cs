using Microsoft.Extensions.Logging;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Worker.Processors;

/// <summary>
/// Generic audit/logging processor — writes every event to the structured log.
/// Useful as a debug subscriber or compliance audit trail on any topic.
/// </summary>
public sealed class LoggingProcessor(ILogger<LoggingProcessor> logger) : IEventProcessor
{
    public Task ProcessAsync(EventContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[AuditLog] Topic={Topic} EventId={Id} PartitionKey={Key} EventTime={Time:O} Payload={Payload}",
            context.Topic,
            context.EventId,
            context.PartitionKey ?? "(none)",
            context.EventTime,
            context.RawPayload);

        return Task.CompletedTask;
    }
}
