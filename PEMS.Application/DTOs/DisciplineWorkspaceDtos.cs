namespace PEMS.Application.DTOs;

public class DisciplineSummaryDto
{
    public int DisciplineId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DocumentsCount { get; set; }
    public int PendingDocumentsCount { get; set; }
    public int EquipmentCount { get; set; }
    public int VDRItemsCount { get; set; }
    public int OpenTasksCount { get; set; }
    public int OverdueTasksCount { get; set; }
}

public class DisciplineTaskDto
{
    public int DisciplineTaskId { get; set; }
    public int DisciplineId { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "Task";
    public string? ResponsibleName { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public bool IsOverdue { get; set; }
}

/// <summary>
/// Discipline Workspace: نمای اختصاصی هر Discipline طبق سند راهنما (بخش ۵) —
/// دیتابیس مشترک است، فقط نمای فیلترشده جدا است (Workspace جدا، Database جدا نیست).
/// </summary>
public class DisciplineWorkspaceDto
{
    public DisciplineSummaryDto Summary { get; set; } = new();
    public List<EngineeringDocumentDto> Documents { get; set; } = new();
    public List<EquipmentDto> Equipment { get; set; } = new();
    public List<VDRItemDto> VDRItems { get; set; } = new();
    public List<MRDto> MRs { get; set; } = new();
    public List<DisciplineTaskDto> Tasks { get; set; } = new();
}
