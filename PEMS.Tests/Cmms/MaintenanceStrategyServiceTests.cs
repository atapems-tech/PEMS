using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵ - MaintenanceStrategy): قبلاً
/// MaintenanceStrategyController.Index کل جدول (یا زیرمجموعه‌ی equipmentId) را با
/// Include(Equipment)+ToListAsync (بدون Skip/Take) می‌خواند. این تست‌ها ثابت می‌کنند
/// GetListAsync حالا در SQL صفحه‌بندی/فیلتر می‌کند.
/// </summary>
public class MaintenanceStrategyServiceTests
{
    private static MaintenanceStrategyService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        // پیشوند نام کلاس - نگاه کنید InstrumentServiceTests.CreateService برای دلیل کامل
        // (EF Core InMemory دیتابیس‌ها را با نام سراسری در کل فرآیند تست نگه می‌دارد).
        ctx = PemsDbContextFactory.CreateInMemory($"MaintenanceStrategy_{dbName}");

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();

        return new MaintenanceStrategyService(ctx);
    }

    private static int _nextId = 1;

    private static (Equipment Equipment, MaintenanceStrategy Strategy) NewStrategy(
        string tagNumber, string strategyType = "Preventive", string consequenceCategory = "Operational", bool isActive = true)
    {
        var id = _nextId++;
        var equipment = new Equipment
        {
            EquipmentId = id,
            TagNumber = tagNumber,
            Name = tagNumber,
            EquipmentType = "Pump",
            ProjectId = 1,
            DisciplineId = 1,
            IsActive = true
        };
        var strategy = new MaintenanceStrategy
        {
            EquipmentId = id,
            Equipment = equipment,
            StrategyType = strategyType,
            ConsequenceCategory = consequenceCategory,
            IsActive = isActive
        };
        return (equipment, strategy);
    }

    private static void AddAll(PEMS.Persistence.Context.PEMSDbContext ctx, params (Equipment Equipment, MaintenanceStrategy Strategy)[] rows)
    {
        foreach (var (equipment, strategy) in rows)
        {
            ctx.Equipments.Add(equipment);
            ctx.MaintenanceStrategies.Add(strategy);
        }
    }

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            AddAll(ctx, NewStrategy($"TAG-{i:D3}"));
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
            AddAll(ctx, NewStrategy($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        AddAll(ctx, NewStrategy("TAG-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 11);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            AddAll(ctx, NewStrategy($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.MaintenanceStrategyId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        for (var i = 0; i < 30; i++)
            AddAll(ctx, NewStrategy($"TAG-{i:D3}"));
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.MaintenanceStrategyId), second.Items.Select(x => x.MaintenanceStrategyId));
    }

    [Fact]
    public async Task GetListAsync_EquipmentIdFilter_MatchesOnlyThatEquipment()
    {
        var service = CreateService(nameof(GetListAsync_EquipmentIdFilter_MatchesOnlyThatEquipment), out var ctx);
        var target = NewStrategy("TAG-TARGET");
        var other = NewStrategy("TAG-OTHER");
        AddAll(ctx, target, other);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(equipmentId: target.Equipment.EquipmentId);

        Assert.Single(result.Items);
        Assert.Equal("TAG-TARGET", result.Items[0].EquipmentTagNumber);
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

    [Fact]
    public async Task GetListAsync_Projection_MapsFieldsCorrectly()
    {
        var service = CreateService(nameof(GetListAsync_Projection_MapsFieldsCorrectly), out var ctx);
        var (eq, strategy) = NewStrategy("TAG-001", strategyType: "Predictive", consequenceCategory: "Safety");
        strategy.FailureModeCode = "Vibration";
        strategy.Justification = "High criticality rotating equipment";
        AddAll(ctx, (eq, strategy));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();
        var dto = Assert.Single(result.Items);

        Assert.Equal(eq.EquipmentId, dto.EquipmentId);
        Assert.Equal("TAG-001", dto.EquipmentTagNumber);
        Assert.Equal("Predictive", dto.StrategyType);
        Assert.Equal("Vibration", dto.FailureModeCode);
        Assert.Equal("Safety", dto.ConsequenceCategory);
        Assert.Equal("High criticality rotating equipment", dto.Justification);
    }

    [Fact]
    public async Task GetListAsync_InactiveStrategies_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactiveStrategies_AreExcluded), out var ctx);
        AddAll(ctx,
            NewStrategy("TAG-ACTIVE"),
            NewStrategy("TAG-INACTIVE", isActive: false));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.Single(result.Items);
        Assert.Equal("TAG-ACTIVE", result.Items[0].EquipmentTagNumber);
    }
}
