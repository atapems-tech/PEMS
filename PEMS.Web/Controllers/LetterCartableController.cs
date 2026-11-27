using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// کارتابل شخصی نامه‌نگاری: برچسب‌ها (درخت)، تنظیمات، تفویض اختیار، قانون‌های مسیریابی، مخاطبین/گروه‌ها، یادآورها و گزارش/SLA.
/// فقط لایه‌ی HTTP: همه‌ی قواعد در سرویس‌های Application/Persistence است و همه‌ی داده‌ها شخصیِ کاربر جاری‌اند
/// (به‌جز موارد سازمانی که مجوز Approve لازم دارند). SCRUM-8c: خواندن‌های JSON اکنون عملیات‌اند (/x/cartable-*)؛ فقط گزارش مانده است.
/// </summary>
public class LetterCartableController : Controller
{
    private readonly ILetterReportService _reports;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;

    public LetterCartableController(ILetterReportService reports, ICurrentUserService currentUser, IPermissionService permissions)
    {
        _reports = reports;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    private int UserId => _currentUser.UserId ?? 0;
    private bool IsAdmin => _currentUser.Roles.Contains("SystemAdmin");

    private async Task<bool> CanApproveAsync() => IsAdmin || await _permissions.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

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
