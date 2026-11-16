using PEMS.Application.Definitions;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Definitions;

/// <summary>Stage 2 of the pipeline with the product's permission service: a definition names its permission as "Module.Action"; none named means no extra right is needed.</summary>
public sealed class PermissionDefinitionAccess(IPermissionService permissions) : IDefinitionAccess
{
    public async Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(permission)) return true;
        var dot = permission.LastIndexOf('.');
        if (dot <= 0 || dot == permission.Length - 1) return false;   // a malformed permission never opens the page
        return await permissions.HasPermissionAsync(roles, permission[..dot], permission[(dot + 1)..]);
    }
}
