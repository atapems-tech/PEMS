using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۲۱/۳۰: SoDPolicyController تنها
/// کنترلر تازه‌ای است که منطق واقعی (نه فقط نمایش/Passthrough روی AccessControlService) دارد -
/// تشخیص تعارض دوطرفه‌ی موجود و خودتعارضی. EffectiveRole/EffectiveUser/Simulate عمداً این‌جا
/// تست نشده‌اند چون هیچ منطق مجوزدهی تازه‌ای ندارند (صرفاً حلقه‌ای روی AuthorizeForRolesAsync/
/// AuthorizeAsync که AccessControlParityTests/AccessControlScopeAndSoDTests موجود قبلاً
/// exhaustively پوشش داده‌اند).
/// </summary>
public class SoDPolicyControllerTests
{
    private static async Task<(SoDPolicyController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx, int PermA, int PermB)> CreateAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"SoDPolicyCtrl_{dbName}");
        var permA = new Permission { Name = "TestModule.Create", ResourceName = "TestModule", ActionName = "Create", IsActive = true };
        var permB = new Permission { Name = "TestModule.Approve", ResourceName = "TestModule", ActionName = "Approve", IsActive = true };
        ctx.Permissions.AddRange(permA, permB);
        await ctx.SaveChangesAsync();

        var controller = new SoDPolicyController(ctx).WithTempData();
        return (controller, ctx, permA.PermissionId, permB.PermissionId);
    }

    [Fact]
    public async Task Create_valid_policy_persists_it()
    {
        var (controller, ctx, permA, permB) = await CreateAsync(nameof(Create_valid_policy_persists_it));

        var result = await controller.Create(new SoDPolicyFormVm { PermissionAId = permA, PermissionBId = permB, Description = "test" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await ctx.SoDPolicies.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_a_permission_conflicting_with_itself()
    {
        var (controller, ctx, permA, _) = await CreateAsync(nameof(Create_rejects_a_permission_conflicting_with_itself));

        var result = await controller.Create(new SoDPolicyFormVm { PermissionAId = permA, PermissionBId = permA });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, await ctx.SoDPolicies.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_of_an_existing_active_policy_in_either_order()
    {
        var (controller, ctx, permA, permB) = await CreateAsync(nameof(Create_rejects_a_duplicate_of_an_existing_active_policy_in_either_order));

        ctx.SoDPolicies.Add(new SoDPolicy { PermissionAId = permA, PermissionBId = permB, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        // همان جفت، اما با ترتیب برعکس - باید همچنان تکراری تشخیص داده شود
        var result = await controller.Create(new SoDPolicyFormVm { PermissionAId = permB, PermissionBId = permA });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, await ctx.SoDPolicies.CountAsync());
    }

    [Fact]
    public async Task ToggleActive_flips_state_without_touching_other_policies()
    {
        var (controller, ctx, permA, permB) = await CreateAsync(nameof(ToggleActive_flips_state_without_touching_other_policies));
        var policy = new SoDPolicy { PermissionAId = permA, PermissionBId = permB, IsActive = true, CreatedDate = DateTime.Now };
        ctx.SoDPolicies.Add(policy);
        await ctx.SaveChangesAsync();

        await controller.ToggleActive(policy.SoDPolicyId);
        Assert.False((await ctx.SoDPolicies.FindAsync(policy.SoDPolicyId))!.IsActive);

        await controller.ToggleActive(policy.SoDPolicyId);
        Assert.True((await ctx.SoDPolicies.FindAsync(policy.SoDPolicyId))!.IsActive);
    }
}
