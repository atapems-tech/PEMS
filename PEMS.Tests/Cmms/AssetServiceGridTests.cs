using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Asset Reconciliation): قبلاً
/// AssetService.GetListAsync واقعاً Skip/Take/CountAsync SQL داشت اما pageSize=25 مستقیم
/// (بدون DataGridPageSize.Normalize) پاس داده می‌شد و View هیچ Pager/Column-resize ای نداشت.
/// این تست‌ها روی لایه‌ی Service تمرکز دارند (pageSize contract) - Pager/data-grid-key سمت View
/// با inspection مستقیم فایل تایید شده‌اند (خارج از scope تست خودکار).
/// </summary>
public class AssetServiceGridTests
{
    private static AssetService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"AssetGrid_{dbName}");

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();

        return new AssetService(ctx, new FunctionalLocationService(ctx));
    }

    private static int _nextId = 1;

    private static (Equipment Equipment, Asset Asset) NewAsset(string assetNumber, string status = "Active", bool isActive = true)
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
        var asset = new Asset
        {
            EquipmentId = id,
            Equipment = equipment,
            AssetNumber = assetNumber,
            Status = status,
            IsActive = isActive
        };
        return (equipment, asset);
    }

    private static void AddAll(PEMS.Persistence.Context.PEMSDbContext ctx, params (Equipment Equipment, Asset Asset)[] rows)
    {
        foreach (var (equipment, asset) in rows)
        {
            ctx.Equipments.Add(equipment);
            ctx.Assets.Add(asset);
        }
    }

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            AddAll(ctx, NewAsset($"AST-{i:D3}"));
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
            AddAll(ctx, NewAsset($"AST-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        // قبل از این فاز، Controller بی‌قید و شرط pageSize=25 پاس می‌داد؛ حالا Service خودش هم
        // مقادیر نامعتبر (از جمله همون ۲۵ی قدیمی) را به ۵۰ برمی‌گرداند.
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        AddAll(ctx, NewAsset("AST-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            AddAll(ctx, NewAsset($"AST-{i:D3}"));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.AssetId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            AddAll(ctx, NewAsset("AST-SAME")); // همه یک AssetNumber یکسان - بدون ThenBy(Id) پایدار نیست
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.AssetId), second.Items.Select(x => x.AssetId));
    }

    [Fact]
    public async Task GetListAsync_StatusFilter_StillWorks()
    {
        var service = CreateService(nameof(GetListAsync_StatusFilter_StillWorks), out var ctx);
        AddAll(ctx,
            NewAsset("AST-ACTIVE", status: "Active"),
            NewAsset("AST-RETIRED", status: "Retired"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(status: "Active");

        Assert.Single(result.Items);
        Assert.Equal("AST-ACTIVE", result.Items[0].AssetNumber);
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
}
