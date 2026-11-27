namespace PEMS.Application.Interfaces;

public sealed record LetterPdfRow(string CompanyName, string? Address, string? Contact, string Number, DateTime Date, string? Reference, string? Category, string? From, string? To, string? Cc, string Subject, string Body,
    string? SenderName, string? SenderTitle, string? SenderDepartment, string? Signed, string Confidentiality, string Priority, DateTime Printed);

/// <summary>SCRUM-839: the data of the letter PDF (what LetterController.ExportPdf drew): the letterhead, the numbers, the lines (Bcc is never printed), the subject, the plain body and the signature; the layout is the definition <c>letter-pdf</c>.</summary>
public interface ILetterPdfOperations
{
    [PEMS.Application.Common.PemsOperation("LetterPdf", "Record", "Correspondence.View")]
    Task<List<LetterPdfRow>> RecordAsync(int id);
}
