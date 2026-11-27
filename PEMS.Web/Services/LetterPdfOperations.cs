using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-839: the record of the letter PDF (see <see cref="ILetterPdfOperations"/>) with the same access rule as the letter page.</summary>
public sealed class LetterPdfOperations(ILetterService letters, IRichTextSanitizerService sanitizer, IPermissionService permissions, ICurrentUserService currentUser, PEMSDbContext context, IConfiguration configuration) : ILetterPdfOperations
{
    public async Task<List<LetterPdfRow>> RecordAsync(int id)
    {
        var userId = currentUser.UserId;
        int? employeeId = userId.HasValue ? await context.Users.Where(u => u.Id == userId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;
        var canSeeAll = currentUser.Roles.Contains("SystemAdmin") || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");
        var access = await letters.GetDetailForViewerAsync(id, new LetterViewer(userId, employeeId, canSeeAll, currentUser.UserName));
        if (access.Outcome == LetterAccessOutcome.Forbidden) throw new UnauthorizedAccessException();
        if (access.Outcome != LetterAccessOutcome.Ok) return [];
        var detail = access.Detail!;
        var l = detail.Letter;
        var head = LetterheadSettings.From(configuration);
        string Names(string type) => string.Join("\u060C ", detail.Recipients.Where(r => r.RecipientType == type).Select(r => r.DisplayName));
        var to = Names("To");
        if (string.IsNullOrWhiteSpace(to)) to = l.Receiver ?? "";
        var from = !string.IsNullOrWhiteSpace(l.SenderName)
            ? l.SenderName + (string.IsNullOrWhiteSpace(l.SenderTitle) ? "" : " - " + l.SenderTitle) + (string.IsNullOrWhiteSpace(l.SenderDepartmentName) ? "" : " (" + l.SenderDepartmentName + ")")
            : l.Sender ?? "";
        var contact = string.Join("  ", new[] { head.Phone, head.Website }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return [new LetterPdfRow(head.CompanyName, head.Address, contact.Length == 0 ? null : contact, l.LetterNumber, l.LetterDate, l.ReferenceNumber, l.Category, from.Length == 0 ? null : from, to.Length == 0 ? null : to, Names("Cc") is { Length: > 0 } cc ? cc : null,
            l.Subject, sanitizer.ToPlainText(detail.Body), l.SenderName, l.SenderTitle, l.SenderDepartmentName, string.IsNullOrWhiteSpace(l.SignedByName) ? null : $"{l.SignedByName} {l.SignedDate:yyyy-MM-dd}", l.ConfidentialityLevel, l.Priority, DateTime.Now)];
    }
}
