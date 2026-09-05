namespace PEMS.Application.Interfaces;

/// <summary>
/// شماره‌گذاری خودکار نامه (همان الگوی DocumentNumberingService، برای Correspondence).
/// </summary>
public interface ILetterNumberingService
{
    Task<string> GenerateNextNumberAsync(string letterType, int? projectId);
}
