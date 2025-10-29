using Microsoft.AspNetCore.Mvc;
using Processors.Interfaces;
using Processors.Models;

namespace Processors.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessorsController : ControllerBase
{
    private readonly IProcessorEngine _processorEngine;
    private readonly ILogger<ProcessorsController> _logger;

    public ProcessorsController(IProcessorEngine processorEngine, ILogger<ProcessorsController> logger)
    {
        _processorEngine = processorEngine;
        _logger = logger;
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<IEnumerable<ProcessorStatistics>>> GetAllStatistics()
    {
        try
        {
            var statistics = await _processorEngine.GetAllStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processor statistics");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{processorName}/statistics")]
    public async Task<ActionResult<ProcessorStatistics>> GetStatistics(string processorName)
    {
        try
        {
            var statistics = await _processorEngine.GetStatisticsAsync(processorName);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{processorName}/start")]
    public async Task<ActionResult> StartProcessor(string processorName)
    {
        try
        {
            await _processorEngine.StartAsync(processorName);
            return Ok(new { message = $"Processor {processorName} started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{processorName}/stop")]
    public async Task<ActionResult> StopProcessor(string processorName)
    {
        try
        {
            await _processorEngine.StopAsync(processorName);
            return Ok(new { message = $"Processor {processorName} stopped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{processorName}/status")]
    public ActionResult<object> GetStatus(string processorName)
    {
        try
        {
            var isRunning = _processorEngine.IsRunning(processorName);
            return Ok(new { processorName, isRunning });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for processor {ProcessorName}", processorName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("all-configured")]
    public async Task<ActionResult<IEnumerable<object>>> GetAllConfiguredProcessors()
    {
        try
        {
            var configuredProcessors = await _processorEngine.GetConfiguredProcessorNamesAsync();
            var result = new List<object>();
            
            foreach (var processorName in configuredProcessors)
            {
                var statistics = await _processorEngine.GetStatisticsAsync(processorName);
                var isRunning = _processorEngine.IsRunning(processorName);
                
                result.Add(new 
                {
                    name = processorName,
                    isRunning = isRunning,
                    isConfigured = true,
                    statistics = statistics
                });
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all configured processors");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("start-all-auto-start")]
    public async Task<ActionResult> StartAllAutoStartProcessors()
    {
        try
        {
            await _processorEngine.StartAllAutoStartProcessorsAsync();
            return Ok(new { message = "All auto-start processors have been started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start all auto-start processors");
            return StatusCode(500, "Internal server error");
        }
    }
}