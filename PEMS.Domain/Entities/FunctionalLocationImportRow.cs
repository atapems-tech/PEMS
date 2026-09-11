namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging برای یک Functional Location مجزا. طبق قاعده‌ی نرمال‌سازی: یک سطر اکسل
/// با چند AREA CODE (مثلاً "85A07, 85A08, 85A10, 85A12") به چند FunctionalLocationImportRow
/// مجزا (یکی برای هر کد + یک ردیف والد سطح-Area) تبدیل می‌شود - RowNumber شماره‌ی توالی
/// Staging است، SourceRowNumber ردیف واقعی اکسل را برای ردیابی نگه می‌دارد.
/// </summary>
public class FunctionalLocationImportRow
{
    public int FunctionalLocationImportRowId { get; set; }

    public int FunctionalLocationImportBatchId { get; set; }
    public FunctionalLocationImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public bool IncludeInCommit { get; set; } = true;

    // Valid, Warning, Error
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    public bool IsUpdate { get; set; }

    // New, Existing, Conflict, Invalid - تعیین‌شده در لحظه‌ی Staging، نه بازسازی‌شده از متن پیام
    public string Status { get; set; } = "New";

    // Create, Update, Skip, Conflict, Reject
    public string Action { get; set; } = "Create";

    public int? CreatedFunctionalLocationId { get; set; }
}
