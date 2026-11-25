using System.Text.Json;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-620 / 772: a board or calendar posts JSON to /a/{key}; the action sees the same values a form post would give.</summary>
public sealed class ActionJsonValuesTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Scalars_become_text_arrays_of_scalars_are_joined_and_objects_are_ignored()
    {
        var values = ActionValues.FromJson(Parse("{\"id\":42,\"to\":\"Done\",\"flag\":true,\"none\":null,\"nested\":{\"a\":1},\"list\":[1,2]}"));
        Assert.Equal("42", values["id"]);
        Assert.Equal("Done", values["to"]);
        Assert.Equal("true", values["flag"]);
        Assert.Null(values["none"]);
        Assert.DoesNotContain("nested", values.Keys);
        Assert.Equal("1,2", values["list"]);   // an array of scalars is one comma separated text (an id list or a list of text, SCRUM-824)
    }

    [Fact]
    public void The_antiforgery_token_and_the_return_url_are_not_values_and_a_non_object_gives_nothing()
    {
        Assert.Empty(ActionValues.FromJson(Parse("{\"__RequestVerificationToken\":\"x\",\"returnUrl\":\"/a\"}")));
        Assert.Empty(ActionValues.FromJson(Parse("[1,2]")));
        Assert.Empty(ActionValues.FromJson(null));
    }

    [Fact]
    public void At_most_one_hundred_names_are_read()
    {
        var json = "{" + string.Join(",", Enumerable.Range(0, 150).Select(i => $"\"k{i}\":{i}")) + "}";
        Assert.Equal(100, ActionValues.FromJson(Parse(json)).Count);
    }
}
