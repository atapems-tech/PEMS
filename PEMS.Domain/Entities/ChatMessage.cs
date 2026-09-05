using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// تاریخچه‌ی گفتگو با AI Engineering Chat Assistant (برای هر کاربر جدا نگه‌داری می‌شود).
/// </summary>
public class ChatMessage
{
    [Key]
    public int ChatMessageId { get; set; }

    // شناسه کاربر Identity (int)
    public int UserId { get; set; }

    // User یا Assistant
    public string Role { get; set; } = "User";

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
