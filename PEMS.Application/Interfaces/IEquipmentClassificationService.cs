using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification» بند ۹: هرگز طبقه‌بندی حدسی روی رکورد Equipment
/// اعمال نمی‌شود - فقط وقتی EquipmentType به‌صورت قطعی (Exact/Deterministic Rule Dictionary) با
/// یک EquipmentClass شناخته‌شده مطابقت دارد. رکوردهایی که از قبل EquipmentClassId دارند هرگز
/// بازنویسی نمی‌شوند (Master Data Protection، مطابق بند ۳۰).
/// </summary>
public interface IEquipmentClassificationService
{
    Task<EquipmentClassificationReportDto> GetClassificationReportAsync();

    Task<int> BackfillAsync();
}
