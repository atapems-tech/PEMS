using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

public class TechnicalQueryService : ITechnicalQueryService
{
    private const string Module = TechnicalQueryWorkflowSeeder.Module;
    private const string BusinessObjectType = "TechnicalQuery";
    private const string Category = TechnicalQueryWorkflowSeeder.Category;

    private static readonly string[] ValidTypes = { "TQ", "TSR", "RFI" };

    private readonly PEMSDbContext _context;
    private readonly IWorkflowRuntime _runtime;

    public TechnicalQueryService(PEMSDbContext context, IWorkflowRuntime runtime)
    {
        _context = context;
        _runtime = runtime;
    }


    public async Task<int> RaiseAsync(
        string queryType, string subject, string? description, int projectId, int disciplineId,
        int? relatedDocumentId, int? relatedEquipmentId, DateTime? requiredResponseDate, string priority,
        int? raisedByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var normalizedType = queryType.Trim().ToUpperInvariant();
        if (!ValidTypes.Contains(normalizedType))
            throw new Exception($"نوع نامعتبر است: {queryType} (باید TQ/TSR/RFI باشد)");

        if (string.IsNullOrWhiteSpace(subject))
            throw new Exception("موضوع الزامی است.");

        var queryNumber = await GenerateNumberAsync(normalizedType, projectId);

        var tq = new TechnicalQuery
        {
            QueryNumber = queryNumber,
            QueryType = normalizedType,
            ProjectId = projectId,
            DisciplineId = disciplineId,
            Subject = subject,
            Description = description,
            RelatedDocumentId = relatedDocumentId,
            RelatedEquipmentId = relatedEquipmentId,
            RequiredResponseDate = requiredResponseDate,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
            RaisedById = raisedByEmployeeId,
            RaisedDate = DateTime.Now,
            Status = "Open",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.TechnicalQueries.AddAsync(tq);
        await _context.SaveChangesAsync();

        // اولین مرحله (PendingResponse) با اعضای پویا: همون دیسیپلین مسئولی که در ثبت مشخص شد
        var firstStageParticipants = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(disciplineId) };

        await _runtime.StartInstanceAsync(
            Module, BusinessObjectType, tq.TechnicalQueryId, Category, projectId, raisedByEmployeeId, firstStageParticipants);

        return tq.TechnicalQueryId;
    }


    public async Task RespondAsync(
        int technicalQueryId, string responseText,
        int actorEmployeeId, int actorDisciplineId,
        string? ipAddress = null, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            throw new Exception("متن پاسخ نمی‌تواند خالی باشد.");

        var tq = await GetOrThrowAsync(technicalQueryId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(technicalQueryId);

        // مرحله‌ی بعدی (PendingConfirmation) با اعضای پویا: دیسیپلین همون کسی که این
        // TQ/TSR/RFI را ثبت کرده - او باید پاسخ را تایید یا رد کند
        var raiserDisciplineId = tq.RaisedById.HasValue
            ? await _context.Employees.Where(e => e.EmployeeId == tq.RaisedById.Value).Select(e => (int?)e.DisciplineId).FirstOrDefaultAsync()
            : null;

        var nextStageParticipants = raiserDisciplineId.HasValue
            ? new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(raiserDisciplineId.Value) }
            : null;

        var actorRoles = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(actorDisciplineId) };

        await _runtime.ExecuteActionAsync(
            instanceId, "Respond", actorEmployeeId, actorRoles, responseText, ipAddress, userAgent, nextStageParticipants);

        tq.ResponseText = responseText;
        tq.RespondedById = actorEmployeeId;
        tq.RespondedDate = DateTime.Now;
        tq.Status = "PendingConfirmation";

        await _context.SaveChangesAsync();
    }


    public async Task ConfirmAsync(
        int technicalQueryId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var tq = await GetOrThrowAsync(technicalQueryId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(technicalQueryId);

        await _runtime.ExecuteActionAsync(instanceId, "Confirm", actorEmployeeId, actorRoles, null, ipAddress, userAgent);

        tq.Status = "Closed";
        await _context.SaveChangesAsync();
    }


    public async Task ReopenAsync(
        int technicalQueryId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var tq = await GetOrThrowAsync(technicalQueryId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(technicalQueryId);

        await _runtime.ExecuteActionAsync(instanceId, "Reopen", actorEmployeeId, actorRoles, reason, ipAddress, userAgent);

        tq.Status = "Reopened";
        await _context.SaveChangesAsync();
    }


    // =========================
    // Helpers
    // =========================

    private async Task<string> GenerateNumberAsync(string queryType, int projectId)
    {
        var count = await _context.TechnicalQueries
            .CountAsync(x => x.ProjectId == projectId && x.QueryType == queryType);

        return $"{queryType}-{projectId}-{(count + 1):0000}";
    }

    private async Task<TechnicalQuery> GetOrThrowAsync(int id)
    {
        var tq = await _context.TechnicalQueries.FirstOrDefaultAsync(x => x.TechnicalQueryId == id);
        if (tq == null) throw new Exception("TQ/TSR/RFI یافت نشد.");
        return tq;
    }

    private async Task<int> GetActiveInstanceIdOrThrowAsync(int technicalQueryId)
    {
        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, technicalQueryId);

        if (instance == null || instance.Status != "Running")
            throw new Exception("این مورد Instance فعالی در گردش‌کار ندارد.");

        return instance.WorkflowInstanceId;
    }
}
