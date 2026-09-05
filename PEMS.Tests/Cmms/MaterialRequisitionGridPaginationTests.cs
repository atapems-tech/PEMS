using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: MaterialRequisition Grid». این کنترلر دو Grid جدا دارد:
/// MR List (Index → GetAllAsync، قبلاً ۳ Include غیرضروری + بدون Skip/Take) و MRQ List
/// (Quotations → مستقیم روی Context، قبلاً کاملاً بدون صفحه‌بندی). HasPermissionAsync→Forbid
/// gate (MaterialRequisition.View) روی هر دو Action باید دست‌نخورده بماند.
/// </summary>
public class MaterialRequisitionGridPaginationTests
{
    private static async Task<(MaterialRequisitionController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.EngineeringManager)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"MRGrid_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };
        var mrService = new MaterialRequisitionService(ctx);

        var controller = new MaterialRequisitionController(mrService, ctx, null!, permissionService, currentUser);
        return (controller, ctx);
    }

    private static int _nextId = 1;

    private static MaterialRequisition NewMR(DateTime createdDate, string status = "Draft") => new()
    {
        MRId = _nextId++,
        MRNumber = $"MR-{_nextId:D4}",
        ProjectId = 1,
        DisciplineId = 1,
        Description = "Test MR",
        Status = status,
        CreatedDate = createdDate
    };

    private static MaterialRequisitionQuotation NewMRQ(int mrId, DateTime createdDate, string status = "Sent") => new()
    {
        MRQId = _nextId++,
        MRQNumber = $"MRQ-{_nextId:D4}",
        MRId = mrId,
        VendorName = "Test Vendor",
        QuotationStatus = status,
        CreatedDate = createdDate
    };

    private static async Task<PagedResult<MRDto>> RunIndex(
        MaterialRequisitionController controller, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<MRDto>>(((ViewResult)await controller.Index(page, pageSize)).Model);

    private static async Task<PagedResult<MRQDto>> RunQuotations(
        MaterialRequisitionController controller, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<MRQDto>>(((ViewResult)await controller.Quotations(page, pageSize)).Model);

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);
        ctx.MaterialRequisitions.Add(NewMR(DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await controller.Index(1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var (controller, ctx) = await CreateController(nameof(Index_DefaultPageSize_Is50));
        for (var i = 0; i < 60; i++)
            ctx.MaterialRequisitions.Add(NewMR(DateTime.Today.AddMinutes(-i)));
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
        var (controller, ctx) = await CreateController($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}");
        for (var i = 0; i < 120; i++)
            ctx.MaterialRequisitions.Add(NewMR(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var (controller, ctx) = await CreateController(nameof(Index_Page1AndPage2_AreStableAndDisjoint));
        for (var i = 0; i < 25; i++)
            ctx.MaterialRequisitions.Add(NewMR(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 20);
        var page2 = await RunIndex(controller, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.MRId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var (controller, ctx) = await CreateController(nameof(Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice));
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.MaterialRequisitions.Add(NewMR(sameDate));
        await ctx.SaveChangesAsync();

        var first = await RunIndex(controller, page: 1, pageSize: 20);
        var second = await RunIndex(controller, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.MRId), second.Items.Select(x => x.MRId));
    }

    [Fact]
    public async Task Index_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var (controller, _) = await CreateController(nameof(Index_EmptyDataset_ReturnsEmptyPagedResult));

        var result = await RunIndex(controller);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Quotations_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Quotations_WithoutViewPermission_ReturnsForbid), role: null);
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await controller.Quotations(1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Quotations_DefaultPageSize_Is50()
    {
        var (controller, ctx) = await CreateController(nameof(Quotations_DefaultPageSize_Is50));
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        for (var i = 0; i < 60; i++)
            ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunQuotations(controller, page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Quotations_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var (controller, ctx) = await CreateController($"{nameof(Quotations_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}");
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        for (var i = 0; i < 120; i++)
            ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunQuotations(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Quotations_Page1AndPage2_AreStableAndDisjoint()
    {
        var (controller, ctx) = await CreateController(nameof(Quotations_Page1AndPage2_AreStableAndDisjoint));
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        for (var i = 0; i < 25; i++)
            ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await RunQuotations(controller, page: 1, pageSize: 20);
        var page2 = await RunQuotations(controller, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.MRQId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Quotations_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var (controller, ctx) = await CreateController(nameof(Quotations_DeterministicOrdering_SameCreatedDate_SameOrderTwice));
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, sameDate));
        await ctx.SaveChangesAsync();

        var first = await RunQuotations(controller, page: 1, pageSize: 20);
        var second = await RunQuotations(controller, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.MRQId), second.Items.Select(x => x.MRQId));
    }

    [Fact]
    public async Task Quotations_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var (controller, _) = await CreateController(nameof(Quotations_EmptyDataset_ReturnsEmptyPagedResult));

        var result = await RunQuotations(controller);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Quotations_ReturnsMRNumber_ViaMRNavigation()
    {
        var (controller, ctx) = await CreateController(nameof(Quotations_ReturnsMRNumber_ViaMRNavigation));
        var mr = NewMR(DateTime.Today);
        ctx.MaterialRequisitions.Add(mr);
        ctx.MaterialRequisitionQuotations.Add(NewMRQ(mr.MRId, DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await RunQuotations(controller);

        Assert.Single(result.Items);
        Assert.Equal(mr.MRNumber, result.Items[0].MRNumber);
    }
}
