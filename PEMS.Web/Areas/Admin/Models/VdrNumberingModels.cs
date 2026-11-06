using PEMS.Application.DTOs;

namespace PEMS.Web.Areas.Admin.Models;

/// <summary>
/// SCRUM-529: the "VDR document numbers" section of Admin > Document numbering. The administrator searches for a VDR item,
/// picks it, and gets the two number forms (CONTRACTOR official number, SUB-CONTRACTOR vendor-format number) that used to be on
/// the VDR details page. The forms still post to VDRController.GenerateContractorNumber / GenerateVendorNumber.
/// </summary>
public sealed class VdrNumberingPanel
{
    /// <summary>The most search results listed under the search box.</summary>
    public const int MaxResults = 20;

    public string? Search { get; init; }
    public IReadOnlyList<VDRListItemDto> Results { get; init; } = [];
    /// <summary>The number of items that match the search (the list shows at most <see cref="MaxResults"/>).</summary>
    public int TotalMatches { get; init; }
    /// <summary>The picked item; null until one is picked.</summary>
    public VDRItemDto? Item { get; init; }
}
