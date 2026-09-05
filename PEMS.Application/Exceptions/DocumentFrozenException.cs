namespace PEMS.Application.Exceptions;

/// <summary>
/// طبق Phase E-01 (اجرای واقعی Freeze): وقتی تلاش می‌شود مدرکی که IsFrozen=true دارد ویرایش یا
/// رویژن جدیدش ساخته شود. باید در لایه‌ی Persistence/Application پرتاب شود - نه فقط با پنهان‌کردن
/// دکمه در View، چون درخواست مستقیم HTTP باید هم همین‌جا رد شود.
/// </summary>
public class DocumentFrozenException : Exception
{
    public DocumentFrozenException(string message) : base(message)
    {
    }
}
