using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave A — ممیزی مستقل» بندهای P1-7 و P1-8: ChangeStatusAsync قبلاً هر
/// رشته‌ای را بدون بررسی گذار قانونی می‌پذیرفت (و روی MR ناموجود بی‌صدا هیچ کاری نمی‌کرد)، و
/// CreateAsync/AddQuotationAsync هیچ‌کدام از ProjectId/DisciplineId/ResponsibleEngineerId/
/// یکتایی MRNumber/وجود MR مرجع را بررسی نمی‌کردند - فقط به Exception سطح FK تکیه می‌شد.
/// </summary>
public class MaterialRequisitionServiceTests
{
    private static MaterialRequisitionService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"MRService_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project", IsActive = true });
        ctx.Projects.Add(new Project { ProjectId = 2, ProjectCode = "INACTIVE", ProjectName = "Inactive Project", IsActive = false });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 2, Code = "INACTIVE", Name = "Inactive Discipline", IsActive = false });
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Eng" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Test", LastName = "Engineer", DepartmentId = 1, IsActive = true });
        ctx.Employees.Add(new Employee { EmployeeId = 2, FirstName = "Inactive", LastName = "Engineer", DepartmentId = 1, IsActive = false });
        ctx.SaveChanges();
        return new MaterialRequisitionService(ctx);
    }

    private static int _nextId = 1;

    private static MaterialRequisition NewMr(PEMS.Persistence.Context.PEMSDbContext ctx, string status = "Draft")
    {
        var mr = new MaterialRequisition
        {
            MRId = _nextId++,
            MRNumber = $"MR-{_nextId:D4}",
            ProjectId = 1,
            DisciplineId = 1,
            Description = "Test MR",
            Status = status,
            CreatedDate = DateTime.Now
        };
        ctx.MaterialRequisitions.Add(mr);
        ctx.SaveChanges();
        return mr;
    }

    // ============================= CreateAsync =============================

    [Fact]
    public async Task CreateAsync_ValidInput_Succeeds()
    {
        var service = CreateService(nameof(CreateAsync_ValidInput_Succeeds), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 1, "TAG-01", "Test", null, 1, null, new());

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task CreateAsync_EmptyMrNumber_Fails()
    {
        var service = CreateService(nameof(CreateAsync_EmptyMrNumber_Fails), out _);

        var (success, error, id) = await service.CreateAsync("   ", 1, 1, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
    }

    [Fact]
    public async Task CreateAsync_DuplicateMrNumber_Fails()
    {
        var service = CreateService(nameof(CreateAsync_DuplicateMrNumber_Fails), out var ctx);
        ctx.MaterialRequisitions.Add(new MaterialRequisition { MRId = 1, MRNumber = "MR-DUP", ProjectId = 1, DisciplineId = 1, Description = "x", Status = "Draft", CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var (success, error, id) = await service.CreateAsync("MR-DUP", 1, 1, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
    }

    [Fact]
    public async Task CreateAsync_NonexistentProject_Fails()
    {
        var service = CreateService(nameof(CreateAsync_NonexistentProject_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 999, 1, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_InactiveProject_Fails()
    {
        var service = CreateService(nameof(CreateAsync_InactiveProject_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 2, 1, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_NonexistentDiscipline_Fails()
    {
        var service = CreateService(nameof(CreateAsync_NonexistentDiscipline_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 999, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_InactiveDiscipline_Fails()
    {
        var service = CreateService(nameof(CreateAsync_InactiveDiscipline_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 2, null, "Test", null, null, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_NonexistentResponsibleEngineer_Fails()
    {
        var service = CreateService(nameof(CreateAsync_NonexistentResponsibleEngineer_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 1, null, "Test", null, 999, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_InactiveResponsibleEngineer_Fails()
    {
        var service = CreateService(nameof(CreateAsync_InactiveResponsibleEngineer_Fails), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 1, null, "Test", null, 2, null, new());

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_NullResponsibleEngineer_Succeeds()
    {
        var service = CreateService(nameof(CreateAsync_NullResponsibleEngineer_Succeeds), out _);

        var (success, error, id) = await service.CreateAsync("MR-0001", 1, 1, null, "Test", null, null, null, new());

        Assert.True(success);
        Assert.Null(error);
    }

    // ============================= ChangeStatusAsync =============================

    [Theory]
    [InlineData("Draft", "Submitted")]
    [InlineData("Draft", "Cancelled")]
    [InlineData("Submitted", "Under Technical Review")]
    [InlineData("Under Technical Review", "Approved")]
    [InlineData("Approved", "Issued to Procurement")]
    [InlineData("Issued to Procurement", "Closed")]
    public async Task ChangeStatusAsync_ValidTransition_Succeeds(string from, string to)
    {
        var service = CreateService($"{nameof(ChangeStatusAsync_ValidTransition_Succeeds)}_{from}_{to}", out var ctx);
        var mr = NewMr(ctx, from);

        var (success, error) = await service.ChangeStatusAsync(mr.MRId, to);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(to, ctx.MaterialRequisitions.Single(x => x.MRId == mr.MRId).Status);
    }

    [Theory]
    [InlineData("Draft", "Approved")]
    [InlineData("Draft", "Closed")]
    [InlineData("Submitted", "Closed")]
    [InlineData("Approved", "Draft")]
    [InlineData("Closed", "Draft")]
    [InlineData("Cancelled", "Draft")]
    public async Task ChangeStatusAsync_InvalidTransition_Fails(string from, string to)
    {
        var service = CreateService($"{nameof(ChangeStatusAsync_InvalidTransition_Fails)}_{from}_{to}", out var ctx);
        var mr = NewMr(ctx, from);

        var (success, error) = await service.ChangeStatusAsync(mr.MRId, to);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal(from, ctx.MaterialRequisitions.Single(x => x.MRId == mr.MRId).Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_NonexistentMr_Fails()
    {
        var service = CreateService(nameof(ChangeStatusAsync_NonexistentMr_Fails), out _);

        var (success, error) = await service.ChangeStatusAsync(999, "Submitted");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ChangeStatusAsync_RepeatedTransition_SecondCallFails()
    {
        var service = CreateService(nameof(ChangeStatusAsync_RepeatedTransition_SecondCallFails), out var ctx);
        var mr = NewMr(ctx, "Draft");

        var first = await service.ChangeStatusAsync(mr.MRId, "Submitted");
        var second = await service.ChangeStatusAsync(mr.MRId, "Submitted");

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.NotNull(second.Error);
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Cancelled")]
    public async Task ChangeStatusAsync_FromTerminalStatus_AlwaysFails(string terminalStatus)
    {
        var service = CreateService($"{nameof(ChangeStatusAsync_FromTerminalStatus_AlwaysFails)}_{terminalStatus}", out var ctx);
        var mr = NewMr(ctx, terminalStatus);

        var (success, error) = await service.ChangeStatusAsync(mr.MRId, "Submitted");

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ============================= AddQuotationAsync =============================

    [Fact]
    public async Task AddQuotationAsync_ExistingMr_Succeeds()
    {
        var service = CreateService(nameof(AddQuotationAsync_ExistingMr_Succeeds), out var ctx);
        var mr = NewMr(ctx);

        var (success, error, id) = await service.AddQuotationAsync(mr.MRId, "MRQ-0001", "Vendor A", null, null, null, null);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task AddQuotationAsync_NonexistentMr_Fails()
    {
        var service = CreateService(nameof(AddQuotationAsync_NonexistentMr_Fails), out var ctx);

        var (success, error, id) = await service.AddQuotationAsync(999, "MRQ-0001", "Vendor A", null, null, null, null);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
        Assert.Empty(ctx.MaterialRequisitionQuotations);
    }
}
