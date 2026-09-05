using PEMS.Domain.Entities;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-03 (رفع P1 ممیزی Performance): تست‌های GetPagedAsync - تمرکز روی دو ریسک اصلی
/// این فاز: (۱) حفظ دقیق رفتار فعلی محدودیت دیسیپلین حین انتقال آن به SQL، (۲) پایداری
/// صفحه‌بندی (بدون ORDER BY صریح، Skip/Take در SQL Server تضمین‌شده نیست).
/// </summary>
public class EngineeringDocumentPagedListTests
{
    private static EngineeringDocumentRepository CreateRepo(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory(dbName);

        // GetPagedAsync شامل Include(Project)/Include(Discipline) است - چون این‌ها روابط
        // اجباری (FK غیر-Nullable برای Project) هستند، باید رکورد اصلی واقعاً وجود داشته باشد
        // وگرنه EF Core InMemory سند را از نتیجه حذف می‌کند (برخلاف رفتار SQL واقعی که فقط
        // Navigation را Null می‌گذارد).
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 5, Code = "MECH", Name = "Mechanical" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 6, Code = "ELEC", Name = "Electrical" });
        ctx.SaveChanges();

        return new EngineeringDocumentRepository(ctx);
    }

    private static EngineeringDocument NewDoc(int projectId, int? disciplineId, DateTime createdDate, string title = "Doc") => new()
    {
        DocumentNo = $"TP-DAT-{Guid.NewGuid():N}".Substring(0, 20),
        Title = title,
        DocumentType = "Datasheet",
        ProjectId = projectId,
        DisciplineId = disciplineId,
        Status = "Draft",
        IsActive = true,
        CreatedDate = createdDate
    };

    [Fact]
    public async Task GetPagedAsync_RestrictedEmployeeWithNoDiscipline_SeesOnlyNullDisciplineDocs()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_RestrictedEmployeeWithNoDiscipline_SeesOnlyNullDisciplineDocs), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mechanical doc"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now, title: "Unassigned doc"));
        await ctx.SaveChangesAsync();

        // کارمندی که دیسیپلین ندارد (disciplineId: null) اما نقشش محدود است -
        // طبق طراحی، باید فقط مدارک بدون‌دیسیپلین را ببیند، نه همه را.
        var result = await repo.GetPagedAsync(new(), restrictToDiscipline: true, disciplineId: null);

        Assert.Single(result.Items);
        Assert.Equal("Unassigned doc", result.Items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_RestrictedEmployeeWithDiscipline_SeesOwnAndNullDisciplineDocs_NotOthers()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_RestrictedEmployeeWithDiscipline_SeesOwnAndNullDisciplineDocs_NotOthers), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mine"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 6, createdDate: DateTime.Now, title: "Other discipline"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now, title: "Unassigned"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetPagedAsync(new(), restrictToDiscipline: true, disciplineId: 5);

        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, x => x.Title == "Other discipline");
    }

    [Fact]
    public async Task GetPagedAsync_Unrestricted_SeesEverything()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_Unrestricted_SeesEverything), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 6, createdDate: DateTime.Now));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now));
        await ctx.SaveChangesAsync();

        var result = await repo.GetPagedAsync(new(), restrictToDiscipline: false, disciplineId: null);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsStableDisjointPages()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_ReturnsStableDisjointPages), out var ctx);

        // طبق سند Global Data Grid Standard بند ۴: فقط ۲۰/۵۰/۱۰۰ اندازه‌ی صفحه‌ی مجاز است - این
        // تست حالا با PageSize=20 (نه یک عدد دلخواه مثل ۵ که دیگر معتبر نیست و به ۵۰ برمی‌گردد)
        // و ۲۵ سند (۲۰ + ۵) پایداری/عدم هم‌پوشانی صفحات را اثبات می‌کند
        for (var i = 0; i < 25; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now.AddMinutes(-i), title: $"Doc {i}"));
        await ctx.SaveChangesAsync();

        var page1 = await repo.GetPagedAsync(new() { Page = 1, PageSize = 20 }, false, null);
        var page2 = await repo.GetPagedAsync(new() { Page = 2, PageSize = 20 }, false, null);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.DocumentId).ToList();
        Assert.Equal(25, allIds.Distinct().Count()); // no duplicates, no gaps across pages
    }

    // طبق «PEMS Phase 6B — P2: Deterministic Ordering». قبلاً فقط OrderByDescending(CreatedDate)
    // بود، بدون ThenBy(Id). حالا ThenByDescending(DocumentId) بین رکوردهای با CreatedDate یکسان
    // هم ترتیب پایدار تضمین می‌کند.
    [Fact]
    public async Task GetPagedAsync_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_DeterministicOrdering_SameCreatedDate_SameOrderTwice), out var ctx);

        var sameDate = DateTime.Now;
        for (var i = 0; i < 10; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: sameDate, title: $"Doc {i}"));
        await ctx.SaveChangesAsync();

        var first = await repo.GetPagedAsync(new() { Page = 1, PageSize = 20 }, false, null);
        var second = await repo.GetPagedAsync(new() { Page = 1, PageSize = 20 }, false, null);

        Assert.Equal(first.Items.Select(x => x.DocumentId), second.Items.Select(x => x.DocumentId));
    }

    [Fact]
    public async Task GetPagedAsync_SearchFilter_MatchesDocumentNoOrTitle()
    {
        var repo = CreateRepo(nameof(GetPagedAsync_SearchFilter_MatchesDocumentNoOrTitle), out var ctx);

        var target = NewDoc(1, null, DateTime.Now, title: "Pressure Vessel Datasheet");
        ctx.EngineeringDocuments.Add(target);
        ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, title: "Unrelated"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetPagedAsync(new() { Search = "Pressure Vessel" }, false, null);

        Assert.Single(result.Items);
        Assert.Equal(target.DocumentId, result.Items[0].DocumentId);
    }

    [Fact]
    public async Task GetSummaryCountsAsync_RespectsDisciplineRestriction_NotOtherFilters()
    {
        var repo = CreateRepo(nameof(GetSummaryCountsAsync_RespectsDisciplineRestriction_NotOtherFilters), out var ctx);

        var mine = NewDoc(1, disciplineId: 5, createdDate: DateTime.Now);
        mine.Status = "Approved";
        var otherDiscipline = NewDoc(1, disciplineId: 6, createdDate: DateTime.Now);
        otherDiscipline.Status = "Approved";
        ctx.EngineeringDocuments.Add(mine);
        ctx.EngineeringDocuments.Add(otherDiscipline);
        await ctx.SaveChangesAsync();

        var counts = await repo.GetSummaryCountsAsync(restrictToDiscipline: true, disciplineId: 5);

        Assert.Equal(1, counts.Total);
        Assert.Equal(1, counts.Approved);
    }
}
