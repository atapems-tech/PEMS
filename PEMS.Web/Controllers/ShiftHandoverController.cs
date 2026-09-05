using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10e: ShiftHandover والد Permit نیست - ماژول بالادستی مستقل خودش را دارد (نه یک
/// کارت روی Permit/Details)، دقیقاً همون شکل PermitController. مجوزدهی درشت از طریق
/// Module="ShiftHandover"؛ محدودیت واقعی Prepare/Acknowledge/Complete در ShiftHandoverService
/// از طریق تایید هویت دقیق کارمند (Outgoing/Incoming) اعمال می‌شود.
/// </summary>
public class ShiftHandoverController : Controller
{
    private readonly IShiftHandoverService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ShiftHandoverController(IShiftHandoverService service, IRoleModuleActionService roleModuleActionService,
        PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _roleModuleActionService = roleModuleActionService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? status)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "View"))
            return Forbid();

        ViewData["Title"] = "Shift Handovers";
        ViewData["Section"] = "CMMS Integration";

        var items = await _service.GetAllAsync(status);
        ViewBag.SelectedStatus = status;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "Create"))
            return Forbid();

        ViewData["Title"] = "New Shift Handover";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .OrderBy(x => x.FirstName)
            .Select(x => new { x.EmployeeId, Label = x.FirstName + " " + x.LastName })
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int outgoingEmployeeId, int incomingEmployeeId, string? shiftLabel, DateTime handoverDate, string? notes)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "Create"))
            return Forbid();

        var (success, error, id) = await _service.CreateAsync(outgoingEmployeeId, incomingEmployeeId, shiftLabel, handoverDate, notes);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create));
        }

        TempData["Message"] = "تحویل شیفت ساخته شد.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "View"))
            return Forbid();

        var handover = await _service.GetDetailsAsync(id);
        if (handover == null) return NotFound();

        ViewData["Title"] = "Shift Handover Details";
        ViewData["Section"] = "CMMS Integration";

        var currentEmployeeId = await GetCurrentEmployeeIdAsync();
        ViewBag.IsOutgoing = currentEmployeeId.HasValue && currentEmployeeId.Value == handover.OutgoingEmployeeId;
        ViewBag.IsIncoming = currentEmployeeId.HasValue && currentEmployeeId.Value == handover.IncomingEmployeeId;

        return View(handover);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Prepare(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "Prepare"))
            return Forbid();

        var (success, error) = await _service.PrepareAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "تحویل شیفت آماده شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "Acknowledge"))
            return Forbid();

        var (success, error) = await _service.AcknowledgeAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "تحویل شیفت تایید شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ShiftHandover", "Complete"))
            return Forbid();

        var (success, error) = await _service.CompleteAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "تحویل شیفت نهایی شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
