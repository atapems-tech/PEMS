using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۱۷: تست‌های متمرکز روی HelpService -
/// دوزبانه‌بودن، Fallback زبان، Context Resolution، جستجوی دوزبانه، انتشار/عدم‌انتشار،
/// جلوگیری از کد تکراری، و مقالات مرتبط.
/// </summary>
public class HelpServiceTests
{
    private static (PEMS.Persistence.Context.PEMSDbContext ctx, HelpService svc) CreateService(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"HelpService_{dbName}");
        var svc = new HelpService(ctx, new RichTextSanitizerService());
        return (ctx, svc);
    }

    private static HelpCategory SeedCategory(PEMS.Persistence.Context.PEMSDbContext ctx, string code = "engineering")
    {
        var cat = new HelpCategory { Code = code, NameEn = "Engineering", NameFa = "مهندسی", DisplayOrder = 0, IsActive = true };
        ctx.HelpCategories.Add(cat);
        ctx.SaveChanges();
        return cat;
    }

    private static HelpArticleAdminEditDto BuildDto(int categoryId, string code, string helpKey = null!) => new()
    {
        Code = code,
        HelpCategoryId = categoryId,
        ArticleType = "Page",
        HelpKey = helpKey,
        DisplayOrder = 0,
        IsPublished = true,
        TitleEn = "Title EN " + code,
        SummaryEn = "Summary EN " + code,
        ContentEn = "<p>Content EN " + code + "</p>",
        SearchKeywordsEn = "keyword-en",
        TitleFa = "عنوان فارسی " + code,
        SummaryFa = "خلاصه فارسی " + code,
        ContentFa = "<p>محتوای فارسی " + code + "</p>",
        SearchKeywordsFa = "کلیدواژه-فارسی"
    };

    [Fact]
    public async Task CreateArticleAsync_MissingEnglishTranslation_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateArticleAsync_MissingEnglishTranslation_Throws));
        var cat = SeedCategory(ctx);
        var dto = BuildDto(cat.HelpCategoryId, "test-1");
        dto.TitleEn = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateArticleAsync(dto, null));
    }

    [Fact]
    public async Task CreateArticleAsync_MissingPersianTranslation_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateArticleAsync_MissingPersianTranslation_Throws));
        var cat = SeedCategory(ctx);
        var dto = BuildDto(cat.HelpCategoryId, "test-2");
        dto.ContentFa = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateArticleAsync(dto, null));
    }

    [Fact]
    public async Task CreateArticleAsync_DuplicateCode_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateArticleAsync_DuplicateCode_Throws));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "dup-code"), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "dup-code"), null));
    }

    [Fact]
    public async Task GetArticleByCodeAsync_ReturnsCorrectLanguage_ForBothLanguages()
    {
        var (ctx, svc) = CreateService(nameof(GetArticleByCodeAsync_ReturnsCorrectLanguage_ForBothLanguages));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "bilingual-1"), null);

        var en = await svc.GetArticleByCodeAsync("bilingual-1", "en");
        var fa = await svc.GetArticleByCodeAsync("bilingual-1", "fa");

        Assert.Equal("Title EN bilingual-1", en!.Title);
        Assert.Equal("en", en.LanguageCode);
        Assert.Equal("عنوان فارسی bilingual-1", fa!.Title);
        Assert.Equal("fa", fa.LanguageCode);
    }

    [Fact]
    public async Task GetArticleByCodeAsync_Unpublished_NotReturnedByDefault()
    {
        var (ctx, svc) = CreateService(nameof(GetArticleByCodeAsync_Unpublished_NotReturnedByDefault));
        var cat = SeedCategory(ctx);
        var dto = BuildDto(cat.HelpCategoryId, "draft-1");
        dto.IsPublished = false;
        await svc.CreateArticleAsync(dto, null);

        var publicResult = await svc.GetArticleByCodeAsync("draft-1", "en");
        var adminResult = await svc.GetArticleByCodeAsync("draft-1", "en", includeUnpublished: true);

        Assert.Null(publicResult);
        Assert.NotNull(adminResult);
    }

    [Fact]
    public async Task GetArticleByContextAsync_ResolvesByHelpKey_PublishedOnly()
    {
        var (ctx, svc) = CreateService(nameof(GetArticleByContextAsync_ResolvesByHelpKey_PublishedOnly));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "ctx-1", "SomeModule.Details"), null);

        var found = await svc.GetArticleByContextAsync("SomeModule.Details", "en");
        var notFound = await svc.GetArticleByContextAsync("NoSuchKey", "en");

        Assert.NotNull(found);
        Assert.Equal("ctx-1", found!.Code);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task SearchAsync_MatchesInRequestedLanguage()
    {
        var (ctx, svc) = CreateService(nameof(SearchAsync_MatchesInRequestedLanguage));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "search-1"), null);

        var enResults = await svc.SearchAsync("keyword-en", "en");
        var faResults = await svc.SearchAsync("کلیدواژه-فارسی", "fa");

        Assert.Contains(enResults, r => r.Code == "search-1");
        Assert.Contains(faResults, r => r.Code == "search-1");
    }

    [Fact]
    public async Task SearchAsync_CrossLanguageMatch_StillFindsArticle()
    {
        // طبق بخش ۶.۴ سند: کاربر فارسی‌زبان که یک اصطلاح انگلیسی تایپ می‌کند باید همچنان
        // نتیجه بگیرد، حتی اگر آن اصطلاح فقط در ترجمه‌ی انگلیسی موجود باشد
        var (ctx, svc) = CreateService(nameof(SearchAsync_CrossLanguageMatch_StillFindsArticle));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "cross-lang-1"), null);

        var resultsWhileBrowsingFa = await svc.SearchAsync("keyword-en", "fa");

        Assert.Contains(resultsWhileBrowsingFa, r => r.Code == "cross-lang-1");
    }

    [Fact]
    public async Task SearchAsync_UnpublishedArticle_NeverReturned()
    {
        var (ctx, svc) = CreateService(nameof(SearchAsync_UnpublishedArticle_NeverReturned));
        var cat = SeedCategory(ctx);
        var dto = BuildDto(cat.HelpCategoryId, "unpub-search");
        dto.IsPublished = false;
        await svc.CreateArticleAsync(dto, null);

        var results = await svc.SearchAsync("keyword-en", "en");

        Assert.DoesNotContain(results, r => r.Code == "unpub-search");
    }

    [Fact]
    public async Task SetPublishedAsync_TogglesVisibility()
    {
        var (ctx, svc) = CreateService(nameof(SetPublishedAsync_TogglesVisibility));
        var cat = SeedCategory(ctx);
        var id = await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "toggle-1"), null);

        await svc.SetPublishedAsync(id, false);
        Assert.Null(await svc.GetArticleByCodeAsync("toggle-1", "en"));

        await svc.SetPublishedAsync(id, true);
        Assert.NotNull(await svc.GetArticleByCodeAsync("toggle-1", "en"));
    }

    [Fact]
    public async Task RelatedArticles_AreReturned_InDetailDto()
    {
        var (ctx, svc) = CreateService(nameof(RelatedArticles_AreReturned_InDetailDto));
        var cat = SeedCategory(ctx);
        var id1 = await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "rel-1"), null);
        var id2 = await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "rel-2"), null);

        var editDto = await svc.GetForEditAsync(id1);
        editDto!.RelatedArticleIds = new List<int> { id2 };
        await svc.UpdateArticleAsync(id1, editDto);

        var detail = await svc.GetArticleByCodeAsync("rel-1", "en");

        Assert.Single(detail!.RelatedArticles);
        Assert.Equal("rel-2", detail.RelatedArticles[0].Code);
    }

    [Fact]
    public async Task DeleteArticleAsync_RemovesArticleAndItsTranslations()
    {
        var (ctx, svc) = CreateService(nameof(DeleteArticleAsync_RemovesArticleAndItsTranslations));
        var cat = SeedCategory(ctx);
        var id = await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "del-1"), null);

        await svc.DeleteArticleAsync(id);

        Assert.Null(await svc.GetArticleByCodeAsync("del-1", "en", includeUnpublished: true));
        Assert.Empty(ctx.HelpArticleTranslations.Where(t => t.HelpArticleId == id));
    }

    [Fact]
    public async Task GetCategoriesAsync_CountsOnlyPublishedArticles()
    {
        var (ctx, svc) = CreateService(nameof(GetCategoriesAsync_CountsOnlyPublishedArticles));
        var cat = SeedCategory(ctx);
        await svc.CreateArticleAsync(BuildDto(cat.HelpCategoryId, "count-1"), null);
        var draft = BuildDto(cat.HelpCategoryId, "count-2");
        draft.IsPublished = false;
        await svc.CreateArticleAsync(draft, null);

        var categories = await svc.GetCategoriesAsync("en");

        Assert.Equal(1, categories.Single(c => c.HelpCategoryId == cat.HelpCategoryId).ArticleCount);
    }
}
