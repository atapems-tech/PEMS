namespace PEMS.Application.Organization;

/// <summary>نتیجه‌ی پیمایش سلسله‌مراتب: یا دارندگان معتبر، یا یک مشکل ساختاری (IssueCode)، یا «مافوق ندارد» (NoSuperior).</summary>
public sealed class ManagerChainResult
{
    public string? IssueCode { get; set; }
    public string? Detail { get; set; }
    public bool NoSuperior { get; set; }
    public int? RequesterPositionId { get; set; }
    public int? ResolvedPositionId { get; set; }
    public List<int> HolderEmployeeIds { get; } = new();
    public List<string> Path { get; } = new();
    public bool Resolved => IssueCode == null && !NoSuperior && HolderEmployeeIds.Count > 0;
}

/// <summary>زمینه‌ی سازمانی یک کارمند (فقط‌خواندنی): Department/Discipline/Domain از Employee، و سمت اصلی از EmployeeAssignment فعالِ اصلی.</summary>
public sealed record OrganizationalContext(
    int EmployeeId, bool Exists, bool IsActive,
    int? DepartmentId, int? DisciplineId, string? OrganizationDomain,
    int? PrimaryPositionId, string? PrimaryPositionTitle, int? PositionDepartmentId, int? ParentPositionId);

/// <summary>
/// مرز پایدار و نازک «حل سازمانی»؛ موتور دوم نیست و منطق را کپی نمی‌کند: به قطعات موجود
/// (OrganizationManagerChain، WorkOrderWorkflow.ResolveDepartmentDomainAsync، EmployeeAssignment) تفویض می‌کند.
/// هیچ ماژولی نباید Employee→Manager / Department→Manager / Team→Manager را خودش پیاده‌سازی کند.
/// </summary>
public interface IOrganizationResolver
{
    /// <summary>Department، Discipline، Domain و سمت اصلیِ معتبر کارمند.</summary>
    Task<OrganizationalContext> GetContextAsync(int employeeId, DateTime? asOf = null, CancellationToken ct = default);

    /// <summary>هویت سازمانی برای حاکمیت Work Order: (دامنه از سلسله‌مراتب Department، Discipline). (null,null) اگر کارمند نباشد.</summary>
    Task<(string? Domain, int? DisciplineId)> GetDomainAndDisciplineAsync(int? employeeId, CancellationToken ct = default);

    /// <summary>دامنه‌ی مؤثر یک Department (خودش یا نزدیک‌ترین والد دارای دامنه).</summary>
    Task<string?> ResolveDomainAsync(int departmentId, CancellationToken ct = default);

    /// <summary>مدیر مستقیم (level 1) / مافوق (level ≥ 2) از سلسله‌مراتب سمت‌ها.</summary>
    Task<ManagerChainResult> ResolveManagerAsync(int employeeId, int level = 1, DateTime? asOf = null, CancellationToken ct = default);

    /// <summary>دارندگان معتبر (تخصیص فعال، در بازه‌ی تاریخ، کارمند فعال) یک سمت.</summary>
    Task<IReadOnlyList<int>> GetPositionHoldersAsync(int positionId, DateTime? asOf = null, CancellationToken ct = default);
}

public static class DepartmentConsistencyStatuses
{
    public const string Consistent = "CONSISTENT";
    public const string Conflict = "CONFLICT";
    public const string EmployeeOnly = "EMPLOYEE_ONLY";
    public const string PositionOnly = "POSITION_ONLY";
    public const string NoDepartment = "NO_DEPARTMENT";
}

public static class LegacyPositionStatuses
{
    public const string NoConflict = "NO_CONFLICT";
    public const string LegacyMatch = "LEGACY_MATCH";
    public const string LegacyConflict = "LEGACY_CONFLICT";
    public const string LegacyReferenceInvalid = "LEGACY_REFERENCE_INVALID";
}

public sealed record EmployeeDepartmentConsistencyRow(
    int EmployeeId, string EmployeeName, int? EmployeeDepartmentId, string? EmployeeDepartment,
    int PrimaryPositionId, string PrimaryPositionTitle, int? PositionDepartmentId, string? PositionDepartment, string Status);

public sealed record LegacyPositionRow(
    int EmployeeId, string EmployeeName, int LegacyPositionId, int? PrimaryPositionId, string Status);

/// <summary>تشخیص‌های فقط‌خواندنیِ سازگاری Employee/Position. هرگز چیزی را اصلاح یا حذف نمی‌کند.</summary>
public interface IOrganizationConsistencyService
{
    /// <summary>برای هر کارمند فعالِ دارای سمت اصلیِ معتبر: وضعیت سازگاری Department کارمند با Department سمت.</summary>
    Task<IReadOnlyList<EmployeeDepartmentConsistencyRow>> GetDepartmentConsistencyAsync(CancellationToken ct = default);

    /// <summary>کارمندانی که Employee.PositionId (میراثی) دارند و وضعیتشان نسبت به تخصیص اصلیِ معتبر.</summary>
    Task<IReadOnlyList<LegacyPositionRow>> GetLegacyPositionConflictsAsync(CancellationToken ct = default);
}
