# PEMS — Equipment Passport Audit & CBM (Condition-Based Maintenance) — Phase 1 Architecture Audit

**Date:** 2026-10-03
**Status:** Complete. No code changed in this phase.
**Method:** Direct repository verification (grep/read of every entity, controller, service, migration, and view named in this document) plus the full working context of this session's immediately-preceding Condition Monitoring implementation (Phases 0–9 of the prior "Standards-Based Mechanical Equipment Passport & Condition Monitoring" master prompt, documented in `docs/audits/PEMS_CM_AUDIT.md` and `docs/audits/PEMS_CM_PRODUCTION_READINESS_REPORT.md`, both in this same repository, both dated 2026-09-29 through 2026-10-01).

---

## 0. Headline finding

**Almost the entire CBM capability this prompt asks for already exists, built in the immediately preceding session on this same codebase, under different names.** This is not a coincidental overlap — it is the same engineering problem (condition-based maintenance for mechanical/rotating/static equipment) solved once already, two days ago, in this same repository. The correct action is **not** to re-implement it under this prompt's vocabulary, but to (a) map this prompt's concepts onto the existing names precisely, (b) extend only the genuine, specific gaps identified below, and (c) never create a second `EquipmentCBMProfile`/`CBMPlan`/`CBMMeasurementPoint`/`CBMFinding` entity family alongside the existing `EvaluationProfile`/`MaintenanceStrategy`/`ConditionMonitoringPoint`/`Finding` family — that would be exactly the "duplicate architecture" this prompt's own Rule #1–#10 forbids.

A fresh repository check (`grep` for `CBMProfile`, `CBMPlan`, `CBMMeasurementPoint`, `CBMInspectionEvent`, `CBMFinding` across `PEMS.Domain`/`PEMS.Application`/`PEMS.Persistence`/`PEMS.Web`) returns **zero matches** — confirming no prior attempt used this prompt's literal naming, and that the existing capability must be found under its own (different) names, listed below.

---

## 1. Concept-by-concept mapping: this prompt's vocabulary → what already exists

