using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System»: تست‌های هسته‌ی مشترک شناسایی - یکتایی، پایداری در
/// برابر تغییر فیلدهای تغییرپذیر، محافظت از تکرار، چرخه‌عمر (Disable/Revoke/Replace)، Resolve،
/// و Bulk Generation.
/// </summary>
public class BarcodeServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"Barcode_{dbName}");

    private static async Task<Equipment> SeedEquipmentAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var eq = new Equipment { TagNumber = tag, Name = tag, EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        return eq;
    }

    // ================= Identity =================

    [Fact]
    public async Task GenerateAsync_NewTarget_CreatesUniqueActiveBarcode()
    {
        var ctx = CreateContext(nameof(GenerateAsync_NewTarget_CreatesUniqueActiveBarcode));
        var eq = await SeedEquipmentAsync(ctx, "EQ-1");
        var service = new BarcodeService(ctx);

        var (barcode, wasExisting) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        Assert.False(wasExisting);
        Assert.StartsWith("PEMS-BR-", barcode.Code);
        Assert.Equal("Active", barcode.Status);
        Assert.Single(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task GenerateAsync_CodeDoesNotContainMutableBusinessData()
    {
        // طبق بند «Barcode Identity Model»: بارکد هرگز داده‌ی کسب‌وکاری (Tag/Description) را حمل نمی‌کند
        var ctx = CreateContext(nameof(GenerateAsync_CodeDoesNotContainMutableBusinessData));
        var eq = await SeedEquipmentAsync(ctx, "SECRET-TAG-123");
        var service = new BarcodeService(ctx);

        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        Assert.DoesNotContain("SECRET-TAG-123", barcode.Code);
    }

    [Fact]
    public async Task GenerateAsync_SameTargetTwice_ReturnsSameActiveBarcode_Idempotent()
    {
        var ctx = CreateContext(nameof(GenerateAsync_SameTargetTwice_ReturnsSameActiveBarcode_Idempotent));
        var eq = await SeedEquipmentAsync(ctx, "EQ-2");
        var service = new BarcodeService(ctx);

        var (first, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);
        var (second, wasExisting) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        Assert.True(wasExisting);
        Assert.Equal(first.Code, second.Code);
        Assert.Single(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task GenerateAsync_UnsupportedTargetType_Throws()
    {
        var ctx = CreateContext(nameof(GenerateAsync_UnsupportedTargetType_Throws));
        var service = new BarcodeService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("Vendor", 1, "Code128", null));
    }

    [Fact]
    public async Task GenerateAsync_NonexistentEquipmentId_Throws_NoOrphanRecordCreated()
    {
        // طبق ممیزی نهایی «Database Integrity»: هرگز نباید یک BarcodeIdentity یتیم (اشاره به
        // Equipment ناموجود) ساخته شود - حتی اگر نوع هدف پشتیبانی‌شده باشد.
        var ctx = CreateContext(nameof(GenerateAsync_NonexistentEquipmentId_Throws_NoOrphanRecordCreated));
        var service = new BarcodeService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("Equipment", 99999, "Code128", null));

        Assert.Empty(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task GenerateAsync_TwoDifferentTargets_DoNotCollideOnCode()
    {
        var ctx = CreateContext(nameof(GenerateAsync_TwoDifferentTargets_DoNotCollideOnCode));
        var e1 = await SeedEquipmentAsync(ctx, "EQ-3");
        var e2 = await SeedEquipmentAsync(ctx, "EQ-4");
        var service = new BarcodeService(ctx);

        var (b1, _) = await service.GenerateAsync("Equipment", e1.EquipmentId, "Code128", null);
        var (b2, _) = await service.GenerateAsync("Equipment", e2.EquipmentId, "Code128", null);

        Assert.NotEqual(b1.Code, b2.Code);
    }

    // ================= Stability =================

    [Fact]
    public async Task GetActiveByTargetAsync_AfterTagChange_BarcodeRemainsSameAndValid()
    {
        // طبق بند «Stability»: تغییر Tag یک تجهیز هرگز بارکد را باطل نمی‌کند - بارکد فقط به
        // EquipmentId وصل است، نه به هیچ فیلد تغییرپذیری.
        var ctx = CreateContext(nameof(GetActiveByTargetAsync_AfterTagChange_BarcodeRemainsSameAndValid));
        var eq = await SeedEquipmentAsync(ctx, "OLD-TAG");
        var service = new BarcodeService(ctx);
        var (original, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        eq.TagNumber = "NEW-TAG-AFTER-RENAME";
        await ctx.SaveChangesAsync();

        var stillActive = await service.GetActiveByTargetAsync("Equipment", eq.EquipmentId);

        Assert.NotNull(stillActive);
        Assert.Equal(original.Code, stillActive!.Code);
        Assert.Equal("Active", stillActive.Status);
    }

    [Fact]
    public async Task ResolveAsync_AfterFunctionalLocationAssignmentChange_StillResolvesSameTarget()
    {
        var ctx = CreateContext(nameof(ResolveAsync_AfterFunctionalLocationAssignmentChange_StillResolvesSameTarget));
        var eq = await SeedEquipmentAsync(ctx, "EQ-FL");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        eq.FunctionalLocationId = 999; // تغییر محل استقرار - نباید بارکد را باطل کند
        await ctx.SaveChangesAsync();

        var result = await service.ResolveAsync(barcode.Code);

        Assert.True(result.Success);
        Assert.Equal(eq.EquipmentId, result.TargetId);
    }

    // ================= Resolution =================

    [Fact]
    public async Task ResolveAsync_ValidActiveBarcode_Succeeds()
    {
        var ctx = CreateContext(nameof(ResolveAsync_ValidActiveBarcode_Succeeds));
        var eq = await SeedEquipmentAsync(ctx, "EQ-5");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        var result = await service.ResolveAsync(barcode.Code);

        Assert.True(result.Success);
        Assert.Equal("Equipment", result.TargetType);
        Assert.Equal(eq.EquipmentId, result.TargetId);
    }

    [Fact]
    public async Task ResolveAsync_UnknownCode_ReturnsNotFound()
    {
        var ctx = CreateContext(nameof(ResolveAsync_UnknownCode_ReturnsNotFound));
        var service = new BarcodeService(ctx);

        var result = await service.ResolveAsync("PEMS-BR-DOESNOTEXIST01");

        Assert.False(result.Success);
        Assert.Equal("BARCODE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_EmptyCode_ReturnsInvalid()
    {
        var ctx = CreateContext(nameof(ResolveAsync_EmptyCode_ReturnsInvalid));
        var service = new BarcodeService(ctx);

        var result = await service.ResolveAsync("");

        Assert.False(result.Success);
        Assert.Equal("BARCODE_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_DisabledBarcode_Rejected()
    {
        var ctx = CreateContext(nameof(ResolveAsync_DisabledBarcode_Rejected));
        var eq = await SeedEquipmentAsync(ctx, "EQ-6");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);
        await service.DisableAsync(barcode.BarcodeIdentityId, "test", null);

        var result = await service.ResolveAsync(barcode.Code);

        Assert.False(result.Success);
        Assert.Equal("BARCODE_DISABLED", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_RevokedBarcode_Rejected()
    {
        var ctx = CreateContext(nameof(ResolveAsync_RevokedBarcode_Rejected));
        var eq = await SeedEquipmentAsync(ctx, "EQ-7");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);
        await service.RevokeAsync(barcode.BarcodeIdentityId, "test", null);

        var result = await service.ResolveAsync(barcode.Code);

        Assert.False(result.Success);
        Assert.Equal("BARCODE_REVOKED", result.ErrorCode);
    }

    // ================= Lifecycle =================

    [Fact]
    public async Task DisableAsync_ActiveBarcode_Succeeds_AndBlocksReDisable()
    {
        var ctx = CreateContext(nameof(DisableAsync_ActiveBarcode_Succeeds_AndBlocksReDisable));
        var eq = await SeedEquipmentAsync(ctx, "EQ-8");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        var (success1, _) = await service.DisableAsync(barcode.BarcodeIdentityId, "damaged label", null);
        var (success2, error2) = await service.DisableAsync(barcode.BarcodeIdentityId, "again", null);

        Assert.True(success1);
        Assert.False(success2);
        Assert.NotNull(error2);
    }

    [Fact]
    public async Task RevokeAsync_NeverDeletesRecord_HistoryPreserved()
    {
        var ctx = CreateContext(nameof(RevokeAsync_NeverDeletesRecord_HistoryPreserved));
        var eq = await SeedEquipmentAsync(ctx, "EQ-9");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        await service.RevokeAsync(barcode.BarcodeIdentityId, "lost", null);

        Assert.Single(ctx.BarcodeIdentities); // رکورد حذف نشد - فقط Status تغییر کرد
        var stillThere = await service.GetByIdAsync(barcode.BarcodeIdentityId);
        Assert.NotNull(stillThere);
        Assert.Equal("Revoked", stillThere!.Status);
    }

    [Fact]
    public async Task ReplaceAsync_RevokesOldAndCreatesNewForSameTarget()
    {
        var ctx = CreateContext(nameof(ReplaceAsync_RevokesOldAndCreatesNewForSameTarget));
        var eq = await SeedEquipmentAsync(ctx, "EQ-10");
        var service = new BarcodeService(ctx);
        var (oldBarcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        var (newBarcode, error) = await service.ReplaceAsync(oldBarcode.BarcodeIdentityId, "QR", null);

        Assert.Null(error);
        Assert.NotNull(newBarcode);
        Assert.NotEqual(oldBarcode.Code, newBarcode!.Code);

        var oldReloaded = await service.GetByIdAsync(oldBarcode.BarcodeIdentityId);
        Assert.Equal("Replaced", oldReloaded!.Status);
        Assert.Equal(newBarcode.BarcodeIdentityId, oldReloaded.ReplacedByBarcodeIdentityId);

        var activeNow = await service.GetActiveByTargetAsync("Equipment", eq.EquipmentId);
        Assert.Equal(newBarcode.Code, activeNow!.Code);
    }

    [Fact]
    public async Task RecordPrintAsync_IncrementsPrintCount()
    {
        var ctx = CreateContext(nameof(RecordPrintAsync_IncrementsPrintCount));
        var eq = await SeedEquipmentAsync(ctx, "EQ-11");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        await service.RecordPrintAsync(barcode.BarcodeIdentityId);
        await service.RecordPrintAsync(barcode.BarcodeIdentityId);

        var reloaded = await service.GetByIdAsync(barcode.BarcodeIdentityId);
        Assert.Equal(2, reloaded!.PrintCount);
    }

    [Fact]
    public async Task RecordPrintAsync_DisabledBarcode_Rejected()
    {
        var ctx = CreateContext(nameof(RecordPrintAsync_DisabledBarcode_Rejected));
        var eq = await SeedEquipmentAsync(ctx, "EQ-12");
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);
        await service.DisableAsync(barcode.BarcodeIdentityId, null, null);

        var (success, error) = await service.RecordPrintAsync(barcode.BarcodeIdentityId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ================= Bulk =================

    [Fact]
    public async Task BulkGenerateAsync_MixOfNewAndExisting_CountsCorrectly()
    {
        var ctx = CreateContext(nameof(BulkGenerateAsync_MixOfNewAndExisting_CountsCorrectly));
        var e1 = await SeedEquipmentAsync(ctx, "BULK-1");
        var e2 = await SeedEquipmentAsync(ctx, "BULK-2");
        var e3 = await SeedEquipmentAsync(ctx, "BULK-3");
        var service = new BarcodeService(ctx);
        await service.GenerateAsync("Equipment", e1.EquipmentId, "Code128", null); // از قبل موجود

        var result = await service.BulkGenerateAsync("Equipment", new List<int> { e1.EquipmentId, e2.EquipmentId, e3.EquipmentId }, "Code128", null);

        Assert.Equal(3, result.Requested);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.AlreadyExisted);
        Assert.Equal(3, await ctx.BarcodeIdentities.CountAsync(x => x.TargetType == "Equipment"));
    }

    [Fact]
    public async Task BulkGenerateAsync_DuplicateIdsInRequest_DeduplicatedNotDoubleCreated()
    {
        var ctx = CreateContext(nameof(BulkGenerateAsync_DuplicateIdsInRequest_DeduplicatedNotDoubleCreated));
        var eq = await SeedEquipmentAsync(ctx, "BULK-DUP");
        var service = new BarcodeService(ctx);

        var result = await service.BulkGenerateAsync("Equipment", new List<int> { eq.EquipmentId, eq.EquipmentId }, "Code128", null);

        Assert.Equal(1, result.Created);
        Assert.Single(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task BulkGenerateAsync_UnsupportedTargetType_AllFail_NoRecordsCreated()
    {
        var ctx = CreateContext(nameof(BulkGenerateAsync_UnsupportedTargetType_AllFail_NoRecordsCreated));
        var service = new BarcodeService(ctx);

        var result = await service.BulkGenerateAsync("Vendor", new List<int> { 1, 2, 3 }, "Code128", null);

        Assert.Equal(3, result.Failed);
        Assert.Empty(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task BulkGenerateAsync_SomeTargetIdsDoNotExist_ExcludedAsFailed_NoOrphanRecords()
    {
        // طبق ممیزی نهایی «Bulk Generation Safety»: شناسه‌های ارسالی سمت سرور دوباره اعتبارسنجی
        // می‌شوند - مثلا تجهیزی که بین رندر صفحه و ارسال فرم حذف شده - نه فقط اعتماد به فیلتر قبلی.
        var ctx = CreateContext(nameof(BulkGenerateAsync_SomeTargetIdsDoNotExist_ExcludedAsFailed_NoOrphanRecords));
        var real = await SeedEquipmentAsync(ctx, "BULK-REAL");
        var service = new BarcodeService(ctx);

        var result = await service.BulkGenerateAsync("Equipment", new List<int> { real.EquipmentId, 88888, 99999 }, "Code128", null);

        Assert.Equal(1, result.Created);
        Assert.Equal(2, result.Failed);
        Assert.Single(ctx.BarcodeIdentities);
        Assert.Equal(real.EquipmentId, ctx.BarcodeIdentities.Single().TargetId);
    }
}
