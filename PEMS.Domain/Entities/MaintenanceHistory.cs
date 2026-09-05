using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class MaintenanceHistory
{
    [Key]
    public int MaintenanceHistoryId { get; set; }

    // تجهیز مرتبط
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // دستور کار مرتبط
    public int? WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    // تاریخ انجام تعمیر
    public DateTime ExecutionDate { get; set; }

    // یافته تعمیرکار
    public string? Finding { get; set; }

    // اقدام انجام شده
    public string? ActionTaken { get; set; }

    // نفر انجام دهنده
    public string? Technician { get; set; }

    // مدت زمان تعمیر (ساعت)
    public decimal? DurationHours { get; set; }

    // هزینه تعمیر
    public decimal? Cost { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}