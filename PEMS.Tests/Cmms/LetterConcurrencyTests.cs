using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Correspondence Workspace» بند ۳۰: فاز قبل صراحتاً اعتراف کرد که تست Concurrency
/// واقعی نوشته نشده بود، چون قابلیت‌اطمینان شبیه‌سازی Concurrency Token در EF Core InMemory
/// تایید نشده بود. یک Spike مستقیم (این فایل نبود، حذف شد) ثابت کرد InMemory هرگز مقدار
/// RowVersion را تولید نمی‌کند (همیشه null) - پس هیچ تست Concurrency واقعی روی InMemory معنا
/// ندارد. این فایل به‌جای آن از همون الگوی موجود (EquipmentCriticalityConcurrencyTests/
/// TransmittalNumberingConcurrencyTests) استفاده می‌کند: یک دیتابیس SQL Server ایزوله واقعی
/// (ساخته‌شده از تاریخچه‌ی کامل Migration واقعی، پس ستون rowversion واقعی SQL Server را دارد)
/// + Barrier برای تضمین هم‌پوشانی واقعی، نه صرفاً Task.WhenAll ساده.
/// </summary>
public class LetterConcurrencyTests
{
    private const string IsolatedDbName = "PEMSDB_LetterConcurrencyTest";
    private const int AuthorUserId = 9001;
    private const int ReviewerUserId = 9002;

    private static LetterService CreateService(PEMS.Persistence.Context.PEMSDbContext ctx)
        => new(ctx, new RichTextSanitizerService(), knowledgeBaseService: null!);

    private static async Task<int> SeedLetterAsync(string status)
    {
        await using var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);

        var letter = new Letter
        {
            LetterType = "Outgoing",
            LetterNumber = $"CT-{Guid.NewGuid():N}"[..14],
            LetterDate = DateTime.Today,
            Receiver = "Concurrency Test Vendor",
            Subject = "Concurrency Test Subject",
            Body = "<p>Concurrency test body</p>",
            ConfidentialityLevel = "Internal",
            Status = status,
            RegisteredById = AuthorUserId,
            IsActive = true,
            CreatedDate = DateTime.Now
        };
        seedCtx.Letters.Add(letter);
        await seedCtx.SaveChangesAsync();
        return letter.LetterId;
    }

    private static async Task CleanupAsync(int letterId)
    {
        await using var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
        var reviews = cleanupCtx.LetterReviews.Where(x => x.LetterId == letterId);
        cleanupCtx.LetterReviews.RemoveRange(reviews);
        var letter = await cleanupCtx.Letters.FindAsync(letterId);
        if (letter != null) cleanupCtx.Letters.Remove(letter);
        await cleanupCtx.SaveChangesAsync();
    }

    [Fact]
    public async Task ApproveAsync_ConcurrentWithReturnForRevisionAsync_ExactlyOneTransitionSucceeds()
    {
        var letterId = await SeedLetterAsync("PendingApproval");

        try
        {
            using var barrier = new Barrier(2);

            var approveTask = Task.Run(async () =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
                var service = CreateService(ctx);
                barrier.SignalAndWait();
                return await service.ApproveAsync(letterId, ReviewerUserId, "Approving");
            });

            var returnTask = Task.Run(async () =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
                var service = CreateService(ctx);
                barrier.SignalAndWait();
                return await service.ReturnForRevisionAsync(letterId, ReviewerUserId, "Needs revision");
            });

            var results = await Task.WhenAll(approveTask, returnTask);

            // دقیقاً یکی از این دو گذار باید موفق شود - نه هر دو (که یعنی یکی نامه را خراب کرده)
            // و نه هیچ‌کدام (که یعنی مکانیزم Concurrency به‌اشتباه هر دو را مسدود کرده)
            var successCount = results.Count(r => r.Success);
            Assert.Equal(1, successCount);

            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var finalLetter = await verifyCtx.Letters.FindAsync(letterId);
            Assert.True(finalLetter!.Status is "Approved" or "ReturnedForRevision");

            // دقیقاً یک ردیف LetterReview باید ساخته شده باشد - نه دو (یکی برای هرکدام)، که یعنی
            // بازنده واقعاً نوشته‌ی خودش را کامل کرده و بعد رد شده - سناریوی ناسازگار تاریخچه
            var reviewCount = verifyCtx.LetterReviews.Count(x => x.LetterId == letterId);
            Assert.Equal(1, reviewCount);
        }
        finally
        {
            await CleanupAsync(letterId);
        }
    }

    [Fact]
    public async Task ApproveAsync_CalledTwiceConcurrently_ExactlyOneSucceeds_NoDuplicateReview()
    {
        // طبق بند ۳۰: «عملیات تکراری هم‌زمان» - همون Approve دوبار پشت‌سرهم توسط دو درخواست هم‌زمان
        var letterId = await SeedLetterAsync("PendingApproval");

        try
        {
            using var barrier = new Barrier(2);

            var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
                var service = CreateService(ctx);
                barrier.SignalAndWait();
                return await service.ApproveAsync(letterId, ReviewerUserId, "Approve attempt");
            }));

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(r => r.Success));
            Assert.Equal(1, results.Count(r => !r.Success));

            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            Assert.Equal("Approved", (await verifyCtx.Letters.FindAsync(letterId))!.Status);
            Assert.Equal(1, verifyCtx.LetterReviews.Count(x => x.LetterId == letterId));
        }
        finally
        {
            await CleanupAsync(letterId);
        }
    }

    [Fact]
    public async Task ResubmitAsync_WithRowVersionCapturedBeforeAConcurrentApproval_FailsSafelyInsteadOfCorrupting()
    {
        // سناریوی «ویرایش در برابر نسخه‌ی تغییر‌کرده»: کاربر A صفحه را باز می‌کند (RowVersion را
        // می‌گیرد) و هم‌زمان یک درخواست دیگر (اینجا شبیه‌سازی‌شده به‌صورت متوالی، نه واقعاً هم‌زمان
        // چون این مسیر یک نویسنده‌ی واحد دارد نه Race چندنفره) وضعیت را عوض می‌کند - سپس تلاش A
        // برای Resubmit با RowVersion قدیمی باید امن شکست بخورد، نه رکورد را خراب کند.
        var letterId = await SeedLetterAsync("ReturnedForRevision");

        try
        {
            byte[]? staleRowVersion;
            await using (var readCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
            {
                var letter = await readCtx.Letters.FindAsync(letterId);
                staleRowVersion = letter!.RowVersion;
                Assert.NotNull(staleRowVersion);
            }

            // یک تغییر دیگر روی همون نامه اعمال می‌شود - RowVersion واقعی در دیتابیس عوض می‌شود
            await using (var otherCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
            {
                var letter = await otherCtx.Letters.FindAsync(letterId);
                letter!.Subject = "Changed by someone else first";
                await otherCtx.SaveChangesAsync();
            }

            await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var service = CreateService(ctx);

            var (success, error) = await service.ResubmitAsync(letterId, "My resubmission", "<p>my body</p>", AuthorUserId, staleRowVersion);

            Assert.False(success);
            Assert.NotNull(error);

            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var finalLetter = await verifyCtx.Letters.FindAsync(letterId);
            // نه محتوای رقیب از بین رفته و نه Resubmit نیمه‌کاره اعمال شده
            Assert.Equal("Changed by someone else first", finalLetter!.Subject);
            Assert.Equal("ReturnedForRevision", finalLetter.Status);
        }
        finally
        {
            await CleanupAsync(letterId);
        }
    }
}
