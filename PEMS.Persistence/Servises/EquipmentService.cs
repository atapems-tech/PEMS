using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Digital Equipment Passport: هر تجهیز به مدارک مهندسی، آیتم‌های VDR، MR های مرتبط
/// (بر اساس شماره تگ)، و تاریخچه‌ی Work Order وصل می‌شود - طبق سند Enterprise Development Guidelines.
/// </summary>
public class EquipmentService : IEquipmentService
{
    private readonly PEMSDbContext _context;
    private readonly IRelationshipService _relationshipService;
    private readonly IFunctionalLocationService _functionalLocationService;

    public EquipmentService(PEMSDbContext context, IRelationshipService relationshipService,
        IFunctionalLocationService functionalLocationService)
    {
        _context = context;
        _relationshipService = relationshipService;
        _functionalLocationService = functionalLocationService;
    }

    public async Task<List<EquipmentDto>> GetAllAsync(
        int? projectId = null, int? disciplineId = null, string? equipmentType = null,
        string? status = null, string? q = null)
    {
        var query = _context.Equipments
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (!string.IsNullOrWhiteSpace(equipmentType)) query = query.Where(x => x.EquipmentType == equipmentType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.TagNumber.ToLower().Contains(term) || x.Name.ToLower().Contains(term)
                || (x.Manufacturer != null && x.Manufacturer.ToLower().Contains(term))
                || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(x => x.TagNumber)
            .Select(x => new EquipmentDto
            {
                EquipmentId = x.EquipmentId,
                TagNumber = x.TagNumber,
                Name = x.Name,
                EquipmentType = x.EquipmentType,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                DisciplineId = x.DisciplineId,
                DisciplineName = x.Discipline!.Name,
                Manufacturer = x.Manufacturer,
                Model = x.Model,
                Status = x.Status,
                DocumentsCount = x.EngineeringDocuments.Count(d => d.IsActive),
                VDRItemsCount = _context.VDRItems.Count(v => v.EquipmentId == x.EquipmentId && v.IsActive),
                WorkOrdersCount = _context.WorkOrders.Count(w => w.EquipmentId == x.EquipmentId && w.IsActive)
            })
            .ToListAsync();
    }


    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - A-4): قبلاً کل جدول (سه ستون) با
    // ToListAsync بارگذاری می‌شد و هر سه شمارنده در حافظه محاسبه می‌شدند. TotalCount/
    // ActiveCount/ByDiscipline/ByStatus حالا CountAsync/GroupBy واقعی SQL هستند (ریسک کم -
    // GroupBy روی یک ستون ساده). ByType عمداً همچنان در حافظه محاسبه می‌شود چون Bucket
    // "نامشخص" (IsNullOrWhiteSpace) قبل از GroupBy اعمال می‌شود؛ ترجمه‌ی دقیق IsNullOrWhiteSpace
    // به SQL Server (فاصله/Tab/خط‌جدید) ریسک تفاوت رفتار دارد - طبق بند A-4 دست‌نخورده ماند،
    // فقط با یک Projection سبک‌تر (فقط EquipmentType، نه کل سه ستون).
    public async Task<EquipmentDashboardDto> GetDashboardAsync(int? projectId = null)
    {
        var query = _context.Equipments.Where(x => x.IsActive).AsQueryable();
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);

        var totalCount = await query.CountAsync();
        var activeCount = await query.CountAsync(x => x.Status == "Active");

        var byDiscipline = await query
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var byStatus = await query
            .GroupBy(x => x.Status)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var equipmentTypes = await query.Select(x => x.EquipmentType).ToListAsync();

