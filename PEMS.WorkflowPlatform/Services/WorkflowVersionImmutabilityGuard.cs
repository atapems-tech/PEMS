using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>تلاش برای تغییر ساختار یک نسخه‌ی Published/Retired گردش‌کار.</summary>
public class WorkflowImmutableVersionException : InvalidOperationException
{
    public WorkflowImmutableVersionException(string message) : base(message) { }
}

/// <summary>
/// لایه‌ی دفاعی دوم (علاوه بر سرویس‌ها): هنگام SaveChanges هر تغییری روی مراحل/گذارهای یک نسخه‌ی
/// Published/Retired، یا حذف/تغییر خود نسخه (به‌جز Published→Retired) رد می‌شود. افزودن نسخه‌ی جدید
/// (حتی مستقیم با وضعیت Published مثل Seeder ها) و ویرایش نسخه‌های Draft/Validated مجاز است.
/// نسخه‌ی قدیمی که Instance های در حال اجرا به آن پین شده‌اند هرگز دست‌کاری نمی‌شود.
/// </summary>
public static class WorkflowVersionImmutabilityGuard
{
    private static readonly HashSet<string> RetireAllowedProps = new() { nameof(WorkflowVersion.Status), nameof(WorkflowVersion.RetiredDate) };

    public static void Enforce(DbContext ctx)
    {
        var (entries, versionIds) = Collect(ctx);
        if (entries.Count == 0) return;
        var dbStatuses = versionIds.Count == 0
            ? new Dictionary<int, string>()
            : ctx.Set<WorkflowVersion>().AsNoTracking().Where(v => versionIds.Contains(v.WorkflowVersionId))
                .Select(v => new { v.WorkflowVersionId, v.Status }).ToList().ToDictionary(x => x.WorkflowVersionId, x => x.Status);
        Check(ctx, entries, dbStatuses);
    }

    public static async Task EnforceAsync(DbContext ctx, CancellationToken ct = default)
    {
        var (entries, versionIds) = Collect(ctx);
        if (entries.Count == 0) return;
        var dbStatuses = versionIds.Count == 0
            ? new Dictionary<int, string>()
            : (await ctx.Set<WorkflowVersion>().AsNoTracking().Where(v => versionIds.Contains(v.WorkflowVersionId))
                .Select(v => new { v.WorkflowVersionId, v.Status }).ToListAsync(ct)).ToDictionary(x => x.WorkflowVersionId, x => x.Status);
        Check(ctx, entries, dbStatuses);
    }

    private static (List<EntityEntry> entries, HashSet<int> untrackedVersionIds) Collect(DbContext ctx)
    {
        var entries = ctx.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && e.Entity is WorkflowVersion or WorkflowStage or WorkflowTransition)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var trackedVersions = ctx.ChangeTracker.Entries<WorkflowVersion>().Select(e => e.Entity.WorkflowVersionId).ToHashSet();
        var ids = new HashSet<int>();
        foreach (var e in entries.Where(e => e.Entity is not WorkflowVersion))
        {
            foreach (var id in VersionIdsOf(e))
                if (id > 0 && !trackedVersions.Contains(id)) ids.Add(id);
        }
        return (entries, ids);
    }

    private static IEnumerable<int> VersionIdsOf(EntityEntry e)
    {
        var cur = e.State == EntityState.Deleted ? 0 : (int)e.CurrentValues[nameof(WorkflowStage.WorkflowVersionId)]!;
        yield return cur;
        if (e.State is EntityState.Modified or EntityState.Deleted)
            yield return (int)e.OriginalValues[nameof(WorkflowStage.WorkflowVersionId)]!;
    }

    private static void Check(DbContext ctx, List<EntityEntry> entries, Dictionary<int, string> dbStatuses)
    {
        var tracked = ctx.ChangeTracker.Entries<WorkflowVersion>().ToDictionary(e => e.Entity.WorkflowVersionId);

        // وضعیت «قبل از این Save» یک نسخه؛ null = نسخه‌ی جدید (قابل‌ویرایش)
        string? StatusBefore(int versionId)
        {
            if (tracked.TryGetValue(versionId, out var te))
                return te.State == EntityState.Added ? null : (string)te.OriginalValues[nameof(WorkflowVersion.Status)]!;
            return dbStatuses.TryGetValue(versionId, out var s) ? s : null;
        }

        foreach (var e in entries)
        {
            if (e.Entity is WorkflowVersion v)
            {
                if (e.State == EntityState.Added) continue;
                var before = (string)e.OriginalValues[nameof(WorkflowVersion.Status)]!;
                if (!WorkflowVersionStatus.IsImmutable(before)) continue;

                if (e.State == EntityState.Deleted)
                    throw new WorkflowImmutableVersionException($"نسخه‌ی {before} «{v.WorkflowVersionId}» قابل حذف نیست.");

                var changed = e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
                if (changed.Count == 0) continue;
                var statusChanged = changed.Contains(nameof(WorkflowVersion.Status)) && v.Status != before;
                if (statusChanged && !(before == WorkflowVersionStatus.Published && v.Status == WorkflowVersionStatus.Retired))
                    throw new WorkflowImmutableVersionException($"نسخه‌ی {before} فقط می‌تواند به Retired برود؛ تغییر به «{v.Status}» مجاز نیست.");
                var illegal = changed.Where(c => !RetireAllowedProps.Contains(c)).ToList();
                if (illegal.Count > 0)
                    throw new WorkflowImmutableVersionException($"نسخه‌ی {before} «{v.WorkflowVersionId}» تغییرناپذیر است (فیلدهای {string.Join(", ", illegal)}).");
                continue;
            }

            foreach (var id in VersionIdsOf(e).Distinct())
            {
                if (id == 0) continue;
                var status = StatusBefore(id);
                if (WorkflowVersionStatus.IsImmutable(status))
                    throw new WorkflowImmutableVersionException(
                        $"نسخه‌ی گردش‌کار {id} در وضعیت {status} است و مراحل/گذارهای آن تغییرناپذیر است. یک Draft جدید بسازید.");
            }
        }
    }
}
