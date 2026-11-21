using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Application.Interfaces;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Authorization;

public sealed class BarcodeOperationsAuthorizationTests
{
    private static readonly OperationCatalog Catalog = OperationCatalog.Scan(typeof(IBarcodeOperations).Assembly);

    private sealed class DenyAccess : IDefinitionAccess
    {
        public string? Permission { get; private set; }

        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default)
        {
            Permission = permission;
            return Task.FromResult(false);
        }
    }

    [Theory]
    [InlineData("Recent", "Barcode.View")]
    [InlineData("Resolve", "Barcode.View")]
    [InlineData("ResolveJson", "Barcode.View")]
    [InlineData("Svg", "Barcode.View")]
    [InlineData("Png", "Barcode.View")]
    [InlineData("LabelPdf", "Barcode.View")]
    [InlineData("LabelSheet", "Barcode.View")]
    [InlineData("Candidates", "Barcode.Approve")]
    [InlineData("Disciplines", "Barcode.Approve")]
    [InlineData("EquipmentTypes", "Barcode.Approve")]
    [InlineData("BulkGenerate", "Barcode.Approve")]
    [InlineData("Generate", "Barcode.Create")]
    [InlineData("Print", "Barcode.Create")]
    [InlineData("Disable", "Barcode.Delete")]
    [InlineData("Revoke", "Barcode.Delete")]
    [InlineData("Replace", "Barcode.Delete")]
    public void Each_barcode_operation_declares_its_required_permission(string action, string permission)
    {
        Assert.Equal(permission, Catalog.Get("Barcode", action)?.Permission);
    }

    [Fact]
    public async Task The_generic_operation_handler_refuses_generate_when_create_permission_is_denied()
    {
        var access = new DenyAccess();
        using var services = new ServiceCollection()
            .AddSingleton<IDefinitionAccess>(access)
            .BuildServiceProvider();
        var handler = new OperationInvokeAction(services.GetRequiredService<IServiceScopeFactory>(), Catalog);
        var invocation = new ActionInvocation(
            new Dictionary<string, string?>(),
            "en",
            ["Viewer"],
            null,
            new JsonObject { ["model"] = "Barcode", ["action"] = "Generate" });

        var outcome = await handler.ExecuteAsync(invocation, CancellationToken.None);

        Assert.False(outcome.Ok);
        Assert.Equal("Barcode.Create", access.Permission);
    }
}
