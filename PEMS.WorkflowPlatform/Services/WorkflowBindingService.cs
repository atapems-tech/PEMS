using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>
/// Binding صریح و قطعی: (Module, EntityType, Context) → Definition [+ نسخه‌ی پین‌شده] در بازه‌ی زمانی مؤثر.
/// «Active» یعنی Binding فعال (IsActive) و در بازه‌ی EffectiveFrom/To. Binding های فعال هم‌پوشان برای یک
/// کلید مجاز نیستند (در ایجاد/فعال‌سازی رد می‌شوند) و اگر به هر دلیلی وجود داشته باشند، Resolve با
/// WorkflowBindingAmbiguousException شکست می‌خورد - هرگز «اولین مورد» انتخاب نمی‌شود.
/// </summary>
public class WorkflowBindingService : IWorkflowBindingService
{
    private readonly IWorkflowDbContext _db;
    public WorkflowBindingService(IWorkflowDbContext db) { _db = db; }

    private static bool Overlaps(DateTime? aFrom, DateTime? aTo, DateTime? bFrom, DateTime? bTo)
        => (aFrom ?? DateTime.MinValue) <= (bTo ?? DateTime.MaxValue) && (bFrom ?? DateTime.MinValue) <= (aTo ?? DateTime.MaxValue);

