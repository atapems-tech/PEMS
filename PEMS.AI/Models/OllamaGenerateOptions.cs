namespace PEMS.AI.Models;

public class OllamaGenerateOptions
{
    public string? Model { get; set; }
    public bool JsonMode { get; set; } = false;
    public double Temperature { get; set; } = 0.1;

    // طبق گزارش سخت‌افزار ضعیف: سقف طول پاسخ - اگر مشخص نشود، بدون محدودیت (رفتار قبلی)
    public int? MaxResponseTokens { get; set; }
}
