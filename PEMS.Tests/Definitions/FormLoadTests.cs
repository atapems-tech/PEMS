using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Persistence.Definitions;
using PEMS.Tests.TestSupport;
using PEMS.UI.Rendering;
using PEMS.Web.Controllers;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public sealed record LoadTestRow(int Id, string Title, string Note, bool Active, DateTime Due);

public interface IFormLoadTestService
{
    [PemsOperation("FormLoadTest", "One")]
    Task<LoadTestRow?> OneAsync(int id);

    [PemsOperation("FormLoadTest", "ByCode")]
    Task<LoadTestRow?> ByCodeAsync(string code);
}

public sealed class FormLoadTestService : IFormLoadTestService
{
    public Task<LoadTestRow?> OneAsync(int id) => Task.FromResult<LoadTestRow?>(id == 5 ? new LoadTestRow(5, "Pump overhaul", "check the seal", true, new DateTime(2026, 11, 20)) : null);
    public Task<LoadTestRow?> ByCodeAsync(string code) => Task.FromResult<LoadTestRow?>(code == "P-1" ? new LoadTestRow(9, "By code", "", false, new DateTime(2026, 11, 21, 9, 30, 0)) : null);
}

/// <summary>SCRUM-818: an edit form loads its saved values from a read operation. <c>/f/{key}/{id}</c> runs the operation with the id (or the sources the form names), fills the fields of the same name from the first row, and is a 404 when there is no row.</summary>
public sealed class FormLoadTests
{
    private static readonly OperationCatalog Operations = OperationCatalog.Scan(typeof(FormLoadTests).Assembly);

    private sealed class InlineStore(string loadJson) : IDefinitionStore
    {
        private string Form => """
            {"id":"item-edit","title":{"fa":"ویرایش","en":"Edit item"},"submitLabel":{"fa":"ذخیره","en":"Save"},"actionKey":"item-edit",
             "fields":[
              {"name":"title","label":{"fa":"عنوان","en":"Title"},"kind":"text"},
              {"name":"note","label":{"fa":"یادداشت","en":"Note"},"kind":"text"},
              {"name":"active","label":{"fa":"فعال","en":"Active"},"kind":"checkbox"},
              {"name":"due","label":{"fa":"سررسید","en":"Due"},"kind":"date"},
              {"name":"other","label":{"fa":"دیگر","en":"Other"},"kind":"text"}]
             ,"load":LOAD}
            """.Replace("LOAD", loadJson);

        public Task<PublishedDefinition?> GetPublishedAsync(string kind, string key, string appKey = "core", CancellationToken ct = default)
            => Task.FromResult<PublishedDefinition?>(kind == "form" && key == "item-edit" ? new PublishedDefinition(kind, key, appKey, 1, Form, "h", """{"fa":"ویرایش","en":"Edit item"}""") : null);
    }

    private sealed class Allow : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class Tokens : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("request-token", "cookie-token", "__RequestVerificationToken", "X-CSRF");
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => GetAndStoreTokens(httpContext);
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private sealed class Urls : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext c) => "/f/item-edit";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => !string.IsNullOrEmpty(url) && url[0] == '/' && !url.StartsWith("//");
        public string? RouteUrl(UrlRouteContext routeContext) => null;
        public string? Link(string? routeName, object? values) => null;
    }

    private sealed class NullTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static FormHostController Host(string loadJson, string query = "")
    {
        var store = new InlineStore(loadJson);
        var scopes = new ServiceCollection().AddSingleton<IDefinitionAccess>(new Allow()).AddScoped<IFormLoadTestService, FormLoadTestService>().AddSingleton(Operations)
            .AddScoped(_ => PemsDbContextFactory.CreateInMemory("formload-" + Guid.NewGuid())).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var ui = PEMS.UI.ServiceCollectionExtensions.AddPemsUi(new ServiceCollection()).BuildServiceProvider().CreateScope().ServiceProvider;
        var access = new Allow();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Planner")], "test")) };
        http.Request.QueryString = new QueryString(query);
        return new FormHostController(new DefinitionPipeline(store, access, []), new ActionExecutor(store, access, []), new FakeCurrentUserService { UserName = "hamid" }, new Tokens(),
            ui.GetRequiredService<IPemsComponentRenderer>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<FormHostController>.Instance, null, null, new OperationReader(scopes, Operations))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            Url = new Urls(),
            TempData = new TempDataDictionary(http, new NullTempData())
        };
    }

    private static string Html(IActionResult result) => System.Net.WebUtility.HtmlDecode(((RenderedPage)((ViewResult)result).Model!).Html);

    private const string ById = """{"model":"FormLoadTest","action":"One"}""";

    [Fact]
    public async Task The_form_is_filled_from_the_operation_with_the_id_of_the_address()
    {
        var html = Html(await Host(ById).Index("item-edit", "5", CancellationToken.None));

        Assert.Contains("value=\"Pump overhaul\"", html);
        Assert.Contains("value=\"check the seal\"", html);
        Assert.Contains("value=\"2026-11-20\"", html);        // a date without a time is the date of a date field
        Assert.Contains("checked", html);                       // the boolean fills the checkbox
    }

    [Fact]
    public async Task An_id_that_gives_no_row_is_not_found()
        => Assert.IsType<NotFoundResult>(await Host(ById).Index("item-edit", "404", CancellationToken.None));

    [Fact]
    public async Task A_form_without_an_id_is_a_new_one_and_stays_empty()
    {
        var html = Html(await Host(ById).Index("item-edit", null, CancellationToken.None));

        Assert.DoesNotContain("Pump overhaul", html);
    }

    [Fact]
    public async Task The_arguments_may_come_from_the_query_and_a_link_can_still_override_a_loaded_value()
    {
        var host = Host("""{"model":"FormLoadTest","action":"ByCode","args":{"code":"param:code"}}""", "?code=P-1&title=From%20the%20link");

        var html = Html(await host.Index("item-edit", "1", CancellationToken.None));

        Assert.Contains("value=\"From the link\"", html);       // the query string is applied last
        Assert.Contains("value=\"2026-11-21\"", html);         // a date field takes the date of the loaded date and time
    }

    [Fact]
    public void A_load_that_names_no_operation_or_a_wrong_argument_is_refused_when_the_definition_is_checked()
    {
        var reader = new OperationReader(null!, Operations);
        string Form(string load) => """{"id":"f","title":{"en":"F","fa":"F"},"submitLabel":{"en":"S","fa":"S"},"actionKey":"a","fields":[{"name":"x","label":{"en":"X","fa":"X"},"kind":"text"}],"load":""" + load + "}";

        Assert.Empty(new FormDefinitionKind(reader).Validate(Form("""{"model":"FormLoadTest","action":"One"}"""), new()));
        Assert.Contains(new FormDefinitionKind(reader).Validate(Form("""{"model":"FormLoadTest","action":"Nope"}"""), new()), e => e.Pointer.StartsWith("/load"));
        Assert.Contains(new FormDefinitionKind(reader).Validate(Form("""{"model":"FormLoadTest","action":"One","args":{"wrong":"param:id"}}"""), new()), e => e.Pointer.StartsWith("/load"));
        Assert.Contains(new FormDefinitionKind().Validate(Form("""{"model":"","action":""}"""), new()), e => e.Pointer == "/load");
    }
}
