using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

// ==============================================================================
// Engineering Copilot Dashboard - طبق فاز ۴ نقشه‌راه معماری PEMS.
// ==============================================================================
public class AIInsightsController : Controller
{
    private readonly IEngineeringCopilotService _copilot;

    public AIInsightsController(IEngineeringCopilotService copilot)
    {
        _copilot = copilot;
    }

    public async Task<IActionResult> Index(int? projectId)
    {
        ViewData["Title"] = "Engineering Copilot";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.NcrPatterns = await _copilot.GetRecurringNcrPatternsAsync(projectId);
        ViewBag.DelayAnalysis = await _copilot.GetDelayRootCauseAnalysisAsync(projectId);

        return View();
    }
}
