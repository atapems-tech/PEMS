using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-841: the status page (the friendly page of a 4xx or 5xx answer). A page definition (public: it opens without signing in) names the variants; the panel picks the variant of the status code of the request
// (the query value "code", or the status of the response) and shows its icon, title and text, the code and the trace id of the request.

/// <summary>One variant of the status panel: the status codes it answers, an icon and the texts.</summary>
public sealed record StatusVariant
{
    public IReadOnlyList<int> Codes { get; init; } = [];
    public string Icon { get; init; } = "bi-exclamation-triangle";
    public required TranslatableText Title { get; init; }
    public required TranslatableText Text { get; init; }
}

/// <summary>Inputs of <see cref="PemsStatusPanel"/>.</summary>
public sealed record StatusPanelOptions : FrameOptions
{
    public IReadOnlyList<StatusVariant> Variants { get; init; } = [];
    /// <summary>The variant of every code no variant names.</summary>
    public required StatusVariant Fallback { get; init; }
    public string HomeUrl { get; init; } = "/";
    public TranslatableText BackLabel { get; init; } = TranslatableText.Of(("en", "Back"), ("fa", "بازگشت"));
    public TranslatableText HomeLabel { get; init; } = TranslatableText.Of(("en", "Home"), ("fa", "صفحه‌ی اصلی"));
    public TranslatableText CodeLabel { get; init; } = TranslatableText.Of(("en", "Code"), ("fa", "کد"));
    public TranslatableText RequestIdLabel { get; init; } = TranslatableText.Of(("en", "Reference"), ("fa", "شناسه‌ی پیگیری"));
}
