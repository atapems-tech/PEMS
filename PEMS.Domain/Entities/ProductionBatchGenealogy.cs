using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ساختار ردیابی ژنولوژی بچ‌های تولیدی (Production Batch Genealogy).
/// بر اساس تصمیم مالک (OD-09)، ردیابی دوطرفه (Forward &amp; Backward) بین دسته‌های مواد اولیه مصرف‌شده
/// و دسته‌های محصولات نهایی/میانی حاصل از ران تولیدی را محقق می‌سازد.
/// </summary>
public class ProductionBatchGenealogy : BaseEntity
{
    public int ProductionBatchGenealogyId { get; set; }

    // بچ خروجی / محصول نهایی یا میانی
    public int OutputBatchId { get; set; }
    public ProductionBatch? OutputBatch { get; set; }

    // بچ ورودی / ماده اولیه مصرف‌شده
    public int InputBatchId { get; set; }
    public ProductionBatch? InputBatch { get; set; }

    // ران تولیدی که این مصرف و تبدیل در آن رخ داده است
    public int? ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public decimal ConsumedQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public string? Notes { get; set; }
}
