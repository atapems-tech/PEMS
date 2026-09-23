using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// تصمیم مستقل هر رشته/نقش در بازبینی چند-رشته‌ای قرارداد (Multi-Discipline Contract Review) -
/// طبق ممیزی Package Procurement Contract Finalization (§E, §F). هر ردیف تصمیم یک رشته‌ی خاص
/// است؛ لیست رشته‌های الزامی برای هر قرارداد از پیکربندی سرویس خوانده می‌شود
/// (ContractWorkflowService.RequiredDisciplines) - دقیقاً همان الگوی
/// ProcurementPackageService.StageOrder - نه هاردکد در دیتابیس و نه اختراع یک ساختار سازمانی.
/// </summary>
public class ContractDisciplineReview : BaseEntity
{
    [Key]
    public int ContractDisciplineReviewId { get; set; }

    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    // Engineering, TechnicalInspection, Legal, Finance, Procurement, HSE
    public string DisciplineRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public int? ReviewerId { get; set; }
    public Employee? Reviewer { get; set; }

    // Pending, Approved, Rejected, ReturnedForRevision
    public string Decision { get; set; } = "Pending";

    public string? Comment { get; set; }

    public DateTime? ReviewDate { get; set; }

    public bool IsActive { get; set; } = true;
}
