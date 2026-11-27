using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public sealed record LetterTemplatePreviewRow(int Id, string Name, string Type, string Priority, string Confidentiality, string Subject, string Body);

public sealed record LetterTemplateRow(int LetterTemplateId, string Name, string LetterType, string Subject, string Priority, string ConfidentialityLevel, bool IsActive, string Status);

/// <summary>
/// SCRUM-717: the letter templates (the list, the saved values of the edit form, the save and the switch of a template) as operations. The preview puts sample values in the tokens (SCRUM-830). Managing templates belongs to the reviewers: Correspondence.Approve is the floor of every operation, as the controller had it.
/// </summary>
public interface ILetterTemplateOperations
{
    [PEMS.Application.Common.PemsOperation("LetterTemplatePage", "List", "Correspondence.Approve")]
    Task<List<LetterTemplateRow>> TemplatesAsync();

    /// <summary>The saved template: the edit form loads it (load) and fills its fields of the same name.</summary>
    [PEMS.Application.Common.PemsOperation("LetterTemplatePage", "One", "Correspondence.Approve")]
    Task<LetterTemplateDto?> TemplateAsync(int id);

    /// <summary>A new template (id 0 or none) or the changes of one.</summary>
    /// <summary>The template with sample values (not the data of a real letter) put in its tokens; the body is rich text that was sanitized when saved, the values are encoded.</summary>
    [PEMS.Application.Common.PemsOperation("LetterTemplatePage", "Preview", "Correspondence.Approve")]
    Task<List<LetterTemplatePreviewRow>> PreviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterTemplatePage", "Save", "Correspondence.Approve")]
    Task<(bool Success, string? Error)> SaveAsync(int? id, string name, string? letterType, string subject, string? body, string priority, string confidentialityLevel, bool isActive = true);

    [PEMS.Application.Common.PemsOperation("LetterTemplatePage", "SetActive", "Correspondence.Approve")]
    Task<(bool Success, string? Error)> SetActiveAsync(int id, bool active);
}
