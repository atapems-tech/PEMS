using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: EquipmentCriticality Grid». قبلاً کل جدول فیلترشده (با Include)
/// با ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard (ClassA/B/C،
/// Unassessed) هم با یک بارگذاری کامل دوم در حافظه محاسبه می‌شدند - اکنون با GroupBy/EXISTS در
/// SQL. Index هیچ Permission gate ای ندارد (طبق کد موجود) - این رفتار عمداً دست‌نخورده باقی
/// مانده است.
/// </summary>
public class EquipmentCriticalityGridPaginationTests
{
    private static EquipmentCriticalityController CreateController(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"EqCritGrid_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();
        return new EquipmentCriticalityController(ctx);
    }

    private static int _nextId = 1;

    private static Equipment NewEquipment(string tag)
    {
        var id = _nextId++;
        return new Equipment { EquipmentId = id, TagNumber = tag, Name = tag, EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true };
    }

    private static EquipmentCriticality NewCriticality(int equipmentId, string criticalityClass = "A") => new()
    {
        EquipmentCriticalityId = _nextId++,
        EquipmentId = equipmentId,
        CriticalityClass = criticalityClass,
        IsActive = true,
        CreatedDate = DateTime.Now
    };

    private static async Task<PagedResult<EquipmentCriticalityListDto>> RunIndex(
        EquipmentCriticalityController controller, string? criticalityClass = null, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<EquipmentCriticalityListDto>>(
            ((ViewResult)await controller.Index(criticalityClass, page, pageSize)).Model);

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var controller = CreateController(nameof(Index_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.EquipmentCriticalities.Add(NewCriticality(eq.EquipmentId));
        }
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Index_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var controller = CreateController($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.EquipmentCriticalities.Add(NewCriticality(eq.EquipmentId));
        }
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var controller = CreateController(nameof(Index_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.EquipmentCriticalities.Add(NewCriticality(eq.EquipmentId));
        }
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 20);
        var page2 = await RunIndex(controller, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.EquipmentCriticalityId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Index_ClassFilter_StillWorksAfterPageSizeChange()
    {
        var controller = CreateController(nameof(Index_ClassFilter_StillWorksAfterPageSizeChange), out var ctx);
        var eqA = NewEquipment("TAG-A");
        var eqB = NewEquipment("TAG-B");
        ctx.Equipments.AddRange(eqA, eqB);
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqB.EquipmentId, "B"));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, criticalityClass: "A");

        Assert.Single(result.Items);
        Assert.Equal("A", result.Items[0].CriticalityClass);
    }

    [Fact]
    public async Task Index_InactiveCriticalities_AreExcluded()
    {
        var controller = CreateController(nameof(Index_InactiveCriticalities_AreExcluded), out var ctx);
        var eq1 = NewEquipment("TAG-ACTIVE");
        var eq2 = NewEquipment("TAG-INACTIVE");
        ctx.Equipments.AddRange(eq1, eq2);
        var active = NewCriticality(eq1.EquipmentId);
        var inactive = NewCriticality(eq2.EquipmentId);
        inactive.IsActive = false;
        ctx.EquipmentCriticalities.Add(active);
        ctx.EquipmentCriticalities.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result.Items);
        Assert.Equal(active.EquipmentCriticalityId, result.Items[0].EquipmentCriticalityId);
    }

    [Fact]
    public async Task Index_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var controller = CreateController(nameof(Index_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await RunIndex(controller);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Index_DashboardCounts_AreGlobal_NotRestrictedByClassFilter()
    {
        var controller = CreateController(nameof(Index_DashboardCounts_AreGlobal_NotRestrictedByClassFilter), out var ctx);
        var eqA1 = NewEquipment("TAG-A1");
        var eqA2 = NewEquipment("TAG-A2");
        var eqB = NewEquipment("TAG-B");
        var eqUnassessed = NewEquipment("TAG-UNASSESSED");
        ctx.Equipments.AddRange(eqA1, eqA2, eqB, eqUnassessed);
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA1.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA2.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqB.EquipmentId, "B"));
        await ctx.SaveChangesAsync();

        var view = (ViewResult)await controller.Index(criticalityClass: "A", page: 1, pageSize: 20);

        Assert.Equal(2, (int)view.ViewData["ClassACount"]!);
        Assert.Equal(1, (int)view.ViewData["ClassBCount"]!); // مستقل از فیلتر "A" فعلی
        Assert.Equal(0, (int)view.ViewData["ClassCCount"]!);
        Assert.Equal(1, (int)view.ViewData["UnassessedCount"]!);
    }
}
