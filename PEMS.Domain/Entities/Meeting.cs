using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// جلسه (طبق نمونه‌ی کارتابل مرجع - بخش "گفتگوها"/جلسات) - دستورجلسه، صورت‌جلسه، حاضرین،
/// و اقدامات پیگیری (که از همان TaskItem موجود با RelatedType="Meeting" استفاده می‌کنند).
/// </summary>
public class Meeting : BaseEntity
{
    [Key]
    public int MeetingId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime MeetingDate { get; set; } = DateTime.Now;

    public string? Location { get; set; }

    public string? OrganizerName { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // طبق ممیزی Package Procurement Contract Finalization (§C, §E، Phase 6.4): زیرساخت Meeting
    // پیش از این هیچ ارتباطی به Contract/ProcurementPackage نداشت - این دو فیلد اختیاری همان الگوی
    // FK اختیاری تکرارشونده در بقیه‌ی پروژه است، نه یک Entity جدید KickOffMeeting.
    // General, KickOff - جلسه‌ی General نیازی به این دو فیلد ندارد
    public string MeetingType { get; set; } = "General";

    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public int? ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public string? Agenda { get; set; }

    public string? MinutesNotes { get; set; }

    // Scheduled, Completed, Cancelled
    public string Status { get; set; } = "Scheduled";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();
}
