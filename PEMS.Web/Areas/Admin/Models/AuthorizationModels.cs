using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Areas.Admin.Models;

// طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۱۵-۱۹: این مدل‌ها فقط برای نمایش‌اند -
// هیچ منطق مجوزدهی‌ای این‌جا محاسبه نمی‌شود، همه چیز از IAccessControlService/IPermissionService
// (موتور واقعی) خوانده می‌شود.

public class EffectivePermissionRowVm
{
    public string ResourceName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string PermissionName => $"{ResourceName}.{ActionName}";
    public bool Allowed { get; set; }
    public bool IsDestructive { get; set; }
    public string? ScopeType { get; set; }
    public string? ScopeValue { get; set; }
    public string? Detail { get; set; }
}

public class EffectiveRoleVm
{
    public string[] AllRoles { get; set; } = Array.Empty<string>();
    public string? SelectedRole { get; set; }
    public List<EffectivePermissionRowVm> Rows { get; set; } = new();

    // طبق «PEMS Authorization Architecture Completion» فاز ۴: شمارش این‌جا (نه با یک Lambda
    // مستقیم در cshtml) - صرفاً یک ملاحظه‌ی سبک‌بندی، نه تصمیم مجوزدهی.
    public int AllowedCount => Rows.Count(r => r.Allowed);
}

public class RoleAssignmentRowVm
{
    public string RoleName { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeValue { get; set; }
    public string AssignmentType { get; set; } = string.Empty;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class EffectiveUserVm
{
    public List<(int Id, string Display)> AllUsers { get; set; } = new();
    public int? SelectedUserId { get; set; }
    public List<string> RoleNames { get; set; } = new();
    public List<RoleAssignmentRowVm> Assignments { get; set; } = new();
    public List<EffectivePermissionRowVm> Rows { get; set; } = new();
    public int AllowedCount => Rows.Count(r => r.Allowed);
}

public class SimulateFormVm
{
    [Required, Display(Name = "User")]
    public int? UserId { get; set; }

    [Required, Display(Name = "Resource")]
    public string ResourceName { get; set; } = string.Empty;

    [Required, Display(Name = "Action")]
    public string ActionName { get; set; } = string.Empty;

    [Display(Name = "Resource ID (optional)")]
    public string? ResourceId { get; set; }

    public List<(int Id, string Display)> AllUsers { get; set; } = new();
    public List<string> AllPermissionNames { get; set; } = new();

    public SimulateResultVm? Result { get; set; }
}

public class SimulateResultVm
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? MatchedRoleName { get; set; }
    public string? MatchedScopeType { get; set; }
    public string? MatchedScopeValue { get; set; }
}
