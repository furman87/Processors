using Processors.Core.Extensions;
using Processors.Worker.Processors;
using Serilog;

// ── Bootstrap Serilog from appsettings.json ─────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Serilog
    builder.Services.AddSerilog((svc, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(svc)
        .Enrich.FromLogContext());

    // ── Core: repositories + producer ───────────────────────────────────────
    builder.Services.AddEventLogCore(builder.Configuration);

    // ── Register processor implementations ──────────────────────────────────
    // Each processor must be registered in DI and then named in the registry.
    // The name must match the "Name" field in EventLog:Processors config.
    builder.Services.AddSingleton<MarketDataProcessor>();
    builder.Services.AddSingleton<LoggingProcessor>();

    // ── Register the framework (creates runners + ProcessorManager) ──────────
    builder.Services.AddProcessorFramework(registry =>
    {
        registry.Register<MarketDataProcessor>("MarketDataProcessor");
        registry.Register<LoggingProcessor>("LoggingProcessor");
    });

    // ── Optional: expose a lightweight management HTTP API on this worker ────
    // Uncomment and configure if you want per-worker management endpoints.
    // builder.WebHost.UseUrls("http://localhost:5010");

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker host terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
