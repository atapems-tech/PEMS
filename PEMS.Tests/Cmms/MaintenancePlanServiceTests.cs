using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵ - MaintenancePlan): قبلاً
/// MaintenancePlanController.Index کل جدول را با Include(Equipment)+Include(MaintenanceStrategy)+
/// ToListAsync (بدون Skip/Take) می‌خواند. این تست‌ها ثابت می‌کنند GetListAsync حالا در SQL
/// صفحه‌بندی می‌کند و IsOverdue (تاریخ گذشته یا Meter از سررسید عبور کرده) درست محاسبه می‌شود.
/// </summary>
public class MaintenancePlanServiceTests
{
    private static MaintenancePlanService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        // پیشوند نام کلاس - نگاه کنید InstrumentServiceTests.CreateService برای دلیل کامل
        // (EF Core InMemory دیتابیس‌ها را با نام سراسری در کل فرآیند تست نگه می‌دارد).
        ctx = PemsDbContextFactory.CreateInMemory($"MaintenancePlan_{dbName}");

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();

        return new MaintenancePlanService(ctx);
    }

    private static int _nextId = 1;

    // طبق قرارداد InMemory Provider (همون نکته‌ای که در EngineeringDocumentPagedListTests
    // مستند شده): MaintenanceStrategyId یک FK اجباری (غیر-Nullable) است - اگر رکورد
    // MaintenanceStrategy واقعاً وجود نداشته باشد، InMemory کل Plan را از نتیجه‌ی Select حذف
    // می‌کند (برخلاف SQL Server واقعی). پس هر Plan تست باید یک Strategy واقعی داشته باشد.
    private static (Equipment Equipment, MaintenanceStrategy Strategy, MaintenancePlan Plan) NewPlan(
        string planNumber, DateTime? nextDueDate, string triggerType = "Calendar",
        decimal? meterInterval = null, decimal? lastReading = null, decimal? nextDueMeter = null)
    {
        var id = _nextId++;
        var equipment = new Equipment
        {
            EquipmentId = id,
            TagNumber = $"TAG-{id:D3}",
            Name = $"Equipment {id}",
            EquipmentType = "Pump",
            ProjectId = 1,
            DisciplineId = 1,
            IsActive = true
        };
        var strategy = new MaintenanceStrategy
        {
            MaintenanceStrategyId = id,
            EquipmentId = id,
            StrategyType = "PM",
            IsActive = true
        };
        var plan = new MaintenancePlan
        {
            EquipmentId = id,
            Equipment = equipment,
            MaintenanceStrategyId = id,
            MaintenanceStrategy = strategy,
            PlanNumber = planNumber,
            Title = $"Plan {planNumber}",
            TriggerType = triggerType,
            NextDueDate = nextDueDate,
            MeterInterval = meterInterval,
            LastExecutionMeterReading = lastReading,
            NextDueMeterReading = nextDueMeter,
            IsActive = true
        };
        return (equipment, strategy, plan);
    }

    private static void AddAll(PEMS.Persistence.Context.PEMSDbContext ctx, params (Equipment Equipment, MaintenanceStrategy Strategy, MaintenancePlan Plan)[] rows)
    {
        foreach (var (equipment, strategy, plan) in rows)
        {
            ctx.Equipments.Add(equipment);
            ctx.MaintenanceStrategies.Add(strategy);
            ctx.MaintenancePlans.Add(plan);
        }
    }

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            AddAll(ctx, NewPlan($"MP-{i:D3}", DateTime.Today.AddDays(i)));
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
            AddAll(ctx, NewPlan($"MP-{i:D3}", DateTime.Today.AddDays(i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        AddAll(ctx, NewPlan("MP-001", DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 9);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            AddAll(ctx, NewPlan($"MP-{i:D3}", DateTime.Today.AddDays(i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.MaintenancePlanId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        // چند Plan با NextDueDate یکسان (Null) - بدون ThenBy(PK)، ترتیب پایدار نیست
        for (var i = 0; i < 10; i++)
            AddAll(ctx, NewPlan($"MP-{i:D3}", null));
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.MaintenancePlanId), second.Items.Select(x => x.MaintenancePlanId));
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
    public async Task GetListAsync_IsOverdue_TrueWhenNextDueDateInPast()
    {
        var service = CreateService(nameof(GetListAsync_IsOverdue_TrueWhenNextDueDateInPast), out var ctx);
        AddAll(ctx,
            NewPlan("MP-PAST", DateTime.Now.AddDays(-5)),
            NewPlan("MP-FUTURE", DateTime.Now.AddDays(5)));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        var past = result.Items.Single(x => x.PlanNumber == "MP-PAST");
        var future = result.Items.Single(x => x.PlanNumber == "MP-FUTURE");
        Assert.True(past.IsOverdue);
        Assert.False(future.IsOverdue);
    }

    [Fact]
    public async Task GetListAsync_IsOverdue_TrueWhenMeterReadingPastNextDue()
    {
        var service = CreateService(nameof(GetListAsync_IsOverdue_TrueWhenMeterReadingPastNextDue), out var ctx);
        AddAll(ctx,
            NewPlan("MP-METER-OVERDUE", null, triggerType: "Meter",
                meterInterval: 1000, lastReading: 5000, nextDueMeter: 4500), // 5000 >= 4500 -> overdue
            NewPlan("MP-METER-OK", null, triggerType: "Meter",
                meterInterval: 1000, lastReading: 3000, nextDueMeter: 4500)); // 3000 < 4500 -> not overdue
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.True(result.Items.Single(x => x.PlanNumber == "MP-METER-OVERDUE").IsOverdue);
        Assert.False(result.Items.Single(x => x.PlanNumber == "MP-METER-OK").IsOverdue);
    }

    [Fact]
    public async Task GetListAsync_Projection_MapsEquipmentAndStrategyCorrectly()
    {
        var service = CreateService(nameof(GetListAsync_Projection_MapsEquipmentAndStrategyCorrectly), out var ctx);
        var (eq, strategy, plan) = NewPlan("MP-001", DateTime.Today.AddDays(30));
        strategy.StrategyType = "TBM";
        AddAll(ctx, (eq, strategy, plan));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();
        var dto = Assert.Single(result.Items);

        Assert.Equal(eq.TagNumber, dto.EquipmentTagNumber);
        Assert.Equal("TBM", dto.StrategyType);
    }

    [Fact]
    public async Task GetListAsync_InactivePlans_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactivePlans_AreExcluded), out var ctx);
        var active = NewPlan("MP-ACTIVE", DateTime.Today);
        var inactive = NewPlan("MP-INACTIVE", DateTime.Today);
        inactive.Plan.IsActive = false;
        AddAll(ctx, active, inactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.Single(result.Items);
        Assert.Equal("MP-ACTIVE", result.Items[0].PlanNumber);
    }
}
