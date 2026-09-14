using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS CMMS — Bulk Define Maintenance Strategy Scope & Location/Equipment Resolution»:
/// تست‌های FunctionalLocationService.GetDescendantIdsAsync - پیمایش واقعی درخت Parent/Child
/// (نه فرض پیشوند رشته‌ای روی Code).
/// </summary>
public class FunctionalLocationDescendantResolutionTests
{
    private static FunctionalLocationService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"FlDescendants_{dbName}");
        return new FunctionalLocationService(ctx);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_EmptyInput_ReturnsEmpty()
    {
        var service = CreateService(nameof(GetDescendantIdsAsync_EmptyInput_ReturnsEmpty), out _);

        var result = await service.GetDescendantIdsAsync(new List<int>(), includeSelf: true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_LeafNodeIncludeSelf_ReturnsOnlyItself()
    {
        var service = CreateService(nameof(GetDescendantIdsAsync_LeafNodeIncludeSelf_ReturnsOnlyItself), out var ctx);
        var leaf = new FunctionalLocation { Code = "LEAF", Name = "Leaf", IsActive = true };
        ctx.FunctionalLocations.Add(leaf);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(new List<int> { leaf.FunctionalLocationId }, includeSelf: true);

        Assert.Single(result);
        Assert.Contains(leaf.FunctionalLocationId, result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_LeafNodeExcludeSelf_ReturnsEmpty()
    {
        var service = CreateService(nameof(GetDescendantIdsAsync_LeafNodeExcludeSelf_ReturnsEmpty), out var ctx);
        var leaf = new FunctionalLocation { Code = "LEAF2", Name = "Leaf2", IsActive = true };
        ctx.FunctionalLocations.Add(leaf);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(new List<int> { leaf.FunctionalLocationId }, includeSelf: false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_MultiLevelTree_ReturnsAllDescendantsAtEveryDepth()
    {
        // درخت: Unit -> Area1, Area2 ; Area1 -> Sub1, Sub2 ; Area2 -> (بدون فرزند)
        var service = CreateService(nameof(GetDescendantIdsAsync_MultiLevelTree_ReturnsAllDescendantsAtEveryDepth), out var ctx);
        var unit = new FunctionalLocation { Code = "UNIT", Name = "Unit", IsActive = true };
        ctx.FunctionalLocations.Add(unit);
        await ctx.SaveChangesAsync();

        var area1 = new FunctionalLocation { Code = "AREA1", Name = "Area1", ParentId = unit.FunctionalLocationId, IsActive = true };
        var area2 = new FunctionalLocation { Code = "AREA2", Name = "Area2", ParentId = unit.FunctionalLocationId, IsActive = true };
        ctx.FunctionalLocations.AddRange(area1, area2);
        await ctx.SaveChangesAsync();

        var sub1 = new FunctionalLocation { Code = "SUB1", Name = "Sub1", ParentId = area1.FunctionalLocationId, IsActive = true };
        var sub2 = new FunctionalLocation { Code = "SUB2", Name = "Sub2", ParentId = area1.FunctionalLocationId, IsActive = true };
        ctx.FunctionalLocations.AddRange(sub1, sub2);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(new List<int> { unit.FunctionalLocationId }, includeSelf: true);

        Assert.Equal(5, result.Count);
        Assert.Contains(unit.FunctionalLocationId, result);
        Assert.Contains(area1.FunctionalLocationId, result);
        Assert.Contains(area2.FunctionalLocationId, result);
        Assert.Contains(sub1.FunctionalLocationId, result);
        Assert.Contains(sub2.FunctionalLocationId, result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_MultiLevelTree_ExcludeSelf_OmitsOnlyTheSeedNode()
    {
        var service = CreateService(nameof(GetDescendantIdsAsync_MultiLevelTree_ExcludeSelf_OmitsOnlyTheSeedNode), out var ctx);
        var unit = new FunctionalLocation { Code = "UNIT3", Name = "Unit3", IsActive = true };
        ctx.FunctionalLocations.Add(unit);
        await ctx.SaveChangesAsync();
        var child = new FunctionalLocation { Code = "CHILD3", Name = "Child3", ParentId = unit.FunctionalLocationId, IsActive = true };
        ctx.FunctionalLocations.Add(child);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(new List<int> { unit.FunctionalLocationId }, includeSelf: false);

        Assert.Single(result);
        Assert.Contains(child.FunctionalLocationId, result);
        Assert.DoesNotContain(unit.FunctionalLocationId, result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_MultipleSeedNodes_UnionsBothSubtreesWithoutDuplicates()
    {
        var service = CreateService(nameof(GetDescendantIdsAsync_MultipleSeedNodes_UnionsBothSubtreesWithoutDuplicates), out var ctx);
        var root1 = new FunctionalLocation { Code = "ROOT1", Name = "Root1", IsActive = true };
        var root2 = new FunctionalLocation { Code = "ROOT2", Name = "Root2", IsActive = true };
        ctx.FunctionalLocations.AddRange(root1, root2);
        await ctx.SaveChangesAsync();
        var childOfRoot1 = new FunctionalLocation { Code = "C1", Name = "C1", ParentId = root1.FunctionalLocationId, IsActive = true };
        ctx.FunctionalLocations.Add(childOfRoot1);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(
            new List<int> { root1.FunctionalLocationId, root2.FunctionalLocationId }, includeSelf: true);

        Assert.Equal(3, result.Count);
        Assert.Contains(root1.FunctionalLocationId, result);
        Assert.Contains(root2.FunctionalLocationId, result);
        Assert.Contains(childOfRoot1.FunctionalLocationId, result);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_OverlappingSeedsInSameSubtree_DoesNotDuplicateOrInfiniteLoop()
    {
        // درخواست هم‌زمان Parent و Child خودش - نباید حلقه یا تکرار ایجاد کند
        var service = CreateService(nameof(GetDescendantIdsAsync_OverlappingSeedsInSameSubtree_DoesNotDuplicateOrInfiniteLoop), out var ctx);
        var parent = new FunctionalLocation { Code = "P4", Name = "P4", IsActive = true };
        ctx.FunctionalLocations.Add(parent);
        await ctx.SaveChangesAsync();
        var child = new FunctionalLocation { Code = "C4", Name = "C4", ParentId = parent.FunctionalLocationId, IsActive = true };
        ctx.FunctionalLocations.Add(child);
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(
            new List<int> { parent.FunctionalLocationId, child.FunctionalLocationId }, includeSelf: true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetDescendantIdsAsync_NonexistentSeedId_ReturnsItAloneWhenIncludeSelf_NoCrash()
    {
        // شناسه‌ای که در جدول وجود ندارد نباید باعث خطا شود - فقط فرزندی برایش پیدا نمی‌شود
        var service = CreateService(nameof(GetDescendantIdsAsync_NonexistentSeedId_ReturnsItAloneWhenIncludeSelf_NoCrash), out var ctx);
        ctx.FunctionalLocations.Add(new FunctionalLocation { Code = "REAL", Name = "Real", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await service.GetDescendantIdsAsync(new List<int> { 99999 }, includeSelf: true);

        Assert.Single(result);
        Assert.Contains(99999, result);
    }
}
