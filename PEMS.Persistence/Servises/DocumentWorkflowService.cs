using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Dtos;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;


// ==============================================================================
// آداپتور نازک روی موتور عمومی IWorkflowRuntime برای ماژول Engineering Document. شامل:
//   - IDC واقعی (فاز D)
//   - اتصال نامه‌نگاری داخلی PEMS (فاز E) - نه ماژول Vendor Correspondence که جداست:
//       * Reject (چه از Check چه از IDC) => یک نامه‌ی داخلی رسمی خودکار ساخته می‌شود
//       * Issue => باید «واحد تحویل» مشخص شود؛ اگر بیرونی باشد، نامه‌ی خروجی رسمی اجباری است
//       * پاسخ کارفرما => باید از روی یک نامه‌ی وارده‌ی واقعی ثبت‌شده باشد، نه متن آزاد
// ==============================================================================
public class DocumentWorkflowService
    : IDocumentWorkflowService
{
    private const string Module = EngineeringDocumentWorkflowSeeder.Module;
    private const string BusinessObjectType = "Document";
    private const string Category = EngineeringDocumentWorkflowSeeder.Category;

    public const string DeliveryUnitInternalDcc = "InternalDCC";
    public const string DeliveryUnitInternalDepartment = "InternalDepartment";
    public const string DeliveryUnitExternal = "External";

    private readonly PEMSDbContext _context;
    private readonly IWorkflowRuntime _runtime;
    private readonly ILetterService _letterService;
    private readonly PEMS.Application.Interfaces.IAccessControlService _accessControlService;

    public DocumentWorkflowService(
        PEMSDbContext context, IWorkflowRuntime runtime, ILetterService letterService,
        PEMS.Application.Interfaces.IAccessControlService accessControlService)
    {
        _context = context;
        _runtime = runtime;
        _letterService = letterService;
        _accessControlService = accessControlService;
    }


    public async Task SubmitAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        IReadOnlyList<int>? idcDisciplineIds = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var document = await GetDocumentOrThrowAsync(documentId);

        // طبق Phase E-04: "Revise & Resubmit" هم مثل "Rejected" یک نقطه‌ی شروع مجاز برای
        // Submit دوباره است - وگرنه ReviseAndResubmitAsync یک بن‌بست می‌ساخت.
        if (document.Status != "Draft" && document.Status != "Rejected" && document.Status != "Revise & Resubmit")
            throw new Exception($"Cannot change status from {document.Status} to Submitted");

        await _runtime.StartInstanceAsync(Module, BusinessObjectType, documentId, Category, document.ProjectId, actionByEmployeeId);

        var previousReferrals = _context.DocumentIdcReferrals.Where(r => r.DocumentId == documentId);
        _context.DocumentIdcReferrals.RemoveRange(previousReferrals);

        if (idcDisciplineIds != null)
        {
            foreach (var disciplineId in idcDisciplineIds.Distinct())
            {
                _context.DocumentIdcReferrals.Add(new DocumentIdcReferral
                {
                    DocumentId = documentId,
                    DisciplineId = disciplineId,
                    CreatedById = actionByEmployeeId,
                    CreatedDate = DateTime.Now
                });
            }
        }

        document.Status = "Submitted";
        document.ModifiedDate = DateTime.Now;

        await SaveWithConcurrencyCheckAsync();
    }


    public async Task CheckAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var document = await GetDocumentOrThrowAsync(documentId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        var idcDisciplineIds = await _context.DocumentIdcReferrals
            .Where(r => r.DocumentId == documentId)
            .Select(r => r.DisciplineId)
            .ToListAsync();

        var idcParticipantRoles = idcDisciplineIds.Select(EngineeringDocumentWorkflowSeeder.DisciplineRole).ToList();

        await _runtime.ExecuteActionAsync(
            instanceId, "Approve", actionByEmployeeId, actorRoles, comment, ipAddress, userAgent, idcParticipantRoles);

        var updated = await _runtime.GetInstanceByIdAsync(instanceId);
        document.Status = MapStageToLegacyStatus(updated!);
        document.ModifiedDate = DateTime.Now;
        document.CheckedById = actionByEmployeeId;
        document.CheckedDate = DateTime.Now;
        document.CheckedBy = await GetEmployeeFullNameAsync(actionByEmployeeId);

        await SaveWithConcurrencyCheckAsync();
    }


    public async Task ApproveAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var document = await GetDocumentOrThrowAsync(documentId);

        // طبق بخش M (بند ۱۵ - Hold Business Rules): سند در Hold نباید تصادفاً مثل یک سند
        // آماده/تأییدشده رفتار کند - Approve دقیقاً همان نوع «finalization» است که باید مسدود شود
        if (document.IsOnHold)
            throw new InvalidOperationException($"مدرک {document.DocumentNo} در وضعیت Hold است و قابل تأیید (Approve) نیست - ابتدا Hold را رفع کنید.");

        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        await _runtime.ExecuteActionAsync(instanceId, "Approve", actionByEmployeeId, actorRoles, comment, ipAddress, userAgent);

        var updated = await _runtime.GetInstanceByIdAsync(instanceId);
        document.Status = MapStageToLegacyStatus(updated!);
        document.ModifiedDate = DateTime.Now;
        document.ApprovedById = actionByEmployeeId;
        document.ApprovedDate = DateTime.Now;
        document.ApprovedBy = await GetEmployeeFullNameAsync(actionByEmployeeId);

        await SaveWithConcurrencyCheckAsync();
    }


    public async Task RejectAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var document = await GetDocumentOrThrowAsync(documentId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        await _runtime.ExecuteActionAsync(instanceId, "Reject", actionByEmployeeId, actorRoles, comment, ipAddress, userAgent);

        var updated = await _runtime.GetInstanceByIdAsync(instanceId);
        document.Status = MapStageToLegacyStatus(updated!);
        document.ModifiedDate = DateTime.Now;
        ClearCheckApproveMetadata(document);

        await SaveWithConcurrencyCheckAsync();

        await CreateInternalRejectionLetterAsync(document, "Check", comment, actionByEmployeeId);
    }


    // ==============================================================================
    // طبق Phase E-04: دقیقاً همان الگوی PermitService.ReturnForReworkAsync - موتور گردش‌کار
    // عمومی هیچ Action مجزای "Return" ندارد؛ از همان گذار واقعی "Reject" (که از هر مرحله‌ای
    // که سند در آن است در دسترس است) استفاده می‌شود، فقط به‌جای اعتماد به وضعیت ضمنی موتور
    // ("Rejected")، این‌جا صریحاً "Revise & Resubmit" ثبت می‌شود.
    // ==============================================================================
    public async Task ReviseAndResubmitAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var document = await GetDocumentOrThrowAsync(documentId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        await _runtime.ExecuteActionAsync(instanceId, "Reject", actionByEmployeeId, actorRoles, comment, ipAddress, userAgent);

        document.Status = "Revise & Resubmit";
        document.ModifiedDate = DateTime.Now;
        ClearCheckApproveMetadata(document);

        await SaveWithConcurrencyCheckAsync();

        await CreateInternalRejectionLetterAsync(document, "Revise & Resubmit", comment, actionByEmployeeId);
    }


    // ==============================================================================
    // فاز E: انتخاب «واحد تحویل» بعد از Approve. اگر بیرونی باشد (DCC کارفرما/پیمانکار)،
    // یک نامه‌ی رسمی خروجی اجباری است. اگر داخلی باشد (DCC خودمان یا واحد دیگر)، ثبت نامه
    // اختیاری است - فقط اگر فیلدهای نامه پر شده باشند ساخته می‌شود.
    // ==============================================================================
    public async Task IssueAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string deliveryUnitType,
        int? deliveryDepartmentId = null,
        string? externalReceiverName = null,
        string? letterNumber = null,
        string? letterSubject = null,
        string? letterBody = null,
        string? issuePurpose = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (deliveryUnitType != DeliveryUnitInternalDcc
            && deliveryUnitType != DeliveryUnitInternalDepartment
            && deliveryUnitType != DeliveryUnitExternal)
        {
            throw new Exception("نوع واحد تحویل نامعتبر است.");
        }

        if (deliveryUnitType == DeliveryUnitExternal)
        {
            if (string.IsNullOrWhiteSpace(externalReceiverName) || string.IsNullOrWhiteSpace(letterSubject))
                throw new Exception("برای تحویل به واحد بیرونی، نام گیرنده و موضوع نامه‌ی رسمی الزامی است.");
        }

        if (deliveryUnitType == DeliveryUnitInternalDepartment && !deliveryDepartmentId.HasValue)
            throw new Exception("برای تحویل به واحد داخلی دیگر، انتخاب واحد الزامی است.");

        var document = await GetDocumentOrThrowAsync(documentId);

        if (document.IsOnHold)
            throw new InvalidOperationException($"مدرک {document.DocumentNo} در وضعیت Hold است و قابل صدور (Issue) نیست - ابتدا Hold را رفع کنید.");

        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        await _runtime.ExecuteActionAsync(instanceId, "Issue", actionByEmployeeId, actorRoles, comment, ipAddress, userAgent);

        var updated = await _runtime.GetInstanceByIdAsync(instanceId);
        document.Status = MapStageToLegacyStatus(updated!);
        document.ModifiedDate = DateTime.Now;

        // طبق Phase E-04: هدف واقعی این صدور مشخص (IFC/IFA/IFI/...) - نه مقدار قدیمی که موقع
        // Create/Edit یا یک رویژن قبلی ثبت شده بود
        if (!string.IsNullOrWhiteSpace(issuePurpose))
            document.IssuePurpose = issuePurpose;

        await SaveWithConcurrencyCheckAsync();

        var withRevisions = await _context.EngineeringDocuments
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        var latestRevision = withRevisions?.Revisions.OrderByDescending(x => x.RevisionId).FirstOrDefault();
        if (latestRevision != null)
        {
            latestRevision.IssuedDate = DateTime.Now;
            await SaveWithConcurrencyCheckAsync();
        }

        // ==============================================================================
        // نامه‌ی تحویل - اجباری فقط برای واحد بیرونی، اختیاری برای دو حالت داخلی
        // ==============================================================================
        var needsLetter = deliveryUnitType == DeliveryUnitExternal || !string.IsNullOrWhiteSpace(letterSubject);
        if (!needsLetter) return;

        var letterType = deliveryUnitType == DeliveryUnitExternal ? "Outgoing" : "Internal";
        var generatedNumber = string.IsNullOrWhiteSpace(letterNumber)
            ? GenerateLetterNumber(document.DocumentNo, "OUT")
            : letterNumber!;

        var letterId = await _letterService.CreateAsync(
            letterType: letterType,
            letterNumber: generatedNumber,
            letterDate: DateTime.Now,
            sender: null,
            receiver: deliveryUnitType == DeliveryUnitExternal ? externalReceiverName : null,
            subject: string.IsNullOrWhiteSpace(letterSubject) ? $"صدور مدرک {document.DocumentNo}" : letterSubject!,
            body: letterBody,
            priority: "Normal",
            confidentialityLevel: "Internal",
            projectId: document.ProjectId,
            disciplineId: document.DisciplineId,
            relatedDocumentId: documentId,
            relatedMRId: null,
            registeredById: actionByEmployeeId);

        if (deliveryUnitType == DeliveryUnitInternalDepartment && deliveryDepartmentId.HasValue)
        {
            await _letterService.AddRecipientAsync(letterId, "To", null, deliveryDepartmentId.Value, null);
        }
        else if (deliveryUnitType == DeliveryUnitExternal && !string.IsNullOrWhiteSpace(externalReceiverName))
        {
            await _letterService.AddRecipientAsync(letterId, "To", null, null, externalReceiverName);
        }
    }


    // ==============================================================================
    // پاسخ کارفرما دیگر متن آزاد نیست - باید نامه‌ی وارده‌ی واقعی انتخاب شود. این نامه اگر
    // هنوز به این مدرک وصل نبود، همین‌جا وصل می‌شود (برای ردیابی دوطرفه در تب Correspondence).
    // ==============================================================================
    public async Task RecordClientResponseAsync(
        int documentId,
        string clientStatus,
        int incomingLetterId,
        string? clientComment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null)
    {
        // طبق فاز ۳ RBAC-ABAC: آرایه‌ی ثابت ClientResponseRoles جایگزین شد با مجوز داده‌محور
        // «Document.RecordClientResponse» (AccessControlSeeder.SeedPhase3PermissionsAsync) -
        // همون سه نقش (SystemAdmin از طریق Bypass عمومی، EngineeringManager، DocumentController).
        if (actorRoles == null || !(await _accessControlService.AuthorizeForRolesAsync(actorRoles, "Document", "RecordClientResponse")).Allowed)
            throw new UnauthorizedAccessException("شما مجاز به ثبت پاسخ کارفرما نیستید.");

        var document = await GetDocumentOrThrowAsync(documentId);

        if (document.Status != "Issued")
            throw new Exception("Client response can only be recorded on issued documents");

        var letter = await _context.Letters.FirstOrDefaultAsync(l => l.LetterId == incomingLetterId && l.IsActive);
        if (letter == null)
            throw new Exception("نامه‌ی وارده‌ی انتخاب‌شده یافت نشد.");

        if (letter.LetterType != "Incoming")
            throw new Exception("پاسخ کارفرما فقط می‌تواند از روی یک نامه‌ی وارده (Incoming) ثبت شود.");

        if (letter.RelatedDocumentId.HasValue && letter.RelatedDocumentId.Value != documentId)
            throw new Exception("این نامه از قبل به مدرک دیگری وصل شده است.");

        if (!letter.RelatedDocumentId.HasValue)
            letter.RelatedDocumentId = documentId;

        var normalizedCode = clientStatus.Trim().ToUpperInvariant();

        document.ClientApprovalStatus = clientStatus;
        document.ClientComment = string.IsNullOrWhiteSpace(clientComment)
            ? $"طبق نامه‌ی وارده شماره {letter.LetterNumber}"
            : $"{clientComment} (طبق نامه‌ی وارده شماره {letter.LetterNumber})";
        document.ClientResponseDate = DateTime.Now;

        document.Status = normalizedCode switch
        {
            "A" => "Client Approved",
            "B" or "C" or "D" => "Rejected",
            _ => throw new Exception($"کد وضعیت کارفرما نامعتبر است: {clientStatus} (باید A/B/C/D باشد)")
        };

        document.ModifiedDate = DateTime.Now;

        if (document.Status == "Rejected")
            ClearCheckApproveMetadata(document);

        await SaveWithConcurrencyCheckAsync();
    }


    public async Task IdcRespondAsync(
        int documentId,
        string decision,
        string? comment,
        int actorEmployeeId,
        int actorDisciplineId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var normalizedDecision = decision.Trim();
        if (normalizedDecision != "Approve" && normalizedDecision != "Comment" && normalizedDecision != "Reject")
            throw new Exception("تصمیم نامعتبر است (باید Approve/Comment/Reject باشد).");

        var document = await GetDocumentOrThrowAsync(documentId);
        var instanceId = await GetActiveInstanceIdOrThrowAsync(documentId);

        var actorRoles = new[] { EngineeringDocumentWorkflowSeeder.DisciplineRole(actorDisciplineId) };

        await _runtime.ExecuteActionAsync(instanceId, normalizedDecision, actorEmployeeId, actorRoles, comment, ipAddress, userAgent);

        var updated = await _runtime.GetInstanceByIdAsync(instanceId);
        document.Status = MapStageToLegacyStatus(updated!);
        document.ModifiedDate = DateTime.Now;

        var causedRejection = updated!.Status == "Rejected";
        if (causedRejection) ClearCheckApproveMetadata(document);

        await SaveWithConcurrencyCheckAsync();

        if (causedRejection)
            await CreateInternalRejectionLetterAsync(document, "IDC", comment, actorEmployeeId);
    }


    public async Task<IReadOnlyList<IdcStatusItemDto>> GetIdcStatusAsync(int documentId)
    {
        var instance = await _context.WorkflowInstances
            .Where(x => x.Module == Module && x.BusinessObjectType == BusinessObjectType && x.BusinessObjectId == documentId)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync();

        if (instance == null) return Array.Empty<IdcStatusItemDto>();

        var idcStage = await _context.WorkflowStages
            .FirstOrDefaultAsync(s => s.WorkflowVersionId == instance.WorkflowVersionId && s.Name == "IDC");

        if (idcStage == null) return Array.Empty<IdcStatusItemDto>();

        var instanceStages = await _context.WorkflowInstanceStages
            .Where(x => x.WorkflowInstanceId == instance.WorkflowInstanceId && x.WorkflowStageId == idcStage.WorkflowStageId)
            .ToListAsync();

        if (instanceStages.Count == 0) return Array.Empty<IdcStatusItemDto>();

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
                return new IdcStatusItemDto
                {
                    DisciplineId = did ?? 0,
                    DisciplineName = did.HasValue ? disciplineNames.GetValueOrDefault(did.Value, "?") : "?",
                    Status = x.Status
                };
            })
            .ToList();
    }


    // =========================
    // Helpers
    // =========================

    private async Task CreateInternalRejectionLetterAsync(
        EngineeringDocument document, string rejectedAtStage, string? comment, int? actionByEmployeeId)
    {
        await _letterService.CreateAsync(
            letterType: "Internal",
            letterNumber: GenerateLetterNumber(document.DocumentNo, "REJ"),
            letterDate: DateTime.Now,
            sender: null,
            receiver: null,
            subject: $"رد مدرک {document.DocumentNo} در مرحله‌ی {rejectedAtStage}",
            body: string.IsNullOrWhiteSpace(comment) ? "بدون توضیح ثبت‌شده." : comment,
            priority: "High",
            confidentialityLevel: "Internal",
            projectId: document.ProjectId,
            disciplineId: document.DisciplineId,
            relatedDocumentId: document.DocumentId,
            relatedMRId: null,
            registeredById: actionByEmployeeId);
    }

    private static string GenerateLetterNumber(string documentNo, string prefix)
        => $"{prefix}-{documentNo}-{DateTime.Now:yyyyMMdd-HHmmss}";

    private static int? ParseDisciplineId(string? role)
    {
        if (role == null || !role.StartsWith(EngineeringDocumentWorkflowSeeder.DisciplineRolePrefix)) return null;
        var idPart = role[EngineeringDocumentWorkflowSeeder.DisciplineRolePrefix.Length..];
        return int.TryParse(idPart, out var id) ? id : null;
    }

    private static void ClearCheckApproveMetadata(EngineeringDocument document)
    {
        document.CheckedById = null;
        document.CheckedDate = null;
        document.CheckedBy = null;
        document.ApprovedById = null;
        document.ApprovedDate = null;
        document.ApprovedBy = null;
    }

    private async Task<EngineeringDocument> GetDocumentOrThrowAsync(int documentId)
    {
        var document = await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null) throw new Exception("Document not found");
        return document;
    }

    private async Task<int> GetActiveInstanceIdOrThrowAsync(int documentId)
    {
        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, documentId);

        if (instance == null || instance.Status != "Running")
            throw new Exception(
                "این مدرک هنوز به موتور گردش‌کار جدید منتقل نشده است (یا Instance فعالی ندارد). " +
                "برای مدارکی که قبل از مهاجرت پلتفرم وسط گردش‌کار بودند، باید اسکریپت انتقال یک‌باره اجرا شود.");

        return instance.WorkflowInstanceId;
    }

    private async Task<string?> GetEmployeeFullNameAsync(int? employeeId)
    {
        if (!employeeId.HasValue) return null;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
        return employee == null ? null : employee.FirstName + " " + employee.LastName;
    }

    private static string MapStageToLegacyStatus(WorkflowInstanceDto instance)
    {
        if (instance.Status == "Rejected") return "Rejected";
        if (instance.Status == "Completed") return "Issued";

        return instance.CurrentStageName switch
        {
            "Check" => "Submitted",
            "IDC" => "IDC Review",
            "Approval" => "Checked",
            "Issue" => "Approved",
            _ => "Submitted"
        };
    }

    private async Task SaveWithConcurrencyCheckAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new Exception(
                "این مدرک هم‌زمان توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید.");
        }
    }
}
