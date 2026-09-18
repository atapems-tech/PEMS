using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Help;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help Phase 4 — Increment 3B» بخش ۱۷ (Workflow accuracy: «Documented statuses
/// match actual code»): این تست‌ها فقط کد را دوباره نمی‌خوانند - واقعاً IReliabilityService
/// را روی یک مجموعه‌ی داده‌ی ساختگیِ کوچک با اعداد شناخته‌شده اجرا می‌کنند و نتیجه را با همان
/// فرمولی که در محتوای مقاله‌ی reliability-overview ادعا شده مقایسه می‌کنند - یعنی اگر فرمولِ
/// واقعیِ سرویس روزی تغییر کند بدون این‌که Help به‌روزرسانی شود، این تست‌ها Fail می‌شوند.
/// ==============================================================================
/// </summary>
public class ReliabilityHelpContentAccuracyTests
{
    private static PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ReliabilityHelpAccuracy_{dbName}");

    [Fact]
    public async Task MTBF_RequiresAtLeastTwoFailures_EqualsSpanDividedByCountMinusOne()
    {
        var ctx = CreateContext(nameof(MTBF_RequiresAtLeastTwoFailures_EqualsSpanDividedByCountMinusOne));
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "P-101", IsActive = true });
        await ctx.SaveChangesAsync();

        // سه خرابی، دقیقاً ۱۰۰ ساعت از هم فاصله دارند - MTBF باید ۱۰۰ باشد (بازه‌ی ۲۰۰ ساعته ÷ (۳−۱))
        var baseDate = new DateTime(2026, 1, 1);
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = 1, FailureDate = baseDate, FailureMode = "Bearing Failure", IsActive = true },
            new FailureRecord { EquipmentId = 1, FailureDate = baseDate.AddHours(100), FailureMode = "Bearing Failure", IsActive = true },
            new FailureRecord { EquipmentId = 1, FailureDate = baseDate.AddHours(200), FailureMode = "Bearing Failure", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var svc = new ReliabilityService(ctx);
        var result = await svc.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.MTBFHours);
    }

    [Fact]
    public async Task MTBF_OnlyOneFailureRecord_IsNull()
    {
        var ctx = CreateContext(nameof(MTBF_OnlyOneFailureRecord_IsNull));
        ctx.Equipments.Add(new Equipment { EquipmentId = 2, TagNumber = "P-102", IsActive = true });
        ctx.FailureRecords.Add(new FailureRecord { EquipmentId = 2, FailureDate = DateTime.Now, FailureMode = "Leakage", IsActive = true });
        await ctx.SaveChangesAsync();

        var svc = new ReliabilityService(ctx);
        var result = await svc.GetEquipmentReliabilityAsync(2);

        Assert.NotNull(result);
        Assert.Null(result!.MTBFHours); // طبق مقاله: با کمتر از ۲ خرابی، MTBF مقداری ندارد
    }

    [Fact]
    public async Task MTTR_OnlyCountsFailuresWithCompletedLinkedWorkOrder()
    {
        var ctx = CreateContext(nameof(MTTR_OnlyCountsFailuresWithCompletedLinkedWorkOrder));
        ctx.Equipments.Add(new Equipment { EquipmentId = 3, TagNumber = "P-103", IsActive = true });
        var completedWorkOrder = new WorkOrder
        {
            WorkOrderId = 1, EquipmentId = 3, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective",
            StartDate = new DateTime(2026, 1, 1, 8, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 14, 0, 0) // ۶ ساعت
        };
        ctx.WorkOrders.Add(completedWorkOrder);
        await ctx.SaveChangesAsync();

        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = 3, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Bearing Failure", IsActive = true },
            new FailureRecord { EquipmentId = 3, WorkOrderId = null, FailureDate = new DateTime(2026, 1, 2), FailureMode = "Bearing Failure", IsActive = true } // بدون دستور کار - نباید در MTTR سهیم باشد
        );
        await ctx.SaveChangesAsync();

        var svc = new ReliabilityService(ctx);
        var result = await svc.GetEquipmentReliabilityAsync(3);

        Assert.NotNull(result);
        Assert.Equal(6.0, result!.MTTRHours); // فقط همان یک خرابیِ دارای دستور کارِ تکمیل‌شده
    }

    [Fact]
    public async Task Availability_EqualsMTBF_DividedBy_MTBFPlusMTTR_TimesOneHundred()
    {
        var ctx = CreateContext(nameof(Availability_EqualsMTBF_DividedBy_MTBFPlusMTTR_TimesOneHundred));
        ctx.Equipments.Add(new Equipment { EquipmentId = 4, TagNumber = "P-104", IsActive = true });

        var wo1 = new WorkOrder { WorkOrderId = 1, EquipmentId = 4, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", StartDate = new DateTime(2026, 1, 1), CompletionDate = new DateTime(2026, 1, 1).AddHours(10) };
        var wo2 = new WorkOrder { WorkOrderId = 2, EquipmentId = 4, WorkOrderNumber = "WO-2", WorkOrderType = "Corrective", StartDate = new DateTime(2026, 2, 1), CompletionDate = new DateTime(2026, 2, 1).AddHours(10) };
        ctx.WorkOrders.AddRange(wo1, wo2);
        await ctx.SaveChangesAsync();

        var baseDate = new DateTime(2026, 1, 1);
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = 4, WorkOrderId = 1, FailureDate = baseDate, FailureMode = "X", IsActive = true },
            new FailureRecord { EquipmentId = 4, WorkOrderId = 2, FailureDate = baseDate.AddHours(90), FailureMode = "X", IsActive = true } // MTBF=90, MTTR=10
        );
        await ctx.SaveChangesAsync();

        var svc = new ReliabilityService(ctx);
        var result = await svc.GetEquipmentReliabilityAsync(4);

        Assert.NotNull(result);
        Assert.Equal(90.0, result!.MTBFHours);
        Assert.Equal(10.0, result.MTTRHours);
        var expectedAvailability = 90.0 / (90.0 + 10.0) * 100; // = 90%
        Assert.Equal(expectedAvailability, result.AvailabilityPercent!.Value, precision: 4);
    }
}
