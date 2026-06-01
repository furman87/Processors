using System.Text.Json;
using System.Text.Json.Nodes;

namespace Processors.Core.Models;

/// <summary>
/// Rich context object passed to <see cref="Interfaces.IEventProcessor.ProcessAsync"/>.
/// Provides strongly-typed access to the event payload and headers.
/// </summary>
public sealed class EventContext
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public long EventId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? PartitionKey { get; init; }
    public DateTimeOffset EventTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string RawPayload { get; init; } = string.Empty;
    public string? RawHeaders { get; init; }

    /// <summary>Deserialize the payload to a strongly-typed object.</summary>
    public T GetPayload<T>() =>
        JsonSerializer.Deserialize<T>(RawPayload, JsonOpts)
        ?? throw new InvalidOperationException(
            $"Payload deserialized to null for type {typeof(T).Name}.");

    /// <summary>Parse the payload as a dynamic JSON node for schemaless access.</summary>
    public JsonNode? GetPayloadNode() => JsonNode.Parse(RawPayload);

    /// <summary>Returns header key/value pairs, or null when no headers were stored.</summary>
    public IReadOnlyDictionary<string, string>? GetHeaders() =>
        RawHeaders is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(RawHeaders, JsonOpts);

    internal static EventContext FromRecord(EventRecord record) => new()
    {
        EventId      = record.Id,
        Topic        = record.Topic,
        PartitionKey = record.PartitionKey,
        EventTime    = DateTime.SpecifyKind(record.EventTime, DateTimeKind.Utc),
        CreatedAt    = DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc),
        RawPayload   = record.Payload,
        RawHeaders   = record.Headers,
    };
}
