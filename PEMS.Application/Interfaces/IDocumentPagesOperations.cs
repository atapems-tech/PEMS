namespace PEMS.Application.Interfaces;

public sealed record DocFileResult(bool Success, string? Error, byte[] Bytes, string FileName, string ContentType);
public sealed record DocChoice(string Value, string Label);
public sealed record DocRegisterRow(int Id, string DocumentNo, string Title, string? DocumentType, string? Area, string Discipline, string Project, string Revision, string Status, bool Frozen, string? CommentStatus, int? DelayDays, DateTime? Created);
public sealed record EngDocRow(int Id, string DocumentNo, string Title, string? Project, string? Area, string? CommentStatus, string? Type, string Discipline, string Revision, string Status, bool Frozen, int? DelayDays, DateTime Created);
public sealed record EngDocCountsRow(int Id, int Total, int Approved, int Pending, int Rejected, int Frozen, int Overdue);
public sealed record VdrListRow(int Id, string DocumentNumber, string? Description, string? Type, string Vendor, string Project, string? Discipline, int? Revision, string? LatestCategory, string Status, bool Overdue);
public sealed record VdrDashboardRow(int Id, int Total, int Overdue, int SlaOverdue, int OnTimePercent, int VendorsAtRisk);
public sealed record MdbListRow(int Id, string Equipment, string Vendor, string? Po, string Project, DateTime? PlannedIssue, DateTime? IndexDue, bool IndexOverdue, string Status, string HardCopy, string SoftCopy);
public sealed record MdbHeadRow(int Id, string Equipment, string Vendor, string Project, string? Po, string Status, DateTime? PlannedIssue, DateTime? IndexDue, bool IndexOverdue, DateTime? IndexSubmitted, string HardCopy, string SoftCopy,
    bool CanIndex, bool CanFinal, bool CanHardCopy, bool CanSoftCopy);
public sealed record MdbDocRow(int Id, string Part, string? DocumentNo, string Title, int? DvdVolume, string NotApplicable, string? Notes);
public sealed record TransmittalListRow(int Id, string No, string Project, string From, string To, string Purpose, int Items, string Status, bool ResponseOverdue, DateTime Date);
public sealed record TransmittalHeadRow(int Id, string No, string Status, string Project, string From, string To, string Purpose, DateTime Created, DateTime? Sent, string? Description, string? ReissuedFrom, string? SupersededBy, string? Cancelled,
    bool ResponseRequired, DateTime? ResponseDue, bool ResponseOverdue, DateTime? ResponseReceived, string? ResponseNote, bool CanSend, bool CanCancel, bool CanRecordResponse, bool CanReissue, bool Locked, int? SupersedesId, int? SupersededById);
public sealed record TransmittalRecipientRow(int Id, string Name, string Type, string? Organization, string ResponseRequired, string Acknowledgement, bool CanAcknowledge);
public sealed record TransmittalItemRow(int Id, string DocumentNo, string Title, string Revision, string File, string? Remarks);
public sealed record TransmittalCreateAnswer(bool Success, string? Error, int NewId);
public sealed record DccCountsRow(int Id, int PendingIssue, int UnlinkedIncoming);
public sealed record DccPendingDocRow(int Id, string DocumentNo, string Title, DateTime? Modified);
public sealed record DccLetterRow(int Id, string Number, string? Sender, string? Subject, DateTime Date, string Link);
public sealed record IntelligenceCountsRow(int Id, int Total, int PendingClassification, int PendingTags, int OpenComments);
public sealed record IntelligenceStageRow(int Id, string Stage, int Count);
public sealed record IntelligencePendingRow(int Id, string DocumentNo, string Title, string Stage, int PendingClassifications, int PendingTags);

/// <summary>
/// SCRUM-830: the read side (and the two files, the transmittal creation) of the controllers DocumentRegister, VDR (list), MDB, Transmittal, DCC (dashboard), DocumentUpload (project choices) and EngineeringDocumentIntelligence (queue) as operations.
/// The same services and the same scope rule as the controllers (the document register shows a discipline-restricted user only the own discipline); the rights are the ones the controllers needed.
/// </summary>
public interface IDocumentPagesOperations
{
    // ---- document register
    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterList", "DocumentRegister.View")]
    Task<List<DocRegisterRow>> RegisterListAsync(string? project = null, string? disciplines = null, string? status = null, string? q = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterProjects", "DocumentRegister.View")]
    Task<List<DocChoice>> RegisterProjectsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterDisciplines", "DocumentRegister.View")]
    Task<List<DocChoice>> RegisterDisciplinesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterStatuses", "DocumentRegister.View")]
    Task<List<DocChoice>> RegisterStatusesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterExport", "DocumentRegister.View")]
    Task<DocFileResult> RegisterExportAsync(string? project = null, string? disciplines = null, string? status = null, string? q = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "RegisterExportSelected", "DocumentRegister.View")]
    Task<DocFileResult> RegisterExportSelectedAsync(List<int> ids);

