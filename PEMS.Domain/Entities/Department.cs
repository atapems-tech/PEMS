namespace PEMS.Domain.Entities;

public class Department
{
    public int DepartmentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // طبق سند RBAC-ABAC Authorization Architecture بخش ۲: سلسله‌مراتب سازمانی واقعی و
    // قابل‌تنظیم - Null یعنی این دپارتمان در بالاترین سطح است (مثلاً "Engineering" بدون والد،
    // "Process"/"Mechanical" زیرمجموعه‌ی آن). عمداً به‌جای Entity جدید "Organization" بالای این -
    // PEMS تک‌شرکتی/on-prem است (BaseEntity.TenantId هنوز خالی/آماده‌ی آینده است)، خودِ Department
    // با این FK خودارجاع، ریشه‌ی سلسله‌مراتب واقعی می‌شود.
    // Phase 3B (Work Order governance): دامنه‌ی سازمانی این دپارتمان (Engineering/Maintenance/Operations/Warehouse). Null = ارث از
    // والد؛ اگر تا ریشه هیچ‌کدام مقدار نداشته باشد، دامنه «نامشخص» است و کنش‌های حاکمیتی Work Order برای اعضای آن رد می‌شود (Fail-closed).
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string? OrganizationDomain { get; set; }

    public int? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}