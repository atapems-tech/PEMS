namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// لاگ ممیزی «پیکربندی» گردش‌کار (ایجاد، Clone، نسخه، ویرایش Draft، Validate، Publish، Binding، ...).
/// کاملاً جدا از WorkflowHistory (که رویدادهای اجرایی Instance را نگه می‌دارد). فقط‌افزودنی است.
/// </summary>
public class WorkflowConfigAudit
{
    public int WorkflowConfigAuditId { get; set; }

    public string Action { get; set; } = "";

    public int? WorkflowDefinitionId { get; set; }
    public int? WorkflowVersionId { get; set; }
    public int? WorkflowBindingId { get; set; }

    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string? PreviousState { get; set; }
    public string? CurrentState { get; set; }
    public string? Details { get; set; }
}

public static class WorkflowConfigAuditActions
{
    public const string DefinitionCreated = "DefinitionCreated";
    public const string DefinitionCloned = "DefinitionCloned";
    public const string DefinitionArchived = "DefinitionArchived";
    public const string VersionCreated = "VersionCreated";
    public const string VersionCloned = "VersionCloned";
    public const string DraftEdited = "DraftEdited";
    public const string Validated = "Validated";
    public const string ValidationFailed = "ValidationFailed";
    public const string Published = "Published";
    public const string Retired = "Retired";
    public const string Activated = "Activated";
    public const string Deactivated = "Deactivated";
    public const string BindingCreated = "BindingCreated";
    public const string BindingChanged = "BindingChanged";
}

public static class WorkflowVersionStatus
{
    public const string Draft = "Draft";
    public const string Validated = "Validated";
    public const string Published = "Published";
    public const string Retired = "Retired";

    public static bool IsEditable(string? status) => status == Draft || status == Validated;
    public static bool IsImmutable(string? status) => status == Published || status == Retired;
}
