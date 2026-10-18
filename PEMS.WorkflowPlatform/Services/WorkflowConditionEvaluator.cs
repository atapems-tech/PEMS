using System.Globalization;
using System.Text.RegularExpressions;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>
/// ارزیاب شرط محدود و بسته (نه موتور عبارت عمومی). گرامر:
///   expr   := and ( OR and )*
///   and    := clause ( AND clause )*
///   clause := Field op value | Field IN ( value {, value} )
///   op     := == | != | &gt; | &gt;= | &lt; | &lt;=        value := 'text' | number | true | false
/// بدون پرانتزگذاری گروهی، بدون توابع، بدون Reflection/اسکریپت. فیلدهای مجاز: Priority, ProjectId, Module,
/// BusinessObjectType, BusinessObjectId, StageName. فیلد ناشناخته = خطای اعتبارسنجی؛ Fact غایب در
/// زمان اجرا = false (به‌جز !=). مقایسه‌ی رشته‌ها بدون حساسیت به حروف؛ &gt; و &lt; فقط برای اعداد.
/// </summary>
public class WorkflowConditionEvaluator : IWorkflowConditionEvaluator
{
    private static readonly string[] CoreFields = { "Priority", "ProjectId", "Module", "BusinessObjectType", "BusinessObjectId", "StageName" };
    private readonly string[] Fields;

    /// <param name="extraFields">فیلدهای Fact ماژول‌ها (از IWorkflowBusinessContextProvider)؛ بدون آن فقط فیلدهای هسته مجازند.</param>
    public WorkflowConditionEvaluator(IEnumerable<string>? extraFields = null)
    {
        Fields = CoreFields.Concat(extraFields ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
    }
    private static readonly Regex Token = new(
        @"\G\s*(?:(?<str>'(?:[^']|'')*')|(?<op>==|!=|>=|<=|>|<)|(?<lp>\()|(?<rp>\))|(?<comma>,)|(?<word>[A-Za-z_][A-Za-z0-9_]*)|(?<num>-?\d+(?:\.\d+)?))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyCollection<string> SupportedFields => Fields;

    private sealed record Clause(string Field, string Op, List<string> Values);

    public IReadOnlyList<string> Validate(string? expression)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(expression)) return errors;
        Parse(expression, errors);
        return errors;
    }

    public bool Evaluate(string? expression, IReadOnlyDictionary<string, string?> facts)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        var errors = new List<string>();
        var groups = Parse(expression, errors);
        if (errors.Count > 0 || groups == null)
            throw new InvalidOperationException("شرط گذار نامعتبر است: " + string.Join("; ", errors));
        return groups.Any(g => g.All(c => Eval(c, facts)));
    }

    private static bool Eval(Clause c, IReadOnlyDictionary<string, string?> facts)
    {
        if (!facts.TryGetValue(c.Field, out var actual) || actual == null) return c.Op == "!=";
        if (c.Op == "in") return c.Values.Any(v => Eq(actual, v));
        if (c.Op == "==") return Eq(actual, c.Values[0]);
        if (c.Op == "!=") return !Eq(actual, c.Values[0]);
        if (!decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
            || !decimal.TryParse(c.Values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var b)) return false;
        return c.Op switch { ">" => a > b, ">=" => a >= b, "<" => a < b, "<=" => a <= b, _ => false };
    }

    private static bool Eq(string a, string b)
        => decimal.TryParse(a, NumberStyles.Number, CultureInfo.InvariantCulture, out var x)
           && decimal.TryParse(b, NumberStyles.Number, CultureInfo.InvariantCulture, out var y)
            ? x == y
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private List<List<Clause>>? Parse(string text, List<string> errors)
    {
        var toks = new List<(string Kind, string Text)>();
        var pos = 0;
        while (pos < text.Length)
        {
            if (string.IsNullOrWhiteSpace(text.Substring(pos))) break;
            var m = Token.Match(text, pos);
            if (!m.Success)
            {
                errors.Add($"نویسه‌ی نامعتبر در موقعیت {pos}.");
                return null;
            }
            pos = m.Index + m.Length;
            foreach (var k in new[] { "str", "op", "lp", "rp", "comma", "word", "num" })
                if (m.Groups[k].Success) { toks.Add((k, m.Groups[k].Value)); break; }
        }

        var groups = new List<List<Clause>> { new() };
        var i = 0;
        while (i < toks.Count)
        {
            if (toks[i].Kind != "word" || !Fields.Contains(toks[i].Text, StringComparer.Ordinal))
            {
                errors.Add($"فیلد «{toks[i].Text}» پشتیبانی نمی‌شود. فیلدهای مجاز: {string.Join(", ", Fields)}.");
                return null;
            }
            var field = toks[i++].Text;
            if (i >= toks.Count) { errors.Add("عملگر ناقص است."); return null; }

            if (toks[i].Kind == "word" && toks[i].Text.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                if (i >= toks.Count || toks[i].Kind != "lp") { errors.Add("پس از IN باید پرانتز باز بیاید."); return null; }
                i++;
                var vals = new List<string>();
                while (true)
                {
                    if (i >= toks.Count || !TryValue(toks[i], out var v)) { errors.Add("مقدار نامعتبر در IN."); return null; }
                    vals.Add(v); i++;
                    if (i < toks.Count && toks[i].Kind == "comma") { i++; continue; }
                    break;
                }
                if (i >= toks.Count || toks[i].Kind != "rp") { errors.Add("پرانتز بسته‌ی IN یافت نشد."); return null; }
                i++;
                groups[^1].Add(new Clause(field, "in", vals));
            }
            else
            {
                if (toks[i].Kind != "op") { errors.Add("عملگر نامعتبر؛ فقط ==, !=, >, >=, <, <=, IN."); return null; }
                var op = toks[i++].Text;
                if (i >= toks.Count || !TryValue(toks[i], out var v)) { errors.Add("مقدار پس از عملگر نامعتبر است."); return null; }
                var numeric = toks[i].Kind == "num";
                i++;
                if ((op is ">" or ">=" or "<" or "<=") && !numeric) { errors.Add($"عملگر {op} فقط برای مقدار عددی مجاز است."); return null; }
                groups[^1].Add(new Clause(field, op, new List<string> { v }));
            }

            if (i >= toks.Count) break;
            if (toks[i].Kind == "word" && toks[i].Text.Equals("AND", StringComparison.OrdinalIgnoreCase)) { i++; }
            else if (toks[i].Kind == "word" && toks[i].Text.Equals("OR", StringComparison.OrdinalIgnoreCase)) { i++; groups.Add(new()); }
            else { errors.Add($"توکن غیرمنتظره «{toks[i].Text}»."); return null; }
            if (i >= toks.Count) { errors.Add("عبارت ناقص است."); return null; }
        }

        if (groups.Count == 1 && groups[0].Count == 0) { errors.Add("عبارت خالی است."); return null; }
        return groups;
    }

    private static bool TryValue((string Kind, string Text) t, out string value)
    {
        value = "";
        switch (t.Kind)
        {
            case "str": value = t.Text[1..^1].Replace("''", "'"); return true;
            case "num": value = t.Text; return true;
            case "word" when t.Text.Equals("true", StringComparison.OrdinalIgnoreCase) || t.Text.Equals("false", StringComparison.OrdinalIgnoreCase):
                value = t.Text.ToLowerInvariant(); return true;
            default: return false;
        }
    }
}
