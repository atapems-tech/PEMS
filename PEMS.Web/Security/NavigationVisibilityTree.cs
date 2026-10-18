namespace PEMS.Web.Security;

/// <summary>
/// طبق «PEMS Phase 6 — Hierarchical Role/Discipline Visibility Correction»: قانون مطلق سند -
/// Navigation Node ≠ Permission ≠ Role ≠ Discipline. یک Permission هرگز نباید همشأن (Sibling) یک
/// آیتم ناوبری باشد؛ نقش/دیسیپلین همیشه Context زیرِ یک گره‌ی ناوبری واقعی‌اند، نه خودِ گره.
///
/// این فایل فقط شکل داده‌ی درختی صفحات مدیریت Visibility را تعریف می‌کند - هیچ تصمیم Visibility/
/// Authorization تازه‌ای این‌جا گرفته نمی‌شود (آن مسئولیت هنوز فقط مال NavigationVisibilityResolver/
/// AccessControlService است؛ این کلاس‌ها فقط خروجی DescribeForRoleAsync را Pivot/گروه‌بندی می‌کنند).
/// </summary>
public class NavigationRoleAccess
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool DefaultVisible { get; set; }
    public bool? OverrideVisible { get; set; }
    public bool EffectiveVisible { get; set; }
    public bool HasOverride => OverrideVisible.HasValue;
}

/// <summary>
/// یک گره‌ی ناوبری واقعی (آیتم CmmsIntegration، یا - وقتی زیرگروه/آیتمی در کار نیست - خودِ ماژول).
/// طبق بند ۷ سند اصلاحی: هویت این گره فقط ModuleKey/ItemKey است - هرگز PermissionId/ResourceName/ActionName.
/// </summary>
public class NavigationItemNode
{
    public string ItemKey { get; set; } = string.Empty;
    public string DisplayLocalizationKey { get; set; } = string.Empty;

    /// <summary>Metadata فقط-نمایشی، نه یک گره‌ی جدا - طبق بند ۱۳ سند اصلاحی. null یعنی این آیتم امروز هیچ دروازه‌ی مجوز مستقلی ندارد.</summary>
    public string? Capability { get; set; }

    public List<NavigationRoleAccess> RoleAccess { get; set; } = new();

    public bool AnyRoleVisible => RoleAccess.Any(r => r.EffectiveVisible);
}

public class NavigationSubgroupNode
{
    public string Label { get; set; } = string.Empty;
    public List<NavigationItemNode> Items { get; set; } = new();
    public bool AnyRoleVisible => Items.Any(i => i.AnyRoleVisible);
}

public class NavigationModuleNode
{
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayLocalizationKey { get; set; } = string.Empty;

    /// <summary>برای ماژول‌های بدون Drill-down سطح-آیتم (همه به‌جز CmmsIntegration) - دسترسی مستقیماً در سطح ماژول.</summary>
    public List<NavigationRoleAccess> RoleAccess { get; set; } = new();

    /// <summary>فقط CmmsIntegration پر می‌شود (طبق تصمیم Scope فاز ۶ - بدون تغییر).</summary>
    public List<NavigationItemNode> Items { get; set; } = new();
    public List<NavigationSubgroupNode> Subgroups { get; set; } = new();

    public bool HasDrillDown => Items.Count > 0 || Subgroups.Count > 0;
}

/// <summary>یک گره‌ی ناوبری برای صفحه‌ی تک‌کاربر (EffectiveUser/Preview As User) - بدون شبکه‌ی چندنقشی، فقط یک مقدار Visible نهایی.</summary>
public class NavigationUserItemNode
{
    public string ItemKey { get; set; } = string.Empty;
    public string DisplayLocalizationKey { get; set; } = string.Empty;
    public string? Capability { get; set; }
    public bool Visible { get; set; }
}

public class NavigationUserSubgroupNode
{
    public string Label { get; set; } = string.Empty;
    public List<NavigationUserItemNode> Items { get; set; } = new();
    public bool AnyVisible => Items.Any(i => i.Visible);
}

public class NavigationUserModuleNode
{
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayLocalizationKey { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public List<NavigationUserItemNode> Items { get; set; } = new();
    public List<NavigationUserSubgroupNode> Subgroups { get; set; } = new();
    public bool HasDrillDown => Items.Count > 0 || Subgroups.Count > 0;
}

/// <summary>
/// طبق بند ۶ سند اصلاحی: درخت همیشه از روی NavigationCatalog (که خودش عیناً همان سلسله‌مراتب
/// _SidebarMenu.cshtml را بازتاب می‌دهد) ساخته می‌شود، هرگز از روی فهرست Permission.
/// </summary>
public static class NavigationVisibilityTreeBuilder
{
    /// <summary>برای EffectiveUser/Preview As User - از روی همان NavigationVisibilitySnapshot که _SidebarMenu.cshtml واقعاً می‌خواند.</summary>
    public static List<NavigationUserModuleNode> BuildUserTree(NavigationVisibilitySnapshot snapshot)
    {
        var modules = new List<NavigationUserModuleNode>();

        foreach (var moduleKey in NavigationCatalog.ModuleKeys)
        {
            var moduleNode = new NavigationUserModuleNode
            {
                ModuleKey = moduleKey,
                DisplayLocalizationKey = NavigationCatalog.ModuleDisplayLocalizationKey[moduleKey],
                Visible = snapshot.IsModuleVisible(moduleKey),
            };

            if (moduleKey == "CmmsIntegration" && moduleNode.Visible)
            {
                var subgroupEmitted = false;
                foreach (var itemKey in NavigationCatalog.CmmsIntegrationItemOrder)
                {
                    var inSubgroup = NavigationCatalog.CmmsMaintenancePlanningSubgroupItemKeys.Contains(itemKey);
                    if (inSubgroup)
                    {
                        if (subgroupEmitted) continue;
                        subgroupEmitted = true;

                        var subgroup = new NavigationUserSubgroupNode { Label = "Maintenance Planning" };
                        foreach (var subItemKey in NavigationCatalog.CmmsMaintenancePlanningSubgroupItemKeys)
                            subgroup.Items.Add(BuildUserItemNode(subItemKey, snapshot));

                        moduleNode.Subgroups.Add(subgroup);
                        continue;
                    }

                    moduleNode.Items.Add(BuildUserItemNode(itemKey, snapshot));
                }
            }

            modules.Add(moduleNode);
        }

        return modules;
    }

