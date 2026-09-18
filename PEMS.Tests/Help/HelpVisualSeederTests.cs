using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Help System — Phase 3» بخش ۱۵: تست‌های HelpVisualSeeder - ایدمپوتنت‌بودن (کلید
/// HelpArticleId+VisualType+CaptionEn)، کامل‌بودن دوزبانه‌ی هر ردیف Seed شده، و این‌که هر
/// WorkflowDiagram فقط به مقاله‌ای که واقعاً وجود دارد وصل می‌شود (Seed هرگز مقاله‌ی جعلی
/// نمی‌سازد - فقط اگر Article.Code از‌قبل seed شده باشد وصل می‌کند).
/// </summary>
public class HelpVisualSeederTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"HelpVisualSeeder_{dbName}");

    private static async Task SeedAllAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await HelpContentSeeder.SeedAsync(provider);
        await HelpVisualSeeder.SeedAsync(provider);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotCreateDuplicateVisuals()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_DoesNotCreateDuplicateVisuals));

        await SeedAllAsync(ctx);
        var countAfterFirstRun = await ctx.HelpVisuals.CountAsync();

        await SeedAllAsync(ctx);
        var countAfterSecondRun = await ctx.HelpVisuals.CountAsync();

        Assert.Equal(countAfterFirstRun, countAfterSecondRun);
        Assert.True(countAfterFirstRun > 0);
    }

    [Fact]
    public async Task SeedAsync_EveryVisual_HasBothLanguageCaptionsAndAccessibilityDescriptions()
    {
        var ctx = CreateContext(nameof(SeedAsync_EveryVisual_HasBothLanguageCaptionsAndAccessibilityDescriptions));
        await SeedAllAsync(ctx);

        var visuals = await ctx.HelpVisuals.ToListAsync();

        Assert.NotEmpty(visuals);
        foreach (var v in visuals)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.CaptionEn), $"Visual {v.HelpVisualId} missing CaptionEn");
            Assert.False(string.IsNullOrWhiteSpace(v.CaptionFa), $"Visual {v.HelpVisualId} missing CaptionFa");
            Assert.False(string.IsNullOrWhiteSpace(v.AccessibilityDescriptionEn), $"Visual {v.HelpVisualId} missing AccessibilityDescriptionEn");
            Assert.False(string.IsNullOrWhiteSpace(v.AccessibilityDescriptionFa), $"Visual {v.HelpVisualId} missing AccessibilityDescriptionFa");
        }
    }

    [Fact]
    public async Task SeedAsync_WorkflowDiagramVisuals_AllHaveSourceModuleAndCategory()
    {
        var ctx = CreateContext(nameof(SeedAsync_WorkflowDiagramVisuals_AllHaveSourceModuleAndCategory));
        await SeedAllAsync(ctx);

        var workflowVisuals = await ctx.HelpVisuals.Where(v => v.VisualType == "WorkflowDiagram").ToListAsync();

        Assert.NotEmpty(workflowVisuals);
        foreach (var v in workflowVisuals)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.SourceWorkflowModule));
            Assert.False(string.IsNullOrWhiteSpace(v.SourceWorkflowCategory));
            Assert.Null(v.DefinitionJson); // طبق طراحی: هرگز یک کپی دستی برای نوع WorkflowDiagram ذخیره نمی‌شود
        }
    }

    [Fact]
    public async Task SeedAsync_ManualVisuals_AllHaveNonEmptyDefinitionJson_AndNoSourceModule()
    {
        var ctx = CreateContext(nameof(SeedAsync_ManualVisuals_AllHaveNonEmptyDefinitionJson_AndNoSourceModule));
        await SeedAllAsync(ctx);

        var manualVisuals = await ctx.HelpVisuals.Where(v => v.VisualType != "WorkflowDiagram").ToListAsync();

        Assert.NotEmpty(manualVisuals);
        foreach (var v in manualVisuals)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.DefinitionJson));
            Assert.Null(v.SourceWorkflowModule);
            Assert.Null(v.SourceWorkflowCategory);
        }
    }

    [Fact]
    public async Task SeedAsync_EveryVisual_IsAttachedToAnArticleThatActuallyExists()
    {
        var ctx = CreateContext(nameof(SeedAsync_EveryVisual_IsAttachedToAnArticleThatActuallyExists));
        await SeedAllAsync(ctx);

        var articleIds = await ctx.HelpArticles.Select(a => a.HelpArticleId).ToListAsync();
        var visualArticleIds = await ctx.HelpVisuals.Select(v => v.HelpArticleId).Distinct().ToListAsync();

        Assert.All(visualArticleIds, id => Assert.Contains(id, articleIds));
    }

    [Fact]
    public async Task SeedAsync_IdcArticle_HasBothWorkflowDiagramAndStepGuide()
    {
        // این مقاله عمداً دو نوع محتوای بصری دارد - تایید می‌کند کلید ایدمپوتنت
        // (HelpArticleId+VisualType+CaptionEn) دو ردیف واقعاً متفاوت را اشتباهی یکی نمی‌بیند
        var ctx = CreateContext(nameof(SeedAsync_IdcArticle_HasBothWorkflowDiagramAndStepGuide));
        await SeedAllAsync(ctx);

        var idcArticle = await ctx.HelpArticles.SingleAsync(a => a.Code == "idc-inter-discipline-check");
        var visuals = await ctx.HelpVisuals.Where(v => v.HelpArticleId == idcArticle.HelpArticleId).ToListAsync();

        Assert.Contains(visuals, v => v.VisualType == "WorkflowDiagram");
        Assert.Contains(visuals, v => v.VisualType == "StepGuide");
    }
}
