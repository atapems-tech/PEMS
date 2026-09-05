using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: Task Grid». قبلاً کل جدول فیلترشده (بر اساس کارمند/کاربر
/// جاری) با ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
/// </summary>
public class TaskGridPaginationTests
{
    private static TaskService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"TaskGrid_{dbName}");
        return new TaskService(ctx, null!);
    }

    private static int _nextId = 1;

    private static TaskItem NewTaskItem(int assignedToEmployeeId, DateTime? dueDate, string status = "Open") => new()
    {
        TaskItemId = _nextId++,
        Title = $"Task {_nextId}",
        AssignedToEmployeeId = assignedToEmployeeId,
        DueDate = dueDate,
        Priority = "Normal",
        Status = status,
        CreatedDate = DateTime.Now
    };

    [Fact]
    public async Task GetMyActionItemsAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_DefaultPageSize_Is50), out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        for (var i = 0; i < 60; i++)
            ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today.AddDays(i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetMyActionItemsAsync(1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetMyActionItemsAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetMyActionItemsAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        for (var i = 0; i < 120; i++)
            ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today.AddDays(i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetMyActionItemsAsync(1, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetMyActionItemsAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        for (var i = 0; i < 25; i++)
            ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today.AddDays(i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetMyActionItemsAsync(1, page: 1, pageSize: 20);
        var page2 = await service.GetMyActionItemsAsync(1, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.TaskItemId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetMyActionItemsAsync_DeterministicOrdering_SameDueDate_SameOrderTwice()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_DeterministicOrdering_SameDueDate_SameOrderTwice), out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.TaskItems.Add(NewTaskItem(5, sameDate));
        await ctx.SaveChangesAsync();

        var first = await service.GetMyActionItemsAsync(1, page: 1, pageSize: 20);
        var second = await service.GetMyActionItemsAsync(1, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.TaskItemId), second.Items.Select(x => x.TaskItemId));
    }

    [Fact]
    public async Task GetMyActionItemsAsync_OpenOnly_ExcludesCompletedAndCancelled()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_OpenOnly_ExcludesCompletedAndCancelled), out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today, status: "Open"));
        ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today, status: "Completed"));
        ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today, status: "Cancelled"));
        await ctx.SaveChangesAsync();

        var result = await service.GetMyActionItemsAsync(1, openOnly: true);

        Assert.Single(result.Items);
        Assert.Equal("Open", result.Items[0].Status);
    }

    [Fact]
    public async Task GetMyActionItemsAsync_OnlyAssignedToCurrentEmployee()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_OnlyAssignedToCurrentEmployee), out var ctx);
        ctx.Users.Add(new PEMS.Persistence.Identity.ApplicationUser { Id = 1, UserName = "u1", EmployeeId = 5 });
        ctx.TaskItems.Add(NewTaskItem(5, DateTime.Today));
        ctx.TaskItems.Add(NewTaskItem(6, DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.GetMyActionItemsAsync(1);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetMyActionItemsAsync_UnknownUser_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetMyActionItemsAsync_UnknownUser_ReturnsEmptyPagedResult), out _);

        var result = await service.GetMyActionItemsAsync(999);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetAssignedByMeAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetAssignedByMeAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
        {
            var t = NewTaskItem(5, DateTime.Today.AddDays(i));
            t.AssignedByApplicationUserId = 1;
            ctx.TaskItems.Add(t);
        }
        await ctx.SaveChangesAsync();

        var result = await service.GetAssignedByMeAsync(1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Fact]
    public async Task GetAssignedByMeAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetAssignedByMeAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
        {
            var t = NewTaskItem(5, DateTime.Today.AddDays(i));
            t.AssignedByApplicationUserId = 1;
            ctx.TaskItems.Add(t);
        }
        await ctx.SaveChangesAsync();

        var page1 = await service.GetAssignedByMeAsync(1, page: 1, pageSize: 20);
        var page2 = await service.GetAssignedByMeAsync(1, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.TaskItemId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }
}
