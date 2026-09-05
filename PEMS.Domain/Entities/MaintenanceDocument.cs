namespace PEMS.Domain.Entities;

/// <summary>
/// نوع تخصصی "مدرک نگهداری و تعمیرات" از Enterprise Document Core. نسخه‌ی اولیه/حداقلی -
/// به Equipment و در صورت وجود به یک Work Order مشخص وصل می‌شود.
/// </summary>
public class MaintenanceDocument : Document
{
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
