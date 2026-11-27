using System.Globalization;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-832: the contextual help panel read (see <see cref="IHelpPagesOperations"/>).</summary>
public sealed class HelpPagesOperations(IHelpService help, IHelpVisualService visuals, IHelpAiExplainerService explainer, ICurrentUserService currentUser) : IHelpPagesOperations
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

    public async Task<List<HelpArticleRow>> ArticleAsync(string? code = null)
        => string.IsNullOrWhiteSpace(code) || await help.GetArticleByCodeAsync(code, Language) is not { } a ? [] : [new HelpArticleRow(a.HelpArticleId, a.Code, a.Title, a.CategoryName, a.HelpCategoryId, a.ArticleType, a.Summary, a.Content, a.ModifiedDate)];

    public async Task<List<HelpRelatedRow>> RelatedAsync(string? code = null)
        => string.IsNullOrWhiteSpace(code) || await help.GetArticleByCodeAsync(code, Language) is not { } a ? [] : a.RelatedArticles.Select(r => new HelpRelatedRow(r.Code, r.Code, r.Title, r.ArticleType)).ToList();

    public async Task<HelpAskResult> AskAsync(string? articleCode, string? question)
    {
        var back = string.IsNullOrWhiteSpace(articleCode) ? "/p/help" : "/p/help-article-view?code=" + Uri.EscapeDataString(articleCode);
        if (string.IsNullOrWhiteSpace(question)) return new(false, "Write a question.", "", "", "", null, back, "");
        var answer = await explainer.ExplainAsync(articleCode, question, Language, currentUser.UserId);
        return new(true, null, answer, "Ask PEMS AI", "Answer", "The answer is based only on the content of the article.", back, "Back to the article");
    }
}
