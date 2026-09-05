using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

public class WorkOrderReviewService : IWorkOrderReviewService
{
    private const string Module = WorkOrderReviewWorkflowSeeder.Module;
    private const string BusinessObjectType = "WorkOrder";
    private const string Category = WorkOrderReviewWorkflowSeeder.Category;

    private readonly PEMSDbContext _context;
    private readonly IWorkflowRuntime _runtime;

    public WorkOrderReviewService(PEMSDbContext context, IWorkflowRuntime runtime)
    {
        _context = context;
        _runtime = runtime;
    }


    public async Task RequestDisciplineReviewAsync(
        int workOrderId, IReadOnlyList<int> disciplineIds,
        int? actionByEmployeeId, string? ipAddress = null, string? userAgent = null)
    {
        if (disciplineIds == null || disciplineIds.Count == 0)
            throw new Exception("حداقل یک دیسیپلین باید انتخاب بشه.");

        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(x => x.WorkOrderId == workOrderId);
        if (workOrder == null) throw new Exception("WorkOrder یافت نشد.");

        var existingInstance = await _runtime.GetInstanceAsync(BusinessObjectType, workOrderId);
        if (existingInstance != null && existingInstance.Status == "Running")
            throw new Exception("این WorkOrder از قبل یک بازبینی چند-دیسیپلینه‌ی فعال داره.");

        // ثبت ارجاع‌ها - همون الگوی DocumentIdcReferral
        var previousReferrals = _context.WorkOrderDisciplineReferrals.Where(r => r.WorkOrderId == workOrderId);
        _context.WorkOrderDisciplineReferrals.RemoveRange(previousReferrals);

        foreach (var disciplineId in disciplineIds.Distinct())
        {
            await _context.WorkOrderDisciplineReferrals.AddAsync(new WorkOrderDisciplineReferral
            {
                WorkOrderId = workOrderId,
                DisciplineId = disciplineId,
                CreatedById = actionByEmployeeId,
                CreatedDate = DateTime.Now
            });
        }

        // یافته‌های چرخه‌ی قبلی (اگه بوده) پاک می‌شه - چرخه‌ی جدید از صفر شروع می‌شه
        var previousFindings = _context.WorkOrderDisciplineFindings.Where(f => f.WorkOrderId == workOrderId);
        _context.WorkOrderDisciplineFindings.RemoveRange(previousFindings);

        await _context.SaveChangesAsync();

        var participantRoles = disciplineIds.Select(EngineeringDocumentWorkflowSeeder.DisciplineRole).ToList();

        await _runtime.StartInstanceAsync(
            Module, BusinessObjectType, workOrderId, Category, null, actionByEmployeeId, participantRoles);
    }


    public async Task RespondAsync(
        int workOrderId, string decision, string? findings, string? failureModeCode, string? failureCauseCode,
        int actorEmployeeId, int actorDisciplineId, string? ipAddress = null, string? userAgent = null)
    {
        var normalizedDecision = decision.Trim();
        if (normalizedDecision != "Approve" && normalizedDecision != "Comment" && normalizedDecision != "Reject")
            throw new Exception("تصمیم نامعتبر است.");

        // طبق ممیزی: قبل از پذیرش پاسخ، باید تایید بشه این دیسیپلین واقعاً برای همین
        // WorkOrder ارجاع شده - وگرنه هر کاربری با هر دیسیپلینی می‌تونست نظر ثبت کنه
        var isReferred = await _context.WorkOrderDisciplineReferrals
            .AnyAsync(r => r.WorkOrderId == workOrderId && r.DisciplineId == actorDisciplineId);

        if (!isReferred)
            throw new Exception("دیسیپلین شما برای این WorkOrder ارجاع نشده - نمی‌تونی نظر ثبت کنی.");

        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, workOrderId);
        if (instance == null || instance.Status != "Running")
            throw new Exception("این WorkOrder بازبینی چند-دیسیپلینه‌ی فعالی نداره.");

        var actorRoles = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(actorDisciplineId) };

        await _runtime.ExecuteActionAsync(
            instance.WorkflowInstanceId, normalizedDecision, actorEmployeeId, actorRoles, findings, ipAddress, userAgent);

        await _context.WorkOrderDisciplineFindings.AddAsync(new WorkOrderDisciplineFinding
        {
            WorkOrderId = workOrderId,
            DisciplineId = actorDisciplineId,
            Decision = normalizedDecision,
            Findings = findings,
            FailureModeCode = failureModeCode,
            FailureCauseCode = failureCauseCode,
            RespondedById = actorEmployeeId,
            RespondedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }


    public async Task<IReadOnlyList<VdrReviewStatusItemDto>> GetDisciplineReviewStatusAsync(int workOrderId)
    {
        var instance = await _context.WorkflowInstances
            .Where(x => x.Module == Module && x.BusinessObjectType == BusinessObjectType && x.BusinessObjectId == workOrderId)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync();

        if (instance == null) return Array.Empty<VdrReviewStatusItemDto>();

        var stage = await _context.WorkflowStages
            .FirstOrDefaultAsync(s => s.WorkflowVersionId == instance.WorkflowVersionId && s.Name == "DisciplineReview");

        if (stage == null) return Array.Empty<VdrReviewStatusItemDto>();

        var instanceStages = await _context.WorkflowInstanceStages
            .Where(x => x.WorkflowInstanceId == instance.WorkflowInstanceId && x.WorkflowStageId == stage.WorkflowStageId)
            .ToListAsync();

        if (instanceStages.Count == 0) return Array.Empty<VdrReviewStatusItemDto>();

        var disciplineIds = instanceStages
            .Select(x => ParseDisciplineId(x.AssignedToRole))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var disciplineNames = await _context.Disciplines
            .Where(d => disciplineIds.Contains(d.DisciplineId))
            .ToDictionaryAsync(d => d.DisciplineId, d => d.Name);

        return instanceStages
            .Select(x =>
            {
                var did = ParseDisciplineId(x.AssignedToRole);
                return new VdrReviewStatusItemDto
                {
                    DisciplineId = did ?? 0,
                    DisciplineName = did.HasValue ? disciplineNames.GetValueOrDefault(did.Value, "?") : "?",
                    Status = x.Status
                };
            })
            .ToList();
    }


    public async Task<bool> HasPendingDisciplineReviewAsync(int workOrderId)
    {
        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, workOrderId);
        return instance != null && instance.Status == "Running";
    }


    private static int? ParseDisciplineId(string? role)
    {
        if (role == null || !role.StartsWith(EngineeringDocumentWorkflowSeeder.DisciplineRolePrefix)) return null;
        var idPart = role[EngineeringDocumentWorkflowSeeder.DisciplineRolePrefix.Length..];
        return int.TryParse(idPart, out var id) ? id : null;
    }
}
