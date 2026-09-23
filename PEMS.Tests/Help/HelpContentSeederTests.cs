using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۱۵ (ایدمپوتنت بودن Seed) و بخش ۳
/// (الزام غیرقابل‌مذاکره‌ی دوزبانه‌بودن از همان ابتدا).
/// </summary>
public class HelpContentSeederTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"HelpSeeder_{dbName}");

    private static async Task SeedAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await HelpContentSeeder.SeedAsync(provider);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotCreateDuplicateArticles()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_DoesNotCreateDuplicateArticles));

        await SeedAsync(ctx);
        var countAfterFirstRun = await ctx.HelpArticles.CountAsync();

        await SeedAsync(ctx);
        var countAfterSecondRun = await ctx.HelpArticles.CountAsync();

        Assert.Equal(countAfterFirstRun, countAfterSecondRun);
        Assert.True(countAfterFirstRun > 0);
    }

    [Fact]
    public async Task SeedAsync_EveryArticle_HasBothEnglishAndPersianTranslations()
    {
        var ctx = CreateContext(nameof(SeedAsync_EveryArticle_HasBothEnglishAndPersianTranslations));
        await SeedAsync(ctx);

        var articles = await ctx.HelpArticles.Include(a => a.Translations).ToListAsync();

        Assert.NotEmpty(articles);
        foreach (var article in articles)
        {
            Assert.Contains(article.Translations, t => t.LanguageCode == "en" && !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Content));
            Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Content));
        }
    }

    [Fact]
    public async Task SeedAsync_AllContextHelpKeysAreUnique_PerPageType()
    {
        // طبق بخش ۵ سند: HelpKey باید پایدار و بدون‌ابهام باشد - حداقل برای مقالات نوع Page
        var ctx = CreateContext(nameof(SeedAsync_AllContextHelpKeysAreUnique_PerPageType));
        await SeedAsync(ctx);

        var pageKeys = await ctx.HelpArticles
            .Where(a => a.ArticleType == "Page" && a.HelpKey != null)
            .Select(a => a.HelpKey)
            .ToListAsync();

        Assert.Equal(pageKeys.Count, pageKeys.Distinct().Count());
    }

    [Fact]
    public async Task SeedAsync_RelatedArticleLinks_AreCreated()
    {
        var ctx = CreateContext(nameof(SeedAsync_RelatedArticleLinks_AreCreated));
        await SeedAsync(ctx);

        var relatedCount = await ctx.HelpRelatedArticles.CountAsync();

        Assert.True(relatedCount > 0);
    }

    [Fact]
    public async Task SeedAsync_KeyEngineeringHelpKeys_ExistAndArePublished()
    {
        // طبق ادغام واقعی: صفحاتی که واقعاً ViewData["HelpKey"] را ست می‌کنند باید محتوای
        // منتشرشده‌ی متناظر داشته باشند - این تست رگرسیون واقعیِ ادغام است
        var ctx = CreateContext(nameof(SeedAsync_KeyEngineeringHelpKeys_ExistAndArePublished));
        await SeedAsync(ctx);

        var expectedKeys = new[]
        {
            "EngineeringDocuments.Index", "EngineeringDocuments.Details", "Employee.Details",
            "WorkRequests.Index", "FunctionalLocations.Index", "MaintenanceStrategies.Index"
        };

        foreach (var key in expectedKeys)
        {
            var article = await ctx.HelpArticles.FirstOrDefaultAsync(a => a.HelpKey == key);
            Assert.True(article != null, $"Expected a Help article with HelpKey '{key}'");
            Assert.True(article!.IsPublished, $"Article for HelpKey '{key}' should be published");
        }
    }

    // ==============================================================================
    // طبق «PEMS Help System — Phase 2» بخش ۵: هر HelpKey تازه‌سیم‌شده در کنترلرهای واقعی
    // (تایید‌شده با Grep مستقیم قبل از نوشتن محتوا) باید به یک مقاله‌ی منتشرشده و دوزبانه
    // برسد - این همان چیزی است که مکانیزم Contextual Help واقعاً هنگام رندر صفحه اجرا می‌کند.
    // ==============================================================================
    [Fact]
    public async Task SeedAsync_Phase2HelpKeys_ExistAndArePublished_AndAreBilingual()
    {
        var ctx = CreateContext(nameof(SeedAsync_Phase2HelpKeys_ExistAndArePublished_AndAreBilingual));
        await SeedAsync(ctx);

        var expectedKeys = new[]
        {
            "Equipment.Index", "Equipment.Details", "WorkOrders.Index", "WorkOrders.Details",
            "MaintenancePlans.Index", "DocumentRegister.Index", "Transmittals.Index", "VDR.Index",
            "MaterialRequisitions.Index", "Permits.Index", "MaterialItems.Index", "Departments.Index",
            "Inspections.Index", "Admin.Users.Index", "Admin.Roles.Index",
            "Admin.PermissionMatrix.Index", "Admin.AuditLog.Index"
        };

        foreach (var key in expectedKeys)
        {
            var article = await ctx.HelpArticles.Include(a => a.Translations).FirstOrDefaultAsync(a => a.HelpKey == key);
            Assert.True(article != null, $"Expected a Help article with HelpKey '{key}'");
            Assert.True(article!.IsPublished, $"Article for HelpKey '{key}' should be published");
            Assert.Contains(article.Translations, t => t.LanguageCode == "en" && !string.IsNullOrWhiteSpace(t.Content));
            Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && !string.IsNullOrWhiteSpace(t.Content));
        }
    }

    [Fact]
    public async Task SeedAsync_TotalArticleCount_MatchesPhase1Through4()
    {
        // طبق بخش ۱ سند («These claims must be independently verified»): تعداد واقعی باید
        // دقیقاً منطبق با آنچه seed می‌شود باشد - نه یک عدد تخمینی در گزارش
        var ctx = CreateContext(nameof(SeedAsync_TotalArticleCount_MatchesPhase1Through4));
        await SeedAsync(ctx);

        var totalArticles = await ctx.HelpArticles.CountAsync();

        Assert.Equal(60, totalArticles); // 20 (Phase 1) + 17 (Phase 2) + 7 (Increment 1) + 9 (Increment 2) + 1 (Increment 3B: reliability-overview) + 1 (Site-wide UI: ui-conventions) + 1 (Workflow Platform Phase 2: work-request-workflow) + 1 (Workflow Platform Phase 3B: work-order-lifecycle) + 1 (Workflow Platform Phase 4: workflow-management) + 1 (Material Management Step 2/3: irn-receiving-and-disposition) + 1 (Package Procurement Contract Finalization: tender-and-contract-finalization)
    }

    [Fact]
    public async Task SeedAsync_ReliabilityOverview_HasCorrectHelpKeyAndIsBilingual()
    {
        var ctx = CreateContext(nameof(SeedAsync_ReliabilityOverview_HasCorrectHelpKeyAndIsBilingual));
        await SeedAsync(ctx);

        var article = await ctx.HelpArticles.Include(a => a.Translations).FirstOrDefaultAsync(a => a.Code == "reliability-overview");

        Assert.NotNull(article);
        Assert.Equal("Reliability.Overview", article!.HelpKey);
        Assert.True(article.IsPublished);
        Assert.Contains(article.Translations, t => t.LanguageCode == "en" && t.Content.Contains("MTBF"));
        Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && t.Content.Contains("MTBF"));
    }

    // ==============================================================================
    // طبق «PEMS Help Phase 4» بخش ۴/۲۰: HelpKey های تازه‌سیم‌شده در کنترلرهای واقعی
    // (MOCController, QCController, TechnicalQueryController, LetterController,
    // ContractController, VendorController, ProductionController) باید به یک مقاله‌ی
    // منتشرشده‌ی دوزبانه برسند - قبل از این فاز این ۷ ماژول اصلاً HelpKey نداشتند.
    // ==============================================================================
    [Fact]
    public async Task SeedAsync_Phase4HelpKeys_ExistAndArePublished_AndAreBilingual()
    {
        var ctx = CreateContext(nameof(SeedAsync_Phase4HelpKeys_ExistAndArePublished_AndAreBilingual));
        await SeedAsync(ctx);

        var expectedKeys = new[]
        {
            "MOC.Index", "QC.Index", "TechnicalQuery.Index", "Letter.Index",
            "Contract.Index", "Vendor.Index", "Production.Dashboard"
        };

        foreach (var key in expectedKeys)
        {
            var article = await ctx.HelpArticles.Include(a => a.Translations).FirstOrDefaultAsync(a => a.HelpKey == key);
            Assert.True(article != null, $"Expected a Help article with HelpKey '{key}'");
            Assert.True(article!.IsPublished, $"Article for HelpKey '{key}' should be published");
            Assert.Contains(article.Translations, t => t.LanguageCode == "en" && !string.IsNullOrWhiteSpace(t.Content));
            Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && !string.IsNullOrWhiteSpace(t.Content));
        }
    }

    [Fact]
    public async Task SeedAsync_OperationsProductionOverview_BackfilledWithProductionDashboardHelpKey()
    {
        // این مقاله در فاز ۲ بدون HelpKey seed شده بود - فاز ۴ آن را بدون تغییر محتوا Backfill می‌کند
        var ctx = CreateContext(nameof(SeedAsync_OperationsProductionOverview_BackfilledWithProductionDashboardHelpKey));
        await SeedAsync(ctx);

        var article = await ctx.HelpArticles.FirstOrDefaultAsync(a => a.Code == "operations-production-overview");

        Assert.NotNull(article);
        Assert.Equal("Production.Dashboard", article!.HelpKey);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_BackfillIsIdempotent()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_BackfillIsIdempotent));

        await SeedAsync(ctx);
        await SeedAsync(ctx);

        var article = await ctx.HelpArticles.SingleAsync(a => a.Code == "operations-production-overview");
        Assert.Equal("Production.Dashboard", article.HelpKey);
    }

    // ==============================================================================
    // طبق «PEMS Help Phase 4 — Increment 2»: ۹ HelpKey تازه‌سیم‌شده در کنترلرهای واقعیِ
    // زیرمجموعه‌های تا‌کنون‌بدون‌راهنما (CriticalityAssessment, Instrument, RcmFmea, SparePart,
    // JobPlan, Permit.Details, StoreRequisition, DocumentRevisions) به‌علاوه‌ی یک HelpKey
    // مشترک («Imports.StagedBatchOverview») که شش کنترلر Import جداگانه به یک مقاله‌ی واحد
    // وصل می‌کند - همان الگویی که HelpService.GetArticleByContextAsync ذاتاً پشتیبانی می‌کند
    // (چند مقاله می‌توانند یک HelpKey را به اشتراک بگذارند؛ این‌جا برعکسش - چند صفحه یک
    // HelpKey - که همان مکانیزم زیرساختی را به همان سادگی استفاده می‌کند).
    // ==============================================================================
    [Fact]
    public async Task SeedAsync_Phase4Increment2HelpKeys_ExistAndArePublished_AndAreBilingual()
    {
        var ctx = CreateContext(nameof(SeedAsync_Phase4Increment2HelpKeys_ExistAndArePublished_AndAreBilingual));
        await SeedAsync(ctx);

        var expectedKeys = new[]
        {
            "CriticalityAssessment.Dashboard", "Instrument.Passport", "RcmFmea.Index", "SparePart.Index",
            "JobPlan.Index", "Permit.Details", "StoreRequisition.Index", "DocumentRevisions.Index",
            "Imports.StagedBatchOverview"
        };

        foreach (var key in expectedKeys)
        {
            var article = await ctx.HelpArticles.Include(a => a.Translations).FirstOrDefaultAsync(a => a.HelpKey == key);
            Assert.True(article != null, $"Expected a Help article with HelpKey '{key}'");
            Assert.True(article!.IsPublished, $"Article for HelpKey '{key}' should be published");
            Assert.Contains(article.Translations, t => t.LanguageCode == "en" && !string.IsNullOrWhiteSpace(t.Content));
            Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && !string.IsNullOrWhiteSpace(t.Content));
        }
    }

    [Fact]
    public async Task SeedAsync_ImportsSharedHelpKey_ResolvesToExactlyOneArticle()
    {
        // شش کنترلر Import همه HelpKey="Imports.StagedBatchOverview" را ست می‌کنند - این تست
        // تایید می‌کند که دقیقاً یک مقاله (نه صفر، نه چند مقاله‌ی متناقض) با این کلید seed شده -
        // چون HelpService.GetArticleByContextAsync با FirstOrDefaultAsync کار می‌کند، وجود دو
        // مقاله‌ی متفاوت با همین کلید می‌توانست باعث شود بعضی صفحات محتوای غلط ببینند
        var ctx = CreateContext(nameof(SeedAsync_ImportsSharedHelpKey_ResolvesToExactlyOneArticle));
        await SeedAsync(ctx);

        var matches = await ctx.HelpArticles.Where(a => a.HelpKey == "Imports.StagedBatchOverview").ToListAsync();

        Assert.Single(matches);
        Assert.Equal("import-batches-overview", matches[0].Code);
    }
}
