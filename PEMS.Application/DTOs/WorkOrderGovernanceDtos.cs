namespace PEMS.Application.DTOs;

/// <summary>کاربر اجراکننده (از سشن سمت‌سرور، هرگز از ورودی فرم).</summary>
public sealed record WorkOrderActor(int? EmployeeId, IReadOnlyList<string> Roles);

public sealed class WorkOrderCreateRequest
{
    public string WorkOrderNumber { get; set; } = "";
    public int EquipmentId { get; set; }
    public string WorkOrderType { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public string? Description { get; set; }
    public int? MaintenancePlanId { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>داده‌های تکمیل کار (قواعد دامنه‌ی Work Order؛ نه بخشی از موتور گردش‌کار).</summary>
public sealed class WorkOrderCompletionData
{
    public string? FailureModeCode { get; set; }
    public string? FailureCauseCode { get; set; }
    public string? RootCause { get; set; }
    public decimal? DowntimeHours { get; set; }
    public string? FailureEffect { get; set; }
    public string? CorrectiveAction { get; set; }
    public decimal? ContractorCost { get; set; }
    public string? ContractorName { get; set; }
}
