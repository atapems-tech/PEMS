using Microsoft.AspNetCore.Mvc;
using PEMS.Web.Navigation;

namespace PEMS.Web.Controllers;

/// <summary>
/// The full results page of the top-bar search (/GlobalSearch?q=...). SCRUM-64: it used to query nine tables directly,
/// with no permission or row check (work orders without WorkOrder/View, confidential letters, documents of other
/// disciplines). It now shows the same results as the command palette: only modules whose access rule the user passes,
/// each through its provider's row scope (<see cref="CommandPaletteService"/>, ISearchableEntityProvider.Access).
/// </summary>
public class GlobalSearchController : Controller
{
    private readonly CommandPaletteService _palette;

    public GlobalSearchController(CommandPaletteService palette)
    {
        _palette = palette;
    }

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Title"] = "Global Search";
        ViewData["Section"] = "";
        ViewBag.Query = q;

        var groups = string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2
            ? Array.Empty<CommandPaletteGroup>()
            : await _palette.SearchAsync(q, Url, 15, ct);
        return View(groups);
    }
}
