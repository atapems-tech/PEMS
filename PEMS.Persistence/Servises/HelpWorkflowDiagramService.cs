using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help System — Phase 3»: پیاده‌سازی واقعیِ Workflow Graph Adapter. تمام داده
/// مستقیماً از WorkflowStage/WorkflowTransition (همان جداولی که IWorkflowRuntime واقعی خودش
/// در لحظه‌ی اجرای گردش‌کار استفاده می‌کند) خوانده می‌شود - این سرویس فقط می‌خواند، هرگز چیزی
/// در موتور گردش‌کار نمی‌نویسد یا تغییر نمی‌دهد.
///
/// برچسب دوزبانه: چون WorkflowStage.Name/WorkflowTransition.ActionType در خودِ موتور رشته‌ی
/// ساده‌ی انگلیسی هستند (طبق طراحی عمدیِ PEMS.WorkflowPlatform - موتور معنای کسب‌وکاری
/// نمی‌داند)، این‌جا یک دیکشنری کوچک و صریحِ ترجمه (فقط لایه‌ی نمایش Help) نام‌های
/// تاییدشده‌ی واقعی را به فارسی برمی‌گرداند. نامی که در دیکشنری نیست (پیام مطمئن‌ که این
/// سرویس نمی‌تواند مقدار غلط اختراع کند) عیناً همان رشته‌ی انگلیسی باقی می‌ماند - نه یک ترجمه‌ی
/// حدسی.
/// ==============================================================================
/// </summary>
public class HelpWorkflowDiagramService : IHelpWorkflowDiagramService
{
    private readonly PEMSDbContext _context;

    public HelpWorkflowDiagramService(PEMSDbContext context)
    {
        _context = context;
    }

    // طبق ممیزی مستقیمِ این فاز روی PEMSDB واقعی (۷ WorkflowDefinition موجود) - نام مرحله‌ها
    private static readonly Dictionary<string, string> StageNameFa = new()
    {
        ["Check"] = "بررسی اولیه",
        ["IDC"] = "بازبینی بین‌دیسیپلینی (IDC)",
        ["Approval"] = "تایید",
        ["Issue"] = "ابلاغ/صدور",
        ["RiskAssessment"] = "ارزیابی ریسک",
        ["SeniorApproval"] = "تایید مدیریت ارشد",
        ["Closure"] = "بستن",
        ["QAApproval"] = "تایید تضمین کیفیت",
        ["HSEReview"] = "بازبینی HSE",
        ["MaintenanceApproval"] = "تایید نگهداری‌وتعمیرات",
        ["OperationsApproval"] = "تایید عملیات",
        ["PendingResponse"] = "در انتظار پاسخ",
        ["PendingConfirmation"] = "در انتظار تایید نهایی",
        ["DisciplineReview"] = "بازبینی دیسیپلین",
        ["LeadReview"] = "بازبینی سرپرست",
        ["ReturnToVendor"] = "بازگشت رسمی به فروشنده",
    };

    private static readonly Dictionary<string, string> ActionTypeFa = new()
    {
        ["Start"] = "شروع",
        ["Approve"] = "تایید",
        ["Reject"] = "رد",
        ["Assess"] = "ارزیابی",
        ["Close"] = "بستن",
        ["Respond"] = "پاسخ",
        ["Reopen"] = "بازگشایی",
        ["Issue"] = "ابلاغ/صدور",
        ["Confirm"] = "تایید نهایی",
    };

    private static string StageLabel(string name, string lang) =>
        lang == "fa" && StageNameFa.TryGetValue(name, out var fa) ? fa : name;

    private static string ActionLabel(string actionType, string lang) =>
        lang == "fa" && ActionTypeFa.TryGetValue(actionType, out var fa) ? fa : actionType;

    public async Task<HelpVisualRenderDto?> BuildAsync(string module, string category, string languageCode)
    {
        var lang = languageCode == "fa" ? "fa" : "en";

        var definition = await _context.WorkflowDefinitions
            .Where(d => d.Module == module && d.Category == category && d.IsActive)
            .FirstOrDefaultAsync();

        if (definition == null) return null;

        var version = await _context.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId && v.Status == "Published")
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        if (version == null) return null;

        var stages = await _context.WorkflowStages
            .Where(s => s.WorkflowVersionId == version.WorkflowVersionId)
            .OrderBy(s => s.StageOrder)
            .ToListAsync();

        if (stages.Count == 0) return null;

        var transitions = await _context.WorkflowTransitions
            .Where(t => t.WorkflowVersionId == version.WorkflowVersionId)
            .ToListAsync();

        var stageIdToName = stages.ToDictionary(s => s.WorkflowStageId, s => s.Name);

        var nodes = stages.Select(s => new HelpVisualRenderNode
        {
            Id = s.Name,
            Label = StageLabel(s.Name, lang),
            IconCssClass = s.StageType == "Parallel" ? "bi-diagram-3" : "bi-arrow-right-circle"
        }).ToList();

        var edges = transitions
            // گذار شروع (FromStageId == null) صرفاً نشان می‌دهد اولین مرحله کدام است - همان
            // چیزی که ترتیب StageOrder از قبل نشان می‌دهد؛ برای وضوح دیداری تکرار نمی‌شود
            .Where(t => t.FromStageId != null)
            .Select(t => new HelpVisualRenderEdge
            {
                FromNodeId = stageIdToName.GetValueOrDefault(t.FromStageId!.Value, "?"),
                ToNodeId = t.ToStageId.HasValue ? stageIdToName.GetValueOrDefault(t.ToStageId.Value) : null,
                Label = ActionLabel(t.ActionType, lang),
                Kind = t.ActionType == "Reject" ? "reject" : (t.ToStageId == null ? "terminal" : "normal")
            })
            .ToList();

        return new HelpVisualRenderDto
        {
            VisualType = "WorkflowDiagram",
            LanguageCode = lang,
            Nodes = nodes,
            Edges = edges,
            SourceWorkflowModule = module,
            SourceWorkflowCategory = category,
            SourceWorkflowVersion = version.VersionNumber
        };
    }

    public async Task<IReadOnlyList<(string Module, string Category, string Name)>> GetAvailableWorkflowSourcesAsync()
    {
        var rows = await _context.WorkflowDefinitions
            .Where(d => d.IsActive)
            .Select(d => new { d.Module, d.Category, d.Name })
            .ToListAsync();

        return rows.Select(x => (x.Module, x.Category, x.Name)).ToList();
    }
}
