namespace PEMS.Application.Interfaces;

/// <summary>
/// شماره‌گذاری خودکار مدارک مهندسی (طبق بازبینی معماری، بند ۱۰): پیش‌فرض خودکار،
/// جایگزینی دستی فقط برای نقش‌هایی که مجوز CanManualNumber دارند.
/// </summary>
public interface IDocumentNumberingService
{
    Task<string> GenerateNextNumberAsync(int projectId, int? disciplineId, string documentType);
}
