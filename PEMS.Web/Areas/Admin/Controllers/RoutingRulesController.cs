using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// مدیریت جدول Routing (Category → Module) و آستانه‌های Confidence Gate از UI،
/// طبق قانون "Workflow Engine باید Configurable باشد، نه Hardcode".
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class RoutingRulesController : Controller
{
    private readonly IRoutingRuleService _service;

    public RoutingRulesController(IRoutingRuleService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "AI Routing Rules";
        ViewData["Section"] = "Administration";

        var rules = await _service.GetAllAsync();
        var confidence = await _service.GetConfidenceSettingsAsync();

        ViewBag.Confidence = confidence;
        ViewBag.Modules = new[] { "MDR", "VDR", "MR", "MRQ", "Contract", "CMMS", "Unrouted" };

        return View(rules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRule(string category, string targetModule, bool allowAutoRouting, string? description)
    {
        await _service.SetRuleAsync(category, targetModule, allowAutoRouting, description);
        TempData["Message"] = $"Updated routing rule for {category}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetConfidence(double highThreshold, double mediumThreshold)
    {
        await _service.UpdateConfidenceSettingsAsync(highThreshold, mediumThreshold);
        TempData["Message"] = "Confidence thresholds updated";
        return RedirectToAction(nameof(Index));
    }
}
