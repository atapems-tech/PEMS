using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

public sealed record DccIncomingAnswer(bool Success, string? Error, int NewId);
public sealed record DccChoice(string Value, string Label);

/// <summary>SCRUM-838: the registration of an incoming letter (what DCCController.RegisterIncoming posted) and the choices of its form as operations over IIncomingCorrespondenceService; the duplicate check asks for a confirmation field.</summary>
public interface IDccIncomingOperations
{
    [PemsOperation("DccIncoming", "Register", "Correspondence.Create")]
    Task<DccIncomingAnswer> RegisterAsync(int? correspondentPartyId, string? newPartyName, string? newPartyType, int? projectId, int? contractId, string? senderReference, string? documentDate, string? receivedDate, string? receiptChannel, string subject, string? summary,
        string? priority, string? category, string? confidentialityLevel, int? disciplineId, int? parentLetterId, bool responseRequired, string? responseDueDate, int? ownerEmployeeId, List<int>? ccEmployeeIds, string? instruction, string? links, bool confirmDuplicate,
        IReadOnlyList<OperationFile>? files, int? employeeId, string culture);

    [PemsOperation("DccIncoming", "Parties", "Correspondence.Create")]
    Task<List<DccChoice>> PartiesAsync();

    [PemsOperation("DccIncoming", "Projects", "Correspondence.Create")]
    Task<List<DccChoice>> ProjectsAsync();

    [PemsOperation("DccIncoming", "Disciplines", "Correspondence.Create")]
    Task<List<DccChoice>> DisciplinesAsync();

    [PemsOperation("DccIncoming", "Employees", "Correspondence.Create")]
    Task<List<DccChoice>> EmployeesAsync();
}
