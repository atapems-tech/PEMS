using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// طبق Phase E-01: دو نوع DbContext تست لازم است -
/// (۱) InMemory برای منطقی که به SQL خام وابسته نیست (مثلاً گاردهای Freeze/Unfreeze),
/// (۲) SQL Server واقعی برای NumberingService، چون تخصیص اتمیک آن از دستور خام
///     T-SQL "UPDATE ... OUTPUT" استفاده می‌کند که نه InMemory و نه SQLite پشتیبانی می‌کنند.
/// </summary>
public static class PemsDbContextFactory
{
    public static PEMSDbContext CreateInMemory(string databaseName)
    {
        var options = new DbContextOptionsBuilder<PEMSDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new PEMSDbContext(options);
    }

    // همون Connection String پیش‌فرض appsettings.json (PEMS.Web) - قابل بازنویسی با متغیر محیطی
    // PEMS_TEST_CONNECTION برای محیط‌های CI/دیگر.
    private const string DefaultConnection =
        "Server=localhost\\SQLEXPRESS;Database=PEMSDB;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PEMS_TEST_CONNECTION") ?? DefaultConnection;

    /// <summary>
    /// طبق درخواست ممیزی: تست‌های وابسته به SQL Server واقعی نباید بی‌صدا Pass نشان بدهند وقتی
    /// دیتابیس در دسترس نیست - این متد یک استثنای واضح پرتاب می‌کند تا علت شکست تست روشن باشد،
    /// نه یک Skip خاموش که پوشش تست دروغین القا کند.
    /// </summary>
    public static PEMSDbContext CreateSqlServerOrThrow()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "این تست به یک SQL Server واقعی نیاز دارد (چون NumberingService از T-SQL خام " +
                "UPDATE...OUTPUT استفاده می‌کند که InMemory/SQLite پشتیبانی نمی‌کنند). " +
                $"اتصال به '{ConnectionString}' ناموفق بود - متغیر محیطی PEMS_TEST_CONNECTION را " +
                "تنظیم کنید یا مطمئن شوید SQL Server dev در دسترس است.", ex);
        }

        var options = new DbContextOptionsBuilder<PEMSDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new PEMSDbContext(options);
    }

    /// <summary>
    /// طبق پذیرش ممیزی G/M/F (بند ۴ - Database Safety): برای تست Race واقعی روی یکتایی
    /// TransmittalNo، باید یک دیتابیس SQL Server ایزوله (نه PEMSDB مشترک Dev) استفاده شود.
    /// همان سرور SQL Server موجود (localhost\SQLEXPRESS یا PEMS_TEST_CONNECTION) را می‌گیرد اما
    /// InitialCatalog را به یک دیتابیس مجزا تغییر می‌دهد و کل تاریخچه‌ی Migration واقعی (همان که
    /// روی PEMSDB اجرا می‌شود) را رویش اعمال می‌کند - نه EnsureCreated (که ممکن است Schema را
    /// متفاوت از تولید بسازد و ایندکس‌های Unique واقعی مثل TransmittalConfiguration را از قلم بیندازد).
    /// </summary>
    public static PEMSDbContext CreateIsolatedSqlServerOrThrow(string isolatedDatabaseName)
    {
        var masterBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "master"
        };

        try
        {
            using var conn = new SqlConnection(masterBuilder.ConnectionString);
            conn.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "این تست به یک SQL Server واقعی نیاز دارد تا یک دیتابیس ایزوله برای تست Race بسازد. " +
                $"اتصال به master روی '{masterBuilder.DataSource}' ناموفق بود.", ex);
        }

        var isolatedBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = isolatedDatabaseName
        };

        var options = new DbContextOptionsBuilder<PEMSDbContext>()
            .UseSqlServer(isolatedBuilder.ConnectionString)
            .Options;

        var ctx = new PEMSDbContext(options);
        ctx.Database.Migrate();

        return ctx;
    }
}
