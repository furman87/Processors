using System.Text.Json;
using Processors.Core.Interfaces;
using Processors.Core.Repositories;

namespace Processors.Core.Producers;

/// <summary>
/// Serializes payloads to JSON and writes them to the event_log table.
/// Inject <see cref="IEventProducer"/> wherever you need to publish events.
/// </summary>
public sealed class EventProducer(IEventRepository repository) : IEventProducer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task<long> ProduceAsync(
        string topic,
        object payload,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson  = JsonSerializer.Serialize(payload, JsonOpts);
        var headersJson  = headers is not null ? JsonSerializer.Serialize(headers, JsonOpts) : null;

        return repository.InsertAsync(topic, payloadJson, partitionKey, headersJson,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<long>> ProduceBatchAsync(
        IEnumerable<ProduceRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var insertRequests = requests.Select(r => new EventInsertRequest
        {
            Topic        = r.Topic,
            PayloadJson  = JsonSerializer.Serialize(r.Payload, JsonOpts),
            PartitionKey = r.PartitionKey,
            HeadersJson  = r.Headers is not null ? JsonSerializer.Serialize(r.Headers, JsonOpts) : null,
            EventTime    = r.EventTime?.UtcDateTime,
        });

        return repository.InsertBatchAsync(insertRequests, cancellationToken);
    }
}
