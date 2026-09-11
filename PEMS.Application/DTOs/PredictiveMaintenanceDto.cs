namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۶: موتور نرم‌افزاری همیشه آماده است (EngineStatus=READY - معماری/
/// سرویس/Entity ها واقعاً وجود دارند) - آنچه واقعاً به اعتبارسنجی نیاز دارد ProductionModelStatus
/// است که تا وقتی یک مدل با داده‌ی واقعی اعتبارسنجی نشده، همیشه NOT_READY می‌ماند.
/// </summary>
public class PredictiveMaintenanceReadinessDto
{
    public string EngineStatus { get; set; } = "READY";
    public string ProductionModelStatus { get; set; } = "NOT_READY";
    public int ModelCount { get; set; }
    public int ValidatedModelCount { get; set; }
    public int ResultCount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PredictiveMaintenanceModelDto
{
    public int PredictiveMaintenanceModelId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TrainingDataStatus { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public int ResultCount { get; set; }
}
