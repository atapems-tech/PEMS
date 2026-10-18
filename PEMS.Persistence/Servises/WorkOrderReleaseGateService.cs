using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۸/۵۳: هیچ Entity جدیدی این‌جا ساخته نشد - فقط سه تکه‌ی از قبل
/// موجود (JobPlan.PermitRequired/LOTORequired از فاز ۴، Permit/IsolationPoint/GasTest از
/// قبل، PermitExecutionRequirement از قبل) به هم وصل شدند. تنها مسیر فعلی برای رسیدن به
/// JobPlan از روی WorkOrder، از طریق MaintenancePlan است (WorkOrder های اصلاحی/اضطراری بدون
/// Plan، JobPlan ای برای بررسی ندارند و این گیت روی آن‌ها بدون اثر می‌ماند - دقیقاً طبق بند
/// ۱۸: «فقط شرایطی که مدل داده پشتیبانی می‌کند»).
/// </summary>
public class WorkOrderReleaseGateService : IWorkOrderReleaseGateService
{
    private readonly PEMSDbContext _context;

    public WorkOrderReleaseGateService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<WorkOrderReleaseGateResultDto> EvaluateAsync(int workOrderId)
    {
        var result = new WorkOrderReleaseGateResultDto();

        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Where(x => x.WorkOrderId == workOrderId)
            .Select(x => new
            {
                x.WorkOrderId,
                JobPlanId = x.MaintenancePlan != null ? x.MaintenancePlan.JobPlanId : null
            })
            .FirstOrDefaultAsync();

        if (workOrder?.JobPlanId == null)
            return result; // بدون JobPlan قابل‌دسترس، چیزی برای بررسی نیست

        var jobPlan = await _context.JobPlans
            .AsNoTracking()
            .Where(x => x.JobPlanId == workOrder.JobPlanId.Value)
            .Select(x => new { x.PermitRequired, x.LOTORequired })
            .FirstOrDefaultAsync();

        if (jobPlan == null || (!jobPlan.PermitRequired && !jobPlan.LOTORequired))
            return result;

        var permit = await _context.Permits
            .AsNoTracking()
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderByDescending(x => x.PermitId)
            .FirstOrDefaultAsync();

        if (jobPlan.PermitRequired)
        {
            if (permit == null || permit.Status != "Active")
            {
                result.CanRelease = false;
                result.Reasons.Add("Permit is required for this Job Plan but no Active permit is linked to this Work Order.");
            }
        }

        if (jobPlan.LOTORequired)
        {
            if (permit == null)
            {
                result.CanRelease = false;
                result.Reasons.Add("LOTO (Isolation) is required for this Job Plan but no permit is linked to this Work Order.");
            }
            else
            {
                // هم‌راستا با ExecutionGateService («Isolation Verified»): همه‌ی نقاط فعال و غیرلغوشده باید Verified باشند؛
                // قبلاً وجود «یک» نقطه‌ی Verified کافی بود و نقاط Requested/Isolated دیگر نادیده گرفته می‌شدند.
                var points = await _context.IsolationPoints.AsNoTracking()
                    .Where(x => x.PermitId == permit.PermitId && x.IsActive && x.Status != "Cancelled")
                    .Select(x => x.Status)
                    .ToListAsync();

                if (points.Count == 0)
                {
                    result.CanRelease = false;
                    result.Reasons.Add("LOTO (Isolation) is required but no Isolation Point on the linked permit has been Verified.");
                }
                else if (points.Any(st => st != "Verified"))
                {
                    result.CanRelease = false;
                    result.Reasons.Add($"LOTO (Isolation) is required but not all Isolation Points are Verified ({points.Count(st => st == "Verified")}/{points.Count}).");
                }
            }
        }

        if (permit != null)
        {
            var requiresGasTest = await _context.PermitExecutionRequirements
                .AsNoTracking()
                .Where(x => x.IsActive && x.PermitType == permit.PermitType)
                .Select(x => (bool?)x.RequireValidGasTest)
                .FirstOrDefaultAsync();

            if (requiresGasTest == true)
            {
                var now = DateTime.Now;
                var hasValidGasTest = await _context.GasTests
                    .AnyAsync(x => x.PermitId == permit.PermitId && x.IsActive &&
                        x.Status == "Passed" && x.ValidUntil.HasValue && x.ValidUntil.Value > now);

                if (!hasValidGasTest)
                {
                    result.CanRelease = false;
                    result.Reasons.Add("A valid (Passed, not expired) Gas Test is required for this permit type but none exists.");
                }
            }
        }

        return result;
    }
}
