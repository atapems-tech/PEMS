using System.Linq;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Work Request/Work Order Scheduling &amp; Maintenance Calendar»:
/// ScheduleWorkOrderAsync دقیقاً هم‌الگو با AssignExecutorAsync (همان مرحله/مجوز/اسکوپ دیسیپلین)
/// - این تست‌ها همان ماتریس اعتبارسنجی/مجوز/گردش‌کار/Audit آن را برای زمان‌بندی پوشش می‌دهند.
/// </summary>
public class WorkOrderSchedulingTests : WorkOrderGovernanceTestBase
{
    private async Task<int> CreateAssignedAsync()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        return id;
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_valid_dates_by_authorized_planner_succeeds_and_persists()
    {
        var id = await CreateAssignedAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);
        var finish = start.AddHours(4);

        var (ok, err) = await Svc.ScheduleWorkOrderAsync(id, start, finish, DisciplineHead);
        Assert.True(ok, err);

        var wo = await Wo(id);
        Assert.Equal(start, wo.PlannedStart);
        Assert.Equal(finish, wo.PlannedFinish);
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_rejects_finish_before_or_equal_to_start()
    {
        var id = await CreateAssignedAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);

        var (ok1, _) = await Svc.ScheduleWorkOrderAsync(id, start, start, DisciplineHead);
        Assert.False(ok1);

        var (ok2, _) = await Svc.ScheduleWorkOrderAsync(id, start, start.AddHours(-1), DisciplineHead);
        Assert.False(ok2);

        var wo = await Wo(id);
        Assert.Null(wo.PlannedStart);
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_rejects_user_without_assign_authority()
    {
        var id = await CreateAssignedAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);

        var (ok, err) = await Svc.ScheduleWorkOrderAsync(id, start, start.AddHours(2), Planner);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_rejects_head_of_a_different_discipline()
    {
        var id = await CreateAssignedAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);

        var elecHead = Actor(DeptMaint, Elec, "MNT.DisciplineHead");
        var (ok, err) = await Svc.ScheduleWorkOrderAsync(id, start, start.AddHours(2), elecHead);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_rejects_when_status_is_not_Assigned()
    {
        // Open (پیش از Planning) - هنوز به مرحله‌ی Assigned نرسیده
        var id = await CreateAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);

        var (ok, err) = await Svc.ScheduleWorkOrderAsync(id, start, start.AddHours(2), DisciplineHead);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_records_an_automatic_audit_entry()
    {
        var id = await CreateAssignedAsync();
        var start = DateTime.Today.AddDays(1).AddHours(8);

        var (ok, err) = await Svc.ScheduleWorkOrderAsync(id, start, start.AddHours(4), DisciplineHead);
        Assert.True(ok, err);

        var audit = Ctx.AuditLogs.Where(a => a.EntityName == "WorkOrder" && a.EntityKeyValue == id.ToString())
            .OrderByDescending(a => a.AuditLogId).FirstOrDefault();
        Assert.NotNull(audit);
        Assert.Equal("Update", audit!.Action);
        Assert.Contains("PlannedStart", audit.ChangedFieldsSummary);
        Assert.Contains("PlannedFinish", audit.ChangedFieldsSummary);
    }
}