        return new EquipmentDashboardDto
        {
            TotalCount = totalCount,
            ActiveCount = activeCount,
            InactiveCount = totalCount - activeCount,
            ByDiscipline = byDiscipline.Select(x => (x.Name, x.Count)).ToList(),
            ByType = equipmentTypes
                .GroupBy(t => string.IsNullOrWhiteSpace(t) ? "نامشخص" : t)
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .ToList(),
            ByStatus = byStatus.Select(x => (x.Name, x.Count)).ToList()
        };
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Equipment Reconciliation): همون
    // فیلترهای GetAllAsync، اما فیلتر/شمارش/صفحه‌بندی/Projection همه در SQL - نه ToListAsync
    // بدون Skip/Take. GetAllAsync دست‌نخورده می‌ماند (فراخوان‌کننده‌ی دیگری ندارد).
    public async Task<PagedResult<EquipmentDto>> GetPagedAsync(
        int page = 1, int pageSize = 50, int? projectId = null, int? disciplineId = null,
        string? equipmentType = null, string? status = null, string? q = null)
    {
        var query = _context.Equipments.Where(x => x.IsActive).AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (!string.IsNullOrWhiteSpace(equipmentType)) query = query.Where(x => x.EquipmentType == equipmentType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.TagNumber.ToLower().Contains(term) || x.Name.ToLower().Contains(term)
                || (x.Manufacturer != null && x.Manufacturer.ToLower().Contains(term))
                || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.TagNumber)
            .ThenBy(x => x.EquipmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EquipmentDto
            {
                EquipmentId = x.EquipmentId,
                TagNumber = x.TagNumber,
                Name = x.Name,
                EquipmentType = x.EquipmentType,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                DisciplineId = x.DisciplineId,
                DisciplineName = x.Discipline!.Name,
                Manufacturer = x.Manufacturer,
                Model = x.Model,
                Status = x.Status,
                DocumentsCount = x.EngineeringDocuments.Count(d => d.IsActive),
                VDRItemsCount = _context.VDRItems.Count(v => v.EquipmentId == x.EquipmentId && v.IsActive),
                WorkOrdersCount = _context.WorkOrders.Count(w => w.EquipmentId == x.EquipmentId && w.IsActive)
            })
            .ToListAsync();

        return new PagedResult<EquipmentDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }


    public async Task<int> CreateAsync(Equipment equipment)
    {
        equipment.CreatedDate = DateTime.Now;
        await _context.Equipments.AddAsync(equipment);
        await _context.SaveChangesAsync();
        return equipment.EquipmentId;
    }

    // طبق CMMS Production Baseline v1 بند ۷: اتصال واقعی به FunctionalLocation و Parent
    // Equipment - همیشه با تایید صریح کاربر، هرگز حدسی از روی فیلد متنی Area.
    public async Task<(bool Success, string? Error)> AssignFoundationAsync(
        int equipmentId, int? functionalLocationId, int? parentEquipmentId, DateTime? installationDate, string? currentUserName)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);
        if (equipment == null) return (false, "تجهیز پیدا نشد.");

        if (parentEquipmentId == equipmentId)
            return (false, "یک تجهیز نمی‌تواند Parent خودش باشد.");

        if (parentEquipmentId.HasValue)
        {
            if (!await _context.Equipments.AnyAsync(x => x.EquipmentId == parentEquipmentId.Value && x.IsActive))
                return (false, "تجهیز Parent انتخاب‌شده نامعتبره.");

            if (await WouldCreateCycleAsync(equipmentId, parentEquipmentId.Value))
                return (false, "این انتخاب باعث یک حلقه در ساختار درختی تجهیزات می‌شود.");
        }

        equipment.FunctionalLocationId = functionalLocationId;
        equipment.ParentEquipmentId = parentEquipmentId;
        equipment.InstallationDate = installationDate;
        equipment.ModifiedBy = currentUserName;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<bool> WouldCreateCycleAsync(int equipmentId, int proposedParentId)
    {
        var all = await _context.Equipments.Select(x => new { x.EquipmentId, x.ParentEquipmentId }).ToListAsync();
        var byId = all.ToDictionary(x => x.EquipmentId, x => x.ParentEquipmentId);

        var cursor = (int?)proposedParentId;
        while (cursor.HasValue)
        {
            if (cursor.Value == equipmentId) return true;
            byId.TryGetValue(cursor.Value, out cursor);
        }

        return false;
    }

    public async Task<EquipmentPassportDto?> GetPassportAsync(int equipmentId)
    {
        var equipment = await _context.Equipments
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.RelatedMR).ThenInclude(m => m!.Project)
            .Include(x => x.RelatedMR).ThenInclude(m => m!.Discipline)
            .Include(x => x.ParentEquipment)
            .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);

        if (equipment == null) return null;

        var children = await _context.Equipments
            .Where(x => x.ParentEquipmentId == equipmentId && x.IsActive)
            .Select(x => new ChildEquipmentSummaryDto { EquipmentId = x.EquipmentId, TagNumber = x.TagNumber, Name = x.Name })
            .ToListAsync();

        var passport = new EquipmentPassportDto
        {
            Equipment = new EquipmentDto
            {
                EquipmentId = equipment.EquipmentId,
                TagNumber = equipment.TagNumber,
                Name = equipment.Name,
                EquipmentType = equipment.EquipmentType,
                ProjectId = equipment.ProjectId,
                ProjectName = equipment.Project?.ProjectName ?? "",
                DisciplineId = equipment.DisciplineId,
                DisciplineName = equipment.Discipline?.Name ?? "",
                Manufacturer = equipment.Manufacturer,
                Model = equipment.Model,
                Status = equipment.Status,
                Description = equipment.Description,
                Area = equipment.Area,
                PIDNumber = equipment.PIDNumber,
                FunctionalLocationId = equipment.FunctionalLocationId,
                FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(equipment.FunctionalLocationId),
                ParentEquipmentId = equipment.ParentEquipmentId,
                ParentEquipmentTagNumber = equipment.ParentEquipment?.TagNumber,
                InstallationDate = equipment.InstallationDate,
                ChildEquipmentCount = children.Count,
                ChildEquipments = children,
                CriticalityClass = await _context.EquipmentCriticalities
                    .Where(x => x.EquipmentId == equipmentId && x.IsActive)
                    .Select(x => x.CriticalityClass)
                    .FirstOrDefaultAsync()
            }
        };

        // Engineering Documents (Related Equipment FK)
        var documents = await _context.EngineeringDocuments
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .ToListAsync();

        passport.Documents = documents.Select(d => new EngineeringDocumentDto
        {
            DocumentId = d.DocumentId,
            DocumentNo = d.DocumentNo,
            Title = d.Title,
            DocumentType = d.DocumentType,
            ProjectId = d.ProjectId,
            ProjectName = d.Project?.ProjectName ?? "",
            DisciplineId = d.DisciplineId ?? 0,
            DisciplineName = d.Discipline?.Name ?? "",
            Status = d.Status,
            RevisionNo = d.RevisionNo,
            CreatedDate = d.CreatedDate
        }).ToList();

        // ==============================================================================
        // طبق درخواست: کلیک روی شماره P&ID باید مستقیم به مدرک مهندسی متناظر منتقل کند. طبق
        // رفع باگ گزارش‌شده: تطبیق کامل (==) کافی نبود چون شماره‌ی ثبت‌شده روی تجهیز گاهی
        // پسوند اضافه (مثلاً شماره‌ی رویژن) در انتها دارد که در DocumentNo واقعی مدرک نیست.
        // حالا از DocumentNumberMatcher (سه‌لایه: کامل → بدون پسوند رویژن → ابتدایی دوطرفه)
        // استفاده می‌شود - نامزدها به همون پروژه محدود می‌شوند تا هم دقیق‌تر باشه هم سریع.
        // ==============================================================================
        if (!string.IsNullOrWhiteSpace(equipment.PIDNumber))
        {
            var candidateDocs = await _context.EngineeringDocuments
                .Where(x => x.IsActive && x.ProjectId == equipment.ProjectId)
                .Select(x => new { x.DocumentId, x.DocumentNo })
                .ToListAsync();

            var bestMatch = PEMS.Shared.Search.DocumentNumberMatcher.FindBestMatch(
                candidateDocs, equipment.PIDNumber, c => c.DocumentNo);

            if (bestMatch != null)
            {
                var pidDoc = await _context.EngineeringDocuments
                    .Include(x => x.Project)
                    .Include(x => x.Discipline)
                    .FirstOrDefaultAsync(x => x.DocumentId == bestMatch.DocumentId);

                if (pidDoc != null)
                {
                    passport.PIDDocument = new EngineeringDocumentDto
                    {
                        DocumentId = pidDoc.DocumentId,
                        DocumentNo = pidDoc.DocumentNo,
                        Title = pidDoc.Title,
                        DocumentType = pidDoc.DocumentType,
                        ProjectId = pidDoc.ProjectId,
                        ProjectName = pidDoc.Project?.ProjectName ?? "",
                        DisciplineId = pidDoc.DisciplineId ?? 0,
                        DisciplineName = pidDoc.Discipline?.Name ?? "",
                        Status = pidDoc.Status,
                        RevisionNo = pidDoc.RevisionNo,
                        CreatedDate = pidDoc.CreatedDate
                    };
                }
            }
        }

        // VDR Items (Direct FK)
        var vdrItems = await _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Submissions)
            .Include(x => x.Project)
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .ToListAsync();

        passport.VDRItems = vdrItems.Select(v =>
        {
            var latest = v.Submissions.OrderBy(s => s.RevisionNo).LastOrDefault();

            return new VDRItemDto
            {
                VDRItemId = v.DocumentId,
                DocumentNumber = v.DocumentNumber,
                DocumentDescription = v.DocumentDescription,
                DocumentTypeCode = v.DocumentTypeCode,
                VendorId = v.VendorId,
                VendorName = v.Vendor?.Name ?? "",
                WorkflowStatus = v.WorkflowStatus,
                LatestRevisionNo = latest?.RevisionNo,
                LatestReviewCategory = latest?.ReviewCategory,
                ProjectName = v.Project?.ProjectName ?? ""
            };
        }).ToList();

        // ==============================================================================
        // طبق درخواست: "وقتی رابطه تشخیص داده شد، تمام اطلاعات یکپارچه دیده بشه" - MR ای که
        // از طریق Relationship Explorer صریحاً تایید شده (equipment.RelatedMRId) هم اینجا
        // نشان داده می‌شود، نه فقط MR هایی که به‌صورت متنی EquipmentTagNumber مشابه دارند.
        // ==============================================================================
        var mrs = await _context.MaterialRequisitions
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.EquipmentTagNumber == equipment.TagNumber)
            .ToListAsync();

        var mrDtos = mrs.Select(m => new MRDto
        {
            MRId = m.MRId,
            MRNumber = m.MRNumber,
            ProjectName = m.Project?.ProjectName ?? "",
            DisciplineName = m.Discipline?.Name ?? "",
            EquipmentTagNumber = m.EquipmentTagNumber,
            Description = m.Description,
            Status = m.Status,
            RequiredDate = m.RequiredDate
        }).ToList();

        if (equipment.RelatedMR != null && mrDtos.All(x => x.MRId != equipment.RelatedMR.MRId))
        {
            mrDtos.Insert(0, new MRDto
            {
                MRId = equipment.RelatedMR.MRId,
                MRNumber = equipment.RelatedMR.MRNumber,
                ProjectName = equipment.RelatedMR.Project?.ProjectName ?? "",
                DisciplineName = equipment.RelatedMR.Discipline?.Name ?? "",
                EquipmentTagNumber = equipment.RelatedMR.EquipmentTagNumber,
                Description = equipment.RelatedMR.Description,
                Status = equipment.RelatedMR.Status,
                RequiredDate = equipment.RelatedMR.RequiredDate
            });
        }

        passport.RelatedMRs = mrDtos;

        // پیشنهادهای تاییدنشده‌ی Relationship Explorer (از روی MRNumberRaw/PONumberRaw)
        var exploration = await _relationshipService.ExploreFromEquipmentAsync(equipmentId);
        passport.SuggestedLinks = exploration?.SuggestedLinks ?? new List<RelationshipNodeDto>();

        // ==============================================================================
        // طبق فاز ۳ نقشه‌راه معماری: تعمیق محوریت Asset - این‌ها قبلاً هیچ‌جای نمای تجهیز
        // دیده نمی‌شدند، در حالی که مستقیم روی همین تجهیز اثر دارند.
        // ==============================================================================
        var ncrs = await _context.NonConformanceReports
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        passport.NonConformanceReports = ncrs.Select(n => new RelationshipNodeDto
        {
            NodeType = "NCR",
            NodeId = n.NonConformanceReportId,
            Label = n.NCRNumber,
            SubLabel = $"{n.Severity} — {n.Status}",
            Priority = n.Severity == "Critical" ? "Urgent" : n.Severity == "Major" ? "High" : null,
            PlannedOrDueDate = n.RaisedDate
        }).ToList();

        var mocs = await _context.MOCs
            .Where(x => x.RelatedEquipmentId == equipmentId && x.IsActive)
            .OrderByDescending(x => x.RequestedDate)
            .ToListAsync();

        passport.MOCs = mocs.Select(m => new RelationshipNodeDto
        {
            NodeType = "MOC",
            NodeId = m.MOCId,
            Label = m.MOCNumber,
            SubLabel = $"{m.RiskLevel} — {m.Status}",
            Priority = m.RiskLevel == "High" ? "Urgent" : null,
            PlannedOrDueDate = m.RequestedDate
        }).ToList();

        var tqs = await _context.TechnicalQueries
            .Where(x => x.RelatedEquipmentId == equipmentId && x.IsActive)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        passport.TechnicalQueries = tqs.Select(t => new RelationshipNodeDto
        {
            NodeType = "TechnicalQuery",
            NodeId = t.TechnicalQueryId,
            Label = t.QueryNumber,
            SubLabel = $"{t.QueryType} — {t.Status}",
            Priority = t.IsOverdue ? "Urgent" : null,
            PlannedOrDueDate = t.RequiredResponseDate
        }).ToList();

        // Maintenance History = Work Requests (نه Work Order): درخواست‌هایی که مستقیم به این تجهیز یا
        // به Asset همین تجهیز وصل‌اند. فقط رکوردهای فعال؛ WorkOrder مرتبط (بعد از تبدیل) فقط برای ردیابی.
        var assetIds = await _context.Assets.Where(a => a.EquipmentId == equipmentId).Select(a => a.AssetId).ToListAsync();
        passport.WorkRequests = await _context.WorkRequests
            .Where(r => r.IsActive && (r.EquipmentId == equipmentId || (r.AssetId != null && assetIds.Contains(r.AssetId.Value))))
            .OrderByDescending(r => r.RequestedDate).ThenByDescending(r => r.WorkRequestId)
            .Select(r => new WorkRequestSummaryDto
            {
                WorkRequestId = r.WorkRequestId,
                RequestNumber = r.RequestNumber,
                Title = r.Title,
                Status = r.Status,
                Priority = r.Priority,
                RequestedDate = r.RequestedDate,
                LinkedWorkOrderId = r.LinkedWorkOrderId,
                LinkedWorkOrderNo = r.LinkedWorkOrder != null ? r.LinkedWorkOrder.WorkOrderNumber : null
            }).ToListAsync();

        // استراتژی + PM: روابط موجود دامنه (EquipmentId مستقیم روی MaintenanceStrategy/MaintenancePlan) - رابطه‌ی جدیدی ساخته نشده
        passport.Strategies = await _context.MaintenanceStrategies
            .Where(s => s.EquipmentId == equipmentId && s.IsActive)
            .OrderBy(s => s.StrategyType)
            .Select(s => new EquipmentStrategySummaryDto
            {
                MaintenanceStrategyId = s.MaintenanceStrategyId,
                StrategyType = s.StrategyType,
                RecommendedTriggerType = s.RecommendedTriggerType,
                Frequency = s.Frequency,
                ConsequenceCategory = s.ConsequenceCategory
            }).ToListAsync();
        passport.Plans = await _context.MaintenancePlans
            .Where(p => p.EquipmentId == equipmentId && p.IsActive)
            .OrderBy(p => p.NextDueDate == null).ThenBy(p => p.NextDueDate)
            .Select(p => new EquipmentPlanSummaryDto
            {
                MaintenancePlanId = p.MaintenancePlanId,
                PlanNumber = p.PlanNumber,
                Title = p.Title,
                TriggerType = p.TriggerType,
                IntervalDays = p.IntervalDays,
                LastExecutionDate = p.LastExecutionDate,
                NextDueDate = p.NextDueDate
            }).ToListAsync();

        // Work Orderهای اجراشده‌ی تجهیز (فقط خواندنی، بخش جداگانه)
        var workOrders = await _context.WorkOrders
            .Where(x => x.EquipmentId == equipmentId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        passport.WorkOrders = workOrders.Select(w => new WorkOrderSummaryDto
        {
            WorkOrderId = w.WorkOrderId,
            WorkOrderNo = w.WorkOrderNumber,
            Description = w.Description,
            Status = w.Status,
            CreatedDate = w.CreatedDate
        }).ToList();

        // طبق درخواست: شناسنامه‌ی ابزاردقیق باید بخشی از همین صفحه باشد - همان تجهیزی که در
        // Instrument/Passport دیده می‌شود، این‌جا هم به‌عنوان یک تب اضافه می‌شود
        var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.EquipmentId == equipmentId);
        if (instrument != null)
        {
            passport.Instrument = new InstrumentPassportSummaryDto
            {
                InstrumentId = instrument.InstrumentId,
                InstrumentType = instrument.InstrumentType,
                LoopNumber = instrument.LoopNumber,
                System = instrument.System,
                SignalClass = instrument.SignalClass,
                ProcessVariable = instrument.ProcessVariable,
                RangeMin = instrument.RangeMin,
                RangeMax = instrument.RangeMax,
                Unit = instrument.Unit,
                OutputSignal = instrument.OutputSignal,
                HazardousArea = instrument.HazardousArea,
                ExProtection = instrument.ExProtection,
                SIL = instrument.SIL,
                JunctionBox = instrument.JunctionBox,
                MarshallingCabinet = instrument.MarshallingCabinet,
                CableNumber = instrument.CableNumber,
                IOCardChannel = instrument.IOCardChannel,
                SystemCabinet = instrument.SystemCabinet,
                CalibrationInterval = instrument.CalibrationInterval,
                LastCalibrationDate = instrument.LastCalibrationDate,
                NextCalibrationDue = instrument.NextCalibrationDue,
                CalibrationStatus = GetCalibrationStatus(instrument.NextCalibrationDue),
                LastMaintenanceDate = instrument.LastMaintenanceDate,
                NextMaintenanceDue = instrument.NextMaintenanceDue,
                CurrentStatus = instrument.CurrentStatus
            };
        }

        // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۳:
        // دقیقاً همون الگوی Instrument بالا. EquipmentId روی MechanicalEquipment اختیاری است (زنجیره‌ی
        // Nullable موجود که باید حفظ شود) - پس این‌جا هنوز با FirstOrDefaultAsync پیدا می‌شود، نه Include.
        var mechanical = await _context.MechanicalEquipments.FirstOrDefaultAsync(m => m.EquipmentId == equipmentId && m.IsActive);
        if (mechanical != null)
        {
            var rotating = await _context.RotatingEquipments.FirstOrDefaultAsync(r => r.MechanicalEquipmentId == mechanical.MechanicalEquipmentId && r.IsActive);
            var staticEq = rotating == null
                ? await _context.StaticEquipments.FirstOrDefaultAsync(s => s.MechanicalEquipmentId == mechanical.MechanicalEquipmentId && s.IsActive)
                : null;

            passport.MechanicalEquipment = new MechanicalEquipmentPassportSummaryDto
            {
                MechanicalEquipmentId = mechanical.MechanicalEquipmentId,
                TagNumber = mechanical.TagNumber,
                EquipmentType = mechanical.EquipmentType,
                DesignPressure = mechanical.DesignPressure,
                DesignTemperature = mechanical.DesignTemperature,
                OperatingPressure = mechanical.OperatingPressure,
                OperatingTemperature = mechanical.OperatingTemperature,
                Material = mechanical.Material,
                Manufacturer = mechanical.Manufacturer,
                Model = mechanical.Model,
                SubType = rotating != null ? "Rotating" : staticEq != null ? "Static" : "Unclassified",
                FlowRate = rotating?.FlowRate,
                Head = rotating?.Head,
                SpeedRPM = rotating?.SpeedRPM,
                RotatingPowerKW = rotating?.PowerKW,
                DriverType = rotating?.DriverType,
                Volume = staticEq?.Volume,
                Orientation = staticEq?.Orientation,
                CodeStandard = staticEq?.CodeStandard
            };
        }

        // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §2/§5-1: پایش وضعیت باید
        // همین‌جا هم خلاصه دیده شود - جزئیات کامل همچنان روی Reliability/ConditionMonitoring
        // و Finding/Index می‌مانند. هیچ منطق ارزیابی/محاسبه‌ی تازه‌ای این‌جا نیست - فقط همون
        // Query هایی که ConditionMonitoringService/FindingController از قبل دارند.
        var cmPoints = await _context.ConditionMonitoringPoints
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .Include(x => x.EvaluationProfile)
            .ToListAsync();

        if (cmPoints.Count > 0)
        {
            var pointIds = cmPoints.Select(p => p.ConditionMonitoringPointId).ToList();
            var lastReadings = await _context.ConditionReadings
                .Where(x => pointIds.Contains(x.ConditionMonitoringPointId))
                .GroupBy(x => x.ConditionMonitoringPointId)
                .Select(g => new { PointId = g.Key, Count = g.Count(), Last = g.OrderByDescending(x => x.ReadingDate).First() })
                .ToListAsync();

            var cbmStrategy = await _context.MaintenanceStrategies
                .Where(x => x.EquipmentId == equipmentId && x.IsActive && x.ConditionMonitoringPointId != null && pointIds.Contains(x.ConditionMonitoringPointId!.Value))
                .FirstOrDefaultAsync();

            DateTime? nextDue = null;
            if (cbmStrategy != null)
            {
                nextDue = await _context.MaintenancePlans
                    .Where(x => x.MaintenanceStrategyId == cbmStrategy.MaintenanceStrategyId && x.IsActive)
                    .OrderBy(x => x.NextDueDate)
                    .Select(x => x.NextDueDate)
                    .FirstOrDefaultAsync();
            }

            var openFindingCount = await _context.Findings
                .CountAsync(x => x.EquipmentId == equipmentId && x.IsActive && x.Status == "Open");

            passport.ConditionMonitoring = new ConditionMonitoringPassportSummaryDto
            {
                PointCount = cmPoints.Count,
                OpenFindingCount = openFindingCount,
                HasCbmStrategy = cbmStrategy != null,
                NextDueDate = nextDue,
                Points = cmPoints.Select(p =>
                {
                    var last = lastReadings.FirstOrDefault(x => x.PointId == p.ConditionMonitoringPointId);
                    return new ConditionMonitoringPointDto
                    {
                        ConditionMonitoringPointId = p.ConditionMonitoringPointId,
                        EquipmentId = p.EquipmentId,
                        VariableType = p.VariableType,
                        Tag = p.Tag,
                        Unit = p.Unit,
                        AlarmThresholdLow = p.AlarmThresholdLow,
                        AlarmThresholdHigh = p.AlarmThresholdHigh,
                        ReadingCount = last?.Count ?? 0,
                        LastReadingDate = last?.Last.ReadingDate,
                        LastValue = last?.Last.Value,
                        LastReadingIsAlarm = last?.Last.IsAlarm ?? false,
                        LastReadingId = last?.Last.ConditionReadingId,
                        EvaluationProfileId = p.EvaluationProfileId,
                        EvaluationProfileCode = p.EvaluationProfile?.Code,
                        LastReadingEvaluationStatus = last?.Last.EvaluationStatus ?? "NotEvaluated"
                    };
                }).ToList()
            };
        }

        // طبق PEMS_CMMS_ARCHITECTURE.md: چرخه‌عمر CMMS این تجهیز (در صورت وجود) - همان الگوی
        // Instrument بالا. اهمیت (Criticality) عمداً از خودِ EquipmentCriticality خوانده
        // می‌شود، نه از فیلد قدیمی/تکراری روی Asset.
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.EquipmentId == equipmentId && a.IsActive);
        if (asset != null)
        {
            passport.Asset = new AssetSummaryDto
            {
                AssetId = asset.AssetId,
                AssetNumber = asset.AssetNumber,
                FunctionalLocation = asset.FunctionalLocation,
                MaintenanceClass = asset.MaintenanceClass,
                CommissioningDate = asset.CommissioningDate,
                Status = asset.Status,
                CriticalityClass = passport.Equipment.CriticalityClass
            };
        }

        return passport;
    }

    // همان منطق InstrumentController.CalibrationStatus - برای هم‌خوانی وضعیت بین Master Instrument
    // List، صفحه‌ی Passport اختصاصی، و همین خلاصه‌ی داخل Equipment/Details
    private static string GetCalibrationStatus(DateTime? nextDue)
    {
        if (!nextDue.HasValue) return "Unknown";
        if (nextDue.Value.Date < DateTime.Today) return "Overdue";
        if (nextDue.Value.Date <= DateTime.Today.AddDays(30)) return "DueSoon";
        return "OK";
    }
}
