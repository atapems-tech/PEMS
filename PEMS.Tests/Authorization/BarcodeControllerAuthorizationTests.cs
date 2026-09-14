using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System» بند «Authorization»: هیچ اکشنی نباید بدون بررسی
/// مجوز واقعی سمت سرور عبور کند - حتی اگر منشأ درخواست یک Scan بارکد باشد. BulkGenerate
/// (پرریسک‌ترین) باید Approve بخواهد؛ Disable/Revoke/Replace باید Delete بخواهند.
/// </summary>
public class BarcodeControllerAuthorizationTests
{
    private static async Task<(BarcodeController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"BarcodeAuth_{dbName}");
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
        var barcodeService = new BarcodeService(ctx);
        var renderingService = new BarcodeRenderingService();

        var controller = new BarcodeController(barcodeService, renderingService, ctx, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (controller, _) = await CreateController(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ResolveJson_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(ResolveJson_NoPermission_ReturnsForbid), role: null);

        var result = await controller.ResolveJson("PEMS-BR-XXXX");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ResolveJson_UnknownCode_ReturnsSuccessFalse()
    {
        var (controller, _) = await CreateController(nameof(ResolveJson_UnknownCode_ReturnsSuccessFalse), role: SystemRoles.Viewer);

        var result = await controller.ResolveJson("PEMS-BR-DOESNOTEXIST");

        var json = Assert.IsType<JsonResult>(result);
        var success = (bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!;
        Assert.False(success);
    }

    [Fact]
    public async Task ResolveJson_ValidMaterialItemBarcode_ReturnsTargetTypeAndId()
    {
        var (controller, ctx) = await CreateController(nameof(ResolveJson_ValidMaterialItemBarcode_ReturnsTargetTypeAndId), role: SystemRoles.Viewer);
        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);
        await ctx.SaveChangesAsync();
        var barcodeService = new BarcodeService(ctx);
        var (barcode, _) = await barcodeService.GenerateAsync("MaterialItem", material.MaterialItemId, "Code128", null);

        var result = await controller.ResolveJson(barcode.Code);

        var json = Assert.IsType<JsonResult>(result);
        var type = json.Value!.GetType();
        Assert.True((bool)type.GetProperty("success")!.GetValue(json.Value)!);
        Assert.Equal("MaterialItem", (string)type.GetProperty("targetType")!.GetValue(json.Value)!);
        Assert.Equal(material.MaterialItemId, (int)type.GetProperty("targetId")!.GetValue(json.Value)!);
    }

    [Fact]
    public async Task Resolve_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Resolve_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Resolve("PEMS-BR-XXXX");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Generate_ViewerRole_CannotGenerate_ReturnsForbid_NoBarcodeCreated()
    {
        var (controller, ctx) = await CreateController(nameof(Generate_ViewerRole_CannotGenerate_ReturnsForbid_NoBarcodeCreated), role: SystemRoles.Viewer);
        var eq = new Equipment { TagNumber = "EQ-AUTH-1", Name = "EQ-AUTH-1", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var result = await controller.Generate("Equipment", eq.EquipmentId, "Code128");

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task Generate_MaintenanceEngineer_HasCreate_GateAllowsThrough()
    {
        var (controller, ctx) = await CreateController(nameof(Generate_MaintenanceEngineer_HasCreate_GateAllowsThrough), role: SystemRoles.MaintenanceEngineer);
        var eq = new Equipment { TagNumber = "EQ-AUTH-2", Name = "EQ-AUTH-2", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var result = await controller.Generate("Equipment", eq.EquipmentId, "Code128");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task BulkGenerate_Get_MaintenanceEngineer_HasApprove_GateAllowsThrough()
    {
        // طبق «Bulk Barcode Generation»: پرریسک‌ترین اکشن - Approve؛ MaintenanceEngineer طبق
        // Seed این ماژول واقعاً Approve دارد (همون قاعده‌ی سایر Bulk/Import Pipelineها).
        var (controller, _) = await CreateController(nameof(BulkGenerate_Get_MaintenanceEngineer_HasApprove_GateAllowsThrough), role: SystemRoles.MaintenanceEngineer);

        var result = await controller.BulkGenerate(null, null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task BulkGenerate_Get_RoleWithoutModulePermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(BulkGenerate_Get_RoleWithoutModulePermission_ReturnsForbid), role: SystemRoles.QAManager);

        var result = await controller.BulkGenerate(null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BulkGenerateCommit_ViewerRole_ReturnsForbid_NoBarcodesCreated()
    {
        var (controller, ctx) = await CreateController(nameof(BulkGenerateCommit_ViewerRole_ReturnsForbid_NoBarcodesCreated), role: SystemRoles.Viewer);
        var eq = new Equipment { TagNumber = "EQ-AUTH-3", Name = "EQ-AUTH-3", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var result = await controller.BulkGenerateCommit(new List<int> { eq.EquipmentId }, "Code128");

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.BarcodeIdentities);
    }

    [Fact]
    public async Task Disable_ViewerRole_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Disable_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Disable(99999, "test", "Equipment", 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Revoke_ViewerRole_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Revoke_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Revoke(99999, "test", "Equipment", 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Replace_ViewerRole_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Replace_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Replace(99999, "Equipment", 1, "Code128");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Disable_MaintenanceEngineer_HasDelete_GateAllowsThrough()
    {
        var (controller, ctx) = await CreateController(nameof(Disable_MaintenanceEngineer_HasDelete_GateAllowsThrough), role: SystemRoles.MaintenanceEngineer);
        var eq = new Equipment { TagNumber = "EQ-AUTH-4", Name = "EQ-AUTH-4", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        var service = new BarcodeService(ctx);
        var (barcode, _) = await service.GenerateAsync("Equipment", eq.EquipmentId, "Code128", null);

        var result = await controller.Disable(barcode.BarcodeIdentityId, "test", "Equipment", eq.EquipmentId);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await service.GetByIdAsync(barcode.BarcodeIdentityId);
        Assert.Equal("Disabled", reloaded!.Status);
    }

    [Fact]
    public async Task Svg_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Svg_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Svg(1);

        Assert.IsType<ForbidResult>(result);
    }
}
