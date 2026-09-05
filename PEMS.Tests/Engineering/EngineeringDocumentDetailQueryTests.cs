using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Persistence.Context;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق ممیزی مقیاس‌پذیری (P1 - Document Detail Split Query): اثبات این‌که AsSplitQuery روی
/// EngineeringDocumentRepository.GetByIdAsync واقعاً چند Round-trip جدا SQL تولید می‌کند (نه یک
/// Join دکارتی که Attachments × Revisions × Workflows را ضرب می‌کند). شواهد قبل از این رفع‌اشکال
/// (طبق گزارش ممیزی): بدترین سند واقعی PEMSDB (۲۶ Revision × ۷۶ Workflow) دقیقاً ۱۹۷۶ ردیف خام
/// برای یک سند برمی‌گرداند. این تست شمارش واقعی DbCommand های اجراشده (نه صرفاً متن SQL که ممکن
/// است در خروجی تست خیلی طولانی و بریده شود) را از طریق ILoggerProvider سبک اثبات می‌کند.
/// </summary>
public class EngineeringDocumentDetailQueryTests
{
    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _logs;
        public CapturingLogger(List<string> logs) => _logs = logs;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            if (msg.Contains("Executed DbCommand"))
                _logs.Add(msg);
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logs;
        public CapturingLoggerProvider(List<string> logs) => _logs = logs;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_logs);
        public void Dispose() { }
    }

    [Fact]
    public async Task GetByIdAsync_UsesAsSplitQuery_IssuesMultipleRoundTrips_NotOneCartesianJoin()
    {
        using var probeCtx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var documentId = await probeCtx.EngineeringDocuments.Where(x => x.IsActive).Select(x => x.DocumentId).FirstOrDefaultAsync();
        Assert.True(documentId > 0, "No active EngineeringDocument exists in the database to test against.");

        var executedCommands = new List<string>();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(executedCommands)));

        var options = new DbContextOptionsBuilder<PEMSDbContext>()
            .UseSqlServer(PemsDbContextFactory.ConnectionString)
            .UseLoggerFactory(loggerFactory)
            .Options;

        await using var loggedCtx = new PEMSDbContext(options);
        var repo = new EngineeringDocumentRepository(loggedCtx);

        var document = await repo.GetByIdAsync(documentId);

        // طبق طراحی AsSplitQuery برای این شکل Include خاص: ۱ Query اصلی (فیلدهای اسکالر +
        // Discipline/Project/Equipment/Contract/FrozenBy/UnfrozenBy) + ۱ برای Attachments +
        // ۱ برای Revisions (با پنج JOIN داخلی به Employee) + ۱ برای Workflows = دقیقاً ۴.
        // اگر این عدد به ۱ برگردد، یعنی AsSplitQuery حذف/غیرفعال شده و ضرب دکارتی برگشته.
        Assert.Equal(4, executedCommands.Count);

        // اثبات تساوی شیء برگشتی: شمارش واقعی روابط باید دقیقاً با COUNT مستقیم دیتابیس یکی باشد -
        // یعنی Split نه چیزی کم انداخته نه تکرار کرده
        Assert.NotNull(document);
        var realAttachmentCount = await probeCtx.Set<PEMS.Domain.Entities.DocumentAttachment>()
            .CountAsync(x => x.DocumentId == documentId && x.IsActive);
        var realRevisionCount = await probeCtx.DocumentRevisions.CountAsync(x => x.DocumentId == documentId && x.IsActive);
        var realWorkflowCount = await probeCtx.Set<PEMS.Domain.Entities.DocumentWorkflow>()
            .CountAsync(x => x.DocumentId == documentId && x.IsActive);

        Assert.Equal(realAttachmentCount, document!.Attachments.Count(x => x.IsActive));
        Assert.Equal(realRevisionCount, document.Revisions.Count(x => x.IsActive));
        Assert.Equal(realWorkflowCount, document.Workflows.Count(x => x.IsActive));
    }
}
