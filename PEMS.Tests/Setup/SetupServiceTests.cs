using Microsoft.Extensions.Configuration;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Setup;

/// <summary>
/// طبق «PEMS — Quick Setup &amp; Domain Import Architecture Refactoring» بند ۵/۲۳: هر آیتم
/// Setup باید یک قاعده‌ی واقعی و قطعی داشته باشد (نه یک درصد فرضی) - این تست‌ها دقیقاً همون
/// سناریوهای بند ۲۳ را می‌سنجند: دیتابیس خالی، نیمه‌پیکربندی‌شده، کامل، پیش‌نیاز غایب، پیش‌نیاز
/// برطرف‌شده.
/// </summary>
public class SetupServiceTests
{
    private static SetupService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx, string? ollamaUrl = "http://localhost:11434", string? ollamaModel = "qwen2.5:3b")
    {
        ctx = PemsDbContextFactory.CreateInMemory($"SetupSvc_{dbName}");

        var configValues = new Dictionary<string, string?>
        {
            ["Ollama:Url"] = ollamaUrl,
            ["Ollama:Model"] = ollamaModel
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        return new SetupService(ctx, configuration);
    }

    [Fact]
    public async Task GetStatusAsync_EmptyDatabase_OnlyAIConfigurationComplete()
    {
        // AI Configuration بر مبنای appsettings است، نه دیتابیس - در دیتابیس کاملاً خالی هم
        // چون Ollama:Url/Model در این تست پیش‌فرض پر است، همون یک مورد Complete می‌شود؛ ۸ مورد
        // دیگر همه به داده‌ی واقعی دیتابیس نیاز دارند که این‌جا خالی است.
        var service = CreateService(nameof(GetStatusAsync_EmptyDatabase_OnlyAIConfigurationComplete), out var ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal(9, status.TotalCount);
        Assert.Equal(1, status.CompletedCount);
        Assert.False(status.IsFullyComplete);
    }

    [Fact]
    public async Task GetStatusAsync_EmptyDatabase_OrganizationIsNotStarted_NotBlocked()
    {
        var service = CreateService(nameof(GetStatusAsync_EmptyDatabase_OrganizationIsNotStarted_NotBlocked), out var ctx);

        var status = await service.GetStatusAsync();
        var organization = status.Steps.Single(x => x.Key == "Organization");

        Assert.Equal(SetupStepStatus.NotStarted, organization.Status);
    }

    [Fact]
    public async Task GetStatusAsync_NoDepartmentOrDiscipline_EmployeesStepIsBlocked_WithBothMissingDependencies()
    {
        var service = CreateService(nameof(GetStatusAsync_NoDepartmentOrDiscipline_EmployeesStepIsBlocked_WithBothMissingDependencies), out var ctx);

        var status = await service.GetStatusAsync();
        var employees = status.Steps.Single(x => x.Key == "Employees");

        Assert.Equal(SetupStepStatus.Blocked, employees.Status);
        Assert.Contains(employees.MissingDependencies, x => x.Contains("Department"));
        Assert.Contains(employees.MissingDependencies, x => x.Contains("Discipline"));
    }

    [Fact]
    public async Task GetStatusAsync_DependencyResolved_EmployeesStepBecomesNotStarted_NotBlocked()
    {
        var service = CreateService(nameof(GetStatusAsync_DependencyResolved_EmployeesStepBecomesNotStarted_NotBlocked), out var ctx);
        ctx.Departments.Add(new Department { DepartmentId = 1, Code = "D1", Name = "Dept 1", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical", IsActive = true });
        await ctx.SaveChangesAsync();

        var status = await service.GetStatusAsync();
        var employees = status.Steps.Single(x => x.Key == "Employees");

        // پیش‌نیازها برطرف شدند، ولی هنوز هیچ Employee ای ساخته نشده - پس Blocked نیست، اما
        // Complete هم نیست.
        Assert.Equal(SetupStepStatus.NotStarted, employees.Status);
        Assert.Empty(employees.MissingDependencies);
    }

    [Fact]
    public async Task GetStatusAsync_DependencyAndDataBothPresent_EmployeesStepComplete()
    {
        var service = CreateService(nameof(GetStatusAsync_DependencyAndDataBothPresent_EmployeesStepComplete), out var ctx);
        ctx.Departments.Add(new Department { DepartmentId = 1, Code = "D1", Name = "Dept 1", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical", IsActive = true });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", DepartmentId = 1, DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var status = await service.GetStatusAsync();
        var employees = status.Steps.Single(x => x.Key == "Employees");

        Assert.Equal(SetupStepStatus.Complete, employees.Status);
    }

    [Fact]
    public async Task GetStatusAsync_NoMaintenanceGroup_TeamsStepBlocked_ActionPointsToMaintenanceMasterData()
    {
        var service = CreateService(nameof(GetStatusAsync_NoMaintenanceGroup_TeamsStepBlocked_ActionPointsToMaintenanceMasterData), out var ctx);

        var status = await service.GetStatusAsync();
        var teams = status.Steps.Single(x => x.Key == "Teams");

        Assert.Equal(SetupStepStatus.Blocked, teams.Status);
        Assert.Equal("MaintenanceMasterData", teams.ActionController);
    }

    [Fact]
    public async Task GetStatusAsync_MaterialMaster_BlockedWithoutCategoryOrUom()
    {
        var service = CreateService(nameof(GetStatusAsync_MaterialMaster_BlockedWithoutCategoryOrUom), out var ctx);

        var status = await service.GetStatusAsync();
        var material = status.Steps.Single(x => x.Key == "MaterialMaster");

        Assert.Equal(SetupStepStatus.Blocked, material.Status);
        Assert.Equal("InventoryMasterData", material.ActionController);
    }

    [Fact]
    public async Task GetStatusAsync_Permissions_CompleteAssoonAsAnyRolePermissionRowExists()
    {
        // طبق بازبینی Program.cs: PermissionService.SeedDefaultsAsync در هر استارتاپ واقعی
        // اجرا می‌شود، پس این مرحله همیشه بعد از اولین اجرا کامل است.
        var service = CreateService(nameof(GetStatusAsync_Permissions_CompleteAssoonAsAnyRolePermissionRowExists), out var ctx);
        ctx.RolePermissions.Add(new RolePermission { RoleName = "EngineeringManager", ModuleName = "EngineeringDocuments", CanView = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var status = await service.GetStatusAsync();
        var permissions = status.Steps.Single(x => x.Key == "Permissions");

        Assert.Equal(SetupStepStatus.Complete, permissions.Status);
    }

    [Fact]
    public async Task GetStatusAsync_AIConfiguration_CompleteWhenOllamaUrlAndModelConfigured()
    {
        var service = CreateService(nameof(GetStatusAsync_AIConfiguration_CompleteWhenOllamaUrlAndModelConfigured), out var ctx, "http://localhost:11434", "qwen2.5:3b");

        var status = await service.GetStatusAsync();
        var ai = status.Steps.Single(x => x.Key == "AIConfiguration");

        Assert.Equal(SetupStepStatus.Complete, ai.Status);
    }

    [Fact]
    public async Task GetStatusAsync_AIConfiguration_NotStartedWhenOllamaNotConfigured()
    {
        var service = CreateService(nameof(GetStatusAsync_AIConfiguration_NotStartedWhenOllamaNotConfigured), out var ctx, ollamaUrl: null, ollamaModel: null);

        var status = await service.GetStatusAsync();
        var ai = status.Steps.Single(x => x.Key == "AIConfiguration");

        Assert.Equal(SetupStepStatus.NotStarted, ai.Status);
    }

    [Fact]
    public async Task GetStatusAsync_FullyConfiguredDatabase_IsFullyComplete_100Percent()
    {
        var service = CreateService(nameof(GetStatusAsync_FullyConfiguredDatabase_IsFullyComplete_100Percent), out var ctx);

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "P1", IsActive = true, CreatedDate = DateTime.Now });
        ctx.Departments.Add(new Department { DepartmentId = 1, Code = "D1", Name = "Dept 1", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical", IsActive = true });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", DepartmentId = 1, DisciplineId = 1, IsActive = true });
        ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = 1, Code = "MG1", Name = "MG1", DisciplineId = 1, IsActive = true });
        ctx.Teams.Add(new Team { TeamId = 1, Code = "T1", Name = "T1", MaintenanceGroupId = 1, IsActive = true });
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now });
        ctx.MaterialCategories.Add(new MaterialCategory { MaterialCategoryId = 1, Code = "C1", Name = "C1", IsActive = true });
        ctx.MaterialUOMs.Add(new MaterialUOM { MaterialUOMId = 1, Code = "EA", Name = "Each", IsActive = true });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MI-1", Description = "Item 1", IsActive = true });
        ctx.EngineeringDocuments.Add(new EngineeringDocument { DocumentId = 1, DocumentNo = "DOC-1", Title = "Doc 1", DocumentType = "Datasheet", ProjectId = 1, IsActive = true });
        ctx.RolePermissions.Add(new RolePermission { RoleName = "EngineeringManager", ModuleName = "EngineeringDocuments", CanView = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var status = await service.GetStatusAsync();

        Assert.True(status.IsFullyComplete);
        Assert.Equal(100, status.PercentComplete);
        Assert.Equal(9, status.CompletedCount);
        Assert.All(status.Steps, s => Assert.Equal(SetupStepStatus.Complete, s.Status));
    }

    [Fact]
    public async Task GetStatusAsync_PartiallyConfigured_PercentReflectsExactRatio()
    {
        var service = CreateService(nameof(GetStatusAsync_PartiallyConfigured_PercentReflectsExactRatio), out var ctx);

        // فقط Organization و Permissions کامل می‌شوند (۲ از ۹) - AI هم به‌طور پیش‌فرض Complete
        // است (appsettings پیش‌فرض تست پر است) پس واقعاً ۳ از ۹.
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "P1", IsActive = true, CreatedDate = DateTime.Now });
        ctx.Departments.Add(new Department { DepartmentId = 1, Code = "D1", Name = "Dept 1", IsActive = true });
        ctx.RolePermissions.Add(new RolePermission { RoleName = "EngineeringManager", ModuleName = "EngineeringDocuments", CanView = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var status = await service.GetStatusAsync();

        Assert.Equal(3, status.CompletedCount);
        Assert.Equal((int)Math.Round(3 * 100.0 / 9), status.PercentComplete);
        Assert.False(status.IsFullyComplete);
    }
}
