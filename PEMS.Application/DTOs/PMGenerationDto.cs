namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 6 — CMMS Operational Excellence & Maintenance Execution» بند ۱۵/۵۵-۵۶:
/// نتیجه‌ی یک اجرای تولید PM - چند WorkOrder واقعاً ساخته شد در برابر چند Plan به دلیل
/// Idempotency (یک WorkOrder فعال از قبل موجود بود) رد شد. این دو عدد هرگز نباید با هم
/// مخلوط شوند.
/// </summary>
public class PMGenerationResultDto
{
    public int WorkOrdersGenerated { get; set; }
    public int PlansSkippedAlreadyHasOpenWorkOrder { get; set; }
    public List<string> GeneratedWorkOrderNumbers { get; set; } = new();
}
