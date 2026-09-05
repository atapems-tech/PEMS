using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10d: GasTest همیشه زیرمجموعه‌ی یک Permit است - هیچ Index مستقلی ندارد؛ همه‌ی
/// اکشن‌ها به Permit/Details/{permitId} برمی‌گردند. مجوزدهی از طریق IRoleModuleActionService
/// با Module="GasTest" (نه "Permit")، دقیقاً همون الگوی IsolationController.
/// </summary>
public class GasTestController : Controller
{
    private readonly IGasTestService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GasTestController(IGasTestService service, IRoleModuleActionService roleModuleActionService,
        PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _roleModuleActionService = roleModuleActionService;
        _context = context;
        _currentUser = currentUser;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int permitId, string location, DateTime testedAt, string? instrumentTag, DateTime? instrumentCalibrationDueDate,
        int? equipmentId, int? functionalLocationId,
        List<string> parameter, List<decimal> value, List<string> unit,
        decimal? otherValue, string? otherUnit)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "GasTest", "Create"))
            return Forbid();

        var testerEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!testerEmployeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر پیدا نشد.";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var readings = new List<GasReadingInputDto>();
        for (var i = 0; i < parameter.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(parameter[i])) continue;
            readings.Add(new GasReadingInputDto
            {
                Parameter = parameter[i],
                Value = i < value.Count ? value[i] : 0,
                Unit = i < unit.Count ? unit[i] : ""
            });
        }

        if (otherValue.HasValue && !string.IsNullOrWhiteSpace(otherUnit))
        {
            readings.Add(new GasReadingInputDto { Parameter = "Other", Value = otherValue.Value, Unit = otherUnit });
        }

        var (success, error, _) = await _service.CreateAsync(
            permitId, location, testedAt, testerEmployeeId.Value, instrumentTag, instrumentCalibrationDueDate,
            readings, equipmentId, functionalLocationId);

        TempData[success ? "Message" : "Error"] = success ? "تست گاز ثبت شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invalidate(int id, int permitId, string reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "GasTest", "Invalidate"))
            return Forbid();

        var (success, error) = await _service.InvalidateAsync(id, permitId, reason, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? "تست گاز باطل شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
