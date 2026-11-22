using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// SCRUM-806: a database view (<c>vw_DefinitionGrantSummary</c>) registered as a read-only entity: how many roles are allowed or denied for each definition. It is the first view read through the generic entity data source
/// (paging, sort, search and the entity's read roles); nothing writes to it.
/// </summary>
public class DefinitionGrantSummary
{
    /// <summary>Kind and definition key joined with a colon: the one-column identity of the group.</summary>
    [Key, MaxLength(161)]
    public string GrantKey { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Kind { get; set; } = string.Empty;

    [MaxLength(120)]
    public string DefinitionKey { get; set; } = string.Empty;

    public int AllowCount { get; set; }

    public int DenyCount { get; set; }

    public int RoleCount { get; set; }
}
