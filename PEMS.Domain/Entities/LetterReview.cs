using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Letter Details — Controlled Lifecycle» بند ۱۲-۱۳: تصمیم بازبین (Approve/Return/
/// Reject) یک مفهوم کاملاً جدا از LetterReferral است - Referral یعنی «این را برای اطلاع/اقدام
/// بفرست»، Review یعنی «آیا این نامه می‌تواند در چرخه‌ی تایید جلو برود». دقیقاً همان الگوی
/// LetterReferral خودش استفاده شده: هر تصمیم یک ردیف مستقل جدید است، پس تاریخچه‌ی کامل
/// چند-دوره‌ای خودکار از همین جدول به‌دست می‌آید - نیازی به جدول Audit جدا نیست.
///
/// Snapshot فقط برای Decision=ReturnedForRevision پر می‌شود (بند ۱۱: حفظ نسخه‌ای که بازبین
/// دیده و برگردانده، پیش از این‌که نویسنده‌ی اصلی آن را ویرایش کند).
/// </summary>
public class LetterReview : BaseEntity
{
    [Key]
    public int LetterReviewId { get; set; }

    public int LetterId { get; set; }
    public Letter? Letter { get; set; }

    // بدون Navigation/FK - همون قرارداد Letter.RegisteredById/ApprovedById (ApplicationUserId)
    public int? ReviewerUserId { get; set; }

    // Approved, ReturnedForRevision, Rejected
    public string Decision { get; set; } = string.Empty;

    public string? Comment { get; set; }

    // فقط برای ReturnedForRevision پر می‌شود - محتوایی که بازبین در همین لحظه دیده است
    public string? PreviousSubjectSnapshot { get; set; }
    public string? PreviousBodySnapshot { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}

public static class LetterReviewDecisions
{
    public const string Approved = "Approved";
    public const string ReturnedForRevision = "ReturnedForRevision";
    public const string Rejected = "Rejected";
}
