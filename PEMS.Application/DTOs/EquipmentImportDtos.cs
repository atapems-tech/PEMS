namespace PEMS.Application.DTOs;

/// <summary>
/// یک ردیف تجهیز از فایل اکسل - طبق نمونه‌های واقعی، ستون‌ها بین فایل مکانیکال و اینسترومنت
/// خیلی فرق دارند، پس همه‌ی متغیرهای رایج این‌جا پوشش داده شده‌اند.
/// </summary>
public class EquipmentImportRowPayload
{
    public string? TagNumber { get; set; }
    public string? Name { get; set; }
    public string? EquipmentTypeCode { get; set; }
    public string? DisciplineCodeRaw { get; set; }
    public string? Area { get; set; }
    public string? PIDNumber { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? MRNumber { get; set; }
    public string? PONumber { get; set; }
}

public class EquipmentImportRowDto
{
    public int EquipmentImportRowId { get; set; }
    public int RowNumber { get; set; }
    public string TagNumber { get; set; } = "";
    public string? Name { get; set; }
    public string? DisciplineCodeRaw { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }
    public bool IncludeInCommit { get; set; } = true;
    public bool IsUpdate { get; set; }
    public EquipmentImportRowPayload? Payload { get; set; }
}

public class EquipmentImportBatchDto
{
    public int EquipmentImportBatchId { get; set; }
    public string FileName { get; set; } = "";
    public int ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateTime ImportDate { get; set; }
    public string Status { get; set; } = "Staging";
    public List<EquipmentImportRowDto> Rows { get; set; } = new();
}
