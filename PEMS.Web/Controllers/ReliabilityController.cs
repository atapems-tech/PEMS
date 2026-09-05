using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-06 (Reliability): نمای ناوگانی MTBF/MTTR/Availability،
/// محاسبه‌شده از روی FailureRecord های واقعی (IReliabilityService).
/// </summary>
public class ReliabilityController : Controller
{
    private readonly IReliabilityService _reliabilityService;

    public ReliabilityController(IReliabilityService reliabilityService)
    {
        _reliabilityService = reliabilityService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Reliability Analysis";
        ViewData["Section"] = "CMMS Integration";

        var fleet = await _reliabilityService.GetFleetReliabilityAsync();
        return View(fleet);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Equipment Reliability";
        ViewData["Section"] = "CMMS Integration";

        var reliability = await _reliabilityService.GetEquipmentReliabilityAsync(id);
        if (reliability == null) return NotFound();

        return View(reliability);
    }
}
