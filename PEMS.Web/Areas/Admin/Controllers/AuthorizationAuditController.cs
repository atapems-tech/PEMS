using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۲۲: نمایشگر اختصاصی
/// AuthorizationAuditLog (تصمیم‌های مجوزدهی) - جدا از AuditLogController موجود (که فقط تغییرات
/// سطح موجودیت را نشان می‌دهد). دو مکانیزم Audit عمداً ادغام نشده‌اند (طبق گزارش معماری §H).
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class AuthorizationAuditController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthorizationAuditController(PEMSDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(
        int? userId, string? resourceName, string? actionName, string? decision, string? resourceId,
        DateTime? fromDate, DateTime? toDate, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Authorization Audit";
        ViewData["Section"] = "Administration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var query = _context.AuthorizationAuditLogs.AsQueryable();

        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(resourceName)) query = query.Where(x => x.ResourceName == resourceName);
        if (!string.IsNullOrWhiteSpace(actionName)) query = query.Where(x => x.ActionName == actionName);
        if (!string.IsNullOrWhiteSpace(decision)) query = query.Where(x => x.Decision == decision);
        if (!string.IsNullOrWhiteSpace(resourceId)) query = query.Where(x => x.ResourceId == resourceId);
        if (fromDate.HasValue) query = query.Where(x => x.Timestamp >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.Timestamp < toDate.Value.AddDays(1));

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.AuthorizationAuditLogId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        var userIds = logs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
        var userNames = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? $"#{u.Id}");
        ViewBag.UserNames = userNames;

        ViewBag.ResourceNames = await _context.AuthorizationAuditLogs.Select(x => x.ResourceName).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Decisions = await _context.AuthorizationAuditLogs.Select(x => x.Decision).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.SelectedUserId = userId;
        ViewBag.SelectedResourceName = resourceName;
        ViewBag.SelectedActionName = actionName;
        ViewBag.SelectedDecision = decision;
        ViewBag.SelectedResourceId = resourceId;
        ViewBag.SelectedFromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.SelectedToDate = toDate?.ToString("yyyy-MM-dd");

        return View(new PagedResult<AuthorizationAuditLog>
        {
            Items = logs,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = total
        });
    }
}
