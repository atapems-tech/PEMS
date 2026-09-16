# PEMS Operations & Production — Phase 1 Implementation Report

**Scope**: Production Core only (ProductionOrder → ProductionRun → ProductionInput/Output →
ProductionBatch/Lot → ProductionLoss → ProductionEvent), per
`docs/audits/PEMS_OPERATIONS_PRODUCTION_ARCHITECTURE_SPEC_v1.md` (Gate: APPROVED WITH
CONDITIONS). Sales/Dispatch, Packaging/Handling Unit, full Warehouse redesign, EPCIS, GS1
Digital Link, mobile/offline, and advanced OEE were explicitly out of scope and were **not**
implemented.

All file paths are relative to the repository root.

---

## 1. Executive Summary

Phase 1 adds a Production Core domain (9 new entities, 2 new application services, 3 new
controllers, 7 new views) on top of the existing PEMS infrastructure, with **zero
modifications to the shape of any pre-existing table** and **zero new cross-cutting
subsystems**. Every integration point identified as reusable in the architecture spec was
reused as designed: `Equipment`/`FunctionalLocation` (via a thin `ProductionWorkCenter`),
`MaterialItem`/`MaterialUOM` (as the Product/UOM master), `InventoryTransaction`/
`MaterialStock` (extended additively, not duplicated), `BarcodeIdentity` (extended with a new
`TargetType`, zero schema change), the existing `RolePermission`/`PermissionService` module
pattern, and the existing `SoDPolicy`/`AccessControlService` framework.

Build: **succeeded, 0 errors**. Full regression: **1354/1354 passing** (1329 pre-existing +
25 new tests, zero regressions, zero flaky failures on this run). Migration: **one additive
migration, `AddProductionCore`, inspected line-by-line — contains only `CreateTable`/
`CreateIndex`, zero `DropTable`/`DropColumn`/`AlterColumn`/raw SQL** — applied to the dev
database successfully.

---

## 2. Pre-Implementation Audit (Step 1 — re-audit against current tree)

Before writing any entity, the following were read directly from the current working tree
(not assumed from the prior architecture-spec pass) to confirm the reuse decisions still
held after commit `866c56a`:

- `PEMS.Domain/Common/BaseEntity.cs` — confirmed `CreatedBy`/`ModifiedBy`/`RowVersion`
  (real EF concurrency token)/`TenantId` are the only common fields; `IsActive`/`CreatedDate`
  are per-entity by convention, not on `BaseEntity`.
- `MaterialItem.cs`, `MaterialUOM.cs`, `Warehouse.cs`, `StorageLocation.cs`,
  `InventoryTransaction.cs` — confirmed field-for-field against the spec; `InventoryTransaction`
  is a plain class (not `BaseEntity`), append-only, with `ReferenceType`/`ReferenceId` already
  designed as an open extension point.
- `Equipment.cs`, `FunctionalLocation.cs` — confirmed `Equipment.FunctionalLocationId` is a
  real, optional FK (human-confirmed, separate from the raw `Area` string), and
  `FunctionalLocation` is a plain self-referencing tree.
- `BarcodeIdentity.cs`, `BarcodeService.cs` — confirmed the exact `SupportedTargetTypes`
  hash set and the three `switch` statements (`TargetExistsAsync`, `GetExistingTargetIdsAsync`,
  `ResolveTargetDisplayLabelAsync`) plus `BarcodeController`'s `RedirectToDetails` switch —
  these are the four places a new `TargetType` must be registered, confirmed unchanged since
  commit `866c56a`.
- `IMaterialStockService.cs` / `MaterialStockService.cs` — confirmed `ReceiveAsync`/
  `AdjustAsync` hard-coded `ReferenceType = "Manual"` and never set `ReferenceId` — this is
  the gap Phase 1 needed to close additively (Section 8).
