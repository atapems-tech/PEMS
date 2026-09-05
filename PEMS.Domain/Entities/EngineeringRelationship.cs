using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// AI Relationship Engine: روابط مهندسی که مستقیم از FK های موجود قابل استنتاج نیستند
/// (مثلاً AI متوجه شده یک GA Drawing به یک Motor Datasheet دیگر ارجاع می‌دهد، بدون این‌که
/// FK صریحی بین آن دو باشد). روابط مبتنی بر FK موجود (تجهیز↔مدرک، Vendor↔PO و ...) نیازی به
/// این جدول ندارند و مستقیم از دیتابیس موجود Query می‌شوند (RelationshipService).
/// </summary>
public class EngineeringRelationship
{
    [Key]
    public int EngineeringRelationshipId { get; set; }

    // Document, Equipment, Vendor, Contract, MR, MRQ, PurchaseOrder, WorkOrder
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }

    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }

    // RelatesTo, References, SupersededBy, PartOf ...
    public string RelationshipType { get; set; } = "RelatesTo";

    public double? Confidence { get; set; }

    public bool IsAIProposed { get; set; }

    public string ReviewStatus { get; set; } = "Approved";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
