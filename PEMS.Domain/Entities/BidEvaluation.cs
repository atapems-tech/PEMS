using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ثبت مستقل تصمیم ارزیابی فنی یا تجاری یک Bid - طبق ممیزی §D/§I (قاعده‌ی ۲): فنی و تجاری
/// همیشه ردیف‌های جدا هستند و هرگز یک تصمیم واحد نیستند؛ Winner Selection (Tender.AwardedBidId)
/// فقط زمانی مجاز است که هر دو نوع Evaluation با Decision=Passed موجود باشند - گیت این قاعده در
/// ContractWorkflowService/TenderService پیاده می‌شود، نه اینجا.
/// </summary>
public class BidEvaluation : BaseEntity
{
    [Key]
    public int BidEvaluationId { get; set; }

    public int BidId { get; set; }
    public Bid? Bid { get; set; }

    // Technical, Commercial
    public string EvaluationType { get; set; } = "Technical";

    public int? EvaluatorId { get; set; }
    public Employee? Evaluator { get; set; }

    public string? DisciplineRole { get; set; }

    // Pending, Passed, Failed
    public string Decision { get; set; } = "Pending";

    public string? Comment { get; set; }

    public DateTime? EvaluationDate { get; set; }

    public bool IsActive { get; set; } = true;
}
