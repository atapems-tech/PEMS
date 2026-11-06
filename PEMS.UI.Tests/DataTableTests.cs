using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-339 slice 6: the typed-column data table writes the enterprise table, formats the cell kinds and shows the empty row; the page supplies every text.</summary>
public sealed class DataTableTests
{
    private sealed record Row(DateTime When, string Who, string Action, string? Note, string Changes);

    private static string Run(PemsDataTableTagHelper helper, TagHelperAttributeList? attributes = null)
    {
        var context = new TagHelperContext("pems-data-table", new TagHelperAttributeList(), new Dictionary<object, object>(), "c");
        var output = new TagHelperOutput("pems-data-table", attributes ?? [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.ProcessAsync(context, output).GetAwaiter().GetResult();
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static readonly DataColumn[] Columns =
    [
        DataColumn.Of<Row>("When", r => r.When, DataCellKind.DateTime, "text-nowrap"),
        DataColumn.Of<Row>("By", r => r.Who),
        DataColumn.Of<Row>("Action", r => r.Action, DataCellKind.Badge, tone: r => r.Action == "Reject" ? "danger" : "success"),
        DataColumn.Of<Row>("Note", r => r.Note, DataCellKind.Muted, headerCss: "w-25"),
        DataColumn.Of<Row>("Changes", r => r.Changes, DataCellKind.Pre),
    ];

    [Fact]
    public void Headers_rows_and_cell_kinds_are_written_from_the_columns()
    {
        var html = Run(new PemsDataTableTagHelper(UiTestRenderer.Instance)
        {
            Items = new[] { new Row(new DateTime(2026, 11, 6, 9, 5, 0), "Hamid", "Approve", "ok", "a=1") },
            Columns = Columns, Dense = true, Css = "mb-0"
        });
        Assert.Contains("<table class=\"table table-enterprise table-sm mb-0\">", html);
        Assert.Contains("<th scope=\"col\">When</th>", html);
        Assert.Contains("<th scope=\"col\">Changes</th>", html);
        Assert.Contains("<th scope=\"col\" class=\"w-25\">Note</th>", html);
        Assert.Contains("<td class=\"text-nowrap\"><bdi dir=\"ltr\">2026-11-06 09:05</bdi></td>", html);
        Assert.Contains("<td>Hamid</td>", html);
        Assert.Contains("badge bg-success", html);
        Assert.Contains("Approve", html);
        Assert.Contains("<span class=\"text-muted\">ok</span>", html);
        Assert.Contains("<pre class=\"mb-0 small\">a=1</pre>", html);
    }

    [Fact]
    public void A_badge_takes_its_tone_from_the_row()
    {
        var html = Run(new PemsDataTableTagHelper(UiTestRenderer.Instance) { Items = new[] { new Row(DateTime.Now, "x", "Reject", null, "") }, Columns = Columns });
        Assert.Contains("badge bg-danger", html);
    }

    [Fact]
    public void No_rows_show_the_empty_text_in_one_cell_spanning_the_columns()
    {
        var html = Run(new PemsDataTableTagHelper(UiTestRenderer.Instance) { Items = Array.Empty<Row>(), Columns = Columns, Empty = "Nothing yet" });
        Assert.Contains("<td colspan=\"5\" class=\"text-muted small\">Nothing yet</td>", html);
        Assert.DoesNotContain("<bdi", html);
    }

    [Fact]
    public void Cell_text_is_encoded_and_the_page_attributes_are_kept()
    {
        var html = Run(new PemsDataTableTagHelper(UiTestRenderer.Instance)
        {
            Items = new[] { new Row(DateTime.Now, "<b>x</b>", "Approve", null, "") }, Columns = Columns
        }, [new("id", "history")]);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("id=\"history\"", html);
    }

    [Fact]
    public void The_plain_table_row_css_and_the_code_kind_are_supported()
    {
        var html = Run(new PemsDataTableTagHelper(UiTestRenderer.Instance)
        {
            Items = new[] { new Row(DateTime.Now, "x", "a", null, "") },
            Columns = [DataColumn.Of<Row>("Who", r => r.Who, DataCellKind.Code)], Enterprise = false, RowCss = "table-warning"
        });
        Assert.Contains("<table class=\"table\">", html);
        Assert.Contains("<tr class=\"table-warning\">", html);
        Assert.Contains("<td><code>x</code></td>", html);
    }

    [Theory]
    [InlineData(typeof(PemsTableHeadTagHelper), "thead")]
    [InlineData(typeof(PemsTableBodyTagHelper), "tbody")]
    [InlineData(typeof(PemsTableFootTagHelper), "tfoot")]
    [InlineData(typeof(PemsTableCaptionTagHelper), "caption")]
    [InlineData(typeof(PemsTableRowTagHelper), "tr")]
    [InlineData(typeof(PemsTableHeaderTagHelper), "th")]
    [InlineData(typeof(PemsTableCellTagHelper), "td")]
    public void A_table_part_writes_its_element_with_the_page_attributes_and_content_unchanged(Type helper, string element)
    {
        var tag = (TagHelper)Activator.CreateInstance(helper)!;
        var context = new TagHelperContext("pems-table-x", new TagHelperAttributeList(), new Dictionary<object, object>(), "c");
        var output = new TagHelperOutput("pems-table-x", [new("class", "text-end"), new("colspan", "3"), new("data-id", "7")], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        output.Content.SetHtmlContent("x &amp; <b>y</b>");
        tag.ProcessAsync(context, output).GetAwaiter().GetResult();
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        Assert.Equal($"<{element} class=\"text-end\" colspan=\"3\" data-id=\"7\">x &amp; <b>y</b></{element}>", writer.ToString());
    }
}
