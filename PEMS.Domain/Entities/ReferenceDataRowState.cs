using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// SCRUM-803: what the product last wrote into one row of a lookup table it ships (reference-data definition). <c>ShippedHash</c> is the fingerprint of the values the product wrote, so a later update can tell an
/// untouched row (updated by us) from one the customer edited (never overwritten, reported). A lookup row without a state row was not written by the product: it belongs to the customer.
/// </summary>
public class ReferenceDataRowState
{
    [Key]
    public int ReferenceDataRowStateId { get; set; }

    /// <summary>The list key of the reference-data definition, e.g. <c>material-uom</c>.</summary>
    [MaxLength(80)]
    public string ListKey { get; set; } = string.Empty;

    /// <summary>The stable key of the row inside the list (the natural key of the lookup, e.g. its Code).</summary>
    [MaxLength(80)]
    public string RowKey { get; set; } = string.Empty;

    /// <summary>platform or application (the owner of the row when it was shipped).</summary>
    [MaxLength(20)]
    public string Owner { get; set; } = "platform";

    [MaxLength(32)]
    public string ShippedHash { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
