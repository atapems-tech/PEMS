using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Help Phase 4 — Increment 3A» بخش ۱۵: تست‌های HelpKnowledgeSeeder - ایدمپوتنت‌بودن
/// (هم مقالات هم Standard References)، کامل‌بودن دوزبانه، یکتاییِ Identifier استانداردها، و
/// این‌که هیچ استانداردی بدون شاهدِ واقعی ادعای «ComplianceEvidenceAvailable» ندارد.
/// </summary>
public class HelpKnowledgeSeederTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"HelpKnowledgeSeeder_{dbName}");

    private static async Task SeedAllAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        // طبق بخش ۷ سند (Reverse Cross-Link Closure): پیوندهای معکوس به مقالات پایه (مثلاً
        // maintenance-strategy-overview) نیاز دارند آن مقالات از قبل وجود داشته باشند - پس
        // HelpContentSeeder هم باید قبل از HelpKnowledgeSeeder اجرا شود، دقیقاً همان الگوی
        // HelpVisualSeederTests.
        await HelpContentSeeder.SeedAsync(provider);
        await HelpKnowledgeSeeder.SeedAsync(provider);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotCreateDuplicateArticlesOrStandards()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_DoesNotCreateDuplicateArticlesOrStandards));

        await SeedAllAsync(ctx);
        var articlesAfterFirst = await ctx.HelpArticles.CountAsync();
        var standardsAfterFirst = await ctx.HelpStandardReferences.CountAsync();

        await SeedAllAsync(ctx);
        var articlesAfterSecond = await ctx.HelpArticles.CountAsync();
        var standardsAfterSecond = await ctx.HelpStandardReferences.CountAsync();

        Assert.Equal(articlesAfterFirst, articlesAfterSecond);
        Assert.Equal(standardsAfterFirst, standardsAfterSecond);
        Assert.True(articlesAfterFirst > 0);
        Assert.True(standardsAfterFirst > 0);
    }

    [Fact]
    public async Task SeedAsync_ExactCounts_Concepts18Total_Disciplines4_Standards8()
    {
        // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۴ (Count Reconciliation)، به‌روزشده در
        // Increment 3B: از ۱۸ مقاله‌ی ArticleType=Concept، فقط ۱۷ تا واقعاً توسط
        // HelpKnowledgeSeeder ساخته می‌شوند (۱۵ از Increment 3A + concept-mtbf-mttr-availability
        // و concept-bad-actor از Increment 3B) - یکی («import-batches-overview») در Increment 2
        // توسط HelpContentSeeder با همین ArticleType از قبل ساخته شده بود.
        var ctx = CreateContext(nameof(SeedAsync_ExactCounts_Concepts18Total_Disciplines4_Standards8));
        await SeedAllAsync(ctx);

        var conceptCount = await ctx.HelpArticles.CountAsync(a => a.ArticleType == "Concept");
        var disciplineCount = await ctx.HelpArticles.CountAsync(a => a.ArticleType == "DisciplineGuide");
        var standardCount = await ctx.HelpArticles.CountAsync(a => a.ArticleType == "StandardReference");
        var newConceptsFromThisSeeder = await ctx.HelpArticles.CountAsync(a => a.ArticleType == "Concept" && a.Code != "import-batches-overview");

        Assert.Equal(18, conceptCount);
        Assert.Equal(17, newConceptsFromThisSeeder);
        Assert.Equal(4, disciplineCount);
        Assert.Equal(8, standardCount);
        Assert.Equal(8, await ctx.HelpStandardReferences.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_NewCategories_ConceptsStandardsDisciplines_AreCreated()
    {
        var ctx = CreateContext(nameof(SeedAsync_NewCategories_ConceptsStandardsDisciplines_AreCreated));
        await SeedAllAsync(ctx);

        foreach (var code in new[] { "concepts", "standards", "disciplines" })
        {
            var exists = await ctx.HelpCategories.AnyAsync(c => c.Code == code);
            Assert.True(exists, $"Expected HelpCategory '{code}' to exist");
        }
    }

    [Fact]
    public async Task SeedAsync_EveryKnowledgeArticle_HasBothEnglishAndPersianTranslations()
    {
        var ctx = CreateContext(nameof(SeedAsync_EveryKnowledgeArticle_HasBothEnglishAndPersianTranslations));
        await SeedAllAsync(ctx);

        var articles = await ctx.HelpArticles
            .Include(a => a.Translations)
            .Where(a => a.ArticleType == "Concept" || a.ArticleType == "DisciplineGuide" || a.ArticleType == "StandardReference")
            .ToListAsync();

        Assert.NotEmpty(articles);
        foreach (var article in articles)
        {
            Assert.Contains(article.Translations, t => t.LanguageCode == "en" && !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Content));
            Assert.Contains(article.Translations, t => t.LanguageCode == "fa" && !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Content));
        }
    }

    [Fact]
    public async Task SeedAsync_Standards_NoDuplicateIdentifiers()
    {
        var ctx = CreateContext(nameof(SeedAsync_Standards_NoDuplicateIdentifiers));
        await SeedAllAsync(ctx);

        var identifiers = await ctx.HelpStandardReferences.Select(s => s.Identifier).ToListAsync();

        Assert.Equal(identifiers.Count, identifiers.Distinct().Count());
    }

    // ==============================================================================
    // طبق بخش ۲/۶/۱۱ سند: «هرگز نباید ادعا شود PEMS با یک استاندارد منطبق است مگر شاهد رسمی
    // وجود داشته باشد» - این تست تایید می‌کند که Seed هیچ استانداردی را با
    // PEMSImplementationStatus=ComplianceEvidenceAvailable بدون یک EvidencePath واقعی ثبت
    // نکرده (در این افزایش عمداً هیچ‌کدام این وضعیت را ندارند - همه ConceptuallyMapped/
    // PartiallyRelated/NotImplemented/ReferenceOnly هستند، چون هیچ شاهد رسمیِ انطباق واقعی در
    // مخزن پیدا نشد).
    // ==============================================================================
    [Fact]
    public async Task SeedAsync_NoStandard_ClaimsComplianceEvidenceAvailable_WithoutEvidencePath()
    {
        var ctx = CreateContext(nameof(SeedAsync_NoStandard_ClaimsComplianceEvidenceAvailable_WithoutEvidencePath));
        await SeedAllAsync(ctx);

        var standards = await ctx.HelpStandardReferences.ToListAsync();

        foreach (var s in standards)
        {
            if (s.PEMSImplementationStatus == "ComplianceEvidenceAvailable")
                Assert.False(string.IsNullOrWhiteSpace(s.EvidencePath), $"{s.Identifier} claims ComplianceEvidenceAvailable without an EvidencePath");
        }
    }

    [Fact]
    public async Task SeedAsync_NoStandard_UsesComplianceEvidenceAvailable_InThisIncrement()
    {
        // طبق ممیزی این افزایش: هیچ شاهد رسمیِ انطباقِ کاملِ واقعی در مخزن پیدا نشد - پس هیچ‌کدام
        // از ۸ استاندارد این فاز نباید ادعای بالاترین سطح («انطباق تاییدشده») را داشته باشد
        var ctx = CreateContext(nameof(SeedAsync_NoStandard_UsesComplianceEvidenceAvailable_InThisIncrement));
        await SeedAllAsync(ctx);

        var anyComplianceVerified = await ctx.HelpStandardReferences.AnyAsync(s => s.PEMSImplementationStatus == "ComplianceEvidenceAvailable");

        Assert.False(anyComplianceVerified);
    }

    [Fact]
    public async Task SeedAsync_KeyConcepts_ExistAndArePublished()
    {
        var ctx = CreateContext(nameof(SeedAsync_KeyConcepts_ExistAndArePublished));
        await SeedAllAsync(ctx);

        var expectedCodes = new[]
        {
            "concept-idc", "concept-mdr", "concept-vdr", "concept-fmea", "concept-rcm",
            "concept-criticality", "concept-permit-to-work", "concept-isolation-loto"
        };

        foreach (var code in expectedCodes)
        {
            var article = await ctx.HelpArticles.FirstOrDefaultAsync(a => a.Code == code);
            Assert.True(article != null, $"Expected concept article '{code}'");
            Assert.True(article!.IsPublished);
        }
    }

    [Fact]
    public async Task SeedAsync_StandardArticle_LinksToStandardReferenceRecord()
    {
        var ctx = CreateContext(nameof(SeedAsync_StandardArticle_LinksToStandardReferenceRecord));
        await SeedAllAsync(ctx);

        var iso14224Article = await ctx.HelpArticles.SingleAsync(a => a.Code == "standard-iso-14224");
        var standardRow = await ctx.HelpStandardReferences.SingleAsync(s => s.HelpArticleId == iso14224Article.HelpArticleId);

        Assert.Equal("ISO 14224", standardRow.Identifier);
        Assert.Equal("StandardReference", iso14224Article.ArticleType);
    }

    // ==============================================================================
    // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۶: هر ۸ استاندارد باید OfficialSourceVerified
    // باشند - چون OfficialReferenceUrl هر ۸ تا واقعاً از دامنه‌ی اصلیِ خودِ سازمان صادرکننده
    // (iso.org/sae.org/api.org/webstore.iec.ch) است، تاییدشده با WebSearch در همین افزایش. این
    // تست همان Backfill اصلاحیِ ۷ رکورد (که قبلاً PublisherMetadataVerified بودند) را هم پوشش
    // می‌دهد.
    // ==============================================================================
    [Fact]
    public async Task SeedAsync_AllEightStandards_AreOfficialSourceVerified()
    {
        var ctx = CreateContext(nameof(SeedAsync_AllEightStandards_AreOfficialSourceVerified));
        await SeedAllAsync(ctx);

        var standards = await ctx.HelpStandardReferences.ToListAsync();

        Assert.Equal(8, standards.Count);
        Assert.All(standards, s => Assert.Equal("OfficialSourceVerified", s.VerificationStatus));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_VerificationBackfillIsIdempotent()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_VerificationBackfillIsIdempotent));

        await SeedAllAsync(ctx);
        await SeedAllAsync(ctx);

        var standards = await ctx.HelpStandardReferences.ToListAsync();
        Assert.Equal(8, standards.Count);
        Assert.All(standards, s => Assert.Equal("OfficialSourceVerified", s.VerificationStatus));
    }

    // ==============================================================================
    // طبق بخش ۷ سند (Reverse Cross-Link Closure): پیوندهای معکوسِ مقالات پایه → مفاهیم/
    // استانداردهای جدید باید واقعاً وجود داشته باشند - نه فقط جهت رفت (مفهوم/استاندارد →
    // مقاله‌ی پایه) که در Increment 3A ساخته شده بود.
    // ==============================================================================
    [Theory]
    [InlineData("maintenance-strategy-overview", "concept-rcm")]
    [InlineData("maintenance-strategy-overview", "standard-iso-14224")]
    [InlineData("work-order-details", "standard-iso-14224")]
    [InlineData("functional-location-vs-equipment", "concept-functional-location")]
    [InlineData("permits-overview", "concept-permit-to-work")]
    [InlineData("qc-qa-overview", "standard-iso-9001")]
    [InlineData("ncr-critical-approval-workflow", "standard-iso-9001")]
    [InlineData("rcm-fmea-overview", "standard-sae-ja1011")]
    [InlineData("criticality-assessment-overview", "standard-iso-55001")]
    [InlineData("instrument-passport", "standard-iec-61508")]
    public async Task SeedAsync_ReverseLink_ExistsFromBaselineArticleToNewKnowledgeArticle(string baselineCode, string newCode)
    {
        var ctx = CreateContext(nameof(SeedAsync_ReverseLink_ExistsFromBaselineArticleToNewKnowledgeArticle) + baselineCode + newCode);
        await SeedAllAsync(ctx);

        var baselineId = await ctx.HelpArticles.Where(a => a.Code == baselineCode).Select(a => a.HelpArticleId).SingleAsync();
        var newId = await ctx.HelpArticles.Where(a => a.Code == newCode).Select(a => a.HelpArticleId).SingleAsync();

        var linkExists = await ctx.HelpRelatedArticles.AnyAsync(r => r.HelpArticleId == baselineId && r.RelatedHelpArticleId == newId);

        Assert.True(linkExists, $"Expected reverse link {baselineCode} -> {newCode}");
    }

    [Fact]
    public async Task SeedAsync_RunTwice_ReverseLinks_DoNotDuplicate()
    {
        var ctx = CreateContext(nameof(SeedAsync_RunTwice_ReverseLinks_DoNotDuplicate));

        await SeedAllAsync(ctx);
        var countAfterFirst = await ctx.HelpRelatedArticles.CountAsync();

        await SeedAllAsync(ctx);
        var countAfterSecond = await ctx.HelpRelatedArticles.CountAsync();

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.True(countAfterFirst > 0);
    }

    [Fact]
    public async Task SeedAsync_NoRelatedArticleLink_IsASelfLink()
    {
        var ctx = CreateContext(nameof(SeedAsync_NoRelatedArticleLink_IsASelfLink));
        await SeedAllAsync(ctx);

        var links = await ctx.HelpRelatedArticles.ToListAsync();

        Assert.DoesNotContain(links, l => l.HelpArticleId == l.RelatedHelpArticleId);
    }

    [Fact]
    public async Task SeedAsync_NoOrphanRelatedArticleLinks()
    {
        var ctx = CreateContext(nameof(SeedAsync_NoOrphanRelatedArticleLinks));
        await SeedAllAsync(ctx);

        var articleIds = (await ctx.HelpArticles.Select(a => a.HelpArticleId).ToListAsync()).ToHashSet();
        var links = await ctx.HelpRelatedArticles.ToListAsync();

        Assert.All(links, l =>
        {
            Assert.Contains(l.HelpArticleId, articleIds);
            Assert.Contains(l.RelatedHelpArticleId, articleIds);
        });
    }
}
