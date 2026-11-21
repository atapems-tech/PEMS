using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// مسیر و توالی عملیات فرآیندی (Process Route).
/// طبق تصمیم مالک در PPC فاز صفر (OD-03)، برای تفکیک صریح از کلمه Routing در پلتفرم اسناد و نامه‌ها،
/// نام اختصاصی ProcessRoute برگزیده شده است.
/// </summary>
public class ProcessRoute : BaseEntity
{
    public int ProcessRouteId { get; set; }

    public string RouteCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int VersionNumber { get; set; } = 1;

    // Draft, Approved, Retired
    public string Status { get; set; } = "Draft";

    public int? PrimaryWorkCenterId { get; set; }
    public ProductionWorkCenter? PrimaryWorkCenter { get; set; }

    public decimal StandardCycleTimeHours { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ProcessRouteOperation> Operations { get; set; } = new List<ProcessRouteOperation>();

    public ICollection<ProcessFormula> CompatibleFormulas { get; set; } = new List<ProcessFormula>();
}
