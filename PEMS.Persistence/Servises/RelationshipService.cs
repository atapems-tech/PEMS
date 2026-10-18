using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// AI Relationship Engine: زنجیره‌ی روابط مهندسی را از روی FK های موجود (قابل‌اعتماد، بدون خطای AI)
/// می‌سازد - طبق مثال سند: Equipment → Engineering/Vendor Documents → Vendor → PO → Contract،
/// و Equipment → MR → MRQ، و Equipment → Work Order.
/// </summary>
public class RelationshipService : IRelationshipService
{
    private readonly PEMSDbContext _context;

    public RelationshipService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<RelationshipExplorationDto?> ExploreFromEquipmentAsync(int equipmentId)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);
        if (equipment == null) return null;

        return await BuildFromEquipmentAsync(equipment);
    }

    public async Task<RelationshipExplorationDto?> ExploreFromDocumentAsync(int documentId)
    {
        var engDoc = await _context.EngineeringDocuments
            .Include(x => x.Vendor)
            .Include(x => x.Contract)
            .Include(x => x.RelatedMR)
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        if (engDoc != null)
        {
            RelationshipExplorationDto result;

            if (engDoc.Equipment != null)
                result = (await BuildFromEquipmentAsync(engDoc.Equipment))!;
            else
                result = new RelationshipExplorationDto();

            result.RootType = "Document";
            result.RootId = engDoc.DocumentId;
            result.RootLabel = $"{engDoc.DocumentNo} - {engDoc.Title}";

            AddUnique(result.Documents, "Document", engDoc.DocumentId, $"{engDoc.DocumentNo} - {engDoc.Title}", engDoc.Status);

            if (engDoc.Vendor != null)
                AddUnique(result.Vendors, "Vendor", engDoc.Vendor.VendorId, engDoc.Vendor.Name, engDoc.Vendor.RiskLevel);

            if (engDoc.Contract != null)
                AddUnique(result.Contracts, "Contract", engDoc.Contract.ContractId, engDoc.Contract.ContractNo, engDoc.Contract.Status);

            if (engDoc.RelatedMR != null)
                AddUnique(result.MaterialRequisitions, "MR", engDoc.RelatedMR.MRId, engDoc.RelatedMR.MRNumber, engDoc.RelatedMR.Status);

            // تگ‌های مرتبط از طریق DocumentTagLink
            var tagLinks = await _context.DocumentTagLinks
                .Include(x => x.EquipmentTag)!.ThenInclude(t => t!.Equipment)
                .Where(x => x.DocumentId == documentId && x.ReviewStatus == "Approved")
                .ToListAsync();

            foreach (var link in tagLinks.Where(l => l.EquipmentTag?.Equipment != null))
                AddUnique(result.Equipment, "Equipment", link.EquipmentTag!.Equipment!.EquipmentId,
                    link.EquipmentTag.Equipment.TagNumber, link.EquipmentTag.Equipment.Name);

            await MergeAIProposedRelationshipsAsync(result, "Document", documentId);

            return result;
        }

        // شاید مدرک Vendor (VDRItem) باشد
        var vdrItem = await _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Equipment)
            .Include(x => x.Po)!.ThenInclude(p => p!.Contract)
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        if (vdrItem != null)
        {
            RelationshipExplorationDto result;

            if (vdrItem.Equipment != null)
                result = (await BuildFromEquipmentAsync(vdrItem.Equipment))!;
            else
                result = new RelationshipExplorationDto();

            result.RootType = "Document";
            result.RootId = vdrItem.DocumentId;
            result.RootLabel = $"{vdrItem.DocumentNumber} - {vdrItem.DocumentDescription}";

            AddUnique(result.VendorDocuments, "VDR", vdrItem.DocumentId, vdrItem.DocumentNumber, vdrItem.WorkflowStatus);

            if (vdrItem.Vendor != null)
                AddUnique(result.Vendors, "Vendor", vdrItem.Vendor.VendorId, vdrItem.Vendor.Name, vdrItem.Vendor.RiskLevel);

            if (vdrItem.Po != null)
            {
                AddUnique(result.PurchaseOrders, "PurchaseOrder", vdrItem.Po.PoId, vdrItem.Po.PoNumber, vdrItem.Po.Status);

                if (vdrItem.Po.Contract != null)
                    AddUnique(result.Contracts, "Contract", vdrItem.Po.Contract.ContractId, vdrItem.Po.Contract.ContractNo, vdrItem.Po.Contract.Status);
            }

            await MergeAIProposedRelationshipsAsync(result, "Document", documentId);

            return result;
        }

        return null;
    }

    public async Task<RelationshipExplorationDto?> ExploreFromVendorAsync(int vendorId)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == vendorId);
        if (vendor == null) return null;

        var result = new RelationshipExplorationDto
        {
            RootType = "Vendor",
            RootId = vendor.VendorId,
            RootLabel = vendor.Name
        };

        AddUnique(result.Vendors, "Vendor", vendor.VendorId, vendor.Name, vendor.RiskLevel);

        var vdrItems = await _context.VDRItems
            .Include(x => x.Equipment)
            .Where(x => x.VendorId == vendorId && x.IsActive)
            .ToListAsync();

        foreach (var v in vdrItems)
        {
            AddUnique(result.VendorDocuments, "VDR", v.DocumentId, v.DocumentNumber, v.WorkflowStatus);

            if (v.Equipment != null)
                AddUnique(result.Equipment, "Equipment", v.Equipment.EquipmentId, v.Equipment.TagNumber, v.Equipment.Name);
        }

        var contracts = await _context.Contracts.Where(x => x.VendorId == vendorId).ToListAsync();
        foreach (var c in contracts)
            AddUnique(result.Contracts, "Contract", c.ContractId, c.ContractNo, c.Status);

        var pos = await _context.PurchaseOrders.Where(x => x.VendorId == vendorId).ToListAsync();
        foreach (var p in pos)
            AddUnique(result.PurchaseOrders, "PurchaseOrder", p.PoId, p.PoNumber, p.Status);

        // طبق گزارش سخت‌افزاری نهایی (Gap ۴): مناقصه‌هایی که این Vendor به آن‌ها دعوت شده
        var invitedTenders = await _context.TenderInvitedVendors
            .Include(x => x.Tender)
            .Where(x => x.VendorId == vendorId && x.Tender != null)
            .Select(x => x.Tender!)
            .ToListAsync();
        foreach (var t in invitedTenders)
            AddUnique(result.Tenders, "Tender", t.TenderId, t.TenderNumber, t.Status);

        // پیشنهادهایی که این Vendor واقعاً ثبت کرده
        var bids = await _context.Bids.Where(x => x.VendorId == vendorId).ToListAsync();
        foreach (var b in bids)
            AddUnique(result.Bids, "Bid", b.BidId, b.BidNumber, b.Status);

        // اخطارهای شروع به کار صادرشده روی قراردادهای همین Vendor
        var ntps = await _context.ContractNTPs
            .Where(x => contracts.Select(c => c.ContractId).Contains(x.ContractId))
            .ToListAsync();
        foreach (var n in ntps)
            AddUnique(result.ContractNTPs, "ContractNTP", n.ContractNTPId, n.NTPNumber, n.Status);

        // جلسات Kick-off همان قراردادها - از همان زیرساخت Meeting موجود
        var kickoffs = await _context.Meetings
            .Where(x => x.MeetingType == "KickOff" && x.ContractId != null && contracts.Select(c => c.ContractId).Contains(x.ContractId!.Value))
            .ToListAsync();
        foreach (var m in kickoffs)
            AddUnique(result.KickoffMeetings, "Meeting", m.MeetingId, m.Title, m.Status);

        var quotations = await _context.MaterialRequisitionQuotations
            .Where(x => x.VendorName == vendor.Name)
            .ToListAsync();
        foreach (var q in quotations)
            AddUnique(result.Quotations, "MRQ", q.MRQId, q.MRQNumber, q.QuotationStatus);

        return result;
    }

    // ==============================================================================
    // طبق ممیزی نهایی Ownership/Traceability (§16): زنجیره‌ی Package→Tender→Bid→Award→Contract→
    // NTP→Kickoff تا این‌جا هیچ نقطه‌ی ورود مستقلی نداشت - فقط از طریق Vendor به‌صورت جزئی دیده
    // می‌شد. هیچ FK/جدول تازه‌ای لازم نیست؛ فقط همان روابط موجود (Tender.ProcurementPackageId،
    // Tender.ContractId، Contract.VendorId، ContractNTP.ContractId، Meeting.ContractId) خوانده
    // می‌شوند.
    // ==============================================================================
    public async Task<RelationshipExplorationDto?> ExploreFromProcurementPackageAsync(int procurementPackageId)
    {
        var package = await _context.ProcurementPackages
            .Include(x => x.MaterialRequisitions)
            .Include(x => x.Quotations)
            .FirstOrDefaultAsync(x => x.ProcurementPackageId == procurementPackageId);
        if (package == null) return null;

        var result = new RelationshipExplorationDto
        {
            RootType = "ProcurementPackage",
            RootId = package.ProcurementPackageId,
            RootLabel = $"{package.PackageNumber} - {package.Title}",
            RootIsActive = package.IsActive
        };

        foreach (var mr in package.MaterialRequisitions)
            AddUnique(result.MaterialRequisitions, "MR", mr.MRId, mr.MRNumber, mr.Status);

        foreach (var q in package.Quotations)
            AddUnique(result.Quotations, "MRQ", q.MRQId, q.MRQNumber, q.QuotationStatus);

        var packageItps = await _context.InspectionTestPlans.Where(x => x.ProcurementPackageId == procurementPackageId).ToListAsync();
        foreach (var i in packageItps)
            AddUnique(result.InspectionTestPlans, "InspectionTestPlan", i.InspectionTestPlanId, i.ITPNumber, i.Status);

        var tenders = await _context.Tenders
            .Include(x => x.AwardedBid)!.ThenInclude(b => b!.Vendor)
            .Include(x => x.Contract)
            .Where(x => x.ProcurementPackageId == procurementPackageId)
            .ToListAsync();

        foreach (var t in tenders)
        {
            AddUnique(result.Tenders, "Tender", t.TenderId, t.TenderNumber, t.Status);

            var bids = await _context.Bids.Include(x => x.Vendor).Where(x => x.TenderId == t.TenderId).ToListAsync();
            foreach (var b in bids)
            {
                AddUnique(result.Bids, "Bid", b.BidId, b.BidNumber, b.Status);
                if (b.Vendor != null)
                    AddUnique(result.Vendors, "Vendor", b.Vendor.VendorId, b.Vendor.Name, b.Vendor.RiskLevel);
            }

            if (t.Contract != null)
            {
                AddUnique(result.Contracts, "Contract", t.Contract.ContractId, t.Contract.ContractNo, t.Contract.Status);

                var ntps = await _context.ContractNTPs.Where(x => x.ContractId == t.Contract.ContractId).ToListAsync();
                foreach (var n in ntps)
                    AddUnique(result.ContractNTPs, "ContractNTP", n.ContractNTPId, n.NTPNumber, n.Status);

                var kickoffs = await _context.Meetings
                    .Where(x => x.MeetingType == "KickOff" && x.ContractId == t.Contract.ContractId)
                    .ToListAsync();
                foreach (var m in kickoffs)
                    AddUnique(result.KickoffMeetings, "Meeting", m.MeetingId, m.Title, m.Status);

                var pos = await _context.PurchaseOrders.Where(x => x.ContractId == t.Contract.ContractId).ToListAsync();
                foreach (var p in pos)
                    AddUnique(result.PurchaseOrders, "PurchaseOrder", p.PoId, p.PoNumber, p.Status);

                var contractItps = await _context.InspectionTestPlans.Where(x => x.ContractId == t.Contract.ContractId).ToListAsync();
                foreach (var i in contractItps)
                    AddUnique(result.InspectionTestPlans, "InspectionTestPlan", i.InspectionTestPlanId, i.ITPNumber, i.Status);
            }
        }

        return result;
    }

    public async Task<RelationshipExplorationDto?> ExploreFromContractAsync(int contractId)
    {
        var contract = await _context.Contracts.Include(x => x.Vendor).FirstOrDefaultAsync(x => x.ContractId == contractId);
        if (contract == null) return null;

        var result = new RelationshipExplorationDto
        {
            RootType = "Contract",
            RootId = contract.ContractId,
            RootLabel = $"{contract.ContractNo}"
        };

        if (contract.Vendor != null)
            AddUnique(result.Vendors, "Vendor", contract.Vendor.VendorId, contract.Vendor.Name, contract.Vendor.RiskLevel);

        // طبق ممیزی (§6): تنها منبع حقیقت رابطه‌ی Contract→Tender، Tender.ContractId است -
        // نه یک FK تازه‌ی روی Contract که با Phase 3 ناسازگار می‌شد.
        var originatingTender = await _context.Tenders
            .Include(x => x.ProcurementPackage)
            .Include(x => x.AwardedBid)!.ThenInclude(b => b!.Vendor)
            .FirstOrDefaultAsync(x => x.ContractId == contractId);

        if (originatingTender != null)
        {
            AddUnique(result.Tenders, "Tender", originatingTender.TenderId, originatingTender.TenderNumber, originatingTender.Status);

            if (originatingTender.ProcurementPackage != null)
            {
                var pkg = originatingTender.ProcurementPackage;
                AddUnique(result.ProcurementPackages, "ProcurementPackage", pkg.ProcurementPackageId, pkg.PackageNumber, pkg.Title);
            }

            var bids = await _context.Bids.Include(x => x.Vendor).Where(x => x.TenderId == originatingTender.TenderId).ToListAsync();
            foreach (var b in bids)
                AddUnique(result.Bids, "Bid", b.BidId, b.BidNumber, b.Status);
        }

        var ntps = await _context.ContractNTPs.Where(x => x.ContractId == contractId).ToListAsync();
        foreach (var n in ntps)
            AddUnique(result.ContractNTPs, "ContractNTP", n.ContractNTPId, n.NTPNumber, n.Status);

        var kickoffs = await _context.Meetings
            .Where(x => x.MeetingType == "KickOff" && x.ContractId == contractId)
            .ToListAsync();
        foreach (var m in kickoffs)
            AddUnique(result.KickoffMeetings, "Meeting", m.MeetingId, m.Title, m.Status);

        var pos = await _context.PurchaseOrders.Where(x => x.ContractId == contractId).ToListAsync();
        foreach (var p in pos)
            AddUnique(result.PurchaseOrders, "PurchaseOrder", p.PoId, p.PoNumber, p.Status);

        var itps = await _context.InspectionTestPlans.Where(x => x.ContractId == contractId).ToListAsync();
        foreach (var i in itps)
            AddUnique(result.InspectionTestPlans, "InspectionTestPlan", i.InspectionTestPlanId, i.ITPNumber, i.Status);

        return result;
    }

    public async Task<RelationshipExplorationDto?> ExploreByFreeTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // طبق رفع باگ ممیزی (بند ۱.۳): الگوی قبلی فقط تگ‌هایی که با حرف شروع می‌شدن رو
        // می‌گرفت (مثل P-101) - تگ‌های واقعی‌ای که با عدد شروع می‌شن (مثل "80-P-6005" که در
        // فایل نمونه‌ی MDR واقعی دیدیم) پیدا نمی‌شدن. حالا هر دو حالت پوشش داده می‌شه،
        // با حداکثر ۲ خط‌تیره (segment) تا خیلی عمومی/پرسروصدا نشه
        var tagMatch = Regex.Match(text, @"\b[A-Za-z0-9]{1,6}-[A-Za-z0-9]{1,6}(?:-[A-Za-z0-9]{1,6})?\b");

        if (tagMatch.Success)
        {
            var equipment = await _context.Equipments
                .FirstOrDefaultAsync(x => x.TagNumber.ToUpper() == tagMatch.Value.ToUpper());

            if (equipment != null)
                return await BuildFromEquipmentAsync(equipment);

            var vdrByDoc = await _context.EngineeringDocuments
                .FirstOrDefaultAsync(x => x.DocumentNo.ToUpper() == tagMatch.Value.ToUpper());

            if (vdrByDoc != null)
                return await ExploreFromDocumentAsync(vdrByDoc.DocumentId);
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        foreach (var word in words)
        {
            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(x => x.Name.ToLower().Contains(word.ToLower()));

            if (vendor != null)
                return await ExploreFromVendorAsync(vendor.VendorId);
        }

        return null;
    }

    public async Task AddAIProposedRelationshipAsync(string sourceType, int sourceId, string targetType, int targetId,
        string relationshipType, double confidence)
    {
        await _context.EngineeringRelationships.AddAsync(new EngineeringRelationship
        {
            SourceType = sourceType,
            SourceId = sourceId,
            TargetType = targetType,
            TargetId = targetId,
            RelationshipType = relationshipType,
            Confidence = confidence,
            IsAIProposed = true,
            ReviewStatus = "Draft",
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }


    // =========================
    // Helpers
    // =========================

    private async Task<RelationshipExplorationDto> BuildFromEquipmentAsync(Equipment equipment)
    {
        var result = new RelationshipExplorationDto
        {
            RootType = "Equipment",
            RootId = equipment.EquipmentId,
            RootLabel = $"{equipment.TagNumber} - {equipment.Name}",
            // طبق ممیزی بند ۴.۲ و ۳.۲: Area و وضعیت عملیاتی خودِ تجهیز، برای دید فرآیند/تولید
            RootArea = equipment.Area,
            RootIsActive = equipment.IsActive
        };

        AddUnique(result.Equipment, "Equipment", equipment.EquipmentId, equipment.TagNumber, equipment.Name);

        // طبق ممیزی بند ۲.۲: تاریخ برنامه‌ریزی‌شده/تاخیر مدارک، نه فقط Status خام
        var engDocs = await _context.EngineeringDocuments
            .Where(x => x.EquipmentId == equipment.EquipmentId && x.IsActive)
            .ToListAsync();
        foreach (var d in engDocs)
            AddUnique(result.Documents, "Document", d.DocumentId, $"{d.DocumentNo} - {d.Title}", d.Status,
                plannedOrDueDate: d.PlannedDate, delayDays: d.DelayDays);

        // ==============================================================================
        // طبق ممیزی بند ۴.۱: خلأ بزرگ - P&ID تجهیز که با تطبیق هوشمند سه‌لایه
        // (DocumentNumberMatcher، همون منطقی که برای Digital Equipment Passport ساختیم) پیدا
        // می‌شد، توی این کاوشگر اصلاً نبود. حالا با همون منطق مشترک (نه پیاده‌سازی تکراری)
        // اضافه می‌شود - محدود به همون پروژه برای دقت و سرعت.
        // ==============================================================================
        if (!string.IsNullOrWhiteSpace(equipment.PIDNumber))
        {
            var pidCandidates = engDocs.Any()
                ? engDocs.Select(d => new { d.DocumentId, d.DocumentNo }).ToList()
                : await _context.EngineeringDocuments
                    .Where(x => x.IsActive && x.ProjectId == equipment.ProjectId)
                    .Select(x => new { x.DocumentId, x.DocumentNo })
                    .ToListAsync();

            var pidMatch = PEMS.Shared.Search.DocumentNumberMatcher.FindBestMatch(
                pidCandidates, equipment.PIDNumber, c => c.DocumentNo);

            if (pidMatch != null)
            {
                var pidDoc = engDocs.FirstOrDefault(d => d.DocumentId == pidMatch.DocumentId)
                    ?? await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == pidMatch.DocumentId);

                if (pidDoc != null)
                {
                    result.PIDDocument = new RelationshipNodeDto
                    {
                        NodeType = "Document",
                        NodeId = pidDoc.DocumentId,
                        Label = pidDoc.DocumentNo,
                        SubLabel = pidDoc.Title
                    };
                }
            }
        }

        var vdrItems = await _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Po)!.ThenInclude(p => p!.Contract)
            .Where(x => x.EquipmentId == equipment.EquipmentId && x.IsActive)
            .ToListAsync();

        foreach (var v in vdrItems)
        {
            AddUnique(result.VendorDocuments, "VDR", v.DocumentId, v.DocumentNumber, v.WorkflowStatus,
                plannedOrDueDate: v.PlannedDate);

            if (v.Vendor != null)
                AddUnique(result.Vendors, "Vendor", v.Vendor.VendorId, v.Vendor.Name, v.Vendor.RiskLevel);

            if (v.Po != null)
            {
                AddUnique(result.PurchaseOrders, "PurchaseOrder", v.Po.PoId, v.Po.PoNumber, v.Po.Status);

                if (v.Po.Contract != null)
                    AddUnique(result.Contracts, "Contract", v.Po.Contract.ContractId, v.Po.Contract.ContractNo, v.Po.Contract.Status);
            }
        }

        // ==============================================================================
        // رفع باگ بحرانی ممیزی بند ۱.۱: قبلاً MR فقط با تطبیق متنی TagNumber پیدا می‌شد. از
        // وقتی (طبق تصمیم قبلی) تگ‌های تکراری با Description متفاوت مجازند، همین تطبیق متنی
        // می‌توانست MR یک تجهیز کاملاً متفاوت را نشان دهد. حالا:
        //   ۱. MR تاییدشده (equipment.RelatedMRId - رابطه‌ی واقعی FK) همیشه با IsConfirmed=true
        //      و مجزا نشان داده می‌شود.
        //   ۲. تطبیق‌های صرفاً متنی به همان پروژه محدود می‌شوند (نه کل سیستم) و با
        //      IsConfirmed=false علامت می‌خورند - تا کاربر بداند این‌ها «احتمالی» هستند، نه قطعی.
        // ==============================================================================
        if (equipment.RelatedMRId.HasValue)
        {
            var confirmedMr = await _context.MaterialRequisitions
                .FirstOrDefaultAsync(x => x.MRId == equipment.RelatedMRId.Value);

            if (confirmedMr != null)
            {
                AddUnique(result.MaterialRequisitions, "MR", confirmedMr.MRId, confirmedMr.MRNumber, confirmedMr.Status,
                    plannedOrDueDate: confirmedMr.RequiredDate, isConfirmed: true);
            }
        }

        var textMatchedMrs = await _context.MaterialRequisitions
            .Where(x => x.EquipmentTagNumber == equipment.TagNumber && x.ProjectId == equipment.ProjectId)
            .ToListAsync();

        foreach (var mr in textMatchedMrs)
        {
            var isConfirmed = equipment.RelatedMRId.HasValue && equipment.RelatedMRId.Value == mr.MRId;
            AddUnique(result.MaterialRequisitions, "MR", mr.MRId, mr.MRNumber,
                isConfirmed ? mr.Status : $"{mr.Status} (احتمالی - فقط طبق تطبیق شماره‌تگ)",
                plannedOrDueDate: mr.RequiredDate, isConfirmed: isConfirmed);
        }

        // رفع باگ N+1 ممیزی بند ۱.۲: به‌جای یک Query جدا برای هر MR، همه‌ی Quotationهای
        // مرتبط با تمام MR های این تجهیز یک‌جا واکشی می‌شوند
        var allMrIds = result.MaterialRequisitions.Select(x => x.NodeId).Distinct().ToList();
        if (allMrIds.Count > 0)
        {
            var allQuotations = await _context.MaterialRequisitionQuotations
                .Where(x => allMrIds.Contains(x.MRId))
                .ToListAsync();

            foreach (var q in allQuotations)
                AddUnique(result.Quotations, "MRQ", q.MRQId, q.MRQNumber, q.QuotationStatus);
        }

        // طبق ممیزی بند ۳.۱: اولویت و تاریخ شروع WorkOrder، نه فقط Status خام - مدیر تولید
        // برای تصمیم سریع به این‌ها نیاز داره
        var workOrders = await _context.WorkOrders
            .Where(x => x.EquipmentId == equipment.EquipmentId)
            .ToListAsync();

        foreach (var w in workOrders)
            AddUnique(result.WorkOrders, "WorkOrder", w.WorkOrderId, w.WorkOrderNumber ?? ("WO-" + w.WorkOrderId), w.Status,
                plannedOrDueDate: w.StartDate, priority: w.Priority);

        // ==============================================================================
        // طبق فاز ۳ نقشه‌راه معماری: تعمیق محوریت Asset - قبلاً این‌ها هیچ‌جای Explorer دیده
        // نمی‌شدند، در حالی که مستقیم روی همین تجهیز اثر دارند.
        // ==============================================================================
        var ncrs = await _context.NonConformanceReports
            .Where(x => x.EquipmentId == equipment.EquipmentId && x.IsActive)
            .ToListAsync();
        foreach (var n in ncrs)
            AddUnique(result.NonConformanceReports, "NCR", n.NonConformanceReportId, n.NCRNumber, $"{n.Severity} — {n.Status}",
                plannedOrDueDate: n.RaisedDate, priority: n.Severity == "Critical" ? "Urgent" : null);

        var mocs = await _context.MOCs
            .Where(x => x.RelatedEquipmentId == equipment.EquipmentId && x.IsActive)
            .ToListAsync();
        foreach (var m in mocs)
            AddUnique(result.MOCs, "MOC", m.MOCId, m.MOCNumber, $"{m.RiskLevel} — {m.Status}",
                plannedOrDueDate: m.RequestedDate, priority: m.RiskLevel == "High" ? "Urgent" : null);

        var tqs = await _context.TechnicalQueries
            .Where(x => x.RelatedEquipmentId == equipment.EquipmentId && x.IsActive)
            .ToListAsync();
        foreach (var t in tqs)
            AddUnique(result.TechnicalQueries, "TechnicalQuery", t.TechnicalQueryId, t.QueryNumber, $"{t.QueryType} — {t.Status}",
                plannedOrDueDate: t.RequiredResponseDate, priority: t.IsOverdue ? "Urgent" : null);

        // ==============================================================================
        // رفع باگ ایمپورت تجهیزات: اگر تجهیز شماره‌ی MRQ/PO خام دارد (از ستون اکسل، بدون
        // اتصال خودکار)، این‌جا فقط در داده‌ی موجود (نه ساخت جدید) دنبال منطبق می‌گردیم و
        // به‌عنوان پیشنهاد (نه رابطه‌ی قطعی) نشان می‌دهیم. کاربر باید صریحاً تایید کند
        // (ConfirmSuggestedLinkAsync) تا FK واقعی برقرار شود.
        // ==============================================================================
        if (!string.IsNullOrWhiteSpace(equipment.MRNumberRaw) && !equipment.RelatedMRId.HasValue)
        {
            var candidateMr = await _context.MaterialRequisitions
                .FirstOrDefaultAsync(x => x.MRNumber.ToLower() == equipment.MRNumberRaw!.Trim().ToLower());

            if (candidateMr != null && result.MaterialRequisitions.All(x => x.NodeId != candidateMr.MRId))
            {
                result.SuggestedLinks.Add(new RelationshipNodeDto
                {
                    NodeType = "MR",
                    NodeId = candidateMr.MRId,
                    Label = candidateMr.MRNumber,
                    SubLabel = $"طبق شماره‌ی خام «{equipment.MRNumberRaw}» - نیاز به تایید",
                    IsAIProposed = true,
                    IsConfirmed = false
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(equipment.PONumberRaw))
        {
            var candidateVdrItem = await _context.VDRItems
                .Include(x => x.Po)
                .Where(x => x.EquipmentId == null && x.Po != null && x.Po.PoNumber.ToLower() == equipment.PONumberRaw!.Trim().ToLower())
                .FirstOrDefaultAsync();

            if (candidateVdrItem != null)
            {
                result.SuggestedLinks.Add(new RelationshipNodeDto
                {
                    NodeType = "VDR",
                    NodeId = candidateVdrItem.DocumentId,
                    Label = candidateVdrItem.DocumentNumber,
                    SubLabel = $"طبق شماره‌ی PO خام «{equipment.PONumberRaw}» - نیاز به تایید",
                    IsAIProposed = true,
                    IsConfirmed = false
                });
            }
        }

        await MergeAIProposedRelationshipsAsync(result, "Equipment", equipment.EquipmentId);

        return result;
    }


    // ==============================================================================
    // تنها نقطه‌ای که FK واقعی (RelatedMRId یا VDRItem.EquipmentId) برقرار می‌شود - همیشه با
    // تایید صریح کاربر، هرگز خودکار حین ایمپورت یا Explore.
    //
    // رفع باگ ممیزی بند ۱.۴: قبلاً بدون هیچ چکی رابطه‌ی موجود را رونویسی می‌کرد - اگر VDR/MR
    // از قبل به یک تجهیز دیگر وصل بود، آن رابطه بی‌صدا از بین می‌رفت. حالا بدون force=true
    // با خطای صریح متوقف می‌شود تا کاربر آگاهانه تصمیم بگیرد.
    // ==============================================================================
    public async Task ConfirmSuggestedLinkAsync(int equipmentId, string targetType, int targetId, bool force = false)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);
        if (equipment == null) throw new Exception("تجهیز یافت نشد.");

        if (targetType == "MR")
        {
            var mr = await _context.MaterialRequisitions.FirstOrDefaultAsync(x => x.MRId == targetId);
            if (mr == null) throw new Exception("درخواست خرید یافت نشد.");

            if (!force && equipment.RelatedMRId.HasValue && equipment.RelatedMRId.Value != mr.MRId)
            {
                var previousMr = await _context.MaterialRequisitions
                    .FirstOrDefaultAsync(x => x.MRId == equipment.RelatedMRId.Value);

                throw new Exception(
                    $"این تجهیز از قبل به MR «{previousMr?.MRNumber ?? equipment.RelatedMRId.Value.ToString()}» وصل است. " +
                    "اگه مطمئنی می‌خوای جایگزینش کنی، دوباره با تایید صریح (force) امتحان کن.");
            }

            equipment.RelatedMRId = mr.MRId;
        }
        else if (targetType == "VDR")
        {
            var vdrItem = await _context.VDRItems.FirstOrDefaultAsync(x => x.DocumentId == targetId);
            if (vdrItem == null) throw new Exception("قلم VDR یافت نشد.");

            if (!force && vdrItem.EquipmentId.HasValue && vdrItem.EquipmentId.Value != equipmentId)
            {
                var previousEquipment = await _context.Equipments
                    .FirstOrDefaultAsync(x => x.EquipmentId == vdrItem.EquipmentId.Value);

                throw new Exception(
                    $"این مدرک VDR از قبل به تجهیز «{previousEquipment?.TagNumber ?? vdrItem.EquipmentId.Value.ToString()}» وصل است. " +
                    "اگه مطمئنی می‌خوای جایگزینش کنی، دوباره با تایید صریح (force) امتحان کن.");
            }

            vdrItem.EquipmentId = equipmentId;
        }
        else
        {
            throw new Exception($"نوع هدف نامعتبر است: {targetType}");
        }

        await _context.SaveChangesAsync();
    }


    // طبق ممیزی بند ۲.۱: دید تحلیل شکاف برای برنامه‌ریزی - کدام تجهیزات هنوز به هیچ
    // مدرک/VDR/MR/WorkOrder وصل نیستند
    public async Task<RelationshipGapReportDto> GetGapReportAsync(int? projectId = null)
    {
        var equipmentQuery = _context.Equipments
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) equipmentQuery = equipmentQuery.Where(x => x.ProjectId == projectId.Value);

        var equipmentList = await equipmentQuery.ToListAsync();
        var equipmentIds = equipmentList.Select(x => x.EquipmentId).ToList();

        // طبق همون رفع N+1 بند ۱.۲: یک Query برای همه، نه یکی به‌ازای هر تجهیز
        var docEquipmentIds = new HashSet<int>(await _context.EngineeringDocuments
            .Where(x => x.IsActive && x.EquipmentId.HasValue && equipmentIds.Contains(x.EquipmentId.Value))
            .Select(x => x.EquipmentId!.Value)
            .Distinct()
            .ToListAsync());

        var vdrEquipmentIds = new HashSet<int>(await _context.VDRItems
            .Where(x => x.IsActive && x.EquipmentId.HasValue && equipmentIds.Contains(x.EquipmentId.Value))
            .Select(x => x.EquipmentId!.Value)
            .Distinct()
            .ToListAsync());

        var mrTagNumbers = new HashSet<string>(await _context.MaterialRequisitions
            .Where(x => x.EquipmentTagNumber != null)
            .Select(x => x.EquipmentTagNumber!)
            .Distinct()
            .ToListAsync(), StringComparer.OrdinalIgnoreCase);

        var relatedMrEquipmentIds = new HashSet<int>(equipmentList.Where(x => x.RelatedMRId.HasValue).Select(x => x.EquipmentId));

        var woEquipmentIds = new HashSet<int>(await _context.WorkOrders
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => x.EquipmentId)
            .Distinct()
            .ToListAsync());

        var report = new RelationshipGapReportDto { TotalEquipmentCount = equipmentList.Count };

        foreach (var eq in equipmentList)
        {
            var hasDocuments = docEquipmentIds.Contains(eq.EquipmentId);
            var hasVdr = vdrEquipmentIds.Contains(eq.EquipmentId);
            var hasMr = relatedMrEquipmentIds.Contains(eq.EquipmentId) || mrTagNumbers.Contains(eq.TagNumber);
            var hasWo = woEquipmentIds.Contains(eq.EquipmentId);

            var linkedCount = new[] { hasDocuments, hasVdr, hasMr, hasWo }.Count(x => x);

            if (linkedCount == 4) report.FullyLinkedCount++;
            else if (linkedCount == 0) report.UnlinkedCount++;
            else report.PartiallyLinkedCount++;

            // فقط تجهیزات ناقص (نه کاملاً وصل) در لیست جزئیات نشون داده می‌شن - وگرنه برای
            // پروژه‌ی بزرگ، گزارش عملاً غیرقابل‌استفاده می‌شه
            if (linkedCount < 4)
            {
                report.Items.Add(new RelationshipGapItemDto
                {
                    EquipmentId = eq.EquipmentId,
                    TagNumber = eq.TagNumber,
                    Name = eq.Name,
                    Area = eq.Area,
                    DisciplineName = eq.Discipline?.Name ?? "",
                    HasDocuments = hasDocuments,
                    HasVdrItems = hasVdr,
                    HasMR = hasMr,
                    HasWorkOrders = hasWo
                });
            }
        }

        report.Items = report.Items.OrderBy(x => x.Area).ThenBy(x => x.TagNumber).ToList();

        return report;
    }


    // ==============================================================================
    // طبق درخواست: تعداد تجهیزات زیاد است - تایید یکی‌یکی عملی نیست. همه‌ی پیشنهادهای کل
    // سیستم/پروژه یک‌جا اسکن می‌شوند (بدون N+1 - همه‌چیز یک‌بار پیش‌بارگذاری و در حافظه
    // تطبیق داده می‌شود، نه یک Query جدا برای هر تجهیز).
    // ==============================================================================
    public async Task<List<BulkSuggestedLinkItemDto>> GetAllSuggestedLinksAsync(int? projectId = null)
    {
        var equipmentQuery = _context.Equipments
            .Where(x => x.IsActive &&
                ((x.MRNumberRaw != null && x.MRNumberRaw != "" && !x.RelatedMRId.HasValue) ||
                 (x.PONumberRaw != null && x.PONumberRaw != "")))
            .AsQueryable();

        if (projectId.HasValue) equipmentQuery = equipmentQuery.Where(x => x.ProjectId == projectId.Value);

        var candidateEquipment = await equipmentQuery.ToListAsync();
        if (candidateEquipment.Count == 0) return new List<BulkSuggestedLinkItemDto>();

        var allMrs = await _context.MaterialRequisitions.ToListAsync();
        var mrByNumber = allMrs
            .GroupBy(x => x.MRNumber.Trim().ToLower())
            .ToDictionary(g => g.Key, g => g.First());

        var unlinkedVdrItems = await _context.VDRItems
            .Include(x => x.Po)
            .Where(x => x.EquipmentId == null && x.Po != null)
            .ToListAsync();
        var vdrByPoNumber = unlinkedVdrItems
            .GroupBy(x => x.Po!.PoNumber.Trim().ToLower())
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<BulkSuggestedLinkItemDto>();

        foreach (var eq in candidateEquipment)
        {
            if (!string.IsNullOrWhiteSpace(eq.MRNumberRaw) && !eq.RelatedMRId.HasValue)
            {
                if (mrByNumber.TryGetValue(eq.MRNumberRaw.Trim().ToLower(), out var mr))
                {
                    results.Add(new BulkSuggestedLinkItemDto
                    {
                        EquipmentId = eq.EquipmentId,
                        EquipmentTagNumber = eq.TagNumber,
                        TargetType = "MR",
                        TargetId = mr.MRId,
                        TargetLabel = mr.MRNumber,
                        MatchedOnRawValue = eq.MRNumberRaw
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(eq.PONumberRaw))
            {
                if (vdrByPoNumber.TryGetValue(eq.PONumberRaw.Trim().ToLower(), out var vdr))
                {
                    results.Add(new BulkSuggestedLinkItemDto
                    {
                        EquipmentId = eq.EquipmentId,
                        EquipmentTagNumber = eq.TagNumber,
                        TargetType = "VDR",
                        TargetId = vdr.DocumentId,
                        TargetLabel = vdr.DocumentNumber,
                        MatchedOnRawValue = eq.PONumberRaw
                    });
                }
            }
        }

        return results.OrderBy(x => x.EquipmentTagNumber).ToList();
    }


    // طبق درخواست: تایید دسته‌جمعی - از همون ConfirmSuggestedLinkAsync استفاده می‌کند (بدون
    // پیاده‌سازی تکراری)، فقط هر مورد مستقل پردازش می‌شود تا یک خطا بقیه را متوقف نکند
    public async Task<(int Confirmed, List<string> Errors)> BulkConfirmAsync(
        IReadOnlyList<(int EquipmentId, string TargetType, int TargetId)> items)
    {
        var confirmed = 0;
        var errors = new List<string>();

        foreach (var (equipmentId, targetType, targetId) in items)
        {
            try
            {
                await ConfirmSuggestedLinkAsync(equipmentId, targetType, targetId, force: false);
                confirmed++;
            }
            catch (Exception ex)
            {
                errors.Add($"تجهیز #{equipmentId} ({targetType} #{targetId}): {ex.Message}");
            }
        }

        return (confirmed, errors);
    }

    private static void AddUnique(List<RelationshipNodeDto> list, string type, int id, string label, string? subLabel,
        DateTime? plannedOrDueDate = null, int? delayDays = null, string? priority = null, bool isConfirmed = true)
    {
        if (list.Any(x => x.NodeType == type && x.NodeId == id)) return;

        list.Add(new RelationshipNodeDto
        {
            NodeType = type,
            NodeId = id,
            Label = label,
            SubLabel = subLabel,
            PlannedOrDueDate = plannedOrDueDate,
            DelayDays = delayDays,
            Priority = priority,
            IsConfirmed = isConfirmed
        });
    }

    // طبق Knowledge Graph (Volume VIII-Bible): علاوه بر روابط قطعی مبتنی بر FK، روابطی که AI از
    // متن خلاصه‌ی مدارک کشف کرده (بدون FK صریح) هم به نتیجه اضافه می‌شوند - با پرچم IsAIProposed
    private async Task MergeAIProposedRelationshipsAsync(RelationshipExplorationDto result, string sourceType, int sourceId)
    {
        var aiLinks = await _context.EngineeringRelationships
            .Where(x => x.SourceType == sourceType && x.SourceId == sourceId && x.IsAIProposed)
            .ToListAsync();

        foreach (var link in aiLinks)
        {
            if (link.TargetType == "Document")
            {
                var engDoc = await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == link.TargetId);

                if (engDoc != null)
                {
                    AddAIProposed(result.Documents, "Document", engDoc.DocumentId, $"{engDoc.DocumentNo} - {engDoc.Title}", "AI-detected reference");
                    continue;
                }

                var vdr = await _context.VDRItems.FirstOrDefaultAsync(x => x.DocumentId == link.TargetId);

                if (vdr != null)
                    AddAIProposed(result.VendorDocuments, "Document", vdr.DocumentId, vdr.DocumentNumber, "AI-detected reference");
            }
            else if (link.TargetType == "Equipment")
            {
                var eq = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == link.TargetId);

                if (eq != null)
                    AddAIProposed(result.Equipment, "Equipment", eq.EquipmentId, eq.TagNumber, "AI-detected reference");
            }
        }
    }

    private static void AddAIProposed(List<RelationshipNodeDto> list, string type, int id, string label, string subLabel)
    {
        if (list.Any(x => x.NodeType == type && x.NodeId == id)) return;

        list.Add(new RelationshipNodeDto
        {
            NodeType = type,
            NodeId = id,
            Label = label,
            SubLabel = subLabel,
            IsAIProposed = true
        });
    }
}
