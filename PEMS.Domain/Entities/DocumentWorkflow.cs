using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class DocumentWorkflow
{
    [Key]
    public int WorkflowId { get; set; }



    // =========================
    // Document Relation
    // =========================

    public int DocumentId { get; set; }

    public EngineeringDocument? Document { get; set; }






    // =========================
    // Status Transition
    // =========================


    // وضعیت قبلی
    public string FromStatus { get; set; } = string.Empty;



    // وضعیت جدید
    public string ToStatus { get; set; } = string.Empty;





    // نوع عملیات
    // Create
    // Submit
    // Review
    // Approve
    // Reject
    // Issue
    // Archive

    public string ActionType { get; set; }
        = string.Empty;







    // توضیحات اقدام

    public string? Comment { get; set; }







    // =========================
    // Audit
    // =========================


    public DateTime ActionDate { get; set; }
        = DateTime.Now;



    public int? ActionById { get; set; }


    public Employee? ActionBy { get; set; }


    // طبق فاز ۱ ممیزی گردش‌کار: سند الزام کرده بود هر تایید باید IP/Browser هم ثبت بشه
    // (بخشی از Digital Approval Trail) - قبلاً اصلاً ثبت نمی‌شد
    public string? IPAddress { get; set; }

    public string? Browser { get; set; }


    public bool IsActive { get; set; } = true;

}