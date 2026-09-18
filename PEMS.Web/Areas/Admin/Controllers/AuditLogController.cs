using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class AuditLogController : Controller
{
    private readonly PEMSDbContext _context;

    public AuditLogController(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? entityName, string? userName, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Audit Log";
        ViewData["HelpKey"] = "Admin.AuditLog.Index";
        ViewData["Section"] = "Administration";

        // طبق سند Global Data Grid Standard بند ۴/۳۶: AuditLog با ~۲۰۵۰۰۰ ردیف دقیقاً همان
        // Grid با اولویت HOT است که سند به‌صراحت ذکر کرده - فقط ۲۰/۵۰/۱۰۰ پذیرفته می‌شود
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(x => x.EntityName == entityName);

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(x => x.UserName == userName);

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.AuditLogId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        ViewBag.EntityNames = await _context.AuditLogs.Select(x => x.EntityName).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.SelectedEntityName = entityName;
        ViewBag.SelectedUserName = userName;

        return View(new PagedResult<AuditLog>
        {
            Items = logs,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = total
        });
    }
}
