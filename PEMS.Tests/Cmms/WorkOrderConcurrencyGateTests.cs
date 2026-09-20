using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Cmms;

/// <summary>
/// Phase 3B - Concurrency Gate چرخه‌عمر Work Order (هم‌ارز Work Request Concurrency Gate): SQL Server واقعی، DbContext های مستقل،
/// دیتابیس موقت PEMS_CONC_* (در پایان حذف می‌شود؛ PEMSDB دست نمی‌خورد). حالت اجباری: هر دو عملیات درست قبل از اولین SaveChanges
/// روی Barrier هم‌گام می‌شوند (بدترین حالت، بدون Sleep).
/// </summary>
public class WorkOrderConcurrencyGateTests : IAsyncLifetime
{
    private const int Iterations = 50;
    private readonly string _dbName = "PEMS_CONC_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";
    private int _mech, _equipment, _num;
    private WorkOrderActor _creatorA = null!, _creatorB = null!;

    private sealed class BarrierInterceptor : SaveChangesInterceptor
    {
        private readonly Barrier _barrier;
        private int _armed = 1;
        public BarrierInterceptor(Barrier barrier) { _barrier = barrier; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
                if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Race barrier timed out: the two operations did not reach the critical point together.");
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private DbContextOptions<PEMSDbContext> Options(IInterceptor? interceptor = null)
    {
        var b = new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection);
        if (interceptor != null) b.AddInterceptors(interceptor);
        return b.Options;
    }

    public async Task InitializeAsync()
    {
        try { await InitializeCoreAsync(); }
        catch { await DisposeAsync(); throw; }
    }

    private async Task InitializeCoreAsync()
    {
        var csb = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName };
        _connection = csb.ConnectionString;
        await using var ctx = new PEMSDbContext(Options());
        await ctx.Database.MigrateAsync();

        ctx.Departments.Add(new Department { Name = "Maintenance", Code = "MNT" });
        ctx.Disciplines.Add(new Discipline { Name = "Mechanical", Code = "MECH", IsActive = true });
        ctx.Projects.Add(new Project { ProjectCode = "CONC", ProjectName = "Concurrency" });
        await ctx.SaveChangesAsync();
        var dept = await ctx.Departments.FirstAsync(); var disc = await ctx.Disciplines.FirstAsync(); var prj = await ctx.Projects.FirstAsync();
        _mech = disc.DisciplineId;
        var a = new Employee { FirstName = "Conc", LastName = "A", DepartmentId = dept.DepartmentId, DisciplineId = _mech, IsActive = true };
        var b = new Employee { FirstName = "Conc", LastName = "B", DepartmentId = dept.DepartmentId, DisciplineId = _mech, IsActive = true };
        ctx.Employees.AddRange(a, b);
        ctx.Equipments.Add(new Equipment { TagNumber = "CONC-101", Name = "Pump", DisciplineId = _mech, ProjectId = prj.ProjectId, IsActive = true });
        await ctx.SaveChangesAsync();
        _equipment = (await ctx.Equipments.FirstAsync()).EquipmentId;

        var sp = new ServiceCollection().AddSingleton(ctx).BuildServiceProvider();
        await WorkOrderReviewWorkflowSeeder.SeedAsync(sp);
        await WorkOrderGovernanceSeeder.SeedAsync(sp);
        await WorkOrderLifecycleWorkflowSeeder.SeedAsync(sp);
        _creatorA = new WorkOrderActor(a.EmployeeId, new[] { "MNT.PlanningExpert" });
        _creatorB = new WorkOrderActor(b.EmployeeId, new[] { "MNT.PlanningExpert" });
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    private static WorkOrderLifecycleService NewService(PEMSDbContext ctx) => new(ctx, new WorkOrderReleaseGateService(ctx));

    private record Outcome(bool Success, string? Error, Exception? Thrown);

