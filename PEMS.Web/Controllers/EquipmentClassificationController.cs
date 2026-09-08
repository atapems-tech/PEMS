using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification» بند ۳۳: گزارش قابل‌بازبینی Backfill - نمایش
/// می‌دهد کدام Equipment به‌صورت خودکار طبقه‌بندی شد (Applied=true، در جدول واقعی هم قبلاً اعمال
/// شده) و کدام نیازمند بازبینی انسانی است (بدون قاعده‌ی قطعی). فقط‌خواندنی است - هیچ Mutation ای
/// این‌جا انجام نمی‌شود؛ Backfill واقعی در استارتاپ برنامه (Program.cs) اجرا می‌شود. طبق بند ۴۲:
/// از همان ماژول مجوز "MaintenanceMasterData" استفاده می‌کند (بدون ماژول مجوز جدید).
/// </summary>
public class EquipmentClassificationController : Controller
{
    private readonly IEquipmentClassificationService _classificationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public EquipmentClassificationController(IEquipmentClassificationService classificationService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _classificationService = classificationService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Equipment Classification Report";
        ViewData["Section"] = "CMMS Integration";

        var report = await _classificationService.GetClassificationReportAsync();
        return View(report);
    }
}
