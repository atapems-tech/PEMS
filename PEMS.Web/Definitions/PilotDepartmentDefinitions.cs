using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.UI.Abstractions;

namespace PEMS.Web.Definitions;

// SCRUM-437/578 pilot (Admin create form): the department create form is the definition files form/core/department-create.json, data-source/core/departments.json and action/core/department-create.json;
// this is the registered code those definitions name. Same rights and same rules as DepartmentController.Create (roles, trimmed values, no duplicate name or code).

/// <summary>The departments as choices (value = id, label = name), for a parent department field.</summary>
public sealed class DepartmentsDataSource(IServiceScopeFactory scopes) : IDataSourceProvider
{
    public string Key => "departments";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin, SystemRoles.EngineeringManager];

    public async Task<IReadOnlyList<FieldOption>> LoadAsync(DataSourceRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var rows = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.DepartmentId, d.Name }).ToListAsync(ct);
        return rows.Select(r => new FieldOption { Value = r.DepartmentId.ToString(System.Globalization.CultureInfo.InvariantCulture), Label = TranslatableText.Literal(r.Name) }).ToList();
    }
}

/// <summary>Creates a department from the values of the form (code, name, description, parentDepartmentId).</summary>
public sealed class DepartmentCreateAction(IServiceScopeFactory scopes) : IActionHandler
{
    public string Key => "department.create";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin, SystemRoles.EngineeringManager];

    public async Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct)
    {
        var name = invocation.Values.GetValueOrDefault("name")?.Trim() ?? "";
        var code = invocation.Values.GetValueOrDefault("code")?.Trim() ?? "";
        var description = invocation.Values.GetValueOrDefault("description")?.Trim();
        if (name.Length == 0) return ActionOutcome.Refused("name", TranslatableText.Of(("en", "The department name is required."), ("fa", "نام دپارتمان الزامی است.")));

        int? parentId = null;
        if (int.TryParse(invocation.Values.GetValueOrDefault("parentDepartmentId"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var p)) parentId = p;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        if (parentId is not null && !await db.Departments.AnyAsync(d => d.DepartmentId == parentId, ct))
            return ActionOutcome.Refused("parentDepartmentId", TranslatableText.Of(("en", "The parent department does not exist."), ("fa", "دپارتمان والد وجود ندارد.")));

        var lower = name.ToLower();
        var lowerCode = code.ToLower();
        if (await db.Departments.AnyAsync(d => d.Name.ToLower() == lower || (code != "" && d.Code.ToLower() == lowerCode), ct))
            return ActionOutcome.Refused("name", TranslatableText.Of(("en", "A department with this name or code already exists."), ("fa", "دپارتمانی با همین نام یا کد از قبل وجود دارد.")));

        db.Departments.Add(new Department { Code = code, Name = name, Description = string.IsNullOrEmpty(description) ? null : description, ParentDepartmentId = parentId, IsActive = true, CreatedDate = DateTime.Now });
        await db.SaveChangesAsync(ct);
        return ActionOutcome.Done();
    }
}
