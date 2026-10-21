namespace PEMS.Application.Interfaces;

/// <summary>
/// شماره‌گذاری خودکار نامه با قالب [DEPT]-[YEAR]-[SEQ] (کد دپارتمان ثبت‌کننده، سال، شمارنده‌ی پنج‌رقمی). departmentId=null → GEN.
/// شمارنده‌ی هر (دپارتمان، سال) هم‌زمانی‌ایمن است (LetterNumberSequence + RowVersion).
/// </summary>
public interface ILetterNumberingService
{
    Task<string> GenerateNextNumberAsync(string letterType, int? projectId, int? departmentId = null);
}
