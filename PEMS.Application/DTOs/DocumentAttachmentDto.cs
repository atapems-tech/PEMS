namespace PEMS.Application.DTOs;

public class DocumentAttachmentDto
{

    public int AttachmentId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string? MimeType { get; set; }

    public int? DocumentRevisionId { get; set; }
    public string? RevisionNo { get; set; }
    public bool IsOfficial { get; set; }
    public string? FileRole { get; set; }
    public string? FileHash { get; set; }


    public string? AIStatus { get; set; }

    public string? AISummary { get; set; }

    public string? AIKeywords { get; set; }


    public DateTime UploadDate { get; set; }


    public bool IsActive { get; set; }


    public string FileSizeText
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";

            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024} KB";

            return $"{FileSize / (1024 * 1024)} MB";
        }
    }

}