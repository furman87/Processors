using Processors.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace Processors.Services;

public interface IProcessorConfigurationService
{
    Task<IEnumerable<ProcessorConfig>> GetAllConfigurationsAsync();
    Task<ProcessorConfig?> GetConfigurationAsync(string processorName);
    Task SaveConfigurationAsync(ProcessorConfig config);
    Task DeleteConfigurationAsync(string processorName);
    Task ReloadConfigurationsAsync();
    event EventHandler<ProcessorConfigurationChangedEventArgs>? ConfigurationChanged;
}

public class ProcessorConfigurationChangedEventArgs : EventArgs
{
    public string ProcessorName { get; set; } = string.Empty;
    public ProcessorConfig? Configuration { get; set; }
    public ConfigurationChangeType ChangeType { get; set; }
}

public enum ConfigurationChangeType
{
    Added,
    Updated,
    Deleted
}

public class JsonProcessorConfigurationService : IProcessorConfigurationService
{
    private readonly string _configurationFilePath;
    private readonly ILogger<JsonProcessorConfigurationService> _logger;
    private readonly FileSystemWatcher _fileWatcher;
    private Dictionary<string, ProcessorConfig> _configurations = new();
    private readonly object _lock = new object();
    private readonly JsonSerializerOptions _jsonOptions;

    public event EventHandler<ProcessorConfigurationChangedEventArgs>? ConfigurationChanged;

    public JsonProcessorConfigurationService(IConfiguration configuration, ILogger<JsonProcessorConfigurationService> logger)
    {
        _logger = logger;
        _configurationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "processors.json");
        
        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        // Ensure the configuration directory exists
        var configDir = Path.GetDirectoryName(_configurationFilePath);
        if (configDir != null && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // Initialize file watcher for hot reload
        _fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_configurationFilePath)!, Path.GetFileName(_configurationFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        
        _fileWatcher.Changed += OnConfigurationFileChanged;
        _fileWatcher.Created += OnConfigurationFileChanged;

        // Load initial configuration
        _ = Task.Run(LoadConfigurationsAsync);
    }

    public async Task<IEnumerable<ProcessorConfig>> GetAllConfigurationsAsync()
    {
        lock (_lock)
        {
            return _configurations.Values.ToList();
        }
    }

    public async Task<ProcessorConfig?> GetConfigurationAsync(string processorName)
    {
        lock (_lock)
        {
            _configurations.TryGetValue(processorName, out var config);
            return config;
        }
    }

