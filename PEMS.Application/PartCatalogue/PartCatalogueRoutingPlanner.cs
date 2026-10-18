namespace PEMS.Application.PartCatalogue;

/// <summary>تعریف یک گام (ردیف PartCatalogueWorkflowStep) به‌شکل مستقل از EF - ورودی Planner.</summary>
public sealed record RoutingStepDef(string StepKey, int Order, string Kind, string ApproverSource, string ConditionKey);

public sealed record PlannedStep(
    string StepKey, int Order, string Kind, bool Skipped, string? SkipReason, int? AssignedEmployeeId);

/// <summary>
/// ورودی‌های حل مسیر. مدیر مستقیم/مافوق و مدیرعامل از جداول سازمانی ماژول (OrgLink / Assignment "CEO") می‌آیند؛
/// Planner خودش به DB دست نمی‌زند (تست‌پذیر).
/// </summary>
public sealed record RoutingContext(
    int RequesterEmployeeId,
    bool IsCritical,
    bool IsCapital,
    bool SectionNeedsSpareReview,
    bool IsNewManufacturer,
    string? PlanningMethodCode,
    Func<int, int?> ManagerOf,
    IReadOnlySet<int> CeoEmployeeIds,
    Func<string, IReadOnlyList<int>> PoolOf);

public sealed class RoutingResult
{
    public List<PlannedStep> Steps { get; } = new();

    /// <summary>خطاهای مسدودکننده (مثلاً «مدیر مستقیم تعریف نشده»). Submit باید رد شود - مسیر ناقص نباید ساکت رد شود.</summary>
    public List<(string Key, string StepKey)> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// منطق فلوچارت «فلوچارت.pdf» به‌صورت داده‌محور: گام‌ها به‌ترتیب، هرکدام با ConditionKey و ApproverSource.
///  • مدیر مستقیمِ درخواست‌دهنده CEO است ⇒ هر دو گام مدیریتی Skip.
///  • مدیرِ بعدی (مافوق) CEO است ⇒ گام مافوق Skip.
///  • Cr/Ca = Critical «یا» Capital ⇒ بررسی سرپرست تعمیرات.
///  • Section غیر Service/Security... ⇒ بررسی کارشناس قطعات. Manufacturer جدید ⇒ Sourcing.
///  • Planning Method = A ⇒ گام کنترل موجودی ندارد.
/// درخواست‌دهنده هرگز تأییدکننده‌ی درخواست خودش نیست (SoD): در Pool حذف می‌شود.
/// </summary>
public static class PartCatalogueRoutingPlanner
{
    public const string ErrNoDirectManager = "Pc_Route_NoDirectManager";
    public const string ErrNoApprover = "Pc_Route_NoApprover";
    public const string ErrUnknownCondition = "Pc_Route_UnknownCondition";
    public const string ErrUnknownSource = "Pc_Route_UnknownSource";

    public static bool EvaluateCondition(string conditionKey, RoutingContext ctx) => conditionKey switch
    {
        "Always" => true,
        "CriticalOrCapital" => ctx.IsCritical || ctx.IsCapital,
        "SectionNeedsSpareReview" => ctx.SectionNeedsSpareReview,
        "NewManufacturer" => ctx.IsNewManufacturer,
        "PlanningMethodNotA" => !PartCatalogueRules.IsPlanningMethodA(ctx.PlanningMethodCode),
        _ => throw new ArgumentOutOfRangeException(nameof(conditionKey), conditionKey, "Unknown routing condition")
    };

    public static RoutingResult Plan(IEnumerable<RoutingStepDef> defs, RoutingContext ctx)
    {
        var result = new RoutingResult();

        var requesterIsCeo = ctx.CeoEmployeeIds.Contains(ctx.RequesterEmployeeId);
        var directManager = ctx.ManagerOf(ctx.RequesterEmployeeId);
        var directIsCeo = directManager.HasValue && ctx.CeoEmployeeIds.Contains(directManager.Value);
        var directMissingReported = false;

        foreach (var def in defs.OrderBy(d => d.Order))
        {
            bool conditionMet;
            try { conditionMet = EvaluateCondition(def.ConditionKey, ctx); }
            catch (ArgumentOutOfRangeException)
            {
                result.Errors.Add((ErrUnknownCondition, def.StepKey));
                continue;
            }

            if (!conditionMet)
            {
                result.Steps.Add(new PlannedStep(def.StepKey, def.Order, def.Kind, true, "Condition:" + def.ConditionKey, null));
                continue;
            }

            switch (def.ApproverSource)
            {
                case "DirectManager":
                    if (requesterIsCeo)
                        result.Steps.Add(Skip(def, "RequesterIsCEO"));
                    else if (directManager == null)
                    {
                        if (!directMissingReported) { result.Errors.Add((ErrNoDirectManager, def.StepKey)); directMissingReported = true; }
                        result.Steps.Add(Skip(def, "NoDirectManager"));
                    }
                    else if (directIsCeo)
                        result.Steps.Add(Skip(def, "DirectManagerIsCEO"));
                    else if (directManager == ctx.RequesterEmployeeId)
                        result.Steps.Add(Skip(def, "SelfApprover"));
                    else
                        result.Steps.Add(new PlannedStep(def.StepKey, def.Order, def.Kind, false, null, directManager));
                    break;

                case "SuperiorManager":
                    if (requesterIsCeo)
                        result.Steps.Add(Skip(def, "RequesterIsCEO"));
                    else if (directManager == null)
                        result.Steps.Add(Skip(def, "NoDirectManager")); // خطای نبودِ مدیر مستقیم قبلاً ثبت شده
                    else if (directIsCeo)
                        result.Steps.Add(Skip(def, "DirectManagerIsCEO"));
                    else
                    {
                        var superior = ctx.ManagerOf(directManager.Value);
                        if (superior == null) result.Steps.Add(Skip(def, "NoSuperiorManager"));
                        else if (ctx.CeoEmployeeIds.Contains(superior.Value)) result.Steps.Add(Skip(def, "SuperiorIsCEO"));
                        else if (superior == ctx.RequesterEmployeeId) result.Steps.Add(Skip(def, "SelfApprover"));
                        else result.Steps.Add(new PlannedStep(def.StepKey, def.Order, def.Kind, false, null, superior));
                    }
                    break;

                case "AssignedPool":
                    var pool = ctx.PoolOf(def.StepKey).Where(e => e != ctx.RequesterEmployeeId).Distinct().ToList();
                    if (pool.Count == 0)
                    {
                        result.Errors.Add((ErrNoApprover, def.StepKey));
                        result.Steps.Add(Skip(def, "NoApprover"));
                    }
                    else
                        result.Steps.Add(new PlannedStep(def.StepKey, def.Order, def.Kind, false, null, null));
                    break;

                default:
                    result.Errors.Add((ErrUnknownSource, def.StepKey));
                    break;
            }
        }

        return result;
    }

    private static PlannedStep Skip(RoutingStepDef def, string reason) =>
        new(def.StepKey, def.Order, def.Kind, true, reason, null);
}
