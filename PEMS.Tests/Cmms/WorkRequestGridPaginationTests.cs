using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: WorkRequest Grid». قبلاً کل جدول فیلترشده (با ۲ Include
/// غیرضروری - چون Select پایین‌تر فقط فیلد اسکالر لازم داشت) با ToListAsync (بدون Skip/Take)
/// بارگذاری می‌شد.
/// </summary>
public class WorkRequestGridPaginationTests
{
    private static WorkRequestService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"WorkRequestGrid_{dbName}");
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Maintenance" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Test", LastName = "User", DepartmentId = 1, IsActive = true });
        ctx.SaveChanges();
        return new WorkRequestService(ctx, null!, null!);
    }

    private static int _nextId = 1;

    private static WorkRequest NewWorkRequest(DateTime requestedDate, string status = "Draft") => new()
    {
        WorkRequestId = _nextId++,
        RequestNumber = $"WR-{_nextId:D4}",
        Title = "Test Request",
        RequestedById = 1,
        RequestedDate = requestedDate,
        Priority = "Medium",
        Status = status,
        IsActive = true
    };

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetListAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetListAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 13);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.WorkRequestId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameRequestedDate_SameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameRequestedDate_SameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.WorkRequests.Add(NewWorkRequest(sameDate));
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.WorkRequestId), second.Items.Select(x => x.WorkRequestId));
    }

    [Fact]
    public async Task GetListAsync_StatusFilter_StillWorksAfterPageSizeChange()
    {
        var service = CreateService(nameof(GetListAsync_StatusFilter_StillWorksAfterPageSizeChange), out var ctx);
        ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today, status: "Draft"));
        ctx.WorkRequests.Add(NewWorkRequest(DateTime.Today, status: "Approved"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(status: "Draft");

        Assert.Single(result.Items);
        Assert.Equal("Draft", result.Items[0].Status);
    }

    [Fact]
    public async Task GetListAsync_InactiveRequests_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactiveRequests_AreExcluded), out var ctx);
        var active = NewWorkRequest(DateTime.Today);
        var inactive = NewWorkRequest(DateTime.Today);
        inactive.IsActive = false;
        ctx.WorkRequests.Add(active);
        ctx.WorkRequests.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.Single(result.Items);
        Assert.Equal(active.WorkRequestId, result.Items[0].WorkRequestId);
    }

    [Fact]
    public async Task GetListAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetListAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetListAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}
