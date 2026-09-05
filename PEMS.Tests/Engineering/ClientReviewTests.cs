using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-02 (بخش Client Review از سند Remediation Directive، §55): این تست‌ها مستقیماً
/// DocumentWorkflowService.RecordClientResponseAsync را صدا می‌زنند - این متد خودش موتور
/// گردش‌کار عمومی را صدا نمی‌زند (طبق طراحی، پاسخ کارفرما جدا از تایید داخلی است)، پس نیازی
/// به Instance فعال نیست؛ فقط باید Document.Status == "Issued" باشد.
/// </summary>
public class ClientReviewTests : EngineeringWorkflowTestBase
{
    private Letter CreateIncomingLetter(int documentId = 0)
    {
        var letter = new Letter
        {
            LetterType = "Incoming",
            LetterNumber = $"IN-{Guid.NewGuid():N}".Substring(0, 15),
            Subject = "Client feedback",
            LetterDate = DateTime.Now,
            IsActive = true,
            RelatedDocumentId = documentId == 0 ? null : documentId
        };
        Ctx.Letters.Add(letter);
        Ctx.SaveChanges();
        return letter;
    }

    private EngineeringDocument CreateIssuedDocument()
    {
        var doc = CreateDraftDocument();
        doc.Status = "Issued";
        Ctx.SaveChanges();
        return doc;
    }

    [Fact]
    public async Task RecordClientResponseAsync_CodeA_ApprovesDocument()
    {
        var doc = CreateIssuedDocument();
        var letter = CreateIncomingLetter();

        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, "looks good", 1, ManagerRole);

        Assert.Equal("Client Approved", doc.Status);
        Assert.Equal("A", doc.ClientApprovalStatus);
        Assert.NotNull(doc.ClientResponseDate);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    public async Task RecordClientResponseAsync_RejectionCodes_RejectDocument_AndClearApprovalMetadata(string code)
    {
        var doc = CreateIssuedDocument();
        doc.ApprovedById = 99; // شبیه‌سازی این‌که مدرک قبلاً تایید داخلی هم داشته
        Ctx.SaveChanges();
        var letter = CreateIncomingLetter();

        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, code, letter.LetterId, "needs rework", 1, ManagerRole);

        Assert.Equal("Rejected", doc.Status);
        Assert.Null(doc.ApprovedById);
    }

    [Fact]
    public async Task RecordClientResponseAsync_InvalidCode_Throws()
    {
        var doc = CreateIssuedDocument();
        var letter = CreateIncomingLetter();

        await Assert.ThrowsAsync<Exception>(
            () => WorkflowService.RecordClientResponseAsync(doc.DocumentId, "Z", letter.LetterId, null, 1, ManagerRole));
    }

    [Fact]
    public async Task RecordClientResponseAsync_UnauthorizedRole_Throws()
    {
        var doc = CreateIssuedDocument();
        var letter = CreateIncomingLetter();

        // طبق DocumentWorkflowService.ClientResponseRoles: فقط SystemAdmin/EngineeringManager/
        // DocumentController مجازند - DisciplineEngineer نه.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, null, 1, DisciplineEngineerRole));
    }

    [Fact]
    public async Task RecordClientResponseAsync_OnNonIssuedDocument_Throws()
    {
        var doc = CreateDraftDocument(); // Status == "Draft", not "Issued"
        var letter = CreateIncomingLetter();

        await Assert.ThrowsAsync<Exception>(
            () => WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, null, 1, ManagerRole));
    }

    [Fact]
    public async Task RecordClientResponseAsync_OutgoingLetter_Throws()
    {
        var doc = CreateIssuedDocument();
        var outgoing = new Letter
        {
            LetterType = "Outgoing",
            LetterNumber = $"OUT-{Guid.NewGuid():N}".Substring(0, 15),
            Subject = "x",
            LetterDate = DateTime.Now,
            IsActive = true
        };
        Ctx.Letters.Add(outgoing);
        Ctx.SaveChanges();

        // پاسخ کارفرما فقط می‌تواند از روی یک نامه‌ی Incoming ثبت شود
        await Assert.ThrowsAsync<Exception>(
            () => WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", outgoing.LetterId, null, 1, ManagerRole));
    }
}
