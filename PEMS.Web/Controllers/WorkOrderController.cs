using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: تکمیل بخش‌های CMMS - Work Order قلب واقعی CMMS است (کار نگهداری واقعی که
/// انجام می‌شود). گردش‌کار: Open → Assigned → InProgress → Completed → Closed. طبق درخواست
/// بازبینی: یک WorkOrder می‌تواند نیاز به ثبت نظر چند دیسیپلین تعمیراتی داشته باشد
/// (Multi-Craft Coordination، EN 13460) قبل از این‌که Completed بشه.
/// </summary>
public class WorkOrderController : Controller
{
    private static readonly string[] StatusFlow = { "Open", "Assigned", "InProgress", "Completed", "Closed" };

    // Canonical CMMS values. These values must match the Create/Edit UI and WorkOrder domain contract.

    private readonly PEMSDbContext _context;
    private readonly IWorkOrderReviewService _reviewService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IWorkOrderLaborService _laborService;
    private readonly IWorkOrderLifecycleService _lifecycleService;
    private readonly ICrewService _crewService;
    private readonly INumberingService _numberingService;
    private readonly IMaterialStockService _materialStockService;
    private readonly IWorkOrderSectionGuard? _sectionGuard;

    // sectionGuard اختیاری است تا سازنده‌ی ۹-پارامتری قبلی (تست‌ها) سازگار بماند؛ DI همیشه آن را تأمین می‌کند. بدون آن، تمام جهش‌های بخش‌های فرزند Fail-Closed رد می‌شوند.
    public WorkOrderController(PEMSDbContext context, IWorkOrderReviewService reviewService,
        ICurrentUserService currentUser, IPermissionService permissionService, IWorkOrderLaborService laborService,
        IWorkOrderLifecycleService lifecycleService, ICrewService crewService, INumberingService numberingService,
        IMaterialStockService materialStockService, IWorkOrderSectionGuard? sectionGuard = null)
    {
        _sectionGuard = sectionGuard;
        _context = context;
        _reviewService = reviewService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _laborService = laborService;
        _lifecycleService = lifecycleService;
        _crewService = crewService;
        _numberingService = numberingService;
        _materialStockService = materialStockService;
    }

    // SCRUM-831: the list (/p/work-orders, /p/work-orders-mine) and the new work order (/f/work-order-create) are definitions over IWorkOrderPagesOperations; the details page stays here.

    // SCRUM-831: the details page is the definition /p/work-order-details (IWorkOrderDetailsOperations and the work-order-* forms and actions); the legacy step is the work-order-advance action, the review request work-order-request-review.

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }

    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue) return null;
        return await _context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DisciplineId).FirstOrDefaultAsync();
    }

    private WoCaller BuildCaller(int? employeeId)
        => new(_currentUser.UserId, _currentUser.UserName, employeeId, _currentUser.Roles, HttpContext?.Connection.RemoteIpAddress?.ToString());

    private static bool IsFa() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

    private bool WantsJson()
        => Request.Headers.XRequestedWith == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// H25-H27: تنها نقطه‌ی بررسی «بخش × فاز × مجوز» برای تمام POSTهای جهشی. null = مجاز. رد = پیام مشخص و بومی‌شده؛ برای فرم‌های کلاسیک Toast + بازگشت به صفحه
    /// (PRG؛ تا صفحه‌ی خطای خالی نشان داده نشود) و برای فراخوان JSON/AJAX وضعیت واقعی HTTP 400/403/404 با بدنه‌ی JSON. هرگز بی‌صدا و هرگز 500.
    /// </summary>
    private async Task<IActionResult?> GuardChildAsync(int workOrderId, WoSection section, WoSectionAction action, string? reason = null, string? recordRef = null)
    {
        if (_sectionGuard == null)
        {
            TempData["Error"] = L("دروازه‌ی بخش‌های Work Order پیکربندی نشده است؛ تغییر مجاز نیست.", "The Work Order section guard is not configured; the change is not allowed.");
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        var result = await _sectionGuard.AuthorizeAsync(workOrderId, section, action, BuildCaller(await GetCurrentEmployeeIdAsync()), reason, recordRef);
        if (result.Allowed) return null;

        var message = result.Message(IsFa());
        Response.Headers["X-PEMS-Policy"] = result.Code;
        if (WantsJson()) return StatusCode(result.HttpStatus, new { code = result.Code, error = message, phase = result.Phase.ToString() });
        if (result.HttpStatus == 404) return NotFound();

        TempData["Error"] = message;
        TempData["PolicyCode"] = result.Code;
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    private async Task RecordChildChangeAsync(int workOrderId, WoSection section, WoSectionAction action, string? recordRef, string? reason, string? before, string? after)
    {
        if (_sectionGuard == null) return;
        try { await _sectionGuard.RecordChangeAsync(workOrderId, section, action, BuildCaller(await GetCurrentEmployeeIdAsync()), recordRef, reason, before, after); }
        catch (Exception) { _context.ChangeTracker.Clear(); /* ثبت ممیزی بهترین‌تلاش است؛ جهش قبلاً ذخیره شده */ }
    }

    /// <summary>B11: آخرین خط دفاع - هر استثنای پیش‌بینی‌نشده به Toast خطای مشخص تبدیل می‌شود (هرگز صفحه‌ی خطا/سکوت).</summary>
    private async Task<(bool Success, string? Error)> SafeAsync(Func<Task<(bool Success, string? Error)>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.ChangeTracker.Clear();
            return (false, L("عملیات انجام نشد (خطای غیرمنتظره): ", "The operation could not be completed (unexpected error): ") + ex.GetBaseException().Message);
        }
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

    private static string L(string fa, string en)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? fa : en;

    private async Task<WorkOrderActor> BuildActorAsync() => new(await GetCurrentEmployeeIdAsync(), _currentUser.Roles);

    private static string? GetNextStatus(string current)
    {
        var index = Array.IndexOf(StatusFlow, current);
        if (index < 0 || index >= StatusFlow.Length - 1) return null;
        return StatusFlow[index + 1];
    }
}
