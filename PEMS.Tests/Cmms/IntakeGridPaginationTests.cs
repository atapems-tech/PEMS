using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — P1: Intake Grid». قبلاً pageSize=25 در Controller مستقیم Hardcode
/// شده بود؛ View از قبل _Pagination.cshtml استفاده می‌کرد اما بدون GridKey (بدون
/// Rows-per-page/Resize/Persistence). GetQueueAsync فقط به PEMSDbContext وابسته است - بقیه‌ی
/// وابستگی‌های IntakeService (AI/Routing/Vendor/...) در این متد استفاده نمی‌شوند.
/// </summary>
public class IntakeGridPaginationTests
{
    private static IntakeService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"IntakeGrid_{dbName}");
        return new IntakeService(ctx, null!, null!, null!, null!, null!, null!, NullLogger<IntakeService>.Instance);
    }

    private static int _nextId = 1;

    private static DocumentAttachment NewAttachment(string fileName, DateTime uploadDate, string stage = "Uploaded") => new()
    {
        AttachmentId = _nextId++,
        FileName = fileName,
        FileExtension = ".pdf",
        FilePath = $"/uploads/{fileName}",
        IntakeStage = stage,
        UploadDate = uploadDate,
        IsActive = true
    };

    [Fact]
    public async Task GetQueueAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetQueueAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetQueueAsync(page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetQueueAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetQueueAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetQueueAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetQueueAsync_InvalidPageSize_FallsBackTo50()
    {
        // دقیقاً مقدار ۲۵ که قبلاً Hardcode شده بود
        var service = CreateService(nameof(GetQueueAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.DocumentAttachments.Add(NewAttachment("file.pdf", DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.GetQueueAsync(page: 1, pageSize: 25);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetQueueAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetQueueAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetQueueAsync(page: 1, pageSize: 20);
        var page2 = await service.GetQueueAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.AttachmentId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetQueueAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetQueueAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.DocumentAttachments.Add(NewAttachment($"file{i}.pdf", sameDate));
        await ctx.SaveChangesAsync();

        var first = await service.GetQueueAsync(page: 1, pageSize: 20);
        var second = await service.GetQueueAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.AttachmentId), second.Items.Select(x => x.AttachmentId));
    }

    [Fact]
    public async Task GetQueueAsync_StageFilter_StillWorksAfterPageSizeChange()
    {
        var service = CreateService(nameof(GetQueueAsync_StageFilter_StillWorksAfterPageSizeChange), out var ctx);
        ctx.DocumentAttachments.Add(NewAttachment("a.pdf", DateTime.Today, stage: "Uploaded"));
        ctx.DocumentAttachments.Add(NewAttachment("b.pdf", DateTime.Today, stage: "Routed"));
        await ctx.SaveChangesAsync();

        var result = await service.GetQueueAsync(stage: "Uploaded", page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("a.pdf", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetQueueAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetQueueAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetQueueAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}
