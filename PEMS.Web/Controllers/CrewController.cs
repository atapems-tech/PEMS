using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۴: ترکیب اجرایی نفرات برای
/// یک فعالیت مشخص - برخلاف Team (واحد سازمانی پایدار)، می‌تواند به یک WorkOrder مشخص وصل
/// باشد یا مستقل/پایدار بماند. همون الگوی TeamController (منطق سه‌حالته از ICrewService).
/// </summary>
/// <remarks>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: این Controller قبلاً
/// هیچ بررسی مجوزی نداشت - CreateCrew/AddMember/RemoveMember برای هر کاربر احرازهویت‌شده‌ای
/// باز بودند. ماژول جدید "Crew" - برخلاف Team، هیچ اکشن دیگری این ماژول را مصرف نمی‌کند
/// (بدون یک GetCrewMembers مشابه)، پس همون نقش‌های مدیریت CMMS کافی است.
///
/// طبق «PEMS — P1 Object-Level Authorization &amp; Data Scope Hardening»: برخلاف TeamController،
/// عمداً هیچ بررسی Object-Scope (مثلاً Discipline) این‌جا اضافه نشد - Crew Entity هیچ
/// DisciplineId/DepartmentId مستقیمی ندارد؛ تنها ارتباط بالقوه (WorkOrderId) هم Nullable است
/// (طبق کامنت خودِ Entity: "WorkOrderId=null یعنی Crew مستقل/پایدار") و حتی وقتی پر باشد فقط با
/// دو پرش غیرمستقیم (Crew → WorkOrder → Equipment → DisciplineId) به یک Discipline می‌رسد.
///
/// طبق تایید صریح صاحب محصول (بعد از این ممیزی): این یک طراحی عمدی است، نه فقط یک استنتاج از
/// نبود Schema - Crew واقعاً باید بتواند چند-دیسیپلینه باشد (مثلاً یک فعالیت مشترک که هم تکنسین
/// مکانیک هم برق نیاز دارد). بنابراین هیچ‌وقت نباید یک قاعده‌ی Object-Scope (Discipline/
/// Department/...) روی Crew اعمال شود - نه امروز، نه در آینده، مگر با یک تصمیم محصولی صریح
/// جدید. این یافته بسته شده تلقی می‌شود (نه یک نقص باز)؛ فقط سطح Module Permission (Crew.View/
/// Create/Edit، طبق پاس قبلی) لایه‌ی مجوز کافی و نهایی برای Crew است.
/// </remarks>
public class CrewController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICrewService _crewService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public CrewController(PEMSDbContext context, ICrewService crewService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _context = context;
        _crewService = crewService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Crew", "View"))
            return Forbid();

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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Crew", "Create"))
            return Forbid();

        var (success, error, _) = await _crewService.CreateAsync(code, name, workOrderId);

        TempData[success ? "Message" : "Error"] = success ? "Crew ساخته شد." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int crewId, int employeeId, int roleId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Crew", "Edit"))
            return Forbid();

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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Crew", "Edit"))
            return Forbid();

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
