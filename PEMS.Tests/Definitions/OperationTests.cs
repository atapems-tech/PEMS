using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public sealed record OpTestActor(int? EmployeeId, IReadOnlyList<string> Roles);

public interface IOpTestService
{
    [PemsOperation("Test.Run", "Test.Run")]
    Task<(bool Success, string? Error)> RunAsync(int id, string kind, string? note, OpTestActor actor);

    [PemsOperation("Test.Flag")]
    Task<bool> FlagAsync(int id, bool on);

    [PemsOperation("Test.Void")]
    Task DoAsync(string text, CancellationToken ct);

    [PemsOperation("Test.Boom")]
    Task BoomAsync();
}

public sealed class OpTestService : IOpTestService
{
    public static string? Last;
    public Task<(bool Success, string? Error)> RunAsync(int id, string kind, string? note, OpTestActor actor)
    {
        Last = $"{id}|{kind}|{note}|{actor.EmployeeId}|{string.Join(",", actor.Roles)}";
        return Task.FromResult(kind == "refuse" ? (false, "The service said no.") : (true, (string?)null));
    }
    public Task<bool> FlagAsync(int id, bool on) { Last = $"flag {id} {on}"; return Task.FromResult(id != 0); }
    public Task DoAsync(string text, CancellationToken ct) { Last = "void " + text; return Task.CompletedTask; }
    public Task BoomAsync() => throw new InvalidOperationException("Segregation of duties.");
}

/// <summary>SCRUM-437: the existing service methods marked [PemsOperation] run from an action definition through one generic handler; the service keeps its rules, the code checks the operation's permission, the definition cannot widen it.</summary>
public sealed class OperationTests
{
    private static readonly OperationCatalog Catalog = OperationCatalog.Scan(typeof(OperationTests).Assembly);

