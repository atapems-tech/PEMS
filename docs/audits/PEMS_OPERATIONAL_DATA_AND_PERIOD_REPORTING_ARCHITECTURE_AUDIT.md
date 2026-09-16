# PEMS Operational Data, Product Management, Packaging & Period-Reporting Architecture — Audit

**Status: AUDIT ONLY. No entity, service, controller, view, migration, permission, test, or
database object was created or modified to produce this document.**

This audit builds on, and does not repeat, the two prior audits
(`PEMS_OPERATIONS_PRODUCTION_ARCHITECTURE_SPEC_v1.md`,
`PEMS_OPERATIONS_ARCHITECTURE_AND_PERIOD_REPORTING_AUDIT.md`) and the now-completed
`PEMS_OPERATIONS_CMS_BOUNDARY_CORRECTION_REPORT.md`. Where those documents already answered a
question with evidence, this document cites and extends them rather than re-deriving from
scratch; where this task asks a genuinely new question (Packaging shape, Gross/Net
production, batch-aware inventory query design, product aliases), fresh repository evidence
was gathered.

---

## 1. Executive Summary

The Operations/CMMS UI boundary is now correctly independent (confirmed, prior task). Beneath
that fixed layer, Phase 1's Production Core is a **solid, extensible foundation** — but five
genuinely unresolved **foundational data-model decisions** stand between it and the next real
features (Packaging, Daily Operations, period reporting):

1. **Gross vs. Net production is not defined anywhere.** `ProductionOutput.Quantity` is a
   single, undifferentiated number. Every downstream KPI (Accepted/Rejected/Rework/Variance)
   depends on this being resolved first, and it is a business definition, not an engineering
   one.
2. **Batch-level inventory visibility does not exist**, but the fix is smaller than it looks:
   PEMS already uses a ledger-plus-projection pattern (`InventoryTransaction` → `MaterialStock`)
   — the correct fix is to make the **existing** ledger batch-aware (one new nullable FK
   column), not to invent a parallel lot-inventory subsystem. This is new, concrete evidence
   this audit adds beyond the prior two documents (Section 10).
3. **No period-type/calendar semantics exist anywhere in real PEMS code** — confirmed again,
   with the same finding as the prior audit, now cross-checked against `Kpi`/`CmmsOperations`/
   `ReportController` dashboards, none of which bucket by Day/Week/Month either.
4. **Packaging should reuse the Order/Run/Input/Output/Loss/Event *pattern* Phase 1
   established, not the `ProductionOutput` *table*.** Packaging has structurally different
   dimensions (package size, package count, pallet count) that do not fit `ProductionOutput`'s
   shape — it needs its own entity family, built the same way, not bolted onto the existing one.
5. **The `"Production"` permission module and `ProductionBatch`'s state machine remain
   unchanged and correct** — nothing in this audit requires touching them (confirmed by
   re-reading, not assumed).

**Final classification (Section 16): ARCHITECTURE READY WITH REQUIRED FOUNDATIONAL CHANGES.**

---

## 2. Current Architecture (re-verified against the live tree)

