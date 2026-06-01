# PostgreSQL Event Log System

A production-ready, append-only event log with a reusable .NET 10 consumer framework, inspired by a simplified Kafka-style architecture — built entirely on **PostgreSQL** and **Dapper**.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    PostgreSQL                           │
│  event_log  ─►  consumer_offsets  ─►  consumer_locks   │
│  dead_letter_events    processor_commands               │
│  processor_heartbeats                                   │
└─────────────────────────────────────────────────────────┘
         ▲                          ▲
         │ write                    │ command
  ┌──────┴──────┐            ┌──────┴──────────┐
  │  Producers  │            │    Dashboard    │
  │ (any app)   │            │  :5050 (web UI) │
  └─────────────┘            └─────────────────┘
                                      │ read heartbeats
         ┌────────────────────────────┘
  ┌──────┴──────────────────────────────────────┐
  │          Worker Host  (per topic)            │
  │  ProcessorManager ─► ProcessorRunner (×N)    │
  │   • polls event_log                          │
  │   • distributed lock per (group, topic)      │
  │   • retry + dead-letter                      │
  │   • heartbeat every 5 s                      │
  └──────────────────────────────────────────────┘
```

### Projects

| Project | Type | Purpose |
|---|---|---|
| `Processors.Core` | Class library | Models, interfaces, repositories, ProcessorRunner/Manager, EventProducer, DI extensions |
| `Processors.Worker` | Worker service | Topic-dedicated host — runs one or more named processors |
| `Processors.Dashboard` | ASP.NET Core web | Operator UI + REST API; reads DB tables, issues commands |

**Worker isolation**: each Worker host is configured for a single topic. Deploy independent Worker containers per topic for fault isolation and independent scaling/deployment.

---

## Quick Start

### 1. Prerequisites

- .NET 10 SDK
- PostgreSQL 14+

### 2. Create the database

```bash
createdb eventlog
psql -U postgres -d eventlog -f sql/run_all.sql
```

Or run each migration individually:

```bash
psql -U postgres -d eventlog -f sql/001_event_log.sql
psql -U postgres -d eventlog -f sql/002_consumer_offsets.sql
psql -U postgres -d eventlog -f sql/003_consumer_locks.sql
psql -U postgres -d eventlog -f sql/004_dead_letter.sql
psql -U postgres -d eventlog -f sql/005_processor_commands.sql
psql -U postgres -d eventlog -f sql/006_processor_heartbeats.sql
```

### 3. Configure connection strings

Edit **`src/Processors.Worker/appsettings.json`** and **`src/Processors.Dashboard/appsettings.json`**:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=eventlog;Username=postgres;Password=yourpassword"
  }
}
```

### 4. Run the Worker

```bash
cd src/Processors.Worker
dotnet run
```

### 5. Run the Dashboard

```bash
cd src/Processors.Dashboard
dotnet run
# Open http://localhost:5050
```

---

## Configuration Reference

### Worker `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=eventlog;Username=postgres;Password=..."
  },
  "EventLog": {
    "Processors": [
      {
        "Name": "MarketDataProcessor",
        "Topic": "market.NQ.ticks",
        "ConsumerGroup": "market-processors",
        "AutoStart": true,
        "MaxThreads": 4,
        "BatchSize": 100,
        "PollingIntervalMs": 100,
        "OrderingRequired": false,
        "MaxRetries": 3,
        "RetryBaseDelayMs": 1000,
        "LockTimeoutSeconds": 30,
        "HeartbeatIntervalSeconds": 5
      }
    ]
  }
}
```

| Field | Default | Description |
|---|---|---|
| `Name` | — | Must match the name used in `registry.Register<T>(name)` |
| `Topic` | — | Event log topic to consume |
| `ConsumerGroup` | — | Logical group; offset is tracked per group |
| `AutoStart` | `true` | Start the processor when the host starts |
| `MaxThreads` | `4` | Concurrent tasks per batch (ignored when `OrderingRequired`) |
| `BatchSize` | `100` | Events fetched per polling iteration |
| `PollingIntervalMs` | `100` | Sleep duration (ms) when no new events exist |
| `OrderingRequired` | `false` | When `true`, events are processed sequentially |
| `MaxRetries` | `3` | Attempts before an event is dead-lettered |
| `RetryBaseDelayMs` | `1000` | Base delay for exponential back-off |
| `LockTimeoutSeconds` | `30` | Distributed lock lease duration |
| `HeartbeatIntervalSeconds` | `5` | How often the runner updates the heartbeat table |

---

## Writing a Processor

Implement `IEventProcessor` and register it:

```csharp
// 1. Implement the interface
public sealed class OrderProcessor(ILogger<OrderProcessor> logger) : IEventProcessor
{
    public async Task ProcessAsync(EventContext context, CancellationToken ct)
    {
        var order = context.GetPayload<Order>();  // strongly-typed deserialization
        logger.LogInformation("Processing order {OrderId}", order.Id);
        // ... business logic
    }
}

