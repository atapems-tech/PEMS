using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class CableConnection
{
    [Key]
    public int ConnectionId { get; set; }

    // کابل مرتبط
    public int CableId { get; set; }

    public Cable? Cable { get; set; }

    // مبدا کابل
    public string? FromPanel { get; set; }

    // ترمینال مبدا
    public string? FromTerminal { get; set; }

    // مقصد کابل
    public string? ToEquipment { get; set; }

    // ترمینال مقصد
    public string? ToTerminal { get; set; }

    // نوع اتصال
    // Power, Control, Instrument
    public string? ConnectionType { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}