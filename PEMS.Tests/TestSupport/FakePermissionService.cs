using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// پیاده‌سازی ساده‌ی IPermissionService برای تست کنترلرهایی که HasPermissionAsync دارند -
/// عمداً موتور واقعی AccessControlService/RolePermissionGrant را دور می‌زند (که جای خودش
/// تست شده) و فقط رفتار Controller در برابر Allow/Deny را می‌سنجد. پیش‌فرض Allow=true.
/// </summary>
public class FakePermissionService : IPermissionService
{
    public bool AllowAll { get; set; } = true;
    public HashSet<(string Module, string Action)> Denied { get; } = new();

    public Task<bool> HasPermissionAsync(IEnumerable<string> userRoles, string moduleName, string action)
    {
        if (Denied.Contains((moduleName, action))) return Task.FromResult(false);
        return Task.FromResult(AllowAll);
    }

    public Task<List<RolePermissionDto>> GetMatrixAsync() => Task.FromResult(new List<RolePermissionDto>());

    public Task SetPermissionsAsync(string roleName, IEnumerable<ModulePermissionInput> permissions) => Task.CompletedTask;

    public Task SeedDefaultsAsync() => Task.CompletedTask;
}
