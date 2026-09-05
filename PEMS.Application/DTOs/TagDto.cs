namespace PEMS.Application.DTOs;

public class TagDto
{
    public int TagId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "secondary";
    public int LetterCount { get; set; }
}
