using PEMS.Application.DTOs;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Workflow;

/// <summary>
/// Phase 4 - چیدمان قطعی (Deterministic) گراف گردش‌کار برای Designer (SVG). موقعیت‌ها در دیتابیس ذخیره نمی‌شوند؛ هر بار از
/// مراحل/گذارهای واقعی نسخه محاسبه می‌شوند: لایه‌بندی بر اساس طولانی‌ترین مسیر روی یال‌های رو‌به‌جلو (یال‌های برگشتی با DFS جدا
/// می‌شوند)، ترتیب داخل هر لایه = StageOrder. یال‌های برگشتی از کنار گره رسم می‌شوند؛ گذارهای پایانی (مقصد null) به‌صورت نشانگر کنار گره.
/// </summary>
public static class WorkflowGraphLayout
{
    private const double NodeW = 190, NodeH = 50, GapX = 50, GapY = 84, Margin = 30, BackLane = 26;

    public static WorkflowGraphModel Compute(IReadOnlyList<WorkflowStage> stages, IReadOnlyList<WorkflowTransition> transitions)
    {
        var g = new WorkflowGraphModel();
        if (stages.Count == 0) { g.Width = 400; g.Height = 120; return g; }

        var ordered = stages.OrderBy(s => s.StageOrder).ThenBy(s => s.WorkflowStageId).ToList();
        var ids = ordered.Select(s => s.WorkflowStageId).ToHashSet();
        var fwdCandidates = transitions.Where(t => t.FromStageId.HasValue && t.ToStageId.HasValue && t.FromStageId != t.ToStageId
                                                   && ids.Contains(t.FromStageId!.Value) && ids.Contains(t.ToStageId!.Value)).ToList();
        var startIds = transitions.Where(t => !t.FromStageId.HasValue && t.ToStageId.HasValue && ids.Contains(t.ToStageId!.Value)).Select(t => t.ToStageId!.Value).Distinct().ToList();
        if (startIds.Count == 0) startIds.Add(ordered[0].WorkflowStageId);

        // DFS: یال‌های برگشتی (مقصد روی پشته‌ی جاری)
        var adj = fwdCandidates.GroupBy(t => t.FromStageId!.Value).ToDictionary(x => x.Key, x => x.OrderBy(t => t.Priority).ThenBy(t => t.WorkflowTransitionId).ToList());
        var state = new Dictionary<int, int>();   // 1 = روی پشته، 2 = تمام‌شده
        var back = new HashSet<int>();
        var post = new List<int>();
        void Dfs(int n)
        {
            state[n] = 1;
            if (adj.TryGetValue(n, out var outs))
                foreach (var t in outs)
                {
                    var to = t.ToStageId!.Value;
                    if (!state.TryGetValue(to, out var st)) Dfs(to);
                    else if (st == 1) back.Add(t.WorkflowTransitionId);
                }
            state[n] = 2; post.Add(n);
        }
        foreach (var s in startIds) if (!state.ContainsKey(s)) Dfs(s);
        foreach (var s in ordered) if (!state.ContainsKey(s.WorkflowStageId)) Dfs(s.WorkflowStageId);

        var forward = fwdCandidates.Where(t => !back.Contains(t.WorkflowTransitionId)).ToList();
        var layer = ordered.ToDictionary(s => s.WorkflowStageId, _ => 0);
        var incoming = forward.GroupBy(t => t.ToStageId!.Value).ToDictionary(x => x.Key, x => x.Select(t => t.FromStageId!.Value).ToList());
        var topo = Enumerable.Reverse(post).ToList();
        foreach (var n in topo)
            if (incoming.TryGetValue(n, out var ins) && ins.Count > 0)
                layer[n] = ins.Max(f => layer[f]) + 1;
        // مراحل بدون اتصال ورودی/شروع بعد از آخرین لایه‌ی متصل
        var startLayerless = ordered.Where(s => !startIds.Contains(s.WorkflowStageId) && !incoming.ContainsKey(s.WorkflowStageId)).ToList();
        var maxLayer = layer.Values.Max();
        foreach (var s in startLayerless) layer[s.WorkflowStageId] = maxLayer + 1;

        var byLayer = ordered.GroupBy(s => layer[s.WorkflowStageId]).OrderBy(x => x.Key).ToList();
        var maxCount = byLayer.Max(x => x.Count());
        var backLanes = back.Count;
        var contentW = maxCount * NodeW + (maxCount - 1) * GapX;
        var totalW = Margin * 2 + contentW + (backLanes > 0 ? (backLanes + 1) * BackLane + 60 : 60);
        var nodes = new Dictionary<int, WorkflowGraphNode>();
        foreach (var grp in byLayer)
        {
            var list = grp.ToList();
            var rowW = list.Count * NodeW + (list.Count - 1) * GapX;
            var x0 = Margin + (contentW - rowW) / 2;
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                var n = new WorkflowGraphNode
                {
                    StageId = s.WorkflowStageId, Name = s.Name, NameFa = s.NameFa, BusinessStatus = s.BusinessStatus, Order = s.StageOrder,
                    Layer = grp.Key, X = x0 + i * (NodeW + GapX), Y = Margin + 24 + grp.Key * (NodeH + GapY), W = NodeW, H = NodeH,
                    IsStart = startIds.Contains(s.WorkflowStageId)
                };
                nodes[s.WorkflowStageId] = n;
            }
        }
        g.Nodes = nodes.Values.OrderBy(n => n.Order).ToList();
        var layers = byLayer.Count;
        g.Width = totalW;
        g.Height = Margin * 2 + 24 + layers * NodeH + (layers - 1) * GapY + 40;

