using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-02 (بخش Workflow از سند Remediation Directive، §55 "Workflow"): این تست‌ها
/// روی گردش‌کار واقعی Check -> IDC -> Approval -> Issue اجرا می‌شوند - نه یک شبیه‌سازی
/// ساده‌شده - همان WorkflowRuntimeService و همان تعریف Seed شده که در Production استفاده
/// می‌شود.
/// </summary>
public class EngineeringWorkflowTests : EngineeringWorkflowTestBase
{
    [Fact]
    public async Task SubmitAsync_FromDraft_MovesToSubmitted()
    {
        var doc = CreateDraftDocument();

        await WorkflowService.SubmitAsync(doc.DocumentId, "submit", 1, ManagerRole);

        Assert.Equal("Submitted", doc.Status);
    }

    [Fact]
    public async Task SubmitAsync_FromNonDraftNonRejected_Throws()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);

        // حالا Status == "Submitted" است - دوباره Submit کردن باید رد شود
        await Assert.ThrowsAsync<Exception>(() => WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole));
    }

    [Fact]
    public async Task CheckAsync_WithNoIdcDisciplines_SkipsIdcStage()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole, idcDisciplineIds: null);

        await WorkflowService.CheckAsync(doc.DocumentId, "checked", 1, SupervisorRole);

        // طبق EngineeringDocumentWorkflowSeeder: مرحله‌ی IDC اگر هیچ دیسیپلینی نداشته باشد،
        // موتور خودش رد می‌کند و مستقیم به Approval می‌رود - MapStageToLegacyStatus آن را
        // "Checked" نشان می‌دهد.
        Assert.Equal("Checked", doc.Status);
    }

    [Fact]
    public async Task CheckAsync_UnauthorizedRole_Throws()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);

        // DisciplineEngineer در ResponsibleRole مرحله‌ی Check نیست (فقط SystemAdmin/
        // EngineeringManager/EngineeringSupervisor)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => WorkflowService.CheckAsync(doc.DocumentId, "checked", 2, DisciplineEngineerRole));
    }

    [Fact]
    public async Task CheckAsync_WithIdcDisciplines_StaysInIdcUntilAllRespond()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole,
            idcDisciplineIds: new[] { DisciplineMech.DisciplineId, DisciplineElec.DisciplineId });

        await WorkflowService.CheckAsync(doc.DocumentId, "checked", 1, SupervisorRole);
        Assert.Equal("IDC Review", doc.Status);

        // فقط یک دیسیپلین پاسخ می‌دهد - هنوز باید در IDC Review بماند
        await WorkflowService.IdcRespondAsync(doc.DocumentId, "Approve", "ok", 3, DisciplineMech.DisciplineId);
        Assert.Equal("IDC Review", doc.Status);

        // دیسیپلین دوم هم پاسخ می‌دهد - حالا باید به Approval برود
        await WorkflowService.IdcRespondAsync(doc.DocumentId, "Approve", "ok", 4, DisciplineElec.DisciplineId);
        Assert.Equal("Checked", doc.Status); // "Approval" stage -> legacy "Checked"
    }

    [Fact]
    public async Task IdcRespondAsync_FromUnreferredDiscipline_Throws()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole,
            idcDisciplineIds: new[] { DisciplineMech.DisciplineId });
        await WorkflowService.CheckAsync(doc.DocumentId, null, 1, SupervisorRole);

        // Electrical هیچ‌وقت به IDC ارجاع نشده - نباید بتواند رأی بدهد
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => WorkflowService.IdcRespondAsync(doc.DocumentId, "Approve", null, 5, DisciplineElec.DisciplineId));
    }

    [Fact]
    public async Task IdcRespondAsync_Reject_RejectsWholeDocument_AndClearsCheckMetadata()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole,
            idcDisciplineIds: new[] { DisciplineMech.DisciplineId });
        await WorkflowService.CheckAsync(doc.DocumentId, null, 1, SupervisorRole);
        Assert.NotNull(doc.CheckedById);

        await WorkflowService.IdcRespondAsync(doc.DocumentId, "Reject", "not acceptable", 3, DisciplineMech.DisciplineId);

        Assert.Equal("Rejected", doc.Status);
        Assert.Null(doc.CheckedById);
        Assert.Null(doc.CheckedDate);

        // طبق درخواست ممیزی: رد در IDC باید یک نامه‌ی داخلی خودکار بسازد
        Assert.Contains(Letters.CreatedLetters, l => l.RelatedDocumentId == doc.DocumentId);
    }

    [Fact]
    public async Task FullLifecycle_DraftToIssued_ReachesIssuedStatus()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);
        await WorkflowService.CheckAsync(doc.DocumentId, null, 1, SupervisorRole);       // -> Checked (no IDC)
        await WorkflowService.ApproveAsync(doc.DocumentId, null, 1, ManagerRole);        // -> Approved
        Assert.Equal("Approved", doc.Status);
        Assert.NotNull(doc.ApprovedById);

        await WorkflowService.IssueAsync(doc.DocumentId, null, 1, DocControllerRole,
            deliveryUnitType: PEMS.Persistence.Services.DocumentWorkflowService.DeliveryUnitInternalDcc);

        Assert.Equal("Issued", doc.Status);
    }

    [Fact]
    public async Task RejectAsync_AtCheckStage_SetsRejectedAndCreatesInternalLetter()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);

        await WorkflowService.RejectAsync(doc.DocumentId, "insufficient data", 1, SupervisorRole);

        Assert.Equal("Rejected", doc.Status);
        Assert.Contains(Letters.CreatedLetters, l => l.RelatedDocumentId == doc.DocumentId);
    }

    // طبق Phase E-04 (Revise & Resubmit)

    [Fact]
    public async Task ReviseAndResubmitAsync_AtCheckStage_SetsDistinctStatus_NotRejected()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);

        await WorkflowService.ReviseAndResubmitAsync(doc.DocumentId, "please add missing data", 1, SupervisorRole);

        Assert.Equal("Revise & Resubmit", doc.Status);
        Assert.Contains(Letters.CreatedLetters, l => l.RelatedDocumentId == doc.DocumentId);
    }

    [Fact]
    public async Task ReviseAndResubmitAsync_ClearsCheckApproveMetadata()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);
        await WorkflowService.CheckAsync(doc.DocumentId, null, 1, SupervisorRole);
        Assert.NotNull(doc.CheckedById);

        await WorkflowService.ReviseAndResubmitAsync(doc.DocumentId, "rework needed", 1, ManagerRole);

        Assert.Null(doc.CheckedById);
        Assert.Null(doc.CheckedDate);
    }

    [Fact]
    public async Task SubmitAsync_FromReviseAndResubmitStatus_Succeeds()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);
        await WorkflowService.ReviseAndResubmitAsync(doc.DocumentId, "rework needed", 1, ManagerRole);
        Assert.Equal("Revise & Resubmit", doc.Status);

        // طبق Phase E-04: بدون این تغییر، "Revise & Resubmit" یک بن‌بست بود - Submit دوباره
        // فقط از Draft/Rejected مجاز بود.
        await WorkflowService.SubmitAsync(doc.DocumentId, "resubmitting with fix", 1, ManagerRole);

        Assert.Equal("Submitted", doc.Status);
    }

    // طبق Phase E-04 (IssuePurpose در لحظه‌ی Issue)

    [Fact]
    public async Task IssueAsync_WithIssuePurpose_PersistsIt()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);
        await WorkflowService.CheckAsync(doc.DocumentId, null, 1, SupervisorRole);
        await WorkflowService.ApproveAsync(doc.DocumentId, null, 1, ManagerRole);

        await WorkflowService.IssueAsync(doc.DocumentId, null, 1, DocControllerRole,
            deliveryUnitType: PEMS.Persistence.Services.DocumentWorkflowService.DeliveryUnitInternalDcc,
            issuePurpose: "Issued For Construction");

        Assert.Equal("Issued For Construction", doc.IssuePurpose);
    }
}
