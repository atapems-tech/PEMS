using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class CorrespondenceReportService : ICorrespondenceReportService
{
    private readonly PEMSDbContext _context;

    public CorrespondenceReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<CorrespondenceReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Letters.Where(x => x.IsActive).AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId);
        if (fromDate.HasValue) query = query.Where(x => x.LetterDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.LetterDate <= toDate.Value);

        var letters = await query
            .Include(x => x.Discipline)
            .ToListAsync();

        var letterIds = letters.Select(x => x.LetterId).ToList();

        var referrals = await _context.LetterReferrals
            .Where(x => letterIds.Contains(x.LetterId))
            .ToListAsync();

        var report = new CorrespondenceReportDto
        {
            TotalLetters = letters.Count,
            TotalIncoming = letters.Count(x => x.LetterType == "Incoming"),
            TotalOutgoing = letters.Count(x => x.LetterType == "Outgoing"),
            TotalInternal = letters.Count(x => x.LetterType == "Internal"),
            OverdueReferrals = referrals.Count(r => r.Status != "Completed" && r.DueDate.HasValue && r.DueDate.Value < DateTime.Now)
        };

        var completed = referrals.Where(r => r.Status == "Completed" && r.CompletedDate.HasValue).ToList();
        report.AvgReferralCompletionDays = completed.Any()
            ? completed.Average(r => (r.CompletedDate!.Value - r.ReferredDate).TotalDays)
            : null;

        // حجم مکاتبات ۱۲ ماه اخیر
        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.VolumeByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Incoming: letters.Count(x => x.LetterType == "Incoming" && x.LetterDate.Year == m.Year && x.LetterDate.Month == m.Month),
            Outgoing: letters.Count(x => x.LetterType == "Outgoing" && x.LetterDate.Year == m.Year && x.LetterDate.Month == m.Month)
        )).ToList();

        report.ByDiscipline = letters
            .Where(x => x.Discipline != null)
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => (Discipline: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByStatus = letters
            .GroupBy(x => x.Status)
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var tagGroups = await _context.LetterTags
            .Include(x => x.Tag)
            .Where(x => letterIds.Contains(x.LetterId) && x.Tag != null)
            .GroupBy(x => new { x.Tag!.Name, x.Tag.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        report.TopTags = tagGroups.Select(x => (x.Name, x.Color, x.Count)).ToList();

        report.TopSenders = letters
            .Where(x => !string.IsNullOrWhiteSpace(x.Sender))
            .GroupBy(x => x.Sender!)
            .Select(g => (Sender: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        return report;
    }
}
