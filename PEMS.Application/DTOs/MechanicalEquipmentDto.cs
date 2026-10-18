namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۳:
/// دقیقاً همون الگوی InstrumentListDto/InstrumentDashboardDto (PEMS.Application/DTOs/InstrumentDto.cs)
/// - فقط فهرست/داشبورد DTO واقعی دارند (طبق «Global Data Grid Standard»)؛ صفحات Passport/Edit
/// مستقیم روی Entity کار می‌کنند. برخلاف Instrument، MechanicalEquipment.EquipmentId اختیاری است
/// (زنجیره‌ی Nullable موجود که باید حفظ شود) - پس TagNumber از خودِ MechanicalEquipment خوانده
/// می‌شود، نه از Equipment.
/// </summary>
public class MechanicalEquipmentListDto
{
    public int MechanicalEquipmentId { get; set; }
    public int? EquipmentId { get; set; }
    public string TagNumber { get; set; } = "";
    public string EquipmentType { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    // Rotating, Static, Unclassified - طبق بند ۲ ممیزی: MechanicalEquipment می‌تواند بدون هیچ
    // زیررده‌ی Rotating/Static فقط با فیلدهای پایه ثبت شود.
    public string SubType { get; set; } = "Unclassified";
}

public class MechanicalEquipmentDashboardDto
{
    public int TotalCount { get; set; }
    public int RotatingCount { get; set; }
    public int StaticCount { get; set; }
    public int UnclassifiedCount { get; set; }
}
