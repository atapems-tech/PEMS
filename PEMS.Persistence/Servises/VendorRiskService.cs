using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// نسخه‌ی اول (Heuristic) امتیازدهی ریسک Vendor طبق معماری پیشنهادی:
/// ترکیب وزن‌دار درصد به‌موقع بودن، نرخ رد/اصلاح (CO/AC/RE)، و تعداد آیتم‌های معوق فعلی.
/// امتیاز بین 0 (کم‌ریسک) تا 1 (پرریسک) است.
/// </summary>
public class VendorRiskService : IVendorRiskService
{
    private readonly PEMSDbContext _context;

    public VendorRiskService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task RecalculateAllAsync()
    {
        var vendorIds = await _context.Vendors.Select(x => x.VendorId).ToListAsync();

        foreach (var id in vendorIds)
            await RecalculateAsync(id);
    }

    public async Task RecalculateAsync(int vendorId)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == vendorId);
        if (vendor == null) return;

        var items = await _context.VDRItems
            .Include(x => x.Submissions)
            .Where(x => x.VendorId == vendorId && x.IsActive)
            .ToListAsync();

        if (!items.Any())
        {
            vendor.RiskLevel = "Unknown";
            vendor.RiskScore = null;
            vendor.RiskAssessedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return;
        }

        var overdueCount = items.Count(VDRService.IsOverdue);
        var overdueRate = (double)overdueCount / items.Count;

        var allSubmissions = items.SelectMany(x => x.Submissions).ToList();

        var reviewedSubmissions = allSubmissions
            .Where(s => !string.IsNullOrWhiteSpace(s.ReviewCategory))
            .ToList();

        var resubmissionRate = reviewedSubmissions.Count == 0
            ? 0
            : (double)reviewedSubmissions.Count(s => s.RequiresResubmission) / reviewedSubmissions.Count;

        var closedCount = items.Count(x => x.WorkflowStatus == "Closed");
        var closureRate = (double)closedCount / items.Count;

        // وزن‌دهی: تاخیر ۴۰٪، نرخ اصلاح/رد ۴۰٪، (۱ - نرخ بسته‌شدن) ۲۰٪
        var riskScore = (0.4 * overdueRate) + (0.4 * resubmissionRate) + (0.2 * (1 - closureRate));

        riskScore = Math.Round(Math.Clamp(riskScore, 0, 1), 4);

        vendor.RiskScore = riskScore;

        vendor.RiskLevel = riskScore switch
        {
            < 0.25 => "Low",
            < 0.55 => "Medium",
            _ => "High"
        };

        vendor.RiskAssessedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }
}
