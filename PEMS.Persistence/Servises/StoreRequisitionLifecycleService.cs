using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// چرخه‌ی عمر StoreRequisition (Indent داخلی) + اثرات آن روی MaterialReservation/InventoryTransaction/MaterialStock.
/// هر عملیات حساس، پیش از هر تغییر، این‌ها را به‌ترتیب بررسی می‌کند (همه الزامی‌اند):
///   ۱) هویت فعال کارمند (EmployeeId=null/ناموجود/غیرفعال ⇒ رد؛ هرگز 0 یا هویت ساختگی)
///   ۲) مجوز ریزدانه‌ی RoleModuleAction «StoreRequisition.&lt;Action&gt;» (SystemAdmin طبق AccessControl)
///   ۳) جدول گذار StoreRequisitionStatuses  ۴) تفکیک وظایف (درخواست‌دهنده ≠ تأییدکننده/تحویل‌دهنده)
///   ۵) ناورداهای مقدار (Issued ≤ Approved ≤ Requested؛ Reserved ≤ OnHand؛ بدون منفی)
/// همه‌ی تغییرات (موجودی + رزرو + تراکنش موجودی + تاریخچه) در یک تراکنش commit/rollback می‌شوند؛ تعارض هم‌زمانی
/// (RowVersion) با تلاش مجدد حداکثر ۳ بار و در نهایت پیام قابل‌فهم برمی‌گردد (نه خطای ۵۰۰). requestKey عملیات را Idempotent می‌کند.
/// ممیزی: PEMSDbContext.SaveChanges برای تک‌تک تغییرات (مقدار قبل/بعد، کاربر، زمان) AuditLog می‌سازد؛ تاریخچه‌ی وضعیت و تراکنش‌ها
/// (با WorkOrder/Line/Reservation) زمینه‌ی کسب‌وکاری را کامل می‌کنند.
/// </summary>
public class StoreRequisitionLifecycleService : IStoreRequisitionLifecycleService
{
    private const int MaxAttempts = 3;
    private readonly PEMSDbContext _context;
    private readonly IRoleModuleActionService _actions;

    public StoreRequisitionLifecycleService(PEMSDbContext context, IRoleModuleActionService actions)
    {
        _context = context;
        _actions = actions;
    }

    // ------------------------------------------------------------------ API

