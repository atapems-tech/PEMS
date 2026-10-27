using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PEMS.UI.Abstractions;
using PEMS.UI.Rendering;

namespace PEMS.UI.Tests;

/// <summary>A definition that is only JSON (as a database form row will be) renders in each language with no text coming from the components.</summary>
public sealed class JsonDescriptorRenderTests : IAsyncLifetime
{
    private ServiceProvider _provider = default!;
    private IPemsComponentRenderer _renderer = default!;

    public Task InitializeAsync()
    {
        _provider = new ServiceCollection().AddPemsUi().BuildServiceProvider();
        _renderer = _provider.CreateScope().ServiceProvider.GetRequiredService<IPemsComponentRenderer>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    private static string Plain(string html) => WebUtility.HtmlDecode(html);     // the renderer encodes non-Latin text

    private Task<string> RenderForm(string culture, FormState? state = null) => _renderer.RenderAsync("form", new Dictionary<string, object?>
    {
        ["Definition"] = PemsJson.Deserialize<FormDefinition>(Sample("instrument-form.json")),
        ["State"] = state ?? FormState.Empty
    }, new CultureInfo(culture));

    [Fact]
    public async Task Form_json_renders_in_persian()
    {
        var html = Plain(await RenderForm("fa-IR"));
        Assert.Contains("ثبت ابزار", html);                      // title
        Assert.Contains(">ذخیره</button>", html);               // submit label
        Assert.Contains(">تگ", html);                          // field label
        Assert.Contains("شناسه یکتا", html);                    // help text
        Assert.Contains("placeholder=\"PT-0001\"", html);       // neutral placeholder is the same in every language
        Assert.Contains(">واحد ۱</option>", html);
        Assert.DoesNotContain("Register instrument", html);
    }

    [Fact]
    public async Task Form_json_renders_in_english()
    {
        var html = Plain(await RenderForm("en"));
        Assert.Contains("Register instrument", html);
        Assert.Contains(">Save</button>", html);
        Assert.Contains("Unique identifier", html);
        Assert.Contains(">Unit 1</option>", html);
        Assert.DoesNotContain("ثبت ابزار", html);
    }

    [Fact]
    public async Task A_missing_translation_falls_back_instead_of_showing_nothing()
    {
        // option "u2" has only an English text: Persian users see it (default language fa is absent, so the first text is used)
        Assert.Contains(">Unit 2</option>", Plain(await RenderForm("fa")));
        // "Active" is a plain JSON string: the same neutral text in both languages
        Assert.Contains("Active", Plain(await RenderForm("fa")));
        Assert.Contains("Active", Plain(await RenderForm("en")));
    }

    [Fact]
    public async Task Validation_errors_are_inputs_too_and_follow_the_language()
    {
        var state = new FormState(new Dictionary<string, string?>(), new Dictionary<string, TranslatableText>
        {
            ["tag"] = TranslatableText.Of(("fa", "تگ الزامی است"), ("en", "Tag is required"))
        });
        var fa = Plain(await RenderForm("fa", state));
        var en = Plain(await RenderForm("en", state));
        Assert.Contains("role=\"alert\">تگ الزامی است<", fa);
        Assert.Contains("role=\"alert\">Tag is required<", en);
        Assert.Contains("aria-invalid=\"true\"", en);
    }

    [Fact]
    public async Task View_json_renders_data_formatted_by_culture_and_never_translated()
    {
        var view = PemsJson.Deserialize<ViewDescriptor>(Sample("instrument-view.json"));
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["tag"] = "PT-0001", ["calibrated"] = new DateOnly(2026, 10, 27), ["price"] = 1234567.5m, ["active"] = true },
            new Dictionary<string, object?> { ["tag"] = "پمپ ۲", ["calibrated"] = new DateTime(2026, 3, 21), ["price"] = 80, ["active"] = false }
        };
        Task<string> Render(string culture, IReadOnlyList<IReadOnlyDictionary<string, object?>> data) => _renderer.RenderAsync("table",
            new Dictionary<string, object?> { ["View"] = view, ["Rows"] = data }, new CultureInfo(culture));

        var fa = Plain(await Render("fa", rows));
        Assert.Contains("<caption class=\"pems-table__caption\">ابزارها</caption>", fa);
        Assert.Contains(">1405/08/05<", fa);                    // Jalali date
        Assert.Contains(">1405/01/01<", fa);
        Assert.Contains(">PT-0001<", fa);                       // data is not translated
        Assert.Contains(">پمپ ۲<", fa);
        Assert.Contains(">بله<", fa);                           // boolean texts are inputs
        Assert.Contains(">خیر<", fa);

        var en = Plain(await Render("en", rows));
        Assert.Contains(">2026-10-27<", en);
        Assert.Contains(">1,234,567.5<", en);
        Assert.Contains(">Yes<", en);
        Assert.Contains(">No<", en);

        Assert.Contains(">رکوردی نیست<", Plain(await Render("fa", [])));           // empty text comes from the definition
        Assert.Contains(">No records<", Plain(await Render("en", [])));
    }

    [Fact]
    public async Task Without_an_empty_text_an_empty_table_shows_none_instead_of_a_built_in_phrase()
    {
        var view = new ViewDescriptor("v", "T", [new ColumnDescriptor("a", "A")]);
        var html = await _renderer.RenderAsync("table", new Dictionary<string, object?> { ["View"] = view });
        Assert.DoesNotContain("pems-table__empty", html);
    }

    [Fact]
    public void Definitions_round_trip_through_json_unchanged()
    {
        var form = PemsJson.Deserialize<FormDefinition>(Sample("instrument-form.json"));
        var again = PemsJson.Deserialize<FormDefinition>(PemsJson.Serialize(form));
        Assert.Equal(form.Title, again.Title);
        Assert.Equal(form.Fields.Count, again.Fields.Count);
        Assert.Equal(form.Fields[1].Options![1].Label, again.Fields[1].Options![1].Label);
        Assert.Equal(PemsJson.Serialize(form), PemsJson.Serialize(again));            // stable: the stored shape does not drift
    }
}
