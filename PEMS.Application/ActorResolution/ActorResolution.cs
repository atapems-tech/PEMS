namespace PEMS.Application.ActorResolution;

/// <summary>وضعیت‌های تشخیصیِ حل بازیگر (جایگزین پیام عمومی «No approver»).</summary>
public static class ActorResolutionStates
{
    public const string Resolved = "RESOLVED";
    public const string MultipleEligibleActors = "MULTIPLE_ELIGIBLE_ACTORS";
    public const string NoConfiguration = "NO_CONFIGURATION";
    public const string NoEligibleActor = "NO_ELIGIBLE_ACTOR";
    public const string RequesterOnly = "REQUESTER_ONLY";
    public const string InactiveActor = "INACTIVE_ACTOR";
    public const string WrongOrganizationScope = "WRONG_ORGANIZATION_SCOPE";
    public const string WrongCommodityScope = "WRONG_COMMODITY_SCOPE";
    public const string MissingPermission = "MISSING_PERMISSION";

    /// <summary>داده‌ی ساختار سازمانی ناقص/نامعتبر است و مدیر مستقیم قابل‌تعیین نیست (Organization Resolution Required). جزئیات: ActorResolutionResult.OrganizationIssue.</summary>
    public const string OrganizationResolutionRequired = "ORGANIZATION_RESOLUTION_REQUIRED";

    /// <summary>مسئولیت برای این درخواست لازم نیست (مثلاً مدیر مستقیم، مدیرعامل است) و مرحله Skip می‌شود.</summary>
    public const string NotApplicable = "NOT_APPLICABLE";

    public static bool IsUsable(string state) => state is Resolved or MultipleEligibleActors;
}

/// <summary>کلیدهای Context که Resolver می‌شناسد (مقدارها رشته‌اند؛ منبع: IWorkflowBusinessContextProvider).</summary>
public static class ActorContextKeys
{
    public const string CommodityGroup1Id = "CommodityGroup1Id";
    public const string CommodityGroup2Id = "CommodityGroup2Id";
}

public sealed record ActorResolutionRequest(
    string ResponsibilityKey,
    int RequesterEmployeeId,
    IReadOnlyDictionary<string, string?> Context,
    DateTime? AsOf = null);

public sealed record ActorCandidate(int EmployeeId, string EmployeeName, string Reason);

public sealed record ActorExclusion(int EmployeeId, string EmployeeName, string Code, string Detail);

public sealed class ActorResolutionResult
{
    public string ResponsibilityKey { get; init; } = string.Empty;
    public string State { get; set; } = ActorResolutionStates.NoConfiguration;
    public string Strategy { get; set; } = "Queue";
    public List<ActorCandidate> Candidates { get; } = new();
    public List<int> SelectedEmployeeIds { get; } = new();
    public List<ActorExclusion> Excluded { get; } = new();

    /// <summary>برای NOT_APPLICABLE: دلیل Skip (مثل RequesterIsCEO).</summary>
    public string? SkipReason { get; set; }

    /// <summary>جزئیات فنی برای مدیر (نه کاربر عادی).</summary>
    public string? Detail { get; set; }

    // --- توضیح‌پذیری حل مدیر (ساختار سازمانی) ---
    /// <summary>برای ORGANIZATION_RESOLUTION_REQUIRED: کد مشکل (PEMS.Application.Organization.OrganizationIssueCodes).</summary>
    public string? OrganizationIssue { get; set; }
    public int? RequesterPositionId { get; set; }
    public int? ResolvedPositionId { get; set; }

    /// <summary>مسیر حل، مثل «Employee → Primary Position → Parent Position → Holder».</summary>
    public List<string> ResolutionPath { get; } = new();

    public bool IsUsable => ActorResolutionStates.IsUsable(State);
}

public sealed record ActorDataQualityFinding(string Severity, string Code, string ResponsibilityKey, string Message);

/// <summary>
/// Enterprise Actor Resolution: «چه کسی برای این مرحله و این موجودیت واجد شرایط اقدام است؟». موتور گردش‌کار نیست و
/// گردش‌کار را اجرا نمی‌کند؛ فقط توسط PEMS.WorkflowPlatform (از طریق Hook) و ابزار مدیریتی/تشخیصی مصرف می‌شود.
/// </summary>
public interface IEnterpriseActorResolver
{
    Task<ActorResolutionResult> ResolveAsync(ActorResolutionRequest request, CancellationToken ct = default);

    /// <summary>بررسی‌های کیفیت داده پیش از فعال‌سازی مسیر (مسئولیت بدون Position، Position بدون اشغال‌کننده، ...).</summary>
    Task<IReadOnlyList<ActorDataQualityFinding>> GetDataQualityAsync(CancellationToken ct = default);
}
