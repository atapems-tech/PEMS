namespace PEMS.Application.DTOs;

public class ExecutionGateResultDto
{
    public string Result { get; set; } = ""; // Eligible, Blocked
    public List<ExecutionGateCheckDto> Checks { get; set; } = new();
    public List<ExecutionGateCheckDto> AdvisoryNotes { get; set; } = new();
}

public class ExecutionGateCheckDto
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = ""; // Required, Conditional
    public bool Passed { get; set; }
    public string Detail { get; set; } = "";
}

public class ExecutionGateDecisionDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Result { get; set; } = "";
    public string? ActorName { get; set; }
    public string? BlockingReasonsSummary { get; set; }
    public bool IsEmergencyOverride { get; set; }
    public string? EmergencyReason { get; set; }
    public DateTime DecisionDate { get; set; }
}
