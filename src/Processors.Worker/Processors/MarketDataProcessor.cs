using Microsoft.Extensions.Logging;
using Processors.Core.Interfaces;
using Processors.Core.Models;
using System.Text.Json;

namespace Processors.Worker.Processors;

/// <summary>
/// Sample processor for market tick data on topic "market.{symbol}.ticks".
/// Demonstrates typed payload deserialization and lightweight business logic.
/// </summary>
public sealed class MarketDataProcessor(ILogger<MarketDataProcessor> logger) : IEventProcessor
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task ProcessAsync(EventContext context, CancellationToken cancellationToken)
    {
        var tick = context.GetPayload<MarketTick>();

        logger.LogInformation(
            "[MarketData] EventId={Id} Symbol={Symbol} Price={Price:F4} Volume={Volume} Time={Time:O}",
            context.EventId, tick.Symbol, tick.Price, tick.Volume, context.EventTime);

        // TODO: write to time-series DB, update order book, trigger alerts, etc.

        return Task.CompletedTask;
    }
}

/// <summary>Strongly-typed payload for market tick events.</summary>
public sealed class MarketTick
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long Volume { get; init; }
    public string Exchange { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;   // "bid" | "ask" | "trade"
    public DateTimeOffset Timestamp { get; init; }
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
