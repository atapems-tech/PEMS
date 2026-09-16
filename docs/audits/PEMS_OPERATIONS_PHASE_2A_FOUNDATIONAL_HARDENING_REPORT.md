# PEMS Operations Phase 2A — Foundational Hardening Report

**Scope**: Production semantics, batch-aware inventory, production targets, downtime,
and a reporting-period/calendar foundation — hardening only, per
`docs/audits/PEMS_OPERATIONAL_DATA_AND_PERIOD_REPORTING_ARCHITECTURE_AUDIT.md`'s roadmap.
Packaging, Daily Operations Submission, QualityTestResult, Product aliases, and any large
dashboard were explicitly out of scope and were **not** implemented — confirmed by the file
list in Section 20 below.

---

## 1. Initial Audit Findings

Before writing any code, the following were re-read directly from the live repository (not
assumed from the prior three audit documents):

- `ProductionOrder`/`ProductionRun`/`ProductionBatch`/`ProductionInput`/`ProductionOutput`/
  `ProductionLoss`/`ProductionEvent`/`ProductionWorkCenter`/`ShiftDefinition` — confirmed
  unchanged since Phase 1; `ProductionOrderService`/`ProductionRunService` confirmed to still
  use plain `Dictionary<string,string[]>` transition tables, not `IWorkflowRuntime`.
- `MaterialItem`, `MaterialUOM`, `Warehouse`, `StorageLocation`, `MaterialStock`,
  `InventoryTransaction` — confirmed unchanged; `IMaterialStockService` confirmed to have
  exactly 4 mutating methods (`ReceiveAsync`/`AdjustAsync`/`ReturnAsync`/`TransferAsync`) plus
  `GetLowStockAsync`/`SetThresholdsAsync`/`GetLastTransactionIdAsync`.
- `IInventoryTransactionService` confirmed to have exactly one method
  (`GetLedgerAsync`) before this phase.
- Repository-wide search for `Gross`, `Net Production`, `GrossQuantity`, `NetQuantity` across
  `PEMS.Domain` and `docs/` — **zero matches outside this session's own prior audit
  documents.** No business definition exists anywhere. (Section 4.)
- Repository-wide search for `Jalali`, `PersianCalendar`, `ShamsiDate` — no real application
  code matches (only unrelated design-mockup scaffold files under `src/stitch-temp/`).
- `IInspectionService` was re-read directly and confirmed (again) to be an Equipment
  condition-monitoring concept (`GetForEquipmentAsync`, `measurementValue`/limits), not a
  reusable downtime-event model — confirming a dedicated `ProductionDowntimeEvent` was
  genuinely needed, not a duplicate of something reusable.
- `ReportController` confirmed to have exactly 4 existing report actions
  (`Index`/`Engineering`/`Vdr`/`Cmms`/`Procurement`), all taking raw `fromDate`/`toDate` with
  **no permission check on any of them** — this existing (imperfect) convention was matched
  exactly for the new `Operations` action rather than introducing an inconsistency by gating
  only the new one.

No contradiction between the prior audit documents and the live code was found. No stop
condition was triggered.

---

## 2. Existing Architecture Reused

| Reused as-is | Evidence |
|---|---|
| `InventoryTransaction` (ledger) / `MaterialStock` (projection) architecture | Not replaced — extended with one nullable column only |
| `IMaterialStockService`'s additive-optional-parameter precedent (from Phase 1's `referenceType`/`referenceId`) | Copied exactly for the new `productionBatchId` parameter |
| `ProductionOrderService`/`ProductionRunService`'s explicit transition-dictionary pattern | Followed for no new state machine was introduced this phase |
| `PEMSDbContext.SaveChangesAsync` audit interceptor | New entities participate automatically, zero new code |
| `RolePermission`/`PermissionService` "Production" module | Unchanged, not renamed (Section 5) |
| `ReportController` + per-module report-service pattern | Extended with one new action + one new service, same shape as the 4 existing ones |
| `ExcelExportHelper`/`ClosedXML`/`QuestPDF` export infrastructure | Not touched — no new report export was built this phase |
| `PagedResult<T>`/`DataGridPageSize` (`PEMS.Application/Common`) | `ReportingPeriod` placed in the same namespace, following the same "shared, reusable, not module-specific" convention |
| `ShiftDefinition`/`ProductionWorkCenter` (Phase 1) | Reused directly by `ProductionTarget`/`ProductionDowntimeEvent` — no parallel master data |

