using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵ - Instrument): قبلاً
/// InstrumentController.Index کل جدول را با ToListAsync (بدون Skip/Take) می‌خواند و
/// فیلتر Status/شمارنده‌های داشبورد را در حافظه حساب می‌کرد. این تست‌ها ثابت می‌کنند
/// GetListAsync/GetDashboardAsync حالا در SQL صفحه‌بندی/فیلتر/شمارش می‌کنند و رفتار قبلی
/// (فیلتر Status محاسبه‌شده از NextCalibrationDue، شمارنده‌ها روی فیلتر q نه Status) حفظ شده.
/// </summary>
public class InstrumentServiceTests
{
    private static InstrumentService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        // پیشوند نام کلاس: EF Core InMemory دیتابیس‌ها را با یک نام سراسری در کل فرآیند تست
        // نگه می‌دارد (نه Scoped به کلاس) - بدون این پیشوند، نام‌های متد یکسان در کلاس‌های
        // تست دیگر (مثلاً GetListAsync_DefaultPageSize_Is50 در چند Service Test) با اجرای
        // موازی xUnit بین کلاس‌ها برخورد می‌کنند (ArgumentException: Key already added).
        ctx = PemsDbContextFactory.CreateInMemory($"Instrument_{dbName}");

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "INST", Name = "Instrumentation" });
        ctx.SaveChanges();

        return new InstrumentService(ctx);
    }

    private static int _nextEquipmentId = 1;

    private static (Equipment Equipment, Instrument Instrument) NewInstrument(
        string tagNumber, DateTime? nextCalibrationDue, string? loopNumber = null)
    {
        var equipmentId = _nextEquipmentId++;
        var equipment = new Equipment
        {
            EquipmentId = equipmentId,
            TagNumber = tagNumber,
            Name = tagNumber,
            EquipmentType = "Instrument",
            ProjectId = 1,
            DisciplineId = 1,
            IsActive = true
        };
        var instrument = new Instrument
        {
            EquipmentId = equipmentId,
            Equipment = equipment,
            InstrumentType = "PT",
            LoopNumber = loopNumber,
            NextCalibrationDue = nextCalibrationDue,
            IsActive = true
        };
        return (equipment, instrument);
    }

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
        {
            var (eq, inst) = NewInstrument($"TAG-{i:D3}", null);
            ctx.Equipments.Add(eq);
            ctx.Instruments.Add(inst);
        }
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 999); // invalid -> normalizes to 50

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
        {
            var (eq, inst) = NewInstrument($"TAG-{i:D3}", null);
            ctx.Equipments.Add(eq);
            ctx.Instruments.Add(inst);
        }
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        var (eq, inst) = NewInstrument("TAG-001", null);
        ctx.Equipments.Add(eq);
        ctx.Instruments.Add(inst);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 7);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
        {
            var (eq, inst) = NewInstrument($"TAG-{i:D3}", null);
            ctx.Equipments.Add(eq);
            ctx.Instruments.Add(inst);
        }
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.InstrumentId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        for (var i = 0; i < 30; i++)
        {
            var (eq, inst) = NewInstrument($"TAG-{i:D3}", null);
            ctx.Equipments.Add(eq);
            ctx.Instruments.Add(inst);
        }
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.InstrumentId), second.Items.Select(x => x.InstrumentId));
    }

    [Fact]
    public async Task GetListAsync_StatusFilter_Overdue_MatchesOnlyPastDueRecords()
    {
        var service = CreateService(nameof(GetListAsync_StatusFilter_Overdue_MatchesOnlyPastDueRecords), out var ctx);
        var (eqOverdue, instOverdue) = NewInstrument("TAG-OVERDUE", DateTime.Today.AddDays(-5));
        var (eqOk, instOk) = NewInstrument("TAG-OK", DateTime.Today.AddDays(90));
        var (eqUnknown, instUnknown) = NewInstrument("TAG-UNKNOWN", null);
        ctx.Equipments.AddRange(eqOverdue, eqOk, eqUnknown);
        ctx.Instruments.AddRange(instOverdue, instOk, instUnknown);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(status: "Overdue");

        Assert.Single(result.Items);
        Assert.Equal("TAG-OVERDUE", result.Items[0].TagNumber);
        Assert.Equal("Overdue", result.Items[0].CalibrationStatus);
    }

    [Fact]
    public async Task GetListAsync_StatusFilter_DueSoon_MatchesWithin30Days()
    {
        var service = CreateService(nameof(GetListAsync_StatusFilter_DueSoon_MatchesWithin30Days), out var ctx);
        var (eqDueSoon, instDueSoon) = NewInstrument("TAG-DUESOON", DateTime.Today.AddDays(10));
        var (eqOk, instOk) = NewInstrument("TAG-OK", DateTime.Today.AddDays(90));
        ctx.Equipments.AddRange(eqDueSoon, eqOk);
        ctx.Instruments.AddRange(instDueSoon, instOk);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(status: "DueSoon");

        Assert.Single(result.Items);
        Assert.Equal("TAG-DUESOON", result.Items[0].TagNumber);
    }

    [Fact]
    public async Task GetListAsync_StatusFilter_Unknown_MatchesNullNextDue()
    {
        var service = CreateService(nameof(GetListAsync_StatusFilter_Unknown_MatchesNullNextDue), out var ctx);
        var (eqUnknown, instUnknown) = NewInstrument("TAG-UNKNOWN", null);
        var (eqOk, instOk) = NewInstrument("TAG-OK", DateTime.Today.AddDays(90));
        ctx.Equipments.AddRange(eqUnknown, eqOk);
        ctx.Instruments.AddRange(instUnknown, instOk);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(status: "Unknown");

        Assert.Single(result.Items);
        Assert.Equal("TAG-UNKNOWN", result.Items[0].TagNumber);
    }

    [Fact]
    public async Task GetListAsync_SearchFilter_MatchesLoopNumberOrTagNumber()
    {
        var service = CreateService(nameof(GetListAsync_SearchFilter_MatchesLoopNumberOrTagNumber), out var ctx);
        var (eqTarget, instTarget) = NewInstrument("PT-1001", null, loopNumber: "LOOP-77");
        var (eqOther, instOther) = NewInstrument("TT-2002", null, loopNumber: "LOOP-88");
        ctx.Equipments.AddRange(eqTarget, eqOther);
        ctx.Instruments.AddRange(instTarget, instOther);
        await ctx.SaveChangesAsync();

        var byTag = await service.GetListAsync(q: "PT-1001");
        var byLoop = await service.GetListAsync(q: "loop-77");

        Assert.Single(byTag.Items);
        Assert.Single(byLoop.Items);
        Assert.Equal("PT-1001", byTag.Items[0].TagNumber);
        Assert.Equal("PT-1001", byLoop.Items[0].TagNumber);
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
    public async Task GetListAsync_Projection_MapsEquipmentFieldsCorrectly()
    {
        var service = CreateService(nameof(GetListAsync_Projection_MapsEquipmentFieldsCorrectly), out var ctx);
        var (eq, inst) = NewInstrument("PT-5001", DateTime.Today.AddDays(200));
        eq.Manufacturer = "Rosemount";
        eq.Model = "3051S";
        inst.LoopNumber = "LOOP-01";
        inst.System = "DCS";
        ctx.Equipments.Add(eq);
        ctx.Instruments.Add(inst);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();
        var dto = Assert.Single(result.Items);

        Assert.Equal(eq.EquipmentId, dto.EquipmentId);
        Assert.Equal("PT-5001", dto.TagNumber);
        Assert.Equal("Rosemount", dto.Manufacturer);
        Assert.Equal("3051S", dto.Model);
        Assert.Equal("LOOP-01", dto.LoopNumber);
        Assert.Equal("DCS", dto.System);
        Assert.Equal("OK", dto.CalibrationStatus);
    }

    [Fact]
    public async Task GetListAsync_InactiveInstruments_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactiveInstruments_AreExcluded), out var ctx);
        var (eqActive, instActive) = NewInstrument("TAG-ACTIVE", null);
        var (eqInactive, instInactive) = NewInstrument("TAG-INACTIVE", null);
        instInactive.IsActive = false;
        ctx.Equipments.AddRange(eqActive, eqInactive);
        ctx.Instruments.AddRange(instActive, instInactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.Single(result.Items);
        Assert.Equal("TAG-ACTIVE", result.Items[0].TagNumber);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsIgnoreStatusButRespectSearch()
    {
        var service = CreateService(nameof(GetDashboardAsync_CountsIgnoreStatusButRespectSearch), out var ctx);
        var (eqOverdue, instOverdue) = NewInstrument("PT-OVERDUE", DateTime.Today.AddDays(-1), loopNumber: "LOOP-A");
        var (eqDueSoon, instDueSoon) = NewInstrument("PT-DUESOON", DateTime.Today.AddDays(5), loopNumber: "LOOP-B");
        var (eqOk, instOk) = NewInstrument("TT-OK", DateTime.Today.AddDays(90), loopNumber: "LOOP-C");
        ctx.Equipments.AddRange(eqOverdue, eqDueSoon, eqOk);
        ctx.Instruments.AddRange(instOverdue, instDueSoon, instOk);
        await ctx.SaveChangesAsync();

        var allCounts = await service.GetDashboardAsync();
        Assert.Equal(3, allCounts.TotalCount);
        Assert.Equal(1, allCounts.OverdueCount);
        Assert.Equal(1, allCounts.DueSoonCount);

        // فیلتر q روی شمارنده‌ها هم اعمال می‌شود (فقط تگ‌های PT-*)
        var filteredCounts = await service.GetDashboardAsync("PT-");
        Assert.Equal(2, filteredCounts.TotalCount);
        Assert.Equal(1, filteredCounts.OverdueCount);
        Assert.Equal(1, filteredCounts.DueSoonCount);
    }
}
