namespace PEMS.Application.DTOs;

/// <summary>
/// یک Functional Location مجزا آماده‌ی Commit - طبق قاعده‌ی نرمال‌سازی، از یک سطر اکسل با چند
/// کد، چند Payload (یکی برای هر کد + یک Payload والد سطح-Area) تولید می‌شود.
/// </summary>
public class FunctionalLocationImportRowPayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Area { get; set; }

    // فقط برای ردیف والد سطح-Area (وقتی سطر منبع چند کد داشته) true است
    public bool IsAreaParentRow { get; set; }

    // فقط روی ردیف‌های فرزند پر می‌شود - نام Area والد که در Commit به ParentId واقعی resolve می‌شود
    public string? ParentAreaName { get; set; }

    // طبق بند ۲۶ (Traceability)
    public string SourceAreaNameRaw { get; set; } = string.Empty;
    public string SourceAreaCodeRaw { get; set; } = string.Empty;
}

public class FunctionalLocationImportRowDto
{
    public int FunctionalLocationImportRowId { get; set; }
    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ParentAreaName { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }
    public bool IncludeInCommit { get; set; } = true;
    public bool IsUpdate { get; set; }

    // طبق پیشنهاد سند مستر - برچسب نمایشی وضعیت/اقدام برای Preview (مشتق‌شده، نه ذخیره‌شده)
    public string Status { get; set; } = "New"; // New, Existing, Conflict, Invalid
    public string Action { get; set; } = "Create"; // Create, Update, Skip, Reject

    public FunctionalLocationImportRowPayload? Payload { get; set; }
}

public class FunctionalLocationImportBatchDto
{
    public int FunctionalLocationImportBatchId { get; set; }
    public string FileName { get; set; } = "";
    public DateTime ImportDate { get; set; }
    public string Status { get; set; } = "Staging";
    public string? ImportedByName { get; set; }
    public List<FunctionalLocationImportRowDto> Rows { get; set; } = new();

    // طبق بند ۲۳ (Import Report)
    public int SourceRowCount { get; set; }
    public int ExpandedLocationCount { get; set; }
    public int NewCount { get; set; }
    public int ExistingCount { get; set; }
    public int ConflictCount { get; set; }
    public int InvalidCount { get; set; }
}

public class FunctionalLocationImportCommitResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Conflicts { get; set; }
    public int Rejected { get; set; }
}
