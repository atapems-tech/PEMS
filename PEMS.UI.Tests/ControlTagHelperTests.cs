using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-487 / #487: the bare form controls write the Bootstrap markup the pages wrote by hand and keep every attribute.</summary>
public sealed class ControlTagHelperTests
{
    private static string Run(TagHelper helper, string tag, TagHelperAttributeList attributes, string inner = "")
    {
        var context = new TagHelperContext(tag, new TagHelperAttributeList(), new Dictionary<object, object>(), "c");
        var output = new TagHelperOutput(tag, attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(inner)));
        output.Content.SetHtmlContent(inner);
        helper.Process(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Theory]
    [InlineData(ControlKind.Text, Size.Medium, null, "form-control")]
    [InlineData(ControlKind.Text, Size.Small, null, "form-control form-control-sm")]
    [InlineData(ControlKind.Text, Size.Large, "mb-2", "form-control form-control-lg mb-2")]
    [InlineData(ControlKind.Select, Size.Medium, null, "form-select")]
    [InlineData(ControlKind.Select, Size.Small, "w-auto", "form-select form-select-sm w-auto")]
    [InlineData(ControlKind.Check, Size.Small, "me-1", "form-check-input me-1")]
    public void Control_classes_are_the_ones_the_pages_wrote(ControlKind kind, Size size, string? css, string expected)
        => Assert.Equal(expected, ControlState.Css(new ControlOptions { Kind = kind, Size = size, CssClass = css }));

    [Fact]
    public void An_input_keeps_its_attributes_and_gets_the_classes_first()
        => Assert.Equal("<input class=\"form-control form-control-sm\" required=\"required\" placeholder=\"Tag\" name=\"tag\" type=\"number\" value=\"3\" />",
            Run(new PemsInputTagHelper(null!) { Scale = "small", Name = "tag", InputTypeName = "number", Value = "3" }, "pems-input",
                [new("required", "required"), new("placeholder", "Tag")]));

    [Fact]
    public void A_check_input_gets_the_check_class()
        => Assert.Equal("<input class=\"form-check-input me-1\" name=\"on\" type=\"checkbox\" />",
            Run(new PemsInputTagHelper(null!) { Kind = "check", Css = "me-1", Name = "on", InputTypeName = "checkbox" }, "pems-input", []));

    [Fact]
    public void A_select_keeps_its_options_and_attributes()
        => Assert.Equal("<select class=\"form-select form-select-sm\" multiple=\"multiple\" name=\"s\"><option>a</option></select>",
            Run(new PemsSelectTagHelper(null!) { Scale = "small", Name = "s" }, "pems-select", [new("multiple", "multiple")], "<option>a</option>"));

    [Fact]
    public void A_textarea_keeps_its_text_and_attributes()
        => Assert.Equal("<textarea class=\"form-control\" rows=\"3\" name=\"n\">hello</textarea>",
            Run(new PemsTextAreaTagHelper(null!) { Name = "n" }, "pems-textarea", [new("rows", "3")], "hello"));

    [Fact]
    public void A_class_the_built_in_helper_added_stays_after_the_control_classes()
        => Assert.Equal("<input class=\"form-control input-validation-error\" />",
            Run(new PemsInputTagHelper(null!), "pems-input", [new("class", "input-validation-error")]));
}
