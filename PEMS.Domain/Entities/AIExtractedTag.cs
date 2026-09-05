using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// تگ تجهیز که توسط هوش مصنوعی از روی یک مدرک مهندسی استخراج شده؛ پیش از تایید کاربر
/// در جدول اصلی EquipmentTag ثبت نمی‌شود.
/// </summary>
public class AIExtractedTag
{
    [Key]
    public int ExtractedTagId { get; set; }

    public int AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    public string TagNumber { get; set; } = string.Empty;
    public string? TagType { get; set; }
    public string? Area { get; set; }
    public string? Service { get; set; }
    public string? Description { get; set; }

    public double? Confidence { get; set; }

    // Draft / Approved / Rejected
    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }

    public int? LinkedEquipmentId { get; set; }
    public Equipment? LinkedEquipment { get; set; }

    public int? CreatedTagId { get; set; }
    public EquipmentTag? CreatedTag { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
