using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// اسکریپت انتقال داده (نه Schema) برای مدارکی که قبل از راه‌اندازی Workflow Platform
/// وسط گردش‌کار قدیمی (Status = Submitted/Checked/Approved) مانده بودند و هنوز هیچ
/// WorkflowInstance ندارند. بدون این اسکریپت، اولین بار که کسی بخواهد روی این مدارک
/// Check/Approve/Issue بزند، DocumentWorkflowService خطای صریح "این مدرک هنوز منتقل نشده"
/// می‌دهد.
///
/// محدودیت شناخته‌شده: تاریخچه‌ی واقعی این مدارک (کی/کِی Check یا Submit شده) از جدول
/// قدیمی DocumentWorkflows به WorkflowHistory منتقل نمی‌شود - فقط یک رکورد History با
/// ActionType="Backfill" ثبت می‌شود که نشان می‌دهد از کجا شروع شده. تاریخچه‌ی قدیمی همچنان
/// در جدول DocumentWorkflows (که دیگر رشد نمی‌کند) برای مراجعه‌ی دستی باقی می‌ماند.
///
/// Idempotent است: هر بار برنامه بالا بیاید اجرا می‌شود ولی فقط مدارک بدون Instance فعال
/// را پردازش می‌کند - اجرای مکرر بی‌خطر است.
/// ==============================================================================
/// </summary>
public static class EngineeringDocumentWorkflowBackfillService
{
    public static async Task<int> BackfillAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var definition = await db.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.Module == EngineeringDocumentWorkflowSeeder.Module
                                       && x.Category == EngineeringDocumentWorkflowSeeder.Category);

        if (definition == null) return 0; // Seeder هنوز اجرا نشده - چیزی برای Backfill نیست

        var version = await db.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId && v.Status == "Published")
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        if (version == null) return 0;

        var stages = await db.WorkflowStages
            .Where(s => s.WorkflowVersionId == version.WorkflowVersionId)
            .ToListAsync();

        var stageByName = stages.ToDictionary(s => s.Name);

        var legacyToStageName = new Dictionary<string, string>
        {
            ["Submitted"] = "Check",
            ["Checked"] = "Approval",
            ["Approved"] = "Issue"
        };

        var candidates = await db.EngineeringDocuments
            .Where(d => d.IsActive && (d.Status == "Submitted" || d.Status == "Checked" || d.Status == "Approved"))
            .ToListAsync();

        var candidateIds = candidates.Select(d => d.DocumentId).ToList();

        var alreadyMigratedIds = new HashSet<int>((await db.WorkflowInstances
            .Where(x => x.Module == EngineeringDocumentWorkflowSeeder.Module
                        && x.BusinessObjectType == "Document"
                        && x.Status == "Running"
                        && candidateIds.Contains(x.BusinessObjectId))
            .Select(x => x.BusinessObjectId)
            .ToListAsync()));

        var backfilledCount = 0;

        foreach (var document in candidates)
        {
            if (alreadyMigratedIds.Contains(document.DocumentId)) continue;
            if (!legacyToStageName.TryGetValue(document.Status, out var stageName)) continue;
            if (!stageByName.TryGetValue(stageName, out var stage)) continue;

            var instance = new WorkflowInstance
            {
                WorkflowVersionId = version.WorkflowVersionId,
                Module = EngineeringDocumentWorkflowSeeder.Module,
                BusinessObjectType = "Document",
                BusinessObjectId = document.DocumentId,
                ProjectId = document.ProjectId,
                CurrentStageId = stage.WorkflowStageId,
                Status = "Running",
                StartDate = document.CreatedDate,
                InitiatedById = null
            };

            db.WorkflowInstances.Add(instance);

            db.WorkflowInstanceStages.Add(new WorkflowInstanceStage
            {
                Instance = instance,
                WorkflowStageId = stage.WorkflowStageId,
                AssignedToRole = stage.ResponsibleRole,
                Status = "Pending",
                AssignedDate = DateTime.Now
            });

            db.WorkflowHistories.Add(new WorkflowHistory
            {
                Instance = instance,
                FromStageId = null,
                ToStageId = stage.WorkflowStageId,
                ActionType = "Backfill",
                Comment = $"مهاجرت خودکار از وضعیت قدیمی «{document.Status}» (پیش از راه‌اندازی Workflow Platform)",
                ActionDate = DateTime.Now
            });

            backfilledCount++;
        }

        if (backfilledCount > 0)
            await db.SaveChangesAsync();

        return backfilledCount;
    }
}
