using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۶: یک ردیف مدرک داخل یکی از سه بخش
/// MDB (Part A=Engineering Dossier, Part B=Manufacturer's Inspection Data Book, Part C=
/// Installation/Startup/Commissioning/O&M Manual). می‌تواند به یک Document موجود (VDRItem/
/// EngineeringDocument) وصل باشد یا فقط عنوان آزاد داشته باشد.
/// </summary>
public class MDBDocument
{
    [Key]
    public int MDBDocumentId { get; set; }

    public int MDBId { get; set; }
    public ManufacturerDataBook? MDB { get; set; }

    // A, B, C
    public string Part { get; set; } = "A";

    public int? DocumentId { get; set; }
    public Document? Document { get; set; }

    public string Title { get; set; } = string.Empty;

    // طبق بخش ۷.۲: هر Part روی یک یا چند DVD (Disk n/N) قرار می‌گیرد
    public int? DvdVolumeNo { get; set; }

    // طبق بخش ۶: بخش‌های غیرقابل‌اجرا با "Not applicable" علامت‌گذاری می‌شوند، نه حذف می‌شوند
    public bool IsNotApplicable { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
