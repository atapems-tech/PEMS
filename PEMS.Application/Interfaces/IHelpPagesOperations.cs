namespace PEMS.Application.Interfaces;

public sealed record HelpRelatedLink(string Code, string Title);

/// <summary>The answer of the contextual help panel: <c>Found</c> is false when no article belongs to the page key.</summary>
public sealed record HelpContextAnswer(bool Found, string? Code = null, string? Title = null, string? Summary = null, string? Content = null, string? LanguageCode = null,
    IReadOnlyList<HelpRelatedLink>? RelatedArticles = null, string? ArticleUrl = null, bool HasVisual = false);

public sealed record HelpArticleRow(int Id, string Code, string Title, string Category, int CategoryId, string Type, string Summary, string Content, DateTime Modified);
public sealed record HelpRelatedRow(string Id, string Code, string Title, string Type);

/// <summary>The answer of the AI question: the answer is a value shown once on its own page (the secret page of the form host) with a way back to the article.</summary>
public sealed record HelpAskResult(bool Success, string? Error, string SecretValue, string SecretTitle, string SecretLabel, string? SecretNote, string BackUrl, string BackLabel);

/// <summary>SCRUM-832: the contextual help panel's read (served as JSON at /x/help-context?key=...), in the language of the request, with the same content rules as HelpController.Context.</summary>
public interface IHelpPagesOperations
{
    [PEMS.Application.Common.PemsOperation("Help", "Context")]
    Task<HelpContextAnswer> ContextAsync(string? key = null);

    /// <summary>SCRUM-841: the article of the Help Center (what HelpController.Article showed), in the language of the request; nothing for an unknown or unpublished code.</summary>
    [PEMS.Application.Common.PemsOperation("Help", "Article")]
    Task<List<HelpArticleRow>> ArticleAsync(string? code = null);

    [PEMS.Application.Common.PemsOperation("Help", "Related")]
    Task<List<HelpRelatedRow>> RelatedAsync(string? code = null);

    /// <summary>"Ask PEMS AI about this page": the answer is limited to the real content of the article.</summary>
    [PEMS.Application.Common.PemsOperation("Help", "Ask")]
    Task<HelpAskResult> AskAsync(string? articleCode, string? question);
}
