namespace PEMS.UI.Rendering;

/// <summary>Marks a component as renderable by key from a descriptor. Extension packs use it to add their own components.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PemsComponentAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
