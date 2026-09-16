using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش T/U: منبع/قابلیت تولید (مثلاً
/// یک خط تولید یا واحد فرآیندی) - یک موجودیت نازک که به FunctionalLocation موجود (اجباری) و در
/// صورت وجود به یک Equipment مشخص (اختیاری) وصل می‌شود. FunctionalLocation/Equipment جدیدی
/// ساخته نمی‌شود - طبق قاعده‌ی REUSE &gt; EXTEND &gt; CREATE.
/// </summary>
public class ProductionWorkCenter : BaseEntity
{
    public int ProductionWorkCenterId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocation { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
