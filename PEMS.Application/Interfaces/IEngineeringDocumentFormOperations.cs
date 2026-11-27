using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

public sealed record EngDocSaveAnswer(bool Success, string? Error, int NewId);
public sealed record EngDocEditRow(int Id, string DocumentNo, string DocumentType, string Title, string? Description, int ProjectId, int DisciplineId, string? Area, string? IssuePurpose, string? PreparedBy, string? CheckedBy, string? ApprovedBy, int? EquipmentId, int? ContractId, int? RelatedMRId);
public sealed record EngDocChoice(string Value, string Label);

/// <summary>
/// SCRUM-830: create, edit and the file upload of an engineering document (what EngineeringDocumentsController did): the same rights, numbering, first revision and workflow row, access-control record, duplicate-number and frozen messages,
/// and for a file the same storage, hash, first-official rule and background processing.
/// </summary>
public interface IEngineeringDocumentFormOperations
{
    [PemsOperation("EngDocForm", "Create", "EngineeringDocuments.Create")]
    Task<EngDocSaveAnswer> CreateAsync(string? documentNo, bool useManualNumber, string title, string? description, string documentType, int projectId, int disciplineId, string? area, int? equipmentId, int? contractId);

    [PemsOperation("EngDocForm", "One", "EngineeringDocuments.Edit")]
    Task<List<EngDocEditRow>> OneAsync(int id);

    [PemsOperation("EngDocForm", "Edit", "EngineeringDocuments.Edit")]
    Task<EngDocSaveAnswer> EditAsync(int id, string documentNo, string documentType, string title, string? description, int projectId, int disciplineId, string? area, string? issuePurpose, string? preparedBy, string? checkedBy, string? approvedBy, int? equipmentId, int? contractId, int? relatedMRId);

    [PemsOperation("EngDocForm", "UploadAttachment", "EngineeringDocuments.Edit")]
    Task<EngDocSaveAnswer> UploadAttachmentAsync(int documentId, OperationFile? file, int? revisionId, string? fileRole);

    [PemsOperation("EngDocForm", "Projects", "EngineeringDocuments.View")]
    Task<List<EngDocChoice>> ProjectsAsync();

    [PemsOperation("EngDocForm", "Disciplines", "EngineeringDocuments.View")]
    Task<List<EngDocChoice>> DisciplinesAsync();

    [PemsOperation("EngDocForm", "Contracts", "EngineeringDocuments.View")]
    Task<List<EngDocChoice>> ContractsAsync();

    [PemsOperation("EngDocForm", "MaterialRequisitions", "EngineeringDocuments.View")]
    Task<List<EngDocChoice>> MaterialRequisitionsAsync();

    [PemsOperation("EngDocForm", "Revisions", "EngineeringDocuments.Edit")]
    Task<List<EngDocChoice>> RevisionsAsync(int documentId);
}
