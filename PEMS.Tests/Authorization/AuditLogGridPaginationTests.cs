using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6B — P2: Deterministic Ordering (Admin/AuditLog)». قبلاً فقط
/// OrderByDescending(Timestamp) بود، بدون ThenBy(Id). AuditLogController فقط به
/// PEMSDbContext وابسته است - مستقیم Instantiate می‌شود، بدون نیاز به Mock.
/// </summary>
public class AuditLogGridPaginationTests
{
    private static AuditLogController CreateController(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"AuditLogGrid_{dbName}");
        return new AuditLogController(ctx);
    }

    private static int _nextId = 1;

    private static AuditLog NewLog(DateTime timestamp, string entityName = "Equipment") => new()
    {
        AuditLogId = _nextId++,
        EntityName = entityName,
        Action = "Update",
        Timestamp = timestamp
    };

    [Fact]
    public async Task Index_DeterministicOrdering_SameTimestamp_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_DeterministicOrdering_SameTimestamp_SameOrderTwice), out var ctx);

        var sameTimestamp = DateTime.Now;
        for (var i = 0; i < 10; i++)
            ctx.AuditLogs.Add(NewLog(sameTimestamp));
        await ctx.SaveChangesAsync();

        var first = Assert.IsType<PagedResult<AuditLog>>(((Microsoft.AspNetCore.Mvc.ViewResult)await controller.Index(null, null, page: 1, pageSize: 20)).Model);
        var second = Assert.IsType<PagedResult<AuditLog>>(((Microsoft.AspNetCore.Mvc.ViewResult)await controller.Index(null, null, page: 1, pageSize: 20)).Model);

        Assert.Equal(first.Items.Select(x => x.AuditLogId), second.Items.Select(x => x.AuditLogId));
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var controller = CreateController(nameof(Index_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.AuditLogs.Add(NewLog(DateTime.Now.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var view = (Microsoft.AspNetCore.Mvc.ViewResult)await controller.Index(null, null, page: 1, pageSize: null);
        var result = Assert.IsType<PagedResult<AuditLog>>(view.Model);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var controller = CreateController(nameof(Index_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.AuditLogs.Add(NewLog(DateTime.Now.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        // طبق ControllerBase.View(model): ViewData روی خودِ Controller Instance یک شیء
        // مشترک است - اگر Model را از دو ViewResult بعد از هر دو فراخوانی بخوانیم، هر دو به
        // همون ViewData مشترک اشاره می‌کنند (Model دومی جای اولی را می‌گیرد). باید بلافاصله
        // بعد از هر Await، Model را استخراج کرد - نه این‌که ViewResult را نگه داشت.
        var page1 = Assert.IsType<PagedResult<AuditLog>>(((Microsoft.AspNetCore.Mvc.ViewResult)await controller.Index(null, null, page: 1, pageSize: 20)).Model);
        var page2 = Assert.IsType<PagedResult<AuditLog>>(((Microsoft.AspNetCore.Mvc.ViewResult)await controller.Index(null, null, page: 2, pageSize: 20)).Model);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.AuditLogId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }
}