    public Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int storeRequisitionId, string nextStatus, string? reason, StoreRequisitionActor actor, string? requestKey = null)
        => RunAsync(storeRequisitionId, requestKey, () => AdvanceCoreAsync(storeRequisitionId, nextStatus, reason, actor, requestKey));

    public Task<(bool Success, string? Error)> SetApprovedQuantitiesAsync(
        int storeRequisitionId, IReadOnlyDictionary<int, decimal> approvedByLineId, StoreRequisitionActor actor, string? requestKey = null)
        => RunAsync(storeRequisitionId, requestKey, () => SetApprovedCoreAsync(storeRequisitionId, approvedByLineId, actor, requestKey));

    public Task<(bool Success, string? Error)> IssueAsync(
        int storeRequisitionId, IReadOnlyDictionary<int, decimal>? quantitiesByLineId, string? reason, StoreRequisitionActor actor, string? requestKey = null)
        => RunAsync(storeRequisitionId, requestKey, () => IssueCoreAsync(storeRequisitionId, quantitiesByLineId, reason, actor, requestKey));

    public Task<bool> IsAllowedAsync(StoreRequisitionActor actor, string action) =>
        _actions.HasActionAsync(actor.Roles, StoreRequisitionActions.Module, action);

    // ------------------------------------------------------------------ تراکنش + Idempotency + تلاش مجدد روی تعارض

    // Phase 2 / P4 (Audit): برنامه با EnableRetryOnFailure اجرا می‌شود (Program.cs) و آن استراتژی تراکنش کاربر را فقط داخل
    // strategy.ExecuteAsync می‌پذیرد؛ بدون این پوشش، BeginTransactionAsync در محیط واقعی InvalidOperationException می‌داد (تست‌ها بدون
    // استراتژی بودند و آن را نمی‌دیدند). هر بار اجرای مجدد روی خطای گذرا از یک ChangeTracker تمیز شروع می‌شود؛ منطق داخلی (RequestKey،
    // تلاش مجدد روی RowVersion) بدون تغییر است و برای اجرای دوباره ایمن است.
    private Task<(bool Success, string? Error)> RunAsync(int id, string? requestKey, Func<Task<(bool Success, string? Error)>> operation)
    {
        var executions = 0;
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(() =>
        {
            if (++executions > 1) _context.ChangeTracker.Clear();
            return RunAttemptsAsync(id, requestKey, operation);
        });
    }

    private async Task<(bool Success, string? Error)> RunAttemptsAsync(int id, string? requestKey, Func<Task<(bool Success, string? Error)>> operation)
    {
        requestKey = string.IsNullOrWhiteSpace(requestKey) ? null : requestKey.Trim();
        if (requestKey is { Length: > 64 }) return (false, "کلید درخواست نامعتبر است.");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // تلاش مجدد با همان کلید (Timeout/دابل‌کلیک): قبلاً انجام شده ⇒ موفق و بدون اثر تکراری
            if (requestKey != null && await _context.StoreRequisitionStatusHistories.AsNoTracking()
                    .AnyAsync(h => h.StoreRequisitionId == id && h.RequestKey == requestKey))
                return (true, null);

            var ownsTransaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null;
            var tx = ownsTransaction ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var result = await operation();
                if (tx != null)
                {
                    if (result.Success) await tx.CommitAsync(); else await tx.RollbackAsync();
                }
                if (!result.Success) _context.ChangeTracker.Clear();
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (tx != null) await tx.RollbackAsync();
                _context.ChangeTracker.Clear();
                if (attempt == MaxAttempts)
                    return (false, "تغییر هم‌زمان توسط کاربر دیگری روی موجودی/درخواست انجام شد؛ دوباره تلاش کنید.");
            }
            catch (DbUpdateException) when (requestKey != null)
            {
                // رقابت دو درخواست هم‌کلید: Index یکتای (درخواست، RequestKey) دومی را رد کرده ⇒ اولی کامل شده است
                if (tx != null) await tx.RollbackAsync();
                _context.ChangeTracker.Clear();
                if (await _context.StoreRequisitionStatusHistories.AsNoTracking().AnyAsync(h => h.StoreRequisitionId == id && h.RequestKey == requestKey))
                    return (true, null);
                throw;
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        }
        return (false, "Operation failed.");
    }

    private static (bool Success, string? Error) Fail(string error) => (false, error);

    private Task<StoreRequisition?> LoadAsync(int id) =>
        _context.StoreRequisitions.Include(x => x.Lines).FirstOrDefaultAsync(x => x.StoreRequisitionId == id);

    /// <summary>هویت + مجوز. خروجی: شناسه‌ی کارمند معتبر یا پیام خطا.</summary>
    private async Task<(int? EmployeeId, string? Error)> AuthorizeAsync(StoreRequisitionActor actor, string action)
    {
        if (actor == null || !actor.EmployeeId.HasValue || actor.EmployeeId.Value <= 0)
            return (null, "حساب کاربری به هیچ پرونده‌ی کارمندی متصل نیست؛ این عملیات بدون هویت کارمند مجاز نیست.");

        var emp = await _context.Employees.AsNoTracking().Where(e => e.EmployeeId == actor.EmployeeId.Value).Select(e => new { e.IsActive }).FirstOrDefaultAsync();
        if (emp == null) return (null, "پرونده‌ی کارمند یافت نشد.");
        if (!emp.IsActive) return (null, "پرونده‌ی کارمند غیرفعال است.");

        if (!await _actions.HasActionAsync(actor.Roles, StoreRequisitionActions.Module, action))
            return (null, $"مجوز «StoreRequisition.{action}» را ندارید.");

        return (actor.EmployeeId.Value, null);
    }

    // ------------------------------------------------------------------ مقدار تأییدشده

    private async Task<(bool Success, string? Error)> SetApprovedCoreAsync(int id, IReadOnlyDictionary<int, decimal> approved, StoreRequisitionActor actor, string? requestKey)
    {
        var (by, authError) = await AuthorizeAsync(actor, StoreRequisitionActions.SetApprovedQuantity);
        if (authError != null) return Fail(authError);

        var req = await LoadAsync(id);
        if (req == null) return Fail("Store requisition not found.");
        // قاعده‌ی قطعی: مقدار تأییدشده فقط پیش از تأیید (Submitted) قابل تعیین است؛ پس از تأیید/رزرو/تحویل تغییرناپذیر است
        if (req.Status != StoreRequisitionStatuses.Submitted) return Fail("مقدار تأییدشده فقط در وضعیت Submitted قابل تعیین است و پس از تأیید تغییر نمی‌کند.");
        if (by == req.RequestedById) return Fail("تفکیک وظایف: درخواست‌دهنده نمی‌تواند مقدار تأییدشده‌ی درخواست خودش را تعیین کند.");

        var changes = new List<string>();
        foreach (var (lineId, qty) in approved)
        {
            var line = req.Lines.FirstOrDefault(l => l.StoreRequisitionLineId == lineId && l.IsActive);
            if (line == null) return Fail($"خط {lineId} به این درخواست تعلق ندارد.");
            if (qty < 0 || qty > line.RequestedQuantity) return Fail($"مقدار تأییدشده‌ی خط {lineId} باید بین ۰ و مقدار درخواستی ({line.RequestedQuantity}) باشد.");
            if (line.IssuedQuantity > qty) return Fail($"مقدار تأییدشده‌ی خط {lineId} نمی‌تواند از مقدار تحویل‌شده ({line.IssuedQuantity}) کمتر باشد.");
            changes.Add($"line {lineId}: {line.ApprovedQuantity?.ToString() ?? "-"} -> {qty}");
            line.ApprovedQuantity = qty;
        }

        await AddHistoryAsync(req, req.Status, req.Status, by, "ApprovedQuantity: " + string.Join("; ", changes), requestKey);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ------------------------------------------------------------------ گذار وضعیت

    private static string ActionFor(string from, string next) => next switch
    {
        StoreRequisitionStatuses.Submitted => StoreRequisitionActions.Submit,
        StoreRequisitionStatuses.Approved => StoreRequisitionActions.Approve,
        StoreRequisitionStatuses.Rejected => StoreRequisitionActions.Reject,
        StoreRequisitionStatuses.Reserved => StoreRequisitionActions.Reserve,
        StoreRequisitionStatuses.Cancelled => StoreRequisitionActions.Cancel,
        StoreRequisitionStatuses.Closed => from == StoreRequisitionStatuses.Issued ? StoreRequisitionActions.Close : StoreRequisitionActions.CloseShort,
        _ => StoreRequisitionActions.Issue
    };

    private async Task<(bool Success, string? Error)> AdvanceCoreAsync(int id, string next, string? reason, StoreRequisitionActor actor, string? requestKey)
    {
        if (next == StoreRequisitionStatuses.Issued)
            return await IssueCoreAsync(id, null, reason, actor, requestKey);   // Issued = تحویل تمام باقی‌مانده‌ی رزرو
        if (next == StoreRequisitionStatuses.PartiallyIssued)
            return Fail("تحویل بخشی فقط از طریق IssueAsync با مقدار هر خط انجام می‌شود.");
        if (!StoreRequisitionStatuses.All.Contains(next)) return Fail($"وضعیت «{next}» نامعتبر است.");

        var req = await LoadAsync(id);
        if (req == null) return Fail("Store requisition not found.");
        var from = req.Status;
        if (!StoreRequisitionStatuses.CanTransition(from, next))
            return Fail($"گذار «{from}» به «{next}» مجاز نیست.");

        var (by, authError) = await AuthorizeAsync(actor, ActionFor(from, next));
        if (authError != null) return Fail(authError);

        if (next == StoreRequisitionStatuses.Approved)
        {
            if (by == req.RequestedById) return Fail("تفکیک وظایف: درخواست‌دهنده نمی‌تواند درخواست خودش را تأیید کند.");

            // مقدار تأییدشده‌ی تعیین‌نشده = مقدار درخواستی (ثبت صریح تا بعداً مبهم نماند)
            foreach (var l in req.Lines.Where(l => l.IsActive && !l.ApprovedQuantity.HasValue)) l.ApprovedQuantity = l.RequestedQuantity;
            if (!req.Lines.Any(l => l.IsActive && (l.ApprovedQuantity ?? 0) > 0)) return Fail("حداقل یک خط باید مقدار تأییدشده‌ی مثبت داشته باشد.");
            req.ApprovedById = by;
            req.ApprovedDate = DateTime.Now;
        }

        if (next == StoreRequisitionStatuses.Reserved)
        {
            var error = await ReserveLinesAsync(req, by!.Value);
            if (error != null) return Fail(error);
        }

        if (next is StoreRequisitionStatuses.Rejected or StoreRequisitionStatuses.Cancelled or StoreRequisitionStatuses.Closed)
        {
            var releaseError = await ReleaseRemainingAsync(req, by!.Value, reason);
            if (releaseError != null) return Fail(releaseError);
        }

        var historyReason = from is StoreRequisitionStatuses.PartiallyIssued or StoreRequisitionStatuses.Reserved && next == StoreRequisitionStatuses.Closed
            ? "CloseShort: " + reason + ShortfallNote(req)
            : reason;
        await AddHistoryAsync(req, from, next, by, historyReason, requestKey);
        req.Status = next;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    private static string ShortfallNote(StoreRequisition req) =>
        " [" + string.Join("; ", req.Lines.Where(l => l.IsActive).Select(l =>
            $"line {l.StoreRequisitionLineId}: approved {(l.ApprovedQuantity ?? l.RequestedQuantity)}, issued {l.IssuedQuantity}")) + "]";

    private async Task AddHistoryAsync(StoreRequisition req, string from, string to, int? by, string? reason, string? requestKey) =>
        await _context.StoreRequisitionStatusHistories.AddAsync(new StoreRequisitionStatusHistory
        {
            StoreRequisitionId = req.StoreRequisitionId, FromStatus = from, ToStatus = to,
            ChangedById = by, ChangedDate = DateTime.Now, Reason = reason, RequestKey = string.IsNullOrWhiteSpace(requestKey) ? null : requestKey.Trim()
        });

    /// <summary>
    /// Phase 2 / P4: رزرو/تحویل/آزادسازی در سطح (متریال، انبار) کار می‌کند و محل ندارد. بیش از یک ردیف فعال (تکراری یا چند محل) مبهم است:
    /// قبلاً FirstOrDefault بدون ترتیب یکی را دلخواه برمی‌داشت (رزرو روی یک ردیف، تحویل روی ردیف دیگر)؛ اکنون خطای صریح بدون هیچ اثر.
    /// </summary>
    private async Task<(MaterialStock? Stock, string? Error)> StockOfAsync(int materialItemId, int warehouseId)
    {
        var lookup = await StockRowResolver.FindForWarehouseAsync(_context, materialItemId, warehouseId, activeOnly: true);
        return lookup.IsAmbiguous
            ? (null, StockRowResolver.AmbiguousMessage(materialItemId, warehouseId, null, lookup.CandidateIds))
            : (lookup.Row, null);
    }

    private async Task AddTransactionAsync(StoreRequisition req, int materialItemId, int warehouseId, string type, decimal qty, int by, string reason,
        int? lineId, MaterialReservation reservation) =>
        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId, WarehouseId = warehouseId, TransactionType = type, Quantity = qty,
            ReferenceType = "StoreRequisition", ReferenceId = req.StoreRequisitionId, WorkOrderId = req.WorkOrderId,
            StoreRequisitionLineId = lineId, MaterialReservation = reservation,
            EmployeeId = by, TransactionDate = DateTime.Now, Reason = reason
        });

    // ------------------------------------------------------------------ رزرو (Idempotent، خط‌به‌خط)

    private async Task<string?> ReserveLinesAsync(StoreRequisition req, int by)
    {
        var lineIds = req.Lines.Select(l => l.StoreRequisitionLineId).ToList();
        var existing = await _context.MaterialReservations
            .Where(r => r.StoreRequisitionLineId != null && lineIds.Contains(r.StoreRequisitionLineId.Value) && r.Status != MaterialReservationStatuses.Released)
            .ToListAsync();

        foreach (var line in req.Lines.Where(l => l.IsActive).OrderBy(l => l.StoreRequisitionLineId))
        {
            var target = line.ApprovedQuantity ?? line.RequestedQuantity;
            var already = existing.Where(r => r.StoreRequisitionLineId == line.StoreRequisitionLineId).Sum(r => r.Quantity);
            var needed = target - already;
            if (needed <= 0) continue;                      // Idempotent: همین خط قبلاً رزرو شده است

            var (stock, stockError) = await StockOfAsync(line.MaterialItemId, req.WarehouseId);
            if (stockError != null) return stockError;
            if (stock == null || stock.AvailableQuantity < needed)
                return $"موجودی قابل‌رزرو کافی نیست برای متریال شماره {line.MaterialItemId}.";

            stock.ReservedQuantity += needed;
            if (stock.ReservedQuantity > stock.OnHandQuantity) return $"رزرو بیش از موجودی برای متریال {line.MaterialItemId}.";
            stock.LastUpdated = DateTime.Now;

            var reservation = new MaterialReservation
            {
                MaterialItemId = line.MaterialItemId, WarehouseId = req.WarehouseId, Quantity = needed,
                Status = MaterialReservationStatuses.Active, StoreRequisitionId = req.StoreRequisitionId,
                StoreRequisitionLineId = line.StoreRequisitionLineId, WorkOrderId = req.WorkOrderId,
                ReservedById = by, ReservedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now
            };
            await _context.MaterialReservations.AddAsync(reservation);

            await AddTransactionAsync(req, line.MaterialItemId, req.WarehouseId, "Reservation", needed, by,
                $"رزرو برای StoreRequisition #{req.RequisitionNumber}", line.StoreRequisitionLineId, reservation);
        }
        return null;
    }

    // ------------------------------------------------------------------ تحویل (کامل/بخشی)

    private async Task<(bool Success, string? Error)> IssueCoreAsync(int id, IReadOnlyDictionary<int, decimal>? quantities, string? reason, StoreRequisitionActor actor, string? requestKey)
    {
        var (by, authError) = await AuthorizeAsync(actor, StoreRequisitionActions.Issue);
        if (authError != null) return Fail(authError);

        var req = await LoadAsync(id);
        if (req == null) return Fail("Store requisition not found.");

        var from = req.Status;
        if (from is not (StoreRequisitionStatuses.Reserved or StoreRequisitionStatuses.PartiallyIssued))
            return Fail($"تحویل فقط در وضعیت Reserved یا PartiallyIssued مجاز است (وضعیت فعلی: {from}).");
        if (by == req.RequestedById) return Fail("تفکیک وظایف: درخواست‌دهنده نمی‌تواند کالای درخواست خودش را تحویل دهد.");

        var lineIds = req.Lines.Select(l => l.StoreRequisitionLineId).ToList();
        var reservations = await _context.MaterialReservations
            .Where(r => (r.StoreRequisitionId == req.StoreRequisitionId || (r.StoreRequisitionLineId != null && lineIds.Contains(r.StoreRequisitionLineId.Value)))
                        && (r.Status == MaterialReservationStatuses.Active || r.Status == MaterialReservationStatuses.PartiallyIssued))
            .OrderBy(r => r.MaterialReservationId).ToListAsync();

        // رزروهای قدیمی (قبل از ردیابی خط‌به‌خط): فقط وقتی کالا دقیقاً یک خط دارد به آن وصل می‌شوند؛ ۰ یا بیش از ۱ ⇒ رد (حدس زده نمی‌شود)
        foreach (var legacy in reservations.Where(r => r.StoreRequisitionLineId == null))
        {
            var candidates = req.Lines.Where(l => l.IsActive && l.MaterialItemId == legacy.MaterialItemId).ToList();
            if (candidates.Count != 1)
                return Fail($"رزرو قدیمی متریال {legacy.MaterialItemId} به یک خط یکتا قابل انتساب نیست؛ ابتدا آن را اصلاح/آزاد کنید.");
            legacy.StoreRequisitionLineId = candidates[0].StoreRequisitionLineId;
        }

        if (quantities != null)
            foreach (var key in quantities.Keys)
                if (req.Lines.All(l => l.StoreRequisitionLineId != key)) return Fail($"خط {key} به این درخواست تعلق ندارد.");

        var issuedAny = false;
        var issueNotes = new List<string>();
        foreach (var line in req.Lines.Where(l => l.IsActive).OrderBy(l => l.StoreRequisitionLineId))
        {
            var lineReservations = reservations.Where(r => r.StoreRequisitionLineId == line.StoreRequisitionLineId).ToList();
            var remainingReserved = lineReservations.Sum(r => r.Quantity - r.IssuedQuantity);
            var approved = line.ApprovedQuantity ?? line.RequestedQuantity;

            decimal qty;
            if (quantities == null) qty = remainingReserved;
            else if (!quantities.TryGetValue(line.StoreRequisitionLineId, out qty)) continue;

            if (qty < 0) return Fail($"مقدار تحویل خط {line.StoreRequisitionLineId} نمی‌تواند منفی باشد.");
            if (qty == 0) continue;
            if (qty > remainingReserved) return Fail($"مقدار تحویل خط {line.StoreRequisitionLineId} ({qty}) از باقی‌مانده‌ی رزرو ({remainingReserved}) بیشتر است.");
            if (line.IssuedQuantity + qty > approved) return Fail($"مقدار تحویل‌شده‌ی خط {line.StoreRequisitionLineId} نمی‌تواند از مقدار تأییدشده ({approved}) بیشتر شود.");

            var toIssue = qty;
            foreach (var res in lineReservations)
            {
                if (toIssue <= 0) break;
                var take = Math.Min(toIssue, res.Quantity - res.IssuedQuantity);
                if (take <= 0) continue;

                var (stock, stockError) = await StockOfAsync(res.MaterialItemId, res.WarehouseId ?? req.WarehouseId);
                if (stockError != null) return Fail(stockError);
                if (stock == null) return Fail($"ردیف موجودی متریال {res.MaterialItemId} یافت نشد.");
                if (stock.OnHandQuantity < take || stock.ReservedQuantity < take) return Fail($"موجودی/رزرو متریال {res.MaterialItemId} برای تحویل کافی نیست.");

                stock.OnHandQuantity -= take;
                stock.ReservedQuantity -= take;
                stock.LastUpdated = DateTime.Now;

                res.IssuedQuantity += take;
                res.Status = res.IssuedQuantity >= res.Quantity ? MaterialReservationStatuses.FullyIssued : MaterialReservationStatuses.PartiallyIssued;

                await AddTransactionAsync(req, res.MaterialItemId, res.WarehouseId ?? req.WarehouseId, "Issue", take, by!.Value,
                    reason ?? $"تحویل برای StoreRequisition #{req.RequisitionNumber}", line.StoreRequisitionLineId, res);
                toIssue -= take;
            }

            line.IssuedQuantity += qty;
            issueNotes.Add($"line {line.StoreRequisitionLineId}: +{qty} (issued {line.IssuedQuantity}/{approved})");
            issuedAny = true;
        }

        if (!issuedAny) return Fail("هیچ مقداری برای تحویل مشخص نشده یا باقی‌مانده‌ی رزرو صفر است.");

        var fullyIssued = req.Lines.Where(l => l.IsActive && (l.ApprovedQuantity ?? l.RequestedQuantity) > 0)
            .All(l => l.IssuedQuantity >= (l.ApprovedQuantity ?? l.RequestedQuantity));
        var to = fullyIssued ? StoreRequisitionStatuses.Issued : StoreRequisitionStatuses.PartiallyIssued;

        await AddHistoryAsync(req, from, to, by, (reason == null ? "" : reason + " | ") + string.Join("; ", issueNotes), requestKey);
        req.Status = to;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ------------------------------------------------------------------ آزادسازی باقی‌مانده‌ی رزرو (Reject/Cancel/Close با کسری)

    private async Task<string?> ReleaseRemainingAsync(StoreRequisition req, int by, string? reason)
    {
        var lineIds = req.Lines.Select(l => l.StoreRequisitionLineId).ToList();
        var reservations = await _context.MaterialReservations
            .Where(r => (r.StoreRequisitionId == req.StoreRequisitionId || (r.StoreRequisitionLineId != null && lineIds.Contains(r.StoreRequisitionLineId.Value)))
                        && (r.Status == MaterialReservationStatuses.Active || r.Status == MaterialReservationStatuses.PartiallyIssued))
            .ToListAsync();

        foreach (var res in reservations)
        {
            var remaining = res.Quantity - res.IssuedQuantity;
            if (remaining > 0)
            {
                var (stock, stockError) = await StockOfAsync(res.MaterialItemId, res.WarehouseId ?? req.WarehouseId);
                if (stockError != null) return stockError;   // مبهم: آزادسازی روی ردیف دلخواه ممنوع؛ کل گذار متوقف (RunAsync تغییرات را Rollback می‌کند)
                if (stock != null)
                {
                    stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - remaining);
                    stock.LastUpdated = DateTime.Now;
                }

                await AddTransactionAsync(req, res.MaterialItemId, res.WarehouseId ?? req.WarehouseId, "ReservationRelease", remaining, by,
                    reason ?? $"آزادسازی رزرو StoreRequisition #{req.RequisitionNumber}", res.StoreRequisitionLineId, res);
            }

            res.Status = res.IssuedQuantity > 0 ? MaterialReservationStatuses.FullyIssued : MaterialReservationStatuses.Released;
        }

        return null;
    }
}
