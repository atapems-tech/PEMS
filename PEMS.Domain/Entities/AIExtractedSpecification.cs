using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class AIExtractedSpecification
{
    [Key]
    public int ExtractedSpecificationId { get; set; }

    public int AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    public string? RelatedTagNumber { get; set; }

    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public string? Unit { get; set; }

    public double? Confidence { get; set; }

    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
