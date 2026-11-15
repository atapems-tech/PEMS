using System.Net;
using PEMS.UI.Tests.Host;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-570: a data-table cell written as a Razor template (<c>DataCell.Template</c>) keeps its tag helpers: the form, its antiforgery token, the button and the encoded row values.</summary>
public sealed class TemplateCellTests(SpikeHost host) : IClassFixture<SpikeHost>
{
    [Fact]
    public async Task A_template_cell_renders_the_form_with_antiforgery_and_the_row_value_per_row()
    {
        var response = await host.Client.GetAsync("/b/template-cell");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<form ").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "name=\"__RequestVerificationToken\"").Count);
        Assert.Contains("name=\"key\" value=\"alpha\"", html);
        Assert.Contains("value=\"beta &amp; &lt;gamma&gt;\"", html);
        Assert.Contains("ALPHA", html);
        Assert.Contains("class=\"d-inline\"", html);
    }
}
