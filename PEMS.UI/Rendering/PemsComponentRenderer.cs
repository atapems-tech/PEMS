using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace PEMS.UI.Rendering;

/// <summary>
/// Renders a component to an HTML string with no HTTP request: for designer previews, e-mail, PDF and the Tag Helper facade.
/// Static rendering only (no circuits, no WebAssembly), so it works offline.
/// </summary>
public interface IPemsComponentRenderer
{
    Task<string> RenderAsync(string key, IDictionary<string, object?> parameters);
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent;
}

public sealed class PemsComponentRenderer(
    IServiceProvider services, ILoggerFactory loggerFactory, IPemsComponentRegistry registry) : IPemsComponentRenderer
{
    public Task<string> RenderAsync(string key, IDictionary<string, object?> parameters) =>
        RenderCoreAsync(registry.Resolve(key), parameters);

    public Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent =>
        RenderCoreAsync(typeof(TComponent), parameters);

    private async Task<string> RenderCoreAsync(Type componentType, IDictionary<string, object?> parameters)
    {
        await using var renderer = new HtmlRenderer(services, loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync(componentType, ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
