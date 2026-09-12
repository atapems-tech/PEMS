using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS EDMS Phase 4 Audit» یافته P4-03: AIWorkflowStage (خط‌سیر AI Intake/Classification)
/// قبلاً وقتی به "Approved" می‌رسید، مستقیماً Document.Status (گردش‌کار رسمی مهندسی) را هم
/// بدون عبور از Submit/Check/DocumentWorkflowService بازنویسی می‌کرد - یک مسیر دوم و ناسازگار
/// برای همان فیلد اقتدارمند. این تست‌ها ثابت می‌کنند که دیگر این‌طور نیست.
/// </summary>
public class AIWorkflowStageAuthorityBoundaryTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, EngineeringDocumentIntelligenceService svc, EngineeringDocument doc)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        var vendorService = new VendorService(ctx);
        var svc = new EngineeringDocumentIntelligenceService(ctx, vendorService);

        var doc = new EngineeringDocument
        {
            DocumentNo = $"D-{Guid.NewGuid():N}"[..15],
            Title = "Test",
            DocumentType = "Datasheet",
            ProjectId = 0,
            Status = "Draft",
            AIWorkflowStage = "Uploaded"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        return (ctx, svc, doc);
    }

    [Fact]
    public async Task AdvanceStageAsync_ToApproved_UpdatesAIWorkflowStage_ButNeverTouchesDocumentStatus()
    {
        var (ctx, svc, doc) = await SeedAsync(nameof(AdvanceStageAsync_ToApproved_UpdatesAIWorkflowStage_ButNeverTouchesDocumentStatus));

        await svc.AdvanceStageAsync(doc.DocumentId, "Approved");

        var reloaded = await ctx.EngineeringDocuments.AsNoTracking().FirstAsync(x => x.DocumentId == doc.DocumentId);

        Assert.Equal("Approved", reloaded.AIWorkflowStage);
        // مدرک هنوز Draft است - AI هرگز نباید به‌تنهایی گردش‌کار رسمی را جلو ببرد
        Assert.Equal("Draft", reloaded.Status);
    }

    [Fact]
    public async Task AdvanceStageAsync_EvenFromCheckedStatus_DoesNotOverwriteDocumentStatus()
    {
        // سناریوی سخت‌گیرانه‌تر: حتی اگر مدرک از قبل واقعاً در گردش‌کار رسمی جلو رفته باشد
        // (Checked)، رسیدن خط‌سیر AI به Approved نباید آن‌را دوباره بازنویسی کند
        var (ctx, svc, doc) = await SeedAsync(nameof(AdvanceStageAsync_EvenFromCheckedStatus_DoesNotOverwriteDocumentStatus));
        doc.Status = "Checked";
        await ctx.SaveChangesAsync();

        await svc.AdvanceStageAsync(doc.DocumentId, "Approved");

        var reloaded = await ctx.EngineeringDocuments.AsNoTracking().FirstAsync(x => x.DocumentId == doc.DocumentId);
        Assert.Equal("Checked", reloaded.Status);
        Assert.Equal("Approved", reloaded.AIWorkflowStage);
    }

    [Fact]
    public async Task AdvanceStageAsync_InvalidStage_Throws()
    {
        var (_, svc, doc) = await SeedAsync(nameof(AdvanceStageAsync_InvalidStage_Throws));

        await Assert.ThrowsAsync<Exception>(() => svc.AdvanceStageAsync(doc.DocumentId, "NotARealStage"));
    }
}
