using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: Inspection Grid». قبلاً کل جدول فعال با ToListAsync (بدون
/// Skip/Take) بارگذاری می‌شد. اولویت‌بندی (Alert/Overdue/DueSoon اول) از روی مقادیر
/// محاسبه‌شده‌ی ConditionStatus/DueStatus است، نه ستونی از DB - پس GetPagedFleetAsync باید
/// صفحه‌بندی را روی نتیجه‌ی نهایی/کامل‌مرتب‌شده انجام دهد، نه قبل از آن.
/// </summary>
public class InspectionGridPaginationTests
{
    private static InspectionService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"InspectionGrid_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();
        return new InspectionService(ctx);
    }

    private static int _nextId = 1;

    private static Equipment NewEquipment(string tag)
    {
        var id = _nextId++;
        return new Equipment { EquipmentId = id, TagNumber = tag, Name = tag, EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true };
    }

    private static InspectionRecord NewRecord(int equipmentId, DateTime inspectionDate, bool isActive = true) => new()
    {
        InspectionRecordId = _nextId++,
        EquipmentId = equipmentId,
        InspectionType = "Visual",
        InspectionDate = inspectionDate,
        IsActive = isActive
    };

    [Fact]
    public async Task GetPagedFleetAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetPagedFleetAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.InspectionRecords.Add(NewRecord(eq.EquipmentId, DateTime.Today.AddDays(-i)));
        }
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedFleetAsync(1, 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetPagedFleetAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetPagedFleetAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.InspectionRecords.Add(NewRecord(eq.EquipmentId, DateTime.Today.AddDays(-i)));
        }
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedFleetAsync(1, pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetPagedFleetAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetPagedFleetAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.InspectionRecords.Add(NewRecord(eq.EquipmentId, DateTime.Today.AddDays(-i)));
        }
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedFleetAsync(1, 20);
        var page2 = await service.GetPagedFleetAsync(2, 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.InspectionRecordId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetPagedFleetAsync_DeterministicOrdering_SameInspectionDate_SameOrderTwice()
    {
        var service = CreateService(nameof(GetPagedFleetAsync_DeterministicOrdering_SameInspectionDate_SameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.InspectionRecords.Add(NewRecord(eq.EquipmentId, sameDate));
        }
        await ctx.SaveChangesAsync();

        var first = await service.GetPagedFleetAsync(1, 20);
        var second = await service.GetPagedFleetAsync(1, 20);

        Assert.Equal(first.Items.Select(x => x.InspectionRecordId), second.Items.Select(x => x.InspectionRecordId));
    }

    [Fact]
    public async Task GetPagedFleetAsync_InactiveRecords_AreExcluded()
    {
        var service = CreateService(nameof(GetPagedFleetAsync_InactiveRecords_AreExcluded), out var ctx);
        var eq1 = NewEquipment("TAG-ACTIVE");
        var eq2 = NewEquipment("TAG-INACTIVE");
        ctx.Equipments.AddRange(eq1, eq2);
        var active = NewRecord(eq1.EquipmentId, DateTime.Today);
        var inactive = NewRecord(eq2.EquipmentId, DateTime.Today, isActive: false);
        ctx.InspectionRecords.Add(active);
        ctx.InspectionRecords.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedFleetAsync(1, 20);

        Assert.Single(result.Items);
        Assert.Equal(active.InspectionRecordId, result.Items[0].InspectionRecordId);
    }

    [Fact]
    public async Task GetPagedFleetAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetPagedFleetAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetPagedFleetAsync(1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedFleetAsync_AlertsSortFirst_AcrossFullSet_NotJustFirstPage()
    {
        // این تست ثابت می‌کند که مرتب‌سازی اولویت (Alert اول) روی کل مجموعه انجام می‌شود، نه
        // فقط صفحه‌ی اول از یک بارگذاری از‌پیش صفحه‌بندی‌شده. یک رکورد Alert در انتهای تاریخ
        // درج می‌شود (اگر صفحه‌بندی زودتر اتفاق بیفتد، ممکن است اصلاً دیده نشود).
        var service = CreateService(nameof(GetPagedFleetAsync_AlertsSortFirst_AcrossFullSet_NotJustFirstPage), out var ctx);
        for (var i = 0; i < 25; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.InspectionRecords.Add(NewRecord(eq.EquipmentId, DateTime.Today.AddDays(-i)));
        }

        var alertEq = NewEquipment("TAG-ALERT");
        ctx.Equipments.Add(alertEq);
        var alertRecord = NewRecord(alertEq.EquipmentId, DateTime.Today.AddDays(-100));
        alertRecord.MeasurementValue = 5;
        alertRecord.LowerLimit = 10;
        ctx.InspectionRecords.Add(alertRecord);
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedFleetAsync(1, 20);

        Assert.Equal(26, page1.TotalCount);
        Assert.Equal("Alert", page1.Items[0].ConditionStatus);
        Assert.Equal(alertRecord.InspectionRecordId, page1.Items[0].InspectionRecordId);
    }
}
