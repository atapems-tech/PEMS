using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-838: registering an incoming letter as an operation (see <see cref="IDccIncomingOperations"/>): the same mapping, rules and messages as DCCController.RegisterIncoming.</summary>
public sealed class DccIncomingOperations(IIncomingCorrespondenceService incoming, ICurrentUserService currentUser, PEMSDbContext context, IStringLocalizer<SharedResource> localizer) : IDccIncomingOperations
{
    private sealed class LinkJson { public string? LinkType { get; set; } public int TargetId { get; set; } }

    private static DateTime? Iso(string? text) => DateTime.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private string Translate(LetterComposeIssue issue)
    {
        var l = localizer["Dcc_Err_" + issue.Code];
        return (l.ResourceNotFound ? issue.Code : l.Value).Replace("{0}", issue.Detail ?? "").Trim();
    }

    public async Task<DccIncomingAnswer> RegisterAsync(int? correspondentPartyId, string? newPartyName, string? newPartyType, int? projectId, int? contractId, string? senderReference, string? documentDate, string? receivedDate, string? receiptChannel, string subject, string? summary,
        string? priority, string? category, string? confidentialityLevel, int? disciplineId, int? parentLetterId, bool responseRequired, string? responseDueDate, int? ownerEmployeeId, List<int>? ccEmployeeIds, string? instruction, string? links, bool confirmDuplicate,
        IReadOnlyList<OperationFile>? files, int? employeeId, string culture)
    {
        var errors = new List<LetterComposeIssue>();
        DateTime? Date(string? text, string field) { if (string.IsNullOrWhiteSpace(text)) return null; var p = Iso(text); if (p == null) errors.Add(new LetterComposeIssue("DateInvalid", field)); return p; }
        var linkList = new List<IncomingLinkInput>();
        if (!string.IsNullOrWhiteSpace(links))
        {
            try { linkList = (JsonSerializer.Deserialize<List<LinkJson>>(links, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()).Select(l => new IncomingLinkInput(l.LinkType ?? "", l.TargetId)).ToList(); }
            catch (JsonException) { errors.Add(new LetterComposeIssue("LinkInvalid", "links")); }
        }
        var input = new IncomingRegistrationInput
        {
            ProjectId = projectId, ContractId = contractId, CorrespondentPartyId = correspondentPartyId, NewPartyName = newPartyName, NewPartyType = newPartyType, SenderReference = senderReference,
            DocumentDate = Date(documentDate, "documentDate"), ReceivedDate = Date(receivedDate, "receivedDate"), ReceiptChannel = string.IsNullOrWhiteSpace(receiptChannel) ? "OfficialLetter" : receiptChannel, Subject = subject ?? "", Summary = summary,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority, Category = string.IsNullOrWhiteSpace(category) ? null : category, ConfidentialityLevel = string.IsNullOrWhiteSpace(confidentialityLevel) ? "Internal" : confidentialityLevel,
            DisciplineId = disciplineId, ParentLetterId = parentLetterId, ResponseRequired = responseRequired, ResponseDueDate = Date(responseDueDate, "responseDueDate"), OwnerEmployeeId = ownerEmployeeId, CcEmployeeIds = ccEmployeeIds ?? new List<int>(),
            Instruction = instruction, Links = linkList, ConfirmDuplicate = confirmDuplicate
        };
        var streams = new List<Stream>();
        try
        {
            foreach (var f in files ?? [])
            {
                var s = new MemoryStream(f.Bytes); streams.Add(s);
                input.Files.Add(new IncomingFileInput { FileName = Path.GetFileName(f.FileName), ContentType = f.ContentType, Length = f.Bytes.Length, Role = "Attachment", Content = s });
            }
            var result = errors.Count > 0 ? new IncomingRegistrationResult { Issues = errors } : await incoming.RegisterAsync(input, currentUser.UserId, employeeId);
            if (result.Success) return new(true, null, result.LetterId ?? 0);
            var message = string.Join(" | ", result.Issues.Select(Translate));
            if (result.DuplicateLetterId.HasValue) message += $" ({result.DuplicateLetterNumber}: /Letter/Details/{result.DuplicateLetterId}; tick the confirmation to register anyway)";
            return new(false, message, 0);
        }
        finally { foreach (var s in streams) await s.DisposeAsync(); }
    }

    public async Task<List<DccChoice>> PartiesAsync() => (await incoming.SearchPartiesAsync(null, null, 200)).Select(p => new DccChoice(p.Id.ToString(), p.Name)).ToList();
    public async Task<List<DccChoice>> ProjectsAsync() => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new DccChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();
    public async Task<List<DccChoice>> DisciplinesAsync() => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new DccChoice(x.DisciplineId.ToString(), x.Name)).ToListAsync();
    public async Task<List<DccChoice>> EmployeesAsync() => await context.Employees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.LastName).Select(x => new DccChoice(x.EmployeeId.ToString(), x.FirstName + " " + x.LastName)).ToListAsync();
}
