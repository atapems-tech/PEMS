using PEMS.Web.Security;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-841: what NotificationController.Open and DocumentRegisterController.Open did (see <see cref="IShellOperations"/>): the same rules; the target page checks its own permissions.</summary>
public sealed class ShellOperations(INotificationService notifications, ICurrentUserService currentUser, PEMSDbContext context) : IShellOperations
{
    // only a path of this site is followed (no open redirect); a notification of someone else is "not found" in the service
    public async Task<ShellRedirect> OpenNotificationAsync(int id)
    {
        if (currentUser.UserId is not { } userId) return new(false, "Sign in first.", "");
        var link = await notifications.OpenAsync(userId, id);
        return new(true, null, !string.IsNullOrWhiteSpace(link) && link.StartsWith('/') && !link.StartsWith("//") && !link.Contains('\\') ? link : "/p/notifications");   // SCRUM-731: the inbox is a definition page
    }

    public async Task<ShellRedirect> OpenDocumentAsync(int id)
    {
        var target = PEMS.Web.Helpers.DocumentLinks.Target(await context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == id));
        return target is { } t ? new(true, null, $"/{t.Controller}/Details/{t.Id}") : new(false, "Not found.", "");
    }
}