- `INumberingService.cs` / `NumberingService.cs` — confirmed it requires a pre-registered
  `Defaults` entry per document type and uses a real SQL Server atomic `UPDATE ... OUTPUT`
  (not supported by the EF InMemory provider — confirmed via `PEMS.Tests/Engineering/
  NumberingServiceTests.cs`'s own comment, which shaped the test-fake strategy in Section 16).
- `WorkOrderLifecycleService.cs` — read in full as the direct template for
  `ProductionOrderService`/`ProductionRunService`'s explicit `Dictionary<string,string[]>`
  legal-transition pattern (confirmed this is the established convention, not
  `IWorkflowRuntime`, for a linear entity-owned status flow).
- `WorkOrderReviewService.cs` / `IWorkflowRuntime.cs` — confirmed `IWorkflowRuntime` is used
  by `WorkOrder` **only** for its separate multi-discipline review sub-process, not for its
  core `Status` flow. This directly informed the Phase 1 decision (Section 9) to keep
  `ProductionOrder`/`ProductionRun`/`ProductionBatch.QualityStatus` as plain domain state
  machines rather than wrapping them in `IWorkflowRuntime` instances.
- `PermissionService.cs` (`Modules` list, `SeedMissingRolesAsync` pattern, the `New(...)`
  helper's boolean parameters) and `SystemRoles.cs` (fixed 9-role set) — confirmed exact
  current state before adding the `"Production"` module.
- `AccessControlSeeder.cs` (`SeedPhase5GovernanceAsync`'s `defaultPolicies` array,
  `SeedFromRolePermissionsAsync`'s automatic `RolePermission → Permission` sync) and
  `DocumentWorkflowController.cs`'s `Approve` action — confirmed the exact SoD-check pattern
  (`IAccessControlService.AuthorizeAsync` + `AccessDecisionReason.DeniedBySoD`) to replicate,
  and confirmed that seeding `SoDPolicy` rows alone is insufficient — the check must be called
  from the controller (this was flagged as risk AO-6 in the architecture spec).
- `Document.cs`, `DocumentAttachment.cs`, `IFileStorageService.cs` — reviewed; **not touched**
  in this phase, since Phase 1 has no document/attachment requirement yet (deferred to a later
  phase per the architecture spec's Phase 6).
- `_SidebarMenu.cshtml` (the exact diff of commit `866c56a` against this same file) — used as
  the literal template for the two new Production nav-link additions (Section 12).

No contradiction between the architecture spec and the current codebase was found. No
supposedly-missing concept turned out to already exist under another name. No stop condition
(Section 47 of the task) was triggered.

---

## 3. Reuse / Extend / Create Matrix (as actually implemented)

| Required Concept | Existing Equivalent | Project | Entity/Table | Decision |
|---|---|---|---|---|
| Product | `MaterialItem` | `PEMS.Domain` | `MaterialItems` | **REUSE** (unchanged) |
| Material | `MaterialItem` | `PEMS.Domain` | `MaterialItems` | **REUSE** |
| UOM | `MaterialUOM` | `PEMS.Domain` | `MaterialUOMs` | **REUSE** |
| Production Order | — | `PEMS.Domain` | `ProductionOrders` | **CREATE** |
| Production Run | — | `PEMS.Domain` | `ProductionRuns` | **CREATE** |
| Batch/Lot | — | `PEMS.Domain` | `ProductionBatches` | **CREATE** |
| Work Center | — (`FunctionalLocation`/`Equipment` referenced, not duplicated) | `PEMS.Domain` | `ProductionWorkCenters` | **CREATE** (thin, FK-only) |
| Production Input | — | `PEMS.Domain` | `ProductionInputs` | **CREATE** |
| Production Output | — | `PEMS.Domain` | `ProductionOutputs` | **CREATE** |
| Production Loss | — | `PEMS.Domain` | `ProductionLosses` | **CREATE** |
| Production Event | — | `PEMS.Domain` | `ProductionEvents` | **CREATE** |
| Shift | `ShiftHandover` (event only, explicitly not a schedule) | `PEMS.Domain` | `ShiftDefinitions` | **CREATE** (minimal, no `ShiftAssignment` scheduling module) |
| Production Schedule | — | — | — | **NOT CREATED** — `PlannedStartDate`/`PlannedFinishDate` on `ProductionOrder` are sufficient for Phase 1; no separate scheduling entity was justified |
| Warehouse | `Warehouse` | `PEMS.Domain` | `Warehouses` | **REUSE** (unchanged) |
| Storage Location | `StorageLocation` | `PEMS.Domain` | `StorageLocations` | **REUSE** (unchanged) |
| Inventory Transaction | `InventoryTransaction` | `PEMS.Domain` | `InventoryTransactions` | **REUSE**, table unchanged; `IMaterialStockService.ReceiveAsync`/`AdjustAsync` **EXTENDED** (2 new optional trailing parameters) |
| Barcode Identity | `BarcodeIdentity` | `PEMS.Domain` | `BarcodeIdentities` | **EXTENDED** (new `TargetType="ProductionBatch"` registration, zero schema change) |
| Equipment | `Equipment` | `PEMS.Domain` | `Equipments` | **REUSE** (unchanged) |
| Functional Location | `FunctionalLocation` | `PEMS.Domain` | `FunctionalLocations` | **REUSE** (unchanged) |
| Workflow | `IWorkflowRuntime` | `PEMS.WorkflowPlatform` | — | **NOT invoked in Phase 1** — deliberate decision, see Section 9 |
| Audit | `AuditLog` + `PEMSDbContext.SaveChangesAsync` interceptor | `PEMS.Domain`/`PEMS.Persistence` | `AuditLogs` | **REUSE** (automatic, zero code) |
| Employee/User | `Employee`, `User` | `PEMS.Domain` | `Employees`, `Users` | **REUSE** (unchanged) |

No duplicate Warehouse, StorageLocation, Inventory ledger, Barcode engine, Workflow engine,
Audit system, Equipment, Functional Location, or Identity/RBAC system was created.

---

## 4. Implemented Domain Model

9 new entities, all in `PEMS.Domain/Entities/` (flat, matching the existing convention — no
new subfolder):

| Entity | Base | Key relationships |
|---|---|---|
| `ProductionWorkCenter` | `BaseEntity` | `FunctionalLocationId` (required FK to existing `FunctionalLocation`), `EquipmentId` (optional FK to existing `Equipment`) |
| `ShiftDefinition` | `BaseEntity` | none (Code/Name/StartTime/EndTime only) |
| `ProductionOrder` | `BaseEntity` | `MaterialItemId`, `UomId`, `ProductionWorkCenterId`, optional `EquipmentId`/`ResponsibleEmployeeId` |
| `ProductionRun` | `BaseEntity` | `ProductionOrderId`, optional `ProductionWorkCenterId`/`EquipmentId`/`OperatorEmployeeId`/`ShiftDefinitionId` |
| `ProductionBatch` | `BaseEntity` | `ProductionOrderId`, `ProductionRunId`, `MaterialItemId`; `Quantity` is a maintained running total (same pattern as `MaterialStock.OnHandQuantity`) |
| `ProductionInput` | plain (append-only, same pattern as `InventoryTransaction`) | `ProductionRunId`, `MaterialItemId`, optional `SourceProductionBatchId` (genealogy hook), optional `InventoryTransactionId` |
| `ProductionOutput` | plain (append-only) | `ProductionRunId`, `MaterialItemId`, required `ProductionBatchId`, optional `InventoryTransactionId` |
| `ProductionLoss` | plain (append-only) | `ProductionRunId`; `LossCategory` validated against a fixed whitelist (`ProcessLoss`/`MaterialLoss`/`Reject`/`Waste`/`Rework`/`Other`), never free text |
| `ProductionEvent` | plain (append-only) | optional `ProductionOrderId`/`ProductionRunId`; lightweight event log, not full event sourcing |

`ProductionBatch` does **not** carry a `BarcodeIdentityId` FK — it is resolved in reverse via
`IBarcodeService.GetActiveByTargetAsync("ProductionBatch", id)`, matching the exact pattern
`Equipment`/`MaterialItem` already use (neither of them carries a barcode FK either).

---

## 5. Production Lifecycle (as implemented)

### ProductionOrder state machine (`ProductionOrderService`, explicit `Dictionary<string,string[]>`)
```
Draft → Planned → Released → InProgress → Completed → Closed
Draft/Planned/Released/OnHold → Cancelled
Released/InProgress → OnHold → (Released or InProgress, via PreHoldStatus)
```
`InProgress` is reached **only** by `ProductionRunService.StartAsync` calling
`IProductionOrderService.AdvanceToInProgressIfNeededAsync` — never by a direct user action —
so an order can never show "in progress" without a real run actually having started.
`CompleteAsync` requires all runs to be terminal (Completed/Cancelled); `CancelAsync` refuses
while any run is still active.

### ProductionRun state machine (`ProductionRunService`)
```
Planned → InProgress ⇄ Paused → Completed
InProgress/Paused → OnHold → InProgress
Planned/Paused/OnHold → Cancelled
```
`CompleteAsync` explicitly refuses a run with zero recorded outputs, returning the actionable
message *"این اجرا بدون ثبت هیچ خروجی تولیدی قابل تکمیل نیست"* (this run cannot be completed
without any recorded production output) rather than a generic error — satisfying the task's
explicit error-message-quality requirement (Section 40 of the task).

### ProductionBatch quality gate (`ProductionRunService`)
```
Produced → AwaitingQC → Released
                       → Blocked → AwaitingQC
                       → Rejected
```
A batch can **never** jump directly from `Produced` to `Released` — verified by test
`BatchQuality_ProducedToReleased_MustPassThroughAwaitingQC`.

---

## 6. Database Changes

Migration name: **`AddProductionCore`**
(`PEMS.Persistence/Migrations/20260915130204_AddProductionCore.cs`)

9 new tables: `ProductionWorkCenters`, `ShiftDefinitions`, `ProductionOrders`,
`ProductionRuns`, `ProductionBatches`, `ProductionEvents`, `ProductionLosses`,
`ProductionInputs`, `ProductionOutputs`. 39 indexes (FK lookup indexes plus unique indexes on
`ProductionOrders.OrderNumber`, `ProductionRuns.RunNumber`, `ProductionBatches.BatchNumber`,
`ProductionWorkCenters.Code`, `ShiftDefinitions.Code` — the exact set requested in the task's
Section 34, none added speculatively beyond that). All quantity columns use `decimal(18,3)`,
matching the existing `MaterialStock`/`InventoryTransaction` precision convention exactly
(confirmed by reading `WarehouseStockConfigurations.cs` before writing the new configuration
classes). All foreign keys use `ReferentialAction.Restrict` (no cascade deletes), matching the
existing `Warehouse`/`StorageLocation` configuration convention.

**No existing table, column, index, or constraint was altered.**

---

## 7. Migration Safety Review

The generated `Up()` method was read in full (694 lines) before being applied. It contains
**exclusively** `CreateTable` and `CreateIndex` calls against the 9 new table names. Zero
`DropTable`, zero `DropColumn`, zero `AlterColumn`, zero `Sql(...)` raw statements, zero
`RenameTable`/`RenameColumn`. The `Down()` method (rollback path, not auto-applied) contains
only `DropTable` calls for the same 9 new tables — correct and harmless, since it would only
ever run against tables this same migration created.

The migration was applied to the configured dev database
(`localhost\SQLEXPRESS`/`PEMSDB`, the same instance used throughout this session) via
`dotnet ef database update`, which completed successfully with no errors. No existing row in
any pre-existing table was read, modified, or deleted by this process. No seed/demo
production transactions were inserted — per the task's explicit instruction (Section 36), the
only new database rows created by this phase are schema (tables/indexes) and the new
`RolePermission`/`Permission`/`RolePermissionGrant`/`SoDPolicy` rows described in Section 10
below, all through the existing idempotent seeder infrastructure, not manual inserts.

---

## 8. Inventory Integration — Architectural Decision (AD-Phase1-01)

Per the task's explicit instruction *"Do not automatically post inventory transactions unless
the existing architecture and business rules support that behavior... If the current system
separates operational recording from inventory posting, preserve that separation"*:

- `ProductionInput`/`ProductionOutput` rows are **always** created as pure production-domain
  facts first (`InventoryTransactionId = null`).
- Posting to inventory is a **separate, explicit, user-triggered action**
  (`PostInputToInventoryAsync`/`PostOutputToInventoryAsync` on `IProductionRunService`, exposed
  as a "Post" button per row in the Run Details view) — never automatic.
- `IMaterialStockService.ReceiveAsync`/`AdjustAsync` were extended **additively** with two new
  optional trailing parameters, `referenceType`/`referenceId` (default `null`, preserving
  `ReferenceType = "Manual"` for every one of the ~8 existing call sites unchanged). A new
  `GetLastTransactionIdAsync(referenceType, referenceId)` method was added rather than changing
  the existing `(bool Success, string? Error)` return tuple shape, because `MaterialItemController.cs`
  and `WorkOrderController.cs` both deconstruct that tuple positionally
  (`var (success, error) = await _materialStockService.ReceiveAsync(...)`) — changing the arity
  would have been a breaking change to two live controllers, so a new query method was added
  instead of widening the return type.
- Each `ProductionInput`/`ProductionOutput` posts to a **precise, 1:1** `ReferenceType`/
  `ReferenceId` pair (`"ProductionInput"`/the input's own ID, `"ProductionOutput"`/the
  output's own ID) — not the coarser `"ProductionRun"` or `"ProductionBatch"` — so that
  double-posting is trivially preventable by checking `InventoryTransactionId == null` on the
  specific row (verified by test `PostOutputToInventory_Twice_IsRejected_NoDuplicatePosting`),
  and the resulting `InventoryTransaction.ReferenceId` is unambiguous even when a run has many
  inputs/outputs.
- Consumption posting validates against **current** on-hand quantity and refuses to drive
  stock negative (`PostInputToInventoryAsync`), rather than allowing an unchecked negative
  balance — verified by test `PostInputToInventory_InsufficientStock_IsRejected_NotNegativeBalance`.

No `ProductionInventory`, `ProductionStock`, `ProductionWarehouse`, or
`ProductionStorageLocation` table was created — `InventoryTransaction`/`MaterialStock`/
`Warehouse`/`StorageLocation` remain the single source of truth for inventory, exactly as
instructed.

---

## 9. Workflow Integration — Architectural Decision

`IWorkflowRuntime` was **not** invoked for `ProductionOrder`/`ProductionRun`/
`ProductionBatch.QualityStatus` in Phase 1. This mirrors a pattern already present in the
existing codebase: `WorkOrder`'s own core `Status` flow (`Open→Assigned→InProgress→
Completed→Closed`) is managed by a plain `WorkOrderLifecycleService` with an explicit
transition dictionary — `IWorkflowRuntime` is used by `WorkOrder` only for its separate,
genuinely multi-actor "discipline review" sub-process (`WorkOrderReviewService`), not for its
linear lifecycle. `ProductionOrder`/`ProductionRun`/`ProductionBatch` are, in Phase 1, single-
actor-per-transition linear state machines (an operator records output, a QC-permissioned
user releases/rejects) — there is no multi-role parallel-approval requirement yet that would
justify the overhead of a `WorkflowDefinition`/`WorkflowStage`/`WorkflowTransition` seed set.

This is a **deliberate, documented** choice, not an oversight: if a future phase needs a real
multi-stage QC approval chain (e.g., two independent QC sign-offs before release), the
extension point is exactly the one `WorkOrderReviewService` demonstrates — inject
`IWorkflowRuntime` into a new thin adapter service, no change to the `ProductionBatch` entity
shape required. This was flagged explicitly in the architecture spec (Section T/AD-3) and is
being revisited here with the same conclusion after direct re-inspection of `WorkOrder`'s own
code, not merely repeated from the earlier audit.

No `ProductionWorkflowEngine`, `ProductionApprovalEngine`, or duplicate state-machine
infrastructure was created, per the task's explicit prohibition.

---

## 10. RBAC / SoD Integration

**Permission module** (`PermissionService.cs`): one new module string, `"Production"`, added
to the existing `Modules` array (no new modules list/table). Seeded via the existing
`SeedMissingRolesAsync("Production", ...)` idempotent pattern, reusing the **same fixed
7-boolean vocabulary** every other module uses (`CanView/CanCreate/CanEdit/CanDelete/
CanApprove/CanHold/CanReleaseHold`) — no new permission columns, no `Production.*` fine-grained
action list invented:

| Role | View | Create | Edit | Delete | Approve | Hold | ReleaseHold |
|---|---|---|---|---|---|---|---|
| MaintenanceEngineer | ✓ | ✓ | ✓ | | | ✓ | ✓ |
| QAManager | ✓ | | | | ✓ | | |
| EngineeringManager | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Viewer | ✓ | | | | | | |

Action-to-permission mapping used by the controllers: View = dashboard/list/details; Create =
create order/run, record input/output/loss, post to inventory, submit batch for QC; Edit =
Plan (confirm an order's schedule); Delete = Cancel (order or run — same "stop an active
record" semantics `Barcode.Delete` already established for Disable/Revoke); Approve =
Release/Complete/Close order, Release/Reject batch (the highest-risk gates, same convention as
every other module's `Approve` mapping); Hold/ReleaseHold = order/run hold-resume and batch
Block/Unblock. **No new `SystemRoles` entry was created** — `MaintenanceEngineer` was judged
the closest existing operational role and `QAManager` the closest existing quality-gate role;
this is flagged as an open question in the architecture spec (dedicated `Production Operator`/
`QC Inspector` roles) and left for a future phase, consistent with the task's instruction to
audit the existing convention before creating new roles rather than inventing them by default.

**SoD** (`AccessControlSeeder.cs`): two new policy rows added to the existing
`SeedPhase5GovernanceAsync` array, mirroring the exact `WorkOrder.Create ↔ WorkOrder.Close`
precedent:
- `Production.Create ↔ Production.Approve`
- `Production.Edit ↔ Production.Approve`

**Critically, per risk AO-6 from the architecture spec** ("a documented history of SoD being
seeded-but-unwired for at least one prior module"), the SoD check was **verified to be wired
into the controllers**, not just seeded: `ProductionOrderController.Release`/`Complete`/
`Close` and `ProductionRunController.ReleaseBatch`/`RejectBatch` each call
`IAccessControlService.AuthorizeAsync` and check `AccessDecisionReason.DeniedBySoD` before
proceeding — copied line-for-line from `DocumentWorkflowController.Approve`'s existing,
already-proven pattern. This closes the exact gap the architecture spec flagged as a
condition of the Architecture Gate.

Because `AccessControlSeeder.SeedFromRolePermissionsAsync` automatically derives the generic
`Permission`/`RolePermissionGrant` rows from whatever is in `RolePermissions` at seed time, the
new `"Production"` module's grants flow into the newer RBAC-ABAC layer with **zero additional
code** — confirmed by reading that method, not assumed.

---

## 11. AuditLog Integration

No code was written for audit logging. Every new entity (`ProductionOrder`, `ProductionRun`,
`ProductionBatch`, `ProductionWorkCenter`, `ShiftDefinition`) is a normal `DbSet` on
`PEMSDbContext`, so `PrepareAuditEntries`/`FinalizeAuditEntries` in `PEMSDbContext.
SaveChangesAsync` automatically captures every Insert/Update against them — who, what, when,
entity, entity ID, and a changed-fields diff — exactly as designed for every other entity in
the system. `ProductionInput`/`ProductionOutput`/`ProductionLoss`/`ProductionEvent` are
append-only by construction (no update/delete method exists anywhere in
`ProductionRunService`), matching the same insert-only guarantee `InventoryTransaction`/
`WorkflowHistory` already provide.

---

## 12. UI Changes

3 new controllers (`ProductionController` — dashboard only; `ProductionOrderController`;
`ProductionRunController`), 7 new views under `PEMS.Web/Views/Production/`,
`PEMS.Web/Views/ProductionOrder/`, `PEMS.Web/Views/ProductionRun/`. All views reuse the
existing shared shell (`_Layout.cshtml`/`_SidebarMenu.cshtml`, untouched in shape),
`content-card`/`table table-enterprise`/`_KpiCard`/`_Pagination` partials, Bootstrap modal
patterns, and the existing global `TempData["Message"]`/`TempData["Error"]` toast mechanism —
no new CSS, no new JS framework, no parallel design system.

`_SidebarMenu.cshtml` change: 2 new `<a class="nav-link">` entries added inside the **existing**
`CMMS Integration` collapsible section (the same section the recent `Barcode` module was added
to), plus `"Production"`/`"ProductionOrder"`/`"ProductionRun"` added to that section's
controller-highlighting array — no new sidebar section was created.

---

## 13. Inventory Integration Summary

Covered in full in Section 8.

## 14. Equipment/Functional Location Integration

`ProductionWorkCenter.FunctionalLocationId` (required) and `.EquipmentId` (optional) are real
FKs to the existing `FunctionalLocations`/`Equipments` tables — no parallel location or
equipment concept was created. `ProductionOrder`/`ProductionRun` reach a Functional Location
exclusively through their `ProductionWorkCenter` (or an optional direct `EquipmentId`
override on the Run), verified by test
`ProductionRun_ReachesFunctionalLocation_ThroughExistingWorkCenterAndEquipment_NoDuplicateEntities`,
which asserts exactly one `Equipment` row and exactly one `FunctionalLocation` row exist after
exercising the full chain — proof no duplicate was silently created.

## 15. BarcodeIdentity Integration

Covered in full architecturally in the spec's Section AB; implemented exactly as designed:
`"ProductionBatch"` added to `BarcodeService.SupportedTargetTypes` plus the three private
switch methods, and `BarcodeController.RedirectToDetails` now routes a resolved
`ProductionBatch` scan to `ProductionRunController.BatchDetails` — reusing the existing
`/Barcode/ResolveJson` scan-to-prefill endpoint and the existing `IBarcodeRenderingService`
(SVG/PNG/PDF) unchanged. Zero schema change (confirmed both by the architecture spec's earlier
analysis and now by the fact that this migration contains no `BarcodeIdentities` table
alteration at all). Verified by 2 new tests in `BarcodeServiceTests.cs`
(`GenerateAsync_ProductionBatchTarget_Succeeds_NoSchemaChangeNeeded`,
`GenerateAsync_ProductionBatchTarget_OrphanTargetIsRejected`).

---

## 16. Test Results

**25 new tests**, all passing in isolation and as part of the full suite:

- `PEMS.Tests/Cmms/ProductionOrderServiceTests.cs` — 9 tests: valid/invalid creation, illegal
  transitions rejected with actionable messages, full Draft→Closed lifecycle (via a real
  started/completed run, not a shortcut), Hold→Resume returning to the *exact* prior status
  (not always `InProgress`), Cancel requiring a reason, Cancel rejected from a terminal state.
- `PEMS.Tests/Cmms/ProductionRunServiceTests.cs` — 14 tests: Run start advancing the parent
  Order to `InProgress`, Complete refused without any output (with the actionable Persian
  message the task's Section 40 asked for), batch creation vs. accumulation (quantity summed,
  not overwritten), input/output/loss rejected outside `InProgress`, loss category whitelist
  enforcement, reconciliation both in the comparable (same-UOM) and explicitly-not-comparable
  (mixed-UOM) cases — proving the difference is never silently forced to balance, the full
  quality gate (`Produced→AwaitingQC→Released`, direct-to-Released rejected,
  `Blocked→AwaitingQC` requiring a reason), inventory posting reusing the real
  `InventoryTransaction` table with the correct `ReferenceType`/`ReferenceId`, double-posting
  rejection, insufficient-stock rejection, and the Equipment/FunctionalLocation
  no-duplication proof (Section 14).
- `PEMS.Tests/Cmms/BarcodeServiceTests.cs` — 2 new tests (Section 15).

**Two test failures found and fixed during this pass** (both were test-design errors, not
product-code bugs — documented for transparency): the initial
`FullLifecycle_DraftToClosed_Succeeds` and `Cancel_FromClosedOrCompleted_IsRejected` tests
assumed an Order could `Complete` directly from `Released` with zero Runs; running them against
the actual `LegalTransitions` dictionary revealed this is correctly disallowed (an Order can
only reach `Completed` after genuinely entering `InProgress`, which requires a real started
Run) — the tests were corrected to exercise the real Order→Run→Output→Complete chain rather
than the product code being changed to accommodate an unrealistic shortcut.

### Full regression

```
dotnet build PEMS.slnx --configuration Debug   → Build succeeded, 0 errors, 22 pre-existing warnings (none in new files)
dotnet test PEMS.Tests/PEMS.Tests.csproj        → Passed! Failed: 0, Passed: 1354, Skipped: 0, Total: 1354, Duration: 7m 17s
```

1354 = 1329 pre-existing (confirmed passing in the prior session) + 25 new. **Zero regressions,
zero excluded tests, zero flaky failures on this run.**

---

## 17. Standards Alignment (Phase 1 subset)

- **ISA-95**: `ProductionOrder`→`ProductionRun`→`ProductionOutput` maps to the Production
  Schedule/Production Performance layer; `ProductionWorkCenter` maps to the Equipment
  Hierarchy's Work Center level, FK'd to the existing `FunctionalLocation` tree rather than
  duplicating it.
- **ISA-88**: applied loosely only, as the spec recommended — `ProductionBatch` carries Batch/
  Lot identity and a quality gate, but no Recipe/Procedure/Phase execution engine was built;
  PEMS remains a records/traceability layer, not a batch controller.
- **ISO 22400**: the data prerequisite for the **Availability** KPI is now closer (Batch/Run
  timestamps exist), but Performance/OEE remain blocked on the still-missing Recipe/
  theoretical-rate master, exactly as flagged in the spec (AO-3) — not addressed in Phase 1,
  per its explicit deferral.
- **GS1**: `ProductionBatch`'s identity is carried by the existing `BarcodeIdentity`
  (internal token, not GTIN/SSCC) — consistent with architectural decision AD-6 (GS1 deferred
  as an optional future carrier layer, not retrofitted).

---

## 18. Known Limitations

1. `Production Operator`/`QC Inspector`/`Warehouse Operator` roles were **not** created —
   Phase 1 reuses `MaintenanceEngineer`/`QAManager`/`EngineeringManager`. This is an explicit,
   documented simplification (Section 10), not an oversight.
2. No UOM conversion — the reconciliation view reports "not comparable" rather than a
   misleading number when Input/Output/Loss use different UOMs, since UOM conversion is
   confirmed unimplemented anywhere in PEMS (pre-existing gap, out of Phase 1 scope).
3. `IWorkflowRuntime` is not used for the quality gate in this phase (Section 9) — a future
   phase needing multi-role QC approval would extend this, not redesign `ProductionBatch`.
4. No `ProductionSchedule`/`ShiftAssignment` scheduling module — `ShiftDefinition` exists but
   is not yet assigned to Runs by anything other than a manual per-Run selection.
5. Razor views were confirmed to compile at build time (Roslyn-mapped warnings against other
   large `.cshtml` files in this same build prove views are compiled, not runtime-only) but
   were **not** exercised in a live browser session against the running app in this pass — no
   login credentials were available (consistent with this session's standing limitation,
   documented honestly rather than claiming a browser verification that did not happen).
6. `ProductionRunController.Create`'s `equipmentId` field is a raw numeric input (not a
   dropdown) — a minor, deliberately deferred UI polish item, not a functional gap.

## 19. Deferred Phase 2+ Items (per the architecture spec's own phase plan)

Packaging/HandlingUnit/Pallet, `TraceabilityEvent` (forward/backward genealogy), Sales/
Dispatch/Customer, Recipe/theoretical-rate master (needed for OEE), mobile/offline sync API,
DCS/PLC/ERP integration, dedicated Production/QC/Warehouse roles, GS1/EPCIS compliance layer.

## 20. Files Changed

**New (24 files):**
```
PEMS.Domain/Entities/ProductionWorkCenter.cs
PEMS.Domain/Entities/ShiftDefinition.cs
PEMS.Domain/Entities/ProductionOrder.cs
PEMS.Domain/Entities/ProductionRun.cs
PEMS.Domain/Entities/ProductionBatch.cs
PEMS.Domain/Entities/ProductionInput.cs
PEMS.Domain/Entities/ProductionOutput.cs
PEMS.Domain/Entities/ProductionLoss.cs
PEMS.Domain/Entities/ProductionEvent.cs
PEMS.Application/DTOs/ProductionDtos.cs
PEMS.Application/Interfaces/IProductionOrderService.cs
PEMS.Application/Interfaces/IProductionRunService.cs
PEMS.Persistence/Configurations/ProductionConfigurations.cs
PEMS.Persistence/Servises/ProductionOrderService.cs
PEMS.Persistence/Servises/ProductionRunService.cs
PEMS.Persistence/Migrations/20260915130204_AddProductionCore.cs (+ .Designer.cs)
PEMS.Web/Controllers/ProductionController.cs
PEMS.Web/Controllers/ProductionOrderController.cs
PEMS.Web/Controllers/ProductionRunController.cs
PEMS.Web/Views/Production/Dashboard.cshtml
PEMS.Web/Views/ProductionOrder/{Index,Details,Create}.cshtml
PEMS.Web/Views/ProductionRun/{Create,Details,BatchDetails}.cshtml
PEMS.Tests/Cmms/ProductionOrderServiceTests.cs
PEMS.Tests/Cmms/ProductionRunServiceTests.cs
docs/audits/PEMS_OPERATIONS_PRODUCTION_ARCHITECTURE_SPEC_v1.md
docs/audits/PEMS_OPERATIONS_PRODUCTION_PHASE1_IMPLEMENTATION_REPORT.md (this file)
```

**Modified (11 files, all additive):**
```
PEMS.Application/Interfaces/IMaterialStockService.cs   (2 new optional params + 1 new method)
PEMS.Persistence/Servises/MaterialStockService.cs      (implements the above)
PEMS.Persistence/Context/PEMSDbContext.cs               (9 new DbSets)
PEMS.Persistence/Migrations/PEMSDbContextModelSnapshot.cs (EF-generated, reflects new tables)
PEMS.Persistence/Servises/NumberingService.cs           (3 new Defaults entries)
PEMS.Persistence/Servises/PermissionService.cs          (1 new module + seeding block)
PEMS.Persistence/Seed/AccessControlSeeder.cs             (2 new SoD policy rows)
PEMS.Persistence/Servises/BarcodeService.cs              (1 new TargetType, 4 switch arms)
PEMS.Web/Controllers/BarcodeController.cs                (1 new redirect arm)
PEMS.Web/Program.cs                                       (2 new DI registrations)
PEMS.Web/Views/Shared/_SidebarMenu.cshtml                 (2 new nav links, existing section)
PEMS.Tests/Cmms/BarcodeServiceTests.cs                    (2 new tests)
```

**Note**: `git status` also shows `PEMS.Web/Views/Shared/_Layout.cshtml`,
`PEMS.Web/Views/Shared/_PemsStageDiagram.cshtml`,
`PEMS.Web/Views/EngineeringDocuments/Details.cshtml`, and `PEMS.Web/wwwroot/css/site.css` as
modified — these are **pre-existing, uncommitted changes from an earlier session task**
(the Engineering Documents whitespace correction), not part of Phase 1, and were not touched
again in this pass.

## 21. Migration Name

`AddProductionCore` (`20260915130204_AddProductionCore`)

## 22. Final Implementation Gate

# IMPLEMENTATION COMPLETE

All Phase 1 critical requirements are implemented and verified:
- Domain model (Order/Run/Batch/Input/Output/Loss/Event/WorkCenter/Shift) ✓
- Explicit, tested state machines with rejected-transition proof ✓
- Equipment/Functional Location integration via existing entities only, no duplicates (proven by test) ✓
- Inventory integration via existing `InventoryTransaction`, additive-only, non-automatic, no double-posting (proven by tests) ✓
- BarcodeIdentity extension, zero schema change (proven by tests) ✓
- RBAC reused (existing 7-action vocabulary, no new roles), SoD reused and **verified wired into controllers**, not just seeded ✓
- AuditLog automatic, zero new code ✓
- Additive-only migration, inspected and confirmed zero destructive operations, applied successfully ✓
- Existing data, tables, and migrations untouched ✓
- Build succeeds, 0 errors ✓
- Full regression: 1354/1354 passing, zero regressions ✓

No commit or push was made as part of this task.
