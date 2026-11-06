using System.Text.Json.Serialization;
using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-339 (slice 6): the typed-column data table. The page owns every text (headers, empty text, cell values already localized, ADR-0008) and builds the columns in C#;
// the component owns the table markup, the cell kinds and the empty row. Audit trails, approval histories and comment lists are configurations of it.

/// <summary>How a cell shows its value.</summary>
public enum DataCellKind
{
    /// <summary>The value as text.</summary>
    Text,
    /// <summary>Muted small text (a reason, a note).</summary>
    Muted,
    /// <summary>A date and time, written yyyy-MM-dd HH:mm (current culture, like <c>ToString("yyyy-MM-dd HH:mm")</c> in a view) in a left-to-right run; the value is a DateTime or DateTimeOffset.</summary>
    DateTime,
    /// <summary>A date, written yyyy-MM-dd (current culture) in a left-to-right run.</summary>
    Date,
    /// <summary>A status pill: the text with the tone the column's <see cref="DataColumn.Tone"/> picks.</summary>
    Badge,
    /// <summary>Preformatted text (a list of changed fields, a diff).</summary>
    Pre,
    /// <summary>Code or an identifier (<c>&lt;code&gt;</c>).</summary>
    Code,
}

/// <summary>One column of <see cref="PemsDataTable"/>: the header text, how to read the value from a row, how to show it. Build it with <see cref="Of{T}"/>.</summary>
public sealed record DataColumn
{
    public required string Header { get; init; }
    public required Func<object, object?> Value { get; init; }
    public DataCellKind Kind { get; init; } = DataCellKind.Text;
    /// <summary>Extra classes on the cells (small, text-nowrap, text-end ...).</summary>
    public string? Css { get; init; }
    /// <summary>Extra classes on the header cell; null means the header has none.</summary>
    public string? HeaderCss { get; init; }
    /// <summary>Wraps the value in one inline element (span, strong, small, em, bdi); null writes the value as is.</summary>
    public CellElement? Element { get; init; }
    /// <summary>Classes on the cell computed from the row (a Razor class expression); added after <see cref="Css"/>.</summary>
    public Func<object, string?>? CssFor { get; init; }
    /// <summary>For <see cref="DataCellKind.Badge"/>: the tone name for a row (neutral, primary, success, danger, warning, info, light, dark); null shows neutral.</summary>
    public Func<object, string?>? Tone { get; init; }

    /// <summary>A typed column: <c>DataColumn.Of&lt;History&gt;(Localizer["Date"].Value, h =&gt; h.ActionDate, DataCellKind.DateTime)</c>.</summary>
    public static DataColumn Of<T>(string header, Func<T, object?> value, DataCellKind kind = DataCellKind.Text, string? css = null, Func<T, string?>? tone = null, string? headerCss = null, CellElement? element = null, Func<T, string?>? cssFor = null) => new()
    {
        Header = header,
        Value = row => value((T)row),
        Kind = kind,
        Css = css,
        HeaderCss = headerCss,
        Element = element,
        CssFor = cssFor is null ? null : row => cssFor((T)row),
        Tone = tone is null ? null : row => tone((T)row),
    };
}

/// <summary>An inline element around a cell value: <c>new CellElement("span", "small text-muted")</c>.</summary>
public sealed record CellElement(string Tag, string? Css = null, string? Dir = null);

/// <summary>Builds a cell the way Razor writes mixed content: markup parts as they are, everything else HTML-encoded, <c>null</c> as nothing.</summary>
public static class DataCell
{
    public static Microsoft.AspNetCore.Html.IHtmlContent Join(params object?[] parts)
    {
        var b = new Microsoft.AspNetCore.Html.HtmlContentBuilder();
        foreach (var p in parts)
        {
            switch (p)
            {
                case null: break;
                case Microsoft.AspNetCore.Html.IHtmlContent h: b.AppendHtml(h); break;
                default: b.Append(p.ToString() ?? ""); break;
            }
        }
        return b;
    }
}

/// <summary>Inputs of <see cref="PemsDataTable"/>: the rows and the columns come from the page (code, not JSON); <see cref="Empty"/> shows in a single row when there are no rows.</summary>
public sealed record DataTableOptions : FrameOptions
{
    [JsonIgnore] public IEnumerable<object>? Items { get; init; }
    [JsonIgnore] public IReadOnlyList<DataColumn>? Columns { get; init; }
    /// <summary>The text of the empty row (already localized); empty writes no row.</summary>
    public string? Empty { get; init; }
    /// <summary>Compact rows (<c>table-sm</c>).</summary>
    public bool Dense { get; init; }
    /// <summary>The enterprise look (<c>table-enterprise</c>, default); false writes the plain Bootstrap table.</summary>
    public bool Enterprise { get; init; } = true;
    /// <summary>Classes on every body row (<c>tr</c>).</summary>
    public string? RowCss { get; init; }
    /// <summary>Classes on a body row computed from the row (a Razor class expression); added after <see cref="RowCss"/>.</summary>
    [JsonIgnore] public Func<object, string?>? RowClass { get; init; }
    public string? Css { get; init; }
}

/// <summary>The rows and the columns of one table, built together so the row type is inferred once: <c>DataRows.Of(Model.History, c =&gt; c.Add("Date", h =&gt; h.When).Add("By", h =&gt; h.Who))</c>.</summary>
public sealed class DataRows
{
    public required IEnumerable<object> Items { get; init; }
    public required IReadOnlyList<DataColumn> Columns { get; init; }

    /// <summary>Classes on a body row computed from the row.</summary>
    public Func<object, string?>? RowClass { get; init; }

    public static DataRows Of<T>(IEnumerable<T> items, Func<DataColumns<T>, DataColumns<T>> columns, Func<T, string?>? rowClass = null) where T : notnull => new()
    {
        Items = items.Cast<object>(),
        Columns = columns(new DataColumns<T>()),
        RowClass = rowClass is null ? null : row => rowClass((T)row)
    };
}

/// <summary>Starts a column list whose row type is inferred from the items: <c>DataColumns.For(history).Add("Date", h =&gt; h.When, DataCellKind.DateTime).Add("By", h =&gt; h.Who)</c>.</summary>
public static class DataColumns
{
    public static DataColumns<T> For<T>(IEnumerable<T> items) => new();
}

/// <summary>A list of <see cref="DataColumn"/> for rows of type <typeparamref name="T"/>; converts to the list the table takes.</summary>
public sealed class DataColumns<T> : IReadOnlyList<DataColumn>
{
    private readonly List<DataColumn> _columns = [];

    public DataColumns<T> Add(string header, Func<T, object?> value, DataCellKind kind = DataCellKind.Text, string? css = null, Func<T, string?>? tone = null, string? headerCss = null, CellElement? element = null, Func<T, string?>? cssFor = null)
    {
        _columns.Add(DataColumn.Of(header, value, kind, css, tone, headerCss, element, cssFor));
        return this;
    }

    public int Count => _columns.Count;
    public DataColumn this[int index] => _columns[index];
    public IEnumerator<DataColumn> GetEnumerator() => _columns.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _columns.GetEnumerator();
}
