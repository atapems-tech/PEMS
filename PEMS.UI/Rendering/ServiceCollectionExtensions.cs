using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEMS.UI.Rendering;

namespace PEMS.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>Adds the component registry and the request-free renderer. Call once at startup.</summary>
    public static IServiceCollection AddPemsUi(this IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddSingleton<IPemsComponentRegistry, PemsComponentRegistry>();
        services.TryAddScoped<IPemsComponentRenderer, PemsComponentRenderer>();
        return services;
    }
}
