using Microsoft.EntityFrameworkCore;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Definitions;

// SCRUM-342 (W1 pilot, third page): the read-only list of the security events of administrator recovery (SecurityEventsController) as a page definition over a grid data source. The grid is read only;
// there is no write action. Same roles as the old page (SystemAdmin).

public sealed class SecurityEventRow
{
    public int Id { get; set; }
    public DateTime When { get; set; }
    public string EventType { get; set; } = "";
    public string Method { get; set; } = "";
    public string Result { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Target { get; set; } = "";
    public string Ip { get; set; } = "";

    public static readonly GridFields<SecurityEventRow> Fields = new GridFields<SecurityEventRow>()
        .Add("when", r => r.When).Add("eventType", r => r.EventType, searchable: true).Add("method", r => r.Method, searchable: true).Add("result", r => r.Result)
        .Add("reason", r => r.Reason, searchable: true).Add("actor", r => r.Actor, searchable: true).Add("target", r => r.Target, searchable: true).Add("ip", r => r.Ip).Add("id", r => r.Id)
        .TieBreak("id");
}

public sealed class SecurityEventsGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "security-events";
    public IReadOnlyCollection<string>? AllowedRoles => [SystemRoles.SystemAdmin];

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        // newest first unless the grid sorts: the sort of the request is applied after this default
        var query = db.SecurityEvents.AsNoTracking().Select(e => new SecurityEventRow
        {
            Id = e.SecurityEventId, When = e.OccurredAtUtc, EventType = e.EventType, Method = e.Method, Result = e.Result, Reason = e.FailureReason ?? "",
            Actor = e.ActorUserName ?? "", Target = e.TargetUserName ?? "", Ip = e.IpAddress ?? ""
        });
        var request2 = request.Grid.Sort.Count == 0 ? request.Grid with { Sort = [new GridSort("when", true)] } : request.Grid;
        var total = await query.ApplyGridFilters(request2, SecurityEventRow.Fields).CountAsync(ct);
        var rows = await query.ApplyGridFilters(request2, SecurityEventRow.Fields).ApplyGridSort(request2, SecurityEventRow.Fields).Paged(request2).ToListAsync(ct);
        return (rows.Cast<object>().ToList(), total);
    }
}
