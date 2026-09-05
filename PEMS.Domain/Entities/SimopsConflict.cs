using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: تعارض کار هم‌زمان بین دو Permit مشخص - برخلاف IsolationPoint/GasTest که یک
/// والد دارند، این‌جا دو والد (PermitAId/PermitBId) وجود دارد. رکورد به‌صورت دستی توسط کاربر
/// از روی لیست Candidate Overlap (محاسبه‌شده در لحظه‌ی نمایش، نه ذخیره‌شده) ایجاد می‌شود - نه
/// یک اسکن پس‌زمینه‌ی خودکار (زیرساخت Scheduler برای این منظور در پروژه وجود ندارد).
/// Severity و ConflictBasis یک‌بار در لحظه‌ی ایجاد محاسبه و منجمد می‌شوند - دقیقاً همون منطق
/// Immutability رکورد ایمنی‌محور GasReading.Result در CMMS-10d.
/// </summary>
public class SimopsConflict : BaseEntity
{
    public int SimopsConflictId { get; set; }

    public int PermitAId { get; set; }
    public Permit? PermitA { get; set; }

    public int PermitBId { get; set; }
    public Permit? PermitB { get; set; }

    // مثال: "Same Equipment + Time Overlap" - یک‌بار در ایجاد محاسبه و منجمد می‌شود.
    public string ConflictBasis { get; set; } = string.Empty;

    // Low, Medium, High, Unknown - از SimopsInteractionRule محاسبه و منجمد می‌شود.
    public string Severity { get; set; } = string.Empty;

    // Potential, Confirmed, Resolved, AcceptedRisk
    public string Status { get; set; } = "Potential";

    public int RaisedByEmployeeId { get; set; }
    public Employee? RaisedByEmployee { get; set; }

    public int? AssessedByEmployeeId { get; set; }
    public Employee? AssessedByEmployee { get; set; }
    public DateTime? AssessedDate { get; set; }

    public int? ResolvedByEmployeeId { get; set; }
    public Employee? ResolvedByEmployee { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }

    public int? AcceptedByEmployeeId { get; set; }
    public Employee? AcceptedByEmployee { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? AcceptanceJustification { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
