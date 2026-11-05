using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using PEMS.UI.Rendering;

namespace PEMS.UI.Tests;

/// <summary>
/// ADR-0013 (the UI component registry): every <c>pems-*</c> tag helper declares how it relates to the component registry, and the keys, the registry and the facades cannot drift apart.
/// A facade that still writes its own markup (<see cref="FacadeKind.Direct"/>) is a debt counted below; the count may only go down.
/// </summary>
public sealed class UiRegistryDriftTests
{
    /// <summary>Lower this number when a Direct facade moves to a registered component; never raise it (new tags get a component first, guideline 02).</summary>
    private const int DirectFacadeBaseline = 24;

    private static string Here([CallerFilePath] string p = "") => p;
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, ".."));

    private static List<Type> Facades() => typeof(PemsFacadeAttribute).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(TagHelper)) && t.GetCustomAttributes<HtmlTargetElementAttribute>().Any(a => a.Tag?.StartsWith("pems-") == true)).ToList();

    private static IReadOnlyCollection<string> RegisteredKeys()
    {
        using var provider = new ServiceCollection().AddPemsUi().BuildServiceProvider();
        return provider.GetRequiredService<IPemsComponentRegistry>().Keys;
    }

    [Fact]
    public void Every_pems_tag_helper_declares_its_facade()
    {
        var missing = Facades().Where(t => t.GetCustomAttribute<PemsFacadeAttribute>() is null).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Add [PemsFacade(UiKeys.X)] (or FacadeKind.Part / Direct) to: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_component_facade_names_a_registered_key_and_renders_that_component()
    {
        var registered = RegisteredKeys();
        var registry = new PemsComponentRegistry();
        foreach (var t in Facades())
        {
            var a = t.GetCustomAttribute<PemsFacadeAttribute>();
            if (a is not { Kind: FacadeKind.Component }) continue;
            Assert.Contains(a.Key!, registered);
            // the facade must really render the component the key stands for (a typed RenderAsync<TComponent> call, or the list family's ListRender.Replace<TComponent>, in its file)
            var source = Directory.EnumerateFiles(Path.Combine(Root(), "PEMS.UI", "TagHelpers"), "*.cs").Select(File.ReadAllText).First(s => s.Contains("class " + t.Name));
            var component = registry.Resolve(a.Key!).Name;
            Assert.True((source.Contains($"RenderAsync<{component}>") || source.Contains($"ListRender.Replace<{component}>")), $"{t.Name} declares key '{a.Key}' but does not render {component}.");
        }
    }

    [Fact]
    public void UiKeys_holds_exactly_the_registered_keys()
    {
        var constants = typeof(UiKeys).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(RegisteredKeys().OrderBy(k => k, StringComparer.Ordinal).ToList(), constants);
    }

    [Fact]
    public void The_registry_registers_with_the_constants_never_with_a_string()
        => Assert.DoesNotMatch(new Regex(@"Register\(""", RegexOptions.None), File.ReadAllText(Path.Combine(Root(), "PEMS.UI", "Rendering", "PemsComponentRegistry.cs")));

    [Fact]
    public void There_is_no_exempt_facade_kind()
        => Assert.Equal(new[] { "Component", "Direct", "Part" }, Enum.GetNames<FacadeKind>().OrderBy(n => n, StringComparer.Ordinal));    // owner decision 2026-11-05: no Native kind; Direct is conversion debt only

    [Fact]
    public void Direct_facades_never_grow_beyond_the_baseline()
    {
        var direct = Facades().Count(t => t.GetCustomAttribute<PemsFacadeAttribute>() is { Kind: FacadeKind.Direct });
        Assert.True(direct <= DirectFacadeBaseline, $"{direct} tag helpers write their own markup (baseline {DirectFacadeBaseline}). Register a component first and use it; lower the baseline when you convert one.");
        if (direct < DirectFacadeBaseline) Assert.Fail($"Lower DirectFacadeBaseline to {direct} (a facade was converted).");
    }
}
