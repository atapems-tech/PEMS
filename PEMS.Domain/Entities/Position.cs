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

    /// <summary>کد یکتای سمت (اختیاری؛ برای Import/ارجاع پایدار). یکتا فقط وقتی مقدار دارد.</summary>
    public string? PositionCode { get; set; }

    /// <summary>
    /// سمت مافوق در سلسله‌مراتب مدیریتی (Line/Direct Manager). مرجع قطعی «مدیر مستقیم»: Employee → سمت اصلی → سمت مافوق → دارنده‌ی سمت.
    /// null = سمت ریشه (معتبر؛ مدیر مستقیم ندارد). سمت می‌تواند خالی (Vacant) باشد؛ ساختار با رفتن کارمند از بین نمی‌رود.
    /// متفاوت از Team.SupervisorEmployeeId (سرپرستی عملیاتی تیم) و از Role (مجوز).
    /// </summary>
    public int? ParentPositionId { get; set; }
    public Position? ParentPosition { get; set; }
    public ICollection<Position> ChildPositions { get; set; } = new List<Position>();

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // اختیاری - یک سمت می‌تواند مختص یک دپارتمان باشد یا سازمانی/عمومی
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // طبق «PEMS Dynamic Maintenance Organization» بخش ۱۴-۱۶: یک سمت (Position) معمولاً جای
    // مشخصی در سلسله‌مراتب تعمیرونگهداری دارد (مثلاً «سرگروه» زیر یک Team، «سرشیفت» زیر یک
    // Team از نوع شیفت). Null یعنی سمت عمومی/سطح دپارتمان (رفتار امروز، دست‌نخورده) - این ستون
    // فقط اضافه شده، DepartmentId جایگزین نشده.
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    // ترتیب نمایش در نمودار سازمانی/جزئیات تیم - پیش‌فرض صفر یعنی بدون ترتیب خاص
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<EmployeeAssignment> Assignments { get; set; } = new List<EmployeeAssignment>();
}
