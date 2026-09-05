namespace PEMS.Application.AI.Models;

public class ExtractedEngineeringData
{
    public ExtractedDocumentInfo? DocumentInfo { get; set; }
    public List<ExtractedEquipmentTagData> EquipmentTags { get; set; } = new();
    public List<ExtractedSpecificationData> Specifications { get; set; } = new();
}

public class ExtractedDocumentInfo
{
    public string? DocumentNo { get; set; }
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? DocumentType { get; set; }
    public string? Discipline { get; set; }
    public string? RevisionNo { get; set; }
    public string? IssuePurpose { get; set; }
    public string? VendorName { get; set; }
    public string? RelatedMRNumber { get; set; }
    public string? Area { get; set; }
    public string? Unit { get; set; }
    public string? System { get; set; }
    public string? Subsystem { get; set; }
    public string? ProjectHint { get; set; }
}

public class ExtractedEquipmentTagData
{
    public string TagNumber { get; set; } = string.Empty;
    public string? TagType { get; set; }
    public string? Area { get; set; }
    public string? Service { get; set; }
    public string? Description { get; set; }
}

public class ExtractedSpecificationData
{
    public string? RelatedTagNumber { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public string? Unit { get; set; }
}
