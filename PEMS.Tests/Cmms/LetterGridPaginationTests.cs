using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — P1: Letter Grid». قبلاً pageSize=25 در Controller مستقیم Hardcode
/// شده بود (نه از QueryString، نه با DataGridPageSize.Normalize). این تست‌ها لایه‌ی Service
/// (LetterService.SearchAsync) را - که خودش هم حالا Normalize می‌کند (Defense-in-Depth) -
/// و ترتیب تعیین‌پذیر (ThenByDescending(LetterId)) را تأیید می‌کنند.
/// </summary>
public class LetterGridPaginationTests
{
    private static LetterService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"LetterGrid_{dbName}");
        return new LetterService(ctx, sanitizer: null!, knowledgeBaseService: null!);
    }

    private static int _nextId = 1;

    private static Letter NewLetter(string subject, DateTime letterDate, string letterType = "Incoming") => new()
    {
        LetterId = _nextId++,
        LetterType = letterType,
        LetterNumber = $"LTR-{_nextId:D4}",
        LetterDate = letterDate,
        Subject = subject,
        ConfidentialityLevel = "Public",
        Status = "Registered",
        IsActive = true
    };

    [Fact]
    public async Task SearchAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(SearchAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.Letters.Add(NewLetter($"Subject {i}", DateTime.Today.AddDays(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 999, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task SearchAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(SearchAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.Letters.Add(NewLetter($"Subject {i}", DateTime.Today.AddDays(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: pageSize, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_InvalidPageSize_FallsBackTo50()
    {
        // دقیقاً مقدار ۲۵ که قبلاً Hardcode شده بود - حالا یک مقدار نامعتبر است و باید به ۵۰ برگردد
        var service = CreateService(nameof(SearchAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.Letters.Add(NewLetter("Subject", DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 25, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task SearchAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(SearchAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.Letters.Add(NewLetter($"Subject {i}", DateTime.Today.AddDays(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 20, canViewConfidential: true, requestingApplicationUserId: null);
        var page2 = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 2, pageSize: 20, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.LetterId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task SearchAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        // چند نامه با LetterDate یکسان - بدون ThenByDescending(LetterId)، ترتیب پایدار نیست
        var service = CreateService(nameof(SearchAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.Letters.Add(NewLetter($"Subject {i}", sameDate));
        await ctx.SaveChangesAsync();

        var first = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 20, canViewConfidential: true, requestingApplicationUserId: null);
        var second = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 20, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Equal(first.Items.Select(x => x.LetterId), second.Items.Select(x => x.LetterId));
    }

    [Fact]
    public async Task SearchAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(SearchAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.SearchAsync(null, null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 50, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_ExistingFilters_StillWorkAfterPageSizeChange()
    {
        // اطمینان از این‌که رفع مشکل pageSize رفتار فیلترهای موجود (letterType) را نشکسته
        var service = CreateService(nameof(SearchAsync_ExistingFilters_StillWorkAfterPageSizeChange), out var ctx);
        ctx.Letters.Add(NewLetter("Incoming letter", DateTime.Today, letterType: "Incoming"));
        ctx.Letters.Add(NewLetter("Outgoing letter", DateTime.Today, letterType: "Outgoing"));
        await ctx.SaveChangesAsync();

        var result = await service.SearchAsync("Incoming", null, null, null, null, null, null, null, null, null,
            page: 1, pageSize: 20, canViewConfidential: true, requestingApplicationUserId: null);

        Assert.Single(result.Items);
        Assert.Equal("Incoming letter", result.Items[0].Subject);
    }
}
