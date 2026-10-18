using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// Digital Equipment Passport → Maintenance History باید بر پایه‌ی Work Request باشد (نه Work Order).
/// Work Order فقط به‌عنوان بخش جداگانه‌ی فقط‌خواندنی می‌آید.
/// </summary>
public class EquipmentPassportWorkRequestTests
{
    [Fact]
    public async Task Passport_MaintenanceHistory_IsWorkRequests_AndWorkOrdersStaySeparate()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(Passport_MaintenanceHistory_IsWorkRequests_AndWorkOrdersStaySeparate));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "P" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B" });
        ctx.Equipments.AddRange(
            new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 },
            new Equipment { EquipmentId = 2, TagNumber = "P-102", Name = "Other", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 });
        ctx.WorkRequests.AddRange(
            new WorkRequest { WorkRequestId = 1, RequestNumber = "WR-1", Title = "Mine", EquipmentId = 1, RequestedById = 1 },
            new WorkRequest { WorkRequestId = 2, RequestNumber = "WR-2", Title = "Other equipment", EquipmentId = 2, RequestedById = 1 },
            new WorkRequest { WorkRequestId = 3, RequestNumber = "WR-3", Title = "Inactive", EquipmentId = 1, RequestedById = 1, IsActive = false });
        ctx.WorkOrders.Add(new WorkOrder { WorkOrderId = 1, WorkOrderNumber = "WO-1", EquipmentId = 1, Status = "Open" });
        await ctx.SaveChangesAsync();

        var svc = new EquipmentService(ctx, new RelationshipService(ctx), new FunctionalLocationService(ctx));
        var passport = await svc.GetPassportAsync(1);

        Assert.NotNull(passport);
        Assert.Equal(new[] { "WR-1" }, passport!.WorkRequests.Select(x => x.RequestNumber));
        Assert.Equal(new[] { "WO-1" }, passport.WorkOrders.Select(x => x.WorkOrderNo));
    }

    [Fact]
    public void PassportView_HasNoWorkOrderCreateLink_AndUsesWorkRequestCreate()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..", ".."));
        var view = File.ReadAllText(Path.Combine(root, "PEMS.Web", "Views", "Equipment", "Details.cshtml"));
        Assert.DoesNotContain("asp-controller=\"WorkOrder\" asp-action=\"Create\"", view);
        Assert.Contains("asp-controller=\"WorkRequest\" asp-action=\"Create\"", view);
    }

    [Fact]
    public async Task Passport_ExposesStrategiesAndPlans_FromExistingEquipmentRelations()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(Passport_ExposesStrategiesAndPlans_FromExistingEquipmentRelations));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "P" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.Equipments.AddRange(
            new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 },
            new Equipment { EquipmentId = 2, TagNumber = "P-102", Name = "Other", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 });
        ctx.MaintenanceStrategies.AddRange(
            new MaintenanceStrategy { MaintenanceStrategyId = 1, EquipmentId = 1, StrategyType = "Preventive", RecommendedTriggerType = "Calendar" },
            new MaintenanceStrategy { MaintenanceStrategyId = 2, EquipmentId = 2, StrategyType = "Predictive", RecommendedTriggerType = "Meter" });
        ctx.MaintenancePlans.Add(new MaintenancePlan { MaintenancePlanId = 1, EquipmentId = 1, MaintenanceStrategyId = 1, PlanNumber = "PM-1", Title = "T", NextDueDate = DateTime.Today });
        await ctx.SaveChangesAsync();

        var svc = new EquipmentService(ctx, new RelationshipService(ctx), new FunctionalLocationService(ctx));
        var passport = await svc.GetPassportAsync(1);

        Assert.Equal(new[] { "Preventive" }, passport!.Strategies.Select(x => x.StrategyType));
        Assert.Equal(new[] { "PM-1" }, passport.Plans.Select(x => x.PlanNumber));
    }

    private static string Here([System.Runtime.CompilerServices.CallerFilePath] string p = "") => p;
}
