using Microsoft.Extensions.Logging;
using Processors.Core.Configuration;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Framework;

/// <summary>
/// Runs the polling loop for a single processor configuration.
/// Handles distributed locking, offset tracking, retry with exponential back-off,
/// dead-lettering, heartbeats, and configurable ordered/concurrent processing.
/// </summary>
public sealed class ProcessorRunner
{
    private readonly ProcessorConfig _config;
    private readonly IEventProcessor _processor;
    private readonly IEventRepository _eventRepo;
    private readonly IOffsetRepository _offsetRepo;
    private readonly ILockRepository _lockRepo;
    private readonly IDeadLetterRepository _deadLetterRepo;
    private readonly IHeartbeatRepository _heartbeatRepo;
    private readonly ILogger<ProcessorRunner> _logger;

    // Stable per-instance identity (survives restarts of the same process, not pod restarts)
    private readonly string _instanceId =
        $"{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 32);

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private Task? _heartbeatTask;

    // Volatile fields read by GetStatus() from any thread
    private volatile int _state = (int)ProcessorState.Stopped;
    private long _eventsProcessed;
    private long _errorCount;
    private long _lastProcessedId;
    private long _latestEventId;
    private int _activeThreads;
    private DateTime? _lastProcessedAt;
    private volatile string? _lastError;

    public string Name => _config.Name;
    public string Topic => _config.Topic;
    public string ConsumerGroup => _config.ConsumerGroup;

    public ProcessorRunner(
        ProcessorConfig config,
        IEventProcessor processor,
        IEventRepository eventRepo,
        IOffsetRepository offsetRepo,
        ILockRepository lockRepo,
        IDeadLetterRepository deadLetterRepo,
        IHeartbeatRepository heartbeatRepo,
        ILogger<ProcessorRunner> logger)
    {
        _config        = config;
        _processor     = processor;
        _eventRepo     = eventRepo;
        _offsetRepo    = offsetRepo;
        _lockRepo      = lockRepo;
        _deadLetterRepo = deadLetterRepo;
        _heartbeatRepo = heartbeatRepo;
        _logger        = logger;
    }

    // ── Public Control API ──────────────────────────────────────────────────

    public ProcessorStatus GetStatus() => new()
    {
        Name             = _config.Name,
        Topic            = _config.Topic,
        ConsumerGroup    = _config.ConsumerGroup,
        State            = (ProcessorState)_state,
        LastProcessedId  = Interlocked.Read(ref _lastProcessedId),
        LatestEventId    = Interlocked.Read(ref _latestEventId),
        ActiveThreads    = _activeThreads,
        MaxThreads       = _config.MaxThreads,
        EventsProcessed  = Interlocked.Read(ref _eventsProcessed),
        ErrorCount       = Interlocked.Read(ref _errorCount),
        LastProcessedAt  = _lastProcessedAt,
        LastError        = _lastError,
    };

    public Task StartAsync(CancellationToken appToken)
    {
        var current = (ProcessorState)_state;
        if (current is ProcessorState.Running or ProcessorState.Starting)
            return Task.CompletedTask;

        _state = (int)ProcessorState.Starting;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(appToken);

        _runTask       = RunLoopAsync(_cts.Token);
        _heartbeatTask = HeartbeatLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        var current = (ProcessorState)_state;
        if (current is ProcessorState.Stopped or ProcessorState.Stopping)
            return;

        _state = (int)ProcessorState.Stopping;
        _cts?.Cancel();

        var tasks = new[] { _runTask, _heartbeatTask }
            .Where(t => t is not null)
            .Select(t => t!.ContinueWith(_ => { })); // swallow cancellation

        await Task.WhenAll(tasks);
        _state = (int)ProcessorState.Stopped;
    }

    public void SetMaxThreads(int maxThreads)
    {
        if (maxThreads > 0)
            _config.MaxThreads = maxThreads;
    }

    // ── Polling Loop ────────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "[{Name}] Starting — topic={Topic} group={Group} instance={Instance}",
            _config.Name, _config.Topic, _config.ConsumerGroup, _instanceId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    bool hasLock = await _lockRepo.TryAcquireOrRenewAsync(
                        _config.ConsumerGroup, _config.Topic,
                        _instanceId, _config.LockTimeoutSeconds, ct);

                    if (!hasLock)
                    {
                        _logger.LogDebug("[{Name}] Could not acquire lock — another instance is running.", _config.Name);
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        continue;
                    }

                    _state = (int)ProcessorState.Running;

                    long lastId = await _offsetRepo.GetOffsetAsync(_config.ConsumerGroup, _config.Topic, ct);
                    Interlocked.Exchange(ref _lastProcessedId, lastId);

                    var events = await _eventRepo.GetNextBatchAsync(_config.Topic, lastId, _config.BatchSize, ct);

