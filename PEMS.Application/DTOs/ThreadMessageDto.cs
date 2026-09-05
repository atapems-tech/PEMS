namespace PEMS.Application.DTOs;

/// <summary>
/// یک پیام در زنجیره‌ی گفتگو (Thread View) - طبق نمونه‌ی کارتابل مرجع: نمایش پیام‌های قبلی
/// هنگام پاسخ‌دادن به یک نامه.
/// </summary>
public class ThreadMessageDto
{
    public int LetterId { get; set; }
    public string Subject { get; set; } = "";
    public string? Body { get; set; }
    public string LetterNumber { get; set; } = "";
    public DateTime LetterDate { get; set; }
    public string? SenderOrRegisteredByName { get; set; }
    public string RecipientsSummary { get; set; } = "";
}
