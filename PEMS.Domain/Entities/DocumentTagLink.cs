using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// ارتباط چندبه‌چند هر نوع مدرک با تگ‌های تجهیز، طبق الگوی AVEVA Engineering، حالا در سطح
/// Enterprise Document Core (برای Engineering Document و Vendor Document هر دو کار می‌کند).
/// جایگزین EngineeringDocumentTagLink قبلی.
/// </summary>
public class DocumentTagLink
{
    [Key]
    public int DocumentTagLinkId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int EquipmentTagId { get; set; }
    public EquipmentTag? EquipmentTag { get; set; }

    public bool IsAIProposed { get; set; }

    public string ReviewStatus { get; set; } = "Approved";

    public double? Confidence { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
