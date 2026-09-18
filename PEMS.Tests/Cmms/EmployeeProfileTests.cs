using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «Employee Details — Stitch UI Redesign»: تست‌های متمرکز روی دو تغییر واقعی این فاز -
/// (۱) چهار فیلد افزایشی جدید Employee به‌درستی ذخیره/بازیابی می‌شوند و مقدار NULL را هم
/// درست پشتیبانی می‌کنند، (۲) کوئری اشتقاق «مدیر» (از طریق عضویت فعال تیم و سرپرست آن) دقیقاً
/// همان شکلی که در EmployeeController.Details استفاده شده را برمی‌گرداند. EmployeeController
/// خودش به UserManager/RoleManager (ASP.NET Identity) وابسته است که این پروژه‌ی تست زیرساخت
/// Mock کردنش را ندارد - بنابراین این تست‌ها مستقیماً روی DbContext/Query واقعی، بدون فراخوانی
/// خودِ Action، رفتار را تایید می‌کنند.
/// </summary>
public class EmployeeProfileTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"EmployeeProfile_{dbName}");

    private static async Task<(Department dept, Discipline disc)> SeedOrgAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var dept = new Department { Code = "ENG", Name = "Engineering" };
        var disc = new Discipline { Code = "MECH", Name = "Mechanical" };
        ctx.Departments.Add(dept);
        ctx.Disciplines.Add(disc);
        await ctx.SaveChangesAsync();
        return (dept, disc);
    }

    [Fact]
    public async Task NewProfileFields_PersistAndReload_WithRealValues()
    {
        var ctx = CreateContext(nameof(NewProfileFields_PersistAndReload_WithRealValues));
        var (dept, disc) = await SeedOrgAsync(ctx);

        var employee = new Employee
        {
            FirstName = "Marcus",
            LastName = "Thorne",
            DepartmentId = dept.DepartmentId,
            DisciplineId = disc.DisciplineId,
            HireDate = new DateTime(2018, 10, 12),
            Location = "Site A - Main Hub",
            Shift = "Day Shift (08:00 - 16:00)",
            HomeAddress = "123 Industrial Rd, Suite 400, Springfield, IL"
        };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var reloaded = await CreateContext(nameof(NewProfileFields_PersistAndReload_WithRealValues))
            .Employees.AsNoTracking().FirstAsync(x => x.EmployeeId == employee.EmployeeId);

        Assert.Equal(new DateTime(2018, 10, 12), reloaded.HireDate);
        Assert.Equal("Site A - Main Hub", reloaded.Location);
        Assert.Equal("Day Shift (08:00 - 16:00)", reloaded.Shift);
        Assert.Equal("123 Industrial Rd, Suite 400, Springfield, IL", reloaded.HomeAddress);
    }

    [Fact]
    public async Task NewProfileFields_DefaultToNull_ForExistingEmployeeShape()
    {
        // طبق رول ۸.۳ سند: فیلدهای افزایشی باید داده‌ی قبلی را نشکنند - کارمندی که با این
        // فیلدها ساخته نشده باید NULL برگرداند، نه یک مقدار جعلی
        var ctx = CreateContext(nameof(NewProfileFields_DefaultToNull_ForExistingEmployeeShape));
        var (dept, disc) = await SeedOrgAsync(ctx);

        var employee = new Employee
        {
            FirstName = "Sarah",
            LastName = "Jenkins",
            DepartmentId = dept.DepartmentId,
            DisciplineId = disc.DisciplineId
        };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Employees.AsNoTracking().FirstAsync(x => x.EmployeeId == employee.EmployeeId);

        Assert.Null(reloaded.HireDate);
        Assert.Null(reloaded.Location);
        Assert.Null(reloaded.Shift);
        Assert.Null(reloaded.HomeAddress);
    }

    [Fact]
    public async Task ManagerDerivation_ActiveTeamMembership_ResolvesTeamSupervisor()
    {
        // همان شکل کوئری‌ای که EmployeeController.Details برای صفحه‌ی Details استفاده می‌کند:
        // TeamMember فعال -> Team -> SupervisorEmployee
        var ctx = CreateContext(nameof(ManagerDerivation_ActiveTeamMembership_ResolvesTeamSupervisor));
        var (dept, disc) = await SeedOrgAsync(ctx);

        var supervisor = new Employee { FirstName = "Sarah", LastName = "Jenkins", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId };
        var employee = new Employee { FirstName = "Marcus", LastName = "Thorne", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId };
        ctx.Employees.AddRange(supervisor, employee);
        await ctx.SaveChangesAsync();

        var group = new MaintenanceGroup { Code = "MG1", Name = "Mechanical Group", DisciplineId = disc.DisciplineId };
        ctx.MaintenanceGroups.Add(group);
        await ctx.SaveChangesAsync();

        var team = new Team { Code = "T1", Name = "Line 1 Crew", MaintenanceGroupId = group.MaintenanceGroupId, SupervisorEmployeeId = supervisor.EmployeeId };
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();

        var role = new MaintenanceRole { Name = "Technician", DisciplineId = disc.DisciplineId };
        ctx.MaintenanceRoles.Add(role);
        await ctx.SaveChangesAsync();

        ctx.TeamMembers.Add(new TeamMember
        {
            TeamId = team.TeamId,
            EmployeeId = employee.EmployeeId,
            RoleId = role.MaintenanceRoleId,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var teamHistory = await ctx.TeamMembers
            .Include(x => x.Team!).ThenInclude(t => t.SupervisorEmployee)
            .Include(x => x.Role)
            .Where(x => x.EmployeeId == employee.EmployeeId)
            .OrderByDescending(x => x.JoinedDate)
            .ToListAsync();

        var currentMembership = teamHistory.FirstOrDefault(x => x.IsActive);
        var manager = currentMembership?.Team?.SupervisorEmployee;

        Assert.NotNull(manager);
        Assert.Equal(supervisor.EmployeeId, manager!.EmployeeId);
        Assert.Equal("Sarah", manager.FirstName);
    }

    [Fact]
    public async Task ManagerDerivation_NoActiveTeamMembership_ResolvesToNull()
    {
        // طبق رول «عدم Fabrication»: وقتی هیچ عضویت فعالی نیست، مدیر باید NULL باشد - نه یک
        // مقدار پیش‌فرض ساختگی
        var ctx = CreateContext(nameof(ManagerDerivation_NoActiveTeamMembership_ResolvesToNull));
        var (dept, disc) = await SeedOrgAsync(ctx);

        var employee = new Employee { FirstName = "Marcus", LastName = "Thorne", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var teamHistory = await ctx.TeamMembers
            .Include(x => x.Team!).ThenInclude(t => t.SupervisorEmployee)
            .Include(x => x.Role)
            .Where(x => x.EmployeeId == employee.EmployeeId)
            .OrderByDescending(x => x.JoinedDate)
            .ToListAsync();

        var currentMembership = teamHistory.FirstOrDefault(x => x.IsActive);
        var manager = currentMembership?.Team?.SupervisorEmployee;

        Assert.Null(manager);
    }
}