    public async Task SaveConfigurationAsync(ProcessorConfig config)
    {
        lock (_lock)
        {
            var isNew = !_configurations.ContainsKey(config.Name);
            _configurations[config.Name] = config;
            
            // Save to file using JsonNode for better handling of complex objects
            var configsArray = JsonSerializer.SerializeToNode(_configurations.Values.ToArray(), _jsonOptions);
            var configData = new JsonObject
            {
                ["Processors"] = configsArray
            };
            
            var json = configData.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configurationFilePath, json);
            
            ConfigurationChanged?.Invoke(this, new ProcessorConfigurationChangedEventArgs
            {
                ProcessorName = config.Name,
                Configuration = config,
                ChangeType = isNew ? ConfigurationChangeType.Added : ConfigurationChangeType.Updated
            });
            
            _logger.LogInformation("Saved configuration for processor {ProcessorName}", config.Name);
        }
    }

    public async Task DeleteConfigurationAsync(string processorName)
    {
        lock (_lock)
        {
            if (_configurations.Remove(processorName))
            {
                // Save to file using JsonNode for better handling of complex objects
                var configsArray = JsonSerializer.SerializeToNode(_configurations.Values.ToArray(), _jsonOptions);
                var configData = new JsonObject
                {
                    ["Processors"] = configsArray
                };
                
                var json = configData.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configurationFilePath, json);
                
                ConfigurationChanged?.Invoke(this, new ProcessorConfigurationChangedEventArgs
                {
                    ProcessorName = processorName,
                    Configuration = null,
                    ChangeType = ConfigurationChangeType.Deleted
                });
                
                _logger.LogInformation("Deleted configuration for processor {ProcessorName}", processorName);
            }
        }
    }

    public async Task ReloadConfigurationsAsync()
    {
        // Clear in-memory cache first
        lock (_lock)
        {
            _configurations.Clear();
        }
        
        // Force reload from file
        await LoadConfigurationsAsync();
        
        _logger.LogInformation("Forced reload of configurations completed");
    }

    private async Task LoadConfigurationsAsync()
    {
        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                _logger.LogWarning("Processor configuration file not found at {Path}. Creating default configuration.", _configurationFilePath);
                await CreateDefaultConfigurationAsync();
                return;
            }

            var json = await File.ReadAllTextAsync(_configurationFilePath);
            
            // Parse using JsonNode to preserve complex object structures
            var jsonNode = JsonNode.Parse(json);
            var processorsArray = jsonNode?["Processors"]?.AsArray();
            
            if (processorsArray != null)
            {
                lock (_lock)
                {
                    _configurations.Clear();
                    
                    foreach (var processorNode in processorsArray)
                    {
                        if (processorNode != null)
                        {
                            var config = ConvertJsonNodeToProcessorConfig(processorNode);
                            if (config != null)
                            {
                                _configurations[config.Name] = config;
                            }
                        }
                    }
                }
                
                _logger.LogInformation("Loaded {Count} processor configurations from {Path}", 
                    _configurations.Count, _configurationFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load processor configurations from {Path}", _configurationFilePath);
        }
    }

    private ProcessorConfig? ConvertJsonNodeToProcessorConfig(JsonNode processorNode)
    {
        try
        {
            var config = new ProcessorConfig
            {
                Name = processorNode["Name"]?.GetValue<string>() ?? "",
                InputTopic = processorNode["InputTopic"]?.GetValue<string>() ?? "",
                ProcessorType = processorNode["ProcessorType"]?.GetValue<string>() ?? "",
                MaxConcurrency = processorNode["MaxConcurrency"]?.GetValue<int>() ?? 1,
                PollingIntervalSeconds = processorNode["PollingIntervalSeconds"]?.GetValue<int>() ?? 5,
                AutoStart = processorNode["AutoStart"]?.GetValue<bool>() ?? true
            };

            // Convert PollingIntervalSeconds to TimeSpan
            config.PollingInterval = TimeSpan.FromSeconds(config.PollingIntervalSeconds);

            // Handle OutputTopics array
            var outputTopicsArray = processorNode["OutputTopics"]?.AsArray();
            if (outputTopicsArray != null)
            {
                config.OutputTopics = outputTopicsArray
                    .Where(item => item != null)
                    .Select(item => item.GetValue<string>())
                    .ToList();
            }

            // Handle CustomSettings with proper preservation of arrays and complex types
            var customSettingsNode = processorNode["CustomSettings"];
            if (customSettingsNode != null)
            {
                config.CustomSettings = ConvertJsonNodeToCustomSettings(customSettingsNode);
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert JSON node to ProcessorConfig");
            return null;
        }
    }

    private Dictionary<string, object> ConvertJsonNodeToCustomSettings(JsonNode customSettingsNode)
    {
        var customSettings = new Dictionary<string, object>();
        
        if (customSettingsNode.AsObject() != null)
        {
            foreach (var property in customSettingsNode.AsObject())
            {
                if (property.Value != null)
                {
                    customSettings[property.Key] = ConvertJsonValue(property.Value);
                }
            }
        }
        
        return customSettings;
    }

    private object ConvertJsonValue(JsonNode jsonNode)
    {
        return jsonNode.GetValueKind() switch
        {
            JsonValueKind.String => jsonNode.GetValue<string>(),
            JsonValueKind.Number => jsonNode.GetValue<decimal>(),
            JsonValueKind.True or JsonValueKind.False => jsonNode.GetValue<bool>(),
            JsonValueKind.Array => jsonNode.AsArray().Select(item => item != null ? ConvertJsonValue(item) : null).ToArray(),
            JsonValueKind.Object => ConvertJsonNodeToCustomSettings(jsonNode),
            JsonValueKind.Null => null!,
            _ => jsonNode.ToString()
        };
    }

    private async Task CreateDefaultConfigurationAsync()
    {
        var defaultConfigs = new ProcessorConfig[]
        {
            new()
            {
                Name = "EmailProcessor",
                InputTopic = "email_queue",
                OutputTopics = new List<string> { "notification_queue" },
                ProcessorType = "EmailMessageProcessor",
                MaxConcurrency = 3,
                PollingIntervalSeconds = 10,
                PollingInterval = TimeSpan.FromSeconds(10),
                AutoStart = true,
                CustomSettings = new Dictionary<string, object>
                {
                    ["EmailProvider"] = "SMTP",
                    ["RetryAttempts"] = 3,
                    ["TimeoutSeconds"] = 30
                }
            },
            new()
            {
                Name = "DataProcessor",
                InputTopic = "data_queue",
                OutputTopics = new List<string> { "analytics_queue", "reporting_queue" },
                ProcessorType = "DataMessageProcessor",
                MaxConcurrency = 5,
                PollingIntervalSeconds = 5,
                PollingInterval = TimeSpan.FromSeconds(5),
                AutoStart = true,
                CustomSettings = new Dictionary<string, object>
                {
                    ["BatchSize"] = 100,
                    ["CompressionEnabled"] = true,
                    ["ValidationLevel"] = "Strict"
                }
            },
            new()
            {
                Name = "NotificationProcessor",
                InputTopic = "notification_queue",
                OutputTopics = new List<string>(),
                ProcessorType = "NotificationMessageProcessor",
                MaxConcurrency = 2,
                PollingIntervalSeconds = 15,
                PollingInterval = TimeSpan.FromSeconds(15),
                AutoStart = false,
                CustomSettings = new Dictionary<string, object>
                {
                    ["Channels"] = new[] { "Email", "SMS", "Push" },
                    ["PriorityQueue"] = true,
                    ["DeliveryTimeout"] = 60
                }
            }
        };

        var configsArray = JsonSerializer.SerializeToNode(defaultConfigs, _jsonOptions);
        var configData = new JsonObject
        {
            ["Processors"] = configsArray
        };
        
        var json = configData.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configurationFilePath, json);
        
        lock (_lock)
        {
            _configurations.Clear();
            foreach (var config in defaultConfigs)
            {
                _configurations[config.Name] = config;
            }
        }
        
        _logger.LogInformation("Created default processor configuration file at {Path}", _configurationFilePath);
    }

    private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce file changes (multiple events can fire for a single file save)
        Task.Delay(1000).ContinueWith(_ =>
        {
            try
            {
                LoadConfigurationsAsync().Wait();
                _logger.LogInformation("Configuration file reloaded due to external changes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload configuration after file change");
            }
        });
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

public class ProcessorConfigurationFile
{
    public ProcessorConfig[] Processors { get; set; } = Array.Empty<ProcessorConfig>();
}