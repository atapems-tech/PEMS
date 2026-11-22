namespace PEMS.Application.Common;

/// <summary>SCRUM-685: a file chosen on a form, as an operation receives it (the name the user's computer gave it, its content type and its bytes). An operation takes <c>OperationFile?</c> for an optional file, or <c>IReadOnlyList&lt;OperationFile&gt;</c> for several; both are checked by the upload profile of the action.</summary>
public sealed record OperationFile(string FileName, string? ContentType, byte[] Bytes);
