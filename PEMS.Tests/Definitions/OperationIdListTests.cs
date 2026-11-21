using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public interface IIdListTestService
{
    [PemsOperation("IdListTest", "Take")]
    Task<(bool Success, string? Error)> TakeAsync(int workOrderId, List<int> disciplineIds);

    [PemsOperation("IdListTest", "TakeArray")]
    Task<bool> TakeArrayAsync(int[] ids);
}

public sealed class IdListTestService : IIdListTestService
{
    public static string? Last;
    public Task<(bool Success, string? Error)> TakeAsync(int workOrderId, List<int> disciplineIds) { Last = workOrderId + ":" + string.Join(",", disciplineIds); return Task.FromResult((true, (string?)null)); }
    public Task<bool> TakeArrayAsync(int[] ids) { Last = "array:" + string.Join(",", ids); return Task.FromResult(true); }
}

/// <summary>SCRUM-818: a list of numbers is bound from the repeated form field (a checkbox group arrives as one comma separated text); redirectBack is a known action setting.</summary>
public sealed class OperationIdListTests
{
    private static readonly OperationCatalog Operations = OperationCatalog.Scan(typeof(OperationIdListTests).Assembly);

    private sealed class Access : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static IServiceScopeFactory Scopes()
        => new ServiceCollection().AddSingleton<IDefinitionAccess>(new Access()).AddScoped<IIdListTestService, IdListTestService>().AddSingleton(Operations).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static Task<ActionOutcome> Run(string action, JsonObject args, params (string, string?)[] values)
        => new OperationInvokeAction(Scopes(), Operations).ExecuteAsync(new ActionInvocation(values.ToDictionary(v => v.Item1, v => v.Item2), "en", ["Planner"], "hamid",
            new JsonObject { ["model"] = "IdListTest", ["action"] = action, ["args"] = args }), CancellationToken.None);

    [Fact]
    public async Task A_list_of_numbers_is_bound_from_the_comma_separated_field()
    {
        var outcome = await Run("Take", new JsonObject { ["workOrderId"] = "field:workOrderId", ["disciplineIds"] = "field:disciplineIds" }, ("workOrderId", "7"), ("disciplineIds", "3,4, 9"));
        Assert.True(outcome.Ok);
        Assert.Equal("7:3,4,9", IdListTestService.Last);
    }

    [Fact]
    public async Task A_missing_list_is_empty_and_an_array_is_bound_too()
    {
        Assert.True((await Run("Take", new JsonObject { ["workOrderId"] = "field:workOrderId" }, ("workOrderId", "7"))).Ok);
        Assert.Equal("7:", IdListTestService.Last);
        Assert.True((await Run("TakeArray", new JsonObject { ["ids"] = "field:ids" }, ("ids", "1,2"))).Ok);
        Assert.Equal("array:1,2", IdListTestService.Last);
    }

    [Fact]
    public async Task A_value_that_is_not_a_number_is_refused_and_the_service_is_not_called()
    {
        IdListTestService.Last = null;
        var outcome = await Run("Take", new JsonObject { ["workOrderId"] = "field:workOrderId", ["disciplineIds"] = "field:disciplineIds" }, ("workOrderId", "7"), ("disciplineIds", "3,x"));
        Assert.False(outcome.Ok);
        Assert.Null(IdListTestService.Last);
    }

    [Fact]
    public void The_definition_check_accepts_a_list_parameter_with_a_field_source()
    {
        var errors = new OperationInvokeAction(Scopes(), Operations).ValidateParameters(new JsonObject { ["model"] = "IdListTest", ["action"] = "Take", ["args"] = new JsonObject { ["workOrderId"] = "field:workOrderId", ["disciplineIds"] = "field:disciplineIds" } });
        Assert.Empty(errors);
    }
}