    private async Task<Outcome[]> RaceAsync(Func<WorkOrderLifecycleService, int, Task<(bool, string?)>> op, bool forced = true)
    {
        var barrier = forced ? new Barrier(2) : null;
        var gate = new TaskCompletionSource();
        using var ready = new CountdownEvent(2);
        var n = 0;
        async Task<Outcome> One()
        {
            var idx = Interlocked.Increment(ref n);
            await using var ctx = new PEMSDbContext(Options(barrier == null ? null : new BarrierInterceptor(barrier)));
            var svc = NewService(ctx);
            ready.Signal();
            await gate.Task;
            try { var (ok, err) = await op(svc, idx); return new Outcome(ok, err, null); }
            catch (Exception ex) { return new Outcome(false, null, ex); }
        }
        var t1 = Task.Run(One); var t2 = Task.Run(One);
        await Task.Run(() => ready.Wait());
        gate.SetResult();
        return await Task.WhenAll(t1, t2);
    }

    private async Task<int> NewWoAsync()
    {
        await using var ctx = new PEMSDbContext(Options());
        var (ok, err, id) = await NewService(ctx).CreateGovernedAsync(new WorkOrderCreateRequest
        { WorkOrderNumber = "WO-C" + Interlocked.Increment(ref _num).ToString("D5"), EquipmentId = _equipment, WorkOrderType = "Preventive", Priority = "Low" }, _creatorA);
        Assert.True(ok, err);
        return id!.Value;
    }

    private record Snapshot(int Instances, int StartRows, int ApproveRows, int Histories, string Status, int BusinessRows, string? InstanceStatus);

    private async Task<Snapshot> SnapshotAsync(int woId)
    {
        await using var ctx = new PEMSDbContext(Options());
        var instances = await ctx.WorkflowInstances.Where(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(woId)).ToListAsync();
        var ids = instances.Select(i => i.WorkflowInstanceId).ToList();
        var hist = await ctx.WorkflowHistories.Where(h => ids.Contains(h.WorkflowInstanceId)).ToListAsync();
        var wo = await ctx.WorkOrders.AsNoTracking().FirstAsync(w => w.WorkOrderId == woId);
        var biz = await ctx.WorkOrderStatusHistories.CountAsync(h => h.WorkOrderId == woId);
        return new Snapshot(instances.Count, hist.Count(h => h.ActionType == "Start"), hist.Count(h => h.ActionType == "Approve"), hist.Count, wo.Status, biz, instances.FirstOrDefault()?.Status);
    }

    private static void AssertNoRawExceptions(Outcome[] r, string msg)
    {
        foreach (var o in r)
        {
            Assert.True(o.Thrown == null, msg + " thrown=" + o.Thrown);
            if (!o.Success)
            {
                Assert.False(string.IsNullOrWhiteSpace(o.Error), msg);
                Assert.DoesNotContain("SqlException", o.Error!); Assert.DoesNotContain("DbUpdate", o.Error!); Assert.DoesNotContain("Microsoft.", o.Error!);
            }
        }
    }

    private async Task RunTransitionRaceAsync(Func<int, WorkOrderActor> actorFor, string mode)
    {
        var failures = new List<string>();
        var losers = new HashSet<string?>();
        for (var i = 1; i <= Iterations; i++)
        {
            var id = await NewWoAsync();
            var r = await RaceAsync((svc, idx) => svc.ExecuteWorkflowActionAsync(id, "Approve", null, actorFor(idx), null));
            var s = await SnapshotAsync(id);
            try
            {
                Assert.True(r.Count(x => x.Success) == 1, $"successes={r.Count(x => x.Success)}");
                Assert.True(s.Instances == 1 && s.StartRows == 1, $"instances={s.Instances} start={s.StartRows}");
                Assert.True(s.ApproveRows == 1 && s.Histories == 2, $"approve={s.ApproveRows} hist={s.Histories}");        // Start + دقیقاً یک Approve
                Assert.True(s.BusinessRows == 1 || s.BusinessRows == 2, $"biz={s.BusinessRows}");
                Assert.Equal("Running", s.InstanceStatus);
                AssertNoRawExceptions(r, "");
            }
            catch (Exception ex) { failures.Add($"#{i} " + ex.Message.Split('\n')[0]); }
            foreach (var o in r.Where(x => !x.Success)) losers.Add(o.Error);
        }
        Assert.True(failures.Count == 0, $"{mode}: {failures.Count}/{Iterations} violated: " + string.Join(" | ", failures.Take(5)));
        Console.WriteLine($"WO-{mode} iterations={Iterations} invariantOK={Iterations - failures.Count} loserMessages=[{string.Join(" ; ", losers)}]");
    }

