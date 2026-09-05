using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق سند معماری Employee/Team/Labor Cost: جایگزین MaintenanceCrewController - Employee
/// دیگر این‌جا تکرار نمی‌شود (فقط EmployeeId ارجاع)، و منطق سه‌حالته‌ی عضویت (Reject/
/// Reactivate/New) از ITeamService می‌آید، نه مستقیم در Controller.
/// </summary>
public class TeamController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ITeamService _teamService;

    public TeamController(PEMSDbContext context, ITeamService teamService)
    {
        _context = context;
        _teamService = teamService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "تیم‌های تعمیرونگهداری";
        ViewData["Section"] = "CMMS Integration";

        var teams = await _context.Teams
            .Include(x => x.MaintenanceGroup).ThenInclude(g => g!.Discipline)
            .Include(x => x.SupervisorEmployee)
            .Include(x => x.Members.Where(m => m.IsActive)).ThenInclude(m => m.Employee)
            .Include(x => x.Members.Where(m => m.IsActive)).ThenInclude(m => m.Role)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        await LoadDropdownsAsync();

        return View(teams);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(string code, string name, int maintenanceGroupId, string? shift, int? supervisorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام تیم الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _context.Teams.AnyAsync(x => x.Name == name.Trim() || x.Code == code.Trim());
        if (duplicate)
        {
            TempData["Error"] = "تیمی با همین کد یا نام از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Teams.AddAsync(new Team
        {
            Code = code.Trim(),
            Name = name.Trim(),
            MaintenanceGroupId = maintenanceGroupId,
            Shift = shift,
            SupervisorEmployeeId = supervisorEmployeeId,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "تیم ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int teamId, int employeeId, int roleId)
    {
        try
        {
            await _teamService.AddOrReactivateMemberAsync(teamId, employeeId, roleId);
            TempData["Message"] = "عضو اضافه شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int teamMemberId)
    {
        try
        {
            await _teamService.RemoveMemberAsync(teamMemberId);
            TempData["Message"] = "عضو از تیم خارج شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveMember(int employeeId, int fromTeamId, int toTeamId, int roleId)
    {
        try
        {
            await _teamService.MoveMemberAsync(employeeId, fromTeamId, toTeamId, roleId);
            TempData["Message"] = "کارمند به تیم جدید منتقل شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ==============================================================================
    // برای فرم Work Order: با انتخاب تیم، فقط اعضای همون تیم (با نقششون) برگردونده بشه -
    // نه لیست کامل کارمندها
    // ==============================================================================
    [HttpGet]
    public async Task<IActionResult> GetTeamMembers(int teamId)
    {
        var members = await _context.TeamMembers
            .Include(x => x.Employee)
            .Include(x => x.Role)
            .Where(x => x.TeamId == teamId && x.IsActive)
            .Select(x => new
            {
                employeeId = x.EmployeeId,
                name = x.Employee!.FirstName + " " + x.Employee.LastName,
                role = x.Role!.Name
            })
            .ToListAsync();

        return Json(members);
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.MaintenanceGroups = await _context.MaintenanceGroups
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .Select(x => new { x.MaintenanceGroupId, x.Name, DisciplineName = x.Discipline!.Name })
            .ToListAsync();

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, FullName = x.FirstName + " " + x.LastName }).ToListAsync();

        ViewBag.MaintenanceRoles = await _context.MaintenanceRoles
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .Select(x => new { x.MaintenanceRoleId, x.Name, DisciplineName = x.Discipline!.Name })
            .ToListAsync();
    }
}
