using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class MaterialRequisitionService : IMaterialRequisitionService
{
    private readonly PEMSDbContext _context;

    public MaterialRequisitionService(PEMSDbContext context)
    {
        _context = context;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول با ۳
    // Include غیرضروری (Select پایین‌تر فقط فیلد اسکالر می‌گیرد) و بدون Skip/Take بارگذاری
    // می‌شد.
    public async Task<PagedResult<MRDto>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        var query = _context.MaterialRequisitions.AsQueryable();

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.MRId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MRDto
            {
                MRId = x.MRId,
                MRNumber = x.MRNumber,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                DisciplineId = x.DisciplineId,
                DisciplineName = x.Discipline!.Name,
                EquipmentTagNumber = x.EquipmentTagNumber,
                Description = x.Description,
                RequiredDate = x.RequiredDate,
                ResponsibleEngineerName = x.ResponsibleEngineer != null
                    ? x.ResponsibleEngineer.FirstName + " " + x.ResponsibleEngineer.LastName
                    : null,
                Status = x.Status,
                CreatedDate = x.CreatedDate,
                QuotationsCount = x.Quotations.Count
            })
            .ToListAsync();

        return new PagedResult<MRDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<MRDto?> GetByIdAsync(int id)
    {
        var mr = await _context.MaterialRequisitions
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.ResponsibleEngineer)
            .Include(x => x.Items)
            .Include(x => x.Quotations)
            .FirstOrDefaultAsync(x => x.MRId == id);

        if (mr == null) return null;

        return new MRDto
        {
            MRId = mr.MRId,
            MRNumber = mr.MRNumber,
            ProjectId = mr.ProjectId,
            ProjectName = mr.Project?.ProjectName ?? "",
            DisciplineId = mr.DisciplineId,
            DisciplineName = mr.Discipline?.Name ?? "",
            EquipmentTagNumber = mr.EquipmentTagNumber,
            Description = mr.Description,
            RequiredDate = mr.RequiredDate,
            ResponsibleEngineerName = mr.ResponsibleEngineer != null
                ? mr.ResponsibleEngineer.FirstName + " " + mr.ResponsibleEngineer.LastName
                : null,
            Status = mr.Status,
            CreatedDate = mr.CreatedDate,
            QuotationsCount = mr.Quotations.Count,
            Items = mr.Items.Select(i => new MRItemDto
            {
                MRItemId = i.MRItemId,
                ItemDescription = i.ItemDescription,
                PartNumber = i.PartNumber,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Remarks = i.Remarks
            }).ToList(),
            Quotations = mr.Quotations.Select(q => new MRQDto
            {
                MRQId = q.MRQId,
                MRQNumber = q.MRQNumber,
                MRId = q.MRId,
                MRNumber = mr.MRNumber,
                VendorName = q.VendorName,
                InquiryDate = q.InquiryDate,
                QuotedAmount = q.QuotedAmount,
                Currency = q.Currency,
                QuotationStatus = q.QuotationStatus,
                TechnicalEvaluation = q.TechnicalEvaluation,
                CommercialEvaluation = q.CommercialEvaluation,
                ApprovalStatus = q.ApprovalStatus,
                Notes = q.Notes
            }).ToList()
        };
    }

    public async Task<int> CreateAsync(string mrNumber, int projectId, int disciplineId, string? equipmentTag,
        string description, DateTime? requiredDate, int? responsibleEngineerId, int? relatedDocumentId,
        List<(string desc, string? part, double qty, string? unit)> items)
    {
        var mr = new MaterialRequisition
        {
            MRNumber = mrNumber,
            ProjectId = projectId,
            DisciplineId = disciplineId,
            EquipmentTagNumber = equipmentTag,
            Description = description,
            RequiredDate = requiredDate,
            ResponsibleEngineerId = responsibleEngineerId,
            RelatedDocumentId = relatedDocumentId,
            Status = "Draft",
            CreatedDate = DateTime.Now
        };

        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.desc)))
        {
            mr.Items.Add(new MaterialRequisitionItem
            {
                ItemDescription = item.desc,
                PartNumber = item.part,
                Quantity = item.qty,
                Unit = item.unit
            });
        }

        await _context.MaterialRequisitions.AddAsync(mr);
        await _context.SaveChangesAsync();

        return mr.MRId;
    }

    public async Task ChangeStatusAsync(int mrId, string newStatus)
    {
        var mr = await _context.MaterialRequisitions.FirstOrDefaultAsync(x => x.MRId == mrId);
        if (mr == null) return;
        mr.Status = newStatus;
        await _context.SaveChangesAsync();
    }

    public async Task<int> AddQuotationAsync(int mrId, string mrqNumber, string vendorName, DateTime? inquiryDate,
        decimal? amount, string? currency, string? notes)
    {
        var mrq = new MaterialRequisitionQuotation
        {
            MRId = mrId,
            MRQNumber = mrqNumber,
            VendorName = vendorName,
            InquiryDate = inquiryDate,
            QuotedAmount = amount,
            Currency = currency ?? "USD",
            QuotationStatus = "Sent",
            Notes = notes,
            CreatedDate = DateTime.Now
        };

        await _context.MaterialRequisitionQuotations.AddAsync(mrq);
        await _context.SaveChangesAsync();

        return mrq.MRQId;
    }

    public async Task UpdateQuotationEvaluationAsync(int mrqId, string technical, string commercial, string approval)
    {
        var mrq = await _context.MaterialRequisitionQuotations.FirstOrDefaultAsync(x => x.MRQId == mrqId);
        if (mrq == null) return;

        mrq.TechnicalEvaluation = technical;
        mrq.CommercialEvaluation = commercial;
        mrq.ApprovalStatus = approval;
        mrq.QuotationStatus = "Evaluated";

        await _context.SaveChangesAsync();
    }
}