    [Fact]
    public Task Identical_transition_by_two_users_applies_exactly_once_50_times()
        => RunTransitionRaceAsync(i => i == 1 ? _creatorA : _creatorB, "identical-transition");

    [Fact]
    public Task Double_submit_by_the_same_user_applies_exactly_once_50_times()
        => RunTransitionRaceAsync(_ => _creatorA, "double-submit");

    [Fact]
    public async Task Simultaneous_creates_each_produce_exactly_one_running_lifecycle_instance()
    {
        for (var i = 1; i <= Iterations; i++)
        {
            var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
            var r = await RaceAsync(async (svc, idx) =>
            {
                var (ok, err, id) = await svc.CreateGovernedAsync(new WorkOrderCreateRequest
                { WorkOrderNumber = "WO-R" + Interlocked.Increment(ref _num).ToString("D5"), EquipmentId = _equipment, WorkOrderType = "Corrective", Priority = "High" }, idx == 1 ? _creatorA : _creatorB);
                if (ok) ids.Add(id!.Value);
                return (ok, err);
            });
            Assert.True(r.All(x => x.Success && x.Thrown == null), $"#{i} " + string.Join(";", r.Select(x => x.Error ?? x.Thrown?.Message)));
            Assert.Equal(2, ids.Distinct().Count());
            foreach (var id in ids)
            {
                var s = await SnapshotAsync(id);
                Assert.True(s.Instances == 1 && s.StartRows == 1 && s.Histories == 1, $"#{i} WO {id}: instances={s.Instances} start={s.StartRows} hist={s.Histories}");
                Assert.Equal(1, s.BusinessRows);
            }
        }
    }

    [Fact]
    public async Task Concurrent_governing_of_the_same_ungoverned_work_order_starts_exactly_one_instance()
    {
        for (var i = 1; i <= 20; i++)
        {
            int id;
            await using (var ctx = new PEMSDbContext(Options()))
            {
                var w = new WorkOrder { EquipmentId = _equipment, WorkOrderNumber = "WO-G" + Interlocked.Increment(ref _num).ToString("D5"), WorkOrderType = "Preventive", Priority = "Low", Status = "Open", IsActive = true, CreatedDate = DateTime.Now.AddMinutes(1) };
                ctx.WorkOrders.Add(w); await ctx.SaveChangesAsync(); id = w.WorkOrderId;
            }
            var r = await RaceAsync(async (svc, idx) => { var (ok, err, _) = await svc.GovernPendingNewAsync(idx == 1 ? _creatorA : _creatorB); return (ok, err); });
            AssertNoRawExceptions(r, $"#{i}");
            var s = await SnapshotAsync(id);
            Assert.True(s.Instances == 1 && s.StartRows == 1 && s.Histories == 1, $"#{i} instances={s.Instances} start={s.StartRows} hist={s.Histories}");
        }
    }

