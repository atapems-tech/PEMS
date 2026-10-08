namespace PEMS.Application.Organization;

/// <summary>کدهای تشخیصی ساختار سازمانی (مشترک بین Validator، Resolver، Import و صفحه‌ی Diagnostics).</summary>
public static class OrganizationIssueCodes
{
    public const string SelfParent = "SELF_PARENT";
    public const string HierarchyCycle = "HIERARCHY_CYCLE";
    public const string ParentPositionInactive = "PARENT_POSITION_INACTIVE";
    public const string ParentPositionVacant = "PARENT_POSITION_VACANT";
    public const string HolderEmployeeInactive = "HOLDER_EMPLOYEE_INACTIVE";
    public const string EmployeeNoPrimaryPosition = "EMPLOYEE_NO_PRIMARY_POSITION";
    public const string MultiplePrimaryPositions = "MULTIPLE_PRIMARY_POSITIONS";
    public const string PrimaryPositionInactive = "PRIMARY_POSITION_INACTIVE";
    public const string AssignmentExpired = "ASSIGNMENT_EXPIRED";
    public const string AssignmentNotYetEffective = "ASSIGNMENT_NOT_YET_EFFECTIVE";
    public const string RootPosition = "ROOT_POSITION_NO_PARENT";
    public const string PositionWithoutDepartment = "POSITION_WITHOUT_DEPARTMENT";
    public const string DepartmentInactive = "POSITION_DEPARTMENT_INACTIVE";
    public const string SelfManager = "HOLDER_IS_REQUESTER";
    /// <summary>بیش از یک دارنده‌ی هم‌زمانِ معتبر برای یک سمت (قاعده‌ی کسب‌وکار: یک دارنده؛ ایندکس یکتا نیز مانع است، این بررسی برای داده‌ی واردشده بدون ایندکس/قدیمی است).</summary>
    public const string MultipleActiveHolders = "MULTIPLE_ACTIVE_HOLDERS";
}

public sealed record OrganizationIssue(string Severity, string Code, string Subject, int? PositionId, int? EmployeeId, string Message);

/// <summary>
/// اعتبارسنجی مرکزی ساختار سازمانی؛ قابل‌استفاده در مدیریت سازمان، Resolver گردش‌کار، Diagnostics، Import و تست.
/// فقط گزارش می‌دهد (هرگز داده را اصلاح یا حذف نمی‌کند).
/// </summary>
public interface IOrganizationValidationService
{
    /// <summary>همه‌ی مشکلات ساختار (سمت‌ها، سلسله‌مراتب، تخصیص‌ها). P1 = جلوگیری از تعیین مدیر؛ P2 = هشدار؛ P3 = اطلاعات.</summary>
    Task<IReadOnlyList<OrganizationIssue>> ValidateAllAsync(CancellationToken ct = default);

    /// <summary>پیش از تغییر سمت مافوق: null = مجاز؛ وگرنه کد مشکل (SELF_PARENT / HIERARCHY_CYCLE / PARENT_POSITION_INACTIVE).</summary>
    Task<string?> ValidateParentChangeAsync(int positionId, int? newParentPositionId, CancellationToken ct = default);
}

public sealed record OrganizationImportRowResult(int Row, string Status, string? Message);

public sealed class OrganizationImportResult
{
    public bool Committed { get; set; }
    public int PositionsCreated { get; set; }
    public int PositionsUpdated { get; set; }
    public int AssignmentsCreated { get; set; }
    public int AssignmentsUpdated { get; set; }
    public int Unchanged { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

public interface IOrganizationImportService
{
    /// <summary>
    /// Import کنترل‌شده‌ی ساختار سازمانی (Position/ParentPosition/Holder/تاریخ‌ها). کل فایل پیش از ذخیره اعتبارسنجی می‌شود؛ هر خطا ⇒ هیچ چیزی ذخیره نمی‌شود.
    /// سلسله‌مراتب از روی داده‌ی فایل ساخته می‌شود؛ هیچ مدیری از داده‌ی موجود حدس زده نمی‌شود.
    /// </summary>
    Task<OrganizationImportResult> ImportAsync(Stream xlsx, bool commit, CancellationToken ct = default);

    /// <summary>قالب خالی + برگه‌های مرجع (دپارتمان‌ها، کارمندان با کد پرسنلی، سمت‌های موجود) برای تهیه‌ی فایل از منبع مجاز. هیچ سطر داده‌ای پیش‌فرض نمی‌شود.</summary>
    Task<byte[]> BuildTemplateAsync(CancellationToken ct = default);
}
