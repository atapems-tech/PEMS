using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Areas.Admin.Models;

public class RoleListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public int UserCount { get; set; }

    public bool IsSystemRole { get; set; }
}


public class RoleFormViewModel
{
    [Required]
    [Display(Name = "Role Name (English, no spaces)")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Display Name")]
    public string? DisplayName { get; set; }

    public string? Description { get; set; }
}
