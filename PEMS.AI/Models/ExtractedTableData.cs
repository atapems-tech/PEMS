namespace PEMS.Application.AI.Models;

/// <summary>
/// خروجی استخراج ردیفی از مدارک جدولی ابزاردقیق/برق (Wiring Connection Diagram, Instrument Index,
/// Marshalling List, I/O List, Loop Drawing). جدا از ExtractedEngineeringData نگه داشته شده چون
/// فیلدهای این جداول (JunctionBox/Cable/MarshallingCabinet/IOCard/...) معنای متفاوتی از
/// equipmentTags/specifications عمومی دارند.
/// </summary>
public class ExtractedTableData
{
    public ExtractedTableDocumentInfo? DocumentInfo { get; set; }
    public List<ExtractedTableRow> Rows { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public int RowCount { get; set; }

    // هشدارهای مدل درباره‌ی چانک ناقص/جدول بریده‌شده - باید به کاربر نمایش داده شود، نه فقط لاگ شود
    public List<string> Warnings { get; set; } = new();
}

public class ExtractedTableDocumentInfo
{
    public string? DocumentNo { get; set; }
    public string? Title { get; set; }
    public string? DocumentType { get; set; }
    public string? Discipline { get; set; }
    public string? RevisionNo { get; set; }
    public string? VendorName { get; set; }
    public string? Area { get; set; }
    public string? Unit { get; set; }
    public string? System { get; set; }
}

public class ExtractedTableRow
{
    // عمداً nullable - ردیف‌های ادامه‌ی توضیحات بدون شماره‌تگ مستقل نباید مدل را مجبور به ساختن مقدار کنند
    public string? TagNumber { get; set; }
    public string? Description { get; set; }
    public string? InstrumentType { get; set; }
    public string? Service { get; set; }
    public string? SignalType { get; set; }
    public string? SignalClass { get; set; }
    public string? JunctionBox { get; set; }
    public string? Terminal { get; set; }
    public string? CableNo { get; set; }
    public string? MarshallingCabinet { get; set; }
    public string? MarshallingTerminal { get; set; }
    public string? SystemCabinet { get; set; }
    public string? IoCard { get; set; }
    public string? IoChannel { get; set; }
    public string? IoAddress { get; set; }
    public string? DcsTagNo { get; set; }
    public string? LoopNo { get; set; }
    public string? Location { get; set; }
    public string? System { get; set; }
    public bool? SpareOrHotSpare { get; set; }
    public string? Remarks { get; set; }
    public Dictionary<string, string>? RawExtra { get; set; }
}
