# PEMS CMMS — Source Data Readiness & Migration Mapping

**Status:** Readiness and mapping specification only. No code was changed. No real legacy CMMS
source dataset was found in this repository or project context — this document is a
schema-and-precedent-based specification for when one becomes available, not a validated migration.

## 1. Executive Summary

No real legacy CMMS export exists anywhere in this repository, project files, or the current
working context (re-verified for this workstream — the situation has not changed since the
`MaintenanceHistory` Import workstream). No named legacy system (SAP PM, Maximo, eMaint, Infor,
Oracle, or otherwise) is referenced anywhere in the codebase or its documentation.

This document is therefore a **readiness specification**, built entirely from: the actual PEMS
domain/persistence model (verified by direct file inspection, not assumption), the actual behavior
of the five existing import pipelines (verified by reading their resolution logic, not just their
names), and the identity/key facts those pipelines already established as precedent. Every mapping
below is either a confirmed repository fact or is explicitly marked as an assumption pending a real
source sample.

Headline finding: of the ~17 CMMS-relevant entities inventoried, only **3 dimensions** already have
a working import path (Equipment, FunctionalLocation, Equipment↔FunctionalLocation) plus the one
built last workstream (MaintenanceHistory). The next-safest candidate for a future implementation
slice is **FailureRecord** (structurally near-identical to the already-built MaintenanceHistory
importer). Several entities — most importantly **WorkOrder**, **WorkRequest**, and anything
requiring **Employee** identity — are migration-**blocked or high-risk** today because they either
carry workflow state that a migration cannot safely fabricate, or depend on an Employee identity
field (`PersonnelCode`) that is optional and not guaranteed populated.

## 2. Source Availability

```text
Real Source Dataset: NOT AVAILABLE
```

Re-verified for this workstream by:
- `git status` — no new data-like files since the `MaintenanceHistory` Import workstream.
- Filesystem search for `.xlsx/.xls/.csv/.mdb/.accdb/.bak/.dacpac` across the repository (excluding
  `bin/obj/node_modules/graphify-out`): only pre-existing files belonging to *other* PEMS features
  (`PEMS.Web/filestore/intake/*.xlsx` — EDMS document intake; `PEMS.Web/filestore/mrs-import/*.xlsx`
  — Material Requisition Slip import). Neither is a legacy CMMS export.
- `PEMS.Web/DATA/` contains only leftover ASP.NET scaffold files (`ApplicationDbContext.cs`,
  `SeedData.cs`), not source data.
- No `legacy`, `migration`, `source-data`, or `sample` data directory exists anywhere in the repo.
- The session's scratchpad directory is empty.
- No attachment was provided in this conversation.

## 3. Legacy System Identification

```text
Legacy System: UNKNOWN
```

