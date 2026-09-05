namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۶: FunctionalLocation از قبل در مدل دیتابیس بود ولی
/// هیچ Controller/View‌ای نداشت. طبق الگوی MaterialCategory (لیست تخت + ستون Parent، نه
/// یک Tree Widget جدا) - سازگار با نگاه فعلی پروژه به لیست‌های سلسله‌مراتبی.
/// </summary>
public class FunctionalLocationListDto
{
    public int FunctionalLocationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Plant { get; set; }
    public string? Area { get; set; }
    public string? Unit { get; set; }
    public int? ParentId { get; set; }
    public string? ParentCode { get; set; }
    public string PathBreadcrumb { get; set; } = "";
    public int EquipmentCount { get; set; }
    public int AssetCount { get; set; }
    public bool IsActive { get; set; }
}

public class FunctionalLocationPickerItemDto
{
    public int FunctionalLocationId { get; set; }
    public string Code { get; set; } = "";
    public string PathBreadcrumb { get; set; } = "";
}
