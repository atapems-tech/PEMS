using PEMS.Application.DTOs;

namespace PEMS.Web.Models;

/// <summary>
/// طبق Phase E-05D: مدل ترکیب صفحه - DTO های موجود E-03/E-05C را مستقیم حمل می‌کند
/// (EngineeringDocumentSummaryCountsDto/EngineeringRevisionQualityDto)، نه یک کپی موازی.
/// </summary>
public class EngineeringDashboardViewModel
{
    public EngineeringDocumentSummaryCountsDto Summary { get; set; } = new();
    public List<EngineeringRevisionQualityDto> Quality { get; set; } = new();

    // null = هیچ سندی هنوز یک چرخه را کامل نکرده - نه صفر درصد
    public double? FirstPassApprovalRate { get; set; }

    public int EngineeringHealthScore { get; set; }
    public List<EngineeringAttentionQueueItem> AttentionQueue { get; set; } = new();
    public List<EngineeringDisciplinePerformanceItem> DisciplinePerformance { get; set; } = new();

    public int? SelectedProjectId { get; set; }
    public int? SelectedDisciplineId { get; set; }
    public int? SelectedMinRevisionCount { get; set; }
}

public class EngineeringAttentionQueueItem
{
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = "";
    public string DisciplineName { get; set; } = "-";
    public string Reason { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public int RevisionCount { get; set; }
    public int RejectionCount { get; set; }
}

public class EngineeringDisciplinePerformanceItem
{
    public string DisciplineName { get; set; } = "-";
    public int DocumentCount { get; set; }
    public int RejectionCount { get; set; }
    public int RevisionCount { get; set; }
    public int RiskScore { get; set; }
    public double RejectionRate { get; set; }
}
