using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 8 — Operational Maintenance &amp; Reliability Data Activation» بند ۲۱/۲۲:
/// یک داشبورد واحد که شمارنده‌های وضعیت WorkRequest و WorkOrder را کنار هم نشان می‌دهد (که در
/// ممیزی مشخص شد هیچ‌جای دیگری موجود نبود)، به‌علاوه یک Backlog نگهداری صفحه‌بندی‌شده. فقط
/// گزارشی/فقط‌خواندنی - هیچ Mutation یا منطق چرخه‌ی عمر جدیدی این‌جا نیست (کنترلرهای موجود
/// WorkOrderController/WorkRequestController مسئول آن باقی می‌مانند).
///
/// روی همان ماژول مجوز "WorkOrder" موجود سوار شده (همان الگویی که KpiController با ماژول
/// "Reliability" موجود استفاده کرد) - هیچ ماژول مجوز جدیدی برای یک داشبورد صرفاً-گزارشی لازم
/// نیست.
/// </summary>
public class CmmsOperationsController : Controller
{
    private readonly ICmmsOperationsService _operationsService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public CmmsOperationsController(ICmmsOperationsService operationsService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _operationsService = operationsService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View"))
            return Forbid();

        ViewData["Title"] = "CMMS Operations Dashboard";
        ViewData["Section"] = "CMMS Integration";

        var dashboard = await _operationsService.GetDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> Backlog(string? status, string? priority, int? equipmentId,
        bool overdueOnly = false, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Backlog";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.EquipmentId = equipmentId;
        ViewBag.OverdueOnly = overdueOnly;

        var result = await _operationsService.GetBacklogAsync(status, priority, equipmentId, overdueOnly, page, pageSize);
        return View(result);
    }
}
