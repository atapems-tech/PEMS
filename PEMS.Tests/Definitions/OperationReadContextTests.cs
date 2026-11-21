using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public interface IReadContextTestService
{
    [PemsOperation("ReadContextTest", "Who")]
    Task<string> WhoAsync(int? employeeId, int? userId);
}

public sealed class ReadContextTestService : IReadContextTestService
{
    public Task<string> WhoAsync(int? employeeId, int? userId) => Task.FromResult($"{employeeId}/{userId}");
}

/// <summary>SCRUM-818: a read operation gets the employee and the user of the signed-in user from the context (context:employeeId, context:userId), as a write does. Before, the employee was silently null.</summary>
public sealed class OperationReadContextTests
{
    private static readonly OperationCatalog Operations = OperationCatalog.Scan(typeof(OperationReadContextTests).Assembly);

    private sealed class Access : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static Dictionary<string, string?> Parameters() => new()
    {
        ["model"] = "ReadContextTest", ["action"] = "Who", ["args"] = """{ "employeeId": "context:employeeId", "userId": "context:userId" }"""
    };

    private static async Task<(OperationReader Reader, int UserId)> SetupAsync()
    {
        var database = "readctx-" + Guid.NewGuid();
        var db = PemsDbContextFactory.CreateInMemory(database);
        var user = new ApplicationUser { UserName = "sara", NormalizedUserName = "SARA", EmployeeId = 7 };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var scopes = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(database)).AddSingleton<IDefinitionAccess>(new Access())
            .AddScoped<IReadContextTestService, ReadContextTestService>().AddSingleton(Operations).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return (new OperationReader(scopes, Operations), user.Id);
    }

    [Fact]
    public async Task The_employee_and_the_user_of_the_signed_in_user_reach_a_read_operation()
    {
        var (reader, userId) = await SetupAsync();

        var invoked = await reader.InvokeAsync(Parameters(), "en", ["Planner"], "sara", default);

        Assert.True(invoked.Allowed);
        Assert.Equal($"7/{userId}", invoked.Answer);
    }

    [Fact]
    public async Task An_unknown_or_missing_user_gives_no_employee_and_no_user()
    {
        var (reader, _) = await SetupAsync();

        Assert.Equal("/", (await reader.InvokeAsync(Parameters(), "en", ["Planner"], "nobody", default)).Answer);
        Assert.Equal("/", (await reader.InvokeAsync(Parameters(), "en", ["Planner"], null, default)).Answer);
    }

    [Fact]
    public void The_definition_check_accepts_context_user_id_for_a_read()
    {
        var errors = new OperationReader(null!, Operations).Validate(new System.Text.Json.Nodes.JsonObject
        {
            ["model"] = "ReadContextTest", ["action"] = "Who", ["args"] = new System.Text.Json.Nodes.JsonObject { ["employeeId"] = "context:employeeId", ["userId"] = "context:userId" }
        });

        Assert.Empty(errors);
    }
}
