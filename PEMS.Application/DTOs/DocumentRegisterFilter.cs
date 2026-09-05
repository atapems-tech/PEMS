namespace PEMS.Application.DTOs;

/// <summary>
/// طبق ممیزی مقیاس‌پذیری (P1 - Document Register): جایگزین فیلتر سمت-کلاینت قدیمی که
/// GetAllAsync (بدون Pagination) را کامل می‌خواند و بعد در حافظه فیلتر می‌کرد. برخلاف
/// EngineeringDocumentListFilter (که ProjectId/DisciplineId تکی به‌عنوان int می‌گیرد و مخصوص
/// EngineeringDocumentsController است)، این‌جا شکل واقعی فیلترهای Document Register حفظ شده:
/// نام پروژه (تطبیق دقیق رشته)، چند-دیسیپلین با نام نمایشی (شامل سطل "(Unassigned)")، وضعیت،
/// و جست‌وجوی آزاد - عمداً کلاس جدا تا GetPagedAsync/ApplyFilters موجود (که EngineeringDocuments
/// از آن استفاده می‌کند) دست‌نخورده بماند.
/// </summary>
public class DocumentRegisterFilter
{
    public string? ProjectName { get; set; }

    // نام‌های نمایشی دیسیپلین انتخاب‌شده - شامل UnassignedDisciplineKey برای مدارک بدون دیسیپلین
    public List<string> Disciplines { get; set; } = new();

    public string? Status { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    // طبق درخواست ممیزی بند ۶: هیچ فراخوانی (حتی داخلی) نباید بتواند این محدودیت را دور بزند -
    // مقدار واقعی همیشه در Repository نیز دوباره Clamp می‌شود، این‌جا فقط پیش‌فرض منطقی صفحه است
    public const int MaxPageSize = 200;

    // برچسب سطل مدارک بدون دیسیپلین - دقیقاً همان رشته‌ای که View/فیلتر قدیمی استفاده می‌کرد
    public const string UnassignedDisciplineKey = "(Unassigned)";
}
