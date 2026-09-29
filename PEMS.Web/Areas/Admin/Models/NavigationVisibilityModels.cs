using PEMS.Web.Security;

namespace PEMS.Web.Areas.Admin.Models;

// طبق سند «PEMS Phase 6 — Hierarchical Role/Discipline Visibility Correction»: این مدل‌ها فقط
// برای نمایش‌اند - هیچ تصمیم Visibility/Authorization این‌جا محاسبه نمی‌شود (همه از
// NavigationVisibilityResolver/NavigationVisibilityTreeBuilder خوانده می‌شود). صفحه‌ی Index خودش
// دیگر مدل جداگانه ندارد - مستقیماً List<NavigationModuleNode> (شکل درختیِ ناوبری‌محور) را View
// می‌کند، دقیقاً طبق قانون مطلق سند: Navigation Node ≠ Permission ≠ Role ≠ Discipline.

public class NavigationVisibilityUserVm
{
    public List<(int Id, string Display)> AllUsers { get; set; } = new();
    public int? SelectedUserId { get; set; }
    public List<string> RoleNames { get; set; } = new();

    // طبق بند «Preview As User»: فقط نمایش - هرگز Session/Identity کاربر جاری را تغییر نمی‌دهد
    public List<NavigationUserModuleNode> Tree { get; set; } = new();
}
