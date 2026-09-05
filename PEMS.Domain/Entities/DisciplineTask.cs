using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// Task/Activity/Deliverable سطح یک Discipline، طبق سند راهنما
/// ("هر Discipline باید Tasks, Activities, Deliverables, Responsibility, KPI, Dashboard داشته باشد").
/// از یک Entity واحد با فیلد Type برای هر سه نوع استفاده شده تا مدل ساده بماند.
/// </summary>
public class DisciplineTask
{
    [Key]
    public int DisciplineTaskId { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Task, Activity, Deliverable
    public string Type { get; set; } = "Task";

    public int? ResponsibleEmployeeId { get; set; }
    public Employee? ResponsibleEmployee { get; set; }

    public DateTime? DueDate { get; set; }

    // Open, InProgress, Completed
    public string Status { get; set; } = "Open";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? CompletedDate { get; set; }
}
