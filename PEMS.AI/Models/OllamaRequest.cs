using System.Text.Json.Serialization;

namespace PEMS.AI.Models;

public class OllamaRequest
{
    public string Model { get; set; } = "qwen2.5:3b";

    public string Prompt { get; set; } = string.Empty;

    public bool Stream { get; set; } = false;

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaRequestOptions? Options { get; set; }
}

public class OllamaRequestOptions
{
    public double Temperature { get; set; } = 0.1;

    // طبق گزارش واقعی: سخت‌افزار ضعیف است - محدودکردن حداکثر طول پاسخ باعث می‌شود تولید
    // زودتر تمام شود، بدون این‌که کیفیت پاسخ‌های کوتاه را واقعاً خراب کند
    [JsonPropertyName("num_predict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumPredict { get; set; }
}
