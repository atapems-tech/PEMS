using Microsoft.AspNetCore.Mvc;
using PEMS.UI.Abstractions;

namespace PEMS.UI.Rendering;

public sealed record PemsTableModel(ViewDescriptor View, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows, string EmptyText);

/// <summary>Option A of ADR-0007: the same table as a View Component, kept for the comparison and as the documented fallback.</summary>
public sealed class PemsTableViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ViewDescriptor view, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, string emptyText = "No records") =>
        View(new PemsTableModel(view, rows, emptyText));
}
