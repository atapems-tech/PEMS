using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق AI Architecture (Volume VIII - Bible): هسته‌ی Semantic Search/RAG واقعی.
/// هر رکورد یک قطعه متن (از خلاصه‌ی AI یک مدرک، یک Comment، یا هر منبع دیگر) + بردار Embedding
/// آن (تولیدشده محلی توسط Ollama، بدون نیاز به سرویس ابری یا Vector Database جداگانه) است.
/// جستجو با محاسبه‌ی شباهت کسینوسی بین Embedding سوال و همه‌ی رکوردها انجام می‌شود - در مقیاس
/// فعلی (چند هزار تا چند ده‌هزار رکورد) کاملاً کافی است؛ اگر مقیاس آینده ایجاب کرد، همین جدول
/// می‌تواند به یک Vector Database واقعی مهاجرت کند بدون تغییر در Interface مصرف‌کنندگان.
/// </summary>
public class KnowledgeBaseEntry : BaseEntity
{
    [Key]
    public int KnowledgeBaseEntryId { get; set; }

    // Document, VDRComment, ProcurementCommunication, DisciplineTask, ...
    public string SourceType { get; set; } = string.Empty;

    public int SourceId { get; set; }

    public string Content { get; set; } = string.Empty;

    // بردار Embedding به‌صورت JSON سریالایز شده (float[])
    public string EmbeddingJson { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? UpdatedDate { get; set; }
}
