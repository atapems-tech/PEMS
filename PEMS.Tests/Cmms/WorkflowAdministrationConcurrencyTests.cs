using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Workflow;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Cmms;

/// <summary>
/// Phase 4 - Concurrency Draft (WorkflowVersion.RowVersion) روی SQL Server واقعی با DbContext های مستقل. دیتابیس موقت PEMS_CONC_* (حذف در پایان).
/// نشان می‌دهد ذخیره‌ی کهنه رد می‌شود، بازنویسی بی‌صدا رخ نمی‌دهد و هیچ استثنای خام EF به کاربر نمی‌رسد.
/// </summary>
public class WorkflowAdministrationConcurrencyTests : IAsyncLifetime
{
    private readonly string _dbName = "PEMS_CONC_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";
    private static readonly WorkflowAdminActor A = new(101, "Admin A", new[] { SystemRoles.SystemAdmin });
    private static readonly WorkflowAdminActor B = new(102, "Admin B", new[] { SystemRoles.SystemAdmin });
    private int _versionId;

    private DbContextOptions<PEMSDbContext> Options() => new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection, o => o.EnableRetryOnFailure()).Options;

    private static WorkflowAdministrationService NewSvc(PEMSDbContext ctx)
        => new(ctx, new AccessControlService(ctx), new WorkflowDefinitionService(ctx, new WorkflowVersionValidator(ctx, null, null, new PemsWorkflowReferenceCatalog(ctx))), new WorkflowBindingService(ctx));

    public async Task InitializeAsync()
    {
        try
        {
            var csb = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName };
            _connection = csb.ConnectionString;
            await using var ctx = new PEMSDbContext(Options());
            await ctx.Database.MigrateAsync();
            if (!await ctx.Roles.AnyAsync(r => r.Name == SystemRoles.SystemAdmin))
            { ctx.Roles.Add(new ApplicationRole { Name = SystemRoles.SystemAdmin, NormalizedName = SystemRoles.SystemAdmin.ToUpperInvariant() }); await ctx.SaveChangesAsync(); }
            var sp = new ServiceCollection().AddSingleton(ctx).BuildServiceProvider();
            await WorkRequestWorkflowSeeder.SeedAsync(sp);
            await WorkflowManagementSeeder.SeedAsync(sp);
            var def = await ctx.WorkflowDefinitions.FirstAsync(d => d.Module == "WorkRequest");
            var src = await ctx.WorkflowVersions.FirstAsync(v => v.WorkflowDefinitionId == def.WorkflowDefinitionId);
            var r = await NewSvc(ctx).CloneVersionAsync(A, src.WorkflowVersionId, "concurrency");
            Assert.True(r.Success, r.Error);
            _versionId = r.Id!.Value;
        }
        catch { await DisposeAsync(); throw; }
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

    private static WorkflowStageEditModel Edit(WorkflowStageEditModel s, string desc) { s.Description = desc; return s; }

    [Fact]
    public async Task Stale_token_save_is_rejected_with_a_clear_message_and_never_overwrites()
    {
        await using var ca = new PEMSDbContext(Options()); await using var cb = new PEMSDbContext(Options());
        var sa = NewSvc(ca); var sb = NewSvc(cb);
        var ma = (await sa.GetDesignerAsync(A, _versionId))!; var mb = (await sb.GetDesignerAsync(B, _versionId))!;
        Assert.Equal(ma.RowVersion, mb.RowVersion); Assert.NotEmpty(ma.RowVersion);

        var stage = ma.Stages[0];
        var first = await sa.UpdateStageAsync(A, _versionId, ma.RowVersion, Edit(stage, "edited by A"));
        Assert.True(first.Success, first.Error);
        Assert.NotEqual(ma.RowVersion, first.RowVersion);                                                   // توکن تغییر کرد

        var stale = await sb.UpdateStageAsync(B, _versionId, mb.RowVersion, Edit(mb.Stages[0], "edited by B (stale)"));
        Assert.False(stale.Success); Assert.True(stale.IsConflict);
        Assert.Contains("another administrator", stale.Error!); Assert.DoesNotContain("Exception", stale.Error!); Assert.DoesNotContain("DbUpdate", stale.Error!);

        await using var chk = new PEMSDbContext(Options());
        Assert.Equal("edited by A", (await chk.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowStageId == stage.StageId)).Description);   // بدون رونویسی
        // با توکن تازه ادامه می‌یابد
        var fresh = (await sb.GetDesignerAsync(B, _versionId))!;
        Assert.True((await sb.UpdateStageAsync(B, _versionId, fresh.RowVersion, Edit(fresh.Stages[0], "edited by B (fresh)"))).Success);
    }

    [Fact]
    public async Task Stale_add_remove_validate_and_publish_are_all_rejected_consistently()
    {
        await using var ca = new PEMSDbContext(Options()); await using var cb = new PEMSDbContext(Options());
        var sa = NewSvc(ca); var sb = NewSvc(cb);
        var m = (await sa.GetDesignerAsync(A, _versionId))!;
        var stale = m.RowVersion;
        Assert.True((await sa.AddStageAsync(A, _versionId, stale, new WorkflowStageEditModel { Name = "Concurrent Stage", StageType = "Sequential", ApprovalStrategy = "AllApprovalsRequired", IsMandatory = true, ResponsibleRole = m.Stages[0].ResponsibleRole })).Success);

        Assert.True((await sb.AddStageAsync(B, _versionId, stale, new WorkflowStageEditModel { Name = "Second", StageType = "Sequential", ApprovalStrategy = "AllApprovalsRequired" })).IsConflict);
        Assert.True((await sb.RemoveTransitionAsync(B, _versionId, stale, m.Transitions[0].TransitionId)).IsConflict);
        Assert.True((await sb.ValidateAsync(B, _versionId, stale)).IsConflict);
        Assert.True((await sb.DiscardDraftAsync(B, _versionId, stale)).IsConflict);
        await using var chk = new PEMSDbContext(Options());
        Assert.Equal(m.Stages.Count + 1, await chk.WorkflowStages.CountAsync(s => s.WorkflowVersionId == _versionId));
        Assert.Equal(m.Transitions.Count, await chk.WorkflowTransitions.CountAsync(t => t.WorkflowVersionId == _versionId));
        Assert.NotEqual("Discarded", (await chk.WorkflowVersions.AsNoTracking().FirstAsync(v => v.WorkflowVersionId == _versionId)).Status);
    }

    [Fact]
    public async Task Two_simultaneous_saves_from_the_same_token_have_exactly_one_winner_20_times()
    {
        for (var i = 1; i <= 20; i++)
        {
            string token; int stageId;
            await using (var c0 = new PEMSDbContext(Options()))
            { var m0 = (await NewSvc(c0).GetDesignerAsync(A, _versionId))!; token = m0.RowVersion; stageId = m0.Stages[0].StageId; }
            var gate = new TaskCompletionSource();
            async Task<WorkflowAdminResult> One(WorkflowAdminActor who, string text)
            {
                await using var ctx = new PEMSDbContext(Options());
                var svc = NewSvc(ctx);
                var m = (await svc.GetDesignerAsync(who, _versionId))!;
                await gate.Task;
                var s = m.Stages.First(x => x.StageId == stageId); s.Description = text;
                return await svc.UpdateStageAsync(who, _versionId, token, s);
            }
            var t1 = Task.Run(() => One(A, "A" + i)); var t2 = Task.Run(() => One(B, "B" + i));
            await Task.Delay(50); gate.SetResult();
            var r = await Task.WhenAll(t1, t2);
            Assert.True(r.Count(x => x.Success) == 1, $"#{i} winners={r.Count(x => x.Success)}");
            var loser = r.Single(x => !x.Success);
            Assert.True(loser.IsConflict, $"#{i} loser error: {loser.Error}");
            await using var chk = new PEMSDbContext(Options());
            var final = (await chk.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowStageId == stageId)).Description;
            Assert.Equal(final, r.Single(x => x.Success) == r[0] ? "A" + i : "B" + i);
        }
    }
}
