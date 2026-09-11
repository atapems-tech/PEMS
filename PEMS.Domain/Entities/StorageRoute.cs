using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۵: قانون مسیریابی «کدام ماژول/نوع سند
/// روی کدام StorageProvider ذخیره شود». ترتیب تفکیک (Resolution Order، طبق IStorageRouteResolver):
/// ۱) یک Route فعال دقیقاً برای Module+DocumentType مشخص
/// ۲) یک Route فعال Module-Default (DocumentType=null، IsDefault=true) برای همان Module
/// ۳) StorageProvider سراسری پیش‌فرض (IsDefault=true, IsEnabled=true)
/// اگر هیچ‌کدام یافت نشود، خطای صریح StorageConfigurationInvalid داده می‌شود - هرگز حدس زده
/// نمی‌شود کدام Provider درست است.
/// </summary>
public class StorageRoute : BaseEntity
{
    [Key]
    public int StorageRouteId { get; set; }

    // مثلاً "EngineeringDocuments", "VDR", "Contracts", "MaterialRequisition", "Equipment", "AIKnowledgeBase"
    public string Module { get; set; } = string.Empty;

    // اختیاری - قانون دقیق‌تر برای یک زیرمجموعه‌ی خاص از همان Module
    public string? DocumentType { get; set; }

    public int StorageProviderId { get; set; }
    public StorageProvider? StorageProvider { get; set; }

    // زیرپوشه‌ی نسبی اختیاری داخل RootPath همان Provider - برای جداسازی بیشتر در یک Provider مشترک
    public string? RelativeRootPath { get; set; }

    public bool IsEnabled { get; set; } = true;

    // true یعنی این Route قانون پیش‌فرض همان Module است (وقتی DocumentType مشخصی نیامده)
    public bool IsDefault { get; set; }

    // در تشخیص تناقض/اولویت بین چند Route هم‌پوشان استفاده می‌شود - عدد بزرگ‌تر اولویت بالاتر
    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
