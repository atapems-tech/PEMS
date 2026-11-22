using PEMS.Web.Definitions;
using Xunit;

namespace PEMS.Tests.Cmms;

/// <summary>SCRUM-718, 760, 719, 762, 689: the commands of the MDB, transmittal, MDR/VDR import review and document intelligence pages keep the permission of the controller action they replaced.</summary>
public class SmallDocumentOperationsTests
{
    private static readonly OperationCatalog Catalog = OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);

    [Theory]
    [InlineData("Transmittal", "MarkSent", "Transmittal.Approve")]
    [InlineData("Transmittal", "Cancel", "Transmittal.Delete")]
    [InlineData("Transmittal", "Reissue", "Transmittal.Create")]
    [InlineData("Transmittal", "RecordResponse", "Transmittal.Edit")]
    [InlineData("Transmittal", "Acknowledge", "Transmittal.Edit")]
    [InlineData("MdrImport", "ToggleRow", "MDRImport.Create")]
    [InlineData("MdrImport", "Commit", "MDRImport.Approve")]
    [InlineData("MdrImport", "Discard", "MDRImport.Delete")]
    [InlineData("MdrImport", "RematchDisciplines", "MDRImport.Approve")]
    [InlineData("MdrImport", "FixLegacyStatuses", "MDRImport.Approve")]
    [InlineData("VdrImport", "ToggleRow", "VDR.Create")]
    [InlineData("VdrImport", "Commit", "VDR.Approve")]
    [InlineData("VdrImport", "Discard", "VDR.Delete")]
    [InlineData("EngineeringIntelligence", "ApproveClassification", "EngineeringDocuments.Approve")]
    [InlineData("EngineeringIntelligence", "RejectClassification", "EngineeringDocuments.Create")]
    [InlineData("EngineeringIntelligence", "ApproveTagLink", "EngineeringDocuments.Approve")]
    [InlineData("EngineeringIntelligence", "AdvanceStage", "EngineeringDocuments.Approve")]
    [InlineData("VdrWorkflow", "SubmitForReview", "VDR.Edit")]
    [InlineData("VdrWorkflow", "DisciplineRespond", "VDR.Edit")]
    [InlineData("VdrWorkflow", "LeadReview", "VDR.Approve")]
    [InlineData("VdrWorkflow", "ReturnToVendor", "VDR.Approve")]
    [InlineData("EngineeringIntelligence", "AddComment", "EngineeringDocuments.Create")]
    public void TheOperationsKeepTheFloorOfTheOldController(string model, string action, string permission)
        => Assert.Equal(permission, Catalog.Get(model, action)!.Permission);

    [Theory]
    [InlineData("Mdb", "AddDocument")]
    [InlineData("Mdb", "MarkFinalIssued")]
    public void TheMdbCommandsAreOperations(string model, string action) => Assert.NotNull(Catalog.Get(model, action));
}
