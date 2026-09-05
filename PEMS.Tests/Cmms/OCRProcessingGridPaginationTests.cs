using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — P2: OCRProcessing Grid». قبلاً Take(200) ثابت بود (بدون Skip/
/// pageSize واقعی) و Include(Document) غیرضروری بود (فقط DocumentNo استفاده می‌شد). شمارنده‌های
/// Dashboard (GetCount) از قبل SQL-side بودند (GroupBy واقعی) و عمداً دست‌نخورده ماندند چون
/// Scope شان (کل جدول) با Grid فیلترشده فرق دارد - این تست‌ها هم آن رفتار Global بودن را تأیید می‌کنند.
/// </summary>
public class OCRProcessingGridPaginationTests
{
    private static OCRProcessingController CreateController(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"OCRProcessingGrid_{dbName}");
        return new OCRProcessingController(ctx, null!, null!);
    }

    private static int _nextId = 1;

    private static DocumentAttachment NewAttachment(string fileName, DateTime uploadDate, string status = "Pending") => new()
    {
        AttachmentId = _nextId++,
        FileName = fileName,
        FileExtension = ".pdf",
        FilePath = $"/uploads/{fileName}",
        AIStatus = status,
        UploadDate = uploadDate,
        IsActive = true
    };

    private static async Task<PagedResult<OCRProcessingAttachmentListDto>> RunIndex(
        OCRProcessingController controller, string? status = null, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<OCRProcessingAttachmentListDto>>(
            ((ViewResult)await controller.Index(status, page, pageSize)).Model);

    [Fact]
    public async Task Index_MoreThan200Attachments_OlderOnesAreReachableViaPage2()
    {
        // دقیقاً همون باگ اصلی: قبلاً هیچ راهی برای دیدن بیش از ۲۰۰ تای اخیر نبود
        var controller = CreateController(nameof(Index_MoreThan200Attachments_OlderOnesAreReachableViaPage2), out var ctx);
        for (var i = 0; i < 220; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 100);
        var page3 = await RunIndex(controller, page: 3, pageSize: 100);

        Assert.Equal(220, page1.TotalCount);
        Assert.Equal(100, page1.Items.Count);
        Assert.Equal(20, page3.Items.Count); // این ۲۰ آیتم قدیمی قبلاً هرگز قابل‌دسترس نبودند
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var controller = CreateController(nameof(Index_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: null);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Index_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var controller = CreateController($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Index_InvalidPageSize_FallsBackTo50()
    {
        var controller = CreateController(nameof(Index_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.DocumentAttachments.Add(NewAttachment("file.pdf", DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: 200);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task Index_DeterministicOrdering_SameUploadDate_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_DeterministicOrdering_SameUploadDate_SameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", sameDate));
        await ctx.SaveChangesAsync();

        var first = await RunIndex(controller, page: 1, pageSize: 20);
        var second = await RunIndex(controller, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.AttachmentId), second.Items.Select(x => x.AttachmentId));
    }

    [Fact]
    public async Task Index_StatusFilter_StillWorksAfterPageSizeChange()
    {
        var controller = CreateController(nameof(Index_StatusFilter_StillWorksAfterPageSizeChange), out var ctx);
        ctx.DocumentAttachments.Add(NewAttachment("a.pdf", DateTime.Today, status: "Completed"));
        ctx.DocumentAttachments.Add(NewAttachment("b.pdf", DateTime.Today, status: "Failed"));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, status: "Completed", page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("a.pdf", result.Items[0].FileName);
    }

    [Fact]
    public async Task Index_DashboardCounts_AreGlobal_NotRestrictedByStatusFilter()
    {
        // شمارنده‌های بالای صفحه (Pending/Processing/Completed/Failed) باید همیشه کل جدول را
        // نشان دهند - حتی وقتی کاربر روی یک Status خاص فیلتر کرده. تغییر این رفتار ممنوع بود.
        var controller = CreateController(nameof(Index_DashboardCounts_AreGlobal_NotRestrictedByStatusFilter), out var ctx);
        ctx.DocumentAttachments.Add(NewAttachment("a.pdf", DateTime.Today, status: "Completed"));
        ctx.DocumentAttachments.Add(NewAttachment("b.pdf", DateTime.Today, status: "Completed"));
        ctx.DocumentAttachments.Add(NewAttachment("c.pdf", DateTime.Today, status: "Failed"));
        await ctx.SaveChangesAsync();

        var view = (ViewResult)await controller.Index(status: "Completed", page: 1, pageSize: 20);
        var counts = (Dictionary<string, int>)view.ViewData["Counts"]!;

        Assert.Equal(2, counts["Completed"]);
        Assert.Equal(1, counts["Failed"]); // با این‌که Grid فقط Completed را نشان می‌دهد، شمارنده‌ها کل جدول را می‌بینند
    }

    [Fact]
    public async Task Index_DocumentNo_PopulatedWithoutInclude()
    {
        var controller = CreateController(nameof(Index_DocumentNo_PopulatedWithoutInclude), out var ctx);
        var doc = new EngineeringDocument { DocumentId = 1, DocumentNo = "DOC-001", Title = "Test", DocumentType = "Datasheet", ProjectId = 1, Status = "Draft", IsActive = true };
        ctx.EngineeringDocuments.Add(doc);
        var attachment = NewAttachment("a.pdf", DateTime.Today);
        attachment.DocumentId = 1;
        ctx.DocumentAttachments.Add(attachment);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result.Items);
        Assert.Equal("DOC-001", result.Items[0].DocumentNo);
    }
}
