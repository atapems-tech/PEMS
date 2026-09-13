using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PEMS.Persistence.Context;

/// <summary>
/// طبق «PEMS Correspondence Workspace» بند ۲: این کارخانه‌ی Design-Time قبلاً رشته‌ی اتصال را
/// مستقیماً Hardcode می‌کرد (همیشه PEMSDB زنده)، پس هر override با متغیر محیطی
/// (ConnectionStrings__PEMSConnection) که برای هدف‌گیری یک دیتابیس Repro یک‌بارمصرف تنظیم
/// می‌شد، بی‌صدا نادیده گرفته می‌شد - دقیقاً همان مشکلی که در فاز قبل باعث شد Migration
/// «LetterControlledLifecycle» به‌جای یک دیتابیس Repro، مستقیم روی PEMSDB زنده اجرا شود.
///
/// اصلاح: حالا اول متغیر محیطی ConnectionStrings__PEMSConnection (همون قرارداد Binding
/// استاندارد ASP.NET Core برای کلید تودرتوی ConnectionStrings:PEMSConnection) بررسی می‌شود؛
/// اگر نبود، همان appsettings.json واقعی PEMS.Web خوانده می‌شود (dotnet-ef پوشه‌ی جاری را به
/// --startup-project تغییر می‌دهد، پس این فایل همان‌جا در دسترس است)؛ رشته‌ی Hardcode قبلی فقط
/// به‌عنوان آخرین Fallback باقی مانده، نه رفتار پیش‌فرض بی‌صدا. (توجه: از Microsoft.Extensions.
/// Configuration.* استفاده نشده - یک تعارض Assembly در گراف وابستگی‌های این پروژه باعث خطای
/// کامپایل CS1929 روی متدهای الحاقی IConfigurationBuilder می‌شد؛ System.Text.Json مستقیم و
/// بدون هیچ وابستگی جدیدی همون نتیجه را می‌دهد.)
/// </summary>
public class PEMSDbContextFactory : IDesignTimeDbContextFactory<PEMSDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=PEMSDB;Trusted_Connection=True;TrustServerCertificate=True;";

    public PEMSDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        // طبق بند ۲: هرگز بی‌صدا فرض نکن هدف کدام دیتابیس است - همیشه صریحاً روی خروجی طراحی‌زمان چاپ کن
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        Console.WriteLine($"[PEMSDbContextFactory] Design-time DbContext target database: '{target.InitialCatalog}' on server '{target.DataSource}'");

        var optionsBuilder = new DbContextOptionsBuilder<PEMSDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new PEMSDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var envOverride = Environment.GetEnvironmentVariable("ConnectionStrings__PEMSConnection");
        if (!string.IsNullOrWhiteSpace(envOverride))
            return envOverride;

        foreach (var fileName in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!File.Exists(path)) continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings) &&
                    connStrings.TryGetProperty("PEMSConnection", out var pemsConn) &&
                    pemsConn.GetString() is { Length: > 0 } value)
                {
                    return value;
                }
            }
            catch (JsonException)
            {
                // فایل Config نامعتبر - نادیده گرفته می‌شود، به Fallback زیر می‌رود
            }
        }

        return FallbackConnectionString;
    }
}
