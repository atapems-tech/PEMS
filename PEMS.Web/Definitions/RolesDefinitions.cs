using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.UI.Abstractions;

namespace PEMS.Web.Definitions;

// SCRUM-342 (W1 pilot, second pair): the Roles list and the role create form as definitions (page/core/roles.json, form/core/role-create.json, data-source/core/roles-grid.json, action/core/role-create.json).
// This is the registered code they name; same roles (SystemAdmin only) and same rules as RolesController: a new role is Default-Deny and the user is sent to its permissions.

public sealed class RoleRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Category { get; set; } = "";
    public bool System { get; set; }
    public int Users { get; set; }
    /// <summary>The active permission grants; -1 for SystemAdmin (unlimited, not "unconfigured").</summary>
    public int Permissions { get; set; }
    public int Order { get; set; }

    public static readonly GridFields<RoleRow> Fields = new GridFields<RoleRow>()
        .Add("name", r => r.Name, searchable: true).Add("displayName", r => r.DisplayName, searchable: true).Add("description", r => r.Description, searchable: true)
        .Add("domain", r => r.Domain).Add("category", r => r.Category).Add("system", r => r.System).Add("users", r => r.Users).Add("permissions", r => r.Permissions).Add("order", r => r.Order).Add("id", r => r.Id)
        .TieBreak("id");
}

public sealed class RolesGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "roles";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin];

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var roles = await db.Roles.AsNoTracking().ToListAsync(ct);
        var users = await db.UserRoles.GroupBy(ur => ur.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var grants = await db.RolePermissionGrants.Where(g => g.IsActive).GroupBy(g => g.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var rows = roles.Select(r => new RoleRow
        {
            Id = r.Id, Name = r.Name ?? "", DisplayName = r.DisplayName ?? "", Description = r.Description ?? "", Domain = r.Domain ?? "", Category = r.Category ?? "",
            System = r.IsSystemRole || SystemRoles.All.Contains(r.Name), Users = users.GetValueOrDefault(r.Id), Order = r.SortOrder == 0 ? int.MaxValue : r.SortOrder,
            Permissions = r.Name == SystemRoles.SystemAdmin ? -1 : grants.GetValueOrDefault(r.Id)
        }).AsQueryable();
        var total = rows.ApplyGridFilters(request.Grid, RoleRow.Fields).Count();
        var page = rows.ApplyGridFilters(request.Grid, RoleRow.Fields).ApplyGridSort(request.Grid, RoleRow.Fields).Paged(request.Grid).Cast<object>().ToList();
        return (page, total);
    }
}

/// <summary>Creates a role from the form values (name, displayName, description): the same rules as <c>RolesController.Create</c>.</summary>
public sealed class RoleCreateAction(IServiceScopeFactory scopes) : IActionHandler
{
    public string Key => "role.create";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin];

    public async Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct)
    {
        var name = invocation.Values.GetValueOrDefault("name")?.Trim() ?? "";
        if (name.Length == 0) return ActionOutcome.Refused("name", TranslatableText.Of(("en", "The role name is required."), ("fa", "نام نقش الزامی است.")));
        using var scope = scopes.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (await roleManager.RoleExistsAsync(name))
            return ActionOutcome.Refused("name", TranslatableText.Of(("en", "A role with this name already exists."), ("fa", "نقشی با این نام از قبل وجود دارد.")));
        var display = invocation.Values.GetValueOrDefault("displayName")?.Trim();
        var description = invocation.Values.GetValueOrDefault("description")?.Trim();
        var created = await roleManager.CreateAsync(new ApplicationRole(name) { DisplayName = string.IsNullOrEmpty(display) ? null : display, Description = string.IsNullOrEmpty(description) ? null : description });
        if (!created.Succeeded) return ActionOutcome.Refused(TranslatableText.Literal(string.Join(" ", created.Errors.Select(e => e.Description))));
        // a new role is Default-Deny: no permission is written for it; the user goes straight to its permissions
        return ActionOutcome.Done(TranslatableText.Of(("en", "Role created. Configure its permissions below before assigning it to users."), ("fa", "نقش ایجاد شد. پیش از اختصاص به کاربران، مجوزهای آن را در همین بخش تنظیم کنید.")),
            "/Admin/PermissionMatrix#role-" + Uri.EscapeDataString(name));
    }
}
