using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق ممیزی مقیاس‌پذیری (P0 - Document Register Remediation): تست‌های GetRegisterPagedAsync/
/// GetRegisterAllMatchingAsync/GetRegisterByIdsAsync/GetRegisterFilterOptionsAsync - جایگزین
/// GetAllAsync قدیمی (بدون Pagination) که DocumentRegisterController استفاده می‌کرد. تمرکز روی
/// سه ریسک اصلی این فاز: (۱) حفظ دقیق رفتار فعلی محدودیت دیسیپلین/فیلترها/سطل "(Unassigned)"
/// حین انتقال از حافظه به SQL، (۲) صحت DelayDays که یک Property [NotMapped] است و فرمولش این‌جا
/// عمداً کپی شده (نه فراخوانی)، (۳) واقعاً در SQL اجرا شدن Pagination (نه بارگذاری کامل + Skip
/// در حافظه) - اثبات‌شده با TotalCount مستقل از Items.Count و صفحات غیرهم‌پوشان.
/// </summary>
public class DocumentRegisterQueryTests
{
    private static EngineeringDocumentRepository CreateRepo(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory(dbName);

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Projects.Add(new Project { ProjectId = 2, ProjectCode = "TP2", ProjectName = "Other Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 5, Code = "MECH", Name = "Mechanical" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 6, Code = "ELEC", Name = "Electrical" });
        ctx.SaveChanges();

        return new EngineeringDocumentRepository(ctx);
    }

    private static EngineeringDocument NewDoc(
        int projectId, int? disciplineId, DateTime createdDate, string title = "Doc", string status = "Draft") => new()
    {
        DocumentNo = $"TP-DAT-{Guid.NewGuid():N}".Substring(0, 20),
        Title = title,
        DocumentType = "Datasheet",
        ProjectId = projectId,
        DisciplineId = disciplineId,
        Status = status,
        IsActive = true,
        CreatedDate = createdDate
    };

    [Fact]
    public async Task GetRegisterPagedAsync_RestrictedEmployeeWithNoDiscipline_SeesOnlyNullDisciplineDocs()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_RestrictedEmployeeWithNoDiscipline_SeesOnlyNullDisciplineDocs), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mechanical doc"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now, title: "Unassigned doc"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), restrictToDiscipline: true, disciplineId: null);

        Assert.Single(result.Items);
        Assert.Equal("Unassigned doc", result.Items[0].Title);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_RestrictedEmployeeWithDiscipline_SeesOwnAndNullDisciplineDocs_NotOthers()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_RestrictedEmployeeWithDiscipline_SeesOwnAndNullDisciplineDocs_NotOthers), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mine"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 6, createdDate: DateTime.Now, title: "Other discipline"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now, title: "Unassigned"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), restrictToDiscipline: true, disciplineId: 5);

        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, x => x.Title == "Other discipline");
    }

    [Fact]
    public async Task GetRegisterPagedAsync_Unrestricted_SeesEverything_TotalCountMatchesItemCount()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_Unrestricted_SeesEverything_TotalCountMatchesItemCount), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 6, createdDate: DateTime.Now));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), restrictToDiscipline: false, disciplineId: null);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    // اثبات این‌که Pagination واقعاً در SQL اجرا می‌شود (Skip/Take)، نه بارگذاری کامل و برش در
    // حافظه: TotalCount باید همیشه کل تعداد منطبق را نشان دهد، صرف‌نظر از این‌که فقط یک صفحه
    // کوچک از آن واکشی شده - و صفحات نباید هم‌پوشانی/شکاف داشته باشند
    [Fact]
    public async Task GetRegisterPagedAsync_ReturnsStableDisjointPages_TotalCountIndependentOfPageSize()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_ReturnsStableDisjointPages_TotalCountIndependentOfPageSize), out var ctx);

        // طبق سند Global Data Grid Standard بند ۴: فقط ۲۰/۵۰/۱۰۰ اندازه‌ی صفحه‌ی مجاز است - این
        // تست حالا با PageSize=20 (نه یک عدد دلخواه مثل ۵ که دیگر معتبر نیست و به ۵۰ برمی‌گردد)
        // و ۲۵ سند (۲۰ + ۵) پایداری/عدم هم‌پوشانی صفحات را اثبات می‌کند
        for (var i = 0; i < 25; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now.AddMinutes(-i), title: $"Doc {i}"));
        await ctx.SaveChangesAsync();

        var page1 = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Page = 1, PageSize = 20 }, false, null);
        var page2 = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Page = 2, PageSize = 20 }, false, null);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.DocumentId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    // طبق «PEMS Phase 6B — P2: Deterministic Ordering». قبلاً فقط OrderByDescending(CreatedDate)
    // بود، بدون ThenBy(Id) - وقتی چند سند دقیقاً همون CreatedDate را دارند (مثلاً Import دسته‌ای)
    // ترتیب بین دو اجرای پی‌درپی می‌تواند فرق کند. حالا ThenByDescending(DocumentId) این را تضمین می‌کند.
    [Fact]
    public async Task GetRegisterPagedAsync_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_DeterministicOrdering_SameCreatedDate_SameOrderTwice), out var ctx);

        var sameDate = DateTime.Now;
        for (var i = 0; i < 10; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, null, sameDate, $"Doc {i}"));
        await ctx.SaveChangesAsync();

        var first = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Page = 1, PageSize = 20 }, false, null);
        var second = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Page = 1, PageSize = 20 }, false, null);

        Assert.Equal(first.Items.Select(x => x.DocumentId), second.Items.Select(x => x.DocumentId));
    }

    [Fact]
    public async Task GetRegisterPagedAsync_InvalidPageSize_FallsBackToDefault()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_InvalidPageSize_FallsBackToDefault), out var ctx);

        for (var i = 0; i < 5; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, $"Doc {i}"));
        await ctx.SaveChangesAsync();

        // طبق سند Global Data Grid Standard بند ۴: هر مقدار خارج از {۲۰, ۵۰, ۱۰۰} - از جمله
        // یک عدد خیلی بزرگ دستکاری‌شده در QueryString - باید بی‌صدا به پیش‌فرض امن ۵۰ برگردد،
        // نه یک سقف بالاتر (۲۰۰) مثل رفتار قدیمی DocumentRegisterFilter.MaxPageSize
        var result = await repo.GetRegisterPagedAsync(
            new DocumentRegisterFilter { PageSize = 999999 }, false, null);

        Assert.Equal(PEMS.Application.Common.DataGridPageSize.Default, result.PageSize);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_ProjectNameFilter_ExactMatchOnly()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_ProjectNameFilter_ExactMatchOnly), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, "In Test Project"));
        ctx.EngineeringDocuments.Add(NewDoc(2, null, DateTime.Now, "In Other Project"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(
            new DocumentRegisterFilter { ProjectName = "Test Project" }, false, null);

        Assert.Single(result.Items);
        Assert.Equal("In Test Project", result.Items[0].Title);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_MultiDisciplineFilter_IncludesUnassignedBucket()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_MultiDisciplineFilter_IncludesUnassignedBucket), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mech"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 6, createdDate: DateTime.Now, title: "Elec"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: null, createdDate: DateTime.Now, title: "None"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(
            new DocumentRegisterFilter { Disciplines = new List<string> { "Mechanical", DocumentRegisterFilter.UnassignedDisciplineKey } },
            false, null);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, x => x.Title == "Mech");
        Assert.Contains(result.Items, x => x.Title == "None");
        Assert.DoesNotContain(result.Items, x => x.Title == "Elec");
    }

    [Fact]
    public async Task GetRegisterPagedAsync_StatusFilter_ExactMatch()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_StatusFilter_ExactMatch), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, "Approved doc", status: "Approved"));
        ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, "Draft doc", status: "Draft"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Status = "Approved" }, false, null);

        Assert.Single(result.Items);
        Assert.Equal("Approved doc", result.Items[0].Title);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_SearchFilter_MatchesDocumentNoOrTitle()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_SearchFilter_MatchesDocumentNoOrTitle), out var ctx);

        var target = NewDoc(1, null, DateTime.Now, title: "Pressure Vessel Datasheet");
        ctx.EngineeringDocuments.Add(target);
        ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now, title: "Unrelated"));
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter { Search = "Pressure Vessel" }, false, null);

        Assert.Single(result.Items);
        Assert.Equal(target.DocumentId, result.Items[0].DocumentId);
    }

    // DelayDays روی Document.cs یک Property [NotMapped] است با منطق: اگر PlannedDate نداریم
    // null؛ وگرنه reference = ActualDate یا (اگر PlannedDate گذشته Now باشد) Now؛ اگر reference
    // هم null بود، null؛ وگرنه تفاضل روز. فرمول این‌جا عمداً کپی شده (چون EF نمی‌تواند Getter را
    // ترجمه کند) - این تست دقیقاً اثبات می‌کند کپی درست است، نه یک فرمول ساده‌شده‌ی متفاوت.
    [Fact]
    public async Task GetRegisterPagedAsync_DelayDays_MatchesDocumentEntityFormula_PlannedInPastNoActual()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_DelayDays_MatchesDocumentEntityFormula_PlannedInPastNoActual), out var ctx);

        var doc = NewDoc(1, null, DateTime.Now, "Delayed doc");
        doc.PlannedDate = DateTime.Now.AddDays(-10);
        doc.ActualDate = null;
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), false, null);

        var expected = doc.DelayDays; // فرمول واقعی روی خودِ Entity (Getter اصلی، نه کپی)
        Assert.NotNull(expected);
        Assert.Equal(expected, result.Items[0].DelayDays);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_DelayDays_NullWhenPlannedDateInFutureAndNoActual()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_DelayDays_NullWhenPlannedDateInFutureAndNoActual), out var ctx);

        var doc = NewDoc(1, null, DateTime.Now, "Future planned doc");
        doc.PlannedDate = DateTime.Now.AddDays(10);
        doc.ActualDate = null;
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), false, null);

        Assert.Null(doc.DelayDays);
        Assert.Null(result.Items[0].DelayDays);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_DelayDays_NullWhenNoPlannedDate()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_DelayDays_NullWhenNoPlannedDate), out var ctx);

        var doc = NewDoc(1, null, DateTime.Now, "No planned date");
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), false, null);

        Assert.Null(result.Items[0].DelayDays);
    }

    [Fact]
    public async Task GetRegisterPagedAsync_DelayDays_UsesActualDateWhenSet_OnTimeCompletion()
    {
        var repo = CreateRepo(nameof(GetRegisterPagedAsync_DelayDays_UsesActualDateWhenSet_OnTimeCompletion), out var ctx);

        var doc = NewDoc(1, null, DateTime.Now, "Completed early");
        doc.PlannedDate = DateTime.Now.AddDays(10);
        doc.ActualDate = DateTime.Now.AddDays(5); // تکمیل زودتر از موعد، هنوز قبل از Planned
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var result = await repo.GetRegisterPagedAsync(new DocumentRegisterFilter(), false, null);

        var expected = doc.DelayDays;
        Assert.NotNull(expected);
        Assert.True(expected < 0); // زودتر از موعد = تاخیر منفی
        Assert.Equal(expected, result.Items[0].DelayDays);
    }

    [Fact]
    public async Task GetRegisterAllMatchingAsync_ReturnsAllMatchingRows_NotJustOnePage()
    {
        var repo = CreateRepo(nameof(GetRegisterAllMatchingAsync_ReturnsAllMatchingRows_NotJustOnePage), out var ctx);

        for (var i = 0; i < 30; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now.AddMinutes(-i), $"Doc {i}"));
        await ctx.SaveChangesAsync();

        var (items, totalMatching) = await repo.GetRegisterAllMatchingAsync(new DocumentRegisterFilter(), false, null, maxRows: 100);

        Assert.Equal(30, totalMatching);
        Assert.Equal(30, items.Count);
    }

    [Fact]
    public async Task GetRegisterAllMatchingAsync_ExceedsMaxRows_TotalMatchingStillReportsRealCount()
    {
        var repo = CreateRepo(nameof(GetRegisterAllMatchingAsync_ExceedsMaxRows_TotalMatchingStillReportsRealCount), out var ctx);

        for (var i = 0; i < 20; i++)
            ctx.EngineeringDocuments.Add(NewDoc(1, null, DateTime.Now.AddMinutes(-i), $"Doc {i}"));
        await ctx.SaveChangesAsync();

        // maxRows کوچک‌تر از تعداد واقعی - کنترلر باید بتواند تشخیص دهد و به کاربر پیام واضح بدهد
        var (items, totalMatching) = await repo.GetRegisterAllMatchingAsync(new DocumentRegisterFilter(), false, null, maxRows: 5);

        Assert.Equal(20, totalMatching);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public async Task GetRegisterByIdsAsync_RestrictedUser_CannotExportOutOfScopeDocumentById()
    {
        var repo = CreateRepo(nameof(GetRegisterByIdsAsync_RestrictedUser_CannotExportOutOfScopeDocumentById), out var ctx);

        var mine = NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, title: "Mine");
        var otherDiscipline = NewDoc(1, disciplineId: 6, createdDate: DateTime.Now, title: "Not mine");
        ctx.EngineeringDocuments.Add(mine);
        ctx.EngineeringDocuments.Add(otherDiscipline);
        await ctx.SaveChangesAsync();

        // کاربر محدود تلاش می‌کند با دستکاری ids، سند خارج از دیسیپلین خودش را هم بگیرد
        var result = await repo.GetRegisterByIdsAsync(
            new List<int> { mine.DocumentId, otherDiscipline.DocumentId },
            restrictToDiscipline: true, disciplineId: 5);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    [Fact]
    public async Task GetRegisterFilterOptionsAsync_ReturnsDistinctValues_IncludingUnassignedBucket()
    {
        var repo = CreateRepo(nameof(GetRegisterFilterOptionsAsync_ReturnsDistinctValues_IncludingUnassignedBucket), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, status: "Approved"));
        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, status: "Approved"));
        ctx.EngineeringDocuments.Add(NewDoc(2, disciplineId: null, createdDate: DateTime.Now, status: "Draft"));
        await ctx.SaveChangesAsync();

        var options = await repo.GetRegisterFilterOptionsAsync(false, null);

        Assert.Equal(new[] { "Other Project", "Test Project" }, options.Projects);
        Assert.Contains(DocumentRegisterFilter.UnassignedDisciplineKey, options.Disciplines);
        Assert.Contains("Mechanical", options.Disciplines);
        Assert.Equal(new[] { "Approved", "Draft" }, options.Statuses);
    }

    [Fact]
    public async Task GetRegisterFilterOptionsAsync_RestrictedUser_OnlySeesOwnScopeOptions()
    {
        var repo = CreateRepo(nameof(GetRegisterFilterOptionsAsync_RestrictedUser_OnlySeesOwnScopeOptions), out var ctx);

        ctx.EngineeringDocuments.Add(NewDoc(1, disciplineId: 5, createdDate: DateTime.Now, status: "Approved"));
        ctx.EngineeringDocuments.Add(NewDoc(2, disciplineId: 6, createdDate: DateTime.Now, status: "Rejected"));
        await ctx.SaveChangesAsync();

        var options = await repo.GetRegisterFilterOptionsAsync(restrictToDiscipline: true, disciplineId: 5);

        Assert.Single(options.Projects);
        Assert.Equal("Test Project", options.Projects[0]);
        Assert.Single(options.Statuses);
        Assert.Equal("Approved", options.Statuses[0]);
    }
}