---

## 3. Business Assumptions

Only two assumptions were made, both explicitly labeled as unconfirmed rather than silently
applied:

1. **Calendar-day boundaries (midnight-to-midnight, server-local time) are used for
   `PeriodType.Day`/`Month`**, since no operational-day-start-time business rule was found
   anywhere (Section 9 below) — this is the same simplification 100% of the rest of PEMS
   already makes with `DateTime.Now`, not a new assumption invented for this phase.
2. In the Week-boundary demonstration unit test only (not in any shipped default), Saturday was
   used as an illustrative `weekStartDay` value to prove the calculation works — **this is not
   a default anywhere in the shipped code**; `ReportingPeriod.Resolve(PeriodType.Week, ...)`
   throws `InvalidOperationException` if `weekStartDay` is not explicitly supplied by the
   caller, precisely so no default is ever silently assumed in production use (Section 9).

No other business rule was guessed.

---

## 4. Gross/Net Decision — NOT IMPLEMENTED (business decision required)

Per the mandatory Business Semantics Gate (Section 4 of the task): a repository-wide search
for `Gross`, `Net`, `GrossQuantity`, `NetQuantity` across `PEMS.Domain`, comments, existing
reports, and `docs/` found **zero matches outside this session's own prior audit documents**.
No definitive PEMS business rule for Gross vs. Net production exists.

**Per the explicit instruction, implementation of any Gross/Net schema change was stopped.**
No `GrossQuantity` column was added to `ProductionOutput`. `ProductionOutput.Quantity` retains
its exact pre-existing meaning and behavior, unchanged.

**What depends on this decision, for the record:**
- Accepted/Rejected/Pending-QC/Blocked breakdowns implemented in this phase (Section 8) use
  `ProductionOutput.Quantity` and `ProductionBatch.QualityStatus` only — they do **not** claim
  to be Gross or Net figures, just "quantity recorded as output, broken down by quality
  status." This is accurate today regardless of how Gross/Net is eventually defined.
- A future `ProductionOutput.GrossQuantity` (or similar) column, if the business defines one,
  remains additive and does not require redesigning anything built in this phase.
- `OperationsReportSummaryDto` was verified (by a dedicated unit test,
  `OperationsReportSummaryDto_HasNoGrossOrNetField`) to expose no `GrossQuantity`/
  `NetQuantity` property, so no false precision is implied to any consumer of this DTO.

**This remains a business-owner decision required before Gross/Net-specific KPIs (e.g., Yield
computed against a Gross basis) can be implemented.**

---

## 5. ProductionTarget Decision — IMPLEMENTED

A new `ProductionTarget` entity (`PEMS.Domain/Entities/ProductionTarget.cs`) was added,
storing **target only** — `TargetDate`, optional `ProductionOrderId`, `MaterialItemId`,
`ProductionWorkCenterId`, optional `ShiftDefinitionId`, `TargetQuantity`, optional `UomId`,
`IsActive`. It has **no** `ActualQuantity`, `Variance`, or `AchievementPercentage` column —
verified by a dedicated unit test (`ProductionTarget_NeverStoresActualOrVariance`) that
reflects the entity's actual property list at runtime, not just a code-review claim.

**Duplicate prevention**: a two-layer guard, matching the existing `BarcodeIdentity` precedent
in this codebase (an application-level check *and* a database constraint, neither alone):
1. A `ProductionTargetService.CreateAsync` query checks for an existing active target with the
   same `(TargetDate, ProductionWorkCenterId, MaterialItemId, ShiftDefinitionId)` before
   inserting.
2. A SQL Server filtered unique index on the same four columns
   (`IX_ProductionTargets_TargetDate_ProductionWorkCenterId_MaterialItemId_ShiftDefinitionId`,
   filtered `WHERE ShiftDefinitionId IS NOT NULL` — SQL Server's own default behavior, since a
   plain unique index already treats multiple `NULL`s as non-conflicting) catches the true
   concurrent-race case the application check alone cannot.

