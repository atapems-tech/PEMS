using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10c: نقطه‌ی ایزوله‌سازی واقعی فیزیکی (یک شیر، یک بریکر، ...) - همیشه زیرمجموعه‌ی
/// یک Permit است (نه یک Entity مستقل بالادستی) - دقیقاً همون الگوی WorkOrder 1─N Permit در
/// CMMS-10a. "Permit and LOTO must be linked but must not be the same entity."
/// LockNumber/TagNumber عمداً این‌جا به‌عنوان فیلد نگه داشته شده‌اند - نه Entity های Lock/Tag
/// جدا (طبق مدل هدف سند پایه، بند ۱۱).
/// </summary>
public class IsolationPoint : BaseEntity
{
    public int IsolationPointId { get; set; }

    public int PermitId { get; set; }
    public Permit? Permit { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public int? FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocation { get; set; }

    // Electrical, Mechanical, Hydraulic, Pneumatic, Pressure, Steam, Thermal, Chemical,
    // Gravity, StoredEnergy, ProcessEnergy - رشته‌ی ساده + اعتبارسنجی سمت سرور، همون الگوی
    // PermitType/WorkOrderType.
    public string EnergyType { get; set; } = string.Empty;

    // شرح دقیق محل فیزیکی نقطه‌ی ایزوله‌سازی
    public string Location { get; set; } = string.Empty;

    public string? NormalState { get; set; }
    public string? RequiredIsolationState { get; set; }
    public string? IsolationMethod { get; set; }

    public string? LockNumber { get; set; }
    public string? TagNumber { get; set; }

    // Requested, Isolated, Verified, Deisolated, Cancelled
    public string Status { get; set; } = "Requested";

    public int? IsolatedByEmployeeId { get; set; }
    public Employee? IsolatedByEmployee { get; set; }
    public DateTime? IsolatedDate { get; set; }

    public int? VerifiedByEmployeeId { get; set; }
    public Employee? VerifiedByEmployee { get; set; }
    public DateTime? VerifiedDate { get; set; }

    public int? DeisolatedByEmployeeId { get; set; }
    public Employee? DeisolatedByEmployee { get; set; }
    public DateTime? DeisolatedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
