using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Dynamic Maintenance Organization»: پوشش سرویس OrganizationStructureService -
/// تودرتویی گروه/تیم، جلوگیری از چرخه، سمت/خالی‌بودن/اشغال، خروج/جابه‌جایی Employee بین سمت‌ها،
/// و غیرفعال‌سازی سمت که تخصیص فعالش را می‌بندد (نه حذف تاریخچه).
/// </summary>
public class OrganizationStructureServiceTests
{
    private static async Task<PEMSDbContext> SeedBaseAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"OrgStructure_{dbName}");

        var discipline = new Discipline { Code = "MECH", Name = "Mechanical", IsActive = true, CreatedDate = DateTime.Now };
        ctx.Disciplines.Add(discipline);
        await ctx.SaveChangesAsync();

        var department = new Department { Code = "DEP1", Name = "Maintenance", IsActive = true, CreatedDate = DateTime.Now };
        ctx.Departments.Add(department);
        await ctx.SaveChangesAsync();

        var emp1 = new Employee { FirstName = "Ali", LastName = "Ahmadi", DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId, IsActive = true };
        var emp2 = new Employee { FirstName = "Reza", LastName = "Karimi", DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId, IsActive = true };
        ctx.Employees.AddRange(emp1, emp2);
        await ctx.SaveChangesAsync();

        return ctx;
    }

    // ============================== Maintenance Group nesting ==============================

    [Fact]
    public async Task CreateGroup_WithParent_NestsUnderParent()
    {
        var ctx = await SeedBaseAsync(nameof(CreateGroup_WithParent_NestsUnderParent));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var rootId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var childId = await service.CreateGroupAsync("ROT", "Rotating Equipment", disciplineId, rootId, null);

        var child = await ctx.MaintenanceGroups.FirstAsync(x => x.MaintenanceGroupId == childId);
        Assert.Equal(rootId, child.ParentMaintenanceGroupId);
    }

    [Fact]
    public async Task MoveGroup_UnderOwnDescendant_ThrowsCycleError()
    {
        var ctx = await SeedBaseAsync(nameof(MoveGroup_UnderOwnDescendant_ThrowsCycleError));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var rootId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var childId = await service.CreateGroupAsync("ROT", "Rotating Equipment", disciplineId, rootId, null);

        await Assert.ThrowsAsync<Exception>(() => service.MoveGroupAsync(rootId, childId));

        // ناموفق ماندن جابه‌جایی نباید داده را تغییر دهد
        var root = await ctx.MaintenanceGroups.FirstAsync(x => x.MaintenanceGroupId == rootId);
        Assert.Null(root.ParentMaintenanceGroupId);
    }

    [Fact]
    public async Task MoveGroup_UnderItself_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(MoveGroup_UnderItself_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);

        await Assert.ThrowsAsync<Exception>(() => service.MoveGroupAsync(groupId, groupId));
    }

    [Fact]
    public async Task DeactivateGroup_DoesNotCascadeToChildren()
    {
        var ctx = await SeedBaseAsync(nameof(DeactivateGroup_DoesNotCascadeToChildren));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var rootId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var childId = await service.CreateGroupAsync("ROT", "Rotating Equipment", disciplineId, rootId, null);

        await service.DeactivateGroupAsync(rootId);

        var root = await ctx.MaintenanceGroups.FirstAsync(x => x.MaintenanceGroupId == rootId);
        var child = await ctx.MaintenanceGroups.FirstAsync(x => x.MaintenanceGroupId == childId);
        Assert.False(root.IsActive);
        Assert.True(child.IsActive);
    }

    // ============================== Team nesting (sub-team / shift) ==============================

    [Fact]
    public async Task CreateTeamNode_AsShiftUnderParentTeam_InheritsGroupAndParent()
    {
        var ctx = await SeedBaseAsync(nameof(CreateTeamNode_AsShiftUnderParentTeam_InheritsGroupAndParent));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("ELEC", "Electrical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("ELEC-T1", "Electrical Maintenance Team", groupId, null, "Team", null);
        var shiftId = await service.CreateTeamNodeAsync("ELEC-T1-SA", "Shift A", groupId, teamId, "Shift", null);

        var shift = await ctx.Teams.FirstAsync(x => x.TeamId == shiftId);
        Assert.Equal(teamId, shift.ParentTeamId);
        Assert.Equal(groupId, shift.MaintenanceGroupId);
        Assert.Equal("Shift", shift.NodeKind);
    }

    [Fact]
    public async Task CreateTeamNode_ParentInDifferentGroup_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(CreateTeamNode_ParentInDifferentGroup_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var group1 = await service.CreateGroupAsync("ELEC", "Electrical Maintenance", disciplineId, null, null);
        var group2 = await service.CreateGroupAsync("INST", "Instrumentation & Control", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("ELEC-T1", "Electrical Maintenance Team", group1, null, "Team", null);

        await Assert.ThrowsAsync<Exception>(() =>
            service.CreateTeamNodeAsync("INST-SUB", "Invalid sub-team", group2, teamId, "SubTeam", null));
    }

    [Fact]
    public async Task MoveTeamNode_UnderOwnDescendant_ThrowsCycleError()
    {
        var ctx = await SeedBaseAsync(nameof(MoveTeamNode_UnderOwnDescendant_ThrowsCycleError));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var rootTeamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var childTeamId = await service.CreateTeamNodeAsync("T1-SUB", "Sub Team", groupId, rootTeamId, "SubTeam", null);

        await Assert.ThrowsAsync<Exception>(() => service.MoveTeamNodeAsync(rootTeamId, childTeamId, null));
    }

    [Fact]
    public async Task MoveTeamNode_ToRoot_ClearsParent()
    {
        var ctx = await SeedBaseAsync(nameof(MoveTeamNode_ToRoot_ClearsParent));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var rootTeamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var childTeamId = await service.CreateTeamNodeAsync("T1-SUB", "Sub Team", groupId, rootTeamId, "SubTeam", null);

        await service.MoveTeamNodeAsync(childTeamId, null, groupId);

        var child = await ctx.Teams.FirstAsync(x => x.TeamId == childTeamId);
        Assert.Null(child.ParentTeamId);
    }

    // ============================== Position / vacancy / assignment ==============================

    [Fact]
    public async Task CreatePosition_HasNoAssignment_IsVacant()
    {
        var ctx = await SeedBaseAsync(nameof(CreatePosition_HasNoAssignment_IsVacant));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician #01", null, 0);

        var details = await service.GetTeamDetailsAsync(teamId);
        var position = Assert.Single(details!.Positions);
        Assert.True(position.IsVacant);
        Assert.Null(position.CurrentAssignment);
    }

    [Fact]
    public async Task AssignEmployee_FillsPosition_NotVacantAnymore()
    {
        var ctx = await SeedBaseAsync(nameof(AssignEmployee_FillsPosition_NotVacantAnymore));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employeeId = ctx.Employees.First().EmployeeId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Team Head", null, 0);

        await service.AssignEmployeeAsync(positionId, employeeId, null);

        var details = await service.GetTeamDetailsAsync(teamId);
        var position = Assert.Single(details!.Positions);
        Assert.False(position.IsVacant);
        Assert.Equal(employeeId, position.CurrentAssignment!.EmployeeId);
    }

    [Fact]
    public async Task AssignEmployee_ToAlreadyOccupiedPosition_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(AssignEmployee_ToAlreadyOccupiedPosition_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employees = ctx.Employees.ToList();
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Team Head", null, 0);

        await service.AssignEmployeeAsync(positionId, employees[0].EmployeeId, null);

        await Assert.ThrowsAsync<Exception>(() => service.AssignEmployeeAsync(positionId, employees[1].EmployeeId, null));
    }

    [Fact]
    public async Task RemoveAssignment_LeavesPositionVacant_PreservesPositionAndHistory()
    {
        var ctx = await SeedBaseAsync(nameof(RemoveAssignment_LeavesPositionVacant_PreservesPositionAndHistory));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employeeId = ctx.Employees.First().EmployeeId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician #03", null, 0);
        await service.AssignEmployeeAsync(positionId, employeeId, null);

        await service.RemoveAssignmentAsync(positionId, "left the company");

        var position = await ctx.Positions.FirstAsync(x => x.PositionId == positionId);
        Assert.True(position.IsActive); // Position خودش حذف/غیرفعال نمی‌شود

        var assignment = await ctx.EmployeeAssignments.FirstAsync(x => x.PositionId == positionId);
        Assert.False(assignment.IsActive);
        Assert.NotNull(assignment.EndDate);
        Assert.Equal(employeeId, assignment.EmployeeId); // تاریخچه حفظ شده، رکورد حذف نشده
    }

    [Fact]
    public async Task RemoveAssignment_OnAlreadyVacantPosition_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(RemoveAssignment_OnAlreadyVacantPosition_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician #01", null, 0);

        await Assert.ThrowsAsync<Exception>(() => service.RemoveAssignmentAsync(positionId, null));
    }

    [Fact]
    public async Task MoveEmployee_BetweenPositions_VacatesSourceAndFillsTarget()
    {
        var ctx = await SeedBaseAsync(nameof(MoveEmployee_BetweenPositions_VacatesSourceAndFillsTarget));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employeeId = ctx.Employees.First().EmployeeId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamA = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var teamB = await service.CreateTeamNodeAsync("T2", "Team 2", groupId, null, "Team", null);
        var fromPositionId = await service.CreatePositionAsync(teamA, null, "Technician #01", null, 0);
        var toPositionId = await service.CreatePositionAsync(teamB, null, "Technician #01", null, 0);
        await service.AssignEmployeeAsync(fromPositionId, employeeId, null);

        await service.MoveEmployeeAsync(fromPositionId, toPositionId, "transferred");

        var fromAssignment = await ctx.EmployeeAssignments.FirstAsync(x => x.PositionId == fromPositionId);
        var toAssignment = await ctx.EmployeeAssignments.FirstAsync(x => x.PositionId == toPositionId);
        Assert.False(fromAssignment.IsActive);
        Assert.True(toAssignment.IsActive);
        Assert.Equal(employeeId, toAssignment.EmployeeId);
    }

    [Fact]
    public async Task MoveEmployee_FromVacantPosition_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(MoveEmployee_FromVacantPosition_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var fromPositionId = await service.CreatePositionAsync(teamId, null, "Technician #01", null, 0);
        var toPositionId = await service.CreatePositionAsync(teamId, null, "Technician #02", null, 0);

        await Assert.ThrowsAsync<Exception>(() => service.MoveEmployeeAsync(fromPositionId, toPositionId, null));
    }

    [Fact]
    public async Task DeactivatePosition_ClosesActiveAssignment_ButPreservesRecord()
    {
        var ctx = await SeedBaseAsync(nameof(DeactivatePosition_ClosesActiveAssignment_ButPreservesRecord));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employeeId = ctx.Employees.First().EmployeeId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician #01", null, 0);
        await service.AssignEmployeeAsync(positionId, employeeId, null);

        await service.DeactivatePositionAsync(positionId, "role removed");

        var position = await ctx.Positions.FirstAsync(x => x.PositionId == positionId);
        var assignment = await ctx.EmployeeAssignments.FirstAsync(x => x.PositionId == positionId);
        Assert.False(position.IsActive);
        Assert.False(assignment.IsActive);
        Assert.Equal(employeeId, assignment.EmployeeId); // رکورد تاریخچه حذف نشده
    }

    [Fact]
    public async Task GetTeamDetails_PositionHistory_IsChronologicalAndIncludesEndedAssignments()
    {
        // طبق «PEMS Dynamic Maintenance Organization — Phase 6B» بند ۱۷: تاریخچه‌ی کامل
        // (فعال + پایان‌یافته)، نزولی بر اساس StartDate - بدون حذف رکورد پایان‌یافته.
        var ctx = await SeedBaseAsync(nameof(GetTeamDetails_PositionHistory_IsChronologicalAndIncludesEndedAssignments));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employees = ctx.Employees.ToList();
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician #01", null, 0);

        await service.AssignEmployeeAsync(positionId, employees[0].EmployeeId, "first hire");
        await service.RemoveAssignmentAsync(positionId, "left");
        await service.AssignEmployeeAsync(positionId, employees[1].EmployeeId, "replacement");

        var details = await service.GetTeamDetailsAsync(teamId);
        var position = Assert.Single(details!.Positions);

        Assert.Equal(2, position.History.Count);
        Assert.True(position.History[0].IsActive); // جدیدترین (فعال) اول
        Assert.Equal(employees[1].EmployeeId, position.History[0].EmployeeId);
        Assert.False(position.History[1].IsActive);
        Assert.Equal(employees[0].EmployeeId, position.History[1].EmployeeId);
        Assert.NotNull(position.History[1].EndDate);
        Assert.False(position.IsVacant);
        Assert.Equal(employees[1].EmployeeId, position.CurrentAssignment!.EmployeeId);
    }

    // ============================== Phase 6C: Update Position ==============================

    [Fact]
    public async Task UpdatePosition_ChangesTitleDescriptionSortOrder_PreservesTeamAndAssignment()
    {
        var ctx = await SeedBaseAsync(nameof(UpdatePosition_ChangesTitleDescriptionSortOrder_PreservesTeamAndAssignment));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employeeId = ctx.Employees.First().EmployeeId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician", null, 0);
        await service.AssignEmployeeAsync(positionId, employeeId, null);

        await service.UpdatePositionAsync(positionId, "Senior Technician", "Updated via Phase 6C", 5);

        var position = await ctx.Positions.FirstAsync(x => x.PositionId == positionId);
        Assert.Equal("Senior Technician", position.Title);
        Assert.Equal("Updated via Phase 6C", position.Description);
        Assert.Equal(5, position.SortOrder);
        Assert.Equal(teamId, position.TeamId); // Update نباید Team را عوض کند - آن کار MovePositionAsync است

        var assignment = await ctx.EmployeeAssignments.FirstAsync(x => x.PositionId == positionId);
        Assert.True(assignment.IsActive);
        Assert.Equal(employeeId, assignment.EmployeeId); // تخصیص فعال دست‌نخورده می‌ماند
    }

    [Fact]
    public async Task UpdatePosition_EmptyTitle_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(UpdatePosition_EmptyTitle_Throws));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician", null, 0);

        await Assert.ThrowsAsync<Exception>(() => service.UpdatePositionAsync(positionId, "  ", null, 0));
    }

    [Fact]
    public async Task UpdatePosition_UnknownPosition_Throws()
    {
        var ctx = await SeedBaseAsync(nameof(UpdatePosition_UnknownPosition_Throws));
        var service = new OrganizationStructureService(ctx);
        await Assert.ThrowsAsync<Exception>(() => service.UpdatePositionAsync(999999, "X", null, 0));
    }

    // ============================== Phase 6C: Discipline on the team DTO ==============================

    [Fact]
    public async Task GetTeamDetails_PopulatesDisciplineFromMaintenanceGroup()
    {
        var ctx = await SeedBaseAsync(nameof(GetTeamDetails_PopulatesDisciplineFromMaintenanceGroup));
        var discipline = ctx.Disciplines.First();
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", discipline.DisciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);

        var details = await service.GetTeamDetailsAsync(teamId);

        Assert.Equal(discipline.DisciplineId, details!.DisciplineId);
        Assert.Equal(discipline.Name, details.DisciplineName);
    }

    [Fact]
    public async Task GetOrganizationTree_PopulatesDisciplineOnTeamNodes()
    {
        var ctx = await SeedBaseAsync(nameof(GetOrganizationTree_PopulatesDisciplineOnTeamNodes));
        var discipline = ctx.Disciplines.First();
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", discipline.DisciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);

        var tree = await service.GetOrganizationTreeAsync(discipline.DisciplineId);

        var team = tree.Single().Teams.Single(t => t.TeamId == teamId);
        Assert.Equal(discipline.DisciplineId, team.DisciplineId);
        Assert.Equal(discipline.Name, team.DisciplineName);
    }

    // ============================== Phase 6C: Concurrency ==============================

    [Fact]
    public async Task AssignEmployee_ConcurrentAttempts_OnlyOneActiveAssignmentSurvives()
    {
        // طبق بند ۱۸ سند: شبیه‌سازی «کاربر A سمت را خالی می‌بیند، کاربر B تخصیص می‌دهد، کاربر A
        // هم تخصیص می‌دهد» - سرور باید مرجع باقی بماند: تلاش دوم رد می‌شود، فقط یک تخصیص فعال
        // باقی می‌ماند، وضعیت خراب نمی‌شود.
        var ctx = await SeedBaseAsync(nameof(AssignEmployee_ConcurrentAttempts_OnlyOneActiveAssignmentSurvives));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var employees = ctx.Employees.ToList();
        var service = new OrganizationStructureService(ctx);

        var groupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team 1", groupId, null, "Team", null);
        var positionId = await service.CreatePositionAsync(teamId, null, "Technician", null, 0);

        // کاربر B با موفقیت تخصیص می‌دهد
        await service.AssignEmployeeAsync(positionId, employees[0].EmployeeId, "user B");

        // کاربر A (که هنوز سمت را خالی دیده بود) بعداً همان تلاش را می‌زند - باید رد شود
        await Assert.ThrowsAsync<Exception>(() => service.AssignEmployeeAsync(positionId, employees[1].EmployeeId, "user A - stale"));

        var activeAssignments = await ctx.EmployeeAssignments.Where(x => x.PositionId == positionId && x.IsActive).ToListAsync();
        Assert.Single(activeAssignments);
        Assert.Equal(employees[0].EmployeeId, activeAssignments[0].EmployeeId);

        var details = await service.GetTeamDetailsAsync(teamId);
        var position = details!.Positions.Single();
        Assert.False(position.IsVacant);
        Assert.Equal(employees[0].EmployeeId, position.CurrentAssignment!.EmployeeId);
    }

    // ============================== Read model ==============================

    [Fact]
    public async Task GetOrganizationTree_BuildsNestedGroupsAndTeams()
    {
        var ctx = await SeedBaseAsync(nameof(GetOrganizationTree_BuildsNestedGroupsAndTeams));
        var disciplineId = ctx.Disciplines.First().DisciplineId;
        var service = new OrganizationStructureService(ctx);

        var rootGroupId = await service.CreateGroupAsync("MECH", "Mechanical Maintenance", disciplineId, null, null);
        var subGroupId = await service.CreateGroupAsync("ROT", "Rotating Equipment", disciplineId, rootGroupId, null);
        var teamId = await service.CreateTeamNodeAsync("T1", "Team A", subGroupId, null, "Team", null);

        var tree = await service.GetOrganizationTreeAsync(disciplineId);

        var root = Assert.Single(tree);
        Assert.Equal(rootGroupId, root.MaintenanceGroupId);
        var subGroup = Assert.Single(root.Children);
        Assert.Equal(subGroupId, subGroup.MaintenanceGroupId);
        var team = Assert.Single(subGroup.Teams);
        Assert.Equal(teamId, team.TeamId);
    }
}
