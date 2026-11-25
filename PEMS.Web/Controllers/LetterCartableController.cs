using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// کارتابل شخصی نامه‌نگاری: برچسب‌ها (درخت)، تنظیمات، تفویض اختیار، قانون‌های مسیریابی، مخاطبین/گروه‌ها، یادآورها و گزارش/SLA.
/// فقط لایه‌ی HTTP: همه‌ی قواعد در سرویس‌های Application/Persistence است و همه‌ی داده‌ها شخصیِ کاربر جاری‌اند
/// (به‌جز موارد سازمانی که مجوز Approve لازم دارند). پاسخ‌های JSON متن خطا را به زبان صفحه برمی‌گردانند.
/// </summary>
public class LetterCartableController : Controller
{
    private readonly ILetterTagTreeService _tags;
    private readonly ILetterUserSettingsService _settings;
    private readonly ILetterDelegationService _delegations;
    private readonly ILetterRoutingRuleService _rules;
    private readonly ILetterContactService _contacts;
    private readonly ILetterPersonalReminderService _reminders;
    private readonly ILetterReportService _reports;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;
    private readonly PEMSDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LetterCartableController(ILetterTagTreeService tags, ILetterUserSettingsService settings, ILetterDelegationService delegations,
        ILetterRoutingRuleService rules, ILetterContactService contacts, ILetterPersonalReminderService reminders, ILetterReportService reports,
        ICurrentUserService currentUser, IPermissionService permissions, PEMSDbContext context, IStringLocalizer<SharedResource> localizer)
    {
        _tags = tags;
        _settings = settings;
        _delegations = delegations;
        _rules = rules;
        _contacts = contacts;
        _reminders = reminders;
        _reports = reports;
        _currentUser = currentUser;
        _permissions = permissions;
        _context = context;
        _localizer = localizer;
    }

    // ------------------------------------------------------------------ helpers
    private int UserId => _currentUser.UserId ?? 0;
    private bool IsAdmin => _currentUser.Roles.Contains("SystemAdmin");

    private async Task<bool> CanApproveAsync() => IsAdmin || await _permissions.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

    private string T(string? key) => string.IsNullOrEmpty(key) ? "" : (_localizer[key].ResourceNotFound ? key : _localizer[key].Value);

    // ------------------------------------------------------------------ تنظیمات نمایشی
    [HttpGet]
    public async Task<IActionResult> Settings() => Json(await _settings.GetAsync(UserId));

    // ------------------------------------------------------------------ برچسب‌ها
    [HttpGet]
    public async Task<IActionResult> Tags()
        => Json(await _tags.GetTreeAsync(UserId, await CanApproveAsync(), withCounts: true, await CanApproveAsync()));

    // ------------------------------------------------------------------ تفویض اختیار
    [HttpGet]
    public async Task<IActionResult> Delegations()
    {
        var (given, received) = await _delegations.GetAsync(UserId);
        return Json(new { given, received, candidates = await _delegations.GetCandidatesAsync(UserId) });
    }

    // ------------------------------------------------------------------ قانون‌های مسیریابی
    [HttpGet]
    public async Task<IActionResult> Rules()
    {
        var lookups = new
        {
            projects = await _context.Projects.Where(p => p.IsActive).OrderBy(p => p.ProjectName).Select(p => new { id = p.ProjectId, name = p.ProjectName }).Take(300).ToListAsync(),
            disciplines = await _context.Disciplines.Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new { id = d.DisciplineId, name = d.Name }).ToListAsync()
        };
        return Json(new { rules = await _rules.GetAsync(UserId), lookups });
    }

    // ------------------------------------------------------------------ مخاطبین و گروه‌ها
    [HttpGet]
    public async Task<IActionResult> Contacts(string? q) => Json(await _contacts.SearchAsync(q));

    [HttpGet]
    public async Task<IActionResult> Groups() => Json(await _contacts.GetGroupsAsync(UserId, await CanApproveAsync()));

    // ------------------------------------------------------------------ یادآورها
    [HttpGet]
    public async Task<IActionResult> Reminders(bool includeDone = false) => Json(await _reminders.GetAsync(UserId, includeDone));

    // ------------------------------------------------------------------ گزارش و SLA
    public async Task<IActionResult> Report(DateTime? from, DateTime? to)
    {
        ViewData["Title"] = "Ct_ReportTitle";
        ViewData["Section"] = "Correspondence Management";

        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-89)).Date;
        if (end < start) (start, end) = (end, start);
        if ((end - start).TotalDays > 730) start = end.AddDays(-730);

        var model = await _reports.GetAsync(UserId, start, end, await CanApproveAsync());
        return View(model);
    }
}
