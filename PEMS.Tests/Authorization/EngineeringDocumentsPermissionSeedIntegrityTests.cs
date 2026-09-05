using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 5C — Part C: EngineeringDocuments Permission Seed Integrity». RolePermissions
/// یک ردیف به‌ازای هر (RoleName, ModuleName) است - تمام Actionها روی همون یک ردیف ستون Boolean
/// هستند. گارد قدیمی «اگه هر ردیفی برای این ماژول هست کلاً رد شو» در سطح ماژول Idempotent بود
/// اما نمی‌توانست State جزئی (فقط بعضی نقش‌ها دارند) را ترمیم کند. این تست‌ها دقیقاً چهار
/// سناریوی الزامی C-7 را پوشش می‌دهند.
/// </summary>
public class EngineeringDocumentsPermissionSeedIntegrityTests
{
    private static readonly string[] AuthoredRoles =
    {
        SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor,
        SystemRoles.DisciplineEngineer, SystemRoles.DocumentController, SystemRoles.Viewer
    };

    private static PermissionService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"EDSeedIntegrity_{dbName}");
        return new PermissionService(ctx, new AccessControlService(ctx));
    }

    // ==== Scenario 1: Empty EngineeringDocuments permissions ====
    [Fact]
    public async Task Scenario1_EmptyDatabase_SeedsAllFiveAuthoredRoles()
    {
        var service = CreateService(nameof(Scenario1_EmptyDatabase_SeedsAllFiveAuthoredRoles), out var ctx);

        await service.SeedDefaultsAsync();

        var seededRoles = ctx.RolePermissions
            .Where(x => x.ModuleName == "EngineeringDocuments")
            .Select(x => x.RoleName)
            .ToList();

        foreach (var role in AuthoredRoles)
            Assert.Contains(role, seededRoles);
        Assert.Equal(5, seededRoles.Count);
    }

    // ==== Scenario 2: Fully seeded — repeat seed produces no duplicates ====
    [Fact]
    public async Task Scenario2_FullySeeded_RepeatSeed_NoDuplicates()
    {
        var service = CreateService(nameof(Scenario2_FullySeeded_RepeatSeed_NoDuplicates), out var ctx);

        await service.SeedDefaultsAsync();
        var countAfterFirst = ctx.RolePermissions.Count(x => x.ModuleName == "EngineeringDocuments");
        Assert.Equal(5, countAfterFirst);

        await service.SeedDefaultsAsync();
        var countAfterSecond = ctx.RolePermissions.Count(x => x.ModuleName == "EngineeringDocuments");

        Assert.Equal(countAfterFirst, countAfterSecond);
        foreach (var role in AuthoredRoles)
        {
            var rowsForRole = ctx.RolePermissions.Count(x => x.ModuleName == "EngineeringDocuments" && x.RoleName == role);
            Assert.Equal(1, rowsForRole);
        }
    }

    // ==== Scenario 3: Partially seeded — only some roles exist, seed completes the rest ====
    [Fact]
    public async Task Scenario3_PartiallySeeded_OnlyMissingRolesAreCompleted_ExistingRolesPreserved()
    {
        var service = CreateService(nameof(Scenario3_PartiallySeeded_OnlyMissingRolesAreCompleted_ExistingRolesPreserved), out var ctx);

        // یک نقش از قبل با مقادیر عمداً متفاوت از تعریف Authored (شبیه‌سازی SetPermissionsAsync
        // دستی از پنل Admin) - این ردیف نباید Overwrite شود.
        var preExisting = new RolePermission
        {
            RoleName = SystemRoles.DocumentController,
            ModuleName = "EngineeringDocuments",
            CanView = true,
            CanCreate = false, // متفاوت از مقدار Authored (که Create=true است)
            CreatedDate = DateTime.Now
        };
        ctx.RolePermissions.Add(preExisting);
        await ctx.SaveChangesAsync();

        await service.SeedDefaultsAsync();

        var rows = ctx.RolePermissions.Where(x => x.ModuleName == "EngineeringDocuments").ToList();

        Assert.Equal(5, rows.Count);

        // نقش از‌قبل‌موجود دست‌نخورده ماند - Overwrite نشد
        var documentControllerRow = rows.Single(x => x.RoleName == SystemRoles.DocumentController);
        Assert.False(documentControllerRow.CanCreate);
        Assert.Equal(preExisting.RolePermissionId, documentControllerRow.RolePermissionId);

        // چهار نقش غایب دیگر با مقادیر Authored درست اضافه شدند
        var manager = rows.Single(x => x.RoleName == SystemRoles.EngineeringManager);
        Assert.True(manager.CanCreate);
        Assert.True(manager.CanDelete);
        Assert.True(manager.CanApprove);

        var supervisor = rows.Single(x => x.RoleName == SystemRoles.EngineeringSupervisor);
        Assert.True(supervisor.CanApprove);
        Assert.False(supervisor.CanDelete);

        var discipline = rows.Single(x => x.RoleName == SystemRoles.DisciplineEngineer);
        Assert.True(discipline.CanHold);
        Assert.False(discipline.CanApprove);

        var viewer = rows.Single(x => x.RoleName == SystemRoles.Viewer);
        Assert.True(viewer.CanView);
        Assert.False(viewer.CanCreate);
    }

    // ==== Scenario 4: Existing unrelated module (WorkOrder) — must stay unchanged, EngineeringDocuments still reconciled ====
    [Fact]
    public async Task Scenario4_ExistingWorkOrderSeed_WorkOrderUnchanged_EngineeringDocumentsReconciled()
    {
        var service = CreateService(nameof(Scenario4_ExistingWorkOrderSeed_WorkOrderUnchanged_EngineeringDocumentsReconciled), out var ctx);

        // شبیه‌سازی دقیق دیتابیس واقعی Dev قبل از رفع فاز ۵B: فقط WorkOrder Seed شده بود
        await service.SeedDefaultsAsync();
        var workOrderCountAfterFirst = ctx.RolePermissions.Count(x => x.ModuleName == "WorkOrder");
        Assert.True(workOrderCountAfterFirst > 0);

        await service.SeedDefaultsAsync();
        var workOrderCountAfterSecond = ctx.RolePermissions.Count(x => x.ModuleName == "WorkOrder");

        Assert.Equal(workOrderCountAfterFirst, workOrderCountAfterSecond);

        var engineeringDocumentsRoles = ctx.RolePermissions
            .Where(x => x.ModuleName == "EngineeringDocuments")
            .Select(x => x.RoleName)
            .ToList();
        foreach (var role in AuthoredRoles)
            Assert.Contains(role, engineeringDocumentsRoles);
    }

    [Fact]
    public async Task PermissionDefinitionVsGrant_AreVerifiedSeparately()
    {
        // بند بسیار مهم C-3: RolePermission (Definition) و RolePermissionGrant (Grant واقعی
        // روی AspNetRoles) دو چیز جدا هستند - این تست ثابت می‌کند Seed فقط RolePermission را
        // می‌سازد؛ AccessControlSeeder (مسیر جدا) مسئول تبدیل آن به RolePermissionGrant است.
        var service = CreateService(nameof(PermissionDefinitionVsGrant_AreVerifiedSeparately), out var ctx);

        await service.SeedDefaultsAsync();

        var definitionCount = ctx.RolePermissions.Count(x => x.ModuleName == "EngineeringDocuments");
        var grantCount = ctx.RolePermissionGrants.Count();

        Assert.Equal(5, definitionCount);
        Assert.Equal(0, grantCount); // SeedDefaultsAsync خودش RolePermissionGrant نمی‌سازد - این کار AccessControlSeeder است
    }
}
