using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// تعریف داده‌محور گام‌های تأیید «درخواست کاتالوگ قطعه» (به‌جای Hard-code در Controller).
/// مسیر هر درخواست هنگام Submit با ارزیابی ConditionKey هر گام ساخته (Snapshot) می‌شود؛ مدیر می‌تواند ترتیب/فعال‌بودن/
/// شرط و گیرنده‌ی هر گام را عوض کند. فلوچارت اصلی (فایل «فلوچارت.pdf»):
/// DirectManager → SuperiorManager → [Critical/Capital] MaintenanceSupervisor → [Section فنی] SparePartsSpecialist →
/// [Manufacturer جدید] Sourcing → CommodityGroupReview → PartCataloguer (ثبت در IFS و اطلاع‌رسانی) → [Planning≠A] InventoryControl.
/// </summary>
public class PartCatalogueWorkflowStep : BaseEntity
{
    public int WorkflowStepId { get; set; }

    /// <summary>کلید پایدار (DirectManager, SuperiorManager, MaintenanceSupervisor, ...)؛ در Assignment/History استفاده می‌شود.</summary>
    public string StepKey { get; set; } = string.Empty;

    public int StepOrder { get; set; }

    public string NameEn { get; set; } = string.Empty;
    public string NameFa { get; set; } = string.Empty;

    /// <summary>Review | Approval | Registration - تعیین‌کننده‌ی مجوز لازم (Review/Approve) برای انجام گام.</summary>
    public string Kind { get; set; } = PartCatalogueStepKinds.Review;

    /// <summary>DirectManager | SuperiorManager | AssignedPool - گیرنده‌ی گام چگونه حل می‌شود.</summary>
    public string ApproverSource { get; set; } = PartCatalogueApproverSources.AssignedPool;

    /// <summary>Always | CriticalOrCapital | SectionNeedsSpareReview | NewManufacturer | PlanningMethodNotA.</summary>
    public string ConditionKey { get; set; } = PartCatalogueConditions.Always;

    public bool IsActive { get; set; } = true;

    /// <summary>نیازمند تأیید کاربر/مدیر (مثلاً شرط Section در فلوچارت کامل نیست).</summary>
    public bool NeedsConfirmation { get; set; }
}

public static class PartCatalogueStepKinds
{
    public const string Review = "Review";
    public const string Approval = "Approval";
    public const string Registration = "Registration";
}

public static class PartCatalogueApproverSources
{
    public const string DirectManager = "DirectManager";
    public const string SuperiorManager = "SuperiorManager";
    public const string AssignedPool = "AssignedPool";
}

public static class PartCatalogueConditions
{
    public const string Always = "Always";

    /// <summary>برچسب «Cr/Ca» فلوچارت: Critical یا Capital (نه فقط Critical).</summary>
    public const string CriticalOrCapital = "CriticalOrCapital";
    public const string SectionNeedsSpareReview = "SectionNeedsSpareReview";
    public const string NewManufacturer = "NewManufacturer";
    public const string PlanningMethodNotA = "PlanningMethodNotA";

    public static readonly string[] All =
        { Always, CriticalOrCapital, SectionNeedsSpareReview, NewManufacturer, PlanningMethodNotA };
}

/// <summary>
/// گیرنده‌های هر گام که یک «نقش/جایگاه» است (کارشناس قطعات، واحد Sourcing، ...) یا نقش ویژه‌ی CEO.
/// StepKey = کلید گام، یا "CEO" برای تعیین مدیرعامل (برای قاعده‌ی Skip). موجود نبودن مفهوم مدیرعامل/مدیر مستقیم در
/// مدل سازمانی فعلی PEMS (Employee بدون ManagerId) باعث ساخت این جدول ماژولی شد.
/// </summary>
public class PartCatalogueApproverAssignment : BaseEntity
{
    public const string CeoKey = "CEO";

    public int ApproverAssignmentId { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>پیوند «مدیر مستقیم» هر کارمند (Employee.ManagerId در PEMS وجود ندارد). مدیر مافوق = مدیر مستقیمِ مدیر مستقیم.</summary>
public class PartCatalogueOrgLink : BaseEntity
{
    public int OrgLinkId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int ManagerEmployeeId { get; set; }
    public Employee? Manager { get; set; }
}
