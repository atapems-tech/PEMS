using System.Globalization;
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
    /// <param name="culture">The language and formatting culture to render in; null keeps the current UI culture.</param>
    Task<string> RenderAsync(string key, IDictionary<string, object?> parameters, CultureInfo? culture = null);
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters, CultureInfo? culture = null) where TComponent : IComponent;
}

public sealed class PemsComponentRenderer(
    IServiceProvider services, ILoggerFactory loggerFactory, IPemsComponentRegistry registry) : IPemsComponentRenderer
{
    public Task<string> RenderAsync(string key, IDictionary<string, object?> parameters, CultureInfo? culture = null) =>
        RenderCoreAsync(registry.Resolve(key), parameters, culture);

    public Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters, CultureInfo? culture = null) where TComponent : IComponent =>
        RenderCoreAsync(typeof(TComponent), parameters, culture);

    private async Task<string> RenderCoreAsync(Type componentType, IDictionary<string, object?> parameters, CultureInfo? culture)
    {
        // The culture is an async-local value, so setting it for this call does not leak into other requests running in parallel.
        var previous = CultureInfo.CurrentUICulture;
        if (culture is not null) CultureInfo.CurrentUICulture = culture;
        try
        {
            await using var renderer = new HtmlRenderer(services, loggerFactory);
            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync(componentType, ParameterView.FromDictionary(parameters));
                return output.ToHtmlString();
            });
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
