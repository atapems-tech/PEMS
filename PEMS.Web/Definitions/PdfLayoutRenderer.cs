using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PEMS.Application.Definitions;
using PEMS.UI.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Web.Definitions;

/// <summary>
/// SCRUM-839: the PDF engine. A PDF layout is a definition (the parameters of a data source of the provider <c>pdf</c>): the page (size, orientation, margin, font, direction), named datasets (read operations, read with the rights of the user),
/// and the blocks of the header, the body and the footer. One renderer draws every PDF of the product from such a layout; a form or page gets its PDF by naming the layout's key, and the layout is a stored definition that the form editor can edit later.
///
/// Blocks (field <c>type</c>): <c>text</c>, <c>heading</c> (text, size, bold, align, color), <c>keyValue</c> (rows of label and value), <c>table</c> (dataset, columns with header, field, width, align), <c>html</c> (rich text drawn as plain text),
/// <c>row</c> (items with a width and blocks), <c>line</c>, <c>spacer</c> (height), <c>pageNumber</c>, <c>image</c> (a base64 value). A text may hold <c>{{dataset.field}}</c> (the first row of the dataset), inside a table cell <c>{{field}}</c> is the row;
/// a block with <c>showWhen</c> ("dataset.field") is drawn only when that value is not empty or false. A text is a translatable text (<c>{"en", "fa"}</c>) or a plain string.
/// </summary>
public static class PdfLayoutRenderer
{
    private static readonly string[] Types = ["text", "heading", "keyValue", "table", "html", "row", "line", "spacer", "pageNumber", "image"];
    private static readonly Regex Merge = new(@"\{\{([A-Za-z0-9_.]{1,80})\}\}", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>What is wrong with a layout as a definition: the shape, the block types, the datasets a block names.</summary>
    public static IReadOnlyList<DefinitionFieldError> Validate(JsonObject? layout, IReadOnlyCollection<string> datasets)
    {
        var errors = new List<DefinitionFieldError>();
        if (layout is null) return [new("/layout", "required", "Def_Err_Required", "A PDF layout is an object with a body.")];
        if (layout["body"] is not JsonArray) errors.Add(new("/layout/body", "required", "Def_Err_Required", "The layout needs a body (a list of blocks)."));
        foreach (var part in new[] { "header", "body", "footer" })
            if (layout[part] is JsonArray blocks) Check(blocks, "/layout/" + part, datasets, errors);
        return errors;
    }

    private static void Check(JsonArray blocks, string at, IReadOnlyCollection<string> datasets, List<DefinitionFieldError> errors)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is not JsonObject b) { errors.Add(new($"{at}/{i}", "wrong-type", "Def_Err_Type", "A block is an object.")); continue; }
            var type = Str(b["type"]);
            if (type is null || !Types.Contains(type)) { errors.Add(new($"{at}/{i}/type", "wrong-value", "Def_Err_Type", $"The block type must be one of: {string.Join(", ", Types)}.")); continue; }
            if (type == "table" && (Str(b["dataset"]) is not { } d || !datasets.Contains(d))) errors.Add(new($"{at}/{i}/dataset", "unknown-dataset", "Def_Err_UnknownField", "A table names one of the datasets of the layout."));
            if (type == "table" && b["columns"] is not JsonArray) errors.Add(new($"{at}/{i}/columns", "required", "Def_Err_Required", "A table needs columns."));
            if (type == "row" && b["items"] is JsonArray items)
                for (var j = 0; j < items.Count; j++)
                    if (items[j] is JsonObject item && item["blocks"] is JsonArray inner) Check(inner, $"{at}/{i}/items/{j}/blocks", datasets, errors);
        }
    }

    private sealed class Context(IReadOnlyDictionary<string, JsonArray> data, CultureInfo culture)
    {
        public IReadOnlyDictionary<string, JsonArray> Data { get; } = data;
        public CultureInfo Culture { get; } = culture;
        public JsonObject? Row { get; set; }

        public string Text(JsonNode? node)
        {
            if (node is null) return "";
            if (node is JsonObject o && (o["en"] is not null || o["fa"] is not null))
                return Fill(o.Deserialize<TranslatableText>(PemsJson.Options)?.Resolve(Culture) ?? "");
            return Fill(Str(node) ?? "");
        }

        public string Fill(string text) => Merge.Replace(text, m => Value(m.Groups[1].Value));

        public string Value(string path)
        {
            JsonNode? node;
            var dot = path.IndexOf('.');
            if (Row is not null && Row.ContainsKey(dot < 0 ? path : path[..dot])) node = Path(Row, path);
            else if (dot > 0 && Data.TryGetValue(path[..dot], out var rows) && rows.Count > 0 && rows[0] is JsonObject first) node = Path(first, path[(dot + 1)..]);
            else if (dot < 0 && Row is not null) node = Row[path];
            else node = null;
            return Show(node);
        }

        private static JsonNode? Path(JsonObject root, string path)
        {
            JsonNode? cur = root;
            foreach (var part in path.Split('.')) { cur = (cur as JsonObject)?[part]; if (cur is null) return null; }
            return cur;
        }

        private string Show(JsonNode? node)
        {
            if (node is null) return "";
            if (node is JsonValue v)
            {
                if (v.TryGetValue<DateTime>(out var dt)) return dt.ToString(dt.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (v.TryGetValue<bool>(out var b)) return b ? "✓" : "";
                if (v.TryGetValue<string>(out var s)) return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && s.Length >= 10 && char.IsDigit(s[0]) && s.Contains('-') ? d.ToString(d.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : s;
                return v.ToString();
            }
            return node.ToJsonString();
        }

        public bool Truthy(string path) { var s = Value(path); return !string.IsNullOrWhiteSpace(s) && s != "0" && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase); }
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is JsonValue v2 ? v2.ToString() : null;
    private static float Num(JsonNode? n, float fallback) => n is JsonValue v && v.TryGetValue<double>(out var d) ? (float)d : fallback;
    private static bool Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>The datasets are the answers of the read operations as JSON (an object is one row); <paramref name="culture"/> picks the texts and the direction of a layout whose direction is <c>auto</c>.</summary>
    public static byte[] Render(JsonObject layout, IReadOnlyDictionary<string, JsonArray> data, CultureInfo culture)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var page = layout["page"] as JsonObject ?? [];
        var size = Str(page["size"]) switch { "A3" => PageSizes.A3, "A5" => PageSizes.A5, "Letter" => PageSizes.Letter, _ => PageSizes.A4 };
        if (Str(page["orientation"]) == "landscape") size = size.Landscape();
        var margin = Num(page["margin"], 2f);
        var fontSize = Num(page["fontSize"], 10f);
        var family = Str(page["fontFamily"]) ?? "Arial";
        var direction = Str(layout["direction"]) ?? "auto";
        var rtl = direction == "rtl" || direction == "auto" && culture.TextInfo.IsRightToLeft;
        var context = new Context(data, culture);

        var document = Document.Create(container => container.Page(p =>
        {
            p.Size(size);
            p.Margin(margin, Unit.Centimetre);
            p.DefaultTextStyle(x => x.FontSize(fontSize).FontFamily(family));
            if (rtl) p.ContentFromRightToLeft();
            if (layout["header"] is JsonArray header && header.Count > 0) p.Header().Column(col => Blocks(col, header, context));
            p.Content().PaddingVertical(Num(page["contentPadding"], 8f)).Column(col => Blocks(col, (JsonArray)layout["body"]!, context));
            if (layout["footer"] is JsonArray footer && footer.Count > 0) p.Footer().Column(col => Blocks(col, footer, context));
        }));
        return document.GeneratePdf();
    }

    private static void Blocks(ColumnDescriptor column, JsonArray blocks, Context ctx)
    {
        foreach (var node in blocks.OfType<JsonObject>())
        {
            if (Str(node["showWhen"]) is { } when && !ctx.Truthy(when)) continue;
            Draw(column.Item(), node, ctx);
        }
    }

    private static void Draw(IContainer container, JsonObject b, Context ctx)
    {
        switch (Str(b["type"]))
        {
            case "text":
            case "heading":
            {
                var heading = Str(b["type"]) == "heading";
                var text = ctx.Text(b["text"]);
                if (text.Length == 0 && !Flag(b["keepEmpty"])) return;
                var aligned = Str(b["align"]) switch { "center" => container.AlignCenter(), "end" => container.AlignRight(), _ => container };
                var padded = aligned.PaddingTop(Num(b["paddingTop"], 0f)).PaddingBottom(Num(b["paddingBottom"], 0f));
                var t = padded.Text(text);
                if (Num(b["size"], heading ? 16f : 0f) is var size && size > 0) t.FontSize(size);
                if (heading || Flag(b["bold"])) t.Bold();
                if (Str(b["color"]) is { } color) t.FontColor(color);
                break;
            }
            case "html":
                container.PaddingTop(Num(b["paddingTop"], 0f)).Text(ToPlain(ctx.Text(b["text"])));
                break;
            case "keyValue":
            {
                var rows = (b["rows"] as JsonArray)?.OfType<JsonObject>().Where(r => ctx.Text(r["value"]).Length > 0 || Flag(r["keepEmpty"])).ToList() ?? [];
                if (rows.Count == 0) return;
                container.Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(Num(b["labelWidth"], 1f)); c.RelativeColumn(Num(b["valueWidth"], 3f)); });
                    foreach (var r in rows)
                    {
                        t.Cell().Padding(2).Text(ctx.Text(r["label"])).SemiBold();
                        t.Cell().Padding(2).Text(ctx.Text(r["value"]));
                    }
                });
                break;
            }
            case "table":
            {
                var columns = (b["columns"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
                if (!ctx.Data.TryGetValue(Str(b["dataset"]) ?? "", out var rows) || columns.Count == 0) return;
                if (rows.Count == 0) { if (b["emptyText"] is not null) container.Text(ctx.Text(b["emptyText"])).Italic(); return; }
                container.Table(t =>
                {
                    t.ColumnsDefinition(c => { foreach (var col in columns) c.RelativeColumn(Num(col["width"], 1f)); });
                    foreach (var col in columns) t.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(3).Text(ctx.Text(col["header"])).SemiBold();
                    foreach (var row in rows.OfType<JsonObject>())
                    {
                        ctx.Row = row;
                        foreach (var col in columns)
                        {
                            var value = col["text"] is not null ? ctx.Text(col["text"]) : ctx.Value(Str(col["field"]) ?? "");
                            var cell = t.Cell().Border(0.5f).Padding(3);
                            var aligned = Str(col["align"]) switch { "center" => cell.AlignCenter(), "end" => cell.AlignRight(), _ => cell };
                            aligned.Text(string.IsNullOrEmpty(value) ? " " : value);
                        }
                        ctx.Row = null;
                    }
                });
                break;
            }
            case "row":
            {
                var items = (b["items"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
                container.Row(r =>
                {
                    foreach (var item in items)
                    {
                        var fixedWidth = item["fixedWidth"] is not null;
                        var cell = fixedWidth ? r.ConstantItem(Num(item["fixedWidth"], 100f)) : r.RelativeItem(Num(item["width"], 1f));
                        var aligned = Str(item["align"]) switch { "end" => cell.AlignRight(), "center" => cell.AlignCenter(), _ => cell };
                        aligned.Column(col => Blocks(col, item["blocks"] as JsonArray ?? [], ctx));
                    }
                });
                break;
            }
            case "line":
                container.PaddingTop(Num(b["paddingTop"], 4f)).LineHorizontal(Num(b["thickness"], 1f));
                break;
            case "spacer":
                container.Height(Num(b["height"], 8f));
                break;
            case "pageNumber":
                container.AlignCenter().Text(t => { t.DefaultTextStyle(x => x.FontSize(Num(b["size"], 8f))); if (Str(b["prefix"]) is { } prefix) t.Span(ctx.Text(b["prefix"])); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                break;
            case "image":
            {
                var raw = ctx.Text(b["field"] is JsonValue ? JsonValue.Create("{{" + Str(b["field"]) + "}}") : null);
                try { container.MaxHeight(Num(b["height"], 80f)).Image(Convert.FromBase64String(raw)); } catch (FormatException) { }
                break;
            }
        }
    }

    /// <summary>Rich text as plain text: line breaks at the ends of paragraphs, lines and list items, the tags dropped, the entities decoded.</summary>
    public static string ToPlain(string html)
    {
        var s = Regex.Replace(html ?? "", @"<\s*br\s*/?>|</\s*(p|div|li|h[1-6]|tr)\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        return Regex.Replace(WebUtility.HtmlDecode(s), @"\n{3,}", "\n\n").Trim();
    }
}

/// <summary>SCRUM-839: the data source provider <c>pdf</c>: parameters <c>layout</c> (see <see cref="PdfLayoutRenderer"/>), <c>datasets</c> (name to a read operation: model, action, args with param:x from the query), <c>fileName</c> (a text with {{dataset.field}}) and <c>disposition</c>; served by <c>GET /x/{key}</c>.</summary>
public sealed class PdfLayoutProvider(OperationReader reader) : IDataSourceProvider
{
    public string Key => "pdf";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        var errors = new List<DefinitionFieldError>();
        var datasets = parameters?["datasets"] as JsonObject ?? [];
        foreach (var (name, spec) in datasets)
            foreach (var e in reader.Validate(spec as JsonObject)) errors.Add(new($"/datasets/{name}{e.Pointer}", e.Code, e.MessageKey, e.Message));
        errors.AddRange(PdfLayoutRenderer.Validate(parameters?["layout"] as JsonObject, datasets.Select(d => d.Key).ToList()));
        return errors;
    }

    public Task<IReadOnlyList<FieldOption>> LoadAsync(DataSourceRequest request, CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldOption>>([]);
}