    private static NavigationUserItemNode BuildUserItemNode(string itemKey, NavigationVisibilitySnapshot snapshot)
    {
        NavigationCatalog.CmmsIntegrationItemResourceMap.TryGetValue(itemKey, out var resource);
        return new NavigationUserItemNode
        {
            ItemKey = itemKey,
            DisplayLocalizationKey = NavigationCatalog.CmmsItemDisplayLocalizationKey.TryGetValue(itemKey, out var label) ? label : itemKey,
            Capability = resource == null ? null : $"{resource}.View",
            Visible = snapshot.IsCmmsItemVisible(itemKey),
        };
    }

    public static List<NavigationModuleNode> BuildTree(
        IReadOnlyList<(int RoleId, string RoleName)> roles,
        IReadOnlyDictionary<string, List<NavigationVisibilityRowDescription>> rowsByRole)
    {
        var modules = new List<NavigationModuleNode>();

        foreach (var moduleKey in NavigationCatalog.ModuleKeys)
        {
            var moduleNode = new NavigationModuleNode
            {
                ModuleKey = moduleKey,
                DisplayLocalizationKey = NavigationCatalog.ModuleDisplayLocalizationKey[moduleKey],
            };

            if (moduleKey == "CmmsIntegration")
            {
                var subgroupEmitted = false;
                foreach (var itemKey in NavigationCatalog.CmmsIntegrationItemOrder)
                {
                    var inSubgroup = NavigationCatalog.CmmsMaintenancePlanningSubgroupItemKeys.Contains(itemKey);
                    if (inSubgroup)
                    {
                        if (subgroupEmitted) continue;
                        subgroupEmitted = true;

                        var subgroup = new NavigationSubgroupNode { Label = "Maintenance Planning" };
                        foreach (var subItemKey in NavigationCatalog.CmmsMaintenancePlanningSubgroupItemKeys)
                            subgroup.Items.Add(BuildItemNode(subItemKey, roles, rowsByRole));

                        moduleNode.Subgroups.Add(subgroup);
                        continue;
                    }

                    moduleNode.Items.Add(BuildItemNode(itemKey, roles, rowsByRole));
                }
            }
            else
            {
                moduleNode.RoleAccess = roles
                    .Select(r => BuildRoleAccess(r, rowsByRole, moduleKey, itemKey: null))
                    .ToList();
            }

            modules.Add(moduleNode);
        }

        return modules;
    }

    private static NavigationItemNode BuildItemNode(
        string itemKey,
        IReadOnlyList<(int RoleId, string RoleName)> roles,
        IReadOnlyDictionary<string, List<NavigationVisibilityRowDescription>> rowsByRole)
    {
        NavigationCatalog.CmmsIntegrationItemResourceMap.TryGetValue(itemKey, out var resource);

        return new NavigationItemNode
        {
            ItemKey = itemKey,
            DisplayLocalizationKey = NavigationCatalog.CmmsItemDisplayLocalizationKey.TryGetValue(itemKey, out var label) ? label : itemKey,
            Capability = resource == null ? null : $"{resource}.View",
            RoleAccess = roles.Select(r => BuildRoleAccess(r, rowsByRole, "CmmsIntegration", itemKey)).ToList(),
        };
    }

    private static NavigationRoleAccess BuildRoleAccess(
        (int RoleId, string RoleName) role,
        IReadOnlyDictionary<string, List<NavigationVisibilityRowDescription>> rowsByRole,
        string moduleKey,
        string? itemKey)
    {
        if (!rowsByRole.TryGetValue(role.RoleName, out var rows))
        {
            return new NavigationRoleAccess { RoleId = role.RoleId, RoleName = role.RoleName };
        }

        var row = rows.FirstOrDefault(r => r.ModuleKey == moduleKey && r.ItemKey == itemKey);
        if (row == null)
        {
            return new NavigationRoleAccess { RoleId = role.RoleId, RoleName = role.RoleName };
        }

        return new NavigationRoleAccess
        {
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            DefaultVisible = row.DefaultVisible,
            OverrideVisible = row.OverrideVisible,
            EffectiveVisible = row.EffectiveVisible,
        };
    }
}
