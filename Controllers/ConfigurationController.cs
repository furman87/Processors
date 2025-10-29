using Microsoft.AspNetCore.Mvc;
using Processors.Models;
using Processors.Services;
using System.Text.Json;

namespace Processors.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IProcessorConfigurationService _configurationService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(IProcessorConfigurationService configurationService, ILogger<ConfigurationController> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessorConfig>>> GetAllConfigurations()
    {
        try
        {
            var configurations = await _configurationService.GetAllConfigurationsAsync();
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processor configurations");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{processorName}")]
    public async Task<ActionResult<ProcessorConfig>> GetConfiguration(string processorName)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationAsync(processorName);
            
            if (configuration == null)
            {
                return NotFound($"Configuration for processor '{processorName}' not found");
            }

            // Return configuration with proper JSON serialization options
            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateConfiguration([FromBody] ProcessorConfig configuration)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                return BadRequest("Processor name is required");
            }

            // Check if configuration already exists
            var existingConfig = await _configurationService.GetConfigurationAsync(configuration.Name);
            if (existingConfig != null)
            {
                return Conflict($"Configuration for processor '{configuration.Name}' already exists");
            }

            await _configurationService.SaveConfigurationAsync(configuration);
            return CreatedAtAction(nameof(GetConfiguration), new { processorName = configuration.Name }, configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create configuration for processor {ProcessorName}", configuration.Name);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{processorName}")]
    public async Task<ActionResult> UpdateConfiguration(string processorName, [FromBody] ProcessorConfig configuration)
    {
        try
        {
            if (processorName != configuration.Name)
            {
                return BadRequest("Processor name in URL does not match configuration name");
            }

            // Check if configuration exists
            var existingConfig = await _configurationService.GetConfigurationAsync(processorName);
            if (existingConfig == null)
            {
                return NotFound($"Configuration for processor '{processorName}' not found");
            }

            await _configurationService.SaveConfigurationAsync(configuration);
            return Ok(new { message = $"Configuration for processor '{processorName}' updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{processorName}")]
    public async Task<ActionResult> DeleteConfiguration(string processorName)
    {
        try
        {
            // Check if configuration exists
            var existingConfig = await _configurationService.GetConfigurationAsync(processorName);
            if (existingConfig == null)
            {
                return NotFound($"Configuration for processor '{processorName}' not found");
            }

            await _configurationService.DeleteConfigurationAsync(processorName);
            return Ok(new { message = $"Configuration for processor '{processorName}' deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete configuration for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("reload")]
    public async Task<ActionResult> ReloadConfigurations()
    {
        try
        {
            await _configurationService.ReloadConfigurationsAsync();
            return Ok(new { message = "Configurations reloaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configurations");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{processorName}/validate")]
    public async Task<ActionResult> ValidateConfiguration(string processorName)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationAsync(processorName);
            
            if (configuration == null)
            {
                return NotFound($"Configuration for processor '{processorName}' not found");
            }

            var validationResults = ValidateProcessorConfig(configuration);
            
            return Ok(new 
            { 
                isValid = validationResults.Count == 0,
                errors = validationResults 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate configuration for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    private List<string> ValidateProcessorConfig(ProcessorConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Name))
            errors.Add("Processor name is required");

        if (string.IsNullOrWhiteSpace(config.InputTopic))
            errors.Add("Input topic is required");

        if (string.IsNullOrWhiteSpace(config.ProcessorType))
            errors.Add("Processor type is required");

        if (config.MaxConcurrency <= 0)
            errors.Add("Max concurrency must be greater than 0");

        if (config.PollingInterval.TotalSeconds <= 0)
            errors.Add("Polling interval must be greater than 0");

        // Validate output topics
        if (config.OutputTopics.Any(topic => string.IsNullOrWhiteSpace(topic)))
            errors.Add("Output topics cannot contain empty values");

        return errors;
    }

    [HttpGet("{processorName}/debug")]
    public async Task<ActionResult> DebugConfiguration(string processorName)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationAsync(processorName);
            
            if (configuration == null)
            {
                return NotFound($"Configuration for processor '{processorName}' not found");
            }

            // Return detailed debug information
            return Ok(new 
            { 
                processorName = configuration.Name,
                inputTopic = configuration.InputTopic,
                outputTopics = configuration.OutputTopics,
                processorType = configuration.ProcessorType,
                maxConcurrency = configuration.MaxConcurrency,
                pollingIntervalSeconds = configuration.PollingIntervalSeconds,
                autoStart = configuration.AutoStart,
                customSettings = configuration.CustomSettings,
                customSettingsJson = System.Text.Json.JsonSerializer.Serialize(configuration.CustomSettings, new JsonSerializerOptions { WriteIndented = true }),
                debugInfo = new {
                    customSettingsType = configuration.CustomSettings?.GetType().Name,
                    customSettingsCount = configuration.CustomSettings?.Count ?? 0,
                    channelsType = configuration.CustomSettings?.ContainsKey("Channels") == true ? 
                        configuration.CustomSettings["Channels"]?.GetType().Name : "Not Found",
                    channelsValue = configuration.CustomSettings?.ContainsKey("Channels") == true ? 
                        configuration.CustomSettings["Channels"] : "Not Found"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to debug configuration for processor {ProcessorName}", processorName);
            return StatusCode(500, $"Debug failed: {ex.Message}");
        }
    }

    [HttpPost("force-reload")]
    public async Task<ActionResult> ForceReloadConfigurations()
    {
        try
        {
            await _configurationService.ReloadConfigurationsAsync();
            return Ok(new { message = "Configurations force reloaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force reload configurations");
            return StatusCode(500, "Internal server error");
        }
    }
}