using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// Engineering Document Intelligence: طراحی UI و جریان کار مطابق همان الگوی موفق VDR -
/// Dashboard + صف بازبینی + صفحه‌ی Review با تایید/رد هر پیشنهاد AI + Comment Management.
/// Upload → AI Processing → Classification → Discipline Assignment → Engineering Review →
/// Comment → Revision → Approval → Knowledge Base
/// </summary>
/// <remarks>
/// طبق «PEMS — P0 AI Pipeline Endpoint Hardening»: این Controller قبلاً هیچ بررسی مجوزی نداشت -
/// ApproveClassification (که مستقیم روی فیلدهای خودِ EngineeringDocument می‌نویسد:
/// Discipline/Vendor/MR/RevisionNo/IssuePurpose) و AdvanceStage برای هر کاربر احرازهویت‌شده‌ای
/// باز بودند. طبق بند ۹ راهنمای ممیزی، ماژول جدیدی ساخته نشد - همون ماژول موجود
/// "EngineeringDocuments" استفاده شد چون این Controller دقیقاً همون موجودیت (EngineeringDocument)
/// را در مرحله‌ی بعدتر از چرخه‌ی عمرش تغییر می‌دهد؛ مرز تایید (Approve) هم از قبل دقیقاً همون
/// چیزی است که باید باشد: فقط EngineeringManager/EngineeringSupervisor روی این ماژول
/// CanApprove=true دارند (تفکیک وظایف - DisciplineEngineer/DocumentController تدوین/پردازش
/// می‌کنند اما تایید نهایی نمی‌دهند - دقیقاً همون قاعده‌ای که برای خودِ EngineeringDocuments از
/// قبل جاافتاده).
/// </remarks>
public class EngineeringDocumentIntelligenceController : Controller
{
    private readonly IEngineeringDocumentIntelligenceService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public EngineeringDocumentIntelligenceController(
        IEngineeringDocumentIntelligenceService service,
        ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _service = service;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "View"))
            return Forbid();

        ViewData["Title"] = "Document Intelligence";
        ViewData["Section"] = "AI Engineering Intelligence";

        var dashboard = await _service.GetDashboardAsync();
        var pending = await _service.GetPendingReviewAsync();

        ViewBag.Dashboard = dashboard;

        return View(pending);
    }

    public async Task<IActionResult> Review(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "View"))
            return Forbid();

        ViewData["Title"] = "Document Intelligence Review";
        ViewData["Section"] = "AI Engineering Intelligence";

        var detail = await _service.GetDetailAsync(id);
        if (detail == null) return NotFound();

        return View(detail);
    }

}
