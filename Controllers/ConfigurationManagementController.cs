using Microsoft.AspNetCore.Mvc;
using Processors.Services;

namespace Processors.Controllers;

public class ConfigurationManagementController : Controller
{
    private readonly IProcessorConfigurationService _configurationService;

    public ConfigurationManagementController(IProcessorConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IActionResult> Index()
    {
        var configurations = await _configurationService.GetAllConfigurationsAsync();
        return View(configurations);
    }

    public IActionResult Create()
    {
        return View();
    }

    public async Task<IActionResult> Edit(string name)
    {
        var configuration = await _configurationService.GetConfigurationAsync(name);
        if (configuration == null)
        {
            return NotFound();
        }
        return View(configuration);
    }
}