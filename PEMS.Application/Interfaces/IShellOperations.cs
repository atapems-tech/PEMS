namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-841: an answer that leads the browser on: RedirectUrl is a path of this site. The file host (<c>GET /x/{key}</c>) redirects to it; a refusal (Success false) is a 404.</summary>
public sealed record ShellRedirect(bool Success, string? Error, string RedirectUrl);

/// <summary>
/// SCRUM-841: the small answers of the shell that were controller actions (see the definitions <c>notification-open</c> and <c>document-open</c>, served by the file host): opening a notification (it is marked read, the user goes to its link) and
/// opening a document on the details page of its own type.
/// </summary>
public interface IShellOperations
{
    [PEMS.Application.Common.PemsOperation("Shell", "OpenNotification")]
    Task<ShellRedirect> OpenNotificationAsync(int id);

    [PEMS.Application.Common.PemsOperation("Shell", "OpenDocument")]
    Task<ShellRedirect> OpenDocumentAsync(int id);
}