| Area | Current state | Evidence |
|---|---|---|
| Operations navigation | Independent `Operations & Production` sidebar accordion, `MenuAccess.Operations` | `_SidebarMenu.cshtml`, `MenuAccess.cs` (prior task, confirmed still in place) |
| CMMS navigation | Unchanged, no Production links remain inside it | `_SidebarMenu.cshtml` `cmmsControllers` array |
| Production controllers | `ProductionController` (Dashboard only), `ProductionOrderController`, `ProductionRunController` — all `ViewData["Section"] = "Operations & Production"` | `PEMS.Web/Controllers/Production*.cs` |
| Production services | `ProductionOrderService`, `ProductionRunService` — explicit `Dictionary<string,string[]>` transition tables, no `IWorkflowRuntime` use (deliberate, Section 9 of the Phase 1 report) | `PEMS.Persistence/Servises/Production*.cs` |
| Production entities | `ProductionOrder`, `ProductionRun`, `ProductionBatch`, `ProductionInput`, `ProductionOutput`, `ProductionLoss`, `ProductionEvent`, `ProductionWorkCenter`, `ShiftDefinition` — unchanged since Phase 1 | `PEMS.Domain/Entities/Production*.cs`, `ShiftDefinition.cs` |
| `MaterialItem` | Unchanged: `MaterialCode`, `MaterialType`, `Description`, `LongDescription`, `CategoryId`, `DisciplineId`, `UomId`, `Manufacturer`, `ManufacturerPartNumber`, `VendorId`, `Specification`, `Model`, `Size`, `MaterialGrade`, `IsSerialized`/`IsBatchControlled` (unimplemented flags), `IsStockItem`, `IsCritical`, `IsActive` | `PEMS.Domain/Entities/MaterialItem.cs` |
| `MaterialUOM` | Unchanged: `Code`, `Name`, `ConversionFactor` (present, unused) | `PEMS.Domain/Entities/MaterialUOM.cs` |
| `ProductionBatch` | Unchanged: `Quantity` (maintained running total), `QualityStatus` (`Produced→AwaitingQC→Released/Blocked/Rejected`) | `PEMS.Domain/Entities/ProductionBatch.cs` |
| `Warehouse`/`StorageLocation`/`MaterialStock`/`InventoryTransaction` | Unchanged. `MaterialStock` keyed only by `(MaterialItemId, WarehouseId, StorageLocationId)` — **no batch/quality field**, confirmed again by re-reading | `PEMS.Domain/Entities/{Warehouse,StorageLocation,MaterialStock,InventoryTransaction}.cs` |
| Quality-related entities | **None exist beyond `ProductionBatch.QualityStatus`.** `Inspection`/`IInspectionService` was checked directly in this pass and confirmed to be an **Equipment-condition-monitoring** concept (`GetForEquipmentAsync(equipmentId)`, `measurementValue`/`lowerLimit`/`upperLimit`, `nextInspectionDate`) — a CMMS asset-inspection tool, not a product/batch quality-test entity. No `Lot`/`Serial` entity class exists anywhere in `PEMS.Domain`. | `PEMS.Application/Interfaces/IInspectionService.cs`; repo-wide grep for `class Lot\b`/`class Serial\b` — zero matches |
| `BarcodeIdentity` | Unchanged, `"ProductionBatch"` already a supported `TargetType` (Phase 1) | `BarcodeIdentity.cs`, `BarcodeService.cs` |
| `AuditLog` | Unchanged, automatic `SaveChangesAsync` interceptor, covers all Production entities already | `PEMSDbContext.cs` |
| Reporting infrastructure | `ReportController` + per-module `IXxxReportService` (`ICorrespondenceReportService`, `IEngineeringReportService`, `IVDRReportService`, `ICmmsReportService`, `IProcurementReportService`) — every one takes raw `fromDate`/`toDate` only, **no `PeriodType`, no Day/Week/Month bucketing anywhere** | `PEMS.Web/Controllers/ReportController.cs` |
| Dashboard infrastructure | `KpiController` (CMMS reliability KPIs), `CmmsOperationsController` (`Dashboard` action), `ProductionController.Dashboard` (Phase 1) — all render a live snapshot on request; none pre-aggregate or cache | confirmed via controller inspection |
| Date/calendar utilities | **None.** No `PeriodType`, no Jalali/Persian-calendar conversion, no week-start-day config anywhere in real application code (only in unrelated design-mockup scaffold files under `src/stitch-temp/`, not app code) | repo-wide grep, consistent with the prior audit's finding, re-confirmed |
| Export/PDF/Excel infrastructure | `ClosedXML` + shared `ExcelExportHelper` (`AddStyledHeaderRow`/`FinalizeExportSheet`); `QuestPDF` (already used for Barcode labels via `IBarcodeRenderingService.RenderLabelSheetPdf`, and by `LetterController`) | confirmed in the original architecture spec, still current |

---

## 3. Product Master Architecture

### 3.1 Verdict: REUSE `MaterialItem`, EXTEND via a future 1:1 profile — unchanged from the prior audit, with one addition

Re-checked field-by-field against this task's expanded list:

| Requirement | Present? |
|---|---|
| Product code | ✅ `MaterialCode` |
| Product name | ✅ `Description`/`LongDescription` |
| Product family | ⚠️ `MaterialType` is a broad free-string category, not a normalized Family concept |
| Product grade | ✅ `MaterialGrade` |
| Product type | ⚠️ same as Family |
| Product specification | ✅ `Specification` |
| Base UOM | ✅ `UomId` |
| Packaging type | ❌ Missing |
| Product status/lifecycle | ❌ Missing (only the generic `IsActive` bool) |
| Product version | ❌ Missing |
| Product classification | ⚠️ `CategoryId` exists (`MaterialCategory`) but is procurement/spare-parts-oriented, not necessarily finished-goods-oriented |
| Bulk vs. packaged product | ❌ Missing |
| Product-to-material relationship | N/A — `MaterialItem` **is** the material |
| Product lifecycle (Draft/Active/Discontinued) | ❌ Missing |
| **Product aliases / alternate codes** (new in this task) | ❌ **Missing** — no alias table, no alternate-code field anywhere on `MaterialItem`. If a customer- or vendor-facing product code ever needs to differ from the internal `MaterialCode`, there is currently no mechanism. |

