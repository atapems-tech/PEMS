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
}

public sealed class PemsComponentRegistry : IPemsComponentRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.OrdinalIgnoreCase);

    public PemsComponentRegistry()
    {
        Register("button", typeof(PemsButton));
        Register("badge", typeof(PemsBadge));
        Register("skeleton", typeof(PemsSkeleton));
        Register("field", typeof(PemsField));
        Register("table", typeof(PemsTable));
        Register("form", typeof(PemsForm));
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

    public Type Resolve(string key) => _types.TryGetValue(key, out var t)
        ? t
        : throw new KeyNotFoundException($"No component is registered for key '{key}'.");
}