        // یال‌ها
        var pairCount = new Dictionary<(int, int), int>();
        var terminalCount = new Dictionary<int, int>();
        var backIndex = 0;
        var rightEdge = Margin + contentW;
        foreach (var t in transitions.OrderBy(t => t.Priority).ThenBy(t => t.WorkflowTransitionId))
        {
            var e = new WorkflowGraphEdge
            {
                TransitionId = t.WorkflowTransitionId, FromStageId = t.FromStageId, ToStageId = t.ToStageId, ActionType = t.ActionType,
                Label = t.Label, LabelFa = t.LabelFa, RequiredPermission = t.RequiredPermission
            };
            if (!t.FromStageId.HasValue && t.ToStageId.HasValue && nodes.TryGetValue(t.ToStageId.Value, out var st))
            {
                e.Kind = "Start"; e.Path = $"M {F(st.X + st.W / 2)} {F(st.Y - 22)} L {F(st.X + st.W / 2)} {F(st.Y)}";
                e.LabelX = st.X + st.W / 2 + 6; e.LabelY = st.Y - 10;
            }
            else if (t.FromStageId.HasValue && !t.ToStageId.HasValue && nodes.TryGetValue(t.FromStageId.Value, out var fn))
            {
                terminalCount.TryGetValue(fn.StageId, out var k); terminalCount[fn.StageId] = k + 1;
                fn.HasTerminal = true;
                var sx = fn.X + fn.W; var sy = fn.Y + 12 + k * 14;
                e.Kind = "Terminal"; e.Path = $"M {F(sx)} {F(sy)} L {F(sx + 22)} {F(sy)}";
                e.LabelX = sx + 26; e.LabelY = sy + 4;
            }
            else if (t.FromStageId.HasValue && t.ToStageId.HasValue && nodes.TryGetValue(t.FromStageId.Value, out var a) && nodes.TryGetValue(t.ToStageId.Value, out var b))
            {
                if (t.FromStageId == t.ToStageId)
                {
                    e.Kind = "Back"; var sx = a.X + a.W; var sy = a.Y + a.H - 10;
                    e.Path = $"M {F(sx)} {F(sy)} C {F(sx + 40)} {F(sy + 10)}, {F(sx + 40)} {F(sy - 24)}, {F(sx)} {F(sy - 24)}";
                    e.LabelX = sx + 42; e.LabelY = sy - 6;
                }
                else if (back.Contains(t.WorkflowTransitionId))
                {
                    e.Kind = "Back"; backIndex++;
                    var lane = rightEdge + 30 + backIndex * BackLane;
                    var sx = a.X + a.W; var sy = a.Y + a.H / 2; var tx = b.X + b.W; var ty = b.Y + b.H / 2;
                    e.Path = $"M {F(sx)} {F(sy)} C {F(lane)} {F(sy)}, {F(lane)} {F(ty)}, {F(tx)} {F(ty)}";
                    e.LabelX = lane - 4; e.LabelY = (sy + ty) / 2;
                }
                else
                {
                    e.Kind = "Forward";
                    pairCount.TryGetValue((a.StageId, b.StageId), out var k); pairCount[(a.StageId, b.StageId)] = k + 1;
                    var off = k * 16;
                    var sx = a.X + a.W / 2 + off; var sy = a.Y + a.H; var tx = b.X + b.W / 2 + off; var ty = b.Y;
                    var mid = (sy + ty) / 2;
                    e.Path = $"M {F(sx)} {F(sy)} C {F(sx)} {F(mid)}, {F(tx)} {F(mid)}, {F(tx)} {F(ty)}";
                    e.LabelX = (sx + tx) / 2 + 6; e.LabelY = mid;
                }
            }
            else continue;
            g.Edges.Add(e);
        }
        if (backIndex > 0) g.Width = Math.Max(g.Width, rightEdge + 30 + (backIndex + 1) * BackLane + 60);
        return g;
    }

    private static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
