using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

public class MOCService : IMOCService
{
    private const string Module = MOCWorkflowSeeder.Module;
    private const string BusinessObjectType = "MOC";
    private const string Category = MOCWorkflowSeeder.Category;

    private static readonly string[] ValidRiskLevels = { "Low", "Medium", "High" };

    private readonly PEMSDbContext _context;
    private readonly IWorkflowRuntime _runtime;

    public MOCService(PEMSDbContext context, IWorkflowRuntime runtime)
    {
        _context = context;
        _runtime = runtime;
    }


    public async Task<int> RaiseAsync(
        string title, string description, string? reason, string changeType, string riskLevel,
        int projectId, int disciplineId, int? relatedDocumentId, int? relatedEquipmentId,
        DateTime? temporaryExpiryDate, bool trainingRequired,
        int? raisedByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new Exception("عنوان الزامی است.");

        var normalizedRisk = string.IsNullOrWhiteSpace(riskLevel) ? "Low" : riskLevel.Trim();
        if (!ValidRiskLevels.Contains(normalizedRisk))
            throw new Exception($"سطح ریسک نامعتبر است: {riskLevel} (باید Low/Medium/High باشد)");

        var mocNumber = await GenerateNumberAsync(projectId);

        var moc = new MOC
        {
            MOCNumber = mocNumber,
            ProjectId = projectId,
            DisciplineId = disciplineId,
            Title = title,
            Description = description,
            Reason = reason,
            ChangeType = string.IsNullOrWhiteSpace(changeType) ? "Permanent" : changeType,
            TemporaryExpiryDate = temporaryExpiryDate,
            RiskLevel = normalizedRisk,
            RelatedDocumentId = relatedDocumentId,
            RelatedEquipmentId = relatedEquipmentId,
            TrainingRequired = trainingRequired,
            RequestedById = raisedByEmployeeId,
            RequestedDate = DateTime.Now,
            Status = "PendingRiskAssessment",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.MOCs.AddAsync(moc);
        await _context.SaveChangesAsync();

        var firstStageParticipants = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(disciplineId) };

        await _runtime.StartInstanceAsync(
            Module, BusinessObjectType, moc.MOCId, Category, projectId, raisedByEmployeeId, firstStageParticipants);

        return moc.MOCId;
    }


    public async Task AssessRiskAsync(
        int mocId, string riskAssessmentText, int? actorEmployeeId, int actorDisciplineId,
        string? ipAddress = null, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(riskAssessmentText))
            throw new Exception("متن ارزیابی ریسک نمی‌تواند خالی باشد.");

        var moc = await GetOrThrowAsync(mocId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(mocId);

        var actorRoles = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(actorDisciplineId) };

        await _runtime.ExecuteActionAsync(instanceId, "Assess", actorEmployeeId, actorRoles, riskAssessmentText, ipAddress, userAgent);

        moc.RiskAssessmentText = riskAssessmentText;
        moc.RiskAssessedById = actorEmployeeId;
        moc.RiskAssessedDate = DateTime.Now;
        moc.Status = "PendingApproval";

