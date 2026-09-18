using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Engineering;

/// <summary>
/// DIAGNOSTIC ONLY - طبق ادعای «WorkflowHistory بعد از اصلاحات مالکیت IDC دیگر رکورد
/// نمی‌سازد»: این تست دقیقاً همان مسیر EngineeringWorkflowTestBase را تکرار می‌کند اما روی یک
/// دیتابیس SQL Server ایزوله‌ی واقعی (نه InMemory) اجرا می‌شود، تا هر تفاوت رفتاری بین دو
/// Provider (مثلاً RowVersion/Concurrency Token واقعی) که ممکن است روی InMemory پنهان بماند،
/// آشکار شود. طبق سیاست پروژه، هیچ داده‌ی واقعی PEMSDB لمس نمی‌شود -
/// CreateIsolatedSqlServerOrThrow یک دیتابیس کاملاً جدا با Migration واقعی می‌سازد.
/// </summary>
public class EngineeringWorkflowHistorySqlServerDiagnosticTests
{
    [Fact]
    public async Task IdcRespondAsync_OnRealSqlServer_CreatesWorkflowHistoryRows()
    {
        var dbName = $"PEMS_IDC_History_Diag_{Guid.NewGuid():N}".Substring(0, 40);
        var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(dbName);

        try
        {
            var project = new Project { ProjectName = "Diag Project", ProjectCode = "DIAG" };
            var department = new Department { Code = "ENG-DIAG", Name = "Engineering Diag" };
            var disciplineMech = new Discipline { Name = "Mechanical", Code = "MECH-DIAG" };
            ctx.Projects.Add(project);
            ctx.Departments.Add(department);
            ctx.Disciplines.Add(disciplineMech);
            await ctx.SaveChangesAsync();

            // طبق کشف این تست: EF InMemory (تست‌های موجود) هرگز FK واقعی را چک نمی‌کند - روی
            // SQL Server واقعی، actionByEmployeeId باید به یک Employee واقعی اشاره کند
            var managerEmployee = new Employee { FirstName = "Diag", LastName = "Manager", DepartmentId = department.DepartmentId, DisciplineId = disciplineMech.DisciplineId };
            var supervisorEmployee = new Employee { FirstName = "Diag", LastName = "Supervisor", DepartmentId = department.DepartmentId, DisciplineId = disciplineMech.DisciplineId };
            var reviewerEmployee = new Employee { FirstName = "Diag", LastName = "Reviewer", DepartmentId = department.DepartmentId, DisciplineId = disciplineMech.DisciplineId };
            ctx.Employees.AddRange(managerEmployee, supervisorEmployee, reviewerEmployee);
            await ctx.SaveChangesAsync();

            var services = new ServiceCollection();
            services.AddSingleton(ctx);
            var provider = services.BuildServiceProvider();
            await EngineeringDocumentWorkflowSeeder.SeedAsync(provider);

            var runtime = new WorkflowRuntimeService(ctx);
            var letters = new FakeLetterService();
            var workflowService = new DocumentWorkflowService(ctx, runtime, letters, new AccessControlService(ctx));

            var doc = new EngineeringDocument
            {
                DocumentNo = $"DIAG-{Guid.NewGuid():N}".Substring(0, 20),
                Title = "Diagnostic Document",
                DocumentType = "Datasheet",
                ProjectId = project.ProjectId,
                Status = "Draft"
            };
            ctx.EngineeringDocuments.Add(doc);
            await ctx.SaveChangesAsync();

            string[] managerRole = { SystemRoles.EngineeringManager };
            string[] supervisorRole = { SystemRoles.EngineeringSupervisor };

            await workflowService.SubmitAsync(doc.DocumentId, "submit", managerEmployee.EmployeeId, managerRole,
                idcDisciplineIds: new[] { disciplineMech.DisciplineId });

            var historyAfterSubmit = await ctx.WorkflowHistories.CountAsync();

            await workflowService.CheckAsync(doc.DocumentId, "checked", supervisorEmployee.EmployeeId, supervisorRole);

            var historyAfterCheck = await ctx.WorkflowHistories.CountAsync();

            await workflowService.IdcRespondAsync(doc.DocumentId, "Reject", "missing calcs", reviewerEmployee.EmployeeId, disciplineMech.DisciplineId);

            var historyAfterIdc = await ctx.WorkflowHistories.CountAsync();

            // اگر ادعای رگرسیون درست باشد، این Assert ها شکست می‌خورند (نه فقط تعداد رکورد
            // InMemory که قبلاً سبز بود)
            Assert.True(historyAfterSubmit > 0, $"Expected WorkflowHistory rows after Submit/Start, got {historyAfterSubmit}");
            Assert.True(historyAfterCheck > historyAfterSubmit, $"Expected new WorkflowHistory rows after Check ({historyAfterSubmit} -> {historyAfterCheck})");
            Assert.True(historyAfterIdc > historyAfterCheck, $"Expected new WorkflowHistory rows after IdcRespond ({historyAfterCheck} -> {historyAfterIdc})");

            var instanceId = ctx.WorkflowInstances
                .Where(x => x.BusinessObjectType == "Document" && x.BusinessObjectId == doc.DocumentId)
                .OrderByDescending(x => x.StartDate)
                .Select(x => x.WorkflowInstanceId)
                .First();

            var idcHistoryRows = await ctx.WorkflowHistories
                .Where(h => h.WorkflowInstanceId == instanceId)
                .OrderBy(h => h.ActionDate)
                .ToListAsync();

            Assert.NotEmpty(idcHistoryRows);
            Assert.Contains(idcHistoryRows, h => h.Comment == "missing calcs");
            Assert.Equal("Checked", doc.Status); // ownership-correction: IDC reject does not terminate the document
        }
        finally
        {
            await ctx.Database.EnsureDeletedAsync();
        }
    }
}
