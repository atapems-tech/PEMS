using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-694: the write commands of QCController (ITP and NCR), the logic moved without change (see <see cref="IQcOperations"/>).</summary>
public sealed class QcOperations(PEMSDbContext context, IWorkflowRuntime runtime, ICurrentUserService currentUser, IHttpContextAccessor http) : IQcOperations
{
    private const string NcrModule = NcrApprovalWorkflowSeeder.Module;
    private const string NcrBusinessObjectType = "NonConformanceReport";

    private async Task<int?> EmployeeIdAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        return await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
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
