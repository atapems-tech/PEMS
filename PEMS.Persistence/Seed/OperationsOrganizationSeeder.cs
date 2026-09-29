using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Work Order Governance Remediation» F1: ممیزی زنده‌ی PEMSDB نشان داد هیچ Department ای
/// با OrganizationDomain='Operations' وجود ندارد (0 ردیف) - و هیچ Department ای هم با نام
/// "Operations" (برای تصحیح Mapping به‌جای ساخت رکورد تازه، طبق قدم ۲ درخواست) پیدا نشد. این دقیقاً
/// همان منطق Name-Match موجود در WorkOrderGovernanceSeeder (بند ۷۳-۷۷ آن فایل) است که فقط یک‌بار در
/// راه‌اندازی اول اجرا شده و دیگر برای Department های بعدی اجرا نمی‌شود - این Seeder مستقل و
/// Idempotent است تا این خلأ را (بدون دست‌زدن به Marker قبلی) ببندد.
///
/// طبق قدم ۳ درخواست: این Seeder فقط رکورد سازمانی را اضافه می‌کند - هیچ Employee واقعی به این
/// Department تخصیص داده نمی‌شود (آن یک تصمیم کسب‌وکاری واقعی است که باید جداگانه توسط صاحب کسب‌وکار
/// گرفته شود - طبق دستور صریح "Do not assign an existing employee to Operations merely to make a
/// test pass"). فقط زیرساخت سازمانی برای این‌که نقش OPS.IssueAuthority (که از قبل مجوز WorkOrder.Issue
/// را دارد) بتواند واقعاً به یک Department با دامنه‌ی Operations وصل شود، آماده می‌شود.
/// </summary>
public static class OperationsOrganizationSeeder
{
    public const string DepartmentName = "Operations";
    public const string DepartmentCode = "OPS";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        // Idempotent: اگر هر Department ای از قبل به دامنه‌ی Operations نگاشت شده، دیگر کاری نیست
        if (await db.Departments.AnyAsync(d => d.OrganizationDomain == OrganizationDomains.Operations))
            return;

        // قدم ۲ درخواست: اگر Department ای با همین نام از قبل وجود دارد (فقط Mapping ناقص مانده)، همان
        // را تصحیح کن - رکورد تازه نساز.
        var byName = await db.Departments.FirstOrDefaultAsync(d => d.Name == DepartmentName);
        if (byName != null)
        {
            byName.OrganizationDomain = OrganizationDomains.Operations;
            await db.SaveChangesAsync();
            return;
        }

        // قدم ۳ درخواست: هیچ رکورد سازمانی موجودی برای Operations پیدا نشد - حداقل تصحیح افزودنی
        // (یک Department، نه یک Entity/معماری تازه) اضافه می‌شود.
        db.Departments.Add(new Department
        {
            Code = DepartmentCode,
            Name = DepartmentName,
            OrganizationDomain = OrganizationDomains.Operations,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await db.SaveChangesAsync();
    }
}
