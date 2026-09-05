using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق ممیزی مقیاس‌پذیری (P0 - AuditLog Query Performance): تا امروز AuditLog هیچ Index ای
/// جز PK نداشت - با شواهد واقعی ثابت شد (STATISTICS IO + اجرای واقعی Plan روی PEMSDB): هر
/// Query صفحه‌ی Admin/AuditLog (فیلترشده یا نه) دقیقاً ۳۴۰۰ Logical Read (کل جدول) و
/// Clustered Index Scan کامل + Sort روی کل ۲۰۵٬۰۹۶ ردیف انجام می‌داد - چه فیلتر EntityName
/// اعمال شده باشد چه نه.
///
/// دو Index حداقلیِ کافی (نه سه‌تا): (Timestamp) برای Query بدون فیلتر (ORDER BY Timestamp
/// DESC رایج‌ترین حالت صفحه)، و (EntityName, Timestamp) که هم Query فیلترشده با EntityName را
/// (Seek به‌جای Scan + بدون Sort مجزا چون داده از قبل به ترتیب Timestamp است) و هم Query
/// SELECT DISTINCT EntityName را (اسکن فقط ستون اول همین Index، نه کل ردیف) پوشش می‌دهد.
///
/// عمداً Index روی UserName اضافه نشد: AuditLogs پرنویس‌ترین جدول کل سیستم است (هر Insert/
/// Update/Delete در هر ماژولی از طریق Override سراسری SaveChangesAsync این‌جا می‌نویسد) - افزودن
/// یک Index سوم یعنی سربار نوشتن روی مسیر نوشتنِ کل برنامه، فقط برای بهینه‌سازی یک صفحه‌ی Admin
/// که فرکانس واقعی استفاده از فیلتر UserName آن مشخص نیست. اگر بعداً با داده‌ی واقعی ثابت شد
/// این فیلتر پرمصرف است، به‌صورت هدفمند اضافه شود - نه حدسی از پیش.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasIndex(x => x.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp");

        builder.HasIndex(x => new { x.EntityName, x.Timestamp })
            .HasDatabaseName("IX_AuditLogs_EntityName_Timestamp");
    }
}
