using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// ثبت خودکار هر تغییر (Insert/Update/Delete) روی هر موجودیت سیستم، طبق سند Security
/// Requirements ("Audit Log", "Change History"). به‌صورت سراسری و بدون نیاز به کد اضافه
/// در هر سرویس، از طریق SaveChanges/SaveChangesAsync در PEMSDbContext پر می‌شود.
/// </summary>
public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityKeyValue { get; set; } = string.Empty;

    // Insert, Update, Delete
    public string Action { get; set; } = string.Empty;

    // فقط فیلدهایی که واقعاً تغییر کرده‌اند، به‌صورت "FieldName: old -> new" هر خط
    public string? ChangedFieldsSummary { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    // طبق معماری AI: اگر این تغییر ناشی از یک عملیات AI-Assisted بوده (مثلاً تایید AI Extraction)
    public bool IsAIAssisted { get; set; } = false;
}
