namespace PEMS.Application.Interfaces;

/// <summary>The answer of a tag scan command: NewId is the value for the redirect (the token of the scan, or the number of equipment created).</summary>
public sealed record TagScanResult(bool Success, string? Error, string NewId);

public sealed record TagScanOption(string Value, string Label);

/// <summary>
/// SCRUM-841: what EquipmentImportController did for the PDF tag scan, as operations: the scan of an uploaded PDF (a best-effort list of tag-number-looking text, kept for the user for a short time under a token),
/// the candidates as the choices of the review form, and the creation of the chosen tags as equipment. The forms are the definitions <c>equipment-scan-pdf</c> and <c>equipment-scan-select</c>.
/// </summary>
public interface IEquipmentTagScanOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentTagScan", "Scan", "EquipmentImport.Create")]
    Task<TagScanResult> ScanAsync(int? projectId, PEMS.Application.Common.OperationFile? file);

    [PEMS.Application.Common.PemsOperation("EquipmentTagScan", "Candidates", "EquipmentImport.Create")]
    Task<List<TagScanOption>> CandidatesAsync(string? token = null, int? projectId = null);

    [PEMS.Application.Common.PemsOperation("EquipmentTagScan", "Create", "EquipmentImport.Approve")]
    Task<TagScanResult> CreateAsync(int? projectId, int? disciplineId, string? tags);

    [PEMS.Application.Common.PemsOperation("EquipmentTagScan", "Projects", "EquipmentImport.Create")]
    Task<List<TagScanOption>> ProjectsAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentTagScan", "Disciplines", "EquipmentImport.Create")]
    Task<List<TagScanOption>> DisciplinesAsync();
}