No commercial or custom CMMS product name appears anywhere in `.claude/CLAUDE.md`, `docs/`, source
comments, or `PEMS_PROJECT_INTELLIGENCE_REPORT.md`. Because the legacy system is unidentified, this
document cannot assume any product-specific export shape (e.g., SAP PM's `IH01`/`IE02`-style
hierarchy or Maximo's `WORKORDER`/`ASSETMETER` table shapes). All mappings below are based solely on
PEMS's own target schema plus the generic, format-agnostic shape every existing PEMS importer
already uses (Excel with an auto-detected header row).

## 4. PEMS Target Entity Inventory

All facts below were verified by direct inspection of the actual entity classes in
`PEMS.Domain/Entities/` (not assumed). "Existing import path" reflects the actual state of
`PEMS.Web/Controllers/*Import*.cs` and `PEMS.Domain/Entities/*Import*.cs`.

| # | Entity (actual class) | PK | Business key | Required fields (non-nullable, no default) | Key FKs | Existing import | Safe to import independently? |
|---|---|---|---|---|---|---|---|
| 1 | `FunctionalLocation` | `FunctionalLocationId` | `Code` | `Code`, `Name` | `ParentId` (self, optional) | **Yes** — `FunctionalLocationImportController`/`Service` | Yes |
| 2 | Plant / Area / Unit | *(no standalone entity)* | — | — | flat `string?` fields **on** `FunctionalLocation` (`Plant`, `Area`, `Unit`) | rides along with FunctionalLocation import | N/A — not separate entities |
| 3 | `Equipment` | `EquipmentId` | `TagNumber` + `Description` (composite — see §6) | `TagNumber`, `Name`, `EquipmentType`, `ProjectId`, `DisciplineId` | `ProjectId`, `DisciplineId`, `FunctionalLocationId` (optional), `EquipmentClassId` (optional) | **Yes** — `EquipmentImportController`/`Service` | Yes, after FunctionalLocation/Project/Discipline exist |
| 4 | `EquipmentClass` | `EquipmentClassId` | `Code` | `Code`, `Name` | self (`ParentEquipmentClassId`, optional) | No | Yes — small reference table, admin-managed today |
| 5 | `EquipmentCriticality` | `EquipmentCriticalityId` | none (Equipment-scoped) | `EquipmentId`, `CriticalityClass` | `EquipmentId` (required) | No dedicated import; has its own **assessment workflow** (`CriticalityAssessmentController`) | Review required — importing raw legacy classifications would bypass the existing assessment/approval flow |
| 6 | `MaintenanceGroup` | `MaintenanceGroupId` | `Code` | `Code`, `Name`, `DisciplineId` | `DisciplineId` | No | Yes — small reference table |
| 7 | `MaintenanceType` | `MaintenanceTypeId` | `Code` | `Code`, `Name` | none | No | Yes — small reference table; note `WorkOrder.WorkOrderType` is still a free string, deliberately not wired to this table yet (per its own code comment) |
| 8 | `MaintenanceStrategy` | `MaintenanceStrategyId` | none (Equipment+FailureModeCode is the de facto dedup key — see §8) | `EquipmentId`, `StrategyType` | `EquipmentId` (required), `MaintenanceStrategyTypeId` (optional) | No dedicated import (the closed "Bulk Define" workstream is a **UI selection flow within PEMS**, not a file-based import) | Review required — adjacent to the closed workstream; any import would need its own separate, explicitly-scoped design |
| 9 | `MaintenancePlan` | `MaintenancePlanId` | `PlanNumber` (uniqueness not DB-enforced — unverified) | `EquipmentId`, `MaintenanceStrategyId`, `PlanNumber`, `Title` | `EquipmentId`, `MaintenanceStrategyId` (both required, non-nullable), `JobPlanId` (optional) | No | Blocked until MaintenanceStrategy import exists (hard FK dependency) |
| 10 | `MaintenanceHistory` | `MaintenanceHistoryId` | none — composite content key established last workstream | `EquipmentId`, `ExecutionDate` | `EquipmentId` (required), `WorkOrderId` (optional, soft) | **Yes** — `MaintenanceHistoryImportController`/`Service` (closed, approved) | Yes — done |
| 11 | `WorkRequest` | `WorkRequestId` | `RequestNumber` | `RequestNumber`, `Title`, `RequestedById` | `RequestedById` (**required** Employee FK), `EquipmentId`/`AssetId`/`FunctionalLocationId` (each optional) | No | High risk — carries a `Draft→Submitted→UnderReview→Approved→{Rejected\|Planned→Converted}` workflow; a migration cannot safely fabricate which state a historical request should land in without a business decision, and `RequestedById` requires resolvable Employee identity |
| 12 | `WorkOrder` | `WorkOrderId` | `WorkOrderNumber` | `EquipmentId`, `WorkOrderNumber`, `WorkOrderType` | `EquipmentId` (required), `MaintenancePlanId`/`SourceWorkRequestId`/`AssignedTeamId`/`AssignedEmployeeId` (all optional) | No | High risk — ISO 14224 fields (`FailureModeCode`/`FailureCauseCode`) are required by `.claude/CLAUDE.md` convention for Corrective/Emergency records; `Status` workflow state would need a business decision for historical rows (e.g., "always Closed") |
| 13 | `WorkOrderLabor` | `WorkOrderLaborId` | none | `WorkOrderId`, `EmployeeId` | `WorkOrderId`, `EmployeeId` (**both required**), `TeamId` (optional) | No | Blocked on Employee identity (see §9) and on WorkOrder import existing first |
| 14 | `FailureRecord` | `FailureRecordId` | none | `EquipmentId`, `FailureDate`, `FailureMode` | `EquipmentId` (required), `WorkOrderId` (optional, soft) | No | **Recommended next slice** — structurally near-identical to `MaintenanceHistory` (flat, append-only, Equipment required + WorkOrder optional soft reference); the exact same composite-key idempotency pattern applies directly |
| 15 | `SparePart` | `SparePartId` | `PartNumber` | `PartNumber`, `PartName` | none to Equipment directly (linked via `MaintenanceSpareUsage` historically) | No | **Do not target this entity** — see §11, it is a deprecated path superseded by `MaterialItem` |
| 16 | `MaterialItem` | *(Inventory module)* | — | — | — | **Yes** — `MrsImportController`/`Service` (Excel Stage→Review→Commit, already exists) | This is the actual current "spare parts / materials" migration target, not `SparePart` |
| 17 | `Vendor` | `VendorId` | `VendorCode` (optional) or `Name` | `Name` | none required | No | Out of CMMS scope — this `Vendor` entity's real associations (`Contracts`, `VDRItems`, `EngineeringDocuments`) are EDMS/VDR-domain, not CMMS `WorkOrder`; `WorkOrder.ContractorName` is a plain free-text string with **no FK to `Vendor` at all** |
| 18 | `Employee` (Technician/Personnel references) | `EmployeeId` | `PersonnelCode` (nullable, DB-unique only when non-null) | `FirstName`, `LastName`, `DepartmentId`, `DisciplineId` | `DepartmentId`, `DisciplineId` | No | See §9 — identity is only safe when `PersonnelCode` is populated |
| 19 | Cost records | *(no standalone entity)* | — | — | distributed across `WorkOrder.ContractorCost`, `MaintenanceSpareUsage.TotalCost`, `WorkOrderLabor.LaborCost`, `MaintenanceHistory.Cost` | rides along with each owning entity's import | N/A — not a separate entity to migrate |
| 20 | Attachments | `MaintenanceDocument`, `EngineeringDocument`, etc. | — | — | — | No dedicated CMMS-history-attachment import | Out of scope for this phase (task explicitly excludes EDMS/document migration) |

## 5. Existing Import Capability Inventory

All five existing Stage → Review → Commit pipelines share one shape: ClosedXML Excel parsing with
auto-detected header row (best-scoring row within the first ~6 rows), a `Batch` (file/date/status)
+ `Row` (raw JSON payload + resolution status) pair, deterministic (non-fuzzy) matching only, no
per-row `SaveChanges`, and server-side permission gates on Upload (Create)/Review (View)/Commit
(Approve)/Discard (Delete). What differs is **the actual identity/idempotency rule each one uses** —
verified by reading their resolution code, not assumed to be identical:

| Pipeline | Input | Identity key (verified) | Create vs. update | Conflict behavior |
|---|---|---|---|---|
| `EquipmentImportService` | Excel | **`TagNumber` + `Description`, combined** (`BuildEquipmentKey`) | Supports both — `IsUpdate` flag set when the combined key already exists | A file row whose Tag+Description differs from an existing row (even same Tag) is treated as a *different* logical key, not an update to that Tag |
| `FunctionalLocationImportService` | Excel | `Code` only | Supports both — skips (no-op) if `Name` is unchanged, proposes an update if `Name` differs | Strict Code match; no fuzzy code handling |
| `EquipmentFunctionalLocationAssignmentService` | Excel | `Equipment.TagNumber` + `FunctionalLocation.Code`, both exact | Assignment-only — never creates Equipment or FunctionalLocation | `AlreadyAssigned` (different existing value) requires an explicit separate "Replace" confirmation; never silently overwritten |
| `MrsImportService` | Excel | Not re-verified in this pass (out of CMMS-core scope — Inventory module) | — | — |
| `MaintenanceHistoryImportService` | Excel | **Composite, established because none existed**: `EquipmentId + ExecutionDate.Date + NormalizedActionTaken + NormalizedTechnician` | Create-only, append-only — never updates | `Duplicate` excluded by default; reviewer may explicitly force-include (see its own documentation) |

**Important nuance for future mapping work:** `EquipmentImport`'s identity key is *not* `TagNumber`
alone — it is `TagNumber + Description` combined. A source file with the same Tag but a changed
Description will be staged as a *new* candidate row, not recognized as the same equipment. This is
existing, intentional PEMS behavior (not something this document proposes changing), but it means
"authoritative Equipment identity = TagNumber" (as stated in the previous workstream's `docs/audits/
PEMS_EQUIPMENT_FUNCTIONAL_LOCATION_ASSIGNMENT_IMPLEMENTATION.md`) is only exactly true for the
*assignment* pipeline, not for the *import* pipeline. Both facts are recorded here because they are
both true, evidence-based, and not contradictory once the distinction is made explicit.

## 6. Source Data Profile

**Not possible.** No real source file exists to profile (see §2). No row counts, column
inventories, duplicate-key counts, or value-distribution statistics can be produced honestly. Any
such numbers would be fabricated and are explicitly forbidden by this task's instructions.

## 7. Source-to-PEMS Mapping Tables

Because no real source schema is available, only two mapping tables can be produced honestly: one
for the entity that already has a *proven* mapping (MaintenanceHistory, reused verbatim from its own
closed-workstream documentation) and one *template* for the recommended next entity (FailureRecord),
marked explicitly as an unverified proposal pending a real source sample.

### 7a. MaintenanceHistory (implemented, proven — reference only, not re-implemented here)

```text
Source Entity: (unknown legacy source — generic "maintenance history export")
Source Identifier: none assumed
Source Business Key: none — composite (Equipment Tag + Execution Date + Action + Technician)
Target PEMS Entity: MaintenanceHistory
Target Identifier: MaintenanceHistoryId
Field Mapping: see docs/audits/PEMS_CMMS_DATA_MIGRATION_MAINTENANCE_HISTORY_IMPORT.md §4
Transformation: date parsing, decimal parsing (null on failure, never coerced to 0)
Required/Optional: Equipment Tag + Execution Date required; everything else optional
Reference Resolution: Equipment.TagNumber exact; WorkOrder.WorkOrderNumber exact, soft/optional
Duplicate Rule: composite content key, excluded by default, reviewer override allowed
Conflict Rule: UnresolvedEquipment/ConflictEquipment/ValidationError never committable
Unsupported Values: unparseable numeric fields dropped to null with no row-level error
Migration Priority: DONE
```

### 7b. FailureRecord (proposed — NOT implemented, NOT verified against a real source)

```text
Source Entity: ASSUMED "failure event export" — no real source confirmed
Source Identifier: ASSUMED none stable
Source Business Key: PROPOSED composite, mirroring MaintenanceHistory:
    (Equipment Tag, Failure Date, Failure Mode)
Target PEMS Entity: FailureRecord
Target Identifier: FailureRecordId
Field Mapping (proposed, by direct structural analogy to MaintenanceHistory's proven mapping):
    Equipment Tag        → FailureRecord.EquipmentId (resolved via Equipment.TagNumber, exact)
    Failure Date          → FailureRecord.FailureDate (required)
    Failure Mode           → FailureRecord.FailureMode (required, free text — no FailureMode
                              lookup-table resolution attempted; FailureModeId stays null,
                              matching the entity's own "AI-Ready, optional" comment)
    Failure Cause           → FailureRecord.FailureCause
    Root Cause               → FailureRecord.RootCause
    Corrective Action         → FailureRecord.CorrectiveAction
    Downtime Hours              → FailureRecord.DowntimeHours (decimal, null on unparseable)
    Work Order Number             → FailureRecord.WorkOrderId (WorkOrder.WorkOrderNumber exact,
                                      soft/optional — identical rule to MaintenanceHistory)
    Description                    → FailureRecord.Description
Transformation: same date/decimal rules as MaintenanceHistory
Required/Optional: Equipment Tag + Failure Date + Failure Mode required (FailureMode is
    required on the target entity, unlike MaintenanceHistory's optional ActionTaken —
    this is a real schema difference, not an oversight)
Reference Resolution: identical rules to MaintenanceHistory (Equipment exact/required,
    WorkOrder exact/soft)
Duplicate Rule: PROPOSED — same composite-content-key pattern as MaintenanceHistory
Conflict Rule: PROPOSED — identical UnresolvedEquipment/ConflictEquipment/ValidationError shape
Unsupported Values: PROPOSED — same as MaintenanceHistory
Migration Priority: RECOMMENDED NEXT SLICE (see §14) — but only once a real source sample
    exists to confirm actual column names/shape; this table is a structural readiness proposal,
    not a validated mapping
```

No other entity's mapping is produced here — WorkOrder, WorkRequest, MaintenancePlan,
MaintenanceStrategy, WorkOrderLabor, and anything requiring Employee identity are marked
Blocked/High-Risk in §4/§9/§13 rather than given a speculative mapping table, per the task's
explicit instruction not to invent mappings where the repository/source does not define them.

## 8. Identity and Idempotency Strategy

| Entity | Authoritative key | Fallback | Unique? | Stable across re-export? | Idempotent import possible? |
|---|---|---|---|---|---|
| FunctionalLocation | `Code` | none | Enforced only at the *import* layer (app-level check), no DB unique index confirmed | Assumed yes (it's a physical/organizational code) | Yes — proven |
| Equipment | `TagNumber` for assignment; `TagNumber + Description` for import | none | No DB uniqueness constraint on `TagNumber` (confirmed by direct inspection, used by the Equipment↔FL Assignment `ConflictEquipment` path) | Assumed yes | Yes — proven, with the caveat in §5 |
| WorkOrder | `WorkOrderNumber` | none | Not confirmed at DB level | Assumed yes | Not implemented; likely straightforward once WorkOrder import is scoped |
| MaintenanceStrategy | none — Equipment+FailureModeCode (app-level convention) | none | Enforced only by app logic (the `BulkCreate` skip-if-active-duplicate rule) | N/A | N/A — no file-based import exists or is recommended here |
| MaintenanceHistory | none — composite content key (Equipment+Date+Action+Technician) | none | App-level only | N/A (content-based) | Yes — proven, with documented limitation (coincidental collisions need reviewer judgment) |
| FailureRecord | none currently | PROPOSED composite (Equipment+Date+FailureMode), mirroring MaintenanceHistory | N/A | N/A | Would be yes, pending real source validation |
| **Employee** | `PersonnelCode` | **none safe** — Name-only matching is explicitly forbidden by this task and by general data-integrity practice (`FirstName`+`LastName` collide easily) | DB-unique **only when non-null** (per the entity's own code comment: "برخی کارکنان/پیمانکاران ممکن است هنوز تخصیص داده نشده باشد" — some staff/contractors may not yet have one assigned) | Assumed yes when populated | **Partial** — records with `PersonnelCode` populated in the source can resolve safely; records without one cannot be auto-matched and must be manually mapped or rejected |
| Vendor | `VendorCode` (optional) | `Name` (risky) | Not confirmed unique | Assumed yes when populated | Not recommended without a real code — and out of CMMS scope regardless (§4) |

```text
Migration Readiness — Employee-dependent entities (WorkOrder.AssignedEmployeeId,
WorkOrderLabor.EmployeeId, WorkRequest.RequestedById, and any "Technician" field a future
importer chooses to resolve as a real FK rather than free text):
BLOCKED — IDENTITY NOT FULLY DETERMINED (partial: only PersonnelCode-bearing source records
resolve safely; this is why MaintenanceHistoryImportService deliberately stores Technician
as free text and never attempts to resolve it to an Employee FK — that was already the
correct, evidence-based decision made last workstream, confirmed still correct here)
```

## 9. Dependency Graph

```
Reference / Master Data (Discipline, Project, EquipmentClass, MaintenanceGroup, MaintenanceType)
        │  no dedicated import; small, admin-managed — READY (manual entry sufficient)
        ▼
Plant / Area / Unit
        │  not separate entities — ride along with FunctionalLocation import — READY
        ▼
Functional Locations
        │  READY — DONE (existing importer)
        ▼
Equipment
        │  READY — DONE (existing importer), depends on FunctionalLocation/Project/Discipline
        ▼
Equipment ↔ Functional Location
        │  READY — DONE (existing assignment pipeline)
        ▼
Maintenance Groups / Types
        │  READY (small reference tables, no import needed at CMMS scale)
        ▼
Maintenance Strategies
        │  PENDING — adjacent to the closed "Bulk Define" workstream; any file-based import
        │  would need its own explicit, separately-scoped design (not attempted here)
        ▼
Maintenance Plans / PM
        │  BLOCKED — hard FK dependency on MaintenanceStrategy existing first
        ▼
Maintenance History
        │  READY — DONE (closed, approved)
        ▼
Failure Records
        │  PENDING — RECOMMENDED NEXT SLICE (§7b, §14) — depends only on Equipment
        │  (+ optional WorkOrder), same shape as the already-proven MaintenanceHistory pipeline
        ▼
Work Requests
        │  BLOCKED — workflow-state business decision + Employee identity (RequestedById required)
        ▼
Work Orders
        │  HIGH RISK — ISO 14224 required-field convention for Corrective/Emergency,
        │  workflow Status decision, optional Employee/Team assignment
        ▼
Work Order Labor / Spare Part Consumption / Costs
        │  BLOCKED — WorkOrderLabor needs WorkOrder import + Employee identity first;
        │  material consumption's real target is MaterialItem (MrsImport, already exists,
        │  not SparePart — see §11)
```

## 10. Data Quality Findings

No real source data exists to profile, so no source-specific data-quality issues (duplicate tags,
mixed-language values, inconsistent date formats, etc.) can be reported — reporting any would be
fabrication. What *can* be reported, from the target schema alone, are **structural data-quality
risks any future real import will need to handle**, each already given a deterministic rule by the
existing pipelines:

| Risk | Classification | Existing/recommended rule |
|---|---|---|
| Duplicate Equipment Tag in the source | Review required | `EquipmentImportService` already treats Tag+Description as the dedup key — a repeated Tag with different Description stages as a second candidate, not silently merged |
| Equipment Tag with no matching PEMS record | Auto-resolvable via import (creates new) | Existing `EquipmentImportService` behavior |
| Functional Location code with no PEMS match | Auto-resolvable via import (creates new) | Existing `FunctionalLocationImportService` behavior |
| Equipment/FunctionalLocation reference that resolves to more than one PEMS record | Reject (row-level) | Every existing pipeline's `ConflictEquipment`/`ConflictFunctionalLocation` status — proven, not proposed |
| Work Order Number reference with no match | Auto-resolvable (soft) | `MaintenanceHistoryImportService`'s proven rule: warn, leave the FK null, never block the row |
| Duplicate historical event (Maintenance History / Failure Record) | Review required | Composite content key, excluded by default, reviewer override permitted |
| Employee/Technician reference with no `PersonnelCode` | Manual mapping required | No automatic resolution attempted anywhere in PEMS today; `MaintenanceHistoryImportService` deliberately stores Technician as free text for exactly this reason |
| Mixed Persian/English values | Review required | No existing importer applies language normalization; values are stored as-is (matches `.claude/CLAUDE.md`'s convention that UI/domain comments may be Persian while data stays as entered) |
| Retired/obsolete Equipment in the source | Review required | `IsActive` flag exists on `Equipment`; every existing import filters `Where(x => x.IsActive)` when resolving references, so an inactive/retired Equipment Tag will resolve as `UnresolvedEquipment` unless explicitly reactivated first — this is a real business decision, not an automatic behavior |
| Negative/invalid Cost or Duration values | Auto-resolvable | `MaintenanceHistoryImportService`'s proven rule: unparseable numeric values are dropped to `null`, never silently coerced to `0` or left as garbage |

## 11. Required Business Decisions

These cannot be resolved by code or by this document — they require a decision from whoever owns
the migration:

1. **SparePart vs. MaterialItem.** `MaintenanceSpareUsage` (the WorkOrder↔parts-consumption link)
   was already switched from `SparePart` to `MaterialItem` in a prior PEMS phase, per that entity's
   own code comment ("SparePart remains for compatibility but is not the future growth path").
   **Recommendation: any future "spare parts" migration should target `MaterialItem` via the
   existing `MrsImport` pipeline, not `SparePart`.** This is stated as a recommendation because it
   follows directly from evidence already in the repository, not as a new architectural decision
   being made here.
2. **Historical WorkOrder status.** Should imported historical Work Orders always land as `Closed`,
   or should the source status be trusted and mapped through? This needs an explicit answer before
   any WorkOrder import is designed — it is a genuine business decision, not a technical one.
3. **ISO 14224 completeness for historical Corrective/Emergency Work Orders.** `.claude/CLAUDE.md`
   states these fields are required for reliability analysis. Real legacy exports frequently lack
   structured failure-mode/cause codes. A decision is needed: reject incomplete historical rows,
   import them with `FailureModeCode`/`FailureCauseCode` left null (accepting reduced analysis
   fidelity for historical data), or require manual classification before commit.
4. **Employee records without `PersonnelCode`.** Reject those source rows' Employee-linked fields,
   assign them to a placeholder, or require HR/admin to backfill `PersonnelCode` before migration.
5. **EquipmentCriticality import vs. the existing assessment workflow.** Should legacy criticality
   classifications be imported as historical/reference data, or should they instead feed into
   (not bypass) the existing `CriticalityAssessmentController` review process?

## 12. Migration Priority

Ordered by dependency-graph readiness (§9) and risk (§4), not by business importance:

```text
1. Reference/master data (Discipline, Project, EquipmentClass, MaintenanceGroup, MaintenanceType)
   — READY, low volume, manual entry likely sufficient
2. FunctionalLocation — READY, DONE
3. Equipment — READY, DONE
4. Equipment ↔ FunctionalLocation — READY, DONE
5. MaintenanceHistory — READY, DONE
6. FailureRecord — RECOMMENDED NEXT (structurally proven pattern, no blockers)
7. MaintenanceStrategy (file-based import, if ever pursued) — PENDING, needs its own
   explicitly-scoped design separate from the closed Bulk Define UI workstream
8. MaintenancePlan — BLOCKED on #7
9. WorkOrder — HIGH RISK, needs business decisions (§11.2, §11.3) before design
10. WorkRequest — BLOCKED, workflow-state decision + Employee identity
11. WorkOrderLabor / MaintenanceSpareUsage (via MaterialItem) — BLOCKED on #9 and Employee identity
```

## 13. Ready / Pending / Blocked Matrix

| Entity | Status |
|---|---|
| FunctionalLocation | READY — DONE |
| Equipment | READY — DONE |
| Equipment ↔ FunctionalLocation | READY — DONE |
| MaintenanceHistory | READY — DONE |
| FailureRecord | PENDING — recommended next, no blockers, needs real source sample |
| MaintenanceGroup / MaintenanceType / EquipmentClass | READY — low volume, manual entry viable |
| EquipmentCriticality | PENDING — needs business decision (§11.5) |
| MaintenanceStrategy (file import) | PENDING — needs its own separately-scoped design |
| MaintenancePlan | BLOCKED — depends on MaintenanceStrategy |
| WorkOrder | BLOCKED — needs business decisions (§11.2, §11.3) |
| WorkRequest | BLOCKED — workflow-state decision + Employee identity |
| WorkOrderLabor | BLOCKED — depends on WorkOrder + Employee identity |
| SparePart | BLOCKED (deliberately) — wrong target entity, see §11.1 |
| MaterialItem | READY — DONE (existing MrsImport, out of CMMS-core scope for this doc) |
| Vendor | OUT OF SCOPE — not a CMMS WorkOrder concept in this schema |

## 14. Recommended Next Implementation Slice

**FailureRecord**, following the exact `MaintenanceHistoryImportService` pattern (Batch/Row
Stage→Review→Commit, ClosedXML Excel parsing, composite content-key idempotency, Equipment
required + WorkOrder optional/soft, independent `"FailureRecordImport"` permission module,
automatic audit reuse). Rationale:

- No dependency blockers (only needs Equipment, already imported).
- Structurally near-identical to a pipeline already built, reviewed, tested (19 tests), and
  approved last workstream — lowest-risk possible next step.
- Genuinely useful on its own (failure history feeds reliability/MTBF analysis, per
  `.claude/CLAUDE.md`'s stated ISO 14224 intent).

**This document does not implement it.** Per this task's explicit instructions, implementation
should wait for either (a) a real source sample to validate the proposed mapping in §7b, or (b) an
explicit follow-up instruction to proceed on the schema-only proposal, whichever the workstream
owner prefers.

## 15. Limitations

- No real legacy CMMS source file exists — every mapping in §7 is either a restatement of an
  already-proven, already-tested mapping (MaintenanceHistory) or an explicitly-labeled, unverified
  proposal (FailureRecord). Nothing here should be read as validated against real data.
- Source data profiling (§6) could not be performed at all — there is nothing to profile.
- Legacy system identity is unknown — no product-specific export assumptions were made.
- `MaintenanceStrategy` file-based import readiness is intentionally left PENDING rather than
  designed in any detail, to avoid any risk of overlapping with the closed "Bulk Define" workstream.
- Some FK/uniqueness facts (e.g., whether `WorkOrder.WorkOrderNumber` has a DB-level unique
  constraint) were checked against entity class definitions and known EF configuration patterns but
  not re-verified against a live schema dump in this pass; where uncertain, this document says so
  explicitly rather than asserting a fact it could not confirm.

## 16. Evidence and Verification Method

- Entity facts: direct `Read` of the actual `.cs` files under `PEMS.Domain/Entities/` (not
  assumed from naming or prior summaries).
- Import behavior facts: direct `Grep`/`Read` of the actual resolution logic in
  `PEMS.Persistence/Servises/EquipmentImportService.cs` and
  `PEMS.Persistence/Servises/FunctionalLocationImportService.cs` for this workstream (re-verified,
  not carried over from memory) — the `EquipmentImport` Tag+Description composite-key finding and
  the `FunctionalLocationImport` Code-only finding were both confirmed this way.
- Controller inventory: `ls PEMS.Web/Controllers | grep -i import` and equivalent listings — exact,
  not estimated.
- Source-dataset absence: filesystem search across the whole repository (excluding build/output
  directories) for common data-export extensions, plus `git status`, re-run fresh for this
  workstream rather than assumed unchanged from the prior one.
- No claim in this document states or implies that a real production migration has been run,
  tested against real data, or validated by any party with access to an actual legacy export.
