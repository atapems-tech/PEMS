using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// گزارش تدارکات - طبق درخواست: هر بخش گزارش‌گیری با فیلترهای گوناگون مخصوص خودش.
/// </summary>
public class ProcurementReportService : IProcurementReportService
{
    private readonly PEMSDbContext _context;

    public ProcurementReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<ProcurementReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.ProcurementPackages
            .AsNoTracking()
            .Include(x => x.Discipline)
            .Include(x => x.MaterialRequisitions)
            .Include(x => x.Quotations)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedDate <= toDate.Value);

        var packages = await query.ToListAsync();

        var report = new ProcurementReportDto
        {
            TotalPackages = packages.Count,
            TotalMRs = packages.Sum(x => x.MaterialRequisitions.Count),
            TotalMRQs = packages.Sum(x => x.Quotations.Count),
            OpenCount = packages.Count(x => x.Status != "Closed"),
            ClosedCount = packages.Count(x => x.Status == "Closed")
        };

        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.VolumeByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Count: packages.Count(x => x.CreatedDate.Year == m.Year && x.CreatedDate.Month == m.Month)
        )).ToList();

        report.ByStatus = packages
            .GroupBy(x => x.Status)
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByVendor = packages
            .SelectMany(x => x.Quotations)
            .Where(q => !string.IsNullOrWhiteSpace(q.VendorName))
            .GroupBy(q => q.VendorName!)
            .Select(g => (Vendor: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        report.ByDiscipline = packages
            .Where(x => x.Discipline != null)
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => (Discipline: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        // طبق Gap ۵: افزودنی خالص - زنجیره‌ی Tender/Bid/Contract/PO/NTP که پیش از این وجود نداشت
        var packageIds = packages.Select(x => x.ProcurementPackageId).ToList();

        var tenderQuery = _context.Tenders.AsNoTracking().Where(t => packageIds.Contains(t.ProcurementPackageId));
        report.TotalTenders = await tenderQuery.CountAsync();

        var tenderIds = await tenderQuery.Select(t => t.TenderId).ToListAsync();
        report.TotalBids = await _context.Bids.AsNoTracking().CountAsync(b => tenderIds.Contains(b.TenderId));

        var contractIds = await tenderQuery.Where(t => t.ContractId != null).Select(t => t.ContractId!.Value).ToListAsync();
        var contractsQuery = _context.Contracts.AsNoTracking().Where(c => contractIds.Contains(c.ContractId));
        report.TotalContracts = await contractsQuery.CountAsync();

        report.TotalPurchaseOrders = await _context.PurchaseOrders.AsNoTracking().CountAsync(p => p.ContractId != null && contractIds.Contains(p.ContractId!.Value));
        report.TotalNTPsIssued = await _context.ContractNTPs.AsNoTracking().CountAsync(n => contractIds.Contains(n.ContractId));

        report.ByContractStatus = await contractsQuery
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => ValueTuple.Create(x.Status, x.Count))
            .ToListAsync();

        return report;
    }

    // طبق Gap ۵: پروجکشن مستقیم (بدون بارگذاری کامل Entity) + صفحه‌بندی واقعی سمت SQL - چون این
    // Join چندگانه‌تر و سنگین‌تر از خلاصه‌ی بالاست
    public async Task<PagedResult<ProcurementPipelineRowDto>> GetPipelineAsync(
        int? projectId = null, int? disciplineId = null, DateTime? fromDate = null, DateTime? toDate = null,
        int page = 1, int pageSize = 25)
    {
        var query = _context.Tenders
            .AsNoTracking()
            .Include(t => t.ProcurementPackage)
            .Where(t => t.ProcurementPackage != null)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(t => t.ProcurementPackage!.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(t => t.ProcurementPackage!.DisciplineId == disciplineId.Value);
        if (fromDate.HasValue) query = query.Where(t => t.IssueDate == null || t.IssueDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(t => t.IssueDate == null || t.IssueDate <= toDate.Value);

        query = query.OrderByDescending(t => t.TenderId);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new ProcurementPipelineRowDto
            {
                PackageNumber = t.ProcurementPackage!.PackageNumber,
                TenderNumber = t.TenderNumber,
                TenderStatus = t.Status,
                WinnerVendorName = t.AwardedBid != null ? t.AwardedBid.Vendor!.Name : null,
                BidStatus = t.AwardedBid != null ? t.AwardedBid.Status : null,
                TechnicalEvaluationStatus = t.AwardedBid != null
                    ? t.AwardedBid.Evaluations.Where(e => e.EvaluationType == "Technical").Select(e => e.Decision).FirstOrDefault()
                    : null,
                CommercialEvaluationStatus = t.AwardedBid != null
                    ? t.AwardedBid.Evaluations.Where(e => e.EvaluationType == "Commercial").Select(e => e.Decision).FirstOrDefault()
                    : null,
                ContractNo = t.Contract != null ? t.Contract.ContractNo : null,
                ContractStatus = t.Contract != null ? t.Contract.Status : null,
                ContractValue = t.Contract != null ? (t.Contract.CurrentValue ?? t.Contract.ContractValue) : null,
                PoNumber = t.ContractId != null
                    ? _context.PurchaseOrders.Where(p => p.ContractId == t.ContractId).Select(p => p.PoNumber).FirstOrDefault()
                    : null,
                NtpStatus = t.ContractId != null
                    ? _context.ContractNTPs.Where(n => n.ContractId == t.ContractId).OrderByDescending(n => n.IssuedDate).Select(n => n.Status).FirstOrDefault()
                    : null,
                KickoffStatus = t.ContractId != null
                    ? _context.Meetings.Where(m => m.ContractId == t.ContractId && m.MeetingType == "KickOff").Select(m => m.Status).FirstOrDefault()
                    : null
            })
            .ToListAsync();

        return new PagedResult<ProcurementPipelineRowDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
