using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Areas.Admin.Models;

public class SoDPolicyListItemVm
{
    public int Id { get; set; }
    public string PermissionA { get; set; } = string.Empty;
    public string PermissionB { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class SoDPolicyFormVm
{
    [Required, Display(Name = "First permission (Create/Edit side)")]
    public int? PermissionAId { get; set; }

    [Required, Display(Name = "Conflicts with (Approve/Close side)")]
    public int? PermissionBId { get; set; }

    [Display(Name = "Why")]
    public string? Description { get; set; }

    public List<(int Id, string Name)> AllPermissions { get; set; } = new();
}
