using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «Bulk Define Maintenance Strategy — Location and Tag Selection» ممیزی: تست‌های رفتار
/// جدید فیلتر (چندانتخابی Discipline/EquipmentType، فیلتر Functional Location، جستجوی Tag) و
/// شکاف اعتبارسنجی که در Commit رفع شد (تجهیز غیرفعال). هیچ تستی برای رفتار پشتیبانی‌نشده
/// (مثلاً بسط والد→فرزند Functional Location) اضافه نشد - آن رفتار عمداً پیاده‌سازی نشد.
/// </summary>
public class BulkDefineMaintenanceStrategyTests
{
    private static async Task<(MaintenanceStrategyController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.EngineeringSupervisor)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"BulkStrategy_{dbName}");
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
        var strategyService = new MaintenanceStrategyService(ctx);
        var functionalLocationService = new FunctionalLocationService(ctx);

        var controller = new MaintenanceStrategyController(ctx, strategyService, functionalLocationService, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    private static List<Equipment> GetViewEquipments(IActionResult result)
    {
        var viewResult = Assert.IsType<ViewResult>(result);
        return (List<Equipment>)viewResult.ViewData["Equipments"]!;
    }

    [Fact]
    public async Task BulkCreate_Get_MultipleDisciplineIds_ReturnsUnionOfBothDisciplines()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_MultipleDisciplineIds_ReturnsUnionOfBothDisciplines));
        ctx.Equipments.Add(new Equipment { TagNumber = "M-1", Name = "M-1", EquipmentType = "Pump", DisciplineId = 1, IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "M-2", Name = "M-2", EquipmentType = "Pump", DisciplineId = 2, IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "M-3", Name = "M-3", EquipmentType = "Pump", DisciplineId = 3, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { 1, 2 }, null, null, null);

        var equipments = GetViewEquipments(result);
        Assert.Equal(2, equipments.Count);
        Assert.Contains(equipments, e => e.TagNumber == "M-1");
        Assert.Contains(equipments, e => e.TagNumber == "M-2");
        Assert.DoesNotContain(equipments, e => e.TagNumber == "M-3");
    }

    [Fact]
    public async Task BulkCreate_Get_MultipleEquipmentTypes_ReturnsUnionOfBothTypes()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_MultipleEquipmentTypes_ReturnsUnionOfBothTypes));
        ctx.Equipments.Add(new Equipment { TagNumber = "T-1", Name = "T-1", EquipmentType = "Pump", IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "T-2", Name = "T-2", EquipmentType = "Valve", IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "T-3", Name = "T-3", EquipmentType = "Motor", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, new List<string> { "Pump", "Valve" }, null, null);

        var equipments = GetViewEquipments(result);
        Assert.Equal(2, equipments.Count);
        Assert.DoesNotContain(equipments, e => e.TagNumber == "T-3");
    }

    [Fact]
    public async Task BulkCreate_Get_TagSearch_MatchesPartialTagNumber()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_TagSearch_MatchesPartialTagNumber));
        ctx.Equipments.Add(new Equipment { TagNumber = "P-101-A", Name = "Pump A", EquipmentType = "Pump", IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "P-102-B", Name = "Pump B", EquipmentType = "Pump", IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "V-201-C", Name = "Valve C", EquipmentType = "Valve", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, null, null, "P-10");

        var equipments = GetViewEquipments(result);
        Assert.Equal(2, equipments.Count);
        Assert.All(equipments, e => Assert.StartsWith("P-10", e.TagNumber));
    }

    [Fact]
    public async Task BulkCreate_Get_TagSearchAlone_CountsAsAFilter_DoesNotTriggerNoFilterApplied()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_TagSearchAlone_CountsAsAFilter_DoesNotTriggerNoFilterApplied));
        ctx.Equipments.Add(new Equipment { TagNumber = "P-999", Name = "Pump", EquipmentType = "Pump", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, null, null, "P-999");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False((bool)viewResult.ViewData["NoFilterApplied"]!);
    }

    [Fact]
    public async Task BulkCreate_Get_FunctionalLocationFilter_MatchesOnlyEquipmentWithThatLocation()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_FunctionalLocationFilter_MatchesOnlyEquipmentWithThatLocation));
        var fl = new FunctionalLocation { Code = "85A07", Name = "FINISHING", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        ctx.Equipments.Add(new Equipment { TagNumber = "FL-1", Name = "FL-1", EquipmentType = "Pump", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "FL-2", Name = "FL-2", EquipmentType = "Pump", FunctionalLocationId = null, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, null, new List<int> { fl.FunctionalLocationId }, null);

        var equipments = GetViewEquipments(result);
        Assert.Single(equipments);
        Assert.Equal("FL-1", equipments[0].TagNumber);
    }

    [Fact]
    public async Task BulkCreate_Get_FunctionalLocationPickerList_IsPopulated_EvenWithNoEquipmentLinked()
    {
        // طبق بند ۸ ممیزی: نبود داده‌ی پیوند Equipment نباید باعث شود خودِ Picker خالی باشد -
        // این دو مشکل جدا هستند (Problem A در برابر Problem B).
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_FunctionalLocationPickerList_IsPopulated_EvenWithNoEquipmentLinked));
        ctx.FunctionalLocations.Add(new FunctionalLocation { Code = "85A07", Name = "FINISHING", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, null, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var pickerList = (List<PEMS.Application.DTOs.FunctionalLocationPickerItemDto>)viewResult.ViewData["FunctionalLocations"]!;
        Assert.Single(pickerList);
        var equipmentWithFlCount = (int)viewResult.ViewData["EquipmentWithFunctionalLocationCount"]!;
        Assert.Equal(0, equipmentWithFlCount);
    }

    [Fact]
    public async Task BulkCreate_Post_InactiveEquipment_SkippedAndNotCreated()
    {
        // طبق بند ۱۳ ممیزی: POST قبلاً هرگز دوباره تایید نمی‌کرد که تجهیز هنوز فعال است
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Post_InactiveEquipment_SkippedAndNotCreated));
        var active = new Equipment { TagNumber = "A-1", Name = "A-1", EquipmentType = "Pump", IsActive = true };
        var inactive = new Equipment { TagNumber = "A-2", Name = "A-2", EquipmentType = "Pump", IsActive = false };
        ctx.Equipments.AddRange(active, inactive);
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { active.EquipmentId, inactive.EquipmentId },
            "Preventive", "LEAK", "Operational", "test", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        var created = ctx.MaintenanceStrategies.ToList();
        Assert.Single(created);
        Assert.Equal(active.EquipmentId, created[0].EquipmentId);
    }

    [Fact]
    public async Task BulkCreate_Post_DuplicateEquipmentIdInSameSubmission_OnlyCreatesOneRow()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Post_DuplicateEquipmentIdInSameSubmission_OnlyCreatesOneRow));
        var equipment = new Equipment { TagNumber = "D-1", Name = "D-1", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { equipment.EquipmentId, equipment.EquipmentId },
            "Preventive", "LEAK", "Operational", "test", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task BulkCreate_Get_NoFilterAtAll_ReturnsEmptyList_NotFullFleet()
    {
        // طبق بند ۲۴ فاز ۷.۶: این محافظت عمدی است و در این ممیزی دست‌نخورده باقی ماند
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_NoFilterAtAll_ReturnsEmptyList_NotFullFleet));
        ctx.Equipments.Add(new Equipment { TagNumber = "N-1", Name = "N-1", EquipmentType = "Pump", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, null, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.True((bool)viewResult.ViewData["NoFilterApplied"]!);
        var equipments = (List<Equipment>)viewResult.ViewData["Equipments"]!;
        Assert.Empty(equipments);
    }

    [Fact]
    public async Task BulkCreate_Get_ResultCapRemainsAt200()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Get_ResultCapRemainsAt200));
        for (var i = 0; i < 250; i++)
            ctx.Equipments.Add(new Equipment { TagNumber = $"CAP-{i:D3}", Name = $"CAP-{i:D3}", EquipmentType = "Pump", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(null, new List<string> { "Pump" }, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var equipments = (List<Equipment>)viewResult.ViewData["Equipments"]!;
        Assert.Equal(200, equipments.Count);
        Assert.Equal(250, (int)viewResult.ViewData["MatchingCount"]!);
    }
}
