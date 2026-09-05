using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف مدرک داخل یک Transmittal (شماره رویژن مدرک در لحظه ارسال ثبت می‌شود).
/// </summary>
public class TransmittalItem
{
    [Key]
    public int TransmittalItemId { get; set; }

    public int TransmittalId { get; set; }
    public Transmittal? Transmittal { get; set; }

    public int DocumentId { get; set; }

    // طبق درخواست: Transmittal باید بتواند هم مدارک مهندسی، هم اقلام VDR را در بر بگیرد -
    // قبلاً فقط به EngineeringDocument محدود بود (سازگار با هسته‌ی مشترک Document Core)
    public Document? Document { get; set; }

    public string RevisionNo { get; set; } = "00";

    public string? Remarks { get; set; }
}