**Recommendation (unchanged, re-confirmed): do not create a parallel `Product` entity.**
`MaterialItem` is the confirmed, real (~50k-row) Product/Material master and has already once
suffered a costly parallel-entity mistake (`SparePart` vs. `MaterialItem`, documented in
`MaintenanceSpareUsage.cs`'s own comment) — repeating that pattern for "Product" would be the
exact same mistake with a different name.

**Extension mechanism**: a future, optional, 1:1 `ProductionProductProfile` entity keyed on
`MaterialItemId`, created only for `MaterialItem` rows that are actually finished/produced
goods (not for the ~50k spare-parts/consumables rows) — mirroring the exact precedent already
in the codebase: `Instrument` is explicitly documented as "a specialization of the same
Equipment row, not a separate identity," 1:1 on `Instrument.EquipmentId`. Fields:
`ProductFamily`, `PackagingType`, `ProductStatus`, `ProductVersion`. **Aliases/alternate
codes** are a separate concern from the profile (an alias is 1-to-many per product, not 1:1) —
if genuinely needed, a small `MaterialItemAlias` table (`MaterialItemId`, `AliasCode`,
`AliasType`) is the minimal shape, but this audit does **not** confirm the business need exists
yet — flagged as `OPEN ARCHITECTURAL QUESTION`, not assumed.

Neither entity is created in this pass.

---

## 4. Production Data Source-of-Truth Analysis

| Fact | Authoritative source | Notes |
|---|---|---|
| Production by product/batch/line/work center | `ProductionOutput` | ✅ fully supported today |
| Production by shift | `ProductionRun.ShiftDefinitionId` | ⚠️ Run-level only — a run spanning a shift change cannot split output by shift (unchanged finding from the prior audit) |
| Production by grade | `MaterialItem.MaterialGrade`, joined at report time | ⚠️ not snapshotted on `ProductionOutput` — a later grade change would silently rewrite history in a report (unchanged finding) |
| **Gross production** | ❌ **Not defined** | `ProductionOutput.Quantity` is a single number; whether it represents Gross or Net is currently an unstated assumption, not a documented rule |
| **Net production** | ❌ **Not defined** | same as above — this is the most consequential open question in this section |
| Accepted production | Derivable: `SUM(ProductionOutput.Quantity)` where `ProductionBatch.QualityStatus = "Released"` | ⚠️ derivable via join, no dedicated aggregate/report path built yet |
| Rejected production | Derivable: same join, `QualityStatus = "Rejected"` | same caveat |
| Rework quantity | `ProductionLoss` where `LossCategory = "Rework"` | ⚠️ **Real process gap, not just a reporting one**: a `"Rework"` loss is recorded as a pure loss today — there is no mechanism for reworked material to re-enter a run as a later `ProductionOutput`, so "Rework quantity recovered" cannot be distinguished from "Rework quantity permanently lost." Carried forward from the first audit's Section 7/16, still unresolved. |
| Process/Material loss | `ProductionLoss.LossCategory ∈ {ProcessLoss, MaterialLoss}` | ✅ supported |
| Production target (order-level total) | `ProductionOrder.PlannedQuantity` | ✅ supported at the order level |
| Production target (period-scoped: daily/shift) | ❌ Missing | No entity ties a target to a specific date/shift/line — `PlannedQuantity` is a single total for the whole order's life |
| Actual production | `SUM(ProductionOutput.Quantity)` | ✅ |
| Variance vs. target | ❌ Not computed | Blocked on period-scoped targets existing first |

### 4.1 How to avoid duplicating `ProductionOutput`

For every gap above, the recommended fix is **either a derived query rule (no new table) or a
narrow, single-purpose new entity that references `ProductionOutput`/`ProductionRun`, never
re-stores its quantity**:

- **Accepted/Rejected/Pending** — a query rule (`GROUP BY ProductionBatch.QualityStatus`), not
  a new table. Zero duplication risk because the number is always computed from
  `ProductionOutput` at read time.
- **Gross/Net** — once defined by the business, the cleanest fix is likely a second, optional
  column on the *existing* `ProductionOutput` row (e.g., `GrossQuantity` alongside the
  existing `Quantity`, reinterpreted as Net) rather than a parallel table — this keeps the
  1-input-1-output-row cardinality intact. **Not designed further here** — the business
  definition must come first (Section 13, risk #1).
- **Period-scoped target** — a new, narrow `ProductionTarget` entity (`Date`,
  `ProductionWorkCenterId` or `ProductionOrderId`, `MaterialItemId`, `TargetQuantity`) that
  never stores an *actual* quantity — Variance is always `Actual (from ProductionOutput) −
  Target (from ProductionTarget)`, computed at report time, not stored redundantly.
- **Rework re-entry** — if the business wants reworked material to count as new output, the
  correct fix is a normal `ProductionOutput` row for the reworked run (with a reference back to
  the originating `ProductionLoss`, e.g., an optional `SourceProductionLossId` FK), not a
  separate "RecoveredQuantity" ledger — this reuses the existing fact table rather than
  creating a second one.

None of these are implemented in this pass.

---

## 5. Packaging Architecture Options

### 5.1 Answering the task's core question directly

**Packaging should be modeled as its own Order/Run/Input/Output/Loss/Event entity family,
built with the exact same pattern Phase 1 already established for Production — not as a
`ProductionOutput` subtype, and not as a single new "packaging transaction" bolted onto an
existing table.** Reasoning:

- A `ProductionOutput` row is shaped for "this run produced N units of material M into batch
  B" — it has no concept of package count, package size, or pallet count, and forcing those
  into it would either require nullable Packaging-only columns on a Production table (schema
  pollution) or an EAV-style side table (exactly what this task warns against as a default).
- Packaging's own lifecycle (`Bulk Batch → Packaging Input → Packaging Operation → Packaged
  Product → Package/HandlingUnit → Pallet → Warehouse Receipt`) is structurally identical in
  *shape* to Production's (`Order → Run → Input → Output → Loss → Event`) — same state-machine
  style, same append-only fact tables, same optional-inventory-posting pattern, same
  `BarcodeIdentity` extension technique. Reusing the **pattern** (not the **table**) is the
  correct application of "REUSE > EXTEND > CREATE" here — the thing being reused is Phase 1's
  proven architecture, not literally its rows.

### 5.2 Recommended entity family (not created in this pass)

```
PackagingOrder      — mirrors ProductionOrder (what should be packaged)
PackagingRun        — mirrors ProductionRun (what actually happened)
PackagingInput       — consumes a ProductionBatch (bulk) — mirrors ProductionInput,
                        FK to ProductionBatch (not a new "source batch" concept)
PackagingOutput      — creates/increments a Package — mirrors ProductionOutput
Package              — the packaged unit identity (own BarcodeIdentity TargetType,
                        exactly like ProductionBatch got one in Phase 1)
HandlingUnit/Pallet   — aggregates Package rows (many-to-many, since a pallet can be
                        repacked) — own BarcodeIdentity TargetType too
PackagingLoss         — mirrors ProductionLoss (packaging-specific loss categories:
                        reject, damaged package, repackage)
PackagingEvent        — mirrors ProductionEvent
```

### 5.3 Requested field coverage

| Requirement | Design location |
|---|---|
| Packaging date/shift/line | `PackagingRun` (own `ShiftDefinitionId`/`ProductionWorkCenterId` FKs — **reuse** `ShiftDefinition`/`ProductionWorkCenter`, no new master data) |
| Source product/batch | `PackagingInput.ProductionBatchId` (FK to the existing `ProductionBatch`, not a duplicate) |
| Packaged product | `PackagingOutput.MaterialItemId` (may differ from the bulk `MaterialItem` if packaging changes the product identity, e.g., "Product X, 25kg bag" vs. "Product X, bulk") |
| Input/output quantity | `PackagingInput.Quantity`/`PackagingOutput.Quantity` — same append-only, typed pattern as Production |
| Package count/size | `Package.Count`/`Package.SizeUom`+`SizeQuantity` |
| Pallet count | derived: `COUNT(HandlingUnit)` per `PackagingRun`, or a direct `PackagingOutput.PalletCount` if a coarser record is sufficient |
| Packaging loss/rejected packages | `PackagingLoss` |
| Rework/repackaging | Same open question as Section 4's Production rework — needs the same "reference back to the source loss" pattern, not a duplicate concept invented separately for Packaging |
| Packaging status / QC status | `Package.QualityStatus` — **reuse the exact same string values** (`Produced/AwaitingQC/Released/Blocked/Rejected`) `ProductionBatch.QualityStatus` already uses, for consistency, not a new vocabulary |
| Warehouse destination | Reuse `IMaterialStockService.ReceiveAsync`, exactly as `ProductionRunService.PostOutputToInventoryAsync` already does — same opt-in, explicit-posting, `ReferenceType`/`ReferenceId`-tagged pattern (Phase 1's AD-Phase1-01 decision applies identically here) |

**Not implemented in this pass**, per the task's explicit instruction. Listed in the roadmap
(Section 14).

---

## 6. Operational Parameter Model

Building on the recommendation already made in the first audit's Section 10, this pass
classifies the task's expanded parameter list explicitly:

| Category | Classification | Reasoning |
|---|---|---|
| Production quantity, Packaging quantity, Package count, Pallet count | **Dedicated entity** (already exist or designed in Section 5) | Critical, traceability-bearing, financially/operationally consequential facts — must stay typed and indexed |
| Runtime, Downtime | **Dedicated entity, not yet built** — a `ProductionDowntimeEvent` (start/end timestamp, cause, links to `ProductionRun`/`Equipment`/`WorkRequest`/`WorkOrder`) was already recommended in the *original* architecture spec (Phase 1's own scope) and remains unbuilt. Confirmed still missing — only `WorkOrder.DowntimeHours`/`FailureRecord.DowntimeHours` (single decimal snapshots) exist. This directly blocks the ISO 22400 Availability KPI. | Feeds Availability/OEE calculations and SoD/audit-relevant maintenance-impact analysis — too consequential and too structured (start/end/cause) for a generic metric row |
| Yield, Scrap, Rework (as KPIs) | **Calculated**, never entered | Derived from `ProductionOutput`/`ProductionLoss`, per the original spec's KPI dictionary |
| Energy, Steam, Cooling water consumption, Feed rate | **Good `OperationalMetricEntry` candidates** | Plant/process-specific, likely to vary by site, not core to financial or batch traceability — exactly the "genuinely extensible" case the configurable model is for |
| Product temperature, pressure, moisture, density (process readings) | **Good `OperationalMetricEntry` candidates**, *if* they are process-monitoring readings, not release-decision inputs | See next row for the distinction |
| **Quality measurements that drive a release/reject decision** | **Dedicated entity**, not `OperationalMetricEntry` | If a lab result determines whether `ProductionBatch.QualityStatus` moves to `Released` vs. `Rejected`, it is a regulatory/traceability-critical fact requiring its own auditable record (a future `QualityTestResult` entity linked to `ProductionBatch`, with its own `EnteredBy`/`ApprovedBy`), not a generic metric row that could be silently redefined or deleted by an admin editing `OperationalMetricDefinition` |

### 6.1 Integrity constraints a future `OperationalMetricEntry` design must satisfy

(Recommendation only, not designed in full — carried forward and sharpened from the first
audit):

- `DataType`-driven single-value-column enforcement (`NumericValue`/`TextValue`/
  `BooleanValue`, exactly one populated per `DataType`).
- UOM via a real FK to the existing `MaterialUOM` table, never a free string — the exact
  inconsistency `MaterialUOM`'s own doc comment says it was built to prevent.
- A uniqueness constraint on `(MetricDefinitionId, EntryDate, + dimension columns)` to prevent
  duplicate entries for the same measurement.
- `ApplicableProductType`/`ApplicableProductionUnit` as real FKs (to `MaterialItem`/
  `ProductionWorkCenter`) where applicable, not free text, so dimension filtering in reports is
  reliable.
- Source traceability: `EnteredBy`/`EnteredAt` at minimum; `ApprovedBy`/`ApprovedAt` if the
  metric feeds anything beyond an informational dashboard.
- Correction history: **no update-in-place** — a correction should be a new entry referencing
  the original (same append-only precedent as `ProductionInput`/`Output`/`Loss`), never an
  edited row, for the same audit-integrity reason Phase 1 already applied everywhere else.

---

## 7. Daily Operations Model

Confirms and sharpens the first audit's Section 6/11 recommendation, now evaluating the
task's three explicit options directly:

**Option A (raw quantities stored independently) — REJECTED.** Creates a second source of
truth for numbers `ProductionOutput`/`ProductionLoss`/(future) `PackagingOutput` already own —
directly causes the double-counting risk this task explicitly warns against (Section 13).

**Option B (pure reference collection to source transactions) — CLOSE, BUT INCOMPLETE.**
Correctly avoids duplication, but has no submission/approval semantics — if the business needs
a supervisor to formally sign off that "yesterday's numbers are final," Option B alone doesn't
model that state.

**Option C (validated submission containing references + a lifecycle) — RECOMMENDED**, same
conclusion as the first audit, now explicitly confirmed against the three-option framing this
task provides:

```text
DailyOperationsSubmission (or ShiftProductionSubmission, scoped to a shift instead of a day
if that matches the real business cadence better — an open question for whoever builds this)
  - Scope: Date (+ optional ShiftDefinitionId, ProductionWorkCenterId)
  - Status: Draft → Submitted → UnderReview → Approved → ReturnedForCorrection / Rejected → Locked
  - SubmittedByEmployeeId / ApprovedByEmployeeId / timestamps
  - NO quantity columns of its own — every number it displays is computed, at read time,
    from ProductionOutput/ProductionLoss/(future PackagingOutput) within its Date/Shift/
    WorkCenter scope
  - "Locking" a submission does not freeze the underlying facts (which stay append-only/
    immutable already) — it only prevents new facts from being silently attributed to an
    already-closed period without an explicit correction path (Section 11)
```

Not implemented in this pass — Phase 3+ per the roadmap (Section 14).

---

## 8. Reporting Architecture

Unchanged conclusion from the first audit, re-confirmed against `KpiController`/
`CmmsOperationsController` (neither buckets by period either — both render a live snapshot for
"now"):

1. Add `IOperationsReportService` to the existing `ReportController` family (same
   constructor-injection, same action-per-module pattern already used for 5 other modules).
2. Add one shared `PEMS.Application/Common/ReportingPeriod.cs` (`PeriodType` enum +
   `Resolve(from, to, type)` helper), next to the existing `DataGridPageSize`/`PagedResult<T>`
   — a single place all of PEMS's date logic can eventually converge on, not an
   Operations-only utility.
3. Direct EF aggregate queries (`GroupBy`/`Sum`), matching every existing report service — no
   SQL views, no materialized summaries, no scheduled snapshots, until real slow-query
   evidence justifies them.

### 8.1 Dimension table for the reports this task asks about

| Report | Source entity | Status filter | Date field | UOM | Product | Grade | Line | Shift | Batch | Quality | Warehouse |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Total production | `ProductionOutput` | — | `TransactionDate` | `UomId` | ✅ | via `MaterialItem` | ✅ (`ProductionRun.WorkCenterId`) | ⚠️ Run-level only | ✅ | via `ProductionBatch` join | N/A |
| Accepted/Rejected production | `ProductionOutput` ⋈ `ProductionBatch` | `QualityStatus` | `TransactionDate` | ✅ | ✅ | via join | ✅ | ⚠️ | ✅ | ✅ | N/A |
| Rework | `ProductionLoss` (`LossCategory="Rework"`) | — | `TransactionDate` | ✅ | N/A (Run-level, not per-material) | N/A | ✅ | ⚠️ | N/A | N/A | N/A |
| Packaging quantity/Package/Pallet count | *(future `PackagingOutput`/`Package`/`HandlingUnit`, Section 5)* | | | | | | | | | | |
| Warehouse receipts | `InventoryTransaction` (`TransactionType="Receipt"`) | — | `TransactionDate` | ✅ | ✅ | N/A | N/A | N/A | ⚠️ only via `ReferenceType="ProductionOutput"` indirection (Section 10) | ⚠️ not carried | ✅ |
| QC pending/Released/Held/Rejected product | `ProductionBatch.QualityStatus` | ✅ (is the status) | `QualityStatusChangedDate` | ✅ | ✅ | N/A | N/A | N/A | ✅ (is the batch) | ✅ | ❌ (batch quality status is not yet joinable to warehouse stock — Section 10) |
| Production target vs. actual / variance | *(future `ProductionTarget`, Section 4)* | | | | | | | | | | |
| Production-to-packaging reconciliation | `ProductionOutput` vs. future `PackagingInput` | — | both | must match | ✅ | | | | ✅ (via `PackagingInput.ProductionBatchId`) | | |
| Packaging-to-warehouse reconciliation | future `PackagingOutput` vs. `InventoryTransaction` | — | both | must match | ✅ | | | | | | ✅ |

Not implemented in this pass.

---

## 9. Calendar and Shift Semantics

Re-confirmed, unchanged from the first audit (no new evidence contradicts it):

1. Naive server-local `DateTime` storage everywhere (`DateTime.Now`), no UTC layer — consistent
   with 100% of the rest of the codebase.
2. No timezone-conversion layer anywhere.
3. No Jalali/Persian-calendar conversion in real application code.
4. No week-start-day configuration anywhere.
5. `ShiftDefinition.StartTime`/`EndTime` (`TimeSpan`) can represent a midnight-crossing shift,
   but nothing decides which calendar date a midnight-crossing shift's output belongs to for
   reporting purposes — `ProductionOutput.TransactionDate` is simply "when the entry was
   made."
6. No "operational day start time" concept (implicitly midnight).

**These must be decided by the business owner before Daily/Weekly/Monthly reports (Section 8)
can be deterministic**, exactly as the first audit concluded — restated here because this task
explicitly asks for it again, not because new evidence changed the answer.

---

## 10. Batch, Quality, and Inventory Traceability Gap (deep dive — new evidence this pass)

### 10.1 Direct answers to the task's specific questions

| Question | Answer today | Why |
|---|---|---|
| Quantity by production batch | ❌ Not directly queryable | `MaterialStock` has no batch column; only reachable via `InventoryTransaction.ReferenceType="ProductionOutput" → ProductionOutput.ProductionBatchId`, a 2-hop join, and only for stock that came from Production (not MRS-imported or manually-received stock) |
| Quantity by lot | Same as batch — PEMS does not distinguish "Lot" from "Batch" conceptually; `ProductionBatch` **is** the lot |
| Quantity by quality status | ❌ Not directly queryable | Same 2-hop join required; `QualityStatus` lives only on `ProductionBatch`, never denormalized to the stock row |
| Quantity awaiting QC / released / on hold / rejected | ❌ Same gap | Same reasoning |
| Quantity by warehouse/storage location | ✅ Fully supported | `MaterialStock`'s existing key |
| Quantity by package or pallet | ❌ Does not exist | Packaging doesn't exist yet (Section 5) |
| Quantity consumed **from a specific batch** (production-side) | ✅ Supported | `ProductionInput.SourceProductionBatchId` (Phase 1) |
| Quantity consumed/issued **from a specific batch** (warehouse-side, e.g. FIFO-issuing a specific released batch to a Work Order) | ❌ Not supported | `IMaterialStockService` has no batch-aware issue method; issues/adjustments today only know `(MaterialItemId, WarehouseId, StorageLocationId)` |
| Quantity transferred between locations while preserving batch traceability | ❌ Not supported | `TransferAsync` posts a `TransferOut`/`TransferIn` pair with no batch reference |

### 10.2 Comparing the three design options the task asks about

1. **Batch/quality-aware stock rows** (extend `MaterialStock` itself to be keyed per-batch,
   e.g. add `ProductionBatchId` to its key). **Rejected as the primary fix** — `MaterialStock`
   is an already-live, heavily-used table (every existing Receive/Adjust/Return/Transfer flow
   depends on its current `(MaterialItemId, WarehouseId, StorageLocationId)` uniqueness);
   changing its grain is the most invasive of the three options and would ripple into every
   existing `IMaterialStockService` caller (`MaterialItemController`, `WorkOrderController`,
   `InventoryController`).
2. **Lot-level inventory as a wholly separate subsystem**. **Rejected** — this is exactly the
   "second inventory architecture" the task's own rules prohibit, and PEMS already has a
   working ledger+projection pattern; building a parallel one would fragment, not fix, the gap.
3. **Transaction-ledger source of truth with a stock projection** — **this is already PEMS's
   existing architecture** (`InventoryTransaction` = ledger, `MaterialStock` = projected
   balance, exactly the pattern `InventoryTransaction.cs`'s own doc comment describes).
   **Recommended fix: extend the existing ledger to be batch-aware, not replace the
   architecture.**

### 10.3 Concrete, minimal, additive recommendation (not implemented in this pass)

Add one new **nullable** column, `InventoryTransaction.ProductionBatchId` (FK to
`ProductionBatch`, `ON DELETE RESTRICT`, matching every other FK convention in this table) —
purely additive, no existing column touched, no existing row's meaning changes (it stays
`NULL` for every transaction not tied to a batch — MRS receipts, manual adjustments, etc.).
Populate it going forward from `ProductionRunService.PostOutputToInventoryAsync`/
`PostInputToInventoryAsync` (which already know the batch — no new lookup needed, just one
more field set at insert time). Then:

- **"Batch-level on-hand quantity"** becomes a simple read-model query:
  `SUM(InventoryTransaction.Quantity) GROUP BY MaterialItemId, WarehouseId,
  ProductionBatchId WHERE ProductionBatchId IS NOT NULL` — no change to `MaterialStock`'s
  existing shape or any of its existing callers.
- **"Quantity by quality status in the warehouse"** = the above, joined to
  `ProductionBatch.QualityStatus` — `QualityStatus` is never duplicated into the inventory
  side; it stays owned by `ProductionBatch`, read-joined at query time, preserving single
  source of truth.
- **Batch-aware issue/transfer** would still require new, additive method(s) on
  `IMaterialStockService` (e.g. an optional `productionBatchId` parameter threaded through the
  same way `referenceType`/`referenceId` were added in Phase 1) — a small, precedented
  extension, not a redesign.

This is a genuine, actionable, low-risk recommendation — new analysis this audit contributes
beyond the prior two documents. **Not implemented here.**

---

## 11. Approval, Correction, and Auditability

- `ProductionOrder`/`ProductionBatch` already have SoD-gated Release/Complete/Close/Release-QC/
  Reject (Phase 1) — confirmed unchanged, not re-litigated.
- No correction path exists for a completed Run's historical facts (carried forward from the
  first audit) — recommendation unchanged: a future `ProductionAdjustment` entity (append-only,
  reason-required, referencing the original row), mirroring `InventoryTransaction`'s own
  `"Adjustment"` `TransactionType` precedent.
- The `Draft→Submitted→UnderReview→Approved→ReturnedForCorrection→Rejected→Locked` lifecycle
  this task asks about belongs on the future `DailyOperationsSubmission`/
  `ShiftProductionSubmission` container (Section 7), not on the individual fact rows
  themselves — those stay simple and append-only, exactly as Phase 1 built them.
- **Do not reuse CMMS approval semantics** (e.g. `WorkOrderReviewService`'s
  `IWorkflowRuntime`-based multi-discipline review) **by default** — Operations' permission
  module (`"Production"`) is already independent (confirmed, prior task), and Phase 1's own
  reasoning for staying on plain domain state machines (Section 9 of the Phase 1 report)
  applies equally to a future submission container unless a genuine multi-role parallel
  approval need is identified.

---

## 12. Reuse / Extend / Create Matrix

| Requirement | Existing Support | Reuse | Extend | New Entity Needed | Priority | Risk |
|---|---|---|---|---|---|---|
| Product Master | `MaterialItem` | ✅ | `ProductionProductProfile` (1:1) | — | Medium | Low |
| Product aliases | — | — | — | `MaterialItemAlias` *(open question — need confirmed)* | Low | Low |
| Production (core facts) | `ProductionOutput`/`Input`/`Loss` | ✅ | Gross/Net field (pending business definition) | — | High | Low (additive) |
| Production Target/Variance | `ProductionOrder.PlannedQuantity` (order-level only) | ✅ partial | — | `ProductionTarget` (period-scoped) | Medium | Low |
| Downtime | `WorkOrder.DowntimeHours` (snapshot only) | — | — | `ProductionDowntimeEvent` | High (blocks Availability KPI) | Low |
| Packaging | — | Pattern only (Section 5) | — | `PackagingOrder`/`Run`/`Input`/`Output`/`Loss`/`Event`/`Package`/`HandlingUnit` | Medium (after foundational items) | Medium |
| Daily Operations | `ProductionOutput`/`Loss` (as source facts) | ✅ | — | `DailyOperationsSubmission` (reference-only container) | Medium | Medium (double-count risk if built as Option A) |
| Operational Metrics | — | — | — | `OperationalMetricDefinition`/`Entry` (non-critical params only) | Low | Medium (integrity risk if misused for critical facts) |
| Quality Status (batch) | `ProductionBatch.QualityStatus` | ✅ unchanged | — | — | — | — |
| Quality Test Results (lab data) | — | — | — | `QualityTestResult` (if release decisions need a documented basis) | Medium | Low |
| Warehouse Receipt | `InventoryTransaction`/`MaterialStock` | ✅ unchanged | — | — | — | — |
| Lot/Batch Inventory visibility | `InventoryTransaction` (ledger) | ✅ | `InventoryTransaction.ProductionBatchId` (Section 10) | — | High (unblocks Quality/Warehouse reporting) | Low (additive, nullable) |
| Reporting Periods | `ReportController` family | ✅ pattern | `ReportingPeriod` helper + `IOperationsReportService` | — | High (blocks all period reports) | Low |
| Calendar (Jalali/week-start) | — | — | — | Business decision first, then a small shared service | High (blocks deterministic reports) | Medium (wrong default = silently wrong reports) |
| Shift | `ShiftDefinition` | ✅ | Per-output shift attribution (currently Run-level only) | — | Low | Low |
| KPI | Original spec's KPI dictionary | ✅ conceptually | Needs Gross/Net + Downtime + Target first | — | Medium | — |
| Export | `ExcelExportHelper`/`ClosedXML`/`QuestPDF` | ✅ fully reusable | — | — | Low (ready when reports exist) | Low |

---

## 13. Risks and Double-Counting Scenarios

1. **Daily Operations built as Option A** (Section 7) — the single biggest double-counting
   risk this audit identifies; explicitly rejected.
2. **Gross/Net ambiguity** — if left undefined, different reports could sum
   `ProductionOutput.Quantity` under different implicit assumptions (one report treating it as
   Gross, another as Net), producing numbers that don't reconcile against each other without
   anyone noticing why. This is the audit's highest-priority open question (Section 4).
3. **Rework double-counting** — if reworked material is recorded as both a `ProductionLoss`
   (at the time of rework) and, later, as a fresh `ProductionOutput` (once reprocessed) without
   an explicit link between the two, a naive "Total Production" report would count that
   quantity twice (once implicitly forgiven as a loss reduction, once as new output). The
   `SourceProductionLossId` reference recommended in Section 4.1 exists specifically to make
   this traceable and preventable.
4. **Packaging Input/Output without disciplined `ReferenceType`/`ReferenceId` tagging** — if a
   future Packaging implementation doesn't follow Phase 1's exact `PostInputToInventoryAsync`/
   `PostOutputToInventoryAsync` precedent (1:1 `ReferenceType`/`ReferenceId` per fact row,
   `InventoryTransactionId == null` double-post guard), the same batch's stock could be posted
   to inventory twice — once from Production, once from Packaging.
5. **A future batch-aware inventory extension (Section 10) must extend
   `InventoryTransaction`, not fork it** — building a second ledger "for batch-tracked items
   only" would immediately reintroduce the exact "two parallel inventory systems" risk this
   task's own rules prohibit.
6. **Reports built before Section 9's calendar questions are answered** risk silently
   disagreeing with the business's actual operational calendar (already flagged in the first
   audit, restated here because it remains the single most consequential unresolved input to
   Section 8).

---

## 14. Migration-Safe Implementation Roadmap

Ordered per the task's own requested sequence, with current status noted:

1. **Product Master clarification** — resolve `ProductionProductProfile` need + the
   alias/alternate-code open question with the business owner (Section 3). *Not started.*
2. **Operations permission boundary** — ✅ **already done** (prior task; `MenuAccess.Operations`,
   independent sidebar section, corrected breadcrumbs).
3. **Production transaction hardening** — resolve Gross/Net definition (Section 4), add
   `ProductionTarget` and `ProductionDowntimeEvent`. *Not started — highest priority remaining
   item, since Packaging/Reporting/KPIs all depend on it.*
4. **Batch and quality inventory design** — add `InventoryTransaction.ProductionBatchId`
   (Section 10). *Not started — second-highest priority, unblocks Quality/Warehouse reporting
   with a small, low-risk change.*
5. **Packaging model** — build the `PackagingOrder/Run/Input/Output/Loss/Event/Package/
   HandlingUnit` family (Section 5), only after items 3-4 are settled (Packaging's own
   reconciliation reports directly depend on Gross/Net and batch-aware inventory being
   resolved first).
6. **Daily Operations submission** (Section 7) — after Packaging exists, since its scope should
   cover Packaging facts too, not just Production.
7. **Reporting query architecture** — `ReportingPeriod` helper + `IOperationsReportService`
   (Section 8) — can begin in parallel with item 3, since the helper itself has no dependency
   on Packaging/Daily Operations, only on the calendar decision (item 8).
8. **Calendar/shift service** — the Jalali/week-start/operational-day-boundary business
   decisions (Section 9) — **should be resolved before, or in parallel with, item 7**, not
   after; building the `ReportingPeriod` helper with wrong defaults baked in would need to be
   revisited once resolved.
9. **Dashboard and exports** — trivial once items 7-8 exist; `ExcelExportHelper`/`QuestPDF`
   are already fully reusable, no new export infrastructure needed.
10. **Traceability and QR integration** — extending `BarcodeIdentity` to `Package`/
    `HandlingUnit` (Section 5.2) — mechanically identical to how `ProductionBatch` was
    extended in Phase 1, so this is low-effort once Packaging entities exist.

No phase in this roadmap is implemented in this task.

---

## 15. Exact Files and Symbols Requiring Future Changes

```
PEMS.Domain/Entities/ProductionProductProfile.cs (new)       — Section 3
PEMS.Domain/Entities/MaterialItemAlias.cs (new, pending confirmation) — Section 3
PEMS.Domain/Entities/ProductionOutput.cs                     — future GrossQuantity column — Section 4
PEMS.Domain/Entities/ProductionTarget.cs (new)                — Section 4
PEMS.Domain/Entities/ProductionDowntimeEvent.cs (new)          — Section 6
PEMS.Domain/Entities/Packaging*.cs, Package.cs, HandlingUnit.cs (new, family) — Section 5
PEMS.Persistence/Servises/BarcodeService.cs                   — future "Package"/"HandlingUnit" TargetType registration — Section 5.2
PEMS.Domain/Entities/OperationalMetricDefinition.cs / Entry.cs (new, non-critical params only) — Section 6
PEMS.Domain/Entities/QualityTestResult.cs (new, if needed)     — Section 6
PEMS.Domain/Entities/DailyOperationsSubmission.cs (new)         — Section 7
PEMS.Application/Common/ReportingPeriod.cs (new)                — Section 8
PEMS.Application/Interfaces/IOperationsReportService.cs (new)    — Section 8
PEMS.Web/Controllers/ReportController.cs                       — new Operations action — Section 8
PEMS.Domain/Entities/InventoryTransaction.cs                    — new nullable ProductionBatchId column — Section 10
PEMS.Application/Interfaces/IMaterialStockService.cs             — future batch-aware issue/transfer overload — Section 10
PEMS.Domain/Entities/ProductionAdjustment.cs (new)                — Section 11
```

---

## 16. Final Architectural Decision

# ARCHITECTURE READY WITH REQUIRED FOUNDATIONAL CHANGES

Phase 1's Production Core remains a correct, solid, and appropriately extensible foundation —
nothing in this audit found a flaw requiring rework of what already exists (its entities,
state machines, permission module, and inventory-posting decision all hold up under this
deeper questioning). But five foundational data-model decisions — Gross/Net production
definition, period-scoped production targets, a Downtime entity, a batch-aware
`InventoryTransaction` extension, and the business's calendar/week/operational-day semantics —
must be resolved **before** Packaging, Daily Operations, or period-based reporting can be
safely built, because each of those next features directly depends on at least one of the
five. None of the five require touching Phase 1's existing tables destructively; all five are
additive. This is why the classification is "ready with required foundational changes," not
"not ready" (Phase 1's core is sound) and not "ready for implementation" (the five decisions
above are real, unresolved, and consequential enough that building on top of them without
resolving them first risks exactly the double-counting and inconsistent-reporting outcomes
Section 13 describes).

No code was changed in this task. No commit or push was made.
