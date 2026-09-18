using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Help Phase 4 — Increment 3A» بخش ۱۵: تست‌های HelpStandardReferenceService -
/// اعتبارسنجی، یکتاییِ Identifier، رفتار «ComplianceEvidenceAvailable نیازمند EvidencePath است»،
/// و این‌که این سرویس واقعاً روی IHelpService موجود ساخته شده (نه منطق تکراری).
/// </summary>
public class HelpStandardReferenceServiceTests
{
    private static (PEMSDbContext ctx, HelpStandardReferenceService svc) CreateService(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"HelpStandardReferenceService_{dbName}");
        var helpService = new HelpService(ctx, new RichTextSanitizerService());
        var svc = new HelpStandardReferenceService(ctx, helpService);
        return (ctx, svc);
    }

    private static async Task<int> SeedCategoryAsync(PEMSDbContext ctx)
    {
        var cat = new HelpCategory { Code = "standards", NameEn = "Standards", NameFa = "استانداردها", DisplayOrder = 0, IsActive = true };
        ctx.HelpCategories.Add(cat);
        await ctx.SaveChangesAsync();
        return cat.HelpCategoryId;
    }

    private static HelpStandardReferenceAdminEditDto BuildDto(int categoryId, string identifier) => new()
    {
        Code = "standard-" + identifier.ToLower().Replace(" ", "-"),
        HelpCategoryId = categoryId,
        DisplayOrder = 0,
        IsPublished = true,
        TitleEn = "Title EN " + identifier,
        SummaryEn = "Summary EN",
        ContentEn = "<p>Content EN</p>",
        TitleFa = "عنوان فا " + identifier,
        SummaryFa = "خلاصه فا",
        ContentFa = "<p>محتوای فا</p>",
        Identifier = identifier,
        IssuingOrganization = "Test Org",
        ApplicabilityStatus = "Applicable",
        VerificationStatus = "PublisherMetadataVerified",
        PEMSImplementationStatus = "ConceptuallyMapped"
    };

    [Fact]
    public async Task CreateAsync_MissingIdentifier_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_MissingIdentifier_Throws));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 1");
        dto.Identifier = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto, null));
    }

    [Fact]
    public async Task CreateAsync_InvalidApplicabilityStatus_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_InvalidApplicabilityStatus_Throws));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 2");
        dto.ApplicabilityStatus = "TotallyMadeUp";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto, null));
    }

    [Fact]
    public async Task CreateAsync_ComplianceEvidenceAvailable_WithoutEvidencePath_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ComplianceEvidenceAvailable_WithoutEvidencePath_Throws));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 3");
        dto.PEMSImplementationStatus = "ComplianceEvidenceAvailable";
        dto.EvidencePath = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto, null));
        Assert.Contains("EvidencePath", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ComplianceEvidenceAvailable_WithEvidencePath_Succeeds()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ComplianceEvidenceAvailable_WithEvidencePath_Succeeds));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 4");
        dto.PEMSImplementationStatus = "ComplianceEvidenceAvailable";
        dto.EvidencePath = "PEMS.Domain/Entities/SomeFile.cs";

        var id = await svc.CreateAsync(dto, null);

        Assert.True(id > 0);
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdentifier_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_DuplicateIdentifier_Throws));
        var catId = await SeedCategoryAsync(ctx);
        await svc.CreateAsync(BuildDto(catId, "ISO 5"), null);

        var dupe = BuildDto(catId, "ISO 5");
        dupe.Code = "different-code";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dupe, null));
    }

    [Fact]
    public async Task CreateAsync_ThenGetForEdit_RoundTripsStructuredFields()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ThenGetForEdit_RoundTripsStructuredFields));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 6");
        dto.OfficialReferenceUrl = "https://example.org/iso6";
        dto.EditionOrYear = "2020";

        var articleId = await svc.CreateAsync(dto, null);
        var standardRow = ctx.HelpStandardReferences.Single(s => s.HelpArticleId == articleId);

        var edit = await svc.GetForEditAsync(standardRow.HelpStandardReferenceId);

        Assert.NotNull(edit);
        Assert.Equal("ISO 6", edit!.Identifier);
        Assert.Equal("https://example.org/iso6", edit.OfficialReferenceUrl);
        Assert.Equal("2020", edit.EditionOrYear);
        Assert.Equal("Title EN ISO 6", edit.TitleEn);
    }

    [Fact]
    public async Task UpdateAsync_ChangingToDuplicateIdentifier_Throws()
    {
        var (ctx, svc) = CreateService(nameof(UpdateAsync_ChangingToDuplicateIdentifier_Throws));
        var catId = await SeedCategoryAsync(ctx);
        await svc.CreateAsync(BuildDto(catId, "ISO 7"), null);
        var secondArticleId = await svc.CreateAsync(BuildDto(catId, "ISO 8"), null);
        var secondStandardId = ctx.HelpStandardReferences.Single(s => s.HelpArticleId == secondArticleId).HelpStandardReferenceId;

        var updateDto = await svc.GetForEditAsync(secondStandardId);
        updateDto!.Identifier = "ISO 7";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(secondStandardId, updateDto));
    }

    [Fact]
    public async Task SetPublishedAsync_TogglesUnderlyingArticlePublishedFlag()
    {
        var (ctx, svc) = CreateService(nameof(SetPublishedAsync_TogglesUnderlyingArticlePublishedFlag));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 9");
        dto.IsPublished = true;
        var articleId = await svc.CreateAsync(dto, null);
        var standardId = ctx.HelpStandardReferences.Single(s => s.HelpArticleId == articleId).HelpStandardReferenceId;

        await svc.SetPublishedAsync(standardId, false);

        var article = ctx.HelpArticles.Single(a => a.HelpArticleId == articleId);
        Assert.False(article.IsPublished);
    }

    // ==============================================================================
    // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۵ («Identifier normalization is
    // deterministic»): بدون این رفتار، "  ISO 11  " (با فاصله‌ی اضافه) از بررسیِ یکتاییِ
    // AnyAsync رد می‌شد (چون با مقدار trim-شده‌ی ذخیره‌شده برابر نبود) و فقط توسط Unique Index
    // خامِ دیتابیس با یک استثنای زشت متوقف می‌شد، نه پیام تاییدشده‌ی این سرویس.
    // ==============================================================================
    [Fact]
    public async Task CreateAsync_IdentifierWithSurroundingWhitespace_NormalizedBeforeUniquenessCheck()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_IdentifierWithSurroundingWhitespace_NormalizedBeforeUniquenessCheck));
        var catId = await SeedCategoryAsync(ctx);
        await svc.CreateAsync(BuildDto(catId, "ISO 11"), null);

        var dupeWithWhitespace = BuildDto(catId, "  ISO 11  ");
        dupeWithWhitespace.Code = "different-code-2";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dupeWithWhitespace, null));
        Assert.Contains("ISO 11", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_IdentifierWithInternalDoubleSpace_NormalizedToSingleSpace()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_IdentifierWithInternalDoubleSpace_NormalizedToSingleSpace));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO  12"); // دو فاصله‌ی داخلی

        var articleId = await svc.CreateAsync(dto, null);
        var stored = ctx.HelpStandardReferences.Single(s => s.HelpArticleId == articleId);

        Assert.Equal("ISO 12", stored.Identifier); // یک فاصله، نه دو
    }

    // ==============================================================================
    // طبق بخش ۱۰ سند (URL and Security Audit): تنها URL راهنمای PEMS که واقعاً در یک ویژگی
    // <a href> رندر می‌شود - بدون این بررسی، "javascript:..." هم Encode و به‌عنوان لینک
    // قابل‌کلیک رندر می‌شد.
    // ==============================================================================
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("not a url at all")]
    [InlineData("ftp://example.com/file")]
    public async Task CreateAsync_UnsafeOrInvalidOfficialReferenceUrl_Throws(string unsafeUrl)
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_UnsafeOrInvalidOfficialReferenceUrl_Throws) + unsafeUrl.GetHashCode());
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 13 " + unsafeUrl.GetHashCode());
        dto.OfficialReferenceUrl = unsafeUrl;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto, null));
    }

    [Theory]
    [InlineData("https://www.iso.org/standard/64076.html")]
    [InlineData("http://example.com")]
    public async Task CreateAsync_ValidHttpOrHttpsOfficialReferenceUrl_Succeeds(string safeUrl)
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ValidHttpOrHttpsOfficialReferenceUrl_Succeeds) + safeUrl.GetHashCode());
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 14 " + safeUrl.GetHashCode());
        dto.OfficialReferenceUrl = safeUrl;

        var id = await svc.CreateAsync(dto, null);

        Assert.True(id > 0);
    }

    [Fact]
    public async Task CreateAsync_NoOfficialReferenceUrl_DoesNotThrow()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_NoOfficialReferenceUrl_DoesNotThrow));
        var catId = await SeedCategoryAsync(ctx);
        var dto = BuildDto(catId, "ISO 15");
        dto.OfficialReferenceUrl = null;

        var id = await svc.CreateAsync(dto, null);

        Assert.True(id > 0);
    }

    [Fact]
    public async Task DeleteAsync_RemovesArticleAndStandardReferenceRow()
    {
        var (ctx, svc) = CreateService(nameof(DeleteAsync_RemovesArticleAndStandardReferenceRow));
        var catId = await SeedCategoryAsync(ctx);
        var articleId = await svc.CreateAsync(BuildDto(catId, "ISO 10"), null);
        var standardId = ctx.HelpStandardReferences.Single(s => s.HelpArticleId == articleId).HelpStandardReferenceId;

        await svc.DeleteAsync(standardId);

        Assert.Null(await svc.GetForEditAsync(standardId));
        Assert.Empty(ctx.HelpArticles.Where(a => a.HelpArticleId == articleId));
    }

    [Fact]
    public async Task GetMatrixAsync_OrderedByIdentifier_ExcludesUnpublishedWhenRequested()
    {
        var (ctx, svc) = CreateService(nameof(GetMatrixAsync_OrderedByIdentifier_ExcludesUnpublishedWhenRequested));
        var catId = await SeedCategoryAsync(ctx);
        var draft = BuildDto(catId, "ZZZ Draft");
        draft.IsPublished = false;
        await svc.CreateAsync(draft, null);
        await svc.CreateAsync(BuildDto(catId, "AAA Published"), null);

        var fullMatrix = await svc.GetMatrixAsync(includeUnpublished: true);
        var publishedOnly = await svc.GetMatrixAsync(includeUnpublished: false);

        Assert.Equal(2, fullMatrix.Count);
        Assert.Equal("AAA Published", fullMatrix[0].Identifier); // یکتاییِ Order بر اساس Identifier
        Assert.Single(publishedOnly);
        Assert.Equal("AAA Published", publishedOnly[0].Identifier);
    }
}
