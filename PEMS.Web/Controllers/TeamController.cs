using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق سند معماری Employee/Team/Labor Cost: جایگزین MaintenanceCrewController - Employee
/// دیگر این‌جا تکرار نمی‌شود (فقط EmployeeId ارجاع)، و منطق سه‌حالته‌ی عضویت (Reject/
/// Reactivate/New) از ITeamService می‌آید، نه مستقیم در Controller.
/// </summary>
/// <remarks>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: این Controller قبلاً
/// هیچ بررسی مجوزی نداشت - CreateTeam/AddMember/RemoveMember/MoveMember (که عضویت تیم را تغییر
/// می‌دهند و روی تخصیص کار/مسئولیت اجرایی اثر می‌گذارند) برای هر کاربر احرازهویت‌شده‌ای باز
/// بودند. ماژول جدید "Team" چون هیچ ماژول موجودی این قابلیت را پوشش نمی‌دهد.
/// AddMember/RemoveMember/MoveMember با "Edit" گیت شدند (نه Approve) چون فقط ترکیب یک واحد
/// سازمانی موجود را به‌روز می‌کنند، نه یک تصمیم تایید نهایی.
///
/// طبق «PEMS — P1 Object-Level Authorization &amp; Data Scope Hardening»: داشتن Team.Edit به‌تنهایی
/// نباید یعنی «هر Team ای در کل دیتابیس قابل‌ویرایش است». تنها بُعد Scope واقعی که این Schema
/// برایش شواهد قطعی دارد Discipline است - Team.MaintenanceGroup.DisciplineId (اجباری، نه
/// Nullable) و Employee.DisciplineId (اجباری) هر دو همیشه پر هستند؛ EmployeeAreaScope
/// (Plant/Area/Unit) رد شد چون طبق کامنت خودش «جدول خالی Ship می‌شود... صرفاً Advisory»
/// (طراحی صریح قبلی، نه یک بُعد Scope قابل‌اعتماد امروز). SystemAdmin/EngineeringManager معاف‌اند
/// چون در تمام ماژول‌های دیگر PEMS (Contracts/EngineeringDocuments/VDR/...) این دو نقش هرگز به
/// یک Discipline محدود نشده‌اند - محدودکردنشان اینجا خودش یک قاعده‌ی بی‌سابقه و اختراعی بود.
/// اگر کاربر جاری هیچ Employee/Discipline ای نداشته باشد، Fail-Closed (رد می‌شود، نه اجازه).
/// Crew عمداً همین رفتار را ندارد - نه Discipline/Department مستقیم دارد، نه WorkOrderId
/// اجباری، و طبق کامنت خودِ Entity ممکن است عمداً چند-دیسیپلینه باشد؛ افزودن یک قاعده‌ی Scope
/// آن‌جا بدون شواهد Schema، دقیقاً همان «اختراع قاعده‌ی کسب‌وکار» است که این ممیزی ممنوعش کرده.
/// </remarks>
public class TeamController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ITeamService _teamService;
    private readonly IOrganizationStructureService _organizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public TeamController(PEMSDbContext context, ITeamService teamService,
        IOrganizationStructureService organizationService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _context = context;
        _teamService = teamService;
        _organizationService = organizationService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

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

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6A»: صفحه‌ی نمودار سازمانی گرافیکی.
    // این اکشن فقط Shell صفحه را می‌دهد؛ خودِ درخت از طریق همان OrganizationTree (JSON، پایین)
    // با fetch سمت کلاینت خوانده می‌شود - View مستقیم به EF Core دسترسی ندارد.
    [HttpGet]
    public async Task<IActionResult> OrganizationChart(int? disciplineId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

        ViewData["Title"] = "نمودار سازمانی تعمیرونگهداری";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Disciplines = await _context.Disciplines
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.DisciplineId, x.Name })
            .ToListAsync();

        ViewBag.SelectedDisciplineId = disciplineId;

        return View();
    }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6B»: صفحه‌ی جزئیات یک گره‌ی Team
    // (شامل زیرگره‌ها/شیفت‌ها، سمت‌ها، اشغال/خالی، تاریخچه) - روی همان مسیر/کنترلر موجود
    // (بند ۳: مسیر رقیب برای Team Details ساخته نشد). داده فقط از IOrganizationStructureService
    // می‌آید؛ View مستقیم به EF Core دسترسی ندارد.
    [HttpGet]
    public async Task<IActionResult> TeamDetails(int teamId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsTeamWithinDisciplineScopeAsync(teamId, myDisciplineId))
                return Forbid();
        }

        var team = await _organizationService.GetTeamDetailsAsync(teamId);
        if (team == null) return NotFound();

        ViewData["Title"] = team.Name;
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Employees = await _context.Employees
            .Where(x => x.IsActive)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new { x.EmployeeId, FullName = x.FirstName + " " + x.LastName })
            .ToListAsync();

        // طبق بند ۱۲/۱۳: مقصد «جابه‌جایی کارمند» (سمت‌های خالیِ فعال در کل سازمان، نه فقط
        // همین تیم) و مقصد «جابه‌جایی سمت» (کل گره‌های Team فعال) - از همان درخت موجود
        // (GetOrganizationTreeAsync) به‌جای کوئری تازه‌ی EF Core در Controller.
        var tree = await _organizationService.GetOrganizationTreeAsync();
        var allTeams = FlattenTeams(tree).Where(t => t.IsActive).OrderBy(t => t.Name).ToList();

        ViewBag.AllTeamNodes = allTeams
            .Select(t => new { t.TeamId, t.Name, t.NodeKind })
            .ToList();

        ViewBag.VacantPositions = allTeams
            .SelectMany(t => t.Positions.Where(p => p.IsActive && p.IsVacant)
                .Select(p => new { p.PositionId, Label = $"{t.Name} — {p.Title}" }))
            .OrderBy(x => x.Label)
            .ToList();

        return View(team);
    }

    // درخت را به فهرست تخت Team ها می‌شکند (چه زیر گروه‌های تودرتو، چه زیر Team های تودرتو) -
    // فقط برای پرکردن Dropdown های همین صفحه؛ هیچ Query جدیدی نمی‌زند (روی همان درخت خوانده‌شده کار می‌کند).
    private static IEnumerable<OrganizationTeamNodeDto> FlattenTeams(IEnumerable<OrganizationGroupNodeDto> groups)
    {
        foreach (var g in groups)
        {
            foreach (var t in FlattenTeamNodes(g.Teams)) yield return t;
            foreach (var t in FlattenTeams(g.Children)) yield return t;
        }
    }

    private static IEnumerable<OrganizationTeamNodeDto> FlattenTeamNodes(IEnumerable<OrganizationTeamNodeDto> teams)
    {
        foreach (var t in teams)
        {
            yield return t;
            foreach (var c in FlattenTeamNodes(t.Children)) yield return c;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(string code, string name, int maintenanceGroupId, string? shift, int? supervisorEmployeeId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Create"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            var groupDisciplineId = await _context.MaintenanceGroups
                .Where(x => x.MaintenanceGroupId == maintenanceGroupId)
                .Select(x => (int?)x.DisciplineId)
                .FirstOrDefaultAsync();

            if (!myDisciplineId.HasValue || !groupDisciplineId.HasValue || myDisciplineId.Value != groupDisciplineId.Value)
                return Forbid();
        }

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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsTeamWithinDisciplineScopeAsync(teamId, myDisciplineId) ||
                !await IsEmployeeWithinDisciplineScopeAsync(employeeId, myDisciplineId))
                return Forbid();
        }

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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        // طبق ممیزی بند ۱۰-۱۷ (IDOR): قبل از این رفع، Controller هیچ ایده‌ای از اینکه این
        // teamMemberId متعلق به کدام Team است نداشت - یعنی هیچ Scope ای قابل‌بررسی نبود. حالا
        // Team را از خودِ رکورد عضویت Resolve می‌کنیم تا بررسی روی «Object واقعی»، نه فقط ID
        // خام ورودی، انجام شود.
        if (!HasCrossDisciplineAuthority())
        {
            var teamId = await _context.TeamMembers
                .Where(x => x.TeamMemberId == teamMemberId)
                .Select(x => (int?)x.TeamId)
                .FirstOrDefaultAsync();

            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!teamId.HasValue || !await IsTeamWithinDisciplineScopeAsync(teamId.Value, myDisciplineId))
                return Forbid();
        }

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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        // طبق ممیزی بند ۶-۱۱: هر دو سمت Move باید مستقل بررسی شوند - وگرنه یک Team مجاز به‌عنوان
        // مقصد می‌تواند بهانه‌ای برای خارج‌کردن یک کارمند از یک Team غیرمجاز (مبدأ) باشد.
        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            var sourceInScope = await IsTeamWithinDisciplineScopeAsync(fromTeamId, myDisciplineId);
            var targetInScope = await IsTeamWithinDisciplineScopeAsync(toTeamId, myDisciplineId);
            var employeeInScope = await IsEmployeeWithinDisciplineScopeAsync(employeeId, myDisciplineId);

            if (!sourceInScope || !targetInScope || !employeeInScope)
                return Forbid();
        }

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
    // طبق «PEMS Dynamic Maintenance Organization»: زیرتیم/شیفت/واحد به‌عنوان گره (Team.
    // ParentTeamId/NodeKind)، سمت سازمانی (Position) و تخصیص/خروج/جابه‌جایی Employee روی آن.
    // هم‌الگوی دقیق مجوز و Scope دیسیپلینی بالا (Team.Edit/Create + بررسی Discipline).
    // ==============================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubTeam(int maintenanceGroupId, int? parentTeamId, string code, string name,
        string? nodeKind, int? supervisorEmployeeId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Create"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            var groupDisciplineId = await _context.MaintenanceGroups
                .Where(x => x.MaintenanceGroupId == maintenanceGroupId)
                .Select(x => (int?)x.DisciplineId)
                .FirstOrDefaultAsync();

            if (!myDisciplineId.HasValue || !groupDisciplineId.HasValue || myDisciplineId.Value != groupDisciplineId.Value)
                return Forbid();
        }

        try
        {
            await _organizationService.CreateTeamNodeAsync(code, name, maintenanceGroupId, parentTeamId,
                nodeKind ?? "Team", supervisorEmployeeId);
            TempData["Message"] = "زیرمجموعه/شیفت ساخته شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(parentTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveTeamNode(int teamId, int? newParentTeamId, int? newMaintenanceGroupId, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsTeamWithinDisciplineScopeAsync(teamId, myDisciplineId))
                return Forbid();

            if (newParentTeamId.HasValue && !await IsTeamWithinDisciplineScopeAsync(newParentTeamId.Value, myDisciplineId))
                return Forbid();

            if (newMaintenanceGroupId.HasValue)
            {
                var groupDisciplineId = await _context.MaintenanceGroups
                    .Where(x => x.MaintenanceGroupId == newMaintenanceGroupId.Value)
                    .Select(x => (int?)x.DisciplineId)
                    .FirstOrDefaultAsync();
                if (!groupDisciplineId.HasValue || groupDisciplineId.Value != myDisciplineId)
                    return Forbid();
            }
        }

        try
        {
            await _organizationService.MoveTeamNodeAsync(teamId, newParentTeamId, newMaintenanceGroupId);
            TempData["Message"] = "تیم/زیرمجموعه جابه‌جا شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId ?? teamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateTeamNode(int teamId, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsTeamWithinDisciplineScopeAsync(teamId, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.DeactivateTeamNodeAsync(teamId);
            TempData["Message"] = "تیم/زیرمجموعه غیرفعال شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId ?? teamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePosition(int? teamId, string title, string? description, int sortOrder)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!teamId.HasValue || !await IsTeamWithinDisciplineScopeAsync(teamId.Value, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.CreatePositionAsync(teamId, departmentId: null, title, description, sortOrder);
            TempData["Message"] = "سمت سازمانی ساخته شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(teamId);
    }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6C» بند ۷: ویرایش عنوان/توضیحات/ترتیب -
    // هم‌الگوی دقیق مجوز/Scope بقیه‌ی اکشن‌های سمت (Team.Edit + Scope بر اساس تیم فعلی سمت).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePosition(int positionId, string title, string? description, int sortOrder, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsPositionWithinDisciplineScopeAsync(positionId, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.UpdatePositionAsync(positionId, title, description, sortOrder);
            TempData["Message"] = "سمت به‌روزرسانی شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MovePosition(int positionId, int? newTeamId, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsPositionWithinDisciplineScopeAsync(positionId, myDisciplineId))
                return Forbid();
            if (newTeamId.HasValue && !await IsTeamWithinDisciplineScopeAsync(newTeamId.Value, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.MovePositionAsync(positionId, newTeamId);
            TempData["Message"] = "سمت جابه‌جا شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId ?? newTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivatePosition(int positionId, string? notes, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsPositionWithinDisciplineScopeAsync(positionId, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.DeactivatePositionAsync(positionId, notes);
            TempData["Message"] = "سمت غیرفعال شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignEmployeeToPosition(int positionId, int employeeId, string? notes, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsPositionWithinDisciplineScopeAsync(positionId, myDisciplineId) ||
                !await IsEmployeeWithinDisciplineScopeAsync(employeeId, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.AssignEmployeeAsync(positionId, employeeId, notes);
            TempData["Message"] = "کارمند به سمت تخصیص یافت.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAssignment(int positionId, string? notes, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            if (!await IsPositionWithinDisciplineScopeAsync(positionId, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.RemoveAssignmentAsync(positionId, notes);
            TempData["Message"] = "سمت خالی (Vacant) شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveEmployeeBetweenPositions(int fromPositionId, int toPositionId, string? notes, int? returnTeamId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "Edit"))
            return Forbid();

        if (!HasCrossDisciplineAuthority())
        {
            var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
            var employeeId = await _context.EmployeeAssignments
                .Where(x => x.PositionId == fromPositionId && x.IsActive)
                .Select(x => (int?)x.EmployeeId)
                .FirstOrDefaultAsync();

            if (!await IsPositionWithinDisciplineScopeAsync(fromPositionId, myDisciplineId) ||
                !await IsPositionWithinDisciplineScopeAsync(toPositionId, myDisciplineId) ||
                !employeeId.HasValue || !await IsEmployeeWithinDisciplineScopeAsync(employeeId.Value, myDisciplineId))
                return Forbid();
        }

        try
        {
            await _organizationService.MoveEmployeeAsync(fromPositionId, toPositionId, notes);
            TempData["Message"] = "کارمند به سمت جدید منتقل شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToTeamOrIndex(returnTeamId);
    }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6B» بند ۳: هدف مشترک تمام اکشن‌های
    // جهش بالا - وقتی returnTeamId مشخص باشد (از فرم صفحه‌ی جزئیات تیم) همان‌جا برمی‌گردد،
    // وگرنه به Index (رفتار پیش‌فرض قدیمی‌تر، برای فراخوانی مستقیم بدون Context صفحه‌ی تیم).
    private IActionResult RedirectToTeamOrIndex(int? teamId)
        => teamId.HasValue ? RedirectToAction(nameof(TeamDetails), new { teamId = teamId.Value }) : RedirectToAction(nameof(Index));

    // طبق بخش ۲۰/۲۶ سند: مدل خواندنیِ درخت سازمانی - پایه‌ی داده‌ای برای نمودار سازمانی
    // آینده (فاز جداگانه‌ی UI)؛ این‌جا فقط API خواندنی JSON عرضه می‌شود.
    [HttpGet]
    public async Task<IActionResult> OrganizationTree(int? disciplineId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

        var tree = await _organizationService.GetOrganizationTreeAsync(disciplineId);
        return Json(tree);
    }

    [HttpGet]
    public async Task<IActionResult> TeamNodeDetails(int teamId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

        var details = await _organizationService.GetTeamDetailsAsync(teamId);
        if (details == null) return NotFound();
        return Json(details);
    }

    // ==============================================================================
    // برای فرم Work Order: با انتخاب تیم، فقط اعضای همون تیم (با نقششون) برگردونده بشه -
    // نه لیست کامل کارمندها
    // ==============================================================================
    // طبق ممیزی: این AJAX توسط فرم ساخت/جزئیات WorkOrder هم مصرف می‌شود (نه فقط صفحه‌ی Team) -
    // به همون سطح دسترسی View که ماژول WorkOrder به DisciplineEngineer/Viewer می‌دهد نیاز دارد؛
    // وگرنه بازبینی WorkOrder برای این دو نقش، اعضای تیم را نشان نمی‌دهد (Case C - لینک مرده).
    [HttpGet]
    public async Task<IActionResult> GetTeamMembers(int teamId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Team", "View"))
            return Forbid();

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

    // طبق ممیزی: در تمام ماژول‌های دیگر PEMS (Contracts/EngineeringDocuments/VDR/WorkOrder/...)
    // SystemAdmin و EngineeringManager هرگز به یک Discipline محدود نشده‌اند - این دو نقش دارای
    // اختیار نظارتی چند-دیسیپلینه هستند. محدودکردنشان فقط همین‌جا، بدون شاهدی در بقیه‌ی پروژه،
    // خودش یک قاعده‌ی اختراعی بی‌سابقه بود.
    private bool HasCrossDisciplineAuthority() =>
        _currentUser.Roles.Contains(SystemRoles.SystemAdmin) || _currentUser.Roles.Contains(SystemRoles.EngineeringManager);

    // همون الگوی دقیق GetCurrentEmployeeDisciplineIdAsync در WorkOrderController
    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        var employeeId = await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue) return null;

        return await _context.Employees
            .Where(e => e.EmployeeId == employeeId.Value)
            .Select(e => (int?)e.DisciplineId)
            .FirstOrDefaultAsync();
    }

    // Fail-Closed: اگر Scope کاربر (myDisciplineId) یا Discipline تیم قابل‌تعیین نباشد، false
    private async Task<bool> IsTeamWithinDisciplineScopeAsync(int teamId, int? myDisciplineId)
    {
        if (!myDisciplineId.HasValue) return false;

        var teamDisciplineId = await _context.Teams
            .Where(x => x.TeamId == teamId)
            .Select(x => (int?)x.MaintenanceGroup!.DisciplineId)
            .FirstOrDefaultAsync();

        return teamDisciplineId.HasValue && teamDisciplineId.Value == myDisciplineId.Value;
    }

    private async Task<bool> IsEmployeeWithinDisciplineScopeAsync(int employeeId, int? myDisciplineId)
    {
        if (!myDisciplineId.HasValue) return false;

        var employeeDisciplineId = await _context.Employees
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => (int?)x.DisciplineId)
            .FirstOrDefaultAsync();

        return employeeDisciplineId.HasValue && employeeDisciplineId.Value == myDisciplineId.Value;
    }

    // طبق «PEMS Dynamic Maintenance Organization»: هم‌الگوی IsTeamWithinDisciplineScopeAsync -
    // Fail-Closed اگر سمت به هیچ Team ای وصل نباشد (سمت عمومی/سطح دپارتمان)، چون این Schema
    // هیچ بُعد Scope دیگری برایش ندارد.
    private async Task<bool> IsPositionWithinDisciplineScopeAsync(int positionId, int? myDisciplineId)
    {
        if (!myDisciplineId.HasValue) return false;

        var teamId = await _context.Positions
            .Where(x => x.PositionId == positionId)
            .Select(x => x.TeamId)
            .FirstOrDefaultAsync();

        if (!teamId.HasValue) return false;

        return await IsTeamWithinDisciplineScopeAsync(teamId.Value, myDisciplineId);
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
