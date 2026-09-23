using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// پیشنهاد یک فروشنده برای یک Tender - طبق ممیزی §D/§I (قاعده‌ی ۳): برخلاف
/// MaterialRequisitionQuotation.VendorName (رشته‌ی آزاد)، اینجا VendorId یک FK واقعی است.
/// OriginalValue پس از ثبت اولیه دیگر توسط سرویس تغییر داده نمی‌شود (تغییرناپذیری تضمین‌شده در
/// لایه‌ی سرویس، نه دیتابیس)؛ هر تغییر مذاکره‌شده در BidNegotiation ثبت و فقط NegotiatedValue/
/// FinalValue به‌روزرسانی می‌شود - مقدار اصلی هرگز بازنویسی نمی‌شود.
/// </summary>
public class Bid : BaseEntity
{
    [Key]
    public int BidId { get; set; }

    public string BidNumber { get; set; } = string.Empty;

    public int TenderId { get; set; }
    public Tender? Tender { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public DateTime? SubmittedDate { get; set; }

    // مقدار اصلی ثبت‌شده - تغییرناپذیر پس از ثبت (اجرا در لایه‌ی سرویس)
    public decimal? OriginalValue { get; set; }

    public string? Currency { get; set; } = "USD";

    // آخرین مقدار مذاکره‌شده‌ی تاییدشده (از BidNegotiation) - هرگز جایگزین OriginalValue نمی‌شود
    public decimal? NegotiatedValue { get; set; }

    // مقداری که در صورت برد به قرارداد منتقل می‌شود
    public decimal? FinalValue { get; set; }

    public string? TechnicalSubmissionReference { get; set; }
    public string? CommercialSubmissionReference { get; set; }

    // Submitted, UnderTechnicalEvaluation, UnderCommercialEvaluation, UnderNegotiation, Won, Lost, Withdrawn
    public string Status { get; set; } = "Submitted";

    public bool IsActive { get; set; } = true;

    public ICollection<BidEvaluation> Evaluations { get; set; } = new List<BidEvaluation>();
    public ICollection<BidNegotiation> Negotiations { get; set; } = new List<BidNegotiation>();
}
