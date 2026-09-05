using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class LetterNumberingService : ILetterNumberingService
{
    private readonly PEMSDbContext _context;

    public LetterNumberingService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextNumberAsync(string letterType, int? projectId)
    {
        var typeCode = letterType switch
        {
            "Incoming" => "IN",
            "Outgoing" => "OUT",
            "Internal" => "INT",
            _ => "LTR"
        };

        var year = DateTime.Now.Year;

        var projectCode = "GEN";
        if (projectId.HasValue)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId.Value);
            projectCode = project?.ProjectCode ?? "GEN";
        }

        // شمارنده مستقل به‌ازای هر نوع نامه + سال (الگوی متداول دبیرخانه‌ی سازمانی)
        var countThisYear = await _context.Letters.CountAsync(x =>
            x.LetterType == letterType && x.LetterDate.Year == year);

        var sequence = countThisYear + 1;

        return $"{projectCode}-{typeCode}-{year}-{sequence:D4}";
    }
}
