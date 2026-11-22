using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

/// <summary>SCRUM-775: the texts of the annotation bar and note form of <see cref="PemsFilePreview"/> (inputs, ADR-0008); the defaults are the platform's two languages.</summary>
public sealed record FilePreviewTexts
{
    public TranslatableText Annotate { get; init; } = TranslatableText.Of(("en", "Add note"), ("fa", "افزودن یادداشت"));
    public TranslatableText Previous { get; init; } = TranslatableText.Of(("en", "Previous page"), ("fa", "صفحه قبل"));
    public TranslatableText Next { get; init; } = TranslatableText.Of(("en", "Next page"), ("fa", "صفحه بعد"));
    public TranslatableText Page { get; init; } = TranslatableText.Of(("en", "Page"), ("fa", "صفحه"));
    public TranslatableText Pin { get; init; } = TranslatableText.Of(("en", "Note"), ("fa", "یادداشت"));
    public TranslatableText Note { get; init; } = TranslatableText.Of(("en", "Note text"), ("fa", "متن یادداشت"));
    public TranslatableText Save { get; init; } = TranslatableText.Of(("en", "Save"), ("fa", "ذخیره"));
    public TranslatableText Remove { get; init; } = TranslatableText.Of(("en", "Delete"), ("fa", "حذف"));
    public TranslatableText Cancel { get; init; } = TranslatableText.Of(("en", "Cancel"), ("fa", "انصراف"));
    public TranslatableText Loading { get; init; } = TranslatableText.Of(("en", "Loading notes..."), ("fa", "در حال بارگیری یادداشت‌ها..."));
    public TranslatableText Error { get; init; } = TranslatableText.Of(("en", "The note could not be saved or loaded."), ("fa", "یادداشت ذخیره یا بارگیری نشد."));
}
