using PEMS.Application.DTOs;
using PEMS.Web.Models;

namespace PEMS.Web.Helpers;

/// <summary>
/// طبق «PEMS Help System — Phase 3»: تبدیل ساختار تخت Node/Edge (HelpVisualRenderDto) به یک
/// درخت آماده‌ی رندر (HelpNodeTreeItem) - برای Decision Tree/Entity Relationship. Recursion
/// واقعی این‌جا (C# ساده) اتفاق می‌افتد، نه در Razor.
/// </summary>
public static class HelpVisualTreeBuilder
{
    public static List<HelpNodeTreeItem> BuildRoots(HelpVisualRenderDto visual)
    {
        var childrenByParent = visual.Edges
            .Where(e => e.ToNodeId != null)
            .GroupBy(e => e.FromNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var hasIncoming = visual.Edges
            .Where(e => e.ToNodeId != null)
            .Select(e => e.ToNodeId!)
            .ToHashSet();

        var nodeById = visual.Nodes.ToDictionary(n => n.Id, n => n);

        var rootNodes = visual.Nodes.Where(n => !hasIncoming.Contains(n.Id)).ToList();
        if (rootNodes.Count == 0 && visual.Nodes.Count > 0) rootNodes.Add(visual.Nodes[0]);

        // طبق بخش ۱۵ سند («Invalid diagram definition rejection/fallback»): محافظت در برابر
        // چرخه‌ی تصادفی در داده‌ی دستیِ ادمین - هر گره حداکثر یک‌بار در طول یک مسیر بازدید می‌شود
        HelpNodeTreeItem Build(HelpVisualRenderNode node, string? incomingLabel, bool isRoot, HashSet<string> visited)
        {
            var item = new HelpNodeTreeItem
            {
                Label = node.Label,
                IconCssClass = string.IsNullOrEmpty(node.IconCssClass) ? "bi-circle" : node.IconCssClass,
                IncomingEdgeLabel = incomingLabel,
                IsRoot = isRoot
            };

            if (!visited.Add(node.Id)) return item; // چرخه - این‌جا متوقف شو، فرزندی اضافه نکن

            if (childrenByParent.TryGetValue(node.Id, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (edge.ToNodeId == null || !nodeById.TryGetValue(edge.ToNodeId, out var childNode)) continue;
                    item.Children.Add(Build(childNode, edge.Label, false, visited));
                }
            }

            return item;
        }

        return rootNodes.Select(r => Build(r, null, true, new HashSet<string>())).ToList();
    }
}
