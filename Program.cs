using Processors.Interfaces;
using Processors.Services;
using Processors.MessageSources;
using Processors.MessageProcessors;
using StackExchange.Redis;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/processor-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddControllersWithViews();

// Add database services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton<DatabaseInitializer>(sp => 
    new DatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<DatabaseInitializer>>()));

// Add processor configuration service
builder.Services.AddSingleton<IProcessorConfigurationService, JsonProcessorConfigurationService>();

// Add Redis
var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(redisConnectionString));
}

// Add message sources
builder.Services.AddScoped<DatabaseMessageSource>(sp =>
    new DatabaseMessageSource(connectionString, sp.GetRequiredService<ILogger<DatabaseMessageSource>>()));

if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddScoped<RedisMessageSource>(sp =>
        new RedisMessageSource(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger<RedisMessageSource>>()));
}

var fileSystemBasePath = builder.Configuration.GetValue<string>("FileSystem:BasePath") ?? "./MessageData";
builder.Services.AddScoped<FileSystemMessageSource>(sp =>
    new FileSystemMessageSource(fileSystemBasePath, sp.GetRequiredService<ILogger<FileSystemMessageSource>>()));

var httpApiBaseUrl = builder.Configuration.GetValue<string>("HttpApi:BaseUrl");
if (!string.IsNullOrEmpty(httpApiBaseUrl))
{
    builder.Services.AddHttpClient<HttpApiMessageSource>(client =>
    {
        client.BaseAddress = new Uri(httpApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    
    builder.Services.AddScoped<HttpApiMessageSource>(sp =>
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(HttpApiMessageSource));
        return new HttpApiMessageSource(httpClient, httpApiBaseUrl, sp.GetRequiredService<ILogger<HttpApiMessageSource>>());
    });
}

// Register primary message source (you can configure this based on your preference)
builder.Services.AddScoped<IMessageSource>(sp => sp.GetRequiredService<DatabaseMessageSource>());

// Add message logger
builder.Services.AddScoped<IMessageLogger>(sp =>
    new DatabaseMessageLogger(connectionString, sp.GetRequiredService<ILogger<DatabaseMessageLogger>>()));

// Add message publisher
builder.Services.AddScoped<IMessagePublisher, MessagePublisher>();

// Add message processors
builder.Services.AddScoped<EmailMessageProcessor>();
builder.Services.AddScoped<DataMessageProcessor>();
builder.Services.AddScoped<NotificationMessageProcessor>();
builder.Services.AddScoped<AnalyticsMessageProcessor>();
builder.Services.AddScoped<ReportingMessageProcessor>();

// Add processor engine
builder.Services.AddSingleton<IProcessorEngine, ProcessorEngine>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Configure routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();
app.MapHealthChecks("/health");

// Initialize database
try
{
    var dbInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
    await dbInitializer.InitializeAsync();
    
    var isConnected = await dbInitializer.TestConnectionAsync();
    if (isConnected)
    {
        app.Logger.LogInformation("Database connection successful");
    }
    else
    {
        app.Logger.LogWarning("Database connection failed - some features may not work");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to initialize database");
}

// Auto-start processors based on JSON configuration
var autoStart = builder.Configuration.GetValue<bool>("ProcessorSettings:AutoStartProcessors");
if (autoStart)
{
    var processorEngine = app.Services.GetRequiredService<IProcessorEngine>();
    
    // Start processors based on configuration
    _ = Task.Run(async () =>
    {
        await Task.Delay(5000); // Wait for app to fully start
        try
        {
            await processorEngine.StartAllAutoStartProcessorsAsync();
            app.Logger.LogInformation("Auto-started processors based on configuration");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to auto-start processors");
        }
    });
}

app.Run();
