namespace PEMS.Application.DTOs;

// طبق «PEMS — Global Grid Hardening» P1: جایگزین Model = List<Employee> کامل در
// EmployeeController.Index - جدول Employee می‌تواند در یک سازمان به صدها/هزاران ردیف برسد و
// قبلاً بدون Skip/Take بارگذاری می‌شد. این Projection همان فیلدهایی را دارد که هم ردیف جدول و
// هم Modal ویرایش هر ردیف (که فقط برای صفحه‌ی جاری رندر می‌شود، نه کل جدول) لازم دارند.
public class EmployeeListItemDto
{
    public int EmployeeId { get; set; }
    public string PersonnelCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int DisciplineId { get; set; }
    public string? DisciplineName { get; set; }
    public string PersonnelType { get; set; } = "Internal";
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public bool IsActive { get; set; }
}
