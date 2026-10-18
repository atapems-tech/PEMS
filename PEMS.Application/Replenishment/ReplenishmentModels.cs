namespace PEMS.Application.Replenishment;

/// <summary>هویت اقدام‌کننده؛ UserId برای ثبت، EmployeeId برای تاریخچه، Roles برای مجوز.</summary>
public sealed record ReplenishmentActor(int? UserId, int? EmployeeId, IReadOnlyList<string> Roles);

/// <summary>RequestKey: کلید Idempotency (حداکثر ۶۴ نویسه)؛ اجرای تکراری با همان کلید اثری ندارد و خلاصه‌ی اجرای اول را برمی‌گرداند.</summary>
public sealed record ReplenishmentRunRequest(
    IReadOnlyCollection<int>? MaterialItemIds = null,
    IReadOnlyCollection<int>? WarehouseIds = null,
    string? RequestKey = null);

public sealed record ReplenishmentRunResult(
    bool Success, string? Error, bool Replayed, int RunId,
    int ResultsEvaluated, int Created, int Updated, int Unchanged, int Closed, int AutoReopened,
    int NotDefinitive, int NotComparableNoWarehouse, IReadOnlyList<string> NotDefinitiveSummary, bool Forbidden = false)
{
    public static ReplenishmentRunResult Failure(string error, bool forbidden = false) =>
        new(false, error, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<string>(), forbidden);
}

public sealed record ReplenishmentRunSummary(
    int RunId, string Status, DateTime StartedAt, DateTime? CompletedAt, int ResultsEvaluated, int Created, int Updated, int Unchanged,
    int Closed, int AutoReopened, int NotDefinitive, int NotComparableNoWarehouse, IReadOnlyList<string> NotDefinitiveSummary);

public sealed record ReplenishmentCommandResult(bool Success, string? Error = null, bool Conflict = false, bool NotFound = false, bool Forbidden = false);

public sealed record ReplenishmentQuery<T>(bool Authorized, T? Data);

public sealed record ReplenishmentFilter(string? Status = null, string? Search = null, bool? DefinitiveOnly = null);

public sealed class ReplenishmentListItem
{
    public int Id { get; set; }
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialDescription { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = "";
    public string Status { get; set; } = "";
    public int Revision { get; set; }
    public decimal UsableQuantity { get; set; }
    public decimal UnreservedDemand { get; set; }
    public decimal ShortageQuantity { get; set; }
    public decimal RecommendedQuantity { get; set; }
    public bool IsCurrentlyDefinitive { get; set; }
    public DateTime LastEvaluatedAt { get; set; }
}

public sealed class ReplenishmentSourceView
{
    public int SourceType { get; set; }
    /// <summary>صفر وقتی کاربر به منبع دسترسی ندارد (IsRestricted)؛ مقدار همچنان نمایش داده می‌شود.</summary>
    public int SourceLineId { get; set; }
    public string? ParentNumber { get; set; }
    public bool IsRestricted { get; set; }
    public decimal GrossRemainingQuantity { get; set; }
    public decimal CoveredByReservation { get; set; }
    public decimal UnreservedQuantity { get; set; }
}

public sealed class ReplenishmentHistoryView
{
    public int Revision { get; set; }
    public string Action { get; set; } = "";
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public decimal ShortageQuantity { get; set; }
    public decimal UsableQuantity { get; set; }
    public string? ActorName { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class ReplenishmentDetails
{
    public ReplenishmentListItem Summary { get; set; } = new();
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal GrossEligibleDemand { get; set; }
    public decimal CoveredByReservations { get; set; }
    public string? NoticeCodes { get; set; }
    public string? BlockingIssueCodes { get; set; }
    public DateTime FirstDetectedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public string? DismissedReason { get; set; }
    public decimal? DismissedShortageQuantity { get; set; }
    public string? ClosedReason { get; set; }
    public byte[]? RowVersion { get; set; }
    /// <summary>زمان محاسبه‌ی آخرین اجرا و تازگی ورودی‌ها؛ null = نامعلوم (نمایش «نامعلوم»، نه «به‌روز»).</summary>
    public DateTime? LastRunStartedAt { get; set; }
    public DateTime? LastRunCompletedAt { get; set; }
    public DateTime? StockLastUpdatedAt { get; set; }
    public DateTime? DemandLastChangedAt { get; set; }
    public bool RequiresWarehouseAttribution { get; set; }
    public List<ReplenishmentSourceView> Sources { get; set; } = new();
    public List<ReplenishmentHistoryView> History { get; set; } = new();
}
