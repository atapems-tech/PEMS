using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-694, 742: QC (ITP and NCR), the logic moved without change (see <see cref="IQcOperations"/>).</summary>
public sealed class QcOperations(PEMSDbContext context, IWorkflowRuntime runtime, ICurrentUserService currentUser, IHttpContextAccessor http) : IQcOperations
{
    private const string NcrModule = NcrApprovalWorkflowSeeder.Module;
    private const string NcrBusinessObjectType = "NonConformanceReport";

    private async Task<int?> EmployeeIdAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        return await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }

    // ---- reads
    public async Task<List<QcSummaryRow>> SummaryAsync()
        => [new(await context.NonConformanceReports.CountAsync(x => x.IsActive && x.Status != "Closed"), await context.InspectionTestPlans.CountAsync(x => x.IsActive && x.Status == "Active"))];

    private static QcItpRow ItpRow(InspectionTestPlan i) => new(i.InspectionTestPlanId, i.ITPNumber, i.Title, i.QCDiscipline?.Name ?? "", i.Equipment?.TagNumber ?? "", i.Status, i.Revision, i.CreatedDate);
    private static QcNcrRow NcrRow(NonConformanceReport n) => new(n.NonConformanceReportId, n.NCRNumber, n.QCDiscipline?.Name ?? "", n.Equipment?.TagNumber ?? "", n.Severity, n.Status, n.Disposition ?? "", n.RaisedDate, n.ClosedDate);

    public async Task<List<QcNcrRow>> RecentNcrsAsync()
        => (await context.NonConformanceReports.AsNoTracking().Where(x => x.IsActive).Include(x => x.QCDiscipline).Include(x => x.Equipment).OrderByDescending(x => x.RaisedDate).Take(10).ToListAsync()).Select(NcrRow).ToList();

    public async Task<List<QcItpRow>> RecentItpsAsync()
        => (await context.InspectionTestPlans.AsNoTracking().Where(x => x.IsActive).Include(x => x.QCDiscipline).Include(x => x.Equipment).OrderByDescending(x => x.CreatedDate).Take(10).ToListAsync()).Select(ItpRow).ToList();

    public async Task<List<QcItpRow>> ItpListAsync()
        => (await context.InspectionTestPlans.AsNoTracking().Where(x => x.IsActive).Include(x => x.QCDiscipline).Include(x => x.Equipment).OrderByDescending(x => x.CreatedDate).ToListAsync()).Select(ItpRow).ToList();

    public async Task<List<QcItpHeadRow>> ItpHeadAsync(int id)
    {
        var i = await context.InspectionTestPlans.AsNoTracking().Include(x => x.QCDiscipline).Include(x => x.Equipment).Include(x => x.ProcurementPackage).Include(x => x.Contract).FirstOrDefaultAsync(x => x.InspectionTestPlanId == id);
        return i == null ? [] : [new(i.InspectionTestPlanId, i.ITPNumber, i.Title, i.Status, i.Revision, i.QCDiscipline?.Name ?? "", i.Equipment?.TagNumber ?? "", i.ProcurementPackage?.PackageNumber ?? "", i.Contract?.ContractNo ?? "",
            i.ReferenceCodeStandard ?? "", i.Description ?? "", i.ApprovedDate)];
    }

    public async Task<List<QcLineItemRow>> ItpLineItemsAsync(int id)
        => (await context.ITPLineItems.AsNoTracking().Include(x => x.QCDiscipline).Where(x => x.InspectionTestPlanId == id).OrderBy(x => x.SequenceNo).ToListAsync())
            .Select(l => new QcLineItemRow(l.ITPLineItemId, l.SequenceNo, l.Activity, l.QCDiscipline?.Name ?? "", l.InspectionCategory, l.AcceptanceCriteria ?? "", l.ReferenceCodeStandard ?? "", l.ResponsibleParty ?? "", l.Status, l.NotifiedDate)).ToList();

    public async Task<List<QcNcrRow>> NcrListAsync()
        => (await context.NonConformanceReports.AsNoTracking().Where(x => x.IsActive).Include(x => x.QCDiscipline).Include(x => x.Equipment).OrderByDescending(x => x.RaisedDate).ToListAsync()).Select(NcrRow).ToList();

    public async Task<List<QcNcrHeadRow>> NcrHeadAsync(int id)
    {
        var n = await context.NonConformanceReports.AsNoTracking().Include(x => x.QCDiscipline).Include(x => x.Equipment).Include(x => x.ITPLineItem).FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        return n == null ? [] : [new(n.NonConformanceReportId, n.NCRNumber, n.QCDiscipline?.Name ?? "", n.Equipment?.TagNumber ?? "", n.Severity, n.Status, n.Description, n.Disposition ?? "", n.RootCause ?? "", n.CorrectiveAction ?? "",
            n.PreventiveAction ?? "", n.RaisedDate, n.DispositionedDate, n.ClosedDate, n.ITPLineItem?.Activity ?? "")];
    }

    private async Task<List<NonConformanceReport>> ActiveNcrsAsync()
        => await context.NonConformanceReports.AsNoTracking().Where(x => x.IsActive).Include(x => x.QCDiscipline).ToListAsync();

    public async Task<List<QcKpiRow>> KpiAsync()
    {
        var ncrs = await ActiveNcrsAsync();
        var closed = ncrs.Count(x => x.ClosedDate.HasValue);
        var rate = closed == 0 ? 0 : Math.Round(100.0 * ncrs.Count(x => x.ClosedDate.HasValue && (x.ClosedDate.Value - x.RaisedDate).TotalDays <= 14) / closed, 1);
        return [new(1, ncrs.Count, rate)];
    }

    public async Task<List<QcKpiGroupRow>> KpiByDisciplineAsync()
        => (await ActiveNcrsAsync()).GroupBy(x => x.QCDiscipline?.Name ?? "نامشخص")
            .Select(g => new QcKpiGroupRow(g.Key, g.Count(), g.Count(x => x.Status != "Closed"), g.Count(x => x.Severity == "Critical"),
                g.Where(x => x.ClosedDate.HasValue).Select(x => (x.ClosedDate!.Value - x.RaisedDate).TotalDays).DefaultIfEmpty(0).Average())).OrderByDescending(x => x.Total).ToList();

    public async Task<List<QcKpiGroupRow>> KpiBySeverityAsync()
        => (await ActiveNcrsAsync()).GroupBy(x => x.Severity).Select(g => new QcKpiGroupRow(g.Key, g.Count(), 0, 0, 0)).ToList();

    public async Task<List<QcKpiGroupRow>> KpiByMonthAsync()
        => (await ActiveNcrsAsync()).Where(x => x.RaisedDate >= DateTime.Now.AddMonths(-12)).GroupBy(x => x.RaisedDate.ToString("yyyy-MM"))
            .Select(g => new QcKpiGroupRow(g.Key, g.Count(), 0, 0, 0)).OrderBy(x => x.Group).ToList();

    // ---- the choices of the two create forms
    public async Task<List<QcOption>> ProjectOptionsAsync()
        => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new QcOption(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<QcOption>> DisciplineOptionsAsync()
        => await context.QCDisciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new QcOption(x.QCDisciplineId.ToString(), x.Name)).ToListAsync();

    public async Task<List<QcOption>> EquipmentOptionsAsync()
        => await context.Equipments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.TagNumber).Take(300).Select(x => new QcOption(x.EquipmentId.ToString(), x.TagNumber + " - " + x.Name)).ToListAsync();

    public async Task<List<QcOption>> PackageOptionsAsync()
        => await context.ProcurementPackages.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.CreatedDate).Select(x => new QcOption(x.ProcurementPackageId.ToString(), x.PackageNumber)).ToListAsync();

    public async Task<List<QcOption>> ContractOptionsAsync()
        => await context.Contracts.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.CreatedDate).Select(x => new QcOption(x.ContractId.ToString(), x.ContractNo)).ToListAsync();

    // ---- the two creates
    /// <summary>The id of the new plan (the action opens it).</summary>
    public async Task<int> ItpCreateAsync(string itpNumber, int projectId, int qcDisciplineId, string title, int? equipmentId = null, string? referenceCodeStandard = null, string? description = null,
        int? procurementPackageId = null, int? contractId = null)
    {
        var plan = new InspectionTestPlan
        {
            ITPNumber = itpNumber, ProjectId = projectId, QCDisciplineId = qcDisciplineId, EquipmentId = equipmentId, ProcurementPackageId = procurementPackageId, ContractId = contractId, Title = title,
            ReferenceCodeStandard = referenceCodeStandard, Description = description, Status = "Draft", Revision = 0, IsActive = true, CreatedDate = DateTime.Now
        };
        await context.InspectionTestPlans.AddAsync(plan);
        await context.SaveChangesAsync();
        return plan.InspectionTestPlanId;
    }

    /// <summary>The id of the new report (the action opens it).</summary>
    public async Task<int> NcrCreateAsync(string ncrNumber, int projectId, int qcDisciplineId, string description, string severity, int? equipmentId = null, int? itpLineItemId = null)
    {
        var report = new NonConformanceReport
        {
            NCRNumber = ncrNumber, ProjectId = projectId, QCDisciplineId = qcDisciplineId, EquipmentId = equipmentId, ITPLineItemId = itpLineItemId, Description = description, Severity = severity, Status = "Open",
            RaisedById = await EmployeeIdAsync(), RaisedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now
        };
        await context.NonConformanceReports.AddAsync(report);
        await context.SaveChangesAsync();
        return report.NonConformanceReportId;
    }

    public async Task<(bool Success, string? Error)> ItpAddLineItemAsync(int itpId, string activity, int qcDisciplineId, string inspectionCategory, string? acceptanceCriteria = null, string? referenceCodeStandard = null, string? responsibleParty = null)
    {
        var itp = await context.InspectionTestPlans.FirstOrDefaultAsync(x => x.InspectionTestPlanId == itpId);
        if (itp == null) return (false, "ITP not found.");

        var nextSeq = 1 + (await context.ITPLineItems.Where(x => x.InspectionTestPlanId == itpId).Select(x => (int?)x.SequenceNo).MaxAsync() ?? 0);
        await context.ITPLineItems.AddAsync(new ITPLineItem
        {
            InspectionTestPlanId = itpId, SequenceNo = nextSeq, Activity = activity, QCDisciplineId = qcDisciplineId, InspectionCategory = inspectionCategory,
            AcceptanceCriteria = acceptanceCriteria, ReferenceCodeStandard = referenceCodeStandard, ResponsibleParty = responsibleParty, Status = "Pending", IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ItpApproveAsync(int id)
    {
        var itp = await context.InspectionTestPlans.FirstOrDefaultAsync(x => x.InspectionTestPlanId == id);
        if (itp == null) return (false, "ITP not found.");
        if (itp.Status != "Draft") return (false, "فقط ITP در وضعیت Draft قابل تایید است.");

        itp.Status = "Active";
        itp.ApprovedDate = DateTime.Now;
        itp.ApprovedById = await EmployeeIdAsync();
        await context.SaveChangesAsync();
        return (true, null);
    }

    // the hold point (H) means nothing without the record that the responsible person was told
    public async Task<(bool Success, string? Error)> ItpNotifyHoldPointAsync(int lineItemId)
    {
        var lineItem = await context.ITPLineItems.FirstOrDefaultAsync(x => x.ITPLineItemId == lineItemId);
        if (lineItem == null) return (false, "Line item not found.");

        lineItem.NotifiedDate = DateTime.Now;
        lineItem.NotifiedById = await EmployeeIdAsync();
        if (lineItem.Status == "Pending") lineItem.Status = "Notified";
        await context.SaveChangesAsync();
        return (true, null);
    }

    // the inspector records the real result from the ITP row itself and the row status follows
    public async Task<(bool Success, string? Error)> ItpPerformInspectionAsync(int lineItemId, string result, string? inspector = null, string? description = null)
    {
        var lineItem = await context.ITPLineItems.Include(x => x.InspectionTestPlan).FirstOrDefaultAsync(x => x.ITPLineItemId == lineItemId);
        if (lineItem == null) return (false, "Line item not found.");
        if (lineItem.InspectionTestPlan?.EquipmentId == null)
            return (false, "این ITP به هیچ تجهیزی وصل نیست - ثبت بازرسی روی ردیف بدون تجهیز ممکن نیست.");

        await context.InspectionRecords.AddAsync(new InspectionRecord
        {
            EquipmentId = lineItem.InspectionTestPlan.EquipmentId.Value, ITPLineItemId = lineItem.ITPLineItemId, QCDisciplineId = lineItem.QCDisciplineId,
            InspectionType = lineItem.Activity, InspectionDate = DateTime.Now, Inspector = inspector, Result = result, Description = description, IsActive = true, CreatedDate = DateTime.Now
        });
        lineItem.Status = result == "Acceptable" ? "Passed" : "Failed";
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> NcrDispositionAsync(int id, string disposition, string? rootCause = null, string? correctiveAction = null, string? preventiveAction = null)
    {
        var ncr = await context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return (false, "NCR not found.");

        ncr.Disposition = disposition;
        ncr.RootCause = rootCause;
        ncr.CorrectiveAction = correctiveAction;
        ncr.PreventiveAction = preventiveAction;
        ncr.DispositionedById = await EmployeeIdAsync();
        ncr.DispositionedDate = DateTime.Now;

        // a Critical NCR cannot be closed on the word of whoever recorded the disposition: it goes to the independent QA manager approval workflow; Minor and Major are dispositioned directly
        if (ncr.Severity == "Critical")
        {
            await runtime.StartInstanceAsync(NcrModule, NcrBusinessObjectType, ncr.NonConformanceReportId, NcrApprovalWorkflowSeeder.Category, ncr.ProjectId, ncr.DispositionedById);
            ncr.Status = "PendingApproval";
        }
        else
        {
            ncr.Status = "Dispositioned";
        }
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> NcrApproveAsync(int id, string decision, string? comment = null)
    {
        var ncr = await context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return (false, "NCR not found.");
        if (ncr.Status != "PendingApproval") return (false, "این NCR در وضعیت انتظار تایید نیست.");

        var instance = await runtime.GetInstanceAsync(NcrBusinessObjectType, ncr.NonConformanceReportId);
        if (instance == null || instance.Status != "Running") return (false, "گردش‌کار تایید این NCR یافت نشد.");

        try
        {
            var request = http.HttpContext?.Request;
            await runtime.ExecuteActionAsync(instance.WorkflowInstanceId, decision, await EmployeeIdAsync(), currentUser.Roles, comment,
                http.HttpContext?.Connection.RemoteIpAddress?.ToString(), request?.Headers["User-Agent"].ToString());

            ncr.Status = decision == "Approve" ? "Dispositioned" : "Open";
            if (decision != "Approve") ncr.Disposition = null;     // rejected: the earlier disposition is no longer valid
            await context.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> NcrCloseAsync(int id)
    {
        var ncr = await context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return (false, "NCR not found.");
        if (ncr.Status != "Dispositioned") return (false, "قبل از بستن NCR باید Disposition ثبت شده باشد.");

        ncr.Status = "Closed";
        ncr.ClosedById = await EmployeeIdAsync();
        ncr.ClosedDate = DateTime.Now;
        await context.SaveChangesAsync();
        return (true, null);
    }
}
