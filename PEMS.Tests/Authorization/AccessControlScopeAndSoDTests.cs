using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture (فاز ۲): Scope-Based Access (بخش ۱۱) و
/// Separation of Duties (بخش ۱۰) - این‌ها فقط در مسیر AuthorizeAsync (آگاه از کاربر) واقعی‌اند؛
/// AuthorizeForRolesAsync (مسیر سازگاری با PermissionService/RoleModuleActionService موجود،
/// اثبات‌شده در AccessControlParityTests) عمداً بدون Scope/SoD باقی مانده و این فایل به آن
/// دست نمی‌زند.
/// </summary>
public class AccessControlScopeAndSoDTests
{
    // طبق ممیزی: PermissionService.SeedDefaultsAsync عمداً این‌جا فراخوانی نمی‌شود - آن سرویس
    // یک باگ نهفته‌ی از قبل موجود دارد (خارج از محدوده‌ی این فاز، در گزارش مستند شده) که روی یک
    // دیتابیس کاملاً خالی (دقیقاً وضعیت InMemory تست) باعث می‌شود EngineeringDocuments/
    // MaterialRequisition اصلاً Seed نشوند. تست‌های Scope/SoD این فایل روی مکانیزم جدید
    // AccessControlService تمرکز دارند، نه روی داده‌ی Legacy - پس Permission/RolePermissionGrant
    // موردنیاز مستقیماً و به‌صورت قطعی ساخته می‌شود (GrantAsync)، مستقل از هر کوئرک Seed قدیمی.
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, AccessControlService accessControl)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        return (ctx, new AccessControlService(ctx));
    }

    private static async Task<Permission> GrantAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string roleName, string resourceName, string actionName)
    {
        var role = ctx.Roles.First(r => r.Name == roleName);

        var permission = await ctx.Permissions.FirstOrDefaultAsync(p => p.Name == $"{resourceName}.{actionName}");
        if (permission == null)
        {
            permission = new Permission { Name = $"{resourceName}.{actionName}", ResourceName = resourceName, ActionName = actionName, IsActive = true };
            ctx.Permissions.Add(permission);
            await ctx.SaveChangesAsync();
        }

        ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = role.Id, PermissionId = permission.PermissionId, IsActive = true });
        await ctx.SaveChangesAsync();

        return permission;
    }

    private static async Task<ApplicationUser> CreateUserAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string username)
    {
        var user = new ApplicationUser { UserName = username, Email = $"{username}@pems.local", FullName = username };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    // ==================== سلسله‌مراتب Department (بخش ۲) ====================

    [Fact]
    public async Task Department_ParentDepartmentId_SupportsRealHierarchy()
    {
        var (ctx, _) = await SeedAsync(nameof(Department_ParentDepartmentId_SupportsRealHierarchy));

        var parent = new Department { Code = "ENG-P2", Name = "Engineering P2" };
        ctx.Departments.Add(parent);
        await ctx.SaveChangesAsync();

        var child = new Department { Code = "MECH-P2", Name = "Mechanical P2", ParentDepartmentId = parent.DepartmentId };
        ctx.Departments.Add(child);
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Departments.Include(d => d.ParentDepartment).FirstAsync(d => d.DepartmentId == child.DepartmentId);
        Assert.NotNull(reloaded.ParentDepartment);
        Assert.Equal(parent.DepartmentId, reloaded.ParentDepartment!.DepartmentId);
    }

    // ==================== Position (بخش ۳) ====================

    [Fact]
    public async Task Position_LinksToEmployeeAndDepartment_WithoutTouchingLegacyStringField()
    {
        var (ctx, _) = await SeedAsync(nameof(Position_LinksToEmployeeAndDepartment_WithoutTouchingLegacyStringField));

        var dept = new Department { Code = "ENGP-P2", Name = "Engineering Position P2" };
        ctx.Departments.Add(dept);
        await ctx.SaveChangesAsync();

        var disc = new Discipline { Code = "MECP2", Name = "Mechanical Position P2" };
        ctx.Disciplines.Add(disc);
        await ctx.SaveChangesAsync();

        var position = new Position { Title = "Senior Mechanical Engineer", DepartmentId = dept.DepartmentId };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();

        var employee = new Employee
        {
            FirstName = "Test", LastName = "Employee",
            Position = "سمت آزاد قدیمی", // رشته‌ی آزاد دست‌نخورده
            PositionId = position.PositionId,
            DepartmentId = dept.DepartmentId,
            DisciplineId = disc.DisciplineId
        };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Employees.Include(e => e.PositionRef).FirstAsync(e => e.EmployeeId == employee.EmployeeId);
        Assert.Equal("سمت آزاد قدیمی", reloaded.Position);
        Assert.NotNull(reloaded.PositionRef);
        Assert.Equal("Senior Mechanical Engineer", reloaded.PositionRef!.Title);
    }

    // ==================== Scope-Based Access (بخش ۱۱) ====================

    [Fact]
    public async Task AuthorizeAsync_GlobalRoleAssignment_AllowsRegardlessOfContext()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_GlobalRoleAssignment_AllowsRegardlessOfContext));
        await GrantAsync(ctx, SystemRoles.DisciplineEngineer, "EngineeringDocuments", "Edit");
        var user = await CreateUserAsync(ctx, "global.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.DisciplineEngineer);

        // طبق AspNetUserRoles مستقیم = Global، بدون محدودیت Scope
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        var decision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Edit",
            Context = new Dictionary<string, string> { ["Discipline"] = "999" } // نامرتبط - نباید مهم باشد
        });

        Assert.True(decision.Allowed);
        Assert.Equal("Global", decision.MatchedScopeType);
    }

    [Fact]
    public async Task AuthorizeAsync_ScopedRoleAssignment_MatchingContext_Allows()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_ScopedRoleAssignment_MatchingContext_Allows));
        await GrantAsync(ctx, SystemRoles.DisciplineEngineer, "EngineeringDocuments", "Edit");
        var user = await CreateUserAsync(ctx, "scoped.match.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.DisciplineEngineer);

        ctx.RoleAssignments.Add(new RoleAssignment
        {
            UserId = user.Id, RoleId = role.Id, ScopeType = "Discipline", ScopeValue = "5", AssignmentType = "Direct", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var decision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Edit",
            Context = new Dictionary<string, string> { ["Discipline"] = "5" }
        });

        Assert.True(decision.Allowed);
        Assert.Equal("Discipline", decision.MatchedScopeType);
        Assert.Equal("5", decision.MatchedScopeValue);
    }

    [Fact]
    public async Task AuthorizeAsync_ScopedRoleAssignment_MismatchedContext_DeniesByScope()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_ScopedRoleAssignment_MismatchedContext_DeniesByScope));
        await GrantAsync(ctx, SystemRoles.DisciplineEngineer, "EngineeringDocuments", "Edit");
        var user = await CreateUserAsync(ctx, "scoped.mismatch.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.DisciplineEngineer);

        ctx.RoleAssignments.Add(new RoleAssignment
        {
            UserId = user.Id, RoleId = role.Id, ScopeType = "Discipline", ScopeValue = "5", AssignmentType = "Direct", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var decision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Edit",
            Context = new Dictionary<string, string> { ["Discipline"] = "7" }
        });

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedByScope, decision.Reason);
    }

    [Fact]
    public async Task AuthorizeAsync_ScopedRoleAssignment_NoContextProvided_DeniesByScope()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_ScopedRoleAssignment_NoContextProvided_DeniesByScope));
        await GrantAsync(ctx, SystemRoles.DisciplineEngineer, "EngineeringDocuments", "Edit");
        var user = await CreateUserAsync(ctx, "scoped.nocontext.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.DisciplineEngineer);

        ctx.RoleAssignments.Add(new RoleAssignment
        {
            UserId = user.Id, RoleId = role.Id, ScopeType = "Discipline", ScopeValue = "5", AssignmentType = "Direct", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var decision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Edit"
        });

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedByScope, decision.Reason);
    }

    // ==================== Separation of Duties (بخش ۱۰) ====================

    [Fact]
    public async Task AuthorizeAsync_SoDPolicy_SameUserSameResource_SecondActionDenied()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_SoDPolicy_SameUserSameResource_SecondActionDenied));
        var createPermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Create");
        var approvePermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Approve");

        var user = await CreateUserAsync(ctx, "sod.same.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        ctx.SoDPolicies.Add(new SoDPolicy
        {
            PermissionAId = createPermission.PermissionId, PermissionBId = approvePermission.PermissionId,
            Description = "سازنده‌ی درخواست نمی‌تواند همان را تایید کند", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var createDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Create", ResourceId = "MR-1001"
        });
        Assert.True(createDecision.Allowed);

        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Approve", ResourceId = "MR-1001"
        });

        Assert.False(approveDecision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedBySoD, approveDecision.Reason);
    }

    [Fact]
    public async Task AuthorizeAsync_SoDPolicy_DifferentResource_BothAllowed()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_SoDPolicy_DifferentResource_BothAllowed));
        var createPermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Create");
        var approvePermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Approve");

        var user = await CreateUserAsync(ctx, "sod.diffresource.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        ctx.SoDPolicies.Add(new SoDPolicy { PermissionAId = createPermission.PermissionId, PermissionBId = approvePermission.PermissionId, IsActive = true });
        await ctx.SaveChangesAsync();

        var createDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Create", ResourceId = "MR-2001"
        });
        Assert.True(createDecision.Allowed);

        // تاییدِ یک رکورد کاملاً متفاوت - نباید تحت‌تاثیر SoD روی MR-2001 قرار بگیرد
        var approveDecisionOtherRecord = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Approve", ResourceId = "MR-2002"
        });

        Assert.True(approveDecisionOtherRecord.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_NoSoDPolicyDefined_BothActionsAllowed()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_NoSoDPolicyDefined_BothActionsAllowed));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Create");
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Approve");

        var user = await CreateUserAsync(ctx, "sod.none.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        // عمداً هیچ SoDPolicy تعریف نشده

        var createDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Create", ResourceId = "MR-3001"
        });
        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "MaterialRequisition", ActionName = "Approve", ResourceId = "MR-3001"
        });

        Assert.True(createDecision.Allowed);
        Assert.True(approveDecision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_SoDPolicy_ReverseOrder_SecondActionDenied()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_SoDPolicy_ReverseOrder_SecondActionDenied));
        var approvePermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Approve");
        var createPermission = await GrantAsync(ctx, SystemRoles.EngineeringManager, "MaterialRequisition", "Create");

        var user = await CreateUserAsync(ctx, "sod.reverse.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        ctx.SoDPolicies.Add(new SoDPolicy
        {
            PermissionAId = approvePermission.PermissionId,
            PermissionBId = createPermission.PermissionId,
            Description = "ترتیب معکوس هم باید تحت سیاست SoD قرار گیرد",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id,
            ResourceName = "MaterialRequisition",
            ActionName = "Approve",
            ResourceId = "MR-4001"
        });
        Assert.True(approveDecision.Allowed);

        var createDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id,
            ResourceName = "MaterialRequisition",
            ActionName = "Create",
            ResourceId = "MR-4001"
        });

        Assert.False(createDecision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedBySoD, createDecision.Reason);
    }

    [Fact]
    public async Task AuthorizeAsync_CrossModuleRequest_WithoutMatchingPermission_Denied()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(AuthorizeAsync_CrossModuleRequest_WithoutMatchingPermission_Denied));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Edit");

        var user = await CreateUserAsync(ctx, "crossmodule.user");
        var role = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        var decision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id,
            ResourceName = "WorkOrder",
            ActionName = "Edit",
            ResourceId = "WO-9001",
            Context = new Dictionary<string, string> { ["Discipline"] = "5" }
        });

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedByRole, decision.Reason);
    }

    [Fact]
    public async Task SeedPhase5GovernanceAsync_CreatesDefaultSoDPolicies()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(SeedPhase5GovernanceAsync_CreatesDefaultSoDPolicies));
        var services = new ServiceCollection();
        services.AddSingleton(ctx);

        await AccessControlSeeder.SeedPhase5GovernanceAsync(services.BuildServiceProvider());

        var policies = await ctx.SoDPolicies
            .Include(x => x.PermissionA)
            .Include(x => x.PermissionB)
            .ToListAsync();

        Assert.Contains(policies, x => x.PermissionA!.ResourceName == "EngineeringDocuments" && x.PermissionA.ActionName == "Create" && x.PermissionB!.ResourceName == "EngineeringDocuments" && x.PermissionB.ActionName == "Approve");
        Assert.Contains(policies, x => x.PermissionA!.ResourceName == "WorkOrder" && x.PermissionA.ActionName == "Create" && x.PermissionB!.ResourceName == "WorkOrder" && x.PermissionB.ActionName == "Close");
    }
}
