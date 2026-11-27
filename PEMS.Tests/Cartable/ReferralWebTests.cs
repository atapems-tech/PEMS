using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Tests.Web;
using PEMS.Web.Controllers;
using PEMS.Web.Navigation;
using PEMS.Web.Security;

namespace PEMS.Tests.Cartable;

/// <summary>صفحه‌ی نوشتن ارجاع، Details (گردش پیام‌ها، دکمه‌های پاسخ/ارجاع)، اختیار تاییدکننده‌ی تعیین‌شده، و حذف «برچسب‌ها» از منو و هدر سراسری صفحه.</summary>
public class ReferralWebTests
{
    private sealed class User : ICurrentUserService
    {
        public int? UserId { get; init; }
        public string? UserName => "u" + (UserId - 100);
        public string? FullName => "User";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; init; } = new[] { "Staff" };
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Environment.GetEnvironmentVariable("PEMS_REPO_ROOT") ?? FindRoot(), "PEMS.Web" }.Concat(parts).ToArray()));
    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PEMS.Web"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found; set PEMS_REPO_ROOT");
    }

    [Fact]
    public async Task TheCurrentChainApprover_CanApproveWithoutTheModulePermission_OthersCannot()
    {
        var (asApprover, ctx) = Operations(nameof(TheCurrentChainApprover_CanApproveWithoutTheModulePermission_OthersCannot), userId: 102);
        PendingLetter(ctx, 102, 103);

        // the second approver's turn has not come: not allowed yet
        var (notYet, _) = Operations("later", 103);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => notYet.ApproveAsync(900, null));

        var ok = await asApprover.ApproveAsync(900, "fine");
        Assert.True(ok.Success);
        Assert.Equal("Approved", ctx.LetterApprovalSteps.Single(s => s.ApproverUserId == 102).Status);
    }

    [Fact]
    public async Task SomeoneOutsideTheChain_IsForbiddenFromApproveReturnAndReject()
    {
        var (c, ctx) = Operations(nameof(SomeoneOutsideTheChain_IsForbiddenFromApproveReturnAndReject), userId: 105);
        PendingLetter(ctx, 102);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.ApproveAsync(900, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.ReturnForRevisionAsync(900, "no"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.RejectAsync(900, "no"));
        Assert.Equal("Pending", ctx.LetterApprovalSteps.Single().Status);
        Assert.Equal("PendingApproval", ctx.Letters.Single().Status);
    }

    [Fact]
    public async Task AnApproverWithTheModulePermission_StillWorks()
    {
        var (c, ctx) = Operations(nameof(AnApproverWithTheModulePermission_StillWorks), userId: 102, allowAll: true);
        PendingLetter(ctx, 102);
        Assert.True((await c.ReturnForRevisionAsync(900, "please fix")).Success);
        Assert.Equal("ReturnedForRevision", ctx.Letters.Single().Status);
    }

    // ------------------------------------------------------------------ sidebar + global header
    [Fact]
    public void TagsLink_IsGoneFromBothSidebars_AndItsStoredRowIsRetiredNotDeleted()
    {
        Assert.DoesNotContain(NavigationMenuDefaults.All, d => d.Key == "proj.tags");
        Assert.Contains("proj.tags", NavigationMenuDefaults.RetiredKeys);
        Assert.DoesNotContain("asp-controller=\"Tag\"", Web("Views", "Shared", "_SidebarMenu.cshtml"));
    }

    [Fact]
    public async Task SyncDefaults_DeactivatesRetiredKeys_WithoutDeletingThem_AndIsIdempotent()
    {
        var ctx = PemsDbContextFactory.CreateInMemory("RetiredNav_" + Guid.NewGuid());
        var menu = new NavigationMenuService(ctx, new MemoryCache(new MemoryCacheOptions()));
        ctx.NavigationMenuItems.Add(new NavigationMenuItem { Key = "proj.tags", Kind = "Link", TitleKey = "Tags", Icon = "bi-tags", Controller = "Tag", Action = "Index", IsActive = true, IsSystem = true });
        ctx.NavigationMenuItems.Add(new NavigationMenuItem { Key = "custom.tags", Kind = "Link", TitleKey = "Mine", Icon = "bi-tags", IsActive = true, IsSystem = false });
        await ctx.SaveChangesAsync();

        await menu.SyncDefaultsAsync(NavigationMenuDefaults.All, default, NavigationMenuDefaults.RetiredKeys);
        Assert.False(ctx.NavigationMenuItems.Single(x => x.Key == "proj.tags").IsActive);
        Assert.True(ctx.NavigationMenuItems.Single(x => x.Key == "custom.tags").IsActive);   // custom rows are never touched

        var count = ctx.NavigationMenuItems.Count();
        Assert.Equal(0, await menu.SyncDefaultsAsync(NavigationMenuDefaults.All, default, NavigationMenuDefaults.RetiredKeys));
        Assert.Equal(count, ctx.NavigationMenuItems.Count());
    }

    [Fact]
    public void TheGlobalLayout_RendersNoPageHeader()
    {
        var layout = Web("Views", "Shared", "_Layout.cshtml");
        Assert.DoesNotContain("_PageHeader", layout);
        Assert.DoesNotContain("pems-page-head", layout);
    }

    // ------------------------------------------------------------------ the big frame with the fixed action bar
    [Fact]
    public void Cartable_FillsTheWindow_AndKeepsTheActionBarAtTheBottomAlways()
    {
        var css = Web("wwwroot", "css", "pems-cartable.css");
        Assert.Contains(".ct-shell { height: calc(100dvh", css);
        Assert.Matches(@"\.ct-shell \.lp-tablewrap \.lp-bulkbar \{ order: 3; position: static; display: flex;", css);   // always shown, never overlaying the header
        Assert.Contains(".lp-tablewrap > form { display: contents; }", css);
        var view = Web("Views", "Letter", "Index.cshtml");
        Assert.Contains("data-empty=\"@Localizer[\"Ct_NoSelection\"]\"", view);
    }
}
