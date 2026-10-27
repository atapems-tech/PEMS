using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

/// <summary>Identity, accessibility attributes and CSS of one field, computed from the descriptor and the current error.</summary>
public sealed record FieldState(string Id, string? DescribedBy, string InputCss, bool Invalid)
{
    public string HelpId => Id + "-help";
    public string ErrorId => Id + "-error";

    public static FieldState From(FieldDescriptor d, string? error)
    {
        var id = "f-" + d.Name.Replace('.', '-').Replace('[', '-').Replace(']', '-');
        var describedBy = error is not null ? id + "-error" : d.HelpText is not null ? id + "-help" : null;
        var invalid = error is not null;
        return new FieldState(id, describedBy, invalid ? "pems-input pems-input--invalid" : "pems-input", invalid);
    }
}
