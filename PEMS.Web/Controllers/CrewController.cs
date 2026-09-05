using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۴: ترکیب اجرایی نفرات برای
/// یک فعالیت مشخص - برخلاف Team (واحد سازمانی پایدار)، می‌تواند به یک WorkOrder مشخص وصل
/// باشد یا مستقل/پایدار بماند. همون الگوی TeamController (منطق سه‌حالته از ICrewService).
/// </summary>
public class CrewController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICrewService _crewService;

    public CrewController(PEMSDbContext context, ICrewService crewService)
    {
        _context = context;
        _crewService = crewService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Crews";
        ViewData["Section"] = "CMMS Integration";

        var crews = await _context.Crews
            .Include(x => x.WorkOrder)
            .Include(x => x.Members.Where(m => m.IsActive)).ThenInclude(m => m.Employee)
            .Include(x => x.Members.Where(m => m.IsActive)).ThenInclude(m => m.Role)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        await LoadDropdownsAsync();

        return View(crews);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCrew(string code, string name, int? workOrderId)
    {
        var (success, error, _) = await _crewService.CreateAsync(code, name, workOrderId);

        TempData[success ? "Message" : "Error"] = success ? "Crew ساخته شد." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int crewId, int employeeId, int roleId)
    {
        try
        {
            await _crewService.AddOrReactivateMemberAsync(crewId, employeeId, roleId);
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
    public async Task<IActionResult> RemoveMember(int crewMemberId)
    {
        try
        {
            await _crewService.RemoveMemberAsync(crewMemberId);
            TempData["Message"] = "عضو از Crew خارج شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, FullName = x.FirstName + " " + x.LastName }).ToListAsync();

        ViewBag.MaintenanceRoles = await _context.MaintenanceRoles.Where(x => x.IsActive)
            .Select(x => new { x.MaintenanceRoleId, x.Name }).ToListAsync();

        ViewBag.WorkOrders = await _context.WorkOrders
            .Where(x => x.IsActive && x.Status != "Closed")
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new { x.WorkOrderId, x.WorkOrderNumber })
            .ToListAsync();
    }
}
