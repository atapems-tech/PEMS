using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 8» بند ۲۱/۲۲: تست‌های CmmsOperationsService - داشبورد عملیات (شمارنده‌های
/// واقعی WorkRequest/WorkOrder به‌تفکیک وضعیت + Overdue) و Backlog صفحه‌بندی‌شده. همه‌ی
/// شمارنده‌ها باید دقیقاً برابر داده‌ی seed شده باشند - هیچ عددی نباید جعل/گرد شود.
/// </summary>
public class CmmsOperationsServiceTests
{
    private static CmmsOperationsService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"CmmsOps_{dbName}");
        var intelligenceService = new ReliabilityIntelligenceService(ctx);
        return new CmmsOperationsService(ctx, intelligenceService);
    }

    private static int _nextId = 1;

    private static WorkOrder NewWorkOrder(string status, string priority = "Medium", DateTime? dueDate = null, DateTime? createdDate = null) => new()
    {
        WorkOrderId = _nextId++,
        EquipmentId = 1,
        WorkOrderNumber = $"WO-{_nextId:D4}",
        WorkOrderType = "Corrective",
        Priority = priority,
        Status = status,
        DueDate = dueDate,
        CreatedDate = createdDate ?? DateTime.Now,
        IsActive = true
    };

    private static WorkRequest NewWorkRequest(string status) => new()
    {
        WorkRequestId = _nextId++,
        RequestNumber = $"WR-{_nextId:D4}",
        Title = "Test",
        RequestedById = 1,
        RequestedDate = DateTime.Today,
        Priority = "Medium",
        Status = status,
        IsActive = true
    };

    [Fact]
    public async Task GetDashboardAsync_WorkOrderStatusCounts_MatchSeededData()
    {
        var service = CreateService(nameof(GetDashboardAsync_WorkOrderStatusCounts_MatchSeededData), out var ctx);
        ctx.WorkOrders.AddRange(
            NewWorkOrder("Open"), NewWorkOrder("Open"),
            NewWorkOrder("Assigned"),
            NewWorkOrder("InProgress"),
            NewWorkOrder("OnHold"),
            NewWorkOrder("Completed"),
            NewWorkOrder("Closed"), NewWorkOrder("Closed"), NewWorkOrder("Closed"),
            NewWorkOrder("Cancelled"),
            NewWorkOrder("Rejected"));
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, dashboard.WorkOrderOpen);
        Assert.Equal(1, dashboard.WorkOrderAssigned);
        Assert.Equal(1, dashboard.WorkOrderInProgress);
        Assert.Equal(1, dashboard.WorkOrderOnHold);
        Assert.Equal(1, dashboard.WorkOrderCompleted);
        Assert.Equal(3, dashboard.WorkOrderClosed);
        Assert.Equal(1, dashboard.WorkOrderCancelled);
        Assert.Equal(1, dashboard.WorkOrderRejected);
        Assert.Equal(11, dashboard.WorkOrderTotal);
    }

    [Fact]
    public async Task GetDashboardAsync_WorkRequestStatusCounts_MatchSeededData_AndPendingActionExcludesDraftAndPlanned()
    {
        var service = CreateService(nameof(GetDashboardAsync_WorkRequestStatusCounts_MatchSeededData_AndPendingActionExcludesDraftAndPlanned), out var ctx);
        ctx.WorkRequests.AddRange(
            NewWorkRequest("Draft"), NewWorkRequest("Draft"),
            NewWorkRequest("Submitted"),
            NewWorkRequest("UnderReview"),
            NewWorkRequest("Approved"),
            NewWorkRequest("Planned"),
            NewWorkRequest("Converted"),
            NewWorkRequest("Rejected"),
            NewWorkRequest("Cancelled"));
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, dashboard.WorkRequestDraft);
        Assert.Equal(1, dashboard.WorkRequestSubmitted);
        Assert.Equal(1, dashboard.WorkRequestUnderReview);
        Assert.Equal(1, dashboard.WorkRequestApproved);
        Assert.Equal(1, dashboard.WorkRequestPlanned);
        Assert.Equal(1, dashboard.WorkRequestConverted);
        Assert.Equal(9, dashboard.WorkRequestTotal);
        // Submitted + UnderReview + Approved = 3 - نه Draft (هنوز ارسال نشده) و نه Planned (منتظر تبدیل دستی است)
        Assert.Equal(3, dashboard.WorkRequestPendingAction);
    }

    [Fact]
    public async Task GetDashboardAsync_OverdueCount_OnlyCountsOpenWorkOrdersPastDueDate()
    {
        var service = CreateService(nameof(GetDashboardAsync_OverdueCount_OnlyCountsOpenWorkOrdersPastDueDate), out var ctx);
        var past = DateTime.Now.AddDays(-5);
        var future = DateTime.Now.AddDays(5);
        ctx.WorkOrders.AddRange(
            NewWorkOrder("Open", dueDate: past),           // overdue
            NewWorkOrder("Assigned", dueDate: past),       // overdue
            NewWorkOrder("Open", dueDate: future),         // not overdue
            NewWorkOrder("Open", dueDate: null),           // no due date - never counted
            NewWorkOrder("Completed", dueDate: past));     // completed - not part of open backlog, must not count
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, dashboard.WorkOrderOverdue);
    }

    [Fact]
    public async Task GetDashboardAsync_InactiveWorkOrdersAndRequests_AreExcluded()
    {
        var service = CreateService(nameof(GetDashboardAsync_InactiveWorkOrdersAndRequests_AreExcluded), out var ctx);
        var inactiveWo = NewWorkOrder("Open");
        inactiveWo.IsActive = false;
        var inactiveWr = NewWorkRequest("Draft");
        inactiveWr.IsActive = false;
        ctx.WorkOrders.Add(inactiveWo);
        ctx.WorkRequests.Add(inactiveWr);
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(0, dashboard.WorkOrderTotal);
        Assert.Equal(0, dashboard.WorkRequestTotal);
    }

    [Fact]
    public async Task GetBacklogAsync_DefaultFilter_OnlyReturnsNonFinalStatuses()
    {
        var service = CreateService(nameof(GetBacklogAsync_DefaultFilter_OnlyReturnsNonFinalStatuses), out var ctx);
        ctx.WorkOrders.AddRange(
            NewWorkOrder("Open"), NewWorkOrder("Assigned"), NewWorkOrder("InProgress"), NewWorkOrder("OnHold"),
            NewWorkOrder("Completed"), NewWorkOrder("Closed"), NewWorkOrder("Cancelled"), NewWorkOrder("Rejected"));
        await ctx.SaveChangesAsync();

        var result = await service.GetBacklogAsync(pageSize: 20);

        Assert.Equal(4, result.TotalCount);
        Assert.All(result.Items, x => Assert.Contains(x.Status, new[] { "Open", "Assigned", "InProgress", "OnHold" }));
    }

    [Fact]
    public async Task GetBacklogAsync_StatusFilter_OverridesDefaultOpenSet()
    {
        var service = CreateService(nameof(GetBacklogAsync_StatusFilter_OverridesDefaultOpenSet), out var ctx);
        ctx.WorkOrders.AddRange(NewWorkOrder("Open"), NewWorkOrder("Closed"), NewWorkOrder("Closed"));
        await ctx.SaveChangesAsync();

        var result = await service.GetBacklogAsync(status: "Closed", pageSize: 20);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal("Closed", x.Status));
    }

    [Fact]
    public async Task GetBacklogAsync_OverdueOnlyFilter_ExcludesNonOverdueAndNoDueDate()
    {
        var service = CreateService(nameof(GetBacklogAsync_OverdueOnlyFilter_ExcludesNonOverdueAndNoDueDate), out var ctx);
        ctx.WorkOrders.AddRange(
            NewWorkOrder("Open", dueDate: DateTime.Now.AddDays(-2)),
            NewWorkOrder("Open", dueDate: DateTime.Now.AddDays(2)),
            NewWorkOrder("Open", dueDate: null));
        await ctx.SaveChangesAsync();

        var result = await service.GetBacklogAsync(overdueOnly: true, pageSize: 20);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsOverdue);
    }

    [Fact]
    public async Task GetBacklogAsync_AgeDays_ComputedFromRealCreatedDate()
    {
        var service = CreateService(nameof(GetBacklogAsync_AgeDays_ComputedFromRealCreatedDate), out var ctx);
        var wo = NewWorkOrder("Open");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // طبق PEMSDbContext.ApplyCreatedDate: هر Add، CreatedDate را با DateTime.Now بازنویسی
        // می‌کند (Audit invariant سراسری) - برای شبیه‌سازی یک WorkOrder قدیمی، باید بعد از
        // SaveChanges اول (که حالا State=Unchanged است) دوباره Update و Save شود.
        wo.CreatedDate = DateTime.Now.AddDays(-10);
        await ctx.SaveChangesAsync();

        var result = await service.GetBacklogAsync(pageSize: 20);

        Assert.InRange(result.Items[0].AgeDays, 9, 10);
    }

    [Fact]
    public async Task GetBacklogAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetBacklogAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.WorkOrders.Add(NewWorkOrder("Open"));
        await ctx.SaveChangesAsync();

        var result = await service.GetBacklogAsync(pageSize: 7);

        Assert.Equal(50, result.PageSize);
    }
}
