namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// تعریف کلی یک نوع گردش‌کار (مثلاً «تایید مدرک مهندسی»)، مستقل از هر رکورد کسب‌وکاری خاص.
/// طبق سند Baseline: هیچ منطق مدرک/نامه/VDR اینجا نباید باشد - فقط فراداده‌ی گردش‌کار.
/// </summary>
public class WorkflowDefinition
{
    public int WorkflowDefinitionId { get; set; }

    public string Name { get; set; } = "";
    // Phase 4 (additive): نام فارسی نمایشی؛ خالی = «Name» (بازگشت صریح در UI)
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? NameFa { get; set; }

    public string Category { get; set; } = "";

    // نام ماژول مصرف‌کننده (رشته‌ی آزاد، نه FK) - مثلاً "EngineeringDocument", "VDR", "Letter"
    // عمداً رشته است نه Enum بسته، چون ماژول‌های آینده نباید نیاز به تغییر این پروژه داشته باشند
    public string Module { get; set; } = "";

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int? CreatedById { get; set; }

    public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();
}
