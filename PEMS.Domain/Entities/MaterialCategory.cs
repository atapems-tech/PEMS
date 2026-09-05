using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory & Material Management بخش ۶: دسته‌بندی سلسله‌مراتبی متریال - جدا از
/// Discipline (مثلاً "Bearings" زیر "Mechanical" یه Category هست، نه یه Discipline).
/// همون الگوی خودارجاعی FunctionalLocation.
/// </summary>
public class MaterialCategory : BaseEntity
{
    public int MaterialCategoryId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? ParentCategoryId { get; set; }
    public MaterialCategory? Parent { get; set; }
    public ICollection<MaterialCategory> Children { get; set; } = new List<MaterialCategory>();

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
