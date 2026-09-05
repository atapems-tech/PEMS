using System.Text.Json;

namespace PEMS.Application.AI.Services;

public static class AIJsonExtractor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse<T>(string rawModelOutput, out T? result) where T : class
    {
        result = null;
        var candidate = ExtractJsonCandidate(rawModelOutput);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        try
        {
            result = JsonSerializer.Deserialize<T>(candidate, SerializerOptions);
            return result != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractJsonCandidate(string rawModelOutput)
    {
        if (string.IsNullOrWhiteSpace(rawModelOutput))
            return string.Empty;

        var text = rawModelOutput.Trim();

        if (text.StartsWith("```"))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
                text = text[(firstNewLine + 1)..];

            var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                text = text[..closingFence];
        }

        text = text.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return text;
    }
}
