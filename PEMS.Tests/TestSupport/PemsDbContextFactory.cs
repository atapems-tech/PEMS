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

    // Data-integrity rule: the regression suite NEVER touches the shared development database (PEMSDB).
    // Tests that need a real SQL Server use a dedicated regression database that is dropped and re-created from the real
    // migration history once per test process (so test master data - departments, employees, documents - never accumulates
    // in the development/master-data database). Override the server/database with PEMS_TEST_CONNECTION; pointing it at
    // PEMSDB is refused unless PEMS_TEST_ALLOW_SHARED_DB=1 is set explicitly.
    public const string RegressionDatabaseName = "PEMSDB_RegressionTest";

    private const string DefaultConnection =
        "Server=localhost\\SQLEXPRESS;Database=" + RegressionDatabaseName + ";Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PEMS_TEST_CONNECTION") ?? DefaultConnection;

    private static readonly object RegressionGate = new();
    private static bool _regressionReady;

    /// <summary>Drops and re-creates the regression database once per test process (real migrations, no seed). Thread-safe.</summary>
    public static void EnsureRegressionDatabase()
    {
        lock (RegressionGate)
        {
            if (_regressionReady) return;
            var csb = new SqlConnectionStringBuilder(ConnectionString);
            if (string.Equals(csb.InitialCatalog, "PEMSDB", StringComparison.OrdinalIgnoreCase)
                && Environment.GetEnvironmentVariable("PEMS_TEST_ALLOW_SHARED_DB") != "1")
                throw new InvalidOperationException(
                    "Refusing to run regression tests against the shared development database 'PEMSDB'. " +
                    "Use the default isolated database or set PEMS_TEST_CONNECTION to another database " +
                    "(PEMS_TEST_ALLOW_SHARED_DB=1 overrides this guard deliberately).");

            using var ctx = new PEMSDbContext(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(csb.ConnectionString).Options);
            ctx.Database.EnsureDeleted();
            ctx.Database.Migrate();
            RegressionBaselineData.Seed(ctx);
            _regressionReady = true;
        }
    }

    /// <summary>
    /// طبق درخواست ممیزی: تست‌های وابسته به SQL Server واقعی نباید بی‌صدا Pass نشان بدهند وقتی
    /// دیتابیس در دسترس نیست - این متد یک استثنای واضح پرتاب می‌کند تا علت شکست تست روشن باشد،
    /// نه یک Skip خاموش که پوشش تست دروغین القا کند.
    /// </summary>
    public static PEMSDbContext CreateSqlServerOrThrow()
    {
        try
        {
            var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            using var conn = new SqlConnection(master.ConnectionString);
            conn.Open();
            EnsureRegressionDatabase();
        }
        catch (InvalidOperationException) { throw; }
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
