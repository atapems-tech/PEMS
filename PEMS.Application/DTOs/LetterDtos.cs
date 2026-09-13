namespace PEMS.Application.DTOs;

public class LetterDto
{
    public int LetterId { get; set; }
    public string LetterType { get; set; } = "";
    public string LetterNumber { get; set; } = "";
    public DateTime LetterDate { get; set; }
    public string? Sender { get; set; }
    public string? Receiver { get; set; }
    public string Subject { get; set; } = "";
    public string Priority { get; set; } = "Normal";
    public string ConfidentialityLevel { get; set; } = "Internal";
    public string? ProjectName { get; set; }
    public string? DisciplineName { get; set; }
    public int? RelatedDocumentId { get; set; }
    public string? RelatedDocumentNo { get; set; }
    public int? RelatedMRId { get; set; }
    public string? RelatedMRNumber { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public int AttachmentsCount { get; set; }
    public int OpenReferralsCount { get; set; }
    public bool HasOverdueReferral { get; set; }
    public bool IsStarred { get; set; }

    // To / Cc از دید کاربر جاری (اگر گیرنده‌ی این نامه باشد) - طبق نمونه‌ی کارتابل مرجع
    // ("نامه" در برابر "رونوشت")
    public string? MyRecipientType { get; set; }

    public List<TagDto> Tags { get; set; } = new();

    public string? SignedByName { get; set; }
    public DateTime? SignedDate { get; set; }

    // طبق بند ۲۹ (Concurrency): برای Round-trip در فرم ویرایش/ارسال‌مجدد، تا EF بتواند ویرایش
    // هم‌زمان را تشخیص دهد
    public byte[]? RowVersion { get; set; }

    public int? RegisteredById { get; set; }

    public string? ReturnedByName { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public string? ReturnReason { get; set; }

    public string? RejectedByName { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectReason { get; set; }

    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public DateTime? IssuedDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    public int RevisionCount { get; set; }
}

public class LetterReviewDto
{
    public int LetterReviewId { get; set; }
    public string? ReviewerName { get; set; }
    public string Decision { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }

    // طبق «PEMS Correspondence Workspace» بند ۱۷: نمایش نسخه‌ی قبلی (همان‌که بازبین دیده) در کنار
    // فرم ویرایش «Resubmit» - فقط برای Decision=ReturnedForRevision پر می‌شود
    public string? PreviousSubjectSnapshot { get; set; }
    public string? PreviousBodySnapshot { get; set; }
}

public class LetterRecipientDto
{
    public int LetterRecipientId { get; set; }
    public string RecipientType { get; set; } = "To";
    public string DisplayName { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
}

public class LetterReferralDto
{
    public int LetterReferralId { get; set; }
    public int LetterId { get; set; }
    public string? ReferredToEmployeeName { get; set; }
    public string? ReferredToDepartmentName { get; set; }
    public string? Instructions { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Pending";
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
    public string? ReferredByName { get; set; }
    public DateTime ReferredDate { get; set; }
    public string? ResponseNote { get; set; }
    public bool IsOverdue { get; set; }
}

public class LetterAttachmentDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
}

public class LetterDetailDto
{
    public LetterDto Letter { get; set; } = new();
    public string? Body { get; set; }
    public List<LetterReferralDto> Referrals { get; set; } = new();
    public List<LetterAttachmentDto> Attachments { get; set; } = new();
    public List<LetterRecipientDto> Recipients { get; set; } = new();
    public List<LetterReviewDto> Reviews { get; set; } = new();
}

public class LetterDashboardDto
{
    public int PendingLetters { get; set; }
    public int OverdueReferrals { get; set; }
    public int MyOpenReferrals { get; set; }
    public int RecentlyReceived { get; set; }
    public int RecentlySent { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByType { get; set; } = new();
}

public class RelatedLetterDto
{
    public int LetterId { get; set; }
    public string LetterType { get; set; } = "";
    public string LetterNumber { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTime LetterDate { get; set; }
    public string Status { get; set; } = "";
}

public class NotificationDto
{
    public int NotificationId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
}
