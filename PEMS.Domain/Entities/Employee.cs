namespace PEMS.Domain.Entities;

public class Employee
{
    public int EmployeeId { get; set; }

    // کد پرسنلی - برای برخی کارکنان/پیمانکاران ممکن است هنوز تخصیص داده نشده باشد.
    // در سطح دیتابیس فقط مقادیر غیرخالی باید Unique باشند.
    public string? PersonnelCode { get; set; }

    // نام
    public string FirstName { get; set; } = string.Empty;

    // نام خانوادگی
    public string LastName { get; set; } = string.Empty;

    // سمت سازمانی (رشته‌ی آزاد - تاریخی، دست‌نخورده)
    public string? Position { get; set; }

    // طبق سند RBAC-ABAC Authorization Architecture بخش ۳: لینک اختیاری به رکورد واقعی Position -
    // در کنار رشته‌ی آزاد بالا (نه جایگزین آن) تا داده‌ی امروز نشکند؛ مهاجرت تدریجی به فاز بعد موکول می‌شود.
    public int? PositionId { get; set; }
    public Position? PositionRef { get; set; }

    // ایمیل
    public string? Email { get; set; }

    // شماره تماس
    public string? Phone { get; set; }

    // ارتباط با Department
    public int DepartmentId { get; set; }

    public Department? Department { get; set; }

    // ارتباط با Discipline
    public int DisciplineId { get; set; }

    public Discipline? Discipline { get; set; }

    public bool IsActive { get; set; } = true;

    // طبق درخواست: پورتال خودِ کارمند - عکس پروفایل و امضای دیجیتال (ترسیمی) که در جاهای
    // نیازمند امضا (مثلاً تاییدهای گردش‌کار) قابل‌استفاده باشد
    public string? PhotoPath { get; set; }
    public string? SignaturePath { get; set; }

    // ==============================================================================
    // طبق CMMS-10a ADR-01: پرسنل پیمانکار از طریق همین Employee مدل می‌شوند - نه یک Entity
    // موازی جدا. Internal/Contractor/External/Temporary - VendorId فقط برای غیر-Internal پر
    // می‌شود. این همون الگویی است که در سراسر پروژه برای WorkOrderType/Priority استفاده شده:
    // رشته‌ی ساده + اعتبارسنجی سمت سرور، نه Enum یا جدول Lookup جدید.
    // ==============================================================================
    public string PersonnelType { get; set; } = "Internal";

    public int? VendorId { get; set; }
    public Vendor? VendorRef { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // ==============================================================================
    // طبق «Employee Details — Stitch UI Redesign»: این چهار فیلد صرفاً افزایشی (Nullable)
    // هستند - چون CreatedDate معنای «تاریخ ثبت رکورد» دارد نه «تاریخ استخدام واقعی»، و PEMS
    // تا پیش از این هیچ فیلد Location/Shift/آدرس منزل برای Employee نداشت. مدیر مستقیم عمداً
    // این‌جا تکرار نشده - از طریق Team.SupervisorEmployeeId (عضویت فعال در TeamMember) مشتق
    // می‌شود تا Single Source of Truth حفظ شود.
    // ==============================================================================
    public DateTime? HireDate { get; set; }

    // سایت/محل کاری - رشته‌ی آزاد، اختیاری
    public string? Location { get; set; }

    // شیفت کاری خودِ کارمند - مستقل از Team.Shift (که شیفت کل تیم است)
    public string? Shift { get; set; }

    // آدرس منزل - اختیاری
    public string? HomeAddress { get; set; }
}
