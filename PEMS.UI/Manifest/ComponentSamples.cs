using System.Text.Json.Nodes;

namespace PEMS.UI.Manifest;

/// <summary>
/// One small valid options JSON per component key: shown by the component catalog (#326) and used by tests to prove that the generated
/// schema accepts real input and that the component renders from it. Sample texts are data, not component text.
/// </summary>
public static class ComponentSamples
{
    private static readonly Dictionary<string, string> Json = new(StringComparer.OrdinalIgnoreCase)
    {
        ["button"] = """{"label":{"en":"Save","fa":"ذخیره"},"tone":"primary"}""",
        ["badge"] = """{"label":{"en":"Active","fa":"فعال"},"tone":"success"}""",
        ["skeleton"] = """{"lines":3}""",
        ["empty-state"] = """{"title":{"en":"No records","fa":"رکوردی نیست"},"icon":"bi-inbox"}""",
        ["error-state"] = """{"title":{"en":"Could not load","fa":"بارگذاری نشد"}}""",
        ["loading-state"] = """{"title":{"en":"Loading","fa":"در حال بارگذاری"},"lines":2}""",
        ["field"] = """{"descriptor":{"name":"tag","label":{"en":"Tag","fa":"تگ"}},"value":"PT-0001"}""",
        ["table"] = """{"view":{"id":"items","title":{"en":"Items","fa":"موارد"},"columns":[{"key":"tag","header":{"en":"Tag","fa":"تگ"}}]},"rows":[]}""",
        ["form"] = """{"definition":{"id":"item","title":{"en":"Item","fa":"مورد"},"action":"/items","fields":[{"name":"tag","label":{"en":"Tag","fa":"تگ"}}],"submitLabel":{"en":"Save","fa":"ذخیره"}}}"""
    };

    public static JsonNode? Get(string key) => Json.TryGetValue(key, out var j) ? JsonNode.Parse(j) : null;
}
