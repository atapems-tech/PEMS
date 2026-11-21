using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-717: the letter template list, load, save and switch of LetterTemplateController (same service, same order and messages).</summary>
public sealed class LetterTemplateOperations(ILetterTemplateService templates) : ILetterTemplateOperations
{
    public async Task<List<LetterTemplateRow>> TemplatesAsync()
        => (await templates.ListAsync(null, includeInactive: true)).Select(t => new LetterTemplateRow(t.LetterTemplateId, t.Name, t.LetterType ?? "", t.Subject, t.Priority, t.ConfidentialityLevel, t.IsActive, t.IsActive ? "Active" : "Inactive")).ToList();

    public Task<LetterTemplateDto?> TemplateAsync(int id) => templates.GetAsync(id);

    public async Task<(bool Success, string? Error)> SaveAsync(int? id, string name, string? letterType, string subject, string? body, string priority, string confidentialityLevel, bool isActive = true)
    {
        var (success, error, _) = await templates.SaveAsync(new LetterTemplateDto
        {
            LetterTemplateId = id ?? 0, Name = name, LetterType = string.IsNullOrWhiteSpace(letterType) ? null : letterType, Subject = subject, Body = body, Priority = priority, ConfidentialityLevel = confidentialityLevel, IsActive = isActive
        });
        return (success, error);
    }

    public async Task<(bool Success, string? Error)> SetActiveAsync(int id, bool active)
        => await templates.SetActiveAsync(id, active) ? (true, null) : (false, "Template not found.");
}
