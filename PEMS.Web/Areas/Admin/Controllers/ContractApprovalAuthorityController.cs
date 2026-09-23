using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۳): مدیریت ماتریس اختیار تایید قرارداد.
/// هیچ آستانه‌ی پولی از پیش Seed نشده - فقط یک صفحه‌ی مدیریتی برای چیزی که یک SystemAdmin صریحاً
/// تعریف می‌کند، دقیقاً هم‌الگو با RoutingRulesController/PermissionMatrixController.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class ContractApprovalAuthorityController : Controller
{
    private readonly IContractApprovalAuthorityRuleService _service;

    public ContractApprovalAuthorityController(IContractApprovalAuthorityRuleService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Contract Approval Authority";
        ViewData["Section"] = "Administration";

        var rules = await _service.GetAllAsync();
        ViewBag.Roles = SystemRoles.All;

        return View(rules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(decimal? minValue, decimal? maxValue, string? criticalityClass,
        string requiredApproverRole, string? description, int priority)
    {
        var result = await _service.CreateAsync(minValue, maxValue, criticalityClass, requiredApproverRole, description, priority);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Rule created";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int ruleId, decimal? minValue, decimal? maxValue, string? criticalityClass,
        string requiredApproverRole, string? description, int priority)
    {
        var result = await _service.UpdateAsync(ruleId, minValue, maxValue, criticalityClass, requiredApproverRole, description, priority);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Rule updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int ruleId, bool isActive)
    {
        var result = await _service.SetActiveAsync(ruleId, isActive);
        if (!result.Success) TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }
}