        await _context.SaveChangesAsync();
    }


    // ==============================================================================
    // طبق رویه‌ی PSM: اگر RiskLevel=High باشد، مرحله‌ی بعدی (SeniorApproval) با اعضای واقعی
    // فعال می‌شود؛ در غیر این صورت لیست خالی فرستاده می‌شود و موتور خودش آن مرحله را رد
    // می‌کند (همون مکانیزم Auto-Skip که برای IDC ساختیم).
    // ==============================================================================
    public async Task ApproveAsync(
        int mocId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var moc = await GetOrThrowAsync(mocId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(mocId);

        var seniorApprovalParticipants = moc.RiskLevel == "High"
            ? new[] { SystemRoles.SystemAdmin, SystemRoles.EngineeringManager }
            : Array.Empty<string>();

        await _runtime.ExecuteActionAsync(
            instanceId, "Approve", actorEmployeeId, actorRoles, null, ipAddress, userAgent, seniorApprovalParticipants);

        // اگر ریسک بالا نبود، موتور همین الان خودکار تا Closure جلو رفته؛ وضعیت را متناسب ثبت می‌کنیم
        moc.ApprovedById = actorEmployeeId;
        moc.ApprovedDate = DateTime.Now;
        moc.Status = moc.RiskLevel == "High" ? "PendingSeniorApproval" : "Approved";

        await _context.SaveChangesAsync();
    }


    public async Task RejectAsync(
        int mocId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var moc = await GetOrThrowAsync(mocId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(mocId);

        await _runtime.ExecuteActionAsync(instanceId, "Reject", actorEmployeeId, actorRoles, reason, ipAddress, userAgent);

        moc.RejectionReason = reason;
        moc.Status = "Rejected";

        await _context.SaveChangesAsync();
    }


    public async Task RecordImplementationAsync(
        int mocId, string implementationPlan, int actorEmployeeId,
        string? ipAddress = null, string? userAgent = null)
    {
        var moc = await GetOrThrowAsync(mocId);

        if (moc.Status != "Approved" && moc.Status != "Implementing")
            throw new Exception("این MOC هنوز تایید نهایی نشده - ابتدا باید تایید بشه.");

        moc.ImplementationPlan = implementationPlan;
        moc.ImplementedById = actorEmployeeId;
        moc.ImplementedDate = DateTime.Now;
        moc.Status = "PendingClosure";

        await _context.SaveChangesAsync();
    }


    public async Task MarkDocumentsUpdatedAsync(int mocId, string? note, int actorEmployeeId)
    {
        var moc = await GetOrThrowAsync(mocId);

        moc.DocumentsUpdated = true;
        moc.UpdatedDocumentsNote = note;

        await _context.SaveChangesAsync();
    }


    public async Task MarkTrainingCompletedAsync(int mocId, int actorEmployeeId)
    {
        var moc = await GetOrThrowAsync(mocId);

        moc.TrainingCompleted = true;

        await _context.SaveChangesAsync();
    }


    // ==============================================================================
    // طبق رویه‌ی استاندارد MOC: بستن نهایی فقط وقتی چک‌لیست کامل باشد ممکن است - این‌جا
    // نمی‌گذاریم یک MOC بدون به‌روزرسانی مدارک یا بدون آموزش لازم بسته بشه.
    // ==============================================================================
    public async Task CloseAsync(
        int mocId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null)
    {
        var moc = await GetOrThrowAsync(mocId);

        if (!moc.DocumentsUpdated)
            throw new Exception("قبل از بستن MOC باید مدارک متاثر به‌روزرسانی و علامت‌گذاری بشن.");

        if (moc.TrainingRequired && !moc.TrainingCompleted)
            throw new Exception("این MOC نیاز به آموزش دارد و هنوز تکمیل نشده - قبل از بستن باید آموزش انجام بشه.");

        var instanceId = await GetActiveInstanceIdOrThrowAsync(mocId);

        await _runtime.ExecuteActionAsync(instanceId, "Close", actorEmployeeId, actorRoles, null, ipAddress, userAgent);

        moc.Status = "Closed";
        moc.ClosedById = actorEmployeeId;
        moc.ClosedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }


    // =========================
    // Helpers
    // =========================

    private async Task<string> GenerateNumberAsync(int projectId)
    {
        var count = await _context.MOCs.CountAsync(x => x.ProjectId == projectId);
        return $"MOC-{projectId}-{(count + 1):0000}";
    }

    private async Task<MOC> GetOrThrowAsync(int id)
    {
        var moc = await _context.MOCs.FirstOrDefaultAsync(x => x.MOCId == id);
        if (moc == null) throw new Exception("MOC یافت نشد.");
        return moc;
    }

    private async Task<int> GetActiveInstanceIdOrThrowAsync(int mocId)
    {
        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, mocId);

        if (instance == null || instance.Status != "Running")
            throw new Exception("این MOC Instance فعالی در گردش‌کار ندارد.");

        return instance.WorkflowInstanceId;
    }
}
