using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.UI.Abstractions;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Definitions;

// SCRUM-437 (#437): the two handlers that make "a list and a button on a page from stored definitions only" real: grid rows of the Departments (the first grid data source) and a button that runs a workflow
// action. Both are registered code that a definition names; neither is reachable without the rights of the user.

/// <summary>The rows of the department grid: the standard grid query over the departments with the count of active employees. Used by the data source <c>departments-grid</c> and by <c>DepartmentController.Grid</c>.</summary>
public sealed class DepartmentsGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "departments";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin, SystemRoles.EngineeringManager];

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await QueryAsync(scope.ServiceProvider.GetRequiredService<PEMSDbContext>(), request.Grid, ct);
    }

    public static async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(PEMSDbContext db, GridRequest request, CancellationToken ct)
    {
        var counts = await db.Employees.Where(x => x.IsActive).GroupBy(x => x.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var query = db.Departments.AsNoTracking().Select(d => new DepartmentRow { Id = d.DepartmentId, Code = d.Code, Name = d.Name, Description = d.Description, Active = d.IsActive, Created = d.CreatedDate });
        var total = await query.ApplyGridFilters(request, DepartmentRow.Fields).CountAsync(ct);
        var rows = await query.ApplyGridFilters(request, DepartmentRow.Fields).ApplyGridSort(request, DepartmentRow.Fields).Paged(request).ToListAsync(ct);
        foreach (var r in rows) r.Employees = counts.TryGetValue(r.Id, out var n) ? n : 0;
        return (rows.Cast<object>().ToList(), total);
    }
}

public sealed class DepartmentRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public int Employees { get; set; }

    public static readonly GridFields<DepartmentRow> Fields = new GridFields<DepartmentRow>()
        .Add("name", r => r.Name, searchable: true).Add("code", r => r.Code, searchable: true).Add("description", r => r.Description!, searchable: true)
        .Add("active", r => r.Active).Add("created", r => r.Created).Add("id", r => r.Id).TieBreak("id");
}

/// <summary>
/// <c>workflow.execute</c>: a button runs one action of a workflow instance (approve, reject, return ...). The action definition lists in <c>parameters.objectTypes</c> the business object types it may act on
/// (a module that keeps its own status in step with the workflow keeps its own service and is simply not listed); the handler then checks that the engine offers this action to this user and that a reason is
/// given where the shared rule asks for one, and runs it through the runtime, which applies the permission, segregation of duties and conditions again.
/// Values: <c>instanceId</c>, <c>actionType</c>, <c>comment</c>.
/// </summary>
public sealed class WorkflowExecuteAction(IServiceScopeFactory scopes) : IActionHandler
{
    public string Key => "workflow.execute";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        if (parameters?["objectTypes"] is not JsonArray types || types.Count == 0 || types.Any(t => t is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s)))
            return [new("/objectTypes", "required", "Def_Err_Required", "workflow.execute needs parameters.objectTypes: the business object types it may act on (a non-empty list of names).")];
        return [];
    }

    private static TranslatableText Msg(string en, string fa) => TranslatableText.Of(("en", en), ("fa", fa));

    public async Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct)
    {
        if (!int.TryParse(invocation.Values.GetValueOrDefault("instanceId"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var instanceId))
            return ActionOutcome.Refused(Msg("The workflow instance is missing.", "نمونه‌ی گردش‌کار مشخص نیست."));
        var actionType = invocation.Values.GetValueOrDefault("actionType")?.Trim();
        if (string.IsNullOrEmpty(actionType)) return ActionOutcome.Refused(Msg("The action is missing.", "اقدام مشخص نیست."));
        var comment = WorkflowActionRules.Normalize(invocation.Values.GetValueOrDefault("comment"));
        var reasonError = WorkflowActionRules.ValidateAction(actionType, comment);
        if (reasonError is not null) return ActionOutcome.Refused("comment", TranslatableText.Literal(reasonError));

        using var scope = scopes.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IWorkflowRuntime>();
        var readModel = scope.ServiceProvider.GetRequiredService<IWorkflowReadModelService>();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();

        var instance = await runtime.GetInstanceByIdAsync(instanceId);
        var allowedTypes = (invocation.Parameters?["objectTypes"] as JsonArray)?.Select(t => (string?)t).ToHashSet(StringComparer.Ordinal) ?? [];
        if (instance is null || !allowedTypes.Contains(instance.BusinessObjectType)) return ActionOutcome.Refused(Msg("This workflow cannot be acted on from here.", "این گردش‌کار از اینجا قابل اقدام نیست."));
        if (instance.Status != "Running") return ActionOutcome.Refused(Msg($"The workflow is {instance.Status}.", $"گردش‌کار در وضعیت «{instance.Status}» است."));

        var employeeId = invocation.UserName is null ? null : await db.Users.Where(u => u.UserName == invocation.UserName).Select(u => u.EmployeeId).FirstOrDefaultAsync(ct);
        var roles = invocation.Roles.ToList();
        var actions = await readModel.GetAvailableActionsAsync(instanceId, new WorkflowUserContext(employeeId, roles), ct);
        var action = actions.FirstOrDefault(a => a.ActionType == actionType);
        if (action is null) return ActionOutcome.Refused(Msg($"The action \"{actionType}\" is not available to you at the current stage.", $"اقدام «{actionType}» در مرحله‌ی جاری برای شما در دسترس نیست."));
        if (!action.IsAllowed) return ActionOutcome.Refused(Msg($"The action \"{actionType}\" is not allowed ({action.DeniedReason}).", $"اقدام «{actionType}» مجاز نیست ({action.DeniedReason})."));

        try { await runtime.ExecuteActionAsync(instanceId, actionType, employeeId, roles, comment, null, null); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return ActionOutcome.Refused(TranslatableText.Literal(ex.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return ActionOutcome.Refused(Msg("Someone else changed this record at the same time; reload and try again.", "رکورد هم‌زمان توسط کاربر دیگری تغییر کرد؛ دوباره بارگذاری کنید."));
        }
        return ActionOutcome.Done();
    }
}
