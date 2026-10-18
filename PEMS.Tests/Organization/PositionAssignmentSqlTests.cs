using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Organization;

/// <summary>
/// تغییر سمت اصلی روی SQL Server واقعی و با همان SqlServerRetryingExecutionStrategy که برنامه استفاده می‌کند
/// (تراکنش کاربر بیرون از strategy.ExecuteAsync در این استراتژی خطا می‌دهد؛ InMemory این را نشان نمی‌دهد).
/// </summary>
public class PositionAssignmentSqlTests
{
    private static PEMSDbContext RetryingContext()
    {
        using var seed = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMSDB_OrgConstraintTest");   // Migrate()
        var csb = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = "PEMSDB_OrgConstraintTest" };
        return new PEMSDbContext(new DbContextOptionsBuilder<PEMSDbContext>()
            .UseSqlServer(csb.ConnectionString, o => o.EnableRetryOnFailure()).Options);
    }

    [Fact]
    public async Task SetPrimaryPosition_WithRetryingStrategy_ReplacesPrimary_KeepsHistory_AndRefusalLeavesNothingChanged()
    {
        await using var db = RetryingContext();
        var s = Guid.NewGuid().ToString("N")[..8];
        var dept = new Department { Name = "PA " + s, IsActive = true };
        var disc = new Discipline { Name = "PA " + s, Code = s[..6], IsActive = true };
        db.Departments.Add(dept); db.Disciplines.Add(disc); await db.SaveChangesAsync();
        var e1 = new Employee { FirstName = "a", LastName = "b", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
        var e2 = new Employee { FirstName = "c", LastName = "d", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
        db.Employees.AddRange(e1, e2);
        var pA = new Position { Title = "A", PositionCode = "PA-A-" + s, IsActive = true, DepartmentId = dept.DepartmentId };
        var pB = new Position { Title = "B", PositionCode = "PA-B-" + s, IsActive = true, DepartmentId = dept.DepartmentId };
        var pC = new Position { Title = "C", PositionCode = "PA-C-" + s, IsActive = true, DepartmentId = dept.DepartmentId };
        db.Positions.AddRange(pA, pB, pC); await db.SaveChangesAsync();

        var svc = new OrganizationHierarchyService(db, new OrganizationValidationService(db));
        Assert.True((await svc.SetPrimaryPositionAsync(e1.EmployeeId, pA.PositionId)).Success);
        Assert.True((await svc.SetPrimaryPositionAsync(e1.EmployeeId, pB.PositionId)).Success);     // جایگزینی

        var rows = await db.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == e1.EmployeeId).OrderBy(a => a.EmployeeAssignmentId).ToListAsync();
        Assert.Equal(2, rows.Count);                                                                  // تاریخچه حفظ شد
        Assert.False(rows[0].IsActive); Assert.Equal(pA.PositionId, rows[0].PositionId); Assert.NotNull(rows[0].EndDate);
        Assert.True(rows[1].IsActive && rows[1].IsPrimary); Assert.Equal(pB.PositionId, rows[1].PositionId);

        // سمت اشغال‌شده ⇒ رد، و سمت اصلی فعلیِ e1 دست‌نخورده می‌ماند
        Assert.True((await svc.SetPrimaryPositionAsync(e2.EmployeeId, pC.PositionId)).Success);
        var refused = await svc.SetPrimaryPositionAsync(e1.EmployeeId, pC.PositionId);
        Assert.False(refused.Success); Assert.Equal("Org_Err_PositionOccupied", refused.ErrorKey);
        var still = await db.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == e1.EmployeeId && a.IsActive && a.IsPrimary).ToListAsync();
        Assert.Single(still); Assert.Equal(pB.PositionId, still[0].PositionId);
    }
}
