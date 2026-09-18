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
    public async Task SeedAsync_TotalArticleCount_MatchesPhase1PlusPhase2()
    {
        // طبق بخش ۱ سند («These claims must be independently verified»): تعداد واقعی باید
        // دقیقاً منطبق با آنچه seed می‌شود باشد - نه یک عدد تخمینی در گزارش
        var ctx = CreateContext(nameof(SeedAsync_TotalArticleCount_MatchesPhase1PlusPhase2));
        await SeedAsync(ctx);

        var totalArticles = await ctx.HelpArticles.CountAsync();

        Assert.Equal(37, totalArticles); // 20 (Phase 1) + 17 (Phase 2)
    }
}
