namespace PEMS.Domain.Entities;

/// <summary>
/// طبق درخواست: یک WorkOrder ممکن است نیاز به ثبت نظر چند دیسیپلین تعمیراتی مختلف داشته
/// باشد (مثلاً یک تعمیر اساسی کمپرسور که مکانیک + برق + ابزاردقیق هر کدوم باید یافته‌ی خودشون
/// رو ثبت کنن) - طبق رویه‌ی استاندارد هماهنگی چند-تخصصی (Multi-Craft Coordination، EN 13460).
/// همون الگوی DocumentIdcReferral، این‌بار برای WorkOrder.
/// </summary>
public class WorkOrderDisciplineReferral
{
    public int WorkOrderDisciplineReferralId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public int CycleNumber { get; set; } = 1;

    public int? CreatedById { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
