namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۶: Authorize(User, Resource, Action,
/// ResourceId, Context) - درخواست تصمیم مجوزدهی کامل (آگاه از کاربر، شامل Scope/SoD/Workflow).
/// </summary>
public class AccessControlRequest
{
    public int UserId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public IDictionary<string, string>? Context { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// طبق بخش ۱۶ سند: نتیجه باید دلیل رد را هم مشخص کند - Allow/DeniedByRole/DeniedByScope/
/// DeniedBySoD/DeniedByWorkflowState/DeniedByOwnership/DeniedByPolicy.
/// </summary>
public enum AccessDecisionReason
{
    Allow,
    DeniedByRole,
    DeniedByScope,
    DeniedBySoD,
    DeniedByWorkflowState,
    DeniedByOwnership,
    DeniedByPolicy
}

public class AccessDecision
{
    public bool Allowed { get; set; }
    public AccessDecisionReason Reason { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? MatchedRoleName { get; set; }

    // طبق سند بخش ۱۱ (فاز ۲): کدام Scope دقیقاً منجر به Allow شد - برای Audit/Simulator آینده
    public string? MatchedScopeType { get; set; }
    public string? MatchedScopeValue { get; set; }

    public static AccessDecision Allow(string? matchedRoleName = null, string detail = "", string? scopeType = null, string? scopeValue = null) =>
        new()
        {
            Allowed = true,
            Reason = AccessDecisionReason.Allow,
            MatchedRoleName = matchedRoleName,
            Detail = detail,
            MatchedScopeType = scopeType,
            MatchedScopeValue = scopeValue
        };

    public static AccessDecision Deny(AccessDecisionReason reason, string detail) =>
        new() { Allowed = false, Reason = reason, Detail = detail };
}
