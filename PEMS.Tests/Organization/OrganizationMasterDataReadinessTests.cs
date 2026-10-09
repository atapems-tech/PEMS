using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Organization;

/// <summary>Phase 1.5: ممیزی A–J اعتبارسنجی ساختار + قیود واقعی دیتابیس (SQL Server ایزوله).</summary>
public class OrganizationMasterDataReadinessTests
{
    private static PEMSDbContext Db() => PemsDbContextFactory.CreateInMemory("mdr_" + Guid.NewGuid().ToString("N"));

    private static async Task<List<string>> Codes(PEMSDbContext db) =>
        (await new OrganizationValidationService(db).ValidateAllAsync()).Select(i => i.Code).ToList();

    [Fact]
    public async Task A_SelfReference_IsDetectedByFullValidation()
    {
        var db = Db(); var p = new Position { Title = "A", IsActive = true }; db.Positions.Add(p); await db.SaveChangesAsync();
        p.ParentPositionId = p.PositionId; await db.SaveChangesAsync();
        Assert.Contains(OrganizationIssueCodes.SelfParent, await Codes(db));
        Assert.Equal(OrganizationIssueCodes.SelfParent, await new OrganizationValidationService(db).ValidateParentChangeAsync(p.PositionId, p.PositionId));
    }

    [Fact]
    public async Task B_TwoNodeCycle_IsRejectedAndDetected()
    {
        var db = Db();
        var a = new Position { Title = "A", IsActive = true }; var b = new Position { Title = "B", IsActive = true };
        db.Positions.AddRange(a, b); await db.SaveChangesAsync();
        a.ParentPositionId = b.PositionId; await db.SaveChangesAsync();
        Assert.Equal(OrganizationIssueCodes.HierarchyCycle, await new OrganizationValidationService(db).ValidateParentChangeAsync(b.PositionId, a.PositionId));
        b.ParentPositionId = a.PositionId; await db.SaveChangesAsync();           // داده‌ی خراب واردشده بیرون از مسیر معتبر
        Assert.Contains(OrganizationIssueCodes.HierarchyCycle, await Codes(db));
    }

    [Fact]
    public async Task C_MultiNodeCycle_IsRejectedAndDetected()
    {
        var db = Db();
        var ps = Enumerable.Range(0, 4).Select(i => new Position { Title = "P" + i, IsActive = true }).ToList();
        db.Positions.AddRange(ps); await db.SaveChangesAsync();
        ps[0].ParentPositionId = ps[1].PositionId; ps[1].ParentPositionId = ps[2].PositionId; ps[2].ParentPositionId = ps[3].PositionId; await db.SaveChangesAsync();
        Assert.Equal(OrganizationIssueCodes.HierarchyCycle, await new OrganizationValidationService(db).ValidateParentChangeAsync(ps[3].PositionId, ps[0].PositionId));
        ps[3].ParentPositionId = ps[0].PositionId; await db.SaveChangesAsync();
        Assert.Contains(OrganizationIssueCodes.HierarchyCycle, await Codes(db));
    }

