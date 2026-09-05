using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — P2: TableExtraction Grid». قبلاً Take(50) ثابت بود (بدون Skip/
/// CountAsync/pageSize) - Attachmentهای قدیمی‌تر از ۵۰تای اخیر اصلاً قابل‌دیدن نبودند. Index
/// فقط به PEMSDbContext وابسته است - بقیه‌ی وابستگی‌های Controller (AI/Import/CurrentUser) در
/// این Action استفاده نمی‌شوند.
/// </summary>
public class TableExtractionGridPaginationTests
{
    private static TableExtractionController CreateController(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"TableExtractionGrid_{dbName}");
        return new TableExtractionController(ctx, null!, null!, null!);
    }

    private static int _nextId = 1;

    private static DocumentAttachment NewAttachment(string fileName, DateTime uploadDate) => new()
    {
        AttachmentId = _nextId++,
        FileName = fileName,
        FileExtension = ".pdf",
        FilePath = $"/uploads/{fileName}",
        AIStatus = "Pending",
        UploadDate = uploadDate,
        IsActive = true
    };

    private static async Task<PagedResult<TableExtractionAttachmentListDto>> RunIndex(
        TableExtractionController controller, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<TableExtractionAttachmentListDto>>(
            ((ViewResult)await controller.Index(page, pageSize)).Model);

    [Fact]
    public async Task Index_MoreThan50Attachments_OlderOnesAreReachableViaPage2()
    {
        // دقیقاً همون باگ اصلی: قبلاً هیچ راهی برای دیدن بیش از ۵۰ تای اخیر نبود
        var controller = CreateController(nameof(Index_MoreThan50Attachments_OlderOnesAreReachableViaPage2), out var ctx);
        for (var i = 0; i < 75; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 50);
        var page2 = await RunIndex(controller, page: 2, pageSize: 50);

        Assert.Equal(75, page1.TotalCount);
        Assert.Equal(50, page1.Items.Count);
        Assert.Equal(25, page2.Items.Count); // این ۲۵ آیتم قدیمی قبلاً هرگز قابل‌دسترس نبودند
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

        var result = await RunIndex(controller, page: 1, pageSize: 30);

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
    public async Task Index_InactiveAttachments_AreExcluded()
    {
        var controller = CreateController(nameof(Index_InactiveAttachments_AreExcluded), out var ctx);
        var active = NewAttachment("active.pdf", DateTime.Today);
        var inactive = NewAttachment("inactive.pdf", DateTime.Today);
        inactive.IsActive = false;
        ctx.DocumentAttachments.Add(active);
        ctx.DocumentAttachments.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result.Items);
        Assert.Equal("active.pdf", result.Items[0].FileName);
    }
}
