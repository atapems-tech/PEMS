using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Permission Matrix: مجوزهای ریزدانه به‌ازای هر Role و هر ماژول (طبق سند Security Requirements)،
/// جدای دسترسی سطح-منو که در MenuAccess (PEMS.Web) پیاده‌سازی شده.
/// </summary>
public interface IPermissionService
{
    Task<List<RolePermissionDto>> GetMatrixAsync();

    /// <summary>ذخیره‌ی مجوزهای یک نقش برای چند ماژول در یک SaveChanges (Bulk Save).</summary>
    Task SetPermissionsAsync(string roleName, IEnumerable<ModulePermissionInput> permissions);

    Task<bool> HasPermissionAsync(IEnumerable<string> userRoles, string moduleName, string action);

    Task SeedDefaultsAsync();
}
