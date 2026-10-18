using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک دور مذاکره روی یک Bid - طبق ممیزی §D/§I (قاعده‌ی ۴): مقدار اصلی پیشنهاد
/// (Bid.OriginalValue) هرگز اینجا بازنویسی نمی‌شود؛ هر دور فقط یک ردیف تازه با PreviousValue/
/// NegotiatedValue اضافه می‌کند و پس از تایید، Bid.NegotiatedValue به‌روزرسانی می‌شود.
/// </summary>
public class BidNegotiation : BaseEntity
{
    [Key]
    public int BidNegotiationId { get; set; }

    public int BidId { get; set; }
    public Bid? Bid { get; set; }

    public int RoundNo { get; set; }

    public DateTime NegotiationDate { get; set; } = DateTime.Now;

    public string? Participants { get; set; }

    public decimal? PreviousValue { get; set; }

    public decimal? NegotiatedValue { get; set; }

    public string? CommercialChanges { get; set; }
    public string? TechnicalChanges { get; set; }
    public string? ContractualChanges { get; set; }

    public string? Justification { get; set; }

    public string? SupportingDocumentReference { get; set; }

    // Proposed, Approved, Rejected
    public string Status { get; set; } = "Proposed";

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public bool IsActive { get; set; } = true;
}
