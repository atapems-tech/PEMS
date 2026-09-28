using PEMS.Persistence.Services;

namespace PEMS.Tests.Cmms;

/// <summary>
/// Phase 6D: AssignExecutorAsync قبلاً فقط Employee را در برابر ResponsibleDisciplineId چک می‌کرد؛
/// شاخه‌ی teamId هیچ چکی نداشت (هر تیم فعالی از هر دیسیپلینی قابل تخصیص بود). همین‌طور
/// AddLaborEntryAsync اصلاً هیچ چک دیسیپلینی‌ای نداشت. این تست‌ها رفع این دو خلأ را پوشش می‌دهند.
/// </summary>
public class WorkOrderDisciplineScopeTests : WorkOrderGovernanceTestBase
{
    [Fact]
    public async Task AssignExecutorAsync_rejects_team_from_a_different_discipline_than_ResponsibleDisciplineId()
    {
        var id = await CreateAsync();
        await Do(id, "Approve", Creator);
        var (planned, planErr) = await Svc.SetPlanningAsync(id, Mech, Planner);
        Assert.True(planned, planErr);
        await Do(id, "Approve", Planner);

        var elecTeamId = AddTeam(Elec, "ElecCrew");
        var (ok, err) = await Svc.AssignExecutorAsync(id, elecTeamId, null, DisciplineHead);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(err));

        var wo = await Wo(id);
        Assert.Null(wo.AssignedTeamId);
    }

    [Fact]
    public async Task AssignExecutorAsync_accepts_team_from_the_same_discipline_as_ResponsibleDisciplineId()
    {
        var id = await CreateAsync();
        await Do(id, "Approve", Creator);
        var (planned, planErr) = await Svc.SetPlanningAsync(id, Mech, Planner);
        Assert.True(planned, planErr);
        await Do(id, "Approve", Planner);

        var mechTeamId = AddTeam(Mech, "MechCrew");
        var (ok, err) = await Svc.AssignExecutorAsync(id, mechTeamId, null, DisciplineHead);
        Assert.True(ok, err);

        var wo = await Wo(id);
        Assert.Equal(mechTeamId, wo.AssignedTeamId);
    }

    [Fact]
    public async Task AddLaborEntryAsync_rejects_employee_from_a_different_discipline_than_ResponsibleDisciplineId()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        await PrepareAndIssueAsync(id);

        var labor = new WorkOrderLaborService(Ctx);
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            labor.AddLaborEntryAsync(id, ElecExecutor.EmployeeId!.Value, null, DateTime.Today, 4, 0, null));
        Assert.Contains("دیسیپلین", ex.Message);
    }

    [Fact]
    public async Task AddLaborEntryAsync_rejects_team_from_a_different_discipline_than_ResponsibleDisciplineId()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        await PrepareAndIssueAsync(id);

        var elecTeamId = AddTeam(Elec, "ElecCrew2");
        var labor = new WorkOrderLaborService(Ctx);
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            labor.AddLaborEntryAsync(id, Executor.EmployeeId!.Value, elecTeamId, DateTime.Today, 4, 0, null));
        Assert.Contains("دیسیپلین", ex.Message);
    }

    [Fact]
    public async Task AddLaborEntryAsync_accepts_employee_and_team_from_the_same_discipline()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        await PrepareAndIssueAsync(id);

        var mechTeamId = AddTeam(Mech, "MechCrew2");
        var labor = new WorkOrderLaborService(Ctx);
        var laborId = await labor.AddLaborEntryAsync(id, Executor.EmployeeId!.Value, mechTeamId, DateTime.Today, 4, 0, null);
        Assert.True(laborId > 0);
    }

    [Fact]
    public async Task AddLaborEntryAsync_rejects_when_ResponsibleDisciplineId_not_yet_set()
    {
        var id = await CreateAsync();

        var labor = new WorkOrderLaborService(Ctx);
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            labor.AddLaborEntryAsync(id, Executor.EmployeeId!.Value, null, DateTime.Today, 4, 0, null));
        Assert.Contains("دیسیپلین", ex.Message);
    }
}
