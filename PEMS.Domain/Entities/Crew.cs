using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۴: ترکیب اجرایی نفرات برای
/// یک فعالیت مشخص - برخلاف Team (واحد سازمانی پایدار)، Crew می‌تواند پایدار یا مخصوص یک
/// Work Order باشد. WorkOrderId=null یعنی Crew مستقل/پایدار.
/// </summary>
public class Crew : BaseEntity
{
    public int CrewId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<CrewMember> Members { get; set; } = new List<CrewMember>();
}
