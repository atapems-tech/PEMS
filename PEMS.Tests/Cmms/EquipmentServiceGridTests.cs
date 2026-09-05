using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Equipment Reconciliation): قبلاً
/// EquipmentController.Index از GetAllAsync (بدون Skip/Take) استفاده می‌کرد. این تست‌ها ثابت
/// می‌کنند GetPagedAsync حالا در SQL صفحه‌بندی می‌کند - GetAllAsync دست‌نخورده مانده و این تست‌ها
/// آن را تغییر نمی‌دهند.
/// </summary>
public class EquipmentServiceGridTests
{
    private static EquipmentService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"EquipmentGrid_{dbName}");

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();

        return new EquipmentService(ctx, new RelationshipService(ctx), new FunctionalLocationService(ctx));
    }

    private static Equipment NewEquipment(string tag, string status = "Active", bool isActive = true) => new()
    {
        TagNumber = tag,
        Name = tag,
        EquipmentType = "Pump",
        ProjectId = 1,
        DisciplineId = 1,
        Status = status,
        IsActive = isActive
    };

    [Fact]
    public async Task GetPagedAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetPagedAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.Equipments.Add(NewEquipment($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetPagedAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetPagedAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.Equipments.Add(NewEquipment($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetPagedAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.Equipments.Add(NewEquipment("TAG-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(page: 1, pageSize: 13);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_Page1AndPage2_AreDisjoint()
    {
        var service = CreateService(nameof(GetPagedAsync_Page1AndPage2_AreDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.Equipments.Add(NewEquipment($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(page: 1, pageSize: 20);
        var page2 = await service.GetPagedAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.EquipmentId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetPagedAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetPagedAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        for (var i = 0; i < 30; i++)
            ctx.Equipments.Add(NewEquipment($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var first = await service.GetPagedAsync(page: 1, pageSize: 20);
        var second = await service.GetPagedAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.EquipmentId), second.Items.Select(x => x.EquipmentId));
    }

    [Fact]
    public async Task GetPagedAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetPagedAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetPagedAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersMatchGetAllAsyncBehavior()
    {
        var service = CreateService(nameof(GetPagedAsync_FiltersMatchGetAllAsyncBehavior), out var ctx);
        ctx.Equipments.Add(NewEquipment("PUMP-001", status: "Active"));
        ctx.Equipments.Add(NewEquipment("PUMP-002", status: "Decommissioned"));
        ctx.Equipments.Add(NewEquipment("VALVE-001", status: "Active"));
        await ctx.SaveChangesAsync();

        var byStatus = await service.GetPagedAsync(status: "Active");
        var byQ = await service.GetPagedAsync(q: "PUMP");
        var byBoth = await service.GetPagedAsync(status: "Active", q: "PUMP");

        Assert.Equal(2, byStatus.TotalCount);
        Assert.Equal(2, byQ.TotalCount);
        Assert.Equal(1, byBoth.TotalCount);
        Assert.Equal("PUMP-001", byBoth.Items[0].TagNumber);
    }

    [Fact]
    public async Task GetPagedAsync_InactiveEquipment_IsExcluded()
    {
        var service = CreateService(nameof(GetPagedAsync_InactiveEquipment_IsExcluded), out var ctx);
        ctx.Equipments.Add(NewEquipment("TAG-ACTIVE", isActive: true));
        ctx.Equipments.Add(NewEquipment("TAG-INACTIVE", isActive: false));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync();

        Assert.Single(result.Items);
        Assert.Equal("TAG-ACTIVE", result.Items[0].TagNumber);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsMatchRealData_AfterSqlSideConversion()
    {
        var service = CreateService(nameof(GetDashboardAsync_CountsMatchRealData_AfterSqlSideConversion), out var ctx);
        ctx.Equipments.Add(NewEquipment("TAG-1", status: "Active"));
        ctx.Equipments.Add(NewEquipment("TAG-2", status: "Active"));
        ctx.Equipments.Add(NewEquipment("TAG-3", status: "Decommissioned"));
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(3, dashboard.TotalCount);
        Assert.Equal(2, dashboard.ActiveCount);
        Assert.Equal(1, dashboard.InactiveCount);
        Assert.Contains(dashboard.ByStatus, x => x.Status == "Active" && x.Count == 2);
        Assert.Contains(dashboard.ByStatus, x => x.Status == "Decommissioned" && x.Count == 1);
    }

    [Fact]
    public async Task GetDashboardAsync_ByType_StillBucketsBlankTypesAsUnknown()
    {
        // طبق A-4: این Bucket عمداً در حافظه باقی مانده (ریسک ترجمه‌ی IsNullOrWhiteSpace به SQL)
        var service = CreateService(nameof(GetDashboardAsync_ByType_StillBucketsBlankTypesAsUnknown), out var ctx);
        var blank = NewEquipment("TAG-BLANK");
        blank.EquipmentType = "  ";
        ctx.Equipments.Add(blank);
        ctx.Equipments.Add(NewEquipment("TAG-PUMP"));
        await ctx.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Contains(dashboard.ByType, x => x.EquipmentType == "نامشخص" && x.Count == 1);
        Assert.Contains(dashboard.ByType, x => x.EquipmentType == "Pump" && x.Count == 1);
    }
}
