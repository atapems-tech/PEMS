using System.Globalization;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-832: the contextual help panel read (see <see cref="IHelpPagesOperations"/>).</summary>
public sealed class HelpPagesOperations(IHelpService help, IHelpVisualService visuals) : IHelpPagesOperations
{
    private static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? "fa" : "en";

    public async Task<HelpContextAnswer> ContextAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return new HelpContextAnswer(false);
        var article = await help.GetArticleByContextAsync(key, Language);
        if (article == null) return new HelpContextAnswer(false);

        // the panel is text only (the chart stays in the full article); only the link says whether the article has a visual
        var hasVisual = (await visuals.GetVisualsForArticleAsync(article.HelpArticleId, Language)).Any();
        return new HelpContextAnswer(true, article.Code, article.Title, article.Summary, article.Content, article.LanguageCode,
            article.RelatedArticles.Select(r => new HelpRelatedLink(r.Code, r.Title)).ToList(), "/Help/Article?code=" + Uri.EscapeDataString(article.Code), hasVisual);
    }
}
