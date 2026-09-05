namespace PEMS.Application.Interfaces;

public interface IDocumentAIIngestionService
{
    Task ProcessAttachmentAsync(int attachmentId);

    Task ApproveExtractedTagAsync(int extractedTagId, int equipmentId, int? reviewedById = null);
    Task RejectExtractedTagAsync(int extractedTagId, string? comment = null, int? reviewedById = null);

    Task ApproveExtractedSpecificationAsync(int extractedSpecificationId, int? reviewedById = null);
    Task RejectExtractedSpecificationAsync(int extractedSpecificationId, string? comment = null, int? reviewedById = null);
}