                    // Always refresh lag counter (cheap MAX query)
                    var latest = await _eventRepo.GetLatestIdAsync(_config.Topic, ct);
                    Interlocked.Exchange(ref _latestEventId, latest);

                    if (events.Count == 0)
                    {
                        await Task.Delay(_config.PollingIntervalMs, ct);
                        continue;
                    }

                    long hwm = _config.OrderingRequired
                        ? await ProcessSequentiallyAsync(events, ct)
                        : await ProcessConcurrentlyAsync(events, ct);

                    if (hwm > lastId)
                    {
                        await _offsetRepo.CommitOffsetAsync(_config.ConsumerGroup, _config.Topic, hwm, ct);
                        Interlocked.Exchange(ref _lastProcessedId, hwm);
                        _lastProcessedAt = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _state = (int)ProcessorState.Faulted;
                    _lastError = ex.Message;
                    Interlocked.Increment(ref _errorCount);
                    _logger.LogError(ex, "[{Name}] Unhandled error in polling loop — retrying in 5 s.", _config.Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }
        finally
        {
            // Best-effort lock release
            await _lockRepo.ReleaseAsync(_config.ConsumerGroup, _config.Topic, _instanceId, CancellationToken.None);
            _state = (int)ProcessorState.Stopped;
            _logger.LogInformation("[{Name}] Stopped.", _config.Name);
        }
    }

    // ── Batch Processing ────────────────────────────────────────────────────

    private async Task<long> ProcessSequentiallyAsync(IReadOnlyList<EventRecord> events, CancellationToken ct)
    {
        long hwm = 0;
        foreach (var record in events)
        {
            await ProcessWithRetryAsync(record, ct);
            hwm = record.Id;
        }
        return hwm;
    }

    private async Task<long> ProcessConcurrentlyAsync(IReadOnlyList<EventRecord> events, CancellationToken ct)
    {
        int degree = Math.Min(_config.MaxThreads, events.Count);
        Interlocked.Exchange(ref _activeThreads, degree);

        var semaphore = new SemaphoreSlim(degree, degree);

        var tasks = events.Select(async record =>
        {
            await semaphore.WaitAsync(ct);
            try   { await ProcessWithRetryAsync(record, ct); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        Interlocked.Exchange(ref _activeThreads, 0);

        // All events completed (or dead-lettered); advance to the last event
        return events[^1].Id;
    }

    // ── Retry + Dead-Letter ─────────────────────────────────────────────────

    private async Task ProcessWithRetryAsync(EventRecord record, CancellationToken ct)
    {
        var context = EventContext.FromRecord(record);
        int attempt = 0;

        while (true)
        {
            try
            {
                await _processor.ProcessAsync(context, ct);
                Interlocked.Increment(ref _eventsProcessed);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                _lastError = ex.Message;

                if (attempt > _config.MaxRetries)
                {
                    Interlocked.Increment(ref _errorCount);
                    _logger.LogError(ex,
                        "[{Name}] Event {EventId} failed after {Attempts} attempts — sending to dead-letter.",
                        _config.Name, record.Id, attempt);

                    await SendToDeadLetterAsync(record, attempt, ex.Message);
                    return; // Do not block the offset
                }

                var delay = TimeSpan.FromMilliseconds(
                    _config.RetryBaseDelayMs * Math.Pow(2, attempt - 1));

                _logger.LogWarning(ex,
                    "[{Name}] Event {EventId} attempt {Attempt}/{Max} failed — retrying in {Delay} ms.",
                    _config.Name, record.Id, attempt, _config.MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task SendToDeadLetterAsync(EventRecord record, int attempts, string lastError)
    {
        try
        {
            await _deadLetterRepo.InsertAsync(new DeadLetterEvent
            {
                OriginalEventId = record.Id,
                Topic           = record.Topic,
                ConsumerGroup   = _config.ConsumerGroup,
                ProcessorName   = _config.Name,
                Attempts        = attempts,
                LastError       = lastError,
                Payload         = record.Payload,
                Headers         = record.Headers,
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Name}] Failed to write event {EventId} to dead-letter queue.", _config.Name, record.Id);
        }
    }

    // ── Heartbeat Loop ──────────────────────────────────────────────────────

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _heartbeatRepo.UpsertAsync(new ProcessorHeartbeat
                {
                    ProcessorName   = _config.Name,
                    ConsumerGroup   = _config.ConsumerGroup,
                    InstanceId      = _instanceId,
                    Topic           = _config.Topic,
                    State           = ((ProcessorState)_state).ToString(),
                    EventsProcessed = Interlocked.Read(ref _eventsProcessed),
                    ErrorCount      = Interlocked.Read(ref _errorCount),
                    ActiveThreads   = _activeThreads,
                    MaxThreads      = _config.MaxThreads,
                    LastError       = _lastError,
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Name}] Heartbeat write failed.", _config.Name);
            }

            await Task.Delay(interval, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
