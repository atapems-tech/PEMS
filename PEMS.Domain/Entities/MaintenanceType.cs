using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۶: طبقه‌بندی
/// استاندارد «چه نوع کاری است» - همون واژگانی که WorkOrder.WorkOrderType امروز به‌صورت رشته‌ی آزاد
/// استفاده می‌کند (Preventive/Corrective/Emergency طبق CLAUDE.md)، اینجا به یک جدول مرجع واقعی
/// تبدیل می‌شود تا هم UI/AI بتوانند کدهای پایدار را ارجاع بدهند، هم داده به‌صورت خودکار به فارسی/
/// انگلیسی نمایش داده شود. WorkOrder.WorkOrderType (رشته) عمداً دست‌نخورده می‌ماند - این جدول
/// مرجع، جایگزین فوری آن نیست، فقط زیرساخت مرجع/AI-Ready برای آینده است.
/// </summary>
public class MaintenanceType : BaseEntity
{
    public int MaintenanceTypeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    // طبق بند ۱۲: رکورد پایه‌ی سیستم - کاربر نمی‌تواند آن را Hard-Delete کند، فقط Deactivate
    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