    // ---- vendor document register
    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrList", "VDR.View")]
    Task<List<VdrListRow>> VdrListAsync(string? projectId = null, string? vendorId = null, string? status = null, string? search = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrDashboard", "VDR.View")]
    Task<List<VdrDashboardRow>> VdrDashboardAsync(string? projectId = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrProjects", "VDR.View")]
    Task<List<DocChoice>> VdrProjectsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrVendors", "VDR.View")]
    Task<List<DocChoice>> VdrVendorsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrStatuses", "VDR.View")]
    Task<List<DocChoice>> VdrStatusesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "VdrExport", "VDR.View")]
    Task<DocFileResult> VdrExportAsync(string? projectId = null, string? vendorId = null, string? status = null, string? search = null);

    // ---- manufacturer data books
    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbList", "MDB.View")]
    Task<List<MdbListRow>> MdbListAsync(string? projectId = null, string? vendorId = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbProjects", "MDB.View")]
    Task<List<DocChoice>> MdbProjectsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbVendors", "MDB.View")]
    Task<List<DocChoice>> MdbVendorsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbHead", "MDB.View")]
    Task<List<MdbHeadRow>> MdbHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbDocuments", "MDB.View")]
    Task<List<MdbDocRow>> MdbDocumentsAsync(int id, string part);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "MdbDocumentOptions", "MDB.View")]
    Task<List<DocChoice>> MdbDocumentOptionsAsync(int id);

    // ---- transmittals
    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalList", "Transmittal.View")]
    Task<List<TransmittalListRow>> TransmittalListAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalHead", "Transmittal.View")]
    Task<List<TransmittalHeadRow>> TransmittalHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalRecipients", "Transmittal.View")]
    Task<List<TransmittalRecipientRow>> TransmittalRecipientsAsync(int id);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalItems", "Transmittal.View")]
    Task<List<TransmittalItemRow>> TransmittalItemsAsync(int id);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalProjects", "Transmittal.Create")]
    Task<List<DocChoice>> TransmittalProjectsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalDocuments", "Transmittal.Create")]
    Task<List<DocChoice>> TransmittalDocumentsAsync();

    /// <summary>One recipient per line of <paramref name="recipients"/>: name | type (External, Internal, Vendor, Client) | organization | yes when a response is required.</summary>
    [PEMS.Application.Common.PemsOperation("DocumentPage", "TransmittalCreate", "Transmittal.Create")]
    Task<TransmittalCreateAnswer> TransmittalCreateAsync(string transmittalNo, int projectId, string from, string? to, string purpose, string? description, bool responseRequired, string? recipients, List<int> documentIds);


    // ---- engineering documents list
    [PEMS.Application.Common.PemsOperation("DocumentPage", "EngDocsList", "EngineeringDocuments.View")]
    Task<List<EngDocRow>> EngDocsListAsync(string? search = null, string? projectId = null, string? disciplineId = null, string? status = null, string? area = null, string? minRevisionCount = null);

    [PEMS.Application.Common.PemsOperation("DocumentPage", "EngDocsCounts", "EngineeringDocuments.View")]
    Task<List<EngDocCountsRow>> EngDocsCountsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "EngDocsDisciplines", "EngineeringDocuments.View")]
    Task<List<DocChoice>> EngDocsDisciplinesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "EngDocsStatuses", "EngineeringDocuments.View")]
    Task<List<DocChoice>> EngDocsStatusesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "EngDocsExport", "EngineeringDocuments.View")]
    Task<DocFileResult> EngDocsExportAsync(string? search = null, string? projectId = null, string? disciplineId = null, string? status = null, string? area = null, string? minRevisionCount = null);

    // ---- document control center
    [PEMS.Application.Common.PemsOperation("DocumentPage", "DccCounts", "DCC.View")]
    Task<List<DccCountsRow>> DccCountsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "DccPendingIssue", "DCC.View")]
    Task<List<DccPendingDocRow>> DccPendingIssueAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "DccUnlinked", "DCC.View")]
    Task<List<DccLetterRow>> DccUnlinkedAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "DccRecent", "DCC.View")]
    Task<List<DccLetterRow>> DccRecentAsync();

    // ---- document upload and the intelligence queue
    /// <summary>The projects of the upload form; the role gate of the Import Center card is checked by the upload action itself.</summary>
    [PEMS.Application.Common.PemsOperation("DocumentPage", "UploadProjects")]
    Task<List<DocChoice>> UploadProjectsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "IntelligenceCounts", "EngineeringDocuments.View")]
    Task<List<IntelligenceCountsRow>> IntelligenceCountsAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "IntelligenceStages", "EngineeringDocuments.View")]
    Task<List<IntelligenceStageRow>> IntelligenceStagesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentPage", "IntelligencePending", "EngineeringDocuments.View")]
    Task<List<IntelligencePendingRow>> IntelligencePendingAsync();
}
