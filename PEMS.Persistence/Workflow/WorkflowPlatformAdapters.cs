using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Workflow;

/// <summary>
/// کاتالوگ مرجع Validator: نقش‌ها (AspNetRoles)، کارمندان، و جدول Permissions موجود PEMS.
/// </summary>
public class PemsWorkflowReferenceCatalog : IWorkflowReferenceCatalog
{
    private readonly PEMSDbContext _db;
    public PemsWorkflowReferenceCatalog(PEMSDbContext db) { _db = db; }

    public Task<bool> RoleExistsAsync(string role, CancellationToken ct = default)
        => _db.Roles.AnyAsync(r => r.Name == role, ct);

    public Task<bool> EmployeeExistsAsync(int employeeId, CancellationToken ct = default)
        => _db.Employees.AnyAsync(e => e.EmployeeId == employeeId, ct);

    public Task<bool> PermissionExistsAsync(string permissionName, CancellationToken ct = default)
        => _db.Permissions.AnyAsync(p => p.Name == permissionName, ct);
}

/// <summary>
/// هوک مجوز Runtime روی سیستم مجوز موجود (IAccessControlService). RequiredPermission به شکل
/// "Resource.Action" است. نقش‌های اجراکننده را سمت‌سرور خود فراخوان (سرویس ماژول) می‌دهد، نه کلاینت.
/// هیچ سیستم مجوز موازی ساخته نشده است.
/// </summary>
public class AccessControlWorkflowAuthorizationHook : IWorkflowAuthorizationHook
{
    private readonly IAccessControlService _access;
    public AccessControlWorkflowAuthorizationHook(IAccessControlService access) { _access = access; }

    public async Task<WorkflowAuthorizationResult> AuthorizeAsync(WorkflowAuthorizationContext context, CancellationToken ct = default)
    {
        var permission = context.Transition.RequiredPermission?.Trim();
        var dot = permission?.IndexOf('.') ?? -1;
        if (string.IsNullOrEmpty(permission) || dot <= 0 || dot == permission.Length - 1)
            return WorkflowAuthorizationResult.Deny("InvalidPermissionFormat");

        var decision = await _access.AuthorizeForRolesAsync(context.ActorRoles, permission[..dot], permission[(dot + 1)..]);
        if (decision.Allowed) return WorkflowAuthorizationResult.Allow();

        // متن کوتاه و هم‌زبان با فرهنگ درخواست (UI انگلیسی نباید متن فارسی داخلی AccessControl را ببیند)
        var fa = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";
        return WorkflowAuthorizationResult.Deny(fa ? $"مجوز «{permission}» لازم است." : $"Permission \"{permission}\" is required.");
    }
}
