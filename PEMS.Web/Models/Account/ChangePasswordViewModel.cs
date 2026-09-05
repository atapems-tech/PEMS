using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Models.Account;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "رمز فعلی الزامی است")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز فعلی")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز جدید الزامی است")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز جدید")]
    [MinLength(6, ErrorMessage = "رمز باید حداقل ۶ کاراکتر باشد")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز جدید الزامی است")]
    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز جدید")]
    [Compare(nameof(NewPassword), ErrorMessage = "رمز جدید و تکرار آن یکسان نیستند")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
