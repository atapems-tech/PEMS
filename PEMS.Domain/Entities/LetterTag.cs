using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// ارتباط چندبه‌چند نامه و برچسب.
/// </summary>
public class LetterTag
{
    [Key]
    public int LetterTagId { get; set; }

    public int LetterId { get; set; }
    public Letter? Letter { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