    private async Task ValidateTargetAsync(int definitionId, int? pinnedVersionId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from > to) throw new ArgumentException("EffectiveFrom باید قبل از EffectiveTo باشد.");
        var def = await _db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.WorkflowDefinitionId == definitionId, ct)
                  ?? throw new InvalidOperationException("تعریف یافت نشد.");
        if (!def.IsActive) throw new InvalidOperationException("تعریف بایگانی/غیرفعال است.");
        if (pinnedVersionId.HasValue)
        {
            var v = await _db.WorkflowVersions.AsNoTracking().FirstOrDefaultAsync(x => x.WorkflowVersionId == pinnedVersionId, ct)
                    ?? throw new InvalidOperationException("نسخه‌ی پین‌شده یافت نشد.");
            if (v.WorkflowDefinitionId != definitionId) throw new InvalidOperationException("نسخه‌ی پین‌شده متعلق به این تعریف نیست.");
            if (v.Status != WorkflowVersionStatus.Published) throw new InvalidOperationException("فقط نسخه‌ی Published قابل پین شدن است.");
        }
        else if (!await _db.WorkflowVersions.AnyAsync(v => v.WorkflowDefinitionId == definitionId && v.Status == WorkflowVersionStatus.Published, ct))
            throw new InvalidOperationException("تعریف هیچ نسخه‌ی Published ندارد.");
    }

    private async Task EnsureNoOverlapAsync(WorkflowBinding b, CancellationToken ct)
    {
        var others = await _db.WorkflowBindings.AsNoTracking()
            .Where(x => x.IsActive && x.Module == b.Module && x.EntityType == b.EntityType && x.Context == b.Context
                        && x.WorkflowBindingId != b.WorkflowBindingId).ToListAsync(ct);
        if (others.Any(o => Overlaps(o.EffectiveFrom, o.EffectiveTo, b.EffectiveFrom, b.EffectiveTo)))
            throw new WorkflowBindingAmbiguousException(
                $"Binding فعال هم‌پوشانی برای ({b.Module}, {b.EntityType}, {b.Context}) وجود دارد؛ ابتدا آن را غیرفعال یا بازه را اصلاح کنید.");
    }

    public async Task<WorkflowBinding> CreateAsync(string module, string entityType, string context, int definitionId, int? pinnedVersionId,
        DateTime? effectiveFrom, DateTime? effectiveTo, WorkflowActor actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("Module و EntityType الزامی‌اند.");
        await ValidateTargetAsync(definitionId, pinnedVersionId, effectiveFrom, effectiveTo, ct);

        var b = new WorkflowBinding
        {
            Module = module.Trim(), EntityType = entityType.Trim(), Context = (context ?? "").Trim(), WorkflowDefinitionId = definitionId,
            PinnedVersionId = pinnedVersionId, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo, IsActive = true, CreatedById = actor.UserId
        };
        await EnsureNoOverlapAsync(b, ct);
        _db.WorkflowBindings.Add(b);
        await _db.SaveChangesAsync(ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.BindingCreated, actor, definitionId, pinnedVersionId, b.WorkflowBindingId,
            current: Describe(b));
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Activated, actor, definitionId, pinnedVersionId, b.WorkflowBindingId, previous: "Inactive", current: "Active");
        await _db.SaveChangesAsync(ct);
        return b;
    }

    public async Task<WorkflowBinding> ChangeAsync(int bindingId, int? pinnedVersionId, DateTime? effectiveFrom, DateTime? effectiveTo, WorkflowActor actor, CancellationToken ct = default)
    {
        var b = await _db.WorkflowBindings.FirstOrDefaultAsync(x => x.WorkflowBindingId == bindingId, ct) ?? throw new InvalidOperationException("Binding یافت نشد.");
        var before = Describe(b);
        await ValidateTargetAsync(b.WorkflowDefinitionId, pinnedVersionId, effectiveFrom, effectiveTo, ct);
        b.PinnedVersionId = pinnedVersionId; b.EffectiveFrom = effectiveFrom; b.EffectiveTo = effectiveTo;
        if (b.IsActive) await EnsureNoOverlapAsync(b, ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.BindingChanged, actor, b.WorkflowDefinitionId, pinnedVersionId, b.WorkflowBindingId, before, Describe(b));
        await _db.SaveChangesAsync(ct);
        return b;
    }

    public async Task DeactivateAsync(int bindingId, WorkflowActor actor, CancellationToken ct = default)
    {
        var b = await _db.WorkflowBindings.FirstOrDefaultAsync(x => x.WorkflowBindingId == bindingId, ct) ?? throw new InvalidOperationException("Binding یافت نشد.");
        if (!b.IsActive) return;
        b.IsActive = false; b.DeactivatedDate = DateTime.Now;
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Deactivated, actor, b.WorkflowDefinitionId, b.PinnedVersionId, b.WorkflowBindingId, "Active", "Inactive");
        await _db.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(int bindingId, WorkflowActor actor, CancellationToken ct = default)
    {
        var b = await _db.WorkflowBindings.FirstOrDefaultAsync(x => x.WorkflowBindingId == bindingId, ct) ?? throw new InvalidOperationException("Binding یافت نشد.");
        if (b.IsActive) return;
        await ValidateTargetAsync(b.WorkflowDefinitionId, b.PinnedVersionId, b.EffectiveFrom, b.EffectiveTo, ct);
        b.IsActive = true; b.DeactivatedDate = null;
        await EnsureNoOverlapAsync(b, ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Activated, actor, b.WorkflowDefinitionId, b.PinnedVersionId, b.WorkflowBindingId, "Inactive", "Active");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<WorkflowBindingResolution?> ResolveAsync(string module, string entityType, string context, DateTime? at = null, CancellationToken ct = default)
    {
        var now = at ?? DateTime.Now;
        var ctx = context ?? "";
        var matches = (await _db.WorkflowBindings.AsNoTracking()
                .Where(b => b.IsActive && b.Module == module && b.EntityType == entityType && b.Context == ctx).ToListAsync(ct))
            .Where(b => (b.EffectiveFrom ?? DateTime.MinValue) <= now && now <= (b.EffectiveTo ?? DateTime.MaxValue)).ToList();

        if (matches.Count == 0) return null;
        if (matches.Count > 1)
            throw new WorkflowBindingAmbiguousException(
                $"چند Binding فعال برای ({module}, {entityType}, {ctx}) وجود دارد (Id: {string.Join(", ", matches.Select(m => m.WorkflowBindingId))}).");

        var binding = matches[0];
        var def = await _db.WorkflowDefinitions.AsNoTracking().FirstAsync(d => d.WorkflowDefinitionId == binding.WorkflowDefinitionId, ct);
        if (!def.IsActive) throw new InvalidOperationException($"تعریف «{def.Name}» غیرفعال است.");

        WorkflowVersion? version;
        if (binding.PinnedVersionId.HasValue)
        {
            version = await _db.WorkflowVersions.AsNoTracking().FirstOrDefaultAsync(v => v.WorkflowVersionId == binding.PinnedVersionId, ct);
            if (version == null || version.Status != WorkflowVersionStatus.Published)
                throw new InvalidOperationException($"نسخه‌ی پین‌شده‌ی Binding {binding.WorkflowBindingId} دیگر Published نیست.");
        }
        else
        {
            version = await _db.WorkflowVersions.AsNoTracking()
                .Where(v => v.WorkflowDefinitionId == def.WorkflowDefinitionId && v.Status == WorkflowVersionStatus.Published)
                .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
            if (version == null) throw new InvalidOperationException($"هیچ نسخه‌ی منتشرشده‌ای برای گردش‌کار «{def.Name}» یافت نشد.");
        }
        return new WorkflowBindingResolution(binding, def, version);
    }

    private static string Describe(WorkflowBinding b)
        => $"{b.Module}|{b.EntityType}|{b.Context} -> def {b.WorkflowDefinitionId} pin {(b.PinnedVersionId?.ToString() ?? "latest")} [{b.EffectiveFrom:yyyy-MM-dd}..{b.EffectiveTo:yyyy-MM-dd}]";
}