// 2. In Program.cs
builder.Services.AddSingleton<OrderProcessor>();

builder.Services.AddProcessorFramework(registry =>
{
    registry.Register<OrderProcessor>("OrderProcessor");
});

// 3. In appsettings.json, add an entry with "Name": "OrderProcessor"
```

**Worker topology**: each Worker should handle processors for a **single topic** only. Run separate Worker instances for different topics.

---

## Producing Events

```csharp
// Inject IEventProducer
var id = await producer.ProduceAsync(
    topic: "orders.created",
    payload: new { OrderId = 42, Amount = 99.99m },
    partitionKey: "customer-123",
    headers: new Dictionary<string, string> { ["source"] = "checkout-api" });

// Batch (single transaction)
var ids = await producer.ProduceBatchAsync([
    new ProduceRequest { Topic = "orders.created", Payload = order1 },
    new ProduceRequest { Topic = "orders.created", Payload = order2 },
]);
```

---

## Dashboard API

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/processors` | List all processors (from heartbeats + offsets) |
| `GET` | `/api/processors/{name}/{group}` | Single processor status |
| `POST` | `/api/processors/{name}/{group}/start` | Issue start command |
| `POST` | `/api/processors/{name}/{group}/stop` | Issue stop command |
| `POST` | `/api/processors/{name}/{group}/scale` | Issue scale command `{"threads":4}` |
| `GET` | `/api/processors/{name}/{group}/dead-letter` | Recent dead-letter events |
| `GET` | `/api/health` | Health check |

Commands are written to the `processor_commands` table and executed by the Worker's polling loop (every ~5 seconds).

---

## Horizontal Scaling

Run multiple instances of the same Worker configuration:

```bash
# Instance 1
ASPNETCORE_URLS=http://+:5010 dotnet run --project src/Processors.Worker

# Instance 2
ASPNETCORE_URLS=http://+:5011 dotnet run --project src/Processors.Worker
```

Each instance competes for the distributed lock in `consumer_locks`. Only one instance processes events at a time per `(consumer_group, topic)` pair. If the active instance crashes, the lock expires and another instance takes over automatically.

---

## Database Tables

| Table | Purpose |
|---|---|
| `event_log` | Append-only event store |
| `consumer_offsets` | Last processed event ID per (group, topic) |
| `consumer_locks` | Lease-based distributed lock |
| `dead_letter_events` | Events that failed after max retries |
| `processor_commands` | Dashboard → Worker control channel |
| `processor_heartbeats` | Worker → Dashboard status channel |

---

## Advanced Topics

### Dead-Letter Reprocessing

Query dead-letter events and re-insert them into the event log:

```sql
INSERT INTO event_log (topic, partition_key, event_time, payload, headers)
SELECT topic, null, now(), payload, headers
FROM   dead_letter_events
WHERE  topic = 'market.NQ.ticks'
  AND  consumer_group = 'market-processors'
  AND  id IN (/* specific IDs */);
```

### Retention Policy

Delete old events (keep last 30 days):

```sql
DELETE FROM event_log
WHERE created_at < now() - interval '30 days';
```

For high-throughput topics, consider range partitioning `event_log` by `created_at`.

### Adding a New Topic / Worker

1. Create a new Worker project (or add config to existing Worker for same-topic processors).
2. Add processor config to `appsettings.json`.
3. Register the processor in `Program.cs`.
4. Deploy — existing tables and indexes handle any topic name automatically.

---

## Project Structure

```
Processors/
├── sql/
│   ├── 001_event_log.sql
│   ├── 002_consumer_offsets.sql
│   ├── 003_consumer_locks.sql
│   ├── 004_dead_letter.sql
│   ├── 005_processor_commands.sql
│   ├── 006_processor_heartbeats.sql
│   └── run_all.sql
└── src/
    ├── Processors.Core/
    │   ├── Configuration/     EventLogOptions, ProcessorConfig
    │   ├── Models/            EventRecord, EventContext, ProcessorStatus, …
    │   ├── Interfaces/        IEventProcessor, IEventProducer, IEventRepository, …
    │   ├── Repositories/      EventRepository, OffsetRepository, LockRepository, …
    │   ├── Framework/         ProcessorRunner, ProcessorManager
    │   ├── Producers/         EventProducer
    │   └── Extensions/        ServiceCollectionExtensions, ProcessorRegistry
    ├── Processors.Worker/
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── Processors/        MarketDataProcessor, LoggingProcessor (samples)
    └── Processors.Dashboard/
        ├── Program.cs          (minimal API)
        ├── appsettings.json
        ├── Services/           DashboardQueryService
        └── wwwroot/            index.html (operator UI)
```
