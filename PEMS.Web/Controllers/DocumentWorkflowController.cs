using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// داشبورد سراسری گردش کار مدارک: مدارک در انتظار تایید در کل سازمان + تاریخچه‌ی تغییرات وضعیت.
/// </summary>
public class DocumentWorkflowController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentWorkflowService _workflowService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IAccessControlService _accessControlService;

    public DocumentWorkflowController(PEMSDbContext context, IDocumentWorkflowService workflowService,
        ICurrentUserService currentUser, IPermissionService permissionService, IAccessControlService accessControlService)
    {
        _context = context;
        _workflowService = workflowService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _accessControlService = accessControlService;
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

    // طبق «PEMS — Global Grid Hardening» P1: قبلاً صف تایید (Submitted/Checked) بدون Skip/Take
    // با ۲ Include بارگذاری می‌شد - در سازمانی با انباشت مدارک منتظر تایید، این صف نامحدود
    // رشد می‌کند. فیلتر status موجود حفظ شد؛ فقط شمارش/مرتب‌سازی/صفحه‌بندی/Projection در SQL
    // اضافه شدند. "Recent Workflow History" (Take(30) ثابت) یک ویجت فعالیت اخیر است، نه یک
    // Grid کامل - دست‌نخورده ماند.
    public async Task<IActionResult> Index(string? status, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Document Workflow";
        ViewData["Section"] = "Engineering Management";

        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        else
            // طبق بررسی چرخه‌ی مدرک: حالا Submitted (منتظر Check) و Checked (منتظر Approve) هر
            // دو مرحله‌ی جداگانه‌ای هستند که باید در صف نشان داده شوند
            query = query.Where(x => x.Status == "Submitted" || x.Status == "Checked");

        var totalCount = await query.CountAsync();
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var pendingItems = await query
            .OrderBy(x => x.ModifiedDate ?? x.CreatedDate)
            .ThenBy(x => x.DocumentId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new DocumentWorkflowPendingItemDto
            {
                DocumentId = x.DocumentId,
                DocumentNo = x.DocumentNo,
                Title = x.Title,
                ProjectName = x.Project != null ? x.Project.ProjectName : null,
                DisciplineName = x.Discipline != null ? x.Discipline.Name : null,
                Status = x.Status,
                RevisionNo = x.RevisionNo
            })
            .ToListAsync();

        var pending = new PagedResult<DocumentWorkflowPendingItemDto>
        {
            Items = pendingItems,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };

        var history = await _context.DocumentWorkflows
            .Include(x => x.Document)
            .Include(x => x.ActionBy)
            .OrderByDescending(x => x.ActionDate)
            .Take(30)
            .ToListAsync();

        ViewBag.History = history;
        ViewBag.SelectedStatus = status;

        return View(pending);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check(int documentId, string? comment)
    {
        try
        {
            await _workflowService.CheckAsync(
                documentId, comment, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "Document checked - ready for approval";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int documentId, string? comment)
    {
        // طبق «PEMS EDMS Phase 4 Verification» یافته P4-04: این کنترلر یک مسیر دوم و کاملاً
        // ناشناخته برای همان EngineeringDocumentsController.ApproveAsync بود - بدون هیچ بررسی
        // مجوز و بدون بررسی SoD (که در EngineeringDocumentsController.Approve همین فاز اضافه
        // شد). همان دو محافظت این‌جا هم اعمال می‌شود.
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Approve"))
            return Forbid();

        if (_currentUser.UserId.HasValue)
        {
            var sodDecision = await _accessControlService.AuthorizeAsync(new AccessControlRequest
            {
                UserId = _currentUser.UserId.Value,
                ResourceName = "EngineeringDocuments",
                ActionName = "Approve",
                ResourceId = documentId.ToString(),
                IpAddress = GetClientIp()
            });

            if (!sodDecision.Allowed && sodDecision.Reason == AccessDecisionReason.DeniedBySoD)
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(sodDecision.Detail)
                    ? "طبق سیاست تفکیک وظایف، شما نمی‌توانید مدرکی را که خودتان ایجاد/ویرایش کرده‌اید تایید کنید."
                    : sodDecision.Detail;
                return RedirectToAction(nameof(Index));
            }
        }

        try
        {
            await _workflowService.ApproveAsync(
                documentId, comment, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "Document approved";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int documentId, string? comment)
    {
        try
        {
            await _workflowService.RejectAsync(
                documentId, comment, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "Document rejected";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
