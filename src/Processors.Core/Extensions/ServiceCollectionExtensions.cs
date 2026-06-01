using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Processors.Core.Configuration;
using Processors.Core.Framework;
using Processors.Core.Interfaces;
using Processors.Core.Producers;
using Processors.Core.Repositories;

namespace Processors.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Core repositories and the EventProducer.
    /// Call this in both the Worker and the Dashboard.
    /// </summary>
    public static IServiceCollection AddEventLogCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Enable snake_case → PascalCase Dapper mapping globally
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var options = configuration
            .GetSection(EventLogOptions.SectionName)
            .Get<EventLogOptions>() ?? new EventLogOptions();

        // Prefer ConnectionStrings:Postgres, fall back to EventLog:ConnectionString
        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? options.ConnectionString
            ?? throw new InvalidOperationException(
                "No PostgreSQL connection string found. " +
                "Set ConnectionStrings:Postgres or EventLog:ConnectionString.");

        // Store resolved string + options for downstream use
        services.AddSingleton(options);
        services.AddSingleton(new DbConnectionString(connectionString));

        services.AddSingleton<IEventRepository>(_ => new EventRepository(connectionString));
        services.AddSingleton<IOffsetRepository>(_ => new OffsetRepository(connectionString));
        services.AddSingleton<ILockRepository>(_ => new LockRepository(connectionString));
        services.AddSingleton<IDeadLetterRepository>(_ => new DeadLetterRepository(connectionString));
        services.AddSingleton<IHeartbeatRepository>(_ => new HeartbeatRepository(connectionString));
        services.AddSingleton<ICommandRepository>(_ => new CommandRepository(connectionString));
        services.AddSingleton<IEventProducer, EventProducer>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="ProcessorManager"/> hosted service and all runners.
    /// Only call this in the Worker host — not in the Dashboard.
    /// </summary>
    public static IServiceCollection AddProcessorFramework(
        this IServiceCollection services,
        Action<ProcessorRegistry> configure)
    {
        var registry = new ProcessorRegistry();
        configure(registry);
        services.AddSingleton(registry);

        services.AddSingleton<ProcessorManager>(sp =>
        {
            var options    = sp.GetRequiredService<EventLogOptions>();
            var runners    = BuildRunners(sp, registry, options);
            var cmdRepo    = sp.GetRequiredService<ICommandRepository>();
            var logger     = sp.GetRequiredService<ILogger<ProcessorManager>>();
            return new ProcessorManager(runners, cmdRepo, logger);
        });

        // Expose ProcessorManager as the hosted service
        services.AddHostedService(sp => sp.GetRequiredService<ProcessorManager>());

        return services;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<ProcessorRunner> BuildRunners(
        IServiceProvider sp,
        ProcessorRegistry registry,
        EventLogOptions options)
    {
        var runners = new List<ProcessorRunner>();

        foreach (var (name, factory) in registry.Registrations)
        {
            var config = options.Processors.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No ProcessorConfig found for processor '{name}'. " +
                    $"Add an entry to EventLog:Processors in appsettings.json.");

            var processor = factory(sp);

            runners.Add(new ProcessorRunner(
                config,
                processor,
                sp.GetRequiredService<IEventRepository>(),
                sp.GetRequiredService<IOffsetRepository>(),
                sp.GetRequiredService<ILockRepository>(),
                sp.GetRequiredService<IDeadLetterRepository>(),
                sp.GetRequiredService<IHeartbeatRepository>(),
                sp.GetRequiredService<ILogger<ProcessorRunner>>()));
        }

        return runners;
    }
}

/// <summary>Holds the resolved connection string so it can be injected separately if needed.</summary>
public sealed record DbConnectionString(string Value);

/// <summary>
/// Maps processor names to their factory delegates.
/// Used in <see cref="ServiceCollectionExtensions.AddProcessorFramework"/>.
/// </summary>
public sealed class ProcessorRegistry
{
    internal Dictionary<string, Func<IServiceProvider, IEventProcessor>> Registrations { get; } = [];

    /// <summary>Register a processor resolved via DI. Ensure TProcessor is also registered as a service.</summary>
    public ProcessorRegistry Register<TProcessor>(string name)
        where TProcessor : class, IEventProcessor
    {
        Registrations[name] = sp => sp.GetRequiredService<TProcessor>();
        return this;
    }

    /// <summary>Register a processor using a custom factory delegate.</summary>
    public ProcessorRegistry Register(string name, Func<IServiceProvider, IEventProcessor> factory)
    {
        Registrations[name] = factory;
        return this;
    }
}
