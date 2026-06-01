using Processors.Core.Extensions;
using Processors.Core.Interfaces;
using Processors.Dashboard.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, svc, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(svc)
        .Enrich.FromLogContext());

    // ── Core repositories (read-only for dashboard) ──────────────────────────
    builder.Services.AddEventLogCore(builder.Configuration);
    builder.Services.AddSingleton<DashboardQueryService>();

    // CORS — allow frontend dev servers (adjust for production)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();

    app.UseCors();
    app.UseDefaultFiles();   // serves index.html from wwwroot
    app.UseStaticFiles();

    // ── API Endpoints ────────────────────────────────────────────────────────

    var api = app.MapGroup("/api");

    // GET /api/processors
    api.MapGet("/processors", async (DashboardQueryService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetAllStatusesAsync(ct)));

    // GET /api/processors/{name}/{consumerGroup}
    api.MapGet("/processors/{name}/{consumerGroup}", async (
        string name, string consumerGroup,
        DashboardQueryService svc, CancellationToken ct) =>
    {
        var status = await svc.GetStatusAsync(name, consumerGroup, ct);
        return status is null ? Results.NotFound() : Results.Ok(status);
    });

    // POST /api/processors/{name}/{consumerGroup}/start
    api.MapPost("/processors/{name}/{consumerGroup}/start", async (
        string name, string consumerGroup,
        ICommandRepository cmdRepo, CancellationToken ct) =>
    {
        var id = await cmdRepo.IssueCommandAsync(name, consumerGroup, "start", issuedBy: "dashboard", cancellationToken: ct);
        return Results.Accepted($"/api/commands/{id}", new { commandId = id });
    });

    // POST /api/processors/{name}/{consumerGroup}/stop
    api.MapPost("/processors/{name}/{consumerGroup}/stop", async (
        string name, string consumerGroup,
        ICommandRepository cmdRepo, CancellationToken ct) =>
    {
        var id = await cmdRepo.IssueCommandAsync(name, consumerGroup, "stop", issuedBy: "dashboard", cancellationToken: ct);
        return Results.Accepted($"/api/commands/{id}", new { commandId = id });
    });

    // POST /api/processors/{name}/{consumerGroup}/scale   body: {"threads":4}
    api.MapPost("/processors/{name}/{consumerGroup}/scale", async (
        string name, string consumerGroup,
        ScaleRequest body,
        ICommandRepository cmdRepo, CancellationToken ct) =>
    {
        if (body.Threads < 1)
            return Results.BadRequest("threads must be >= 1");

        var id = await cmdRepo.IssueCommandAsync(
            name, consumerGroup, "scale",
            parameters: new { threads = body.Threads },
            issuedBy: "dashboard",
            cancellationToken: ct);

        return Results.Accepted($"/api/commands/{id}", new { commandId = id });
    });

    // GET /api/processors/{name}/{consumerGroup}/dead-letter
    api.MapGet("/processors/{name}/{consumerGroup}/dead-letter", async (
        string name, string consumerGroup,
        DashboardQueryService svc, CancellationToken ct) =>
    {
        // We need the topic — resolve it from heartbeat data
        var status = await svc.GetStatusAsync(name, consumerGroup, ct);
        if (status is null) return Results.NotFound();
        var events = await svc.GetDeadLetterAsync(status.Topic, consumerGroup, 100, ct);
        return Results.Ok(events);
    });

    // GET /api/health
    api.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTimeOffset.UtcNow,
    }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Dashboard host terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// ── Local Types ──────────────────────────────────────────────────────────────
record ScaleRequest(int Threads);
