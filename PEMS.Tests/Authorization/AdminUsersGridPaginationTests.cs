using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6B — P1: Admin/Users Grid». قبلاً pageSize=25 با const محلی Hardcode شده
/// بود، بدون GridKey. UsersController.Index از `_userManager.Users` می‌خواند که در پیاده‌سازی
/// پیش‌فرض ASP.NET Identity EF Store دقیقاً همون DbSet&lt;ApplicationUser&gt; Context.Users
/// است - این تست‌ها مستقیم روی همون DbSet با همون شکل Query (OrderBy+ThenBy+Skip+Take) اجرا
/// می‌شوند تا بدون برپاکردن کل UserManager/UserStore، صحت صفحه‌بندی را اثبات کنند.
/// </summary>
public class AdminUsersGridPaginationTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"AdminUsersGrid_{dbName}");

    private static int _nextId = 1;

    private static ApplicationUser NewUser(string userName) => new()
    {
        Id = _nextId++,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        FullName = userName,
        IsActive = true
    };

    private static async Task<(int Total, List<int> Ids, int PageSize)> RunPagedQuery(
        PEMS.Persistence.Context.PEMSDbContext ctx, int page, int? pageSize)
    {
        var query = ctx.Users.AsQueryable();
        var total = await query.CountAsync();

        var normalized = DataGridPageSize.Normalize(pageSize);

        var ids = await query
            .OrderBy(u => u.UserName)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * normalized)
            .Take(normalized)
            .Select(u => u.Id)
            .ToListAsync();

        return (total, ids, normalized);
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var ctx = CreateContext(nameof(Index_DefaultPageSize_Is50));
        for (var i = 0; i < 60; i++)
            ctx.Users.Add(NewUser($"user{i:D3}"));
        await ctx.SaveChangesAsync();

        var (total, ids, pageSize) = await RunPagedQuery(ctx, page: 1, pageSize: 999);

        Assert.Equal(50, pageSize);
        Assert.Equal(50, ids.Count);
        Assert.Equal(60, total);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Index_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var ctx = CreateContext($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}");
        for (var i = 0; i < 120; i++)
            ctx.Users.Add(NewUser($"user{i:D3}"));
        await ctx.SaveChangesAsync();

        var (_, ids, normalized) = await RunPagedQuery(ctx, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, normalized);
        Assert.Equal(Math.Min(pageSize, 120), ids.Count);
    }

    [Fact]
    public async Task Index_InvalidPageSize_FallsBackTo50()
    {
        // دقیقاً مقدار ۲۵ که قبلاً با const Hardcode شده بود
        var ctx = CreateContext(nameof(Index_InvalidPageSize_FallsBackTo50));
        ctx.Users.Add(NewUser("user1"));
        await ctx.SaveChangesAsync();

        var (_, _, normalized) = await RunPagedQuery(ctx, page: 1, pageSize: 25);

        Assert.Equal(50, normalized);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var ctx = CreateContext(nameof(Index_Page1AndPage2_AreStableAndDisjoint));
        for (var i = 0; i < 25; i++)
            ctx.Users.Add(NewUser($"user{i:D3}"));
        await ctx.SaveChangesAsync();

        var (total1, page1Ids, _) = await RunPagedQuery(ctx, page: 1, pageSize: 20);
        var (_, page2Ids, _) = await RunPagedQuery(ctx, page: 2, pageSize: 20);

        Assert.Equal(25, total1);
        Assert.Equal(20, page1Ids.Count);
        Assert.Equal(5, page2Ids.Count);
        Assert.Equal(25, page1Ids.Concat(page2Ids).Distinct().Count());
    }

    [Fact]
    public async Task Index_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        // چند کاربر با UserName یکسان (مجاز در سطح InMemory) - بدون ThenBy(Id) پایدار نیست
        var ctx = CreateContext(nameof(Index_DeterministicOrdering_SameQueryReturnsSameOrderTwice));
        for (var i = 0; i < 10; i++)
            ctx.Users.Add(NewUser("sameuser"));
        await ctx.SaveChangesAsync();

        var (_, first, _) = await RunPagedQuery(ctx, page: 1, pageSize: 20);
        var (_, second, _) = await RunPagedQuery(ctx, page: 1, pageSize: 20);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Index_EmptyDataset_ReturnsEmptyResult()
    {
        var ctx = CreateContext(nameof(Index_EmptyDataset_ReturnsEmptyResult));

        var (total, ids, _) = await RunPagedQuery(ctx, page: 1, pageSize: 50);

        Assert.Equal(0, total);
        Assert.Empty(ids);
    }
}
