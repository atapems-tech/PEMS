namespace PEMS.Web.Areas.Admin.Models;

/// <summary>صفحه‌ی تأیید ریست حساب مدیر (SCRUM-354). هیچ رمزی در این مدل نیست.</summary>
public class AdminResetViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
