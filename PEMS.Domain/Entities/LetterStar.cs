using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// ستاره‌گذاری شخصی هر کاربر روی یک نامه (طبق الگوی کارتابل‌های حرفه‌ای ایرانی مثل Fonix/PARGAR).
/// شخصی است، نه سراسری - هرکس فقط ستاره‌های خودش را می‌بیند.
/// </summary>
public class LetterStar
{
    [Key]
    public int LetterStarId { get; set; }

    public int LetterId { get; set; }
    public Letter? Letter { get; set; }

    public int ApplicationUserId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
