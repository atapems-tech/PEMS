using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Services;

namespace PEMS.Tests.Cmms;

/// <summary>SCRUM-819: the permit list operation (formerly PermitController.Index) takes the waiting permits from the workflow runtime first, longest waiting first, and reads only those rows (ported from PermitGridPaginationTests).</summary>
public class PermitPendingApprovalOperationTests
{
    private static int _next = 1;

    private static Permit NewPermit(string status = "Draft", bool active = true) => new()
    {
        PermitId = _next++, PermitNumber = $"PTW-{_next:D4}", PermitType = "Cold Work", Title = "Test Permit", RequestedByEmployeeId = 0,
        Status = status, Priority = "Medium", CreatedDate = DateTime.Today, IsActive = active
    };

    private static (PermitOperations Operations, PEMS.Persistence.Context.PEMSDbContext Ctx, FakeWorkflowRuntime Runtime) Build(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("PermitPending_" + name);
        var runtime = new FakeWorkflowRuntime();
        var operations = new PermitOperations(new PermitService(ctx, null!, null!, runtime), null!, runtime, null!, null!, ctx);
        return (operations, ctx, runtime);
    }

    [Fact]
    public async Task Only_the_waiting_permits_are_listed_not_a_page_of_all_of_them()
    {
        var (operations, ctx, runtime) = Build(nameof(Only_the_waiting_permits_are_listed_not_a_page_of_all_of_them));
        var all = Enumerable.Range(0, 30).Select(_ => NewPermit()).ToList();
        ctx.Permits.AddRange(all);
        await ctx.SaveChangesAsync();
        runtime.PendingPermitIds = all.Where((_, i) => i % 2 == 0).Select(p => p.PermitId).ToHashSet();

        var rows = await operations.PermitsAsync(["Viewer"], 1, null, pendingMyApproval: true);

        Assert.Equal(15, rows.Count);
        Assert.All(rows, r => Assert.Contains(r.PermitId, runtime.PendingPermitIds));
    }

    [Fact]
    public async Task Waiting_permits_come_longest_waiting_first_and_the_order_is_stable_for_the_same_date()
    {
        var (operations, ctx, runtime) = Build(nameof(Waiting_permits_come_longest_waiting_first_and_the_order_is_stable_for_the_same_date));
        var permits = Enumerable.Range(0, 6).Select(_ => NewPermit()).ToList();
        ctx.Permits.AddRange(permits);
        await ctx.SaveChangesAsync();
        runtime.PendingPermitIds = permits.Select(p => p.PermitId).ToHashSet();
        var sameDate = DateTime.Today;
        runtime.ExplicitAssignedDates = permits.ToDictionary(p => p.PermitId, _ => sameDate);

        var first = (await operations.PermitsAsync(["Viewer"], 1, null, true)).Select(r => r.PermitId).ToList();
        var second = (await operations.PermitsAsync(["Viewer"], 1, null, true)).Select(r => r.PermitId).ToList();

        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(x => x), first);                                      // the same date: ordered by the permit id
    }

    [Fact]
    public async Task Nobody_waiting_or_no_employee_gives_an_empty_list_and_the_status_filter_still_applies()
    {
        var (operations, ctx, runtime) = Build(nameof(Nobody_waiting_or_no_employee_gives_an_empty_list_and_the_status_filter_still_applies));
        var draft = NewPermit("Draft"); var active = NewPermit("Active"); var inactive = NewPermit("Draft", active: false);
        ctx.Permits.AddRange(draft, active, inactive);
        await ctx.SaveChangesAsync();

        Assert.Empty(await operations.PermitsAsync(["Viewer"], 1, null, true));          // nothing waits
        Assert.Empty(await operations.PermitsAsync(["Viewer"], null, null, true));       // no employee for the user
        runtime.PendingPermitIds = [draft.PermitId, active.PermitId, inactive.PermitId];
        var onlyDraft = await operations.PermitsAsync(["Viewer"], 1, "Draft", true);
        Assert.Equal([draft.PermitId], onlyDraft.Select(r => r.PermitId));              // status filter applies and an inactive permit is excluded
    }
}
