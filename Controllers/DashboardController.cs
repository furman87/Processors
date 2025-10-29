using Microsoft.AspNetCore.Mvc;
using Processors.Interfaces;

namespace Processors.Controllers;

public class DashboardController : Controller
{
    private readonly IProcessorEngine _processorEngine;

    public DashboardController(IProcessorEngine processorEngine)
    {
        _processorEngine = processorEngine;
    }

    public async Task<IActionResult> Index()
    {
        var statistics = await _processorEngine.GetAllStatisticsAsync();
        return View(statistics);
    }
}