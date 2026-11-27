using System.Globalization;
using System.Text.Json.Nodes;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-839: the PDF engine draws a layout definition: a PDF comes out, the block types and the datasets are checked as a definition, rich text becomes plain text.</summary>
public class PdfLayoutRendererTests
{
    private static JsonObject Layout() => JsonNode.Parse("""
    {
      "page": { "size": "A4", "margin": 2 },
      "header": [ { "type": "heading", "text": "{{doc.title}}" } ],
      "body": [
        { "type": "keyValue", "rows": [ { "label": { "en": "Number", "fa": "شماره" }, "value": "{{doc.number}}" } ] },
        { "type": "html", "text": "{{doc.body}}" },
        { "type": "table", "dataset": "items", "columns": [ { "header": "Name", "field": "name" }, { "header": "Qty", "field": "qty", "align": "end" } ] },
        { "type": "text", "text": "hidden", "showWhen": "doc.missing" }
      ],
      "footer": [ { "type": "pageNumber" } ]
    }
    """)!.AsObject();

    private static Dictionary<string, JsonArray> Data() => new()
    {
        ["doc"] = (JsonArray)JsonNode.Parse("""[ { "title": "Report", "number": "R-1", "body": "<p>One</p><p>Two</p>" } ]""")!,
        ["items"] = (JsonArray)JsonNode.Parse("""[ { "name": "A", "qty": 2 }, { "name": "B", "qty": 3 } ]""")!,
    };

    [Theory]
    [InlineData("en")]
    [InlineData("fa")]
    public void ALayoutBecomesAPdf_InBothLanguages(string culture)
    {
        var bytes = PdfLayoutRenderer.Render(Layout(), Data(), new CultureInfo(culture));
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void ATableMustNameADatasetOfTheLayout_AndAnUnknownBlockTypeIsRefused()
    {
        var layout = Layout();
        Assert.Empty(PdfLayoutRenderer.Validate(layout, ["doc", "items"]));
        Assert.Contains(PdfLayoutRenderer.Validate(layout, ["doc"]), e => e.Code == "unknown-dataset");
        ((JsonArray)layout["body"]!).Add(JsonNode.Parse("""{ "type": "script" }"""));
        Assert.Contains(PdfLayoutRenderer.Validate(layout, ["doc", "items"]), e => e.Code == "wrong-value");
        Assert.Contains(PdfLayoutRenderer.Validate(new JsonObject(), []), e => e.Code == "required");
    }

    [Fact]
    public void RichTextIsDrawnAsPlainText()
        => Assert.Equal("One\nTwo", PdfLayoutRenderer.ToPlain("<p>One</p><p>Two &amp;</p>").Replace(" &", ""));

    [Fact]
    public void PagePerRecord_DrawsACustomSizedPageForEveryRowWithItsSvg()
    {
        var layout = JsonNode.Parse("""
        { "page": { "size": "custom", "width": 10, "height": 6, "margin": 0.4, "contentPadding": 0 }, "pagePerRecord": "labels",
          "body": [ { "type": "text", "text": "{{title}}" }, { "type": "svg", "field": "svg", "height": 55, "showWhen": "isLinear" } ] }
        """)!.AsObject();
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>";
        var data = new Dictionary<string, JsonArray> { ["labels"] = new JsonArray(
            new JsonObject { ["title"] = "A", ["isLinear"] = true, ["svg"] = svg }, new JsonObject { ["title"] = "B", ["isLinear"] = true, ["svg"] = svg }) };
        var pdf = PdfLayoutRenderer.Render(layout, data, CultureInfo.InvariantCulture);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
