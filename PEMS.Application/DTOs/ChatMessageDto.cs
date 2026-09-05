namespace PEMS.Application.DTOs;

public class ChatMessageDto
{
    public string Role { get; set; } = "User";
    public string Content { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}
