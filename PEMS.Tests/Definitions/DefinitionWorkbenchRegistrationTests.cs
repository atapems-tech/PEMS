using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public sealed class DefinitionWorkbenchRegistrationTests
{
    [Fact]
    public void WorkbenchGridAndDataSourceKindCanBeConstructedTogether()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DefinitionOwnership(null));
        services.AddSingleton<IDefinitionKind, DataSourceDefinitionKind>();
        services.AddSingleton<IGridDataProvider, DefinitionKindsGridProvider>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var kind = Assert.IsType<DataSourceDefinitionKind>(Assert.Single(provider.GetServices<IDefinitionKind>()));
        Assert.IsType<DefinitionKindsGridProvider>(Assert.Single(provider.GetServices<IGridDataProvider>()));
        Assert.Empty(kind.Validate("""{"kind":"grid","provider":"definition-kinds"}""", new DefinitionLimits()));
    }
}
