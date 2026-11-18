using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Context;
using PEMS.Persistence.Definitions;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.UI.Manifest;
using PEMS.UI.Rendering;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-342 (W1 pilot, second pair): the Roles list is a page definition over a grid data source, the role create form is a form definition with a handler that applies the rules of RolesController.</summary>
public sealed class RolesDefinitionsTests
{
    private static ServiceProvider Services(string database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => PemsDbContextFactory.CreateInMemory(database));
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<PEMSDbContext>();
        return services.BuildServiceProvider();
    }

    private static GridDataRequest Query(string query) => new(GridRequest.FromQuery(query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')).Select(p => new KeyValuePair<string, string?>(p[0], p.Length > 1 ? p[1] : ""))), "en", ["SystemAdmin"], "hamid", new Dictionary<string, string?>());

    private static async Task Seed(string database)
    {
        using var db = PemsDbContextFactory.CreateInMemory(database);
        db.Roles.AddRange(new ApplicationRole(SystemRoles.SystemAdmin) { NormalizedName = "SYSTEMADMIN", DisplayName = "System administrator", SortOrder = 1 },
            new ApplicationRole("Inspector") { NormalizedName = "INSPECTOR", DisplayName = "Inspector", Description = "Field inspections", Category = "Functional", SortOrder = 0 },
            new ApplicationRole("Planner") { NormalizedName = "PLANNER", DisplayName = "Planner", SortOrder = 5 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_roles_provider_pages_sorts_searches_and_marks_the_system_administrator_as_unlimited()
    {
        var name = "roles-" + Guid.NewGuid();
        await Seed(name);
        var provider = new RolesGridProvider(Services(name).GetRequiredService<IServiceScopeFactory>());

        var (all, total) = await provider.QueryAsync(Query("?sort=name&pageSize=2"), CancellationToken.None);
        Assert.Equal(3, total);
        Assert.Equal(["Inspector", "Planner"], all.Cast<RoleRow>().Select(r => r.Name));
        Assert.Equal(["Inspector"], (await provider.QueryAsync(Query("?q=field"), CancellationToken.None)).Items.Cast<RoleRow>().Select(r => r.Name));
        var admin = (await provider.QueryAsync(Query("?f.name=eq:SystemAdmin"), CancellationToken.None)).Items.Cast<RoleRow>().Single();
        Assert.Equal((-1, true), (admin.Permissions, admin.System));      // unlimited, not "unconfigured"
        Assert.Equal(["SystemAdmin"], provider.AllowedRoles!);
        await Assert.ThrowsAsync<GridQueryException>(() => provider.QueryAsync(Query("?sort=passwordHash"), CancellationToken.None));
    }

    private static ActionInvocation Invoke(params (string, string?)[] values) => new(values.ToDictionary(v => v.Item1, v => v.Item2), "en", ["SystemAdmin"], "hamid");

    [Fact]
    public async Task Creating_a_role_trims_the_values_refuses_a_duplicate_and_sends_the_user_to_its_permissions()
    {
        var name = "roles-" + Guid.NewGuid();
        var action = new RoleCreateAction(Services(name).GetRequiredService<IServiceScopeFactory>());

        var created = await action.ExecuteAsync(Invoke(("name", " Auditor "), ("displayName", " Auditor role "), ("description", "")), CancellationToken.None);
        Assert.True(created.Ok, created.FormError?.ToString());
        Assert.Equal("/Admin/PermissionMatrix#role-Auditor", created.Redirect);
        Assert.Contains("permissions", created.Message!.Resolve(new CultureInfo("en")));
        Assert.Contains("مجوز", created.Message.Resolve(new CultureInfo("fa")));

        using var db = PemsDbContextFactory.CreateInMemory(name);
        var role = await db.Roles.SingleAsync();
        Assert.Equal(("Auditor", "Auditor role", null), (role.Name, role.DisplayName, role.Description));
        Assert.Empty(await db.RolePermissionGrants.ToListAsync());          // Default-Deny: nothing is granted to a new role

        var again = await action.ExecuteAsync(Invoke(("name", "Auditor")), CancellationToken.None);
        Assert.False(again.Ok);
        Assert.Equal("A role with this name already exists.", again.FieldErrors!["name"].Resolve(new CultureInfo("en")));
        Assert.False((await action.ExecuteAsync(Invoke(("name", " ")), CancellationToken.None)).Ok);
        Assert.Equal(["SystemAdmin"], action.AllowedRoles!);
    }

    [Fact]
    public async Task The_roles_page_and_form_are_shipped_definitions_that_render_and_bind_to_their_data_source_and_action()
    {
        var ui = PEMS.UI.ServiceCollectionExtensions.AddPemsUi(new ServiceCollection()).BuildServiceProvider().CreateScope().ServiceProvider;
        var registry = ui.GetRequiredService<IPemsComponentRegistry>();
        var catalog = ui.GetRequiredService<ComponentManifestCatalog>();
        var kinds = new IDefinitionKind[] { new FormDefinitionKind(), new PageDefinitionKind(registry, catalog), new LayoutDefinitionKind(),
            new DataSourceDefinitionKind([new DepartmentsDataSource(null!), new EntityOptionsProvider(null!, EntityCatalog.Default())], [new DepartmentsGridProvider(null!), new RolesGridProvider(null!), new SecurityEventsGridProvider(null!), new EntityGridProvider(null!, EntityCatalog.Default())]), new ActionDefinitionKind([new DepartmentCreateAction(null!), new RoleCreateAction(null!), new EntityCreateAction(null!, EntityCatalog.Default())]) };
        using var files = new FileDefinitionStore(kinds, new DefinitionLimits(), new DefinitionFileOptions { Folder = Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName), Watch = false }, FilesOwnership.For(kinds));
        var page = (await files.GetPublishedAsync("page", "roles"))!;
        var renderer = new PageRenderer(ui.GetRequiredService<IPemsComponentRenderer>(), catalog, files);
        var html = System.Net.WebUtility.HtmlDecode((await renderer.RenderAsync(new ResolvedDefinition(page, JsonNode.Parse(page.PayloadJson)!), new PipelineRequest("page", "roles", "core", "en", ["SystemAdmin"], "hamid"), new CultureInfo("en"))).Html);
        Assert.Contains("/d/roles-grid", html);
        Assert.Contains("href=\"/f/role-create\"", html);
        Assert.Contains("New role", html);

        var form = (await files.GetPublishedAsync("form", "role-create"))!;
        var refs = kinds.First(k => k.Key == "form").References(form.PayloadJson);
        Assert.Contains(refs, r => r is { ToKind: "action", ToKey: "role-create" });
        var action = (await files.GetPublishedAsync("action", "role-create"))!;
        Assert.Contains("role.create", action.PayloadJson);
    }
}

/// <summary>SCRUM-342 (W1 pilot, third page): the security events list as a read-only page definition.</summary>
public sealed class SecurityEventsPageTests
{
    [Fact]
    public async Task The_provider_lists_newest_first_filters_and_searches_and_only_the_system_administrator_may_read_it()
    {
        var name = "sec-" + Guid.NewGuid();
        using (var db = PemsDbContextFactory.CreateInMemory(name))
        {
            db.SecurityEvents.AddRange(
                new PEMS.Domain.Entities.SecurityEvent { EventType = "Reset", Method = "RecoveryCode", Result = "Success", ActorUserName = "admin", OccurredAtUtc = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc) },
                new PEMS.Domain.Entities.SecurityEvent { EventType = "Reset", Method = "BreakGlass", Result = "Failure", FailureReason = "expired", TargetUserName = "root", OccurredAtUtc = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }
        var services = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(name)).BuildServiceProvider();
        var provider = new SecurityEventsGridProvider(services.GetRequiredService<IServiceScopeFactory>());
        GridDataRequest Ask(params (string, string)[] q) => new(GridRequest.FromQuery(q.Select(x => new KeyValuePair<string, string?>(x.Item1, x.Item2))), "en", ["SystemAdmin"], "x", new Dictionary<string, string?>());

        var all = await provider.QueryAsync(Ask(), CancellationToken.None);
        Assert.Equal(2, all.Total);
        Assert.Equal(["BreakGlass", "RecoveryCode"], all.Items.Cast<SecurityEventRow>().Select(r => r.Method));   // newest first by default
        Assert.Equal(["RecoveryCode"], (await provider.QueryAsync(Ask(("f.result", "eq:Success")), CancellationToken.None)).Items.Cast<SecurityEventRow>().Select(r => r.Method));
        Assert.Equal(["BreakGlass"], (await provider.QueryAsync(Ask(("q", "expired")), CancellationToken.None)).Items.Cast<SecurityEventRow>().Select(r => r.Method));
        Assert.Equal(["SystemAdmin"], provider.AllowedRoles!);
    }
}
