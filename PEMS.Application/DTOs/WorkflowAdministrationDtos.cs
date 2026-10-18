using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Application.DTOs;

/// <summary>Phase 4 - مدیر گردش‌کار: UserId کاربر Identity (برای ممیزی) + نقش‌ها (برای SystemAdmin + مجوز WorkflowManagement.*).</summary>
public sealed record WorkflowAdminActor(int UserId, string Name, IReadOnlyList<string> Roles);

public sealed class WorkflowAdminResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    /// <summary>توکن RowVersion جدید Draft (Base64) پس از ذخیره.</summary>
    public string? RowVersion { get; init; }
    public int? Id { get; init; }
    public bool IsConflict { get; init; }
    public bool IsForbidden { get; init; }
    public WorkflowValidationResult? Validation { get; init; }
    public static WorkflowAdminResult Ok(int? id = null, string? rowVersion = null, WorkflowValidationResult? validation = null) => new() { Success = true, Id = id, RowVersion = rowVersion, Validation = validation };
    public static WorkflowAdminResult Fail(string error, bool conflict = false, bool forbidden = false, WorkflowValidationResult? validation = null) => new() { Error = error, IsConflict = conflict, IsForbidden = forbidden, Validation = validation };
}

public static class WorkflowManagementPermissions
{
    public const string Resource = "WorkflowManagement";
    public const string View = "View";
    public const string Edit = "Edit";
    public const string Validate = "Validate";
    public const string Publish = "Publish";
    public const string Bind = "Bind";
    public const string Retire = "Retire";
    public static readonly string[] All = { View, Edit, Validate, Publish, Bind, Retire };
}

public sealed class WorkflowDefinitionRow
{
    public int DefinitionId { get; set; }
    public string Name { get; set; } = "";
    public string? NameFa { get; set; }
    public string Module { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsActive { get; set; }
    public int? PublishedVersion { get; set; }
    public string? OpenDraftStatus { get; set; }
    public int? OpenDraftVersion { get; set; }
    public int ActiveBindings { get; set; }
    public string? BindingSummary { get; set; }
    public bool Editable { get; set; }
    public string EditableReason { get; set; } = "";
    public DateTime? LastModified { get; set; }
    public DateTime? LastPublished { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class WorkflowVersionRow
{
    public int VersionId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "";
    public int Stages { get; set; }
    public int Transitions { get; set; }
    public DateTime? PublishedDate { get; set; }
    public DateTime? RetiredDate { get; set; }
    public int? ClonedFromVersion { get; set; }
    public string? ChangeSummary { get; set; }
    public bool IsBound { get; set; }
    public int RunningInstances { get; set; }
    public int TotalInstances { get; set; }
}

public sealed class WorkflowBindingRow
{
    public int BindingId { get; set; }
    public string Module { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string Context { get; set; } = "";
    public bool IsActive { get; set; }
    public int? PinnedVersionId { get; set; }
    public int? PinnedVersionNumber { get; set; }
    public string DefinitionName { get; set; } = "";
    public int DefinitionId { get; set; }
    public bool Editable { get; set; }
}

public sealed class WorkflowUsageRow
{
    public int VersionNumber { get; set; }
    public int Running { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Rejected { get; set; }
}

public sealed class WorkflowAuditRow
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string? Actor { get; set; }
    public int? VersionNumber { get; set; }
    public string? Previous { get; set; }
    public string? Current { get; set; }
    public string? Details { get; set; }
}

public sealed class WorkflowDefinitionDetails
{
    public WorkflowDefinitionRow Definition { get; set; } = new();
    public string? Description { get; set; }
    public List<WorkflowVersionRow> Versions { get; set; } = new();
    public List<WorkflowBindingRow> Bindings { get; set; } = new();
    public List<WorkflowUsageRow> Usage { get; set; } = new();
    public List<WorkflowAuditRow> Audit { get; set; } = new();
    public int TotalInstances { get; set; }
}

public sealed class WorkflowGraphNode
{
    public int StageId { get; set; }
    public string Name { get; set; } = "";
    public string? NameFa { get; set; }
    public string? BusinessStatus { get; set; }
    public int Order { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public int Layer { get; set; }
    public bool IsStart { get; set; }
    public bool HasTerminal { get; set; }
}

public sealed class WorkflowGraphEdge
{
    public int TransitionId { get; set; }
    public int? FromStageId { get; set; }
    public int? ToStageId { get; set; }
    public string ActionType { get; set; } = "";
    public string? Label { get; set; }
    public string? LabelFa { get; set; }
    public string? RequiredPermission { get; set; }
    public string Kind { get; set; } = "Forward";   // Forward | Back | Start | Terminal
    public string Path { get; set; } = "";
    public double LabelX { get; set; }
    public double LabelY { get; set; }
}

public sealed class WorkflowGraphModel
{
    public List<WorkflowGraphNode> Nodes { get; set; } = new();
    public List<WorkflowGraphEdge> Edges { get; set; } = new();
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class WorkflowStageEditModel
{
    public int StageId { get; set; }
    public string Name { get; set; } = "";
    public string? NameFa { get; set; }
    public string? BusinessStatus { get; set; }
    public int StageOrder { get; set; }
    public string StageType { get; set; } = "Sequential";
    public string? ResponsibleRole { get; set; }
    public int? ResponsibleEmployeeId { get; set; }
    public string ApprovalStrategy { get; set; } = "AllApprovalsRequired";
    public int? MinimumApprovalCount { get; set; }
    public bool IsMandatory { get; set; } = true;
    public int? TimeoutHours { get; set; }
    public string? Description { get; set; }
}

public sealed class WorkflowTransitionEditModel
{
    public int TransitionId { get; set; }
    public int? FromStageId { get; set; }
    public int? ToStageId { get; set; }
    public string ActionType { get; set; } = "";
    public string? Label { get; set; }
    public string? LabelFa { get; set; }
    public string? RequiredPermission { get; set; }
    public string? ConditionExpression { get; set; }
    public int Priority { get; set; }
    public bool ForbidInitiator { get; set; }
}

public sealed class WorkflowDesignerModel
{
    public int DefinitionId { get; set; }
    public string DefinitionName { get; set; } = "";
    public string? DefinitionNameFa { get; set; }
    public int VersionId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "";
    public bool DefinitionEditable { get; set; }
    public string DefinitionEditableReason { get; set; } = "";
    /// <summary>true = Draft/Validated روی تعریف قابل‌ویرایش.</summary>
    public bool CanEditStructure { get; set; }
    public string RowVersion { get; set; } = "";
    public string? ChangeSummary { get; set; }
    public string? LastEditor { get; set; }
    public WorkflowGraphModel Graph { get; set; } = new();
    public List<WorkflowStageEditModel> Stages { get; set; } = new();
    public List<WorkflowTransitionEditModel> Transitions { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public List<string> ActionTypes { get; set; } = new();
    public List<(int Id, string Name)> Employees { get; set; } = new();
    public int RunningInstances { get; set; }
    public List<WorkflowAuditRow> Audit { get; set; } = new();
}

public sealed class WorkflowBindingImpact
{
    public int BindingId { get; set; }
    public int DefinitionId { get; set; }
    public string Definition { get; set; } = "";
    public int? CurrentVersion { get; set; }
    public int TargetVersion { get; set; }
    public List<WorkflowUsageRow> RunningByVersion { get; set; } = new();
    public int RunningInstances { get; set; }
}
