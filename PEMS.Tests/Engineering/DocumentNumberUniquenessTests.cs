using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-01 (رفع P0 ممیزی شماره‌گذاری، بخش شماره‌ی دستی): برخلاف تخصیص خودکار (که با
/// NumberingService اتمیک شد)، شماره‌ی دستی (CanManualNumber) هنوز می‌تواند با یک مدرک دیگر
/// تصادفی برخورد کند - ایندکس Unique سطح دیتابیس (Documents.DocumentNo) این را رد می‌کند.
/// این تست فقط بعد از اعمال Migration مربوط به این فاز روی دیتابیس هدف موفق می‌شود؛ پیش از آن
/// (یا وقتی SQL Server در دسترس نیست) با پیام واضح Fail می‌شود - طبق همون سیاست عدم Skip خاموش.
/// </summary>
public class DocumentNumberUniquenessTests
{
    [Fact]
    public async Task DuplicateDocumentNo_ViolatesUniqueIndex()
    {
        await using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        // طبق P0 Security Containment §B (شکست‌های آلودگی دیتابیس مشترک): این تست قبلاً وجود
        // ProjectId=1 در PEMSDB مشترک را مفروض می‌گرفت که تضمین‌شده نیست - این‌جا برای هر اجرا
        // یک Project واقعی و مستقل ساخته می‌شود، وابسته به هیچ ردیف از قبل موجود در PEMSDB نیست.
        var project = new Project
        {
            ProjectName = "DocNo Uniqueness Test",
            ProjectCode = $"DNU{Guid.NewGuid():N}".Substring(0, 10)
        };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var sharedNumber = $"TEST-DUP-{Guid.NewGuid():N}".Substring(0, 30);

        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = sharedNumber,
            Title = "First",
            DocumentType = "Datasheet",
            ProjectId = project.ProjectId,
            Status = "Draft"
        });
        await ctx.SaveChangesAsync();

        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = sharedNumber,
            Title = "Second - should collide",
            DocumentType = "Datasheet",
            ProjectId = project.ProjectId,
            Status = "Draft"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}
