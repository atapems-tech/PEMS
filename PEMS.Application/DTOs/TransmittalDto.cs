namespace PEMS.Application.DTOs;

public class TransmittalDto
{
    public int TransmittalId { get; set; }
    public string TransmittalNo { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public DateTime TransmittalDate { get; set; }
    public DateTime? SentDate { get; set; }
    public int ItemsCount { get; set; }
    public List<TransmittalItemDto> Items { get; set; } = new();
    public List<TransmittalRecipientDto> Recipients { get; set; } = new();

    // طبق ممیزی حاکمیت Transmittal (بخش ۵-۶-۸)
    public bool ResponseRequired { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? ResponseReceivedDate { get; set; }
    public string? ResponseNote { get; set; }
    public bool IsResponseOverdue { get; set; }
    public bool IsIssued { get; set; }

    public DateTime? CancelledDate { get; set; }
    public string? CancelledReason { get; set; }

    public int? SupersedesTransmittalId { get; set; }
    public string? SupersedesTransmittalNo { get; set; }
    public int? SupersededByTransmittalId { get; set; }
    public string? SupersededByTransmittalNo { get; set; }
}

public class TransmittalItemDto
{
    public int TransmittalItemId { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public string RevisionNo { get; set; } = "";
    public string? Remarks { get; set; }

    // طبق یافته‌ی G-21: هویت تاریخی فایل رسمی در لحظه‌ی ارسال - null یعنی رویژن/فایل رسمی در
    // آن لحظه قابل تشخیص نبود (مثلاً مدرک هنوز هیچ DocumentRevision واقعی نداشت)
    public int? DocumentRevisionId { get; set; }
    public string? SnapshotFileName { get; set; }
    public string? SnapshotFileHash { get; set; }
    public long? SnapshotFileSize { get; set; }
}

public class TransmittalRecipientDto
{
    public int TransmittalRecipientId { get; set; }
    public string Name { get; set; } = "";
    public string RecipientType { get; set; } = "";
    public string? Organization { get; set; }
    public bool ResponseRequired { get; set; }

    // طبق یافته‌ی G-22
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNote { get; set; }
}

/// <summary>ورودی یک گیرنده هنگام ساخت Transmittal - طبق بخش ۷ (Structured Recipients).</summary>
public class TransmittalRecipientInput
{
    public string Name { get; set; } = "";
    public string RecipientType { get; set; } = "External";
    public string? Organization { get; set; }
    public bool ResponseRequired { get; set; }
}

/// <summary>
/// طبق «PEMS Phase 2A — EDMS Foundation» بخش ۲: خلاصه‌ی سبک برای نمایش پیوند متقابل روی صفحه‌ی
/// Engineering Document Details - عمداً از TransmittalDto کامل (با Items/Recipients تودرتو)
/// جدا نگه داشته شده چون این‌جا فقط یک ردیف خلاصه در یک تب مرتبط لازم است، نه کل رکورد.
/// </summary>
public class RelatedTransmittalDto
{
    public int TransmittalId { get; set; }
    public string TransmittalNo { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime TransmittalDate { get; set; }
    public DateTime? SentDate { get; set; }
    public string Purpose { get; set; } = "";
    public string RevisionNoAtTransmittal { get; set; } = "";
}