`IProductionTargetService`/`ProductionTargetService` were implemented with `CreateAsync`,
`GetListAsync`, and `DeactivateAsync`. **No web controller or view was added** for creating
targets in this phase — per the task's UI-scope limits (Section 30), this capability is
usable and testable via its service/tests today; a minimal UI is deferred to whichever phase
actually consumes targets in a user-facing workflow.

---

## 6. ProductionDowntimeEvent Decision — IMPLEMENTED

A new `ProductionDowntimeEvent` entity was added — `ProductionRunId`, `ProductionWorkCenterId`,
**optional** `EquipmentId` (confirmed not mandatory — a `Material`/`Process`/`Utility`/
`Planned` downtime cause need not reference any equipment), `StartTime`, nullable `EndTime`
(null = still open), `CauseCategory` (controlled whitelist: `Equipment`/`Process`/`Material`/
`Utility`/`Planned`/`Other`), `Description`, optional `SourceWorkRequestId`/
`SourceWorkOrderId`.

**Duration is never stored** — `ProductionDowntimeEventDto.DurationMinutes` is a computed
property (`EndTime - StartTime`), not a database column, verified by a dedicated unit test
(`ProductionDowntimeEvent_DoesNotDuplicateWorkOrderDowntimeHours`) confirming no
`DurationMinutes`/`DowntimeHours` property exists on the entity itself. `WorkOrder.
DowntimeHours`/`FailureRecord.DowntimeHours` were **not** read, written, or migrated by this
phase — confirmed by grep: no file changed in this phase references either field.

`IProductionDowntimeService`/`ProductionDowntimeService` implement `StartAsync`
(validates `CauseCategory` against the whitelist, validates `EquipmentId` only when supplied,
validates optional `WorkRequest`/`WorkOrder` references), `CloseAsync` (rejects `EndTime <=
StartTime`, rejects closing an already-closed event), and `GetForRunAsync`. No web
controller/view — same UI-scope reasoning as Section 5.

---

## 7. Inventory Batch-Link Design — IMPLEMENTED

`InventoryTransaction.ProductionBatchId` (nullable, FK to `ProductionBatch`,
`ON DELETE RESTRICT`, indexed) was added. The ledger/projection architecture
(`InventoryTransaction` = source of truth, `MaterialStock` = projected balance) was **not**
replaced or forked — `MaterialStock`'s grain
`(MaterialItemId, WarehouseId, StorageLocationId)` is unchanged, and no
`BatchInventory`/`LotInventory`/`BatchStock`/`QualityStock` table was created.

`IMaterialStockService.ReceiveAsync`/`AdjustAsync`/`ReturnAsync`/`TransferAsync` each gained
one new, trailing, optional `int? productionBatchId = null` parameter — the exact same
additive pattern Phase 1 already used for `referenceType`/`referenceId`. All 4 existing call
sites (`MaterialItemController.cs` ×3, `WorkOrderController.cs` ×1) were verified unchanged
and still compile without modification (confirmed by the clean build in Section 15 — a
breaking change here would have failed to compile, not just misbehaved).

`ProductionRunService.PostOutputToInventoryAsync`/`PostInputToInventoryAsync` were updated to
pass the already-known batch identity through (`output.ProductionBatchId` — always set, since
it's a required field on `ProductionOutput`; `input.SourceProductionBatchId` — the Phase 1
genealogy hook, may be null) — no new lookup or inference logic was added; the batch identity
was already sitting on the entity being posted.

---

## 8. Batch-Aware Transfer/Issue Behavior — IMPLEMENTED (capability), not yet UI-wired