| This prompt's concept (§06–§13, §31) | Existing PEMS equivalent | Status |
|---|---|---|
| `EquipmentCBMProfile` (Enabled/Disabled, Strategy, Frequency, Responsible Group/Team, Criticality, Method, Review Frequency, Effective From/To) | `MaintenanceStrategy` (`StrategyType="CBM"`) + `MaintenanceStrategy.ConditionMonitoringPointId` (added this session) + `MaintenancePlan` (frequency/trigger) + existing `MaintenanceGroup`/`Team`/`EquipmentCriticality` | **REUSE** — no new entity needed. A CBM profile *is* a `MaintenanceStrategy` row with `StrategyType="CBM"` linked to a `ConditionMonitoringPoint`. |
| `CBMStrategy` classification (Time-Based/Condition-Based/Predictive/RTF/Inspection-Based) | `MaintenanceStrategy.StrategyType` (free string; `"CBM"` already a supported literal alongside `"Preventive"`/`"Predictive"`/`"Corrective"`/`"RunToFailure"`) | **REUSE** |
| `CBMPlan` | `MaintenancePlan` (Calendar/Meter/Both trigger, `NextDueDate`) + `PMTemplate`, created from the `MaintenanceStrategy` exactly like a Preventive plan is today | **REUSE** — confirmed this session: `MaintenanceCalendar` shows CBM-plan-generated work the same way it shows PM, with zero new scheduler code |
| `CBMPlanParameter` / reusable parameter definition (Name, Code, Unit, Data Type, Method, Standard Reference, Engineering Approval) | `ConditionMonitoringPoint.VariableType` (constrained to an 11-value allow-list: Vibration/Temperature/Pressure/Flow/Current/Voltage/Lubrication/OilAnalysis/Thermography/Acoustic/ProcessDeviation) + `EvaluationProfile` (`VariableType`, `StandardReferenceId`, `EquipmentClassId`, `OperatingCondition`, `Version`) | **PARTIAL REUSE — one genuine gap**: `EvaluationProfile` already carries the Standard Reference + versioning this prompt wants, but there is **no reusable "parameter catalog"** independent of a specific point — see §3.1 |
| `CBMMeasurementPoint` | `ConditionMonitoringPoint` (`EquipmentId`, `VariableType`, `Tag`, `Unit`, `AlarmThresholdLow/High`, `EvaluationProfileId`) | **REUSE, but ad-hoc not template-driven** — see §3.2 |
| Instrument / measurement device | `Instrument` entity + `CalibrationHistory` (append-only, `CalibrationDate`/`Result`/`NextDue`) — a complete, working Instrument Passport | **REUSE** — `ConditionReading.Source` records "Portable"/"Manual"/etc. but does **not** FK to a specific `Instrument` row; see §3.3 |
| `CBMInspectionEvent` | No single "event" wrapper exists — each `ConditionReading` *is* the atomic record (one point, one timestamp, one value), matching the append-only philosophy already used for `CalibrationHistory`/`MaintenanceHistory` | **REUSE (different shape, by design)** — see §3.4 for why a wrapper event was deliberately not retrofitted |
| `CBMMeasurementResult` | `ConditionReading` (`Value`, `Unit` via parent point, `ReadingDate`, `Source`, `Provenance="OBSERVED"`, `IsAlarm`, `EvaluationProfileId`/`EvaluationProfileVersion`/`EvaluationStatus` frozen at write time, `Notes`, `RecordedBy`) | **REUSE** — this already satisfies §13's full field list except direct `Instrument` FK (§3.3) and `OperatingCondition` per-reading (§3.5) |
| `ConditionEvaluation` (Normal/Warning/Alarm/Critical/NotEvaluated/RequiresEngineeringReview) | `EvaluationLimit` (per-`EvaluationProfile`, per-Status row, `LowValue`/`HighValue`/inclusive flags) + `ConditionMonitoringService.EvaluateAgainstProfile` (severity-ordered band match, `"NotEvaluated"` when unconfigured — never fabricates `"Normal"`) | **REUSE** — covers Normal/Warning/Alarm/Critical/NotEvaluated exactly. `RequiresEngineeringReview` is the one literal state not present — see §3.6 |
| Trend/history engine | `IConditionMonitoringService.GetTrendAsync` (bounded 7/30/90/365/custom range, 2-year cap, 2000-point hard cap, `Truncated` flag) + `ReliabilityController.Trend`/`ExportTrend` (Chart.js + ClosedXML export) | **REUSE** — built and verified this session |
| `CBMFinding` | `Finding` entity (`EquipmentId`, `ConditionReadingId`/`InspectionRecordId` XOR, `Condition` frozen from source, `FailureModeId`/`FailureCauseId`, `WorkRequestId`, `Status`) | **REUSE** |
| Finding → Work Request → Work Order | `FindingController.RaiseWorkRequest` → `IWorkRequestService.CreateAsync` (same call `InspectionController.RaiseWorkRequest` already made) → existing `WorkRequest → WorkOrder` governed workflow, untouched | **REUSE** |
| Import Center integration | `ConditionReadingImportService`/`Controller` (exact `MaintenanceHistoryImportService` template: Stage→Review→Commit→Discard), registered in `ImportCatalog.All` | **REUSE** |
| Export | `ReliabilityController.ExportTrend` (exact `InstrumentController.Export` template: ClosedXML + `AddStyledHeaderRow`/`FinalizeExportSheet`) | **REUSE** |
| Authorization | `CMFinding`, `ConditionReadingImport` permission modules — added to `PermissionMatrixController`'s grantable-module list **this session**, verified end-to-end (grant → `RolePermissionGrants` → revert) | **REUSE, and a real gap from this prompt's §29 list was found and fixed already** |
| Standards references | `StandardReference` (`Edition`/`PublishedDate`/`EffectiveFrom`/`EffectiveTo`/`Status`, metadata-only, no copyrighted text) — ISO 17359:2018 / ISO 20816-1:2016 / ISO 18436-2:2014 seeded | **REUSE** |
| Dynamic/custom attribute architecture | **Confirmed, fresh grep this phase: zero hits** for `AttributeDefinition`/`CustomField`/`DynamicAttribute`/`AttributeTemplate` anywhere in `PEMS.Domain` | **Does not exist — do not add one.** PEMS' established convention remains flat, purpose-built satellite tables (`Instrument`, `MechanicalEquipment`, `ConditionMonitoringPoint` are all this shape) |

---

## 2. Equipment Passport — current state vs. this prompt's §05/§26 ask

The Equipment Passport (`PEMS.Web/Views/Equipment/Details.cshtml`) already has, as tabs on the single Equipment Details page (confirmed by direct read this phase):

- **Overview** — identification, classification, criticality, functional location (§05 "Identification" — already satisfied)
- **Instrument Passport** tab (`#tab-instrument`, conditional on `Model.Instrument != null`) — full Instrument Passport summary + link to the dedicated `Instrument/Passport` page
- **Mechanical Passport** tab (`#tab-mechanical`, conditional on `Model.MechanicalEquipment != null`, added this session) — Mechanical/Rotating/Static summary + link to the dedicated `MechanicalEquipment/Passport` page
- **Asset** tab — CMMS lifecycle summary
- Documents, Work Orders, Work Requests, Strategies, Plans sections (confirmed present from the broader `EquipmentPassportDto` structure used throughout this session)

