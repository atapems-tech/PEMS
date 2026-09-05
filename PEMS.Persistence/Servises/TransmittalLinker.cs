using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیداکردن یا ساخت یک Transmittal بر اساس شماره‌ای که از ایمپورت MDR/VDR آمده، و افزودن یک
/// مدرک (مهندسی یا VDR - از وقتی TransmittalItem.Document به هسته‌ی مشترک Document عمومی شد)
/// به فهرست اقلام آن. مشترک بین MDRImportService و VDRImportService.
/// </summary>
public static class TransmittalLinker
{
    public static async Task<Transmittal> FindOrCreateAsync(
        PEMSDbContext context, int projectId, string transmittalNo, int documentId, string revisionNo, string sourceLabel)
    {
        var transmittal = await context.Transmittals
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TransmittalNo == transmittalNo);

        if (transmittal == null)
        {
            transmittal = new Transmittal
            {
                TransmittalNo = transmittalNo,
                ProjectId = projectId,
                From = sourceLabel,
                To = "-",
                Purpose = "For Information",
                Status = "Sent",
                TransmittalDate = DateTime.Now,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            await context.Transmittals.AddAsync(transmittal);
            await context.SaveChangesAsync();
        }

        var alreadyLinked = await context.TransmittalItems
            .AnyAsync(x => x.TransmittalId == transmittal.TransmittalId && x.DocumentId == documentId);

        if (!alreadyLinked)
        {
            await context.TransmittalItems.AddAsync(new TransmittalItem
            {
                TransmittalId = transmittal.TransmittalId,
                DocumentId = documentId,
                RevisionNo = revisionNo
            });

            await context.SaveChangesAsync();
        }

        return transmittal;
    }
}