    private sealed class Access(Func<string?, bool> allow) : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(allow(permission));
    }

    private static OperationInvokeAction Handler(Func<string?, bool>? allow = null)
    {
        var db = "op-" + Guid.NewGuid();
        using (var ctx = PemsDbContextFactory.CreateInMemory(db)) { ctx.Users.Add(new ApplicationUser { UserName = "hamid", NormalizedUserName = "HAMID", EmployeeId = 7 }); ctx.SaveChanges(); }
        var sp = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(db)).AddSingleton<IDefinitionAccess>(new Access(allow ?? (_ => true))).AddScoped<IOpTestService, OpTestService>().BuildServiceProvider();
        return new OperationInvokeAction(sp.GetRequiredService<IServiceScopeFactory>(), Catalog);
    }

    private static ActionInvocation Invoke(string parameters, params (string, string?)[] values)
        => new(values.ToDictionary(v => v.Item1, v => v.Item2), "en", ["Reviewer"], "hamid", JsonNode.Parse(parameters)!.AsObject());

    [Fact]
    public void Every_operation_of_the_product_is_found_by_its_attribute_and_a_key_cannot_be_declared_twice()
    {
        Assert.Contains("WorkRequest.Submit", OperationCatalog.Scan(typeof(PemsOperationAttribute).Assembly).Keys);
        Assert.Contains("WorkRequest.WorkflowAction", OperationCatalog.Scan(typeof(PemsOperationAttribute).Assembly).Keys);
        Assert.Equal(["Test.Boom", "Test.Flag", "Test.Run", "Test.Void"], Catalog.Keys.OrderBy(k => k));
        Assert.Equal("Test.Run", Catalog.Get("Test.Run")!.Permission);
        Assert.Null(Catalog.Get("Test.Flag")!.Permission);
    }

    [Fact]
    public async Task Arguments_come_from_the_form_a_literal_or_the_context_and_an_actor_record_is_built_from_the_user()
    {
        var handler = Handler();
        var outcome = await handler.ExecuteAsync(Invoke("""{"operation":"Test.Run","args":{"id":"field:recordId","kind":"literal:approve"}}""", ("recordId", "5"), ("note", " hello ")), CancellationToken.None);
        Assert.True(outcome.Ok);
        Assert.Equal("5|approve|hello|7|Reviewer", OpTestService.Last);     // note: the field of the same name; actor: employee id of the user (from the database) and the roles
    }

    [Fact]
    public async Task The_answer_of_the_service_becomes_the_outcome_and_a_refusal_shows_the_services_own_message()
    {
        var handler = Handler();
        var refused = await handler.ExecuteAsync(Invoke("""{"operation":"Test.Run","args":{"id":"literal:1","kind":"literal:refuse"}}"""), CancellationToken.None);
        Assert.False(refused.Ok);
        Assert.Equal("The service said no.", refused.FormError!.Resolve(new System.Globalization.CultureInfo("en")));
        Assert.True((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Flag","args":{"id":"field:id","on":"literal:true"}}""", ("id", "3")), CancellationToken.None)).Ok);
        Assert.Equal("flag 3 True", OpTestService.Last);
        Assert.False((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Flag","args":{"id":"literal:0","on":"literal:false"}}"""), CancellationToken.None)).Ok);    // a bool false answer
        Assert.True((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Void","args":{"text":"field:t"}}""", ("t", "x")), CancellationToken.None)).Ok);                  // no answer = done
        var boom = await handler.ExecuteAsync(Invoke("""{"operation":"Test.Boom"}"""), CancellationToken.None);                                                            // a rule of the service that throws
        Assert.Equal("Segregation of duties.", boom.FormError!.Resolve(new System.Globalization.CultureInfo("en")));
    }

    [Fact]
    public async Task A_missing_required_value_or_one_that_does_not_fit_is_refused_before_the_service_is_called()
    {
        var handler = Handler();
        OpTestService.Last = "untouched";
        Assert.False((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Flag"}"""), CancellationToken.None)).Ok);                                   // id is a required number
        Assert.False((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Flag"}""", ("id", "abc"), ("on", "true")), CancellationToken.None)).Ok);      // not a number
        Assert.False((await handler.ExecuteAsync(Invoke("""{"operation":"Test.Flag"}""", ("id", "1"), ("on", "perhaps")), CancellationToken.None)).Ok);    // not a bool
        Assert.False((await handler.ExecuteAsync(Invoke("""{"operation":"Nope"}"""), CancellationToken.None)).Ok);
        Assert.Equal("untouched", OpTestService.Last);
    }

    [Fact]
    public async Task The_permission_of_the_operation_is_checked_by_the_code_not_by_the_definition()
    {
        var handler = Handler(permission => permission != "Test.Run");
        OpTestService.Last = "untouched";
        var refused = await handler.ExecuteAsync(Invoke("""{"operation":"Test.Run","args":{"id":"literal:1","kind":"literal:x"},"permission":null}"""), CancellationToken.None);
        Assert.False(refused.Ok);
        Assert.Equal("untouched", OpTestService.Last);
    }

    [Fact]
    public void The_parameters_are_checked_at_load()
    {
        var handler = Handler();
        JsonObject P(string json) => JsonNode.Parse(json)!.AsObject();
        Assert.Empty(handler.ValidateParameters(P("""{"operation":"Test.Run","args":{"id":"field:id","kind":"literal:a","note":"context:userName"}}""")));
        Assert.Contains("unknown-operation", handler.ValidateParameters(P("""{"operation":"Drop.Database"}""")).Select(e => e.Code));
        Assert.Contains("unknown-operation", handler.ValidateParameters(null).Select(e => e.Code));
        Assert.Contains("unknown-parameter", handler.ValidateParameters(P("""{"operation":"Test.Run","args":{"secret":"field:x"}}""")).Select(e => e.Code));
        Assert.Contains("wrong-source", handler.ValidateParameters(P("""{"operation":"Test.Run","args":{"id":"x"}}""")).Select(e => e.Code));
        Assert.Contains("wrong-source", handler.ValidateParameters(P("""{"operation":"Test.Run","args":{"id":"context:password"}}""")).Select(e => e.Code));
    }

    [Fact]
    public async Task A_redirect_takes_its_placeholders_from_the_values_of_the_request()
    {
        var store = new InlineStore("action", "go", """{"kind":"handler","handler":"operation.invoke","parameters":{"operation":"Test.Void","args":{"text":"field:id"}},"redirect":"/Thing/Details/{id}","success":{"en":"Done","fa":"شد"}}""");
        var executor = new ActionExecutor(store, new Access(_ => true), [Handler()]);
        var result = await executor.RunAsync("go", Invoke("{}", ("id", "42")));
        Assert.Equal(ActionStatus.Ok, result.Status);
        Assert.Equal("/Thing/Details/42", result.Redirect);
        Assert.Equal("/Thing/Details/", (await executor.RunAsync("go", Invoke("{}"))).Redirect);       // an unknown placeholder never throws
    }

    private sealed class InlineStore(string kind, string key, string payload) : IDefinitionStore
    {
        public Task<PublishedDefinition?> GetPublishedAsync(string k, string name, string appKey = "core", CancellationToken ct = default)
            => Task.FromResult<PublishedDefinition?>(k == kind && name == key ? new PublishedDefinition(kind, key, appKey, 1, payload, "h") : null);
    }
}
