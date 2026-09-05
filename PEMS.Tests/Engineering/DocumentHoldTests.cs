using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق بخش M ممیزی سخت‌سازی مهندسی (Hold Lifecycle): این تست‌ها مستقیماً روی
/// DocumentHoldService اجرا می‌شوند - همان نقطه‌ی واقعی SaveChanges که تاریخچه‌ی چندچرخه‌ای
/// در DocumentHold ثبت می‌شود، هم‌الگو با EngineeringDocumentFreezeTests.
/// </summary>
public class DocumentHoldTests
{
    private static async Task<(DocumentHoldService svc, EngineeringDocument doc, PEMS.Persistence.Context.PEMSDbContext ctx)> SeedAsync(string dbName, string status = "Draft", bool isFrozen = false)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        var doc = new EngineeringDocument
        {
            DocumentNo = "PRJ-GEN-DAT-0001",
            Title = "Test Document",
            DocumentType = "Datasheet",
            ProjectId = 1,
            Status = status,
            IsFrozen = isFrozen
        };

        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        return (new DocumentHoldService(ctx), doc, ctx);
    }

    [Fact]
    public async Task PlaceHoldAsync_SetsFieldsAndCreatesHistoryRow()
    {
        var (svc, doc, ctx) = await SeedAsync(nameof(PlaceHoldAsync_SetsFieldsAndCreatesHistoryRow));

        await svc.PlaceHoldAsync(doc.DocumentId, "Pending vendor datasheet", responsibleDisciplineId: null, actorEmployeeId: 42);

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.True(reloaded!.IsOnHold);

        var holds = ctx.DocumentHolds.Where(h => h.DocumentId == doc.DocumentId).ToList();
        Assert.Single(holds);
        Assert.Equal("Pending vendor datasheet", holds[0].Reason);
        Assert.Equal(42, holds[0].HoldByEmployeeId);
        Assert.Null(holds[0].ReleaseDate);
    }

    [Fact]
    public async Task PlaceHoldAsync_EmptyReason_Throws()
    {
        var (svc, doc, _) = await SeedAsync(nameof(PlaceHoldAsync_EmptyReason_Throws));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PlaceHoldAsync(doc.DocumentId, "", null, 1));
    }

    [Fact]
    public async Task PlaceHoldAsync_AlreadyOnHold_Throws()
    {
        var (svc, doc, _) = await SeedAsync(nameof(PlaceHoldAsync_AlreadyOnHold_Throws));
        await svc.PlaceHoldAsync(doc.DocumentId, "first hold", null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PlaceHoldAsync(doc.DocumentId, "second hold", null, 1));
    }

    [Fact]
    public async Task PlaceHoldAsync_FrozenDocument_Throws()
    {
        var (svc, doc, _) = await SeedAsync(nameof(PlaceHoldAsync_FrozenDocument_Throws), isFrozen: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PlaceHoldAsync(doc.DocumentId, "cannot hold frozen doc", null, 1));
    }

    [Fact]
    public async Task PlaceHoldAsync_IssuedDocument_Throws()
    {
        var (svc, doc, _) = await SeedAsync(nameof(PlaceHoldAsync_IssuedDocument_Throws), status: "Issued");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PlaceHoldAsync(doc.DocumentId, "cannot hold issued doc", null, 1));
    }

    [Fact]
    public async Task ReleaseHoldAsync_NotOnHold_Throws()
    {
        var (svc, doc, _) = await SeedAsync(nameof(ReleaseHoldAsync_NotOnHold_Throws));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ReleaseHoldAsync(doc.DocumentId, "not on hold", 1));
    }

    [Fact]
    public async Task ReleaseHoldAsync_ClearsIsOnHold_PreservesHoldRecord()
    {
        var (svc, doc, ctx) = await SeedAsync(nameof(ReleaseHoldAsync_ClearsIsOnHold_PreservesHoldRecord));
        await svc.PlaceHoldAsync(doc.DocumentId, "hold reason", null, 7);

        await svc.ReleaseHoldAsync(doc.DocumentId, "info received", 9);

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.False(reloaded!.IsOnHold);

        var hold = ctx.DocumentHolds.Single(h => h.DocumentId == doc.DocumentId);
        Assert.Equal(7, hold.HoldByEmployeeId);
        Assert.Equal("hold reason", hold.Reason);
        Assert.Equal(9, hold.ReleaseByEmployeeId);
        Assert.Equal("info received", hold.ReleaseReason);
        Assert.NotNull(hold.ReleaseDate);
    }

    [Fact]
    public async Task MultipleHoldReleaseCycles_ProduceFullHistory()
    {
        var (svc, doc, ctx) = await SeedAsync(nameof(MultipleHoldReleaseCycles_ProduceFullHistory));

        await svc.PlaceHoldAsync(doc.DocumentId, "hold 1", null, 1);
        await svc.ReleaseHoldAsync(doc.DocumentId, "release 1", 1);
        await svc.PlaceHoldAsync(doc.DocumentId, "hold 2", null, 2);
        await svc.ReleaseHoldAsync(doc.DocumentId, "release 2", 2);
        await svc.PlaceHoldAsync(doc.DocumentId, "hold 3", null, 3);

        var history = await svc.GetHistoryAsync(doc.DocumentId);

        Assert.Equal(3, history.Count);
        Assert.Equal(2, history.Count(h => !h.IsOpen));
        Assert.Single(history, h => h.IsOpen);

        var reloaded = await ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.True(reloaded!.IsOnHold);
    }
}
