using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ساختار فرمولاسیون برای تولید فرآیندی (Process Formula / Recipe).
/// طبق تصمیمات تاییدشده مالک در PPC فاز صفر و سند مشخصات 08:
/// این موجودیت هویت، نسخه و پارامترهای پایه‌ی تولید فرآیندی شامل بازده موردانتظار، هدررفت مجاز،
/// اندازه بچ مرجع و اقلام فرمول را نگهداری می‌کند.
/// </summary>
public class ProcessFormula : BaseEntity
{
    public int ProcessFormulaId { get; set; }

    public string FormulaCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int VersionNumber { get; set; } = 1;

    // Draft, UnderReview, Approved, Superseded, Retired
    public string Status { get; set; } = "Draft";

    public decimal BaseBatchSize { get; set; }

    public int? BaseUomId { get; set; }
    public MaterialUOM? BaseUom { get; set; }

    public decimal ExpectedYieldPercent { get; set; } = 100m;

    public decimal StandardLossPercent { get; set; } = 0m;

    public string? Description { get; set; }

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ProcessFormulaItem> Items { get; set; } = new List<ProcessFormulaItem>();

    public ICollection<ProcessFormulaCoProduct> CoProducts { get; set; } = new List<ProcessFormulaCoProduct>();

    public ICollection<ProcessRoute> CompatibleRoutes { get; set; } = new List<ProcessRoute>();
}