`TransferAsync` and `ReturnAsync` also gained the same optional `productionBatchId` parameter.
When a transfer is explicitly tagged with a batch, **both** legs of the `TransferOut`/
`TransferIn` pair receive the same `ProductionBatchId` — verified by
`TransferAsync_WithBatch_BatchIdentitySurvivesOnBothLegs`, which asserts both generated rows
carry the identical batch ID. `AdjustAsync` (used for both manual stock corrections and
Production's own consumption posting) carries the batch through identically, verified by
`AdjustAsync_WithBatch_ConsumptionPreservesBatchReference`.

**No automatic batch inference exists** — if a caller does not know or does not supply a
`productionBatchId`, the resulting transaction's `ProductionBatchId` stays `null`, exactly as
before this phase (verified by `ReceiveAsync_WithoutBatch_ProductionBatchIdStaysNull_
BehaviorUnchanged`). This was a deliberate choice: `MaterialStock` pools all batches of a
given material/location together, so "which batch is this transfer moving" is inherently
ambiguous unless the caller states it — building automatic inference would have required
exactly the `MaterialStock` grain change this phase was explicitly told to avoid.

**No new UI was built** for a user to pick a batch when issuing/transferring stock manually —
the capability exists at the service layer (and is fully tested), but wiring it into
`MaterialItemController`'s existing Transfer/Adjust forms is deferred, consistent with the
UI-scope limits in Section 30 of the task.

A new read-model query, `IInventoryTransactionService.GetBatchBalancesAsync`, was added
(`InventoryTransactionService.cs`) — a `GROUP BY` over `InventoryTransaction` (never a second
ledger), using the **exact same sign convention `MaterialStockService` already implements**
(`TransferOut` is stored as a positive `Quantity` but subtracted from the running balance — the
query replicates this with a `CASE`-equivalent ternary, not a newly-invented sign rule). It
returns `QualityStatus` by joining to `ProductionBatch` at read time — never duplicated into
the inventory side, verified by `BatchBalance_ResolvesQualityStatus_FromProductionBatch_
NotDuplicated`, which changes `ProductionBatch.QualityStatus` after the first read and asserts
the next query reflects the change immediately (proving there is no stale copy anywhere).

---

## 9. Calendar/Period Semantics

Confirmed, unchanged from the prior audits, and now encoded (not guessed) in
`ReportingPeriod.cs`:

- **Day/Month**: deterministic calendar-day/Gregorian-month boundaries, server-local
  `DateTime`, no UTC layer (matching the rest of PEMS).
- **Week**: `ReportingPeriod.Resolve(PeriodType.Week, ...)` **requires an explicit
  `weekStartDay` parameter and throws `InvalidOperationException` if it is omitted** — verified
  by `Resolve_Week_WithoutExplicitWeekStartDay_Throws`. No default (e.g., Saturday) is baked
  into the shipped resolver, so `ReportController.Operations` (Section 11) deliberately does
  **not** expose a "Week" option in its period-type dropdown yet — offering it today would only
  produce a guaranteed exception, which would be worse than not offering it.
- **Jalali display**: not implemented, not stored, no parallel Jalali column — matches the
  task's explicit prohibition.
- **Operational day boundary** (a start time other than midnight) and **midnight-crossing
  shift attribution**: no business definition was found; **not implemented**. `ShiftDefinition.
  StartTime`/`EndTime` remain exactly as Phase 1 left them — no existing row was reinterpreted
  or modified.
- **Timezone**: no conversion layer; server-local time is used, matching the existing,
  unanimous convention across the entire codebase.

---

## 10. Reporting Foundation

`PEMS.Application/Common/ReportingPeriod.cs` (`PeriodType` enum: `Day`/`Week`/`Month`/
`CustomRange`, one `Resolve(...)` method returning a `Range { Start, End }` with `End`
exclusive) — the single shared place period-boundary logic now lives, per the task's explicit
"do not duplicate week/month/Jalali/shift calculation inside multiple reports" instruction.

`IOperationsReportService`/`OperationsReportService` (`GetSummaryAsync`) — reads only from
`ProductionOutput`, `ProductionLoss`, `ProductionBatch` (for `QualityStatus`), and
`ProductionTarget`. It does **not** read from, and there is no, `DailyOperationsSubmission` —
confirmed there is nothing to double-count against, since that entity was not built this phase
either. Returns `TotalOutputQuantity`, `AcceptedQuantity`/`RejectedQuantity`/
`PendingQcQuantity`/`BlockedQuantity` (from the `ProductionBatch.QualityStatus` join),
`TotalLossQuantity`, `TotalTargetQuantity`, and a computed `VarianceQuantity` that is
**`null`, never `0`, when no target exists** for the requested scope — verified by
`GetSummaryAsync_NoTarget_VarianceIsNull_NotZero`, precisely to avoid the misleading-zero
risk the task warns about.

One new `ReportController.Operations` action was added, following the exact existing
4-sibling-action pattern (same constructor-injection style, same `ViewData["Title"]`/
`["Section"]` convention — `"Operations & Production"`, matching the corrected boundary from
the prior task). A minimal view (`Views/Report/Operations.cshtml`) was added, and an
"Operations" button was added to the existing button row on all 5 pre-existing Report pages
(`Index`/`Engineering`/`Vdr`/`Cmms`/`Procurement`) for consistent cross-navigation — this
mirrors the exact pre-existing pattern those 5 pages already used for each other, not a new
navigation concept.

---

## 11. Migration Details

Migration name: **`AddOperationsFoundationPhase2A`**
(`PEMS.Persistence/Migrations/20260915175326_AddOperationsFoundationPhase2A.cs`)

Contents of `Up()`, read in full before applying: **exactly one `AddColumn` (nullable
`InventoryTransactions.ProductionBatchId`), two `CreateTable` calls (`ProductionTargets`,
`ProductionDowntimeEvents`), a set of `CreateIndex` calls (including the one filtered unique
index from Section 5), and one `AddForeignKey`.** Zero `DropTable`, zero `DropColumn`, zero
`AlterColumn` against any pre-existing table, zero raw `Sql(...)` statements. `Down()` (never
auto-applied) only reverses what this same migration created.

Applied via `dotnet ef database update` against the same dev database used throughout this
session (`localhost\SQLEXPRESS`/`PEMSDB`) — completed successfully, no errors.

---

## 12. Existing Data Preservation

Row counts were captured immediately before and after applying the migration:

| Table | Before | After |
|---|---|---|
| ProductionOrders | 0 | 0 |
| ProductionRuns | 0 | 0 |
| ProductionBatches | 0 | 0 |
| ProductionOutputs | 0 | 0 |
| ProductionInputs | 0 | 0 |
| ProductionLosses | 0 | 0 |
| ProductionEvents | 0 | 0 |
| InventoryTransactions | 2 | 2 (unchanged) |
| InventoryTransactions with `ProductionBatchId` populated | — | **0** |
| MaterialStocks | 2 | 2 (unchanged) |
| RolePermissions | 156 | 156 (unchanged) |
| ProductionTargets (new table) | — | 0 |
| ProductionDowntimeEvents (new table) | — | 0 |

**No row count changed for any pre-existing table. The 2 pre-existing `InventoryTransaction`
rows both have `ProductionBatchId = NULL` after migration — confirmed zero historical batch
relationships were fabricated.** No seed/demo data was inserted into any new table.
(The Production Core tables being empty reflects that this dev database has never been
exercised through the live web app with a logged-in session in this project — a pre-existing
condition unrelated to this phase, not something this phase's migration caused.)

---

## 13. RBAC/SoD Impact

**None.** No permission code was changed — `PermissionService.cs`, `AccessControlSeeder.cs`,
and the `"Production"` module name are untouched. No new web controller/action requiring a new
permission was added (Sections 5, 6 deliberately shipped service+tests only, no UI). The one
new UI surface, `ReportController.Operations`, was built to match its 4 existing siblings
exactly, none of which carry a permission check today — introducing one only on the new action
would have been an inconsistency, not a fix, and redesigning `ReportController`'s
authorization model wholesale is explicitly out of this phase's scope.

## 14. Audit Logging Impact

`ProductionTarget` and `ProductionDowntimeEvent` both inherit `BaseEntity` and are registered
as normal `DbSet`s — `PEMSDbContext.SaveChangesAsync`'s existing interceptor
(`PrepareAuditEntries`/`FinalizeAuditEntries`) covers them automatically, with zero new code,
exactly as it does for every Phase 1 entity. No second audit mechanism was created.

---

## 15. Tests Executed

**39 new tests**, all passing:

- `ProductionTargetServiceTests.cs` — 7 tests: valid creation, zero/negative quantity
  rejection, invalid work center rejection, duplicate-grain rejection (same date/work
  center/material/shift), a *different* shift for the same date/work center/material
  succeeding (proving the grain, not just the date, is what's checked), a structural proof the
  entity has no Actual/Variance field, deactivation excluding a target from the active list.
- `ProductionDowntimeServiceTests.cs` — 7 tests: valid open-event creation, optional
  `EquipmentId` accepted as null, invalid cause-category rejection, duration correctly derived
  from Start/End (45 minutes computed, never stored), end-before-start rejection, double-close
  rejection, a structural proof the entity duplicates neither `DowntimeHours` nor a stored
  `DurationMinutes` column.
- `InventoryBatchTraceabilityTests.cs` — 10 tests, one per item in the task's Section 14
  checklist: Production output creates a batch-linked transaction end-to-end; a non-batch
  `ReceiveAsync` call leaves `ProductionBatchId` null and behaves exactly as before; batch
  balances query correctly excludes non-batch rows and groups by batch; a batch-tagged
  transfer's *both* legs carry the batch; a batch-tagged consumption (`AdjustAsync`) preserves
  the reference; a batch balance reconciles exactly against `MaterialStock.OnHandQuantity` for
  the same scenario; `QualityStatus` resolves live from `ProductionBatch` with no staleness;
  double-posting is still rejected with the new batch-aware path; and a dedicated proof that
  manual/historical transactions never have a batch fabricated onto them.
- `ReportingPeriodTests.cs` — 7 tests: Day = exact midnight-to-midnight boundaries; Month =
  correct Gregorian month including a leap-year February; Week without an explicit start day
  throws (not a guessed default); Week with an explicit start day returns a correct 7-day
  range; CustomRange is inclusive of the requested end date; CustomRange with an inverted or
  missing range throws.
- `OperationsReportServiceTests.cs` — 7 tests: total output summation; the Accepted/Rejected/
  Pending-QC breakdown by `ProductionBatch.QualityStatus`; loss total from `ProductionLoss`;
  variance being `null` (not `0`) with no target recorded; variance correctly computed once a
  target exists; output outside the requested period correctly excluded; a structural proof
  the summary DTO carries no Gross/Net field.
- 1 new regression-coverage test added to the existing `ProductionRunServiceTests.cs`
  (`RecordLoss_RworkCategory_IsRecordedAsPureLoss_NoAutomaticReEntryIntoProduction`), per
  Section 23's explicit instruction to document, not implement, the current Rework behavior.
- 2 new tests added to the existing `BarcodeServiceTests.cs` file were **not** added this
  phase (that work belongs to Phase 1, already present and unchanged).

Targeted run (`--filter` over all Phase 2A test classes plus the existing Phase 1 Production
test classes, to confirm no interaction/regression between old and new Production tests
specifically):
```
Passed! Failed: 0, Passed: 62, Skipped: 0, Total: 62, Duration: 5s
```

## 16. Full Regression Results

```
dotnet build PEMS.slnx --configuration Debug
→ Build succeeded, 0 errors, 22 warnings (all pre-existing, none in any file this phase touched)

dotnet test PEMS.Tests/PEMS.Tests.csproj --no-build
→ Passed! Failed: 0, Passed: 1393, Skipped: 0, Total: 1393, Duration: 10m 1s
```

1393 = 1354 pre-existing (confirmed at the end of the prior Boundary Correction task) + 39 new
Phase 2A tests. **Zero regressions, zero skipped.**

A first attempt at this full run reported 3 failures (all in `PEMS.Tests.Security.
FileSecurityHttpTests`, a `WebApplicationFactory`-based integration test class this phase
never touched) with an implausible reported duration of "14h 25m" — strong evidence the host
machine suspended/slept partway through that run, which is known to disrupt in-process
`TestServer` integration tests through no fault of the code under test. The suite was
re-run fresh, uninterrupted, and came back fully clean (above) — the 3 failures did not
reproduce and are recorded here as a transient environmental artifact, not silently
discarded.

---

## 17. Remaining Architectural Risks

1. **Gross/Net remains undefined** — the single highest-priority open business decision;
   nothing in this phase depends on it being resolved, but Yield/OEE-style KPIs remain blocked
   until it is (Section 4).
2. **Operational-day boundary and midnight-crossing-shift attribution remain undefined** —
   `ReportingPeriod`'s Day/Month boundaries are simple calendar boundaries, which may not match
   how a 22:00–06:00 shift's output should actually be attributed for reporting once that
   business rule is set (Section 9).
3. **Week-start-day remains undefined** — `PeriodType.Week` is implemented but intentionally
   unusable (throws) until a caller supplies an explicit value; no UI exposes it yet.
4. **Batch-aware issue/transfer has no UI** — the service-layer capability is complete and
   tested, but a warehouse user cannot yet pick a specific batch when transferring or adjusting
   stock through the existing `MaterialItemController` forms.
5. **Rework has no re-entry mechanism** — a `"Rework"` loss remains a pure loss with no
   traceable path back into a later `ProductionOutput`; documented and regression-covered, not
   fixed (Section 23 of the task explicitly deferred this).
6. **`MaterialItem.MaterialGrade` is not snapshotted onto `ProductionOutput`** — a future grade
   edit could still silently affect "production by grade" historical reports; not addressed
   this phase (Section 24 of the task explicitly deferred this too).
7. **`ReportController` has no permission gating on any action**, old or new — a pre-existing
   condition across all 5 actions, not introduced or worsened here, but worth a dedicated
   look in a future security-focused pass.

---

## 18. Explicitly Deferred Packaging Work

Not implemented, not partially implemented: `PackagingOrder`/`Run`/`Input`/`Output`/`Loss`/
`Event`, `Package`, `HandlingUnit`/Pallet management, `BarcodeIdentity` extension to Package/
HandlingUnit. Confirmed by the file list in Section 20 — no file with "Packag" in its name was
created.

## 19. Explicitly Deferred Daily Operations Work

Not implemented: `DailyOperationsSubmission`/`ShiftProductionSubmission`, any submission/
approval lifecycle for operational data. `OperationsReportService` reads only from existing
Phase 1 authoritative facts (Section 10) — there is nothing for a future submission entity to
conflict or double-count with, by construction.

## 20. Files Changed

**New (24 files):**
```
PEMS.Domain/Entities/ProductionTarget.cs
PEMS.Domain/Entities/ProductionDowntimeEvent.cs
PEMS.Application/DTOs/OperationsFoundationDtos.cs
PEMS.Application/Common/ReportingPeriod.cs
PEMS.Application/Interfaces/IProductionTargetService.cs
PEMS.Application/Interfaces/IProductionDowntimeService.cs
PEMS.Application/Interfaces/IOperationsReportService.cs
PEMS.Persistence/Configurations/OperationsFoundationConfigurations.cs
PEMS.Persistence/Servises/ProductionTargetService.cs
PEMS.Persistence/Servises/ProductionDowntimeService.cs
PEMS.Persistence/Servises/OperationsReportService.cs
PEMS.Persistence/Migrations/20260915175326_AddOperationsFoundationPhase2A.cs (+ .Designer.cs)
PEMS.Web/Views/Report/Operations.cshtml
PEMS.Tests/Cmms/ProductionTargetServiceTests.cs
PEMS.Tests/Cmms/ProductionDowntimeServiceTests.cs
PEMS.Tests/Cmms/InventoryBatchTraceabilityTests.cs
PEMS.Tests/Cmms/OperationsReportServiceTests.cs
PEMS.Tests/Engineering/ReportingPeriodTests.cs
docs/audits/PEMS_OPERATIONS_PHASE_2A_FOUNDATIONAL_HARDENING_REPORT.md (this file)
```

**Modified (14 files, all additive):**
```
PEMS.Domain/Entities/InventoryTransaction.cs           (1 new nullable FK property)
PEMS.Persistence/Configurations/InventoryTransactionConfiguration.cs (FK + index for the above)
PEMS.Persistence/Context/PEMSDbContext.cs                (2 new DbSets)
PEMS.Persistence/Migrations/PEMSDbContextModelSnapshot.cs (EF-generated, reflects new schema)
PEMS.Application/Interfaces/IMaterialStockService.cs      (1 new optional param on 4 methods)
PEMS.Persistence/Servises/MaterialStockService.cs         (implements the above)
PEMS.Application/Interfaces/IInventoryTransactionService.cs (1 new method)
PEMS.Persistence/Servises/InventoryTransactionService.cs   (implements the above)
PEMS.Persistence/Servises/ProductionRunService.cs          (2 call sites now pass productionBatchId)
PEMS.Web/Controllers/ReportController.cs                   (1 new action, 1 new constructor param)
PEMS.Web/Views/Report/{Index,Engineering,Vdr,Cmms,Procurement}.cshtml (1 new nav button each)
PEMS.Web/Program.cs                                          (3 new DI registrations)
PEMS.Tests/Cmms/ProductionRunServiceTests.cs                 (1 new regression test, Section 23)
```

**Note**: `git status` also shows `.gitignore` and `CLAUDE.md` as modified, and
`PEMS.Web/Views/Shared/{_Layout,_PemsStageDiagram}.cshtml`,
`PEMS.Web/Views/EngineeringDocuments/Details.cshtml`, `PEMS.Web/wwwroot/css/site.css` as
modified — **none of these were touched by this phase.** The `_Layout`/`_PemsStageDiagram`/
`EngineeringDocuments`/`site.css` changes are pre-existing, uncommitted changes from an earlier
session task. The `.gitignore`/`CLAUDE.md` changes were made by a third-party VS Code
extension ("Zaemit Visual HTML/CSS Editor") unrelated to this session's work entirely — flagged
separately to the user in this response, not part of this phase's deliverable, and not
reverted here since altering files outside this task's explicit scope without being asked is
not this phase's place to decide.

## 21. Migration Name

`AddOperationsFoundationPhase2A` (`20260915175326_AddOperationsFoundationPhase2A`)

---

## 22. Bilingual Terminology (English / فارسی)

New Operations Phase 2A concepts introduced by this phase, per the task's bilingual
documentation requirement:

| English | فارسی |
|---|---|
| Production Target | هدف تولید |
| Production Downtime Event | رویداد توقف تولید |
| Batch-Aware Inventory | موجودی بچ-آگاه |
| Reporting Period | بازه‌ی گزارش‌گیری |
| Operational Day | روز عملیاتی |
| Cause Category (downtime) | دسته‌بندی علت (توقف) |
| Variance (Target vs. Actual) | انحراف (هدف در برابر واقعی) |

No new user-facing workflow was introduced this phase (Sections 5-6 are service/test-only, no
UI) — the Operations report page (Section 10) reuses English labels consistent with the rest
of `Views/Report/`, which is entirely English today; introducing Persian labels only on this
one new page would be inconsistent with its 4 existing siblings, so English was kept to match.

---

## 23. Final Report Classification

# FOUNDATION HARDENED WITH DEFERRED BUSINESS DECISIONS

All five foundational items the prior audit's roadmap called for were addressed: two were
**implemented** (`ProductionTarget`, `ProductionDowntimeEvent`), one was **implemented as an
additive schema + service capability** (batch-aware `InventoryTransaction`, including
transfer/issue propagation), one was **implemented as a deterministic foundation with its
one genuinely unresolved input (`weekStartDay`) made to fail loudly rather than guess**
(`ReportingPeriod`), and one (**Gross/Net**) was correctly **not implemented**, per the
task's own explicit Business Semantics Gate, because no business definition exists to
implement against. This is not a partial or incomplete result — declining to guess a business
rule when instructed not to is the correct outcome the gate was designed to produce, not a
shortfall.

Packaging design work can proceed once: (a) Gross/Net is defined by the business owner, since
Packaging's own reconciliation reports will need it; and (b) the operational-day/week-start
calendar decisions are made, since Packaging will want period reporting too. Neither blocker
is a code or architecture problem — both are business decisions this phase correctly
identified, surfaced, and did not invent answers for.

No commit or push was made as part of this task.
