using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// نقش تعمیرونگهداری - در یک دیسیپلین مشخص (مثلاً "تکنسین مکانیک"، "سرگروه برق"). طبق
/// درخواست: تعریف نقش‌ها در دیسیپلین‌های تعمیرونگهداری برای گروه‌های کاری.
/// </summary>
public class MaintenanceRole : BaseEntity
{
    public int MaintenanceRoleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // Junior, Senior, Lead
    public string Level { get; set; } = "Junior";

    // آیا این نقش می‌تواند سرگروه (Crew Leader) یک گروه کاری باشد
    public bool IsCrewLeadRole { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
