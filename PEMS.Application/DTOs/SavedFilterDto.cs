namespace PEMS.Application.DTOs;

public class SavedFilterDto
{
    public int SavedFilterId { get; set; }
    public string Name { get; set; } = "";
    public string QueryString { get; set; } = "";
    public bool IsDefault { get; set; }
}
