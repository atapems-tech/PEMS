using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// فهرست فقط‌خواندنی رویدادهای امنیتی بازیابی حساب مدیر (SCRUM-357). منطق در <see cref="IRecoveryEventService"/>؛
/// این کنترلر فقط فیلتر و صفحه‌بندی را می‌گذراند. هیچ عملیات نوشتنی (ویرایش/حذف) ندارد.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class SecurityEventsController : Controller
{
    private readonly IRecoveryEventService _events;

    public SecurityEventsController(IRecoveryEventService events) => _events = events;

    public async Task<IActionResult> Index(string? method, string? result, int page = 1, int? pageSize = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "Security Events";
        ViewData["Section"] = "Administration";

        // روش/نتیجه فقط از مقادیر شناخته‌شده پذیرفته می‌شود (ورودی آزاد به کوئری نمی‌رسد)
        var methodFilter = RecoveryMethods.IsKnown(method) ? method : null;
        var resultFilter = result is RecoveryResults.Success or RecoveryResults.Failure ? result : null;

        var model = await _events.GetEventsAsync(methodFilter, resultFilter, page < 1 ? 1 : page, DataGridPageSize.Normalize(pageSize), ct);

        ViewBag.Method = methodFilter;
        ViewBag.Result = resultFilter;
        return View(model);
    }
}
