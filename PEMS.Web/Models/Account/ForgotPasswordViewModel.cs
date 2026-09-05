using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Models.Account;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
