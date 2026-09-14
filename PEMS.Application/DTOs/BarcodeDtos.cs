namespace PEMS.Application.DTOs;

public class BarcodeIdentityDto
{
    public int BarcodeIdentityId { get; set; }
    public string Code { get; set; } = "";
    public string Symbology { get; set; } = "";
    public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    public string Status { get; set; } = "";
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int PrintCount { get; set; }
    public DateTime? LastPrintedDate { get; set; }
    public string? DisableReason { get; set; }
    public string? RevokeReason { get; set; }
    public int? ReplacedByBarcodeIdentityId { get; set; }
    public int? ReplacesBarcodeIdentityId { get; set; }

    // طبق بند «Verification»: برچسب قابل‌خواندن برای انسان (نه خودِ توکن) - از رکورد هدف واقعی
    // در لحظه‌ی نمایش خوانده می‌شود، هرگز داخل بارکد کدگذاری نشده است.
    public string? TargetDisplayLabel { get; set; }
}

/// <summary>
/// طبق بند «Barcode Resolution»/«Performance»: حداقل اطلاعات لازم برای تصمیم Authorization و
/// مسیریابی - نه کل رکورد کسب‌وکاری هدف (که فقط بعد از تایید مجوز، توسط صفحه‌ی موجودِ همان
/// منبع بارگذاری می‌شود).
/// </summary>
public class BarcodeResolutionResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public int? BarcodeIdentityId { get; set; }
    public string? TargetType { get; set; }
    public int? TargetId { get; set; }
    public string? Status { get; set; }
}

public class BulkGenerateResultDto
{
    public int Requested { get; set; }
    public int Created { get; set; }
    public int AlreadyExisted { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<int> BarcodeIdentityIds { get; set; } = new();
}
