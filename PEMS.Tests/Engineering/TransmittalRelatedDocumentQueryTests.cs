using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS Phase 2A — EDMS Foundation» بخش ۲: ITransmittalService.GetForDocumentAsync -
/// پیوند متقابل جدید Transmittal روی صفحه‌ی Engineering Document Details. الگوی Setup عیناً
/// همون TransmittalGovernanceTests است.
/// </summary>
public class TransmittalRelatedDocumentQueryTests
{
    private static async Task<(TransmittalService svc, Project project, PEMS.Persistence.Context.PEMSDbContext ctx)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        var project = new Project { ProjectName = "Test Project", ProjectCode = "TP" };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        return (new TransmittalService(ctx), project, ctx);
    }

    private static async Task<int> SeedDocumentAsync(PEMS.Persistence.Context.PEMSDbContext ctx, int projectId)
    {
        var doc = new EngineeringDocument
        {
            DocumentNo = $"TP-GEN-DAT-{Guid.NewGuid():N}"[..20],
            Title = "Test Doc",
            DocumentType = "Datasheet",
            ProjectId = projectId,
            Status = "Draft",
            RevisionNo = "00"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();
        return doc.DocumentId;
    }

    [Fact]
    public async Task GetForDocumentAsync_ReturnsTransmittalsIncludingThatDocument()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(GetForDocumentAsync_ReturnsTransmittalsIncludingThatDocument));
        var documentId = await SeedDocumentAsync(ctx, project.ProjectId);

        var transmittalId = await svc.CreateAsync("TR-0001", project.ProjectId, "PEMS", "Vendor Co.",
            "For Review", null, new List<int> { documentId }, false, new List<PEMS.Application.DTOs.TransmittalRecipientInput>());

        var related = await svc.GetForDocumentAsync(documentId);

        Assert.Single(related);
        Assert.Equal(transmittalId, related[0].TransmittalId);
        Assert.Equal("TR-0001", related[0].TransmittalNo);
        Assert.Equal("For Review", related[0].Purpose);
    }

    [Fact]
    public async Task GetForDocumentAsync_UnrelatedDocument_ReturnsEmpty()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(GetForDocumentAsync_UnrelatedDocument_ReturnsEmpty));
        var documentId = await SeedDocumentAsync(ctx, project.ProjectId);
        var unrelatedDocumentId = await SeedDocumentAsync(ctx, project.ProjectId);

        await svc.CreateAsync("TR-0002", project.ProjectId, "PEMS", "Vendor Co.",
            "For Review", null, new List<int> { documentId }, false, new List<PEMS.Application.DTOs.TransmittalRecipientInput>());

        var related = await svc.GetForDocumentAsync(unrelatedDocumentId);

        Assert.Empty(related);
    }

    [Fact]
    public async Task GetForDocumentAsync_CancelledTransmittal_StillReturned_HistoryPreserved()
    {
        // طبق بخش ۸ (Immutability/History): Cancel فقط Status را عوض می‌کند، IsActive را دست‌نخورده
        // می‌گذارد - این خلاصه باید همچنان تاریخچه‌ی Cancel‌شده را نشان دهد، نه پنهانش کند
        var (svc, project, ctx) = await SeedAsync(nameof(GetForDocumentAsync_CancelledTransmittal_StillReturned_HistoryPreserved));
        var documentId = await SeedDocumentAsync(ctx, project.ProjectId);

        var transmittalId = await svc.CreateAsync("TR-0003", project.ProjectId, "PEMS", "Vendor Co.",
            "For Review", null, new List<int> { documentId }, false, new List<PEMS.Application.DTOs.TransmittalRecipientInput>());
        await svc.CancelAsync(transmittalId, "Superseded by TR-0004", null);

        var related = await svc.GetForDocumentAsync(documentId);

        Assert.Single(related);
        Assert.Equal("Cancelled", related[0].Status);
    }
}
