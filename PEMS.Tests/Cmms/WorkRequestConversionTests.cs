using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 8 — Operational Maintenance &amp; Reliability Data Activation» بند ۱۱/۲۸:
/// در ممیزی فاز ۸ مشخص شد که هیچ تستی روی WorkRequestService.AdvanceStatusAsync/
/// ConvertToWorkOrderAsync وجود ندارد - این‌ها مکانیزم واقعی «درب ورودی» CMMS هستند
/// (Maintenance Request → Work Request → Work Order). این فایل آن شکاف را می‌پوشاند، به‌علاوه
/// اصلاح فاز ۸: قبلاً ConvertToWorkOrderAsync دو SaveChangesAsync جدا داشت (خطر ناسازگاری در
/// صورت خرابی بین آن دو) - حالا یک SaveChangesAsync اتمیک با navigation property fixup است؛
/// تست‌های Success زیر همان مسیر اصلاح‌شده را تایید می‌کنند (WorkOrderId واقعی، نه ۰).
/// </summary>
public class WorkRequestConversionTests
{
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null)
            => Task.FromResult($"WO-TEST-{++_counter:D4}");
    }

    private static WorkRequestService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"WorkRequestConversion_{dbName}");
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Maintenance" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Test", LastName = "User", DepartmentId = 1, IsActive = true });
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Test Pump", IsActive = true });
        ctx.SaveChanges();
        return new WorkRequestService(ctx, new FakeNumberingService(), null!);
    }

    private static WorkRequest NewWorkRequest(string status, int? equipmentId = 1) => new()
    {
        RequestNumber = "WR-0001",
        Title = "Pump vibration",
        RequestedById = 1,
        RequestedDate = DateTime.Today,
        EquipmentId = equipmentId,
        Priority = "Medium",
        Status = status,
        IsActive = true
    };

    [Fact]
    public async Task ConvertToWorkOrderAsync_FromPlannedWithEquipment_Succeeds_AndLinksRealWorkOrderId()
    {
        var service = CreateService(nameof(ConvertToWorkOrderAsync_FromPlannedWithEquipment_Succeeds_AndLinksRealWorkOrderId), out var ctx);
        var wr = NewWorkRequest("Planned");
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        var (success, error, workOrderId) = await service.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");

        Assert.True(success, error);
        Assert.NotNull(workOrderId);
        Assert.True(workOrderId > 0);

        var reloadedWr = ctx.WorkRequests.Single(x => x.WorkRequestId == wr.WorkRequestId);
        Assert.Equal("Converted", reloadedWr.Status);
        Assert.Equal(workOrderId, reloadedWr.LinkedWorkOrderId);

        var workOrder = ctx.WorkOrders.Single(x => x.WorkOrderId == workOrderId);
        Assert.Equal(1, workOrder.EquipmentId);
        Assert.Equal(wr.WorkRequestId, workOrder.SourceWorkRequestId);
        Assert.Equal("Open", workOrder.Status);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_FromPlanned_WritesStatusHistoryRow()
    {
        var service = CreateService(nameof(ConvertToWorkOrderAsync_FromPlanned_WritesStatusHistoryRow), out var ctx);
        var wr = NewWorkRequest("Planned");
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        await service.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");

        var history = ctx.WorkRequestStatusHistories.Where(x => x.WorkRequestId == wr.WorkRequestId).ToList();
        Assert.Contains(history, h => h.ToStatus == "Converted");
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("UnderReview")]
    [InlineData("Approved")]
    [InlineData("Converted")]
    public async Task ConvertToWorkOrderAsync_NotPlanned_Fails_AndCreatesNoWorkOrder(string status)
    {
        var service = CreateService($"{nameof(ConvertToWorkOrderAsync_NotPlanned_Fails_AndCreatesNoWorkOrder)}_{status}", out var ctx);
        var wr = NewWorkRequest(status);
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        var (success, error, workOrderId) = await service.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(workOrderId);
        Assert.Empty(ctx.WorkOrders);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_AlreadyLinked_Fails_DoubleConversionGuard()
    {
        var service = CreateService(nameof(ConvertToWorkOrderAsync_AlreadyLinked_Fails_DoubleConversionGuard), out var ctx);
        var existingWorkOrder = new WorkOrder { EquipmentId = 1, WorkOrderNumber = "WO-EXIST-0001", WorkOrderType = "Corrective", Status = "Open", IsActive = true };
        ctx.WorkOrders.Add(existingWorkOrder);
        await ctx.SaveChangesAsync();

        var wr = NewWorkRequest("Planned");
        wr.LinkedWorkOrderId = existingWorkOrder.WorkOrderId;
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        var (success, error, workOrderId) = await service.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(workOrderId);
        // فقط همان یک WorkOrder قبلی باید وجود داشته باشد - نه یک WorkOrder یتیم دوم
        Assert.Single(ctx.WorkOrders);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_NoEquipmentLinked_Fails()
    {
        var service = CreateService(nameof(ConvertToWorkOrderAsync_NoEquipmentLinked_Fails), out var ctx);
        var wr = NewWorkRequest("Planned", equipmentId: null);
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        var (success, error, workOrderId) = await service.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(workOrderId);
        Assert.Empty(ctx.WorkOrders);
    }

    [Theory]
    [InlineData("Draft", "Submitted", true)]
    [InlineData("Submitted", "UnderReview", true)]
    [InlineData("UnderReview", "Approved", true)]
    [InlineData("Approved", "Planned", true)]
    [InlineData("Draft", "Approved", false)]
    [InlineData("Draft", "Planned", false)]
    [InlineData("Converted", "Draft", false)]
    public async Task AdvanceStatusAsync_EnforcesLegalTransitionMap(string from, string to, bool expectedSuccess)
    {
        var service = CreateService($"{nameof(AdvanceStatusAsync_EnforcesLegalTransitionMap)}_{from}_{to}", out var ctx);
        var wr = NewWorkRequest(from);
        ctx.WorkRequests.Add(wr);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.AdvanceStatusAsync(wr.WorkRequestId, to, "test reason", 1);

        Assert.Equal(expectedSuccess, success);
        var reloaded = ctx.WorkRequests.Single(x => x.WorkRequestId == wr.WorkRequestId);
        Assert.Equal(expectedSuccess ? to : from, reloaded.Status);
    }
}
