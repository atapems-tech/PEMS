using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// خروجی AI Classification روی یک فایل در AI Intake Queue، طبق سند AI Engineering Intelligence
/// Redesign. برخلاف نسخه‌ی قبلی، DocumentId اختیاری است چون در لحظه‌ی طبقه‌بندی هنوز هیچ
/// EngineeringDocument/VDRItem ای ساخته نشده - این خودِ Classification است که تصمیم می‌گیرد
/// اصلاً باید کدام نوع رکورد ساخته شود (Routing).
/// </summary>
public class AIDocumentClassification
{
    [Key]
    public int AIDocumentClassificationId { get; set; }

    public int AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    // فقط بعد از Routing واقعی پر می‌شود (قبلش Null است)
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }

    // Engineering Drawing, Vendor Datasheet, Material Requisition, MRQ, Purchase Order,
    // Contract, Vendor Manual, Inspection Report, Commissioning Report, Correspondence, ...
    public string? PredictedCategory { get; set; }

    public string? PredictedDocumentType { get; set; }
    public string? PredictedDisciplineCode { get; set; }
    public string? PredictedProjectHint { get; set; }
    public string? PredictedArea { get; set; }
    public string? PredictedUnit { get; set; }
    public string? PredictedSystem { get; set; }
    public string? PredictedSubsystem { get; set; }
    public string? PredictedRevisionNo { get; set; }
    public string? PredictedIssuePurpose { get; set; }
    public string? PredictedVendorName { get; set; }
    public string? PredictedMRNumber { get; set; }
    public string? PredictedEquipmentTagNumber { get; set; }

    public double ConfidenceScore { get; set; }

    // High, Medium, Low - از روی ConfidenceScore و آستانه‌های AIConfidenceSettings محاسبه می‌شود
    public string ConfidenceLevel { get; set; } = "Low";

    // نتیجه‌ی Intelligent Routing: MDR, VDR, MR, MRQ, Contract, CMMS, Unrouted
    public string? SuggestedTargetModule { get; set; }

    // Draft (تازه پیشنهاد شده) / Approved (اعمال و Route شد) / Rejected
    public string ReviewStatus { get; set; } = "Draft";

    public int? ReviewedById { get; set; }
    public DateTime? ReviewedDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
