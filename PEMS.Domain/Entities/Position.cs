namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۳: سمت سازمانی واقعی، جدا از Role برنامه -
/// «Position ≠ Role ≠ Permission». Employee.Position (رشته‌ی آزاد موجود) دست‌نخورده باقی می‌ماند؛
/// Employee.PositionId (اختیاری) در کنارش اضافه شده تا مهاجرت تدریجی به رکورد واقعی ممکن باشد
/// بدون شکستن داده‌ی امروز.
/// </summary>
public class Position
{
    public int PositionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // اختیاری - یک سمت می‌تواند مختص یک دپارتمان باشد یا سازمانی/عمومی
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
