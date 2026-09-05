namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS-10a ADR-02: پایه‌ی مجوزدهی عمومی Role/Module/Action - موازی با IPermissionService
/// موجود (که برای ۱۲ ماژول CRUD-محور دست‌نخورده می‌ماند)، برای ماژول‌هایی که مجموعه‌عمل‌شان از
/// View/Create/Edit/Delete/Approve فراتر می‌رود.
/// </summary>
public interface IRoleModuleActionService
{
    Task<bool> HasActionAsync(IEnumerable<string> userRoles, string module, string action);

    Task SeedDefaultsAsync();
}
