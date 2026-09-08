namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification» بند ۹ و ۳۳: یک ردیف از گزارش قابل‌بازبینی
/// Backfill - چه اعمال شده باشد (Applied=true) چه صرفاً برای بازبینی انسانی نمایش داده شود
/// (وقتی هیچ قاعده‌ی قطعی و بدون‌ابهامی برای EquipmentType آن پیدا نشد، Applied=false و
/// ProposedEquipmentClassCode=null - هرگز حدس زده نمی‌شود).
/// </summary>
public class EquipmentClassificationSuggestionDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string CurrentEquipmentType { get; set; } = string.Empty;
    public int? ProposedEquipmentClassId { get; set; }
    public string? ProposedEquipmentClassCode { get; set; }
    public string? ProposedEquipmentClassName { get; set; }
    public string? Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Applied { get; set; }
}

public class EquipmentClassificationReportDto
{
    public List<EquipmentClassificationSuggestionDto> Suggestions { get; set; } = new();
    public int TotalUnclassifiedEquipment { get; set; }
    public int MatchedByRule { get; set; }
    public int UnmatchedNeedsReview { get; set; }
}
