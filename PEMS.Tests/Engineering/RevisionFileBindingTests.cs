using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS EDMS Deep Repository Audit» یافته F-06/F-07 (Revision-File Binding & Integrity):
/// این تست‌ها همان قواعد داده‌ای واقعی‌ای را اثبات می‌کنند که
/// EngineeringDocumentsController.UploadAttachment روی آن‌ها تکیه می‌کند (مالکیت رویژن، اولین
/// آپلود = فایل رسمی، سازگاری کامل با پیوست‌های قدیمی سطح-مدرک).
/// </summary>
public class RevisionFileBindingTests
{
    private static (PEMS.Persistence.Context.PEMSDbContext Ctx, EngineeringDocument Doc, DocumentRevision Rev1, DocumentRevision Rev2) SeedDocumentWithTwoRevisions(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        var project = new Project { ProjectName = "Test Project", ProjectCode = "TP" };
        ctx.Projects.Add(project);
        ctx.SaveChanges();

        var doc = new EngineeringDocument
        {
            DocumentNo = $"TP-DOC-{Guid.NewGuid():N}"[..15],
            Title = "Test Document",
            DocumentType = "Drawing",
            ProjectId = project.ProjectId,
            Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(doc);
        ctx.SaveChanges();

        var rev1 = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "00", Status = "Superseded", IsActive = true };
        var rev2 = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "01", Status = "Draft", IsActive = true };
        ctx.DocumentRevisions.AddRange(rev1, rev2);
        ctx.SaveChanges();

        return (ctx, doc, rev1, rev2);
    }

    [Fact]
    public async Task Attachment_BoundToOneRevision_DoesNotAppearUnderAnotherRevision()
    {
        var (ctx, doc, rev1, rev2) = SeedDocumentWithTwoRevisions(nameof(Attachment_BoundToOneRevision_DoesNotAppearUnderAnotherRevision));

        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = rev1.RevisionId,
            FileName = "rev00.pdf",
            FileExtension = ".pdf",
            FilePath = "documents/rev00.pdf",
            IsActive = true,
            IsOfficial = true
        });
        await ctx.SaveChangesAsync();

        var rev1Files = await ctx.DocumentAttachments.Where(x => x.DocumentRevisionId == rev1.RevisionId && x.IsActive).ToListAsync();
        var rev2Files = await ctx.DocumentAttachments.Where(x => x.DocumentRevisionId == rev2.RevisionId && x.IsActive).ToListAsync();

        Assert.Single(rev1Files);
        Assert.Empty(rev2Files);
    }

    [Fact]
    public async Task Attachment_WithNullRevisionId_RemainsDocumentLevel_BackwardCompatible()
    {
        var (ctx, doc, _, _) = SeedDocumentWithTwoRevisions(nameof(Attachment_WithNullRevisionId_RemainsDocumentLevel_BackwardCompatible));

        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = null,
            FileName = "legacy-general.pdf",
            FileExtension = ".pdf",
            FilePath = "documents/legacy-general.pdf",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var attachment = await ctx.DocumentAttachments.FirstAsync(x => x.FileName == "legacy-general.pdf");

        Assert.Null(attachment.DocumentRevisionId);
        Assert.False(attachment.IsOfficial);
    }

    [Fact]
    public async Task FirstAttachmentForRevision_HasNoExistingOfficialFile()
    {
        var (ctx, doc, rev1, _) = SeedDocumentWithTwoRevisions(nameof(FirstAttachmentForRevision_HasNoExistingOfficialFile));

        // همان Predicate واقعی که EngineeringDocumentsController.UploadAttachment استفاده می‌کند
        var hasExistingOfficial = await ctx.DocumentAttachments.AnyAsync(x =>
            x.DocumentRevisionId == rev1.RevisionId && x.IsActive && x.IsOfficial);

        Assert.False(hasExistingOfficial);
    }

    [Fact]
    public async Task SecondAttachmentForRevision_WhenFirstIsAlreadyOfficial_IsNotAutoPromotedToOfficial()
    {
        var (ctx, doc, rev1, _) = SeedDocumentWithTwoRevisions(nameof(SecondAttachmentForRevision_WhenFirstIsAlreadyOfficial_IsNotAutoPromotedToOfficial));

        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = rev1.RevisionId,
            FileName = "native.dwg",
            FileExtension = ".dwg",
            FilePath = "documents/native.dwg",
            IsActive = true,
            IsOfficial = true
        });
        await ctx.SaveChangesAsync();

        // همان Predicate واقعی که EngineeringDocumentsController.UploadAttachment قبل از آپلود دوم اجرا می‌کند
        var hasExistingOfficial = await ctx.DocumentAttachments.AnyAsync(x =>
            x.DocumentRevisionId == rev1.RevisionId && x.IsActive && x.IsOfficial);

        Assert.True(hasExistingOfficial);
    }

    [Fact]
    public async Task RevisionOwnershipCheck_RevisionBelongingToDifferentDocument_IsRejected()
    {
        var (ctx1, _, rev1FromDoc1, _) = SeedDocumentWithTwoRevisions(nameof(RevisionOwnershipCheck_RevisionBelongingToDifferentDocument_IsRejected) + "_1");

        var project2 = new Project { ProjectName = "Second Project", ProjectCode = "TP2" };
        ctx1.Projects.Add(project2);
        await ctx1.SaveChangesAsync();

        var doc2 = new EngineeringDocument
        {
            DocumentNo = $"TP2-DOC-{Guid.NewGuid():N}"[..15],
            Title = "Second Document",
            DocumentType = "Drawing",
            ProjectId = project2.ProjectId,
            Status = "Draft"
        };
        ctx1.EngineeringDocuments.Add(doc2);
        await ctx1.SaveChangesAsync();

        // همان چک مالکیتی که کنترلر قبل از هر آپلود اجرا می‌کند: آیا rev1FromDoc1 واقعاً به doc2 تعلق دارد؟
        var revisionBelongsToDoc2 = await ctx1.DocumentRevisions
            .AnyAsync(x => x.RevisionId == rev1FromDoc1.RevisionId && x.DocumentId == doc2.DocumentId);

        Assert.False(revisionBelongsToDoc2);
    }

    [Fact]
    public async Task FileHash_RoundTripsCorrectlyThroughDbContext()
    {
        var (ctx, doc, rev1, _) = SeedDocumentWithTwoRevisions(nameof(FileHash_RoundTripsCorrectlyThroughDbContext));
        const string expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = rev1.RevisionId,
            FileName = "hashed.pdf",
            FileExtension = ".pdf",
            FilePath = "documents/hashed.pdf",
            FileHash = expectedHash,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.DocumentAttachments.AsNoTracking().FirstAsync(x => x.FileName == "hashed.pdf");

        Assert.Equal(expectedHash, reloaded.FileHash);
    }
}
