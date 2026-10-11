using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱ - گذارهای ProductionRun،
/// ثبت Input/Output/Loss، ارسال اختیاری به موجودی (Post) و گذار QualityStatus بچ. همان الگوی
/// دیکشنری گذارهای مجاز صریح (WorkOrderLifecycleService).
/// </summary>
public class ProductionRunService : IProductionRunService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;
    private readonly IMaterialStockService _materialStockService;
    private readonly IProductionOrderService _orderService;

    private static readonly Dictionary<string, string[]> RunTransitions = new()
    {
        ["Planned"] = new[] { "InProgress", "Cancelled" },
        ["InProgress"] = new[] { "Paused", "OnHold", "Completed", "Cancelled" },
        ["Paused"] = new[] { "InProgress", "Cancelled" },
        ["OnHold"] = new[] { "InProgress", "Cancelled" },
    };

    // طبق بند ۱۲ سند فاز: فهرست کنترل‌شده - نه متن آزاد
    public static readonly string[] LossCategories = { "ProcessLoss", "MaterialLoss", "Reject", "Waste", "Rework", "Other" };

    private static readonly Dictionary<string, string[]> BatchQualityTransitions = new()
    {
        ["Produced"] = new[] { "AwaitingQC" },
        ["AwaitingQC"] = new[] { "Released", "Blocked", "Rejected" },
        ["Blocked"] = new[] { "AwaitingQC" },
    };

    public ProductionRunService(
        PEMSDbContext context, INumberingService numberingService,
        IMaterialStockService materialStockService, IProductionOrderService orderService)
    {
        _context = context;
        _numberingService = numberingService;
        _materialStockService = materialStockService;
        _orderService = orderService;
    }

    public async Task<ProductionRunDto?> GetByIdAsync(int productionRunId)
    {
        var run = await _context.ProductionRuns
            .Include(x => x.ProductionOrder).ThenInclude(o => o!.MaterialItem)
            .Include(x => x.ProductionWorkCenter)
            .Include(x => x.Equipment)
            .Include(x => x.OperatorEmployee)
            .Include(x => x.ShiftDefinition)
            .Include(x => x.Inputs).ThenInclude(i => i.MaterialItem)
            .Include(x => x.Inputs).ThenInclude(i => i.Uom)
            .Include(x => x.Inputs).ThenInclude(i => i.SourceProductionBatch)
            .Include(x => x.Inputs).ThenInclude(i => i.Employee)
            .Include(x => x.Outputs).ThenInclude(o => o.MaterialItem)
            .Include(x => x.Outputs).ThenInclude(o => o.Uom)
            .Include(x => x.Outputs).ThenInclude(o => o.ProductionBatch)
            .Include(x => x.Outputs).ThenInclude(o => o.Employee)
            .Include(x => x.Losses).ThenInclude(l => l.Uom)
            .Include(x => x.Losses).ThenInclude(l => l.Employee)
            .Include(x => x.Events).ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);

        if (run == null) return null;

        var dto = new ProductionRunDto
        {
            ProductionRunId = run.ProductionRunId,
            ProductionOrderId = run.ProductionOrderId,
            OrderNumber = run.ProductionOrder?.OrderNumber ?? "",
            MaterialCode = run.ProductionOrder?.MaterialItem?.MaterialCode ?? "",
            MaterialDescription = run.ProductionOrder?.MaterialItem?.Description ?? "",
            RunNumber = run.RunNumber,
            Status = run.Status,
            StartDate = run.StartDate,
            EndDate = run.EndDate,
            WorkCenterCode = run.ProductionWorkCenter?.Code,
            EquipmentTagNumber = run.Equipment?.TagNumber,
            OperatorEmployeeName = run.OperatorEmployee != null ? $"{run.OperatorEmployee.FirstName} {run.OperatorEmployee.LastName}" : null,
            ShiftName = run.ShiftDefinition?.Name,
            Notes = run.Notes,
            HoldReason = run.HoldReason,
            Inputs = run.Inputs.OrderByDescending(i => i.ProductionInputId).Select(i => new ProductionInputDto
            {
                ProductionInputId = i.ProductionInputId,
                MaterialItemId = i.MaterialItemId,
                MaterialCode = i.MaterialItem?.MaterialCode ?? "",
                Quantity = i.Quantity,
                UomCode = i.Uom?.Code,
                SourceProductionBatchId = i.SourceProductionBatchId,
                SourceBatchNumber = i.SourceProductionBatch?.BatchNumber,
                PostedToInventory = i.InventoryTransactionId.HasValue,
                EmployeeName = i.Employee != null ? $"{i.Employee.FirstName} {i.Employee.LastName}" : null,
                TransactionDate = i.TransactionDate,
                Notes = i.Notes
            }).ToList(),
            Outputs = run.Outputs.OrderByDescending(o => o.ProductionOutputId).Select(o => new ProductionOutputDto
            {
                ProductionOutputId = o.ProductionOutputId,
                MaterialItemId = o.MaterialItemId,
                MaterialCode = o.MaterialItem?.MaterialCode ?? "",
                ProductionBatchId = o.ProductionBatchId,
                BatchNumber = o.ProductionBatch?.BatchNumber ?? "",
                Quantity = o.Quantity,
                UomCode = o.Uom?.Code,
                OutputClassification = o.OutputClassification,
                PostedToInventory = o.InventoryTransactionId.HasValue,
                EmployeeName = o.Employee != null ? $"{o.Employee.FirstName} {o.Employee.LastName}" : null,
                TransactionDate = o.TransactionDate,
                Notes = o.Notes
            }).ToList(),
            Losses = run.Losses.OrderByDescending(l => l.ProductionLossId).Select(l => new ProductionLossDto
            {
                ProductionLossId = l.ProductionLossId,
                Quantity = l.Quantity,
                UomCode = l.Uom?.Code,
                LossCategory = l.LossCategory,
                EmployeeName = l.Employee != null ? $"{l.Employee.FirstName} {l.Employee.LastName}" : null,
                TransactionDate = l.TransactionDate,
                Notes = l.Notes
            }).ToList(),
            Events = run.Events.OrderByDescending(e => e.ProductionEventId).Select(e => new ProductionEventDto
            {
                ProductionEventId = e.ProductionEventId,
                EventType = e.EventType,
                EventDate = e.EventDate,
                EmployeeName = e.Employee != null ? $"{e.Employee.FirstName} {e.Employee.LastName}" : null,
                Details = e.Details
            }).ToList()
        };

        dto.Reconciliation = BuildReconciliation(run);
        return dto;
    }

    private static ProductionReconciliationDto BuildReconciliation(ProductionRun run)
    {
        var inputUoms = run.Inputs.Select(i => i.UomId).Distinct().ToList();
        var outputUoms = run.Outputs.Select(o => o.UomId).Distinct().ToList();
        var lossUoms = run.Losses.Select(l => l.UomId).Distinct().ToList();
        var allUoms = inputUoms.Concat(outputUoms).Concat(lossUoms).Distinct().ToList();

        var totalInput = run.Inputs.Sum(i => i.Quantity);
        var totalOutput = run.Outputs.Sum(o => o.Quantity);
        var totalLoss = run.Losses.Sum(l => l.Quantity);

        // طبق بند ۱۲ سند فاز: عدد را مصنوعی برابر نمی‌کنیم - فقط وقتی همه‌ی واحدها یکی هستند
        // (یا هیچ‌کدام واحد ندارند) مقایسه‌ی عددی معنی‌دار است؛ در غیر این صورت صریحاً اعلام
        // می‌کنیم که قابل‌مقایسه نیست (چون تبدیل واحد در این نسخه از PEMS پیاده نشده - طبق ممیزی).
        if (allUoms.Count <= 1 && (run.Inputs.Any() || run.Outputs.Any() || run.Losses.Any()))
        {
            return new ProductionReconciliationDto
            {
                IsComparable = true,
                TotalInputQuantity = totalInput,
                TotalOutputQuantity = totalOutput,
                TotalLossQuantity = totalLoss,
                Difference = totalInput - totalOutput - totalLoss
            };
        }

        return new ProductionReconciliationDto
        {
            IsComparable = false,
            Note = "ورودی/خروجی/افت این اجرا واحدهای اندازه‌گیری متفاوتی دارند (تبدیل واحد در این نسخه پیاده‌سازی نشده) - مقایسه‌ی عددی مستقیم گمراه‌کننده است.",
            TotalInputQuantity = totalInput,
            TotalOutputQuantity = totalOutput,
            TotalLossQuantity = totalLoss,
            Difference = null
        };
    }

    public async Task<(bool Success, string? Error, int? ProductionRunId)> CreateAsync(
        int productionOrderId, int? productionWorkCenterId, int? equipmentId,
        int? operatorEmployeeId, int? shiftDefinitionId, string? notes, int? createdByEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.", null);

        if (order.Status != "Released" && order.Status != "InProgress")
            return (false, $"فقط سفارش‌های «Released» یا «InProgress» می‌توانند اجرای جدید داشته باشند - وضعیت فعلی: «{order.Status}».", null);

        var runNumber = await _numberingService.GetNextNumberAsync("ProductionRun", order.OrderNumber);

        var run = new ProductionRun
        {
            ProductionOrderId = productionOrderId,
            RunNumber = runNumber,
            Status = "Planned",
            ProductionWorkCenterId = productionWorkCenterId,
            EquipmentId = equipmentId,
            OperatorEmployeeId = operatorEmployeeId,
            ShiftDefinitionId = shiftDefinitionId,
            Notes = notes,
            CreatedDate = DateTime.Now
        };

        await _context.ProductionRuns.AddAsync(run);
        await _context.SaveChangesAsync();

        return (true, null, run.ProductionRunId);
    }

    public async Task<(bool Success, string? Error)> StartAsync(int productionRunId, int? actorEmployeeId)
    {
        var run = await _context.ProductionRuns.Include(x => x.ProductionOrder).FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");

        if (!RunTransitions.TryGetValue(run.Status, out var allowed) || !allowed.Contains("InProgress"))
            return (false, $"گذار از وضعیت «{run.Status}» به «InProgress» مجاز نیست.");

        if (run.ProductionOrder == null || (run.ProductionOrder.Status != "Released" && run.ProductionOrder.Status != "InProgress"))
            return (false, "سفارش تولید مربوطه Released/InProgress نیست.");

        run.Status = "InProgress";
        run.StartDate ??= DateTime.Now;
        await _context.SaveChangesAsync();

        await _orderService.AdvanceToInProgressIfNeededAsync(run.ProductionOrderId);
        await LogEventAsync(run.ProductionOrderId, run.ProductionRunId, "RunStarted", actorEmployeeId, null);
        return (true, null);
    }

    public Task<(bool Success, string? Error)> PauseAsync(int productionRunId, int? actorEmployeeId)
        => AdvanceRunAsync(productionRunId, "Paused", "RunPaused", actorEmployeeId, null);

    public async Task<(bool Success, string? Error)> ResumeAsync(int productionRunId, int? actorEmployeeId)
    {
        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");

        if (run.Status != "Paused" && run.Status != "OnHold")
            return (false, $"فقط اجرای «Paused» یا «OnHold» قابل ازسرگیری است - وضعیت فعلی: «{run.Status}».");

        run.Status = "InProgress";
        run.HoldReason = null;
        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, run.ProductionRunId, "RunResumed", actorEmployeeId, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> HoldAsync(int productionRunId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل تعلیق باید مشخص شود.");
        var (success, error) = await AdvanceRunAsync(productionRunId, "OnHold", "RunHeld", actorEmployeeId, reason);
        return (success, error);
    }

    public async Task<(bool Success, string? Error)> CompleteAsync(int productionRunId, int? actorEmployeeId)
    {
        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");

        if (!RunTransitions.TryGetValue(run.Status, out var allowed) || !allowed.Contains("Completed"))
            return (false, $"گذار از وضعیت «{run.Status}» به «Completed» مجاز نیست.");

        var hasOutput = await _context.ProductionOutputs.AnyAsync(x => x.ProductionRunId == productionRunId);
        if (!hasOutput)
            return (false, "این اجرا بدون ثبت هیچ خروجی تولیدی قابل تکمیل نیست.");

        run.Status = "Completed";
        run.EndDate = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, run.ProductionRunId, "RunCompleted", actorEmployeeId, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(int productionRunId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل لغو باید مشخص شود.");

        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");

        if (!RunTransitions.TryGetValue(run.Status, out var allowed) || !allowed.Contains("Cancelled"))
            return (false, $"اجرا در وضعیت «{run.Status}» قابل لغو نیست.");

        run.Status = "Cancelled";
        run.CancellationReason = reason;
        run.EndDate = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, run.ProductionRunId, "RunCancelled", actorEmployeeId, reason);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RecordInputAsync(
        int productionRunId, int materialItemId, decimal quantity, int? uomId,
        int? sourceProductionBatchId, string? notes, int? actorEmployeeId)
    {
        if (quantity <= 0) return (false, "مقدار مصرفی باید بزرگ‌تر از صفر باشد.");

        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");
        if (run.Status != "InProgress") return (false, "ثبت ورودی فقط برای اجرای در حال انجام (InProgress) مجاز است.");

        var materialExists = await _context.MaterialItems.AnyAsync(x => x.MaterialItemId == materialItemId && x.IsActive);
        if (!materialExists) return (false, "متریال انتخاب‌شده معتبر نیست.");

        if (sourceProductionBatchId.HasValue)
        {
            var batchExists = await _context.ProductionBatches.AnyAsync(x => x.ProductionBatchId == sourceProductionBatchId.Value);
            if (!batchExists) return (false, "بچ منبع انتخاب‌شده معتبر نیست.");
        }

        await _context.ProductionInputs.AddAsync(new ProductionInput
        {
            ProductionRunId = productionRunId,
            MaterialItemId = materialItemId,
            Quantity = quantity,
            UomId = uomId,
            SourceProductionBatchId = sourceProductionBatchId,
            EmployeeId = actorEmployeeId,
            TransactionDate = DateTime.Now,
            Notes = notes
        });

        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, productionRunId, "InputConsumed", actorEmployeeId, $"{quantity} از متریال #{materialItemId}");
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? ProductionBatchId)> RecordOutputAsync(
        int productionRunId, int materialItemId, int? productionBatchId, decimal quantity, int? uomId,
        string outputClassification, string? notes, int? actorEmployeeId)
    {
        if (quantity <= 0) return (false, "مقدار تولیدی باید بزرگ‌تر از صفر باشد.", null);

        var normalizedClassification = string.IsNullOrWhiteSpace(outputClassification) ? "Primary" : outputClassification;
        if (normalizedClassification != "Primary" && normalizedClassification != "ByProduct")
            return (false, "دسته‌بندی خروجی نامعتبر است.", null);

        var run = await _context.ProductionRuns.Include(x => x.ProductionOrder).FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.", null);
        if (run.Status != "InProgress") return (false, "ثبت خروجی فقط برای اجرای در حال انجام (InProgress) مجاز است.", null);

        var material = await _context.MaterialItems.FirstOrDefaultAsync(x => x.MaterialItemId == materialItemId && x.IsActive);
        if (material == null) return (false, "متریال/محصول انتخاب‌شده معتبر نیست.", null);

        ProductionBatch batch;

        if (productionBatchId.HasValue)
        {
            var existingBatch = await _context.ProductionBatches.FirstOrDefaultAsync(x => x.ProductionBatchId == productionBatchId.Value);
            if (existingBatch == null) return (false, "بچ انتخاب‌شده معتبر نیست.", null);
            if (existingBatch.ProductionRunId != productionRunId) return (false, "بچ انتخاب‌شده متعلق به این اجرا نیست.", null);
            if (existingBatch.MaterialItemId != materialItemId) return (false, "بچ انتخاب‌شده برای متریال دیگری ثبت شده است.", null);

            batch = existingBatch;
        }
        else
        {
            var batchNumber = await _numberingService.GetNextNumberAsync("ProductionBatch");
            batch = new ProductionBatch
            {
                BatchNumber = batchNumber,
                ProductionOrderId = run.ProductionOrderId,
                ProductionRunId = productionRunId,
                MaterialItemId = materialItemId,
                UomId = uomId,
                Quantity = 0,
                ProducedDate = DateTime.Now,
                QualityStatus = "Produced",
                CreatedDate = DateTime.Now
            };
            await _context.ProductionBatches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }

        // مجموع نگهداری‌شده - فقط این‌جا تغییر می‌کند (طبق کامنت ProductionBatch.cs)
        batch.Quantity += quantity;

        await _context.ProductionOutputs.AddAsync(new ProductionOutput
        {
            ProductionRunId = productionRunId,
            MaterialItemId = materialItemId,
            ProductionBatchId = batch.ProductionBatchId,
            Quantity = quantity,
            UomId = uomId,
            OutputClassification = normalizedClassification,
            EmployeeId = actorEmployeeId,
            TransactionDate = DateTime.Now,
            Notes = notes
        });

        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, productionRunId, "OutputRecorded", actorEmployeeId, $"{quantity} به بچ {batch.BatchNumber}");
        return (true, null, batch.ProductionBatchId);
    }

    public async Task<(bool Success, string? Error)> RecordLossAsync(
        int productionRunId, decimal quantity, int? uomId, string lossCategory, string? notes, int? actorEmployeeId)
    {
        if (quantity <= 0) return (false, "مقدار افت باید بزرگ‌تر از صفر باشد.");
        if (!LossCategories.Contains(lossCategory)) return (false, "دسته‌بندی افت نامعتبر است.");

        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");
        if (run.Status != "InProgress") return (false, "ثبت افت فقط برای اجرای در حال انجام (InProgress) مجاز است.");

        await _context.ProductionLosses.AddAsync(new ProductionLoss
        {
            ProductionRunId = productionRunId,
            Quantity = quantity,
            UomId = uomId,
            LossCategory = lossCategory,
            EmployeeId = actorEmployeeId,
            TransactionDate = DateTime.Now,
            Notes = notes
        });

        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, productionRunId, "LossRecorded", actorEmployeeId, $"{quantity} ({lossCategory})");
        return (true, null);
    }

    public Task<(bool Success, string? Error)> PostInputToInventoryAsync(
        int productionInputId, int warehouseId, int? storageLocationId, int? actorEmployeeId)
        // Phase 2 / P4: قفل سند + خواندن بعد از قفل؛ دو کلیک هم‌زمان روی همین سند فقط یک‌بار ثبت می‌شوند (جزئیات: PostingScope)
        => PostingScope.RunAsync(_context, $"ProductionInput:{productionInputId}", () => PostInputCoreAsync(productionInputId, warehouseId, storageLocationId, actorEmployeeId));

    private async Task<(bool Success, string? Error, bool Commit)> PostInputCoreAsync(
        int productionInputId, int warehouseId, int? storageLocationId, int? actorEmployeeId)
    {
        var input = await _context.ProductionInputs.Include(x => x.ProductionRun)
            .FirstOrDefaultAsync(x => x.ProductionInputId == productionInputId);
        if (input == null) return (false, "ردیف ورودی یافت نشد.", false);
        if (input.InventoryTransactionId.HasValue) return (false, "این ورودی قبلاً به موجودی ارسال شده است.", false);

        // ثبت قبلی که لینکش ذخیره نشده (شکست بین دو ذخیره): از دفتر کل پیدا و فقط لینک می‌شود، ثبت دوباره نمی‌شود
        var postedTx = await _materialStockService.GetLastTransactionIdAsync("ProductionInput", productionInputId);
        if (postedTx.HasValue)
        {
            input.InventoryTransactionId = postedTx;
            await _context.SaveChangesAsync();
            return (false, "این ورودی قبلاً به موجودی ارسال شده است.", true);
        }

        // Phase 2 / P4: کلید کامل (متریال، انبار، محل)؛ ردیف‌های مبهم ⇒ خطای صریح. مصرف به‌صورت «کاهش نسبی» روی مقدار تازه‌ی ردیف ثبت می‌شود
        // (AdjustByAsync) - قبلاً موجودی خوانده و مقدار مطلق نوشته می‌شد که با عملیات هم‌زمان دیگر روی همان ردیف Race داشت.
        // طبق «PEMS Operations Phase 2A» بخش ۱۰: اگر ماده‌ی مصرفی خودش از یک ProductionBatch
        // شناخته‌شده آمده باشد (SourceProductionBatchId - ژنالوژی فاز ۱)، همان هویت بچ روی
        // تراکنش موجودی هم ثبت می‌شود؛ در غیر این صورت Null می‌ماند (بدون حدس).
        var (success, error) = await _materialStockService.AdjustByAsync(
            input.MaterialItemId, warehouseId, storageLocationId, -input.Quantity,
            $"مصرف تولید - اجرای {input.ProductionRun?.RunNumber}", actorEmployeeId,
            referenceType: "ProductionInput", referenceId: input.ProductionInputId,
            productionBatchId: input.SourceProductionBatchId);

        if (!success) return (false, error, false);

        input.InventoryTransactionId = await _materialStockService.GetLastTransactionIdAsync("ProductionInput", input.ProductionInputId);
        await _context.SaveChangesAsync();
        return (true, null, true);
    }

    public Task<(bool Success, string? Error)> PostOutputToInventoryAsync(
        int productionOutputId, int warehouseId, int? storageLocationId, int? actorEmployeeId)
        // Phase 2 / P4: قفل سند + خواندن بعد از قفل؛ دو کلیک هم‌زمان روی همین سند فقط یک‌بار ثبت می‌شوند (جزئیات: PostingScope)
        => PostingScope.RunAsync(_context, $"ProductionOutput:{productionOutputId}", () => PostOutputCoreAsync(productionOutputId, warehouseId, storageLocationId, actorEmployeeId));

    private async Task<(bool Success, string? Error, bool Commit)> PostOutputCoreAsync(
        int productionOutputId, int warehouseId, int? storageLocationId, int? actorEmployeeId)
    {
        var output = await _context.ProductionOutputs.Include(x => x.ProductionBatch)
            .FirstOrDefaultAsync(x => x.ProductionOutputId == productionOutputId);
        if (output == null) return (false, "ردیف خروجی یافت نشد.", false);
        if (output.InventoryTransactionId.HasValue) return (false, "این خروجی قبلاً به موجودی ارسال شده است.", false);

        var postedTx = await _materialStockService.GetLastTransactionIdAsync("ProductionOutput", productionOutputId);
        if (postedTx.HasValue)
        {
            output.InventoryTransactionId = postedTx;
            await _context.SaveChangesAsync();
            return (false, "این خروجی قبلاً به موجودی ارسال شده است.", true);
        }

        // طبق «PEMS Operations Phase 2C — Production Safety Correction»: قبل از این تغییر، این
        // متد وضعیت کیفیت بچ را اصلاً بررسی نمی‌کرد - یک بچ Blocked/Rejected می‌توانست عیناً مثل
        // یک بچ Released وارد موجودیِ در دسترس شود (چون MaterialStock هیچ سطل "موجود ولی
        // بلوکه" جدایی ندارد - هر چیزی که Post شود بلافاصله AvailableQuantity را افزایش
        // می‌دهد). طبق تصمیم صریح سند فاز (بدون مسیر Override در این فاز - اگر روزی لازم شد،
        // یک تصمیم کسب‌وکاری جدا و مجزاست، نه یک اثر جانبیِ این اصلاح ایمنی): فقط بچِ Released
        // اجازه‌ی Post شدن دارد. Produced/AwaitingQC (هنوز تصمیم‌گیری نشده) و Blocked/Rejected
        // (تصمیم منفی) همگی رد می‌شوند - نه فقط دو حالت منفی.
        if (output.ProductionBatch == null || output.ProductionBatch.QualityStatus != "Released")
        {
            var currentStatus = output.ProductionBatch?.QualityStatus ?? "نامشخص";
            return (false, $"این خروجی قابل ارسال به موجودی نیست - بچ مربوطه هنوز Released نشده (وضعیت فعلی: {currentStatus}). فقط بچ‌های Released قابل ارسال به موجودی هستند.", false);
        }

        // طبق «PEMS Operations Phase 2A» بخش ۱۰: خروجی همیشه ProductionBatchId مشخص دارد
        // (فیلد اجباری روی ProductionOutput) - همان هویت بچ مستقیم روی تراکنش موجودی ثبت
        // می‌شود، بدون هیچ جستجوی حدسی جدید.
        var (success, error) = await _materialStockService.ReceiveAsync(
            output.MaterialItemId, warehouseId, storageLocationId, output.Quantity,
            $"دریافت تولید - بچ {output.ProductionBatch?.BatchNumber}", actorEmployeeId,
            referenceType: "ProductionOutput", referenceId: output.ProductionOutputId,
            productionBatchId: output.ProductionBatchId);

        if (!success) return (false, error, false);

        output.InventoryTransactionId = await _materialStockService.GetLastTransactionIdAsync("ProductionOutput", output.ProductionOutputId);
        await _context.SaveChangesAsync();
        return (true, null, true);
    }

    public async Task<ProductionBatchDto?> GetBatchByIdAsync(int productionBatchId)
    {
        var batch = await _context.ProductionBatches
            .Include(x => x.MaterialItem)
            .Include(x => x.Uom)
            .Include(x => x.ProductionOrder)
            .Include(x => x.ProductionRun)
            .Include(x => x.QualityStatusChangedBy)
            .FirstOrDefaultAsync(x => x.ProductionBatchId == productionBatchId);

        if (batch == null) return null;

        return new ProductionBatchDto
        {
            ProductionBatchId = batch.ProductionBatchId,
            BatchNumber = batch.BatchNumber,
            ProductionOrderId = batch.ProductionOrderId,
            OrderNumber = batch.ProductionOrder?.OrderNumber ?? "",
            ProductionRunId = batch.ProductionRunId,
            RunNumber = batch.ProductionRun?.RunNumber ?? "",
            MaterialItemId = batch.MaterialItemId,
            MaterialCode = batch.MaterialItem?.MaterialCode ?? "",
            MaterialDescription = batch.MaterialItem?.Description ?? "",
            Quantity = batch.Quantity,
            UomCode = batch.Uom?.Code,
            ProducedDate = batch.ProducedDate,
            QualityStatus = batch.QualityStatus,
            QualityStatusReason = batch.QualityStatusReason,
            QualityStatusChangedDate = batch.QualityStatusChangedDate,
            QualityStatusChangedByName = batch.QualityStatusChangedBy != null ? $"{batch.QualityStatusChangedBy.FirstName} {batch.QualityStatusChangedBy.LastName}" : null,
            Notes = batch.Notes
        };
    }

    public Task<(bool Success, string? Error)> SubmitBatchForQcAsync(int productionBatchId, int? actorEmployeeId)
        => AdvanceBatchAsync(productionBatchId, "AwaitingQC", "BatchSubmittedForQC", actorEmployeeId, null);

    public Task<(bool Success, string? Error)> ReleaseBatchAsync(int productionBatchId, int? actorEmployeeId)
        => AdvanceBatchAsync(productionBatchId, "Released", "BatchQualityStatusChanged", actorEmployeeId, null);

    public async Task<(bool Success, string? Error)> BlockBatchAsync(int productionBatchId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل توقف باید مشخص شود.");
        return await AdvanceBatchAsync(productionBatchId, "Blocked", "BatchQualityStatusChanged", actorEmployeeId, reason);
    }

    public Task<(bool Success, string? Error)> UnblockBatchAsync(int productionBatchId, int? actorEmployeeId)
        => AdvanceBatchAsync(productionBatchId, "AwaitingQC", "BatchQualityStatusChanged", actorEmployeeId, null);

    public async Task<(bool Success, string? Error)> RejectBatchAsync(int productionBatchId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل رد باید مشخص شود.");
        return await AdvanceBatchAsync(productionBatchId, "Rejected", "BatchQualityStatusChanged", actorEmployeeId, reason);
    }

    private async Task<(bool Success, string? Error)> AdvanceBatchAsync(
        int productionBatchId, string nextStatus, string eventType, int? actorEmployeeId, string? reason)
    {
        var batch = await _context.ProductionBatches.FirstOrDefaultAsync(x => x.ProductionBatchId == productionBatchId);
        if (batch == null) return (false, "بچ تولید یافت نشد.");

        if (!BatchQualityTransitions.TryGetValue(batch.QualityStatus, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار وضعیت کیفیت از «{batch.QualityStatus}» به «{nextStatus}» مجاز نیست.");

        batch.QualityStatus = nextStatus;
        batch.QualityStatusReason = reason;
        batch.QualityStatusChangedDate = DateTime.Now;
        batch.QualityStatusChangedById = actorEmployeeId;
        await _context.SaveChangesAsync();

        await LogEventAsync(batch.ProductionOrderId, batch.ProductionRunId, eventType, actorEmployeeId, $"بچ {batch.BatchNumber}: {nextStatus}" + (reason != null ? $" ({reason})" : ""));
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> AdvanceRunAsync(int productionRunId, string nextStatus, string eventType, int? actorEmployeeId, string? reason)
    {
        var run = await _context.ProductionRuns.FirstOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null) return (false, "اجرای تولید یافت نشد.");

        if (!RunTransitions.TryGetValue(run.Status, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{run.Status}» به «{nextStatus}» مجاز نیست.");

        if (nextStatus == "OnHold") run.HoldReason = reason;

        run.Status = nextStatus;
        await _context.SaveChangesAsync();
        await LogEventAsync(run.ProductionOrderId, productionRunId, eventType, actorEmployeeId, reason);
        return (true, null);
    }

    private async Task LogEventAsync(int? orderId, int? runId, string eventType, int? employeeId, string? details)
    {
        await _context.ProductionEvents.AddAsync(new ProductionEvent
        {
            ProductionOrderId = orderId,
            ProductionRunId = runId,
            EventType = eventType,
            EventDate = DateTime.Now,
            EmployeeId = employeeId,
            Details = details
        });
        await _context.SaveChangesAsync();
    }
}
