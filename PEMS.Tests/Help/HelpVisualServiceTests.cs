using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Help System — Phase 3» بخش ۱۵: تست‌های HelpVisualService - اعتبارسنجی
/// (Validate)، امنیتِ پارسر تعریف دستی (ParseManualDefinition - هرگز HTML/JSON دلخواه از
/// کلاینت پذیرفته نمی‌شود)، CRUD، انتشار/عدم‌انتشار، و Fallback امن (هرگز نمودار جعلی/شکسته
/// رندر نمی‌شود).
/// </summary>
public class HelpVisualServiceTests
{
    private static (PEMSDbContext ctx, HelpVisualService svc) CreateService(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"HelpVisualService_{dbName}");
        var svc = new HelpVisualService(ctx, new HelpWorkflowDiagramService(ctx));
        return (ctx, svc);
    }

    private static async Task<int> SeedArticleAsync(PEMSDbContext ctx)
    {
        var cat = new HelpCategory { Code = "cat", NameEn = "Cat", NameFa = "دسته", DisplayOrder = 0, IsActive = true };
        ctx.HelpCategories.Add(cat);
        await ctx.SaveChangesAsync();

        var article = new HelpArticle { Code = "art-1", HelpCategoryId = cat.HelpCategoryId, ArticleType = "Workflow", IsPublished = true };
        ctx.HelpArticles.Add(article);
        await ctx.SaveChangesAsync();
        return article.HelpArticleId;
    }

    private static HelpVisualAdminEditDto BuildManualDto(int articleId, string nodeDefLines) => new()
    {
        HelpArticleId = articleId,
        VisualType = "DecisionTree",
        DisplayOrder = 0,
        IsPublished = true,
        ManualDefinitionText = nodeDefLines,
        CaptionEn = "Caption EN",
        CaptionFa = "عنوان فا",
        AccessibilityDescriptionEn = "Access EN",
        AccessibilityDescriptionFa = "دسترس‌پذیری فا"
    };

    // ============ Validate ============

    [Fact]
    public async Task CreateAsync_InvalidVisualType_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_InvalidVisualType_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ");
        dto.VisualType = "NotARealType";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_MissingCaption_EitherLanguage_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_MissingCaption_EitherLanguage_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ");
        dto.CaptionFa = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_MissingAccessibilityDescription_EitherLanguage_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_MissingAccessibilityDescription_EitherLanguage_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ");
        dto.AccessibilityDescriptionEn = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WorkflowDiagram_WithoutSourceModuleCategory_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_WorkflowDiagram_WithoutSourceModuleCategory_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, null!);
        dto.VisualType = "WorkflowDiagram";

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_ManualType_WithoutDefinitionText_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ManualType_WithoutDefinitionText_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    // ============ Security: manual-definition parser ============

    [Fact]
    public async Task CreateAsync_EdgeReferencingUndeclaredNode_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_EdgeReferencingUndeclaredNode_Throws));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ\nEDGE|a|nonexistent|Go|برو|normal");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_NoValidNodeLines_Throws()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_NoValidNodeLines_Throws));
        var articleId = await SeedArticleAsync(ctx);
        // فقط یک خط ناقص (کمتر از فیلدهای لازم) - نباید به‌عنوان NODE معتبر شمرده شود
        var dto = BuildManualDto(articleId, "NODE|onlyid");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_LabelsContainingAngleBrackets_StoredAsLiteralText_NotInterpretedAsStructure()
    {
        // نکته‌ی امنیتی: برچسب حاوی «<script>» باید عیناً به‌عنوان متن ساده در JSON ذخیره شود -
        // نه به‌عنوان HTML اجراشدنی (Razor در زمان رندر Encode می‌کند) و نه به‌عنوان یک خط
        // NODE/EDGE اضافی (چون پارسر خط‌به‌خط است، نه یک زبان تودرتو).
        var (ctx, svc) = CreateService(nameof(CreateAsync_LabelsContainingAngleBrackets_StoredAsLiteralText_NotInterpretedAsStructure));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|<script>alert(1)</script>|برچسب فا");

        var id = await svc.CreateAsync(dto);

        // System.Text.Json به‌طور پیش‌فرض کاراکترهای حساس HTML را در متن JSON Escape می‌کند
        // (رفتار امن پیش‌فرض) - پس این‌جا فقط تایید می‌شود که رشته‌ی خام JSON هرگز تگ اجراشدنی
        // ندارد، و بعد از Deserialize دوباره به همان متن اصلی (Round-trip) برمی‌گردد.
        var stored = ctx.HelpVisuals.Single(v => v.HelpVisualId == id);
        Assert.DoesNotContain("<script>", stored.DefinitionJson);

        var rendered = await svc.GetVisualsForArticleAsync(articleId, "en", includeUnpublished: true);
        Assert.Equal("<script>alert(1)</script>", rendered.Single().Nodes.Single().Label);
        // این برچسب صرفاً یک رشته‌ی متنی روی DTO است - Encode واقعی مسئولیت لایه‌ی Razor (@Model.Label) است،
        // نه این سرویس؛ این تست فقط تایید می‌کند که سرویس چیزی را اجرا/بازتفسیر نمی‌کند.
    }

    [Fact]
    public async Task CreateAsync_PipeCharacterInsideLabel_DoesNotAllowFieldInjection()
    {
        // اگر یک ادمین بدخواه سعی کند با کاراکتر «|» اضافی فیلدهای بیشتری تزریق کند، Split
        // ساده باعث می‌شود فیلدهای اضافه صرفاً نادیده گرفته شوند یا به آخرین فیلد مجاز بچسبند -
        // هرگز باعث ساخت یک Node/Edge غیرمنتظره‌ی جدید نمی‌شود.
        var (ctx, svc) = CreateService(nameof(CreateAsync_PipeCharacterInsideLabel_DoesNotAllowFieldInjection));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|Label|برچسب|icon|EDGE|x|y|Injected|تزریق|reject");

        var id = await svc.CreateAsync(dto);
        var stored = ctx.HelpVisuals.Single(v => v.HelpVisualId == id);
        var def = System.Text.Json.JsonSerializer.Deserialize<HelpVisualDefinition>(stored.DefinitionJson!)!;

        Assert.Single(def.Nodes); // فقط یک NODE واقعی - نه یک EDGE جدا
        Assert.Empty(def.Edges);
    }

    // ============ CRUD / lifecycle ============

    [Fact]
    public async Task CreateAsync_ThenGetForEdit_RoundTripsManualDefinitionText()
    {
        var (ctx, svc) = CreateService(nameof(CreateAsync_ThenGetForEdit_RoundTripsManualDefinitionText));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ\nNODE|b|B|ب\nEDGE|a|b|Go|برو|normal");

        var id = await svc.CreateAsync(dto);
        var edit = await svc.GetForEditAsync(id);

        Assert.NotNull(edit);
        Assert.Contains("NODE|a|A|آ|", edit!.ManualDefinitionText);
        Assert.Contains("EDGE|a|b|Go|برو|normal", edit.ManualDefinitionText);
    }

    [Fact]
    public async Task SetPublishedAsync_TogglesVisibility_InPublicResolution()
    {
        var (ctx, svc) = CreateService(nameof(SetPublishedAsync_TogglesVisibility_InPublicResolution));
        var articleId = await SeedArticleAsync(ctx);
        var dto = BuildManualDto(articleId, "NODE|a|A|آ");
        dto.IsPublished = true;
        var id = await svc.CreateAsync(dto);

        Assert.Single(await svc.GetVisualsForArticleAsync(articleId, "en"));

        await svc.SetPublishedAsync(id, false);
        Assert.Empty(await svc.GetVisualsForArticleAsync(articleId, "en"));
        Assert.Single(await svc.GetVisualsForArticleAsync(articleId, "en", includeUnpublished: true));
    }

    [Fact]
    public async Task DeleteAsync_RemovesVisual()
    {
        var (ctx, svc) = CreateService(nameof(DeleteAsync_RemovesVisual));
        var articleId = await SeedArticleAsync(ctx);
        var id = await svc.CreateAsync(BuildManualDto(articleId, "NODE|a|A|آ"));

        await svc.DeleteAsync(id);

        Assert.Null(await svc.GetForEditAsync(id));
    }

    // ============ Rendering / accuracy fallback ============

    [Fact]
    public async Task GetVisualsForArticleAsync_WorkflowDiagram_MissingSourceWorkflow_SkippedNotFaked()
    {
        var (ctx, svc) = CreateService(nameof(GetVisualsForArticleAsync_WorkflowDiagram_MissingSourceWorkflow_SkippedNotFaked));
        var articleId = await SeedArticleAsync(ctx);

        // مستقیماً یک HelpVisual با ماژول/دسته‌ی واقعاً موجودنبود در دیتابیس ثبت می‌کنیم
        // (شبیه‌سازی این‌که گردش‌کار منبع بعداً حذف/غیرفعال شده) - نباید نمودار جعلی نمایش دهد.
        ctx.HelpVisuals.Add(new HelpVisual
        {
            HelpArticleId = articleId, VisualType = "WorkflowDiagram", IsPublished = true,
            SourceWorkflowModule = "DoesNotExist", SourceWorkflowCategory = "DoesNotExist",
            CaptionEn = "C", CaptionFa = "ج", AccessibilityDescriptionEn = "A", AccessibilityDescriptionFa = "د"
        });
        await ctx.SaveChangesAsync();

        var visuals = await svc.GetVisualsForArticleAsync(articleId, "en");

        Assert.Empty(visuals);
    }

    [Fact]
    public async Task GetVisualsForArticleAsync_WorkflowDiagram_RealSource_ResolvesLiveFromWorkflowTables()
    {
        var (ctx, svc) = CreateService(nameof(GetVisualsForArticleAsync_WorkflowDiagram_RealSource_ResolvesLiveFromWorkflowTables));
        var articleId = await SeedArticleAsync(ctx);

        var def = new WorkflowDefinition { Name = "X", Module = "RealModule", Category = "RealCategory", IsActive = true };
        ctx.WorkflowDefinitions.Add(def);
        await ctx.SaveChangesAsync();
        var version = new WorkflowVersion { WorkflowDefinitionId = def.WorkflowDefinitionId, VersionNumber = 1, Status = "Published" };
        ctx.WorkflowVersions.Add(version);
        await ctx.SaveChangesAsync();
        ctx.WorkflowStages.Add(new WorkflowStage { WorkflowVersionId = version.WorkflowVersionId, Name = "OnlyStage", StageOrder = 0 });
        await ctx.SaveChangesAsync();

        ctx.HelpVisuals.Add(new HelpVisual
        {
            HelpArticleId = articleId, VisualType = "WorkflowDiagram", IsPublished = true,
            SourceWorkflowModule = "RealModule", SourceWorkflowCategory = "RealCategory",
            CaptionEn = "C", CaptionFa = "ج", AccessibilityDescriptionEn = "A", AccessibilityDescriptionFa = "د"
        });
        await ctx.SaveChangesAsync();

        var visuals = await svc.GetVisualsForArticleAsync(articleId, "en");

        var visual = Assert.Single(visuals);
        Assert.Single(visual.Nodes);
        Assert.Equal("OnlyStage", visual.Nodes[0].Id);
        Assert.Equal(1, visual.SourceWorkflowVersion);
    }

    [Fact]
    public async Task GetVisualsForArticleAsync_OrdersByDisplayOrder()
    {
        var (ctx, svc) = CreateService(nameof(GetVisualsForArticleAsync_OrdersByDisplayOrder));
        var articleId = await SeedArticleAsync(ctx);

        var dtoSecond = BuildManualDto(articleId, "NODE|a|Second|دوم");
        dtoSecond.DisplayOrder = 1;
        dtoSecond.CaptionEn = "Second";
        var dtoFirst = BuildManualDto(articleId, "NODE|a|First|اول");
        dtoFirst.DisplayOrder = 0;
        dtoFirst.CaptionEn = "First";

        await svc.CreateAsync(dtoSecond);
        await svc.CreateAsync(dtoFirst);

        var visuals = await svc.GetVisualsForArticleAsync(articleId, "en");

        Assert.Equal("First", visuals[0].Caption);
        Assert.Equal("Second", visuals[1].Caption);
    }
}
