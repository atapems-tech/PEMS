using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق ممیزی فاز ۵ (Permission Seed Audit): PermissionService.SeedDefaultsAsync قبلاً یک گارد
/// سراسری «اگه هر ردیفی هست کلاً رد شو» داشت که همیشه بعد از بلوک‌های WorkOrder/
/// StoreRequisition/WorkRequest/Inspection/Transmittal اجرا می‌شد - چون آن بلوک‌ها همیشه اول
/// رکورد اضافه می‌کنند، این ۸ ماژول (EngineeringDocuments/VDR/Contracts/Vendors/
/// MaterialRequisition/Equipment/DisciplineWorkspace/Correspondence) هرگز Seed نمی‌شدند، حتی
/// روی یک دیتابیس کاملاً خالی. این تست‌ها ثابت می‌کنند با گارد Idempotent هر-ماژول-جدا (همون
/// الگوی WorkOrder)، همه‌ی ماژول‌ها روی یک دیتابیس خالی Seed می‌شوند، و اجرای دوباره (Re-run)
/// هیچ ردیف تکراری اضافه نمی‌کند و هیچ داده‌ی موجودی را دست‌نمی‌زند.
/// </summary>
public class PermissionSeedGapFixTests
{
    private static readonly string[] PreviouslyDeadModules =
    {
        "EngineeringDocuments", "VDR", "Contracts", "Vendors",
        "MaterialRequisition", "Equipment", "DisciplineWorkspace", "Correspondence"
    };

    private static readonly string[] AlreadyWorkingModules =
    {
        "WorkOrder", "StoreRequisition", "WorkRequest", "Inspection", "Transmittal"
    };

    [Fact]
    public async Task SeedDefaultsAsync_OnEmptyDatabase_SeedsAllPreviouslyDeadModules()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(SeedDefaultsAsync_OnEmptyDatabase_SeedsAllPreviouslyDeadModules));
        var service = new PermissionService(ctx, new AccessControlService(ctx));

        await service.SeedDefaultsAsync();

        foreach (var module in PreviouslyDeadModules)
        {
            var count = ctx.RolePermissions.Count(x => x.ModuleName == module);
            Assert.True(count > 0, $"Expected {module} to have seeded RolePermission rows, but found none.");
        }
    }

    [Fact]
    public async Task SeedDefaultsAsync_OnEmptyDatabase_StillSeedsAlreadyWorkingModules()
    {
        // طبق اطمینان از عدم Regression: ۵ ماژولی که قبلاً هم درست کار می‌کردند (گارد جداگانه‌ی
        // خودشان بالای متد) نباید تحت تأثیر این بازآرایی قرار بگیرند.
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(SeedDefaultsAsync_OnEmptyDatabase_StillSeedsAlreadyWorkingModules));
        var service = new PermissionService(ctx, new AccessControlService(ctx));

        await service.SeedDefaultsAsync();

        foreach (var module in AlreadyWorkingModules)
        {
            var count = ctx.RolePermissions.Count(x => x.ModuleName == module);
            Assert.True(count > 0, $"Expected {module} to still have seeded RolePermission rows.");
        }
    }

    [Fact]
    public async Task SeedDefaultsAsync_CalledTwice_DoesNotDuplicateRows()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(SeedDefaultsAsync_CalledTwice_DoesNotDuplicateRows));
        var service = new PermissionService(ctx, new AccessControlService(ctx));

        await service.SeedDefaultsAsync();
        var countsAfterFirstRun = (PreviouslyDeadModules.Concat(AlreadyWorkingModules))
            .ToDictionary(m => m, m => ctx.RolePermissions.Count(x => x.ModuleName == m));

        await service.SeedDefaultsAsync();

        foreach (var module in PreviouslyDeadModules.Concat(AlreadyWorkingModules))
        {
            var countAfterSecondRun = ctx.RolePermissions.Count(x => x.ModuleName == module);
            Assert.Equal(countsAfterFirstRun[module], countAfterSecondRun);
        }
    }

    [Fact]
    public async Task SeedDefaultsAsync_OnDatabaseWithOnlyWorkOrderSeeded_StillAddsTheOtherEightModules()
    {
        // دقیقاً سناریوی باگ اصلی: یک دیتابیس که فقط WorkOrder را از اجرای قبلی این متد دارد
        // (شبیه‌سازی PEMSDB واقعی توسعه) - قبل از این رفع، این حالت باعث می‌شد گارد سراسری
        // AnyAsync() بلافاصله true برگردد و ۸ ماژول دیگر هرگز Seed نشوند.
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(SeedDefaultsAsync_OnDatabaseWithOnlyWorkOrderSeeded_StillAddsTheOtherEightModules));
        var service = new PermissionService(ctx, new AccessControlService(ctx));

        await service.SeedDefaultsAsync();
        Assert.True(ctx.RolePermissions.Count(x => x.ModuleName == "WorkOrder") > 0);

        foreach (var module in PreviouslyDeadModules)
        {
            var count = ctx.RolePermissions.Count(x => x.ModuleName == module);
            Assert.True(count > 0, $"Expected {module} to have seeded RolePermission rows even though WorkOrder already existed.");
        }
    }
}
