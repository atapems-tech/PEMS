using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف خام استخراج‌شده از جدول (Wiring Diagram/Instrument Index/...) پیش از تایید. مقادیر همان
/// چیزی هستند که DeterministicTableExtractor/AI برگردانده - این‌جا فقط برای بازبینی نگه داشته می‌شوند.
/// </summary>
public class TableExtractionRow
{
    [Key]
    public int TableExtractionRowId { get; set; }

    public int TableExtractionBatchId { get; set; }
    public TableExtractionBatch? Batch { get; set; }

    public string? TagNumber { get; set; }
    public string? Description { get; set; }
    public string? JunctionBox { get; set; }
    public string? Terminal { get; set; }
    public string? CableNo { get; set; }
    public string? MarshallingCabinet { get; set; }
    public string? MarshallingTerminal { get; set; }
    public string? SystemCabinet { get; set; }
    public string? IoCard { get; set; }
    public string? IoChannel { get; set; }
    public string? IoAddress { get; set; }
    public string? DcsTagNo { get; set; }
    public string? LoopNo { get; set; }
    public string? System { get; set; }
    public string? SignalClass { get; set; }
    public bool? SpareOrHotSpare { get; set; }
    public string? Remarks { get; set; }

    // فیلدهای ناشناخته (ستون‌هایی که به هیچ فیلد استاندارد نگاشت نشدند) - JSON از Dictionary اصلی
    public string? RawExtraJson { get; set; }

    // Unmatched, MatchedNoInstrument, MatchedHasInstrument, Confirmed, Rejected
    public string MatchStatus { get; set; } = "Unmatched";

    // Equipment ای که با تطبیق TagNumber پیدا شد (قبل از تایید کاربر - فقط پیشنهاد است)
    public int? MatchedEquipmentId { get; set; }
    public Equipment? MatchedEquipment { get; set; }

    // بعد از Confirm - Instrument ای که ساخته/به‌روزرسانی شد
    public int? ResultInstrumentId { get; set; }
    public Instrument? ResultInstrument { get; set; }

    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComment { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
