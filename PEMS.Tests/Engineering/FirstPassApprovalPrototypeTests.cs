using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-05C (Mandatory First Gate): این فایل خودِ Prototype اعتبارسنجی First-Pass
/// Approval است - نه یک شبیه‌سازی جدا، بلکه دقیقاً همان DocumentWorkflowService/
/// WorkflowRuntimeService واقعی روی SQL Server واقعی، تا هم شکل SQL تولیدشده (ToQueryString)
/// و هم صحت منطقی روی سناریوهای واقعی چرخه‌ی مدرک تایید شود.
///
/// یافته‌ی کلیدی این بررسی: داخل یک WorkflowInstance، Reject همیشه پایانی است
/// (WorkflowRuntimeService.ExecuteActionAsync: instance.Status = Reject بودن یعنی "Rejected"،
/// نه یک برگشت داخل همان Instance). یعنی "First-Pass Approval" در سطح یک Instance بی‌معناست
/// (هر Instance یا مستقیم Completed می‌شود یا Rejected - هرگز هر دو). تعریف واقعی و
/// معنادار این متریک در سطح "سند" است: آیا زودترین WorkflowInstance (بر اساس StartDate)
/// که برای این سند شروع شده، Completed بوده - نه هر Instance ای که بعداً Completed شود.
/// این تعریف به‌درستی رویژن بعد از یک Issue موفق (بدون Reject قبلی) را جریمه نمی‌کند.
/// </summary>
public class FirstPassApprovalPrototypeTests
{
    // طبق تجربه‌ی EF Core: GroupBy(...).Select(g => g.OrderBy(...).First()) گاهی به‌خوبی
    // Translate نمی‌شود (بسته به نسخه). الگوی NOT EXISTS همبسته (correlated subquery) پایدارتر
    // و همیشه کاملاً سمت سرور Translate می‌شود - این‌جا هر دو را می‌سازیم و SQL واقعی را با
    // ToQueryString() بررسی می‌کنیم، نه فقط حدس می‌زنیم.
    private static IQueryable<int> FirstPassApprovedDocumentIds(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var instances = ctx.WorkflowInstances
            .Where(wi => wi.Module == "EngineeringDocument" && wi.BusinessObjectType == "Document");

        // زودترین Instance هر سند = آن‌که هیچ Instance قدیمی‌تری (StartDate کمتر) برای همان سند ندارد
        var earliestInstances = instances.Where(wi => !instances.Any(other =>
            other.BusinessObjectId == wi.BusinessObjectId && other.StartDate < wi.StartDate));

        return earliestInstances.Where(wi => wi.Status == "Completed").Select(wi => wi.BusinessObjectId);
    }

