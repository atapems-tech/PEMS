using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using PEMS.UI.Components;

namespace PEMS.UI.Rendering;

/// <summary>Maps a component key ("field", "table", ...) to its component type. The only place that knows which type renders which key.</summary>
public interface IPemsComponentRegistry
{
    void Register(string key, Type componentType);
    int RegisterAssembly(Assembly assembly);
    Type Resolve(string key);
    /// <summary>Every registered key (for the component manifests and the catalog).</summary>
    IReadOnlyCollection<string> Keys { get; }
}

public sealed class PemsComponentRegistry : IPemsComponentRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.OrdinalIgnoreCase);

    public PemsComponentRegistry()
    {
        Register(UiKeys.Button, typeof(PemsButton));
        Register(UiKeys.Badge, typeof(PemsBadge));
        Register(UiKeys.Skeleton, typeof(PemsSkeleton));
        Register(UiKeys.EmptyState, typeof(PemsEmptyState));
        Register(UiKeys.ErrorState, typeof(PemsErrorState));
        Register(UiKeys.LoadingState, typeof(PemsLoadingState));
        Register(UiKeys.Field, typeof(PemsField));
        Register(UiKeys.Table, typeof(PemsTable));
        Register(UiKeys.Form, typeof(PemsForm));
        Register(UiKeys.FormSection, typeof(PemsFormSection));
        Register(UiKeys.ActionBar, typeof(PemsActionBar));
        Register(UiKeys.Alert, typeof(PemsAlert));
        Register(UiKeys.Modal, typeof(PemsModal));
        Register(UiKeys.Drawer, typeof(PemsDrawer));
        Register(UiKeys.ConfirmDialog, typeof(PemsConfirmDialog));
        Register(UiKeys.Tabs, typeof(PemsTabs));
        Register(UiKeys.Breadcrumb, typeof(PemsBreadcrumb));
        Register(UiKeys.Stepper, typeof(PemsStepper));
        Register(UiKeys.Menu, typeof(PemsMenu));
        Register(UiKeys.SidePanelNav, typeof(PemsSidePanelNav));
        Register(UiKeys.Card, typeof(PemsCard));
        Register(UiKeys.Progress, typeof(PemsProgress));
        Register(UiKeys.Accordion, typeof(PemsAccordion));
        Register(UiKeys.AccordionItem, typeof(PemsAccordionItem));
        Register(UiKeys.ListGroup, typeof(PemsListGroup));
        Register(UiKeys.Spinner, typeof(PemsSpinner));
    }

    public void Register(string key, Type componentType)
    {
        if (!typeof(IComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"{componentType.FullName} is not a component.", nameof(componentType));
        if (!_types.TryAdd(key, componentType) && _types[key] != componentType)
            throw new InvalidOperationException($"Component key '{key}' is already registered for {_types[key].FullName}.");
    }

    /// <summary>Registers every component of <paramref name="assembly"/> that carries <see cref="PemsComponentAttribute"/>.</summary>
    public int RegisterAssembly(Assembly assembly)
    {
        var count = 0;
        foreach (var type in assembly.GetTypes())
        {
            var attr = type.GetCustomAttribute<PemsComponentAttribute>();
            if (attr is null) continue;
            Register(attr.Key, type);
            count++;
        }
        return count;
    }

    public IReadOnlyCollection<string> Keys => _types.Keys.ToList();

    public Type Resolve(string key) => _types.TryGetValue(key, out var t)
        ? t
        : throw new KeyNotFoundException($"No component is registered for key '{key}'.");
}