    [Fact]
    public async Task D_E_F_G_InactiveParent_VacantParent_InactiveHolder_MultipleHolders_AreDetected()
    {
        var db = Db();
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "a", LastName = "b", IsActive = false }, new Employee { EmployeeId = 2, FirstName = "c", LastName = "d", IsActive = true },
            new Employee { EmployeeId = 3, FirstName = "e", LastName = "f", IsActive = true });
        var child = new Position { Title = "child", IsActive = true }; var inactiveParent = new Position { Title = "ip", IsActive = false };
        var vacantParent = new Position { Title = "vp", IsActive = true }; var shared = new Position { Title = "shared", IsActive = true };
        var c2 = new Position { Title = "c2", IsActive = true }; var c3 = new Position { Title = "c3", IsActive = true };
        db.Positions.AddRange(child, inactiveParent, vacantParent, shared, c2, c3); await db.SaveChangesAsync();
        child.ParentPositionId = inactiveParent.PositionId; c2.ParentPositionId = vacantParent.PositionId; c3.ParentPositionId = shared.PositionId;
        db.EmployeeAssignments.AddRange(
            new EmployeeAssignment { PositionId = c3.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-1) },     // دارنده‌ی غیرفعال
            new EmployeeAssignment { PositionId = shared.PositionId, EmployeeId = 2, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-1) },
            new EmployeeAssignment { PositionId = shared.PositionId, EmployeeId = 3, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(-1) });  // دومین دارنده (بدون ایندکس InMemory)
        await db.SaveChangesAsync();
        var codes = await Codes(db);
        Assert.Contains(OrganizationIssueCodes.ParentPositionInactive, codes);
        Assert.Contains(OrganizationIssueCodes.ParentPositionVacant, codes);
        Assert.Contains(OrganizationIssueCodes.HolderEmployeeInactive, codes);
        Assert.Contains(OrganizationIssueCodes.MultipleActiveHolders, codes);
    }

    [Fact]
    public async Task H_I_J_MultiplePrimary_Expired_Future_AreDetected_AndNotUsedAsCurrent()
    {
        var db = Db();
        db.Employees.Add(new Employee { EmployeeId = 1, FirstName = "a", LastName = "b", IsActive = true });
        var p1 = new Position { Title = "p1", IsActive = true }; var p2 = new Position { Title = "p2", IsActive = true }; var p3 = new Position { Title = "p3", IsActive = true };
        db.Positions.AddRange(p1, p2, p3); await db.SaveChangesAsync();
        db.EmployeeAssignments.AddRange(
            new EmployeeAssignment { PositionId = p1.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-9) },
            new EmployeeAssignment { PositionId = p2.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-9) },
            new EmployeeAssignment { PositionId = p3.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(-9), EndDate = DateTime.Now.AddDays(-1) },
            new EmployeeAssignment { PositionId = p3.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(5) });
        await db.SaveChangesAsync();
        var codes = await Codes(db);
        Assert.Contains(OrganizationIssueCodes.MultiplePrimaryPositions, codes);
        Assert.Contains(OrganizationIssueCodes.AssignmentExpired, codes);
        Assert.Contains(OrganizationIssueCodes.AssignmentNotYetEffective, codes);
        // سمت‌های p3 (منقضی/آینده) دارنده‌ی «جاری» نیستند
        var chain = await OrganizationManagerChain.WalkAsync(db, 99, 1, DateTime.Now);
        Assert.NotNull(chain.IssueCode);
    }

    [Fact]
    public async Task RootPosition_WithValidDirectManager_SuperiorIsNotApplicable_NotBlocked()
    {
        var db = Db();
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "a", LastName = "b", IsActive = true }, new Employee { EmployeeId = 2, FirstName = "c", LastName = "d", IsActive = true });
        await db.SaveChangesAsync();
        await OrgSeed.ChainAsync(db, 1, 2);                                        // 1 → 2 (ریشه)
        var direct = await OrganizationManagerChain.WalkAsync(db, 1, 1, DateTime.Now);
        Assert.True(direct.Resolved); Assert.Equal(2, Assert.Single(direct.HolderEmployeeIds));
        var superior = await OrganizationManagerChain.WalkAsync(db, 1, 2, DateTime.Now);
        Assert.True(superior.NoSuperior); Assert.Null(superior.IssueCode);        // معتبر؛ مسدود نیست
        var rootRequester = await OrganizationManagerChain.WalkAsync(db, 2, 1, DateTime.Now);
        Assert.Equal(OrganizationIssueCodes.RootPosition, rootRequester.IssueCode);   // درخواست‌دهنده‌ی ریشه: مدیر مستقیم ندارد، بدون CEO
    }

    // ------------------------------------------------------------------ قیود واقعی SQL Server (ایزوله)

    [Fact]
    public async Task RealSqlServer_EnforcesUniquePrimary_UniqueHolder_UniquePositionCode_AndParentFk()
    {
        await using var db = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMSDB_OrgConstraintTest");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dept = new Department { Name = "OrgCT " + suffix, IsActive = true };
        var disc = new Discipline { Name = "OrgCT " + suffix, Code = suffix[..6], IsActive = true };
        db.Departments.Add(dept); db.Disciplines.Add(disc); await db.SaveChangesAsync();
        var e1 = new Employee { FirstName = "a", LastName = "b", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
        var e2 = new Employee { FirstName = "c", LastName = "d", DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
        db.Employees.AddRange(e1, e2);
        var pA = new Position { Title = "A", PositionCode = "CT-A-" + suffix, IsActive = true, DepartmentId = dept.DepartmentId };
        var pB = new Position { Title = "B", PositionCode = "CT-B-" + suffix, IsActive = true, DepartmentId = dept.DepartmentId };
        db.Positions.AddRange(pA, pB); await db.SaveChangesAsync();

        db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = pA.PositionId, EmployeeId = e1.EmployeeId, IsActive = true, IsPrimary = true, StartDate = DateTime.Now });
        await db.SaveChangesAsync();

        // دومین سمت اصلیِ فعال برای همان کارمند ⇒ ایندکس یکتا
        db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = pB.PositionId, EmployeeId = e1.EmployeeId, IsActive = true, IsPrimary = true, StartDate = DateTime.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // دومین دارنده‌ی فعال برای همان سمت ⇒ ایندکس یکتا
        db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = pA.PositionId, EmployeeId = e2.EmployeeId, IsActive = true, IsPrimary = true, StartDate = DateTime.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // کد سمت تکراری ⇒ ایندکس یکتا
        db.Positions.Add(new Position { Title = "dup", PositionCode = pA.PositionCode, IsActive = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // والد ناموجود ⇒ FK
        var bad = await db.Positions.FirstAsync(p => p.PositionId == pB.PositionId);
        bad.ParentPositionId = int.MaxValue;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // سمت اصلیِ غیرفعال/فرعی همزمان مجاز است (فقط «فعال و اصلی» یکتاست)
        db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = pB.PositionId, EmployeeId = e1.EmployeeId, IsActive = true, IsPrimary = false, AssignmentType = "Secondary", StartDate = DateTime.Now });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Template_HasReferenceSheets_ButNoPrefilledOrganizationRows()
    {
        var db = Db();
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Ops", IsActive = true });
        db.Employees.Add(new Employee { EmployeeId = 1, FirstName = "a", LastName = "b", PersonnelCode = "P1", IsActive = true });
        await db.SaveChangesAsync();
        var bytes = await new OrganizationImportService(db).BuildTemplateAsync();
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        Assert.Equal("Organization", wb.Worksheets.First().Name);
        Assert.Null(wb.Worksheet("Organization").Row(2).CellsUsed().FirstOrDefault());           // هیچ سطر پیش‌فرضی (بدون حدس)
        Assert.Equal("Ops", wb.Worksheet("Ref-Departments").Cell(2, 1).GetString());
        Assert.Equal("P1", wb.Worksheet("Ref-Employees").Cell(2, 1).GetString());
    }

    [Fact]
    public async Task Readiness_OnlyCounts_AndReportsMissingReportingLines()
    {
        var db = Db();
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "a", LastName = "b", IsActive = true }, new Employee { EmployeeId = 2, FirstName = "c", LastName = "d", PersonnelCode = "P2", IsActive = true });
        db.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { UserName = "u1", NormalizedUserName = "U1", EmployeeId = 1, FullName = "u1", IsActive = true });
        await db.SaveChangesAsync();
        await OrgSeed.ChainAsync(db, 2);                       // یک سمت ریشه، فقط کارمند ۲
        var r = await new OrganizationHierarchyService(db, new OrganizationValidationService(db)).GetReadinessAsync();
        Assert.Equal(1, r.ActivePositions); Assert.Equal(0, r.PositionsWithParent); Assert.Equal(1, r.RootPositions);
        Assert.Equal(2, r.ActiveEmployees); Assert.Equal(1, r.ActiveEmployeesWithPrimaryPosition);
        Assert.Equal(1, r.ActiveEmployeesWithoutPersonnelCode); Assert.Equal(1, r.ActiveUsersWithoutPrimaryPosition);
    }

    // ------------------------------------------------------------------ Department optional + the supplied petrochemical file

    [Fact]
    public async Task Import_BlankDepartment_IsAllowed_ButAnUnknownDepartmentStillRejected_AndBlankNeverClearsExisting()
    {
        var db = Db();
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Ops", IsActive = true }); await db.SaveChangesAsync();
        var svc = new OrganizationImportService(db);
        MemoryStream X(params string[][] rows)
        {
            var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Org");
            var h = new[] { "Department", "PositionCode", "PositionTitle", "ParentPositionCode" };
            for (var c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
            for (var r = 0; r < rows.Length; r++) for (var c = 0; c < rows[r].Length; c++) ws.Cell(r + 2, c + 1).Value = rows[r][c];
            var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0; return ms;
        }

        Assert.NotEmpty((await svc.ImportAsync(X(new[] { "CEO", "TOP", "Top", "" }), false)).Errors);                           // 'CEO' is not a department
        var ok = await svc.ImportAsync(X(new[] { "", "TOP", "Top", "" }, new[] { "Ops", "MID", "Mid", "TOP" }), true);
        Assert.Empty(ok.Errors); Assert.True(ok.Committed);
        Assert.Null((await db.Positions.SingleAsync(p => p.PositionCode == "TOP")).DepartmentId);
        Assert.Equal(1, (await db.Positions.SingleAsync(p => p.PositionCode == "MID")).DepartmentId);

        await svc.ImportAsync(X(new[] { "", "MID", "Mid", "TOP" }), true);                                                     // blank = unspecified, never clears
        Assert.Equal(1, (await db.Positions.SingleAsync(p => p.PositionCode == "MID")).DepartmentId);
    }

    [Fact]
    public async Task PetrochemicalCompletedFile_PassesImportPreview_AgainstTheRealDepartmentNames()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "OrganizationStructure_Petrochemical1_Completed.xlsx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var db = Db();
        foreach (var (name, i) in new[] { "Construction", "Document Control", "Engineering", "Finance", "HSE", "Human Resources", "IT", "Maintenance", "Operations", "Planning & Control", "Procurement", "Project Management", "QA/QC", "WAREHOUSE" }.Select((n, i) => (n, i)))
            db.Departments.Add(new Department { DepartmentId = i + 1, Code = "D" + i, Name = name, IsActive = true });
        await db.SaveChangesAsync();

        var path = Path.Combine(dir!.FullName, "docs", "OrganizationStructure_Petrochemical1_Completed.xlsx");
        // the only department the owner may reference beyond the 14 real ones is the CEO's (row 2); it is optional (blank) or must be created first
        string? ceoDept;
        using (var wb = new XLWorkbook(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) ceoDept = wb.Worksheet("Organization").Cell(2, 1).GetString().Trim();

        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var preview = await new OrganizationImportService(db).ImportAsync(fs, commit: false);
            if (string.IsNullOrEmpty(ceoDept) || db.Departments.Any(d => d.Name == ceoDept))
                Assert.Empty(preview.Errors);
            else
                Assert.Equal(new[] { $"Row 2: Unknown department '{ceoDept}'." }, preview.Errors);       // exactly one error: the department must exist (never auto-created)
            Assert.Equal(0, await db.Positions.CountAsync());                                           // preview never saves
        }

        // once that department exists the whole file is clean
        if (!string.IsNullOrEmpty(ceoDept) && !db.Departments.Any(d => d.Name == ceoDept))
        {
            db.Departments.Add(new Department { DepartmentId = 99, Code = "EXEC", Name = ceoDept, IsActive = true }); await db.SaveChangesAsync();
        }
        await using var fs2 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var clean = await new OrganizationImportService(db).ImportAsync(fs2, commit: false);
        Assert.Empty(clean.Errors);
        Assert.Equal(246, clean.PositionsCreated);
    }
}