    [Fact]
    public void CandidateQuery_TranslatesFullyToServerSideSql_NoClientEvaluation()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var sql = FirstPassApprovedDocumentIds(ctx).ToQueryString();
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "e05c_firstpass_sql.txt"), sql);

        // اگر EF Core نتواند این را کامل سمت سرور Translate کند، یا Exception پرتاب می‌کند
        // (نسخه‌های جدید EF Core - رفتار پیش‌فرض از EF Core 3 به بعد) یا در متن SQL هیچ اثری
        // از NOT EXISTS/subquery همبسته دیده نمی‌شود. هر دو حالت این تست را Fail می‌کند.
        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);

        // یافته‌ی مستند: خروجی واقعی ToQueryString این‌جا ثبت می‌شود (در گزارش E-05C) تا
        // اثبات باشد نه فقط ادعا.
    }

    [Fact]
    public async Task Scenario_SingleInstance_ApprovedDirectly_IsFirstPass()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (doc, _, _) = await SeedAndRunToIssuedAsync(ctx, "A");

        var ids = await FirstPassApprovedDocumentIds(ctx).ToListAsync();

        Assert.Contains(doc.DocumentId, ids);
    }

    [Fact]
    public async Task Scenario_RejectedThenResubmittedThenApproved_IsNotFirstPass()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var project = await SeedProjectAsync(ctx, "B");
        var employeeId = await SeedEmployeeAsync(ctx, "B");
        var runtime = new WorkflowRuntimeService(ctx);
        var letters = new PEMS.Tests.TestSupport.FakeLetterService();
        var workflow = new DocumentWorkflowService(ctx, runtime, letters, new AccessControlService(ctx));

        var doc = new EngineeringDocument
        {
            DocumentNo = $"FPA-B-{Guid.NewGuid():N}".Substring(0, 20),
            Title = "Doc B", DocumentType = "Datasheet", ProjectId = project.ProjectId, Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        // تلاش اول: رد می‌شود (Instance اول Rejected)
        await workflow.SubmitAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.RejectAsync(doc.DocumentId, "missing data", employeeId, new[] { "EngineeringSupervisor" });

        // تلاش دوم: کامل تایید و صادر می‌شود (Instance دوم Completed)
        await workflow.SubmitAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.CheckAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringSupervisor" });
        await workflow.ApproveAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.IssueAsync(doc.DocumentId, null, employeeId, new[] { "DocumentController" },
            DocumentWorkflowService.DeliveryUnitInternalDcc);

        var ids = await FirstPassApprovedDocumentIds(ctx).ToListAsync();

        // طبق تعریف: زودترین Instance (تلاش اول) Rejected بود - پس این سند First-Pass نیست،
        // حتی اگر الان در نهایت Issued باشد.
        Assert.DoesNotContain(doc.DocumentId, ids);
    }

    [Fact]
    public async Task Scenario_ApprovedThenLaterRevisedAndReissued_StillCountsAsFirstPass()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (doc, workflow, employeeId) = await SeedAndRunToIssuedAsync(ctx, "C");

        // طبق بررسی واقعی CreateRevision (EngineeringDocumentsController): این اکشن حتی روی
        // مدرک Issued هم مجاز است و فقط Status را به Draft برمی‌گرداند - موتور گردش‌کار را
        // اصلاً لمس نمی‌کند. رویژن جدید این‌جا مستقیماً شبیه‌سازی می‌شود (بدون رفتن از کنترلر).
        ctx.DocumentRevisions.Add(new DocumentRevision
        {
            DocumentId = doc.DocumentId, RevisionNo = "01", Status = "Draft",
            RevisionDate = DateTime.Now, IsActive = true
        });
        doc.Status = "Draft";
        await ctx.SaveChangesAsync();

        // چرخه‌ی دوم: دوباره بدون رد شدن تایید و صادر می‌شود (Instance دوم هم Completed)
        await workflow.SubmitAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.CheckAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringSupervisor" });
        await workflow.ApproveAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.IssueAsync(doc.DocumentId, null, employeeId, new[] { "DocumentController" },
            DocumentWorkflowService.DeliveryUnitInternalDcc);

        var ids = await FirstPassApprovedDocumentIds(ctx).ToListAsync();

        // طبق تعریف: زودترین Instance (چرخه‌ی اول، پیش از رویژن) Completed بود - این سند
        // همچنان First-Pass محسوب می‌شود، با وجود رویژن و صدور دوباره‌ی بعدی.
        Assert.Contains(doc.DocumentId, ids);
    }

    [Fact]
    public async Task Scenario_NeverSubmitted_HasNoInstance_ExcludedFromRate()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var project = await SeedProjectAsync(ctx, "D");
        var doc = new EngineeringDocument
        {
            DocumentNo = $"FPA-D-{Guid.NewGuid():N}".Substring(0, 20),
            Title = "Doc D", DocumentType = "Datasheet", ProjectId = project.ProjectId, Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var ids = await FirstPassApprovedDocumentIds(ctx).ToListAsync();

        // طبق قانون "Missing نباید Zero/False جا زده شود": سندی که هیچ‌وقت Submit نشده نه در
        // فهرست First-Pass است و نه باید در مخرج کسر نرخ (Rate) شمرده شود - این تست فقط
        // تایید می‌کند که چنین سندی اصلاً در نتیجه‌ی Query ظاهر نمی‌شود (نه true و نه false).
        Assert.DoesNotContain(doc.DocumentId, ids);
    }

    // طبق پذیرش ممیزی G/M/F (بخش B - "Existing test-data failures"): این تست‌ها قبلاً وجود
    // EmployeeId=1/DisciplineId=1 در PEMSDB مشترک را مفروض می‌گرفتند که هیچ‌کدام تضمین‌شده نیست
    // (P0 Security Containment §B این را دوباره تایید کرد - DisciplineId=1 دیگر در PEMSDB مشترک
    // وجود ندارد). این‌جا برای هر اجرا یک Discipline و Employee واقعی و کاملاً مستقل ساخته
    // می‌شود - وابسته به هیچ ردیف از قبل موجود در PEMSDB نیست.
    private static async Task<int> SeedEmployeeAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        // Name هم مثل Code یک ایندکس Unique دارد (IX_Departments_Name) - باید هردو واقعاً یکتا
        // باشند، نه فقط بر مبنای tag کوتاه که بین اجراهای مختلف تست تکرار می‌شود.
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var department = new Department { Code = $"FPA{tag}{uniqueSuffix}"[..10], Name = $"FPA Test Dept {tag} {uniqueSuffix}" };
        var discipline = new Discipline { Code = $"FPD{tag}{uniqueSuffix}"[..10], Name = $"FPA Test Disc {tag} {uniqueSuffix}" };
        ctx.Departments.Add(department);
        ctx.Disciplines.Add(discipline);
        await ctx.SaveChangesAsync();

        var employee = new Employee
        {
            FirstName = "FPA", LastName = $"Test{tag}",
            DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId
        };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        return employee.EmployeeId;
    }

    private static async Task<Project> SeedProjectAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        // طبق مشاهده: EngineeringDocumentWorkflowSeeder.SeedAsync Idempotent است (چک AnyAsync
        // قبل از درج) - صدا زدنش این‌جا اگر دیتابیس واقعی از قبل Seed شده باشد بی‌اثر است،
        // و اگر نشده باشد، SubmitAsync بدون آن با خطای "تعریف گردش‌کار یافت نشد" شکست می‌خورد.
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await EngineeringDocumentWorkflowSeeder.SeedAsync(services.BuildServiceProvider());

        var project = new Project { ProjectName = $"FPA Test {tag}", ProjectCode = $"FPA{tag}{Guid.NewGuid():N}".Substring(0, 10) };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project;
    }

    private static async Task<(EngineeringDocument doc, DocumentWorkflowService workflow, int employeeId)> SeedAndRunToIssuedAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var project = await SeedProjectAsync(ctx, tag);
        var employeeId = await SeedEmployeeAsync(ctx, tag);
        var runtime = new WorkflowRuntimeService(ctx);
        var letters = new PEMS.Tests.TestSupport.FakeLetterService();
        var workflow = new DocumentWorkflowService(ctx, runtime, letters, new AccessControlService(ctx));

        var doc = new EngineeringDocument
        {
            DocumentNo = $"FPA-{tag}-{Guid.NewGuid():N}".Substring(0, 20),
            Title = $"Doc {tag}", DocumentType = "Datasheet", ProjectId = project.ProjectId, Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        await workflow.SubmitAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.CheckAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringSupervisor" });
        await workflow.ApproveAsync(doc.DocumentId, null, employeeId, new[] { "EngineeringManager" });
        await workflow.IssueAsync(doc.DocumentId, null, employeeId, new[] { "DocumentController" },
            DocumentWorkflowService.DeliveryUnitInternalDcc);

        return (doc, workflow, employeeId);
    }
}