**What is genuinely missing, matching this prompt's §26 exactly:** there is **no "Condition Monitoring" tab on the Equipment Passport itself**. The entire CM capability (points, readings, evaluation, trend, CBM strategy link) is fully built and working, but only reachable via the separate `Reliability/ConditionMonitoring?equipmentId=X` page and the separate `Finding/Index?equipmentId=X` page — not embedded in the passport the way Instrument/Mechanical are. This is the single most concrete, correctly-scoped gap this audit identifies, and it maps directly onto this prompt's own explicit instruction: *"Enhance the existing Equipment Details / Passport page. Do not create an unrelated standalone equipment page."*

---

## 3. Genuine gaps (the only things that should actually be built)

### 3.1 — No reusable parameter catalog independent of a point
Today, `VariableType` is a fixed 11-value allow-list on `ConditionMonitoringService.ValidVariableTypes`, and `EvaluationProfile` is keyed by `VariableType` + optional `EquipmentClassId`. There is no standalone "CBM Parameter" master-data row (Name/Code/Unit/Method/StandardReference) that a `ConditionMonitoringPoint` merely *references* — each point just carries its own `VariableType`/`Unit` directly. **This is a real but minor gap**: the existing shape already prevents the main risk (free-text parameter sprawl) via the allow-list, so extending it is optional polish, not a blocking gap.

### 3.2 — No equipment-class-driven measurement-point templates (§09, §17)
`ConditionMonitoringPoint` rows are created one at a time, per equipment, by a user typing a `Tag` and picking a `VariableType` — there is no "when Equipment Class = Centrifugal Pump, suggest DE Bearing Vibration / NDE Bearing Vibration / Motor Temperature / Suction Pressure / Discharge Pressure" template mechanism. **Confirmed gap, fresh grep this phase: zero hits** for `MeasurementPointTemplate`/`EquipmentClassMeasurementPoint`. This is genuinely useful (§17's pump/compressor/fan/gearbox template lists) but is additive UI/data convenience on top of the existing `ConditionMonitoringPoint` table — not a new entity family, just a new small `EquipmentClassMeasurementPointTemplate` lookup table feeding the existing "Add Monitoring Point" form.

### 3.3 — `ConditionReading` does not FK to a specific `Instrument`
`ConditionReading.Source` is a free string (`"Manual"`/`"SCADA"`/`"OPC"`/`"Portable"`) describing the *channel*, not a specific instrument asset with a calibration due date. This prompt's §11/§13 wants the measurement to reference *which* instrument took it (so an expired-calibration instrument's readings are traceable). **Confirmed gap.** The existing `Instrument`/`CalibrationHistory` architecture is the correct reuse target — this needs only an optional `ConditionReading.InstrumentId` FK, additive, nullable (most `ConditionReading` rows today have no instrument at all, by design, since `Source="Manual"` logbook entries never had one).

### 3.4 — No `CBMInspectionEvent` wrapper around a batch of measurements
Deliberately not built, and this audit recommends **not** building it as its own entity. `CalibrationHistory` (the closest existing precedent for "one visit, one record") is one row per calibration event, not a parent-with-many-child-measurements shape — and `MaintenanceHistory` follows the same one-row-per-visit pattern. Retrofitting a parent "event" entity around existing `ConditionReading` rows would require either (a) a new FK on every `ConditionReading` to a new `CBMInspectionEvent` table (schema churn on a table already in production use as of this session), or (b) grouping existing readings by `(EquipmentId, timestamp-proximity)` heuristically (fragile, not reproducible). **Recommendation: if a "visit summary" view is wanted, build it as a read-only aggregation query (group existing `ConditionReading` rows by equipment + date) — not a new table.** This avoids both the duplicate-architecture risk and a disruptive schema change to a table with live data.

### 3.5 — No per-reading `OperatingCondition`
`EvaluationProfile.OperatingCondition` exists (e.g. "Normal Load", "Startup") as a profile-selection criterion, but an individual `ConditionReading` does not itself record what operating condition the equipment was in at the moment of measurement — it only records which profile *version* was used to evaluate it. **Minor, real gap** if a site needs "this reading was taken during startup, don't alarm on it" — currently the only way to get that effect is a separate `EvaluationProfile` scoped to "Startup," which already works but requires a human to manually pick the right profile per visit rather than recording the condition and having the system pick.

