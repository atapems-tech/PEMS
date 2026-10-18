using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۲): تاریخچه‌ی append-only پاسخ‌های یک
/// ProcurementCommunication - پیش از این، ProcurementCommunication.ResponseText با هر پاسخ تازه
/// جای‌گیر بازنویسی می‌شد و پاسخ قبلی بدون اثر گم می‌شد. دقیقاً همان الگوی Document/
/// DocumentRevision: فیلدهای هم‌نام روی ProcurementCommunication فقط Cache آخرین ردیف این جدول‌اند.
/// </summary>
public class ProcurementCommunicationResponse : BaseEntity
{
    [Key]
    public int ProcurementCommunicationResponseId { get; set; }

    public int ProcurementCommunicationId { get; set; }
    public ProcurementCommunication? ProcurementCommunication { get; set; }

    public string ResponseText { get; set; } = string.Empty;

    public string? RespondedByName { get; set; }

    public DateTime ResponseDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}
