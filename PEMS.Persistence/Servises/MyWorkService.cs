using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.WorkflowPlatform.Dtos;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

public class MyWorkService : IMyWorkService
{
    private readonly IWorkflowRuntime _runtime;

    public MyWorkService(IWorkflowRuntime runtime) => _runtime = runtime;

    private static MyApprovalItemDto Map(PendingTaskDto t) => new()
    {
        WorkflowInstanceId = t.WorkflowInstanceId,
        WorkflowInstanceStageId = t.WorkflowInstanceStageId,
        Module = t.Module,
        BusinessObjectType = t.BusinessObjectType,
        BusinessObjectId = t.BusinessObjectId,
        StageName = t.StageName,
        StageNameFa = t.StageNameFa,
        AssignedDate = t.AssignedDate,
        DueDate = t.DueDate,
        IsOverdue = t.IsOverdue,
        IsPersonal = t.IsPersonal
    };

    private static MyApprovalsSummaryDto Summarize(IReadOnlyCollection<MyApprovalItemDto> items) => new()
    {
        Total = items.Count,
        Overdue = items.Count(x => x.IsOverdue),
        Personal = items.Count(x => x.IsPersonal),
        WithSla = items.Count(x => x.DueDate.HasValue),
        ByModule = items.GroupBy(x => x.Module).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count())
    };

    public async Task<MyApprovalsSummaryDto> GetSummaryAsync(int employeeId, IReadOnlyList<string> roles)
    {
        var tasks = await _runtime.GetPendingTasksAsync(employeeId, roles);
        return Summarize(tasks.Select(Map).ToList());
    }

    public async Task<MyApprovalsPageDto> GetApprovalsAsync(int employeeId, IReadOnlyList<string> roles, string? module, bool overdueOnly, bool personalOnly, int page, int pageSize)
    {
        // مرجع واحد: همان منبعی که موتور هنگام اقدام اعمال می‌کند (نقش / کاندیدای Snapshot / تفکیک وظایف)
        var all = (await _runtime.GetPendingTasksAsync(employeeId, roles)).Select(Map).ToList();
        var summary = Summarize(all);   // شمارنده‌ها همیشه کل کارتابل کاربر را نشان می‌دهند، نه فقط صفحه‌ی فیلترشده

        IEnumerable<MyApprovalItemDto> q = all;
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(x => string.Equals(x.Module, module, StringComparison.OrdinalIgnoreCase));
        if (overdueOnly) q = q.Where(x => x.IsOverdue);
        if (personalOnly) q = q.Where(x => x.IsPersonal);

        var ordered = q.OrderByDescending(x => x.IsOverdue)
            .ThenByDescending(x => x.IsPersonal)          // کار شخصی قبل از صف نقش (وگرنه زیر صدها کار صف گم می‌شود)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenBy(x => x.AssignedDate)
            .ThenBy(x => x.WorkflowInstanceStageId)
            .ToList();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new MyApprovalsPageDto
        {
            Page = new PagedResult<MyApprovalItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = ordered.Count },
            Summary = summary,
            Module = string.IsNullOrWhiteSpace(module) ? null : module,
            OverdueOnly = overdueOnly,
            PersonalOnly = personalOnly
        };
    }
}
