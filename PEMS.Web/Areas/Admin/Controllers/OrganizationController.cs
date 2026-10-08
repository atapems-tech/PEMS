using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.Organization;
using PEMS.Persistence.Identity;
using PEMS.Web.Resources;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// مدیریت سازمان: سلسله‌مراتب سمت‌ها (ParentPosition)، دارندگان، سمت اصلی/فرعی و تاریخ‌های اعتبار + صفحه‌ی Diagnostics.
/// مرجع «مدیر مستقیم» گردش‌کارها همین داده است. هیچ ApproverId/ManagerId/PositionId از مرورگر به‌عنوان «مدیر» پذیرفته نمی‌شود؛
/// فقط ساختار (سمت↔مافوق، سمت↔دارنده) ویرایش می‌شود و آن هم فقط توسط SystemAdmin و با اعتبارسنجی سمت سرور.
/// کنترلر نازک است؛ منطق در IOrganizationHierarchyService / IOrganizationValidationService.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class OrganizationController : Controller
{
    private readonly IOrganizationHierarchyService _hierarchy;
    private readonly IOrganizationValidationService _validation;
    private readonly IOrganizationConsistencyService _consistency;
    private readonly IStringLocalizer<SharedResource> _l;

    public OrganizationController(IOrganizationHierarchyService hierarchy, IOrganizationValidationService validation, IStringLocalizer<SharedResource> l, IOrganizationConsistencyService consistency)
    {
        _hierarchy = hierarchy;
        _validation = validation;
        _consistency = consistency;
        _l = l;
    }

    private void Flash(OrgOpResult r, string okKey)
    {
        if (r.Success) TempData["Message"] = _l[okKey].Value;
        else TempData["Error"] = _l[r.ErrorKey ?? "Org_Err_Generic"].Value + (string.IsNullOrEmpty(r.Detail) ? "" : " " + r.Detail);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Organization Structure";
        ViewData["HelpKey"] = "Admin.Organization.Index";
        ViewData["Section"] = "Administration";
        ViewBag.Employees = await _hierarchy.GetActiveEmployeesAsync();
        var rows = await _hierarchy.GetPositionRowsAsync();
        var issues = await _validation.ValidateAllAsync();
        foreach (var r in rows) r.IssueCount = issues.Count(i => i.PositionId == r.PositionId && i.Severity != "P3");
        return View(rows);
    }

    /// <summary>نمودار سازمانی: فقط نمایش (بدون هیچ جهش). داده از همان مدل Position/EmployeeAssignment؛ Resolver و Workflow دست‌نخورده.</summary>
    [HttpGet]
    public IActionResult Chart()
    {
        ViewData["Title"] = "Organization Chart";
        ViewData["HelpKey"] = "Admin.Organization.Index";
        ViewData["Section"] = "Administration";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ChartData() => Json(await _hierarchy.GetChartAsync());

    [HttpGet]
    public async Task<IActionResult> Diagnostics()
    {
        ViewData["Title"] = "Organization Diagnostics";
        ViewData["HelpKey"] = "Admin.Organization.Index";
        ViewData["Section"] = "Administration";
        ViewBag.Blocked = await _hierarchy.GetBlockedRequestsAsync();
        ViewBag.Readiness = await _hierarchy.GetReadinessAsync();
        ViewBag.DeptConsistency = await _consistency.GetDepartmentConsistencyAsync();
        ViewBag.LegacyPositions = await _consistency.GetLegacyPositionConflictsAsync();
        return View(await _validation.ValidateAllAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetHierarchy(int positionId, string? positionCode, int? parentPositionId)
    {
        Flash(await _hierarchy.SetPositionHierarchyAsync(positionId, positionCode, parentPositionId), "Org_Msg_HierarchySaved");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int positionId, int employeeId, bool isPrimary, DateTime? startDate, DateTime? endDate, string? notes)
    {
        Flash(await _hierarchy.AssignAsync(new OrganizationAssignmentRequest(positionId, employeeId, isPrimary, startDate ?? DateTime.Now, endDate, notes)), "Org_Msg_Assigned");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssignment(int assignmentId, bool isPrimary, DateTime startDate, DateTime? endDate)
    {
        Flash(await _hierarchy.UpdateAssignmentAsync(assignmentId, isPrimary, startDate, endDate), "Org_Msg_AssignmentUpdated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EndAssignment(int assignmentId, string? notes)
    {
        Flash(await _hierarchy.EndAssignmentAsync(assignmentId, notes), "Org_Msg_AssignmentEnded");
        return RedirectToAction(nameof(Index));
    }
}