    [Fact]
    public async Task Database_index_alone_prevents_duplicate_running_lifecycle_instances_for_direct_runtime_calls()
    {
        for (var i = 1; i <= 20; i++)
        {
            int id;
            await using (var ctx = new PEMSDbContext(Options()))
            {
                var w = new WorkOrder { EquipmentId = _equipment, WorkOrderNumber = "WO-D" + Interlocked.Increment(ref _num).ToString("D5"), WorkOrderType = "Preventive", Priority = "Low", Status = "Open", IsActive = true };
                ctx.WorkOrders.Add(w); await ctx.SaveChangesAsync(); id = w.WorkOrderId;
            }
            var barrier = new Barrier(2);
            var results = new System.Collections.Concurrent.ConcurrentBag<Exception?>();
            async Task One()
            {
                await using var ctx = new PEMSDbContext(Options(new BarrierInterceptor(barrier)));
                var rt = new WorkflowRuntimeService(ctx);
                try { await rt.StartInstanceAsync(WorkOrderWorkflow.Module, WorkOrderWorkflow.LifecycleEntityType, id, WorkOrderWorkflow.LifecycleContext, null, 1); results.Add(null); }
                catch (Exception ex) { results.Add(ex); }
            }
            await Task.WhenAll(Task.Run(One), Task.Run(One));
            var s = await SnapshotAsync(id);
            Assert.True(s.Instances == 1 && s.Histories == 1, $"#{i} instances={s.Instances} histories={s.Histories}");
            Assert.Equal(1, results.Count(x => x == null));
            Assert.IsType<WorkflowConcurrencyConflictException>(results.Single(x => x != null));
        }
    }

    [Fact]
    public async Task A_loser_after_a_completed_winner_gets_a_clean_bilingual_state_message()
    {
        var id = await NewWoAsync();
        await using var c1 = new PEMSDbContext(Options()); await using var c2 = new PEMSDbContext(Options());
        var s1 = NewService(c1); var s2 = NewService(c2);
        Assert.True((await s1.ExecuteWorkflowActionAsync(id, "Approve", null, _creatorA, null)).Success);
        var (ok, err) = await s2.ExecuteWorkflowActionAsync(id, "Approve", null, _creatorB, null);   // Creator دیگر در مرحله‌ی Planning اختیار ندارد
        Assert.False(ok); Assert.False(string.IsNullOrWhiteSpace(err));
        Assert.DoesNotContain("Exception", err!);
        var s = await SnapshotAsync(id);
        Assert.Equal(1, s.ApproveRows);
    }

    [Fact]
    public async Task Governed_operations_work_under_the_production_retrying_execution_strategy()
    {
        // Program.cs: EnableRetryOnFailure() - تراکنش کاربر بیرون از Execution Strategy در برنامه‌ی واقعی خطا می‌دهد (در InMemory دیده نمی‌شود).
        var opts = new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection, o => o.EnableRetryOnFailure()).Options;
        await using var ctx = new PEMSDbContext(opts);
        var svc = NewService(ctx);
        var (ok, err, id) = await svc.CreateGovernedAsync(new WorkOrderCreateRequest { WorkOrderNumber = "WO-RS1", EquipmentId = _equipment, WorkOrderType = "Preventive", Priority = "Low" }, _creatorA);
        Assert.True(ok, err);
        var (ok2, err2) = await svc.ExecuteWorkflowActionAsync(id!.Value, "Approve", null, _creatorA, null);
        Assert.True(ok2, err2);
        ctx.WorkOrders.Add(new WorkOrder { EquipmentId = _equipment, WorkOrderNumber = "WO-RS2", WorkOrderType = "Preventive", Priority = "Low", Status = "Open", IsActive = true, CreatedDate = DateTime.Now.AddMinutes(1) });
        await ctx.SaveChangesAsync();
        var (gok, gerr, count) = await svc.GovernPendingNewAsync(_creatorA);
        Assert.True(gok, gerr); Assert.Equal(1, count);

        var wr = new WorkRequest { RequestNumber = "WR-RS", Title = "t", RequestedById = _creatorA.EmployeeId!.Value, EquipmentId = _equipment, Priority = "Low", Status = "Planned", IsActive = true };
        ctx.WorkRequests.Add(wr); await ctx.SaveChangesAsync();
        var (cok, cerr, woId) = await new WorkRequestService(ctx, new FakeNumbering(), null!).ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "t");
        Assert.True(cok, cerr);
        Assert.Equal(1, (await SnapshotAsync(woId!.Value)).Instances);
    }

    private sealed class FakeNumbering : PEMS.Application.Interfaces.INumberingService
    {
        private int _n;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) => Task.FromResult($"{documentType}-RS{++_n:D4}");
    }
}
