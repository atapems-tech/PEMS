namespace PEMS.Application.DTOs;

public class UserDashboardWidgetDto
{
    public string WidgetKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DisplayOrder { get; set; }
    public string Size { get; set; } = "Medium";
    public bool IsVisible { get; set; } = true;
}
