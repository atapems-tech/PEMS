using PEMS.Application.Exceptions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-01 (رفع P0 ممیزی Freeze): این تست‌ها مستقیماً روی EngineeringDocumentRepository
/// اجرا می‌شوند - همان نقطه‌ی واقعی SaveChanges که گارد IsFrozen در آن پیاده شده، نه یک شبیه‌سازی
/// جدا. InMemory کافی است چون این مسیر به SQL خام (مثل NumberingService) وابسته نیست.
/// </summary>
public class EngineeringDocumentFreezeTests
{
    private static async Task<(EngineeringDocumentRepository repo, EngineeringDocument doc, PEMS.Persistence.Context.PEMSDbContext ctx)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        var doc = new EngineeringDocument
        {
            DocumentNo = "PRJ-GEN-DAT-0001",
            Title = "Test Document",
            DocumentType = "Datasheet",
            ProjectId = 1,
            Status = "Draft"
        };

        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        return (new EngineeringDocumentRepository(ctx), doc, ctx);
    }

    [Fact]
    public async Task FreezeAsync_SetsFreezeFields()
    {
        var (repo, doc, ctx) = await SeedAsync(nameof(FreezeAsync_SetsFreezeFields));

        await repo.FreezeAsync(doc.DocumentId, actorEmployeeId: 42, reason: "Issued for Construction");

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsFrozen);
        Assert.Equal(42, reloaded.FrozenById);
        Assert.Equal("Issued for Construction", reloaded.FreezeReason);
        Assert.NotNull(reloaded.FrozenDate);
    }

    [Fact]
    public async Task FreezeAsync_AlreadyFrozen_Throws()
    {
        var (repo, doc, _) = await SeedAsync(nameof(FreezeAsync_AlreadyFrozen_Throws));

        await repo.FreezeAsync(doc.DocumentId, 1, "first freeze");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.FreezeAsync(doc.DocumentId, 1, "second freeze"));
    }

    [Fact]
    public async Task UnfreezeAsync_NotFrozen_Throws()
    {
        var (repo, doc, _) = await SeedAsync(nameof(UnfreezeAsync_NotFrozen_Throws));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.UnfreezeAsync(doc.DocumentId, 1, "not frozen yet"));
    }

    [Fact]
    public async Task UnfreezeAsync_PreservesOriginalFreezeHistory()
    {
        var (repo, doc, ctx) = await SeedAsync(nameof(UnfreezeAsync_PreservesOriginalFreezeHistory));

        await repo.FreezeAsync(doc.DocumentId, 7, "original freeze reason");
        await repo.UnfreezeAsync(doc.DocumentId, 9, "unfreeze reason");

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsFrozen);

        // تاریخچه‌ی انجماد اصلی نباید پاک شود - فقط فیلدهای Unfrozen* جدید اضافه می‌شوند
        Assert.Equal(7, reloaded.FrozenById);
        Assert.Equal("original freeze reason", reloaded.FreezeReason);
        Assert.Equal(9, reloaded.UnfrozenById);
        Assert.Equal("unfreeze reason", reloaded.UnfreezeReason);
        Assert.NotNull(reloaded.UnfrozenDate);
    }

    [Fact]
    public async Task UpdateAsync_OnFrozenDocument_Throws_DirectBypassResistance()
    {
        var (repo, doc, _) = await SeedAsync(nameof(UpdateAsync_OnFrozenDocument_Throws_DirectBypassResistance));
        await repo.FreezeAsync(doc.DocumentId, 1, "frozen for test");

        // شبیه‌سازی یک درخواست مستقیم API/Controller که سعی می‌کند مدرک منجمد را ویرایش کند -
        // بدون رفتن از مسیر View. باید همین‌جا (لایه‌ی Persistence، نه فقط Controller) رد شود.
        var editAttempt = new EngineeringDocument
        {
            DocumentId = doc.DocumentId,
            DocumentNo = "TAMPERED-0001",
            Title = "Tampered Title",
            DocumentType = doc.DocumentType,
            ProjectId = doc.ProjectId
        };

        await Assert.ThrowsAsync<DocumentFrozenException>(() => repo.UpdateAsync(editAttempt));
    }

    [Fact]
    public async Task UpdateAsync_OnNonFrozenDocument_Succeeds()
    {
        var (repo, doc, ctx) = await SeedAsync(nameof(UpdateAsync_OnNonFrozenDocument_Succeeds));

        var edit = new EngineeringDocument
        {
            DocumentId = doc.DocumentId,
            DocumentNo = doc.DocumentNo,
            Title = "Updated Title",
            DocumentType = doc.DocumentType,
            ProjectId = doc.ProjectId
        };

        await repo.UpdateAsync(edit);

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.Equal("Updated Title", reloaded!.Title);
    }
}
