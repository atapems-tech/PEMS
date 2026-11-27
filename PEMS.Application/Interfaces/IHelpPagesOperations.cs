namespace PEMS.Application.Interfaces;

public sealed record HelpRelatedLink(string Code, string Title);

/// <summary>The answer of the contextual help panel: <c>Found</c> is false when no article belongs to the page key.</summary>
public sealed record HelpContextAnswer(bool Found, string? Code = null, string? Title = null, string? Summary = null, string? Content = null, string? LanguageCode = null,
    IReadOnlyList<HelpRelatedLink>? RelatedArticles = null, string? ArticleUrl = null, bool HasVisual = false);

/// <summary>SCRUM-832: the contextual help panel's read (served as JSON at /x/help-context?key=...), in the language of the request, with the same content rules as HelpController.Context.</summary>
public interface IHelpPagesOperations
{
    [PEMS.Application.Common.PemsOperation("Help", "Context")]
    Task<HelpContextAnswer> ContextAsync(string? key = null);
}
