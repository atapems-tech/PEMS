using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ActorResolution;
using PEMS.Application.PartCatalogue;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Workflow;

/// <summary>
/// ثابت‌ها و Factهای گردش‌کار «تأیید درخواست کاتالوگ قطعه» روی PEMS.WorkflowPlatform (مرجع نهایی مسیر تأیید).
/// مسیر قدیمی ماژول (PartCatalogueRoutingPlanner) فقط برای درخواست‌های Legacy باقی مانده و DEPRECATED است.
/// </summary>
public static class PartCatalogueWorkflow
{
    public const string Module = "PartCatalogue";
    public const string EntityType = "PartCatalogueRequest";
    public const string Context = "Approval";
    public const string DefinitionName = "Part Catalogue Request Approval";

    /// <summary>فیلدهای Fact که شرط‌های مرحله (EntryCondition) می‌توانند استفاده کنند.</summary>
    public static readonly string[] Fields =
    {
        "IsCritical", "IsCapital", "IsCriticalOrCapital", "SectionNeedsSpareReview", "IsNewManufacturer",
        "PlanningMethodCode", "PlanningMethodNotA", "RequestType", "RequesterEmployeeId", "Urgent",
        ActorContextKeys.CommodityGroup1Id, ActorContextKeys.CommodityGroup2Id
    };

    /// <summary>نگاشت ConditionKey ذخیره‌شده‌ی مرحله (PartCatalogueWorkflowStep) به عبارت شرط پلتفرم. null = همیشه.</summary>
    public static string? ConditionExpression(string? conditionKey) => conditionKey switch
    {
        PartCatalogueConditions.CriticalOrCapital => "IsCriticalOrCapital == true",
        PartCatalogueConditions.SectionNeedsSpareReview => "SectionNeedsSpareReview == true",
        PartCatalogueConditions.NewManufacturer => "IsNewManufacturer == true",
        PartCatalogueConditions.PlanningMethodNotA => "PlanningMethodNotA == true",
        _ => null
    };

    /// <summary>مجوز لازم هر مرحله: Approval ⇒ Approve؛ بقیه ⇒ Review.</summary>
    public static string PermissionAction(string kind) => kind == PartCatalogueStepKinds.Approval ? "Approve" : "Review";
}

/// <summary>Factهای کسب‌وکاری درخواست برای شرط‌ها و Resolver (Commodity Group و ...).</summary>
public class PartCatalogueWorkflowContextProvider : IWorkflowBusinessContextProvider
{
    private readonly PEMSDbContext _ctx;

    public PartCatalogueWorkflowContextProvider(PEMSDbContext ctx) => _ctx = ctx;

    public IReadOnlyCollection<string> SupportedFields => PartCatalogueWorkflow.Fields;

    public bool Supports(string module, string businessObjectType) =>
        module == PartCatalogueWorkflow.Module && businessObjectType == PartCatalogueWorkflow.EntityType;

    public async Task<IReadOnlyDictionary<string, string?>> GetFactsAsync(string module, string businessObjectType, int businessObjectId, CancellationToken ct = default)
    {
        var r = await _ctx.PartCatalogueRequests.AsNoTracking().Where(x => x.PartCatalogueRequestId == businessObjectId)
            .Select(x => new
            {
                x.IsCritical, x.IsCapital, x.IsNewManufacturer, x.RequestType, x.RequesterEmployeeId, x.Urgent,
                x.CommodityGroup1Id, x.CommodityGroup2Id,
                PlanningCode = x.PlanningMethod != null ? x.PlanningMethod.Code : null,
                SpareReview = x.Section != null ? (bool?)x.Section.RequiresSparePartsReview : null
            }).FirstOrDefaultAsync(ct);

        static string B(bool v) => v ? "true" : "false";
        var facts = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (r == null) return facts;

        facts["IsCritical"] = B(r.IsCritical);
        facts["IsCapital"] = B(r.IsCapital);
        facts["IsCriticalOrCapital"] = B(r.IsCritical || r.IsCapital);
        facts["SectionNeedsSpareReview"] = B(r.SpareReview ?? true);        // بدون Section ⇒ محافظه‌کارانه «بررسی لازم است» (مثل مسیریاب قبلی)
        facts["IsNewManufacturer"] = B(r.IsNewManufacturer);
        facts["PlanningMethodCode"] = r.PlanningCode;
        facts["PlanningMethodNotA"] = B(!PartCatalogueRules.IsPlanningMethodA(r.PlanningCode));
        facts["RequestType"] = r.RequestType;
        facts["RequesterEmployeeId"] = r.RequesterEmployeeId.ToString();
        facts["Urgent"] = B(r.Urgent);
        facts[ActorContextKeys.CommodityGroup1Id] = r.CommodityGroup1Id?.ToString();
        facts[ActorContextKeys.CommodityGroup2Id] = r.CommodityGroup2Id?.ToString();
        return facts;
    }
}

/// <summary>
/// اتصال Enterprise Actor Resolver به موتور گردش‌کار. خود موتور بازیگر حل نمی‌کند؛ این Hook نتیجه‌ی Resolver را به
/// Resolved/Skipped/Failed ترجمه می‌کند و جزئیات تشخیصی را به‌صورت JSON برای Snapshot مرحله برمی‌گرداند.
/// </summary>
public class ActorResolutionWorkflowHook : IWorkflowActorResolutionHook
{
    private readonly IEnterpriseActorResolver _resolver;

    public ActorResolutionWorkflowHook(IEnterpriseActorResolver resolver) => _resolver = resolver;

    public static string ToJson(ActorResolutionResult r) => JsonSerializer.Serialize(new
    {
        r.ResponsibilityKey, r.State, r.Strategy, r.SkipReason, r.Detail,
        // Snapshot سازمانی (فقط برای ردیابی/Audit؛ درخواست باز هرگز بی‌صدا دوباره حل نمی‌شود): مسیر، سمت‌ها، کد مشکل، زمان حل
        r.OrganizationIssue, r.RequesterPositionId, r.ResolvedPositionId, ResolutionPath = r.ResolutionPath, ResolvedAt = DateTime.Now,
        Candidates = r.Candidates.Select(c => new { c.EmployeeId, c.EmployeeName, c.Reason }),
        Selected = r.SelectedEmployeeIds,
        Excluded = r.Excluded.Select(e => new { e.EmployeeId, e.EmployeeName, e.Code, e.Detail })
    });

    public async Task<WorkflowActorResolutionOutcome> ResolveAsync(WorkflowActorResolutionContext context, CancellationToken ct = default)
    {
        var res = await _resolver.ResolveAsync(
            new ActorResolutionRequest(context.Stage.ResponsibilityKey!, context.InitiatedByEmployeeId ?? 0, context.Facts), ct);
        var json = ToJson(res);

        if (res.State == ActorResolutionStates.NotApplicable)
            return new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Skipped, res.State, Array.Empty<WorkflowActorCandidate>(), res.SkipReason, json);

        if (!res.IsUsable)
            return new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Failed, res.State, Array.Empty<WorkflowActorCandidate>(), null, json);

        var cands = res.Candidates.Select(c => new WorkflowActorCandidate(c.EmployeeId, c.Reason, res.SelectedEmployeeIds.Contains(c.EmployeeId))).ToList();
        return new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, res.State, cands, null, json);
    }
}
