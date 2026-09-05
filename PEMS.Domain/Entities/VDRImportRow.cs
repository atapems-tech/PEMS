using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging از فایل اکسل VDR، پیش از تایید و ورود نهایی.
/// PayloadJson شامل تمام فیلدهای پارس‌شده (اطلاعات مدرک + لیست چرخه‌های رویژن) به‌صورت JSON است
/// تا نیازی به یک جدول جداگانه برای هر ستون نباشد.
/// </summary>
public class VDRImportRow
{
    [Key]
    public int VDRImportRowId { get; set; }

    public int VDRImportBatchId { get; set; }
    public VDRImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string? VendorNameRaw { get; set; }

    // JSON سریالایز شده از VDRImportRowPayload (در PEMS.Application.DTOs)
    public string PayloadJson { get; set; } = string.Empty;

    // Valid, Warning, Error
    public string ValidationStatus { get; set; } = "Valid";

    public string? ValidationMessage { get; set; }

    public bool IsDuplicate { get; set; }

    // آیا کاربر این ردیف را برای Commit انتخاب کرده (پیش‌فرض: بله مگر Error باشد)
    public bool IncludeInCommit { get; set; } = true;

    public int? CreatedVDRItemId { get; set; }
}