### 3.6 — `RequiresEngineeringReview` is not a distinct evaluation state
Today's `EvaluationStatus` literal set is `Normal`/`Warning`/`Alarm`/`Critical`/`NotEvaluated` — this prompt's §14 adds `RequiresEngineeringReview` as a sixth state, distinct from `NotEvaluated` (meaning "a human looked and could not conclusively classify this," vs. "no limit is configured at all"). **Real, small, additive gap**: add the literal as a sixth allowed `EvaluationStatus` string (no schema change — it's already a plain string column, not an enum/check-constraint) and a UI action to set it manually on a reading.

### 3.7 — No configurable Condition→Priority mapping (§22)
`FindingController.RaiseWorkRequest` currently maps `Finding.Condition` to `WorkRequest.Priority` via a **hardcoded C# switch** (`"Critical"/"Reject" → "Critical"`, `"Alarm"/"Poor" → "High"`, `"Warning" → "Medium"`, default `"Medium"`). This prompt's §22 explicitly requires this mapping to be *configurable and auditable*, not hardcoded. **Confirmed real gap, fresh grep this phase: zero hits** for `ConditionPriorityMapping`/`PriorityRule`. This is the most clearly "must fix" item in this audit — it's explicitly named as a rule (§22), it's cheap to fix (a small lookup table + admin UI, following the exact `MaintenanceMasterDataController` CRUD pattern already used for every other small lookup this session), and the current hardcoded version is a real, demonstrable violation of "do not automatically equate Critical=Priority X unless explicitly configured."

### 3.8 — No CBM Dashboard (§27–§28)
**Confirmed gap, fresh grep this phase: zero hits** for `CbmDashboard`. The data needed already exists (point counts, finding counts by status, overdue-via-`MaintenancePlan.NextDueDate` through the `MaintenanceStrategy.ConditionMonitoringPointId` link) but no aggregation controller/view exists yet. This is pure additive UI on top of existing queries — no new entity.

### 3.9 — No separate Evaluate/Approve permission split
This prompt's §29 anticipates `Equipment.CBM.Evaluate` as distinct from `Equipment.CBM.Record`/`.Approve`. In the existing design, evaluation is **deterministic and automatic** (computed and frozen the instant a reading is recorded, per §26 of the prior CM master prompt — "never retroactively reinterpret"), not a discretionary human act requiring its own gate. **This is an intentional design difference, not a gap** — adding an "Evaluate" permission would imply a human evaluation step that does not exist and should not be invented merely to match this prompt's permission list. If item 3.6 (`RequiresEngineeringReview`) is built, *that* manual override action is the correct place for a dedicated permission (e.g. `CMFinding.Override` or similar), not a generic "Evaluate."

---

## 4. Explicit non-findings (checked, confirmed not to exist, correctly so)

- No second Equipment entity, no second Work Order/Work Request system, no second workflow engine, no second authorization engine, no second document system, no second Import Center — all re-verified this phase by the same checks the prior session's own audit already performed, now re-confirmed after this session's additions did not introduce any of them.
- No dynamic/EAV attribute system (§03 "Existing dynamic/custom data architecture") — confirmed absent, and PEMS' satellite-table convention should continue to be followed for any of §3's additive fields, never a first EAV system.

---

## 5. Recommended additive scope (Phase 4 onward, after this audit)

In priority order, matching this document's own gap numbering:

1. **Equipment Passport "Condition Monitoring" tab** (§2) — the highest-value, most literally-requested item. Pulls together: the point list (already queried by `IConditionMonitoringService.GetPointsForEquipmentAsync`), the linked `MaintenanceStrategy`/`MaintenancePlan.NextDueDate` (new join, no new table), and a link to `Finding/Index?equipmentId=X`.
2. **Condition→Priority configurable mapping** (§3.7) — the one item explicitly flagged as a rule violation if left hardcoded.
3. **`ConditionReading.InstrumentId`** optional FK (§3.3) — small, additive, nullable.
4. **`RequiresEngineeringReview`** as a sixth `EvaluationStatus` literal + manual-override action (§3.6).
5. **CBM Dashboard** (§3.8) — pure aggregation UI, no new entity.
6. **Equipment-class measurement-point templates** (§3.2) — convenience, lowest urgency.
7. **Per-reading `OperatingCondition`** (§3.5) — convenience, lowest urgency.
8. **Visit-summary aggregation view** in place of a `CBMInspectionEvent` entity (§3.4) — only if a real user need for it surfaces; do not build speculatively.

None of these require a new Equipment/Plan/Measurement/Finding entity family. All are additive columns, one or two small new lookup tables, or pure UI/query work on top of what already exists and was verified working end-to-end in the immediately preceding session.

---

## 6. Phase 1 verdict

**PASS.** Audit complete. The overwhelming majority of this prompt's requested capability already exists and was verified working this same week. Proceeding, on confirmation, to implement only the 8 items in §5 — each one individually small, additive, and precedented — rather than any wholesale re-implementation under this prompt's `CBM*` vocabulary.
