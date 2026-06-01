using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Processors.Core.Interfaces;
using Processors.Core.Models;

namespace Processors.Core.Framework;

/// <summary>
/// Hosted service that owns the lifecycle of all <see cref="ProcessorRunner"/> instances.
/// On startup it starts processors that have AutoStart = true, then polls the
/// processor_commands table so the dashboard can issue start/stop/scale commands.
/// </summary>
public sealed class ProcessorManager : BackgroundService
{
    private readonly IReadOnlyList<ProcessorRunner> _runners;
    private readonly ICommandRepository _commandRepo;
    private readonly ILogger<ProcessorManager> _logger;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 32);

    public ProcessorManager(
        IReadOnlyList<ProcessorRunner> runners,
        ICommandRepository commandRepo,
        ILogger<ProcessorManager> logger)
    {
        _runners     = runners;
        _commandRepo = commandRepo;
        _logger      = logger;
    }

    // ── IHostedService ──────────────────────────────────────────────────────

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var runner in _runners)
        {
            _logger.LogInformation("Auto-starting processor '{Name}'.", runner.Name);
            await runner.StartAsync(cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await Task.WhenAll(_runners.Select(r => r.StopAsync()));
    }

    // ── BackgroundService ───────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessorManager command-polling loop started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndExecuteCommandsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in command-polling loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    // ── Public Control API (used by embedded management endpoints) ──────────

    public IReadOnlyList<ProcessorStatus> GetAllStatuses() =>
        _runners.Select(r => r.GetStatus()).ToList();

    public ProcessorStatus? GetStatus(string name) =>
        FindRunner(name)?.GetStatus();

    public async Task StartProcessorAsync(string name, CancellationToken ct = default)
    {
        var runner = FindRunner(name);
        if (runner is null) { _logger.LogWarning("Processor '{Name}' not found.", name); return; }
        await runner.StartAsync(ct);
    }

    public async Task StopProcessorAsync(string name)
    {
        var runner = FindRunner(name);
        if (runner is null) return;
        await runner.StopAsync();
    }

    public void ScaleProcessor(string name, int maxThreads)
        => FindRunner(name)?.SetMaxThreads(maxThreads);

    // ── Command Polling ─────────────────────────────────────────────────────

    private async Task PollAndExecuteCommandsAsync(CancellationToken ct)
    {
        var commands = await _commandRepo.ClaimPendingAsync(_instanceId, limit: 10, ct);

        foreach (var cmd in commands)
        {
            _logger.LogInformation(
                "Executing command '{Command}' for processor '{Name}'.",
                cmd.Command, cmd.ProcessorName);

            try
            {
                await ExecuteCommandAsync(cmd, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to execute command '{Command}' for '{Name}'.",
                    cmd.Command, cmd.ProcessorName);
            }
        }
    }

    private async Task ExecuteCommandAsync(PendingCommand cmd, CancellationToken ct)
    {
        switch (cmd.Command.ToLowerInvariant())
        {
            case "start":
                await StartProcessorAsync(cmd.ProcessorName, ct);
                break;

            case "stop":
                await StopProcessorAsync(cmd.ProcessorName);
                break;

            case "scale":
                if (cmd.Parameters is not null)
                {
                    using var doc = JsonDocument.Parse(cmd.Parameters);
                    if (doc.RootElement.TryGetProperty("threads", out var el))
                        ScaleProcessor(cmd.ProcessorName, el.GetInt32());
                }
                break;

            default:
                _logger.LogWarning("Unknown command '{Command}'.", cmd.Command);
                break;
        }
    }

    private ProcessorRunner? FindRunner(string name) =>
        _runners.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
