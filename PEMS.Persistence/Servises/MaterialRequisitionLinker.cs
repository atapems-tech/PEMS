using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق درخواست: خروجی ایمپورت MDR/VDR باید جدول MR/MRQ را هم بسازد، نه فقط لینک کند - و
/// چون در PEMS شماره‌ی MR و MRQ یکی است (نه دو شماره‌ی جدا)، هر دو با یک شماره‌ی مشترک
/// ساخته می‌شوند.
/// </summary>
public static class MaterialRequisitionLinker
{
    public static async Task<MaterialRequisition> FindOrCreateAsync(
        PEMSDbContext context,
        string mrNumber,
        int projectId,
        int? disciplineId,
        string? vendorName = null)
    {
        var trimmedNumber = mrNumber.Trim();

        var existing = await context.MaterialRequisitions
            .FirstOrDefaultAsync(x => x.MRNumber.ToLower() == trimmedNumber.ToLower() && x.ProjectId == projectId);

        if (existing != null) return existing;

        var mr = new MaterialRequisition
        {
            MRNumber = trimmedNumber,
            ProjectId = projectId,
            // اگر دیسیپلین مشخص نبود، به دیسیپلین اول پروژه به‌عنوان پیش‌فرض موقت نسبت داده
            // می‌شود - کاربر بعداً می‌تواند اصلاح کند؛ خالی‌گذاشتن ممکن نیست چون DisciplineId
            // روی MaterialRequisition اجباری است
            DisciplineId = disciplineId ?? await context.Disciplines.Select(x => x.DisciplineId).FirstOrDefaultAsync(),
            Description = $"Auto-created from MDR/VDR import (MR/MRQ No: {trimmedNumber})",
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await context.MaterialRequisitions.AddAsync(mr);
        await context.SaveChangesAsync();

        // طبق تایید کاربر: شماره MR و MRQ در PEMS یکی است - همان شماره برای MRQ هم استفاده می‌شود
        var mrqExists = await context.MaterialRequisitionQuotations
            .AnyAsync(x => x.MRId == mr.MRId && x.MRQNumber.ToLower() == trimmedNumber.ToLower());

        if (!mrqExists)
        {
            await context.MaterialRequisitionQuotations.AddAsync(new MaterialRequisitionQuotation
            {
                MRQNumber = trimmedNumber,
                MRId = mr.MRId,
                VendorName = vendorName ?? "Unknown (from import)",
                QuotationStatus = "Not Sent",
                CreatedDate = DateTime.Now,
                IsActive = true
            });

            await context.SaveChangesAsync();
        }

        return mr;
    }
}
