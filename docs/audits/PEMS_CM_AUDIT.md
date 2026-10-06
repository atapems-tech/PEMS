# PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring — Phase 0 Audit

**Date:** 2026-09-29
**Status:** Complete. No code changed in this phase.
**Method:** Full-repository audit via three parallel research passes covering (1) Equipment/Instrument/Asset/Document architecture, (2) Inspection/RcmFmea/Reliability/CriticalityAssessment/MaintenanceMasterData, (3) MaintenancePlan/JobPlan/MaintenanceCalendar/Import Center/WorkRequest — plus direct verification of `StandardReference`.

---

## 0. Headline finding

**A substantial fraction of this mandate already exists in the codebase, mostly unwired.** Most critically, `ConditionMonitoringPoint`/`ConditionReading` (a Phase 9 migration, `20260910131407_Phase9_ConditionMonitoring_Predictive_AIGovernance`) already implement typed measurement points with alarm thresholds and append-only readings, with a working service, DTOs, and a live UI page under `ReliabilityController`. A full `MechanicalEquipment → RotatingEquipment/StaticEquipment/ElectricalEquipment` specialization hierarchy plus `EquipmentDatasheet` already exist in the Domain model and are fully EF-migrated into the database — but have **zero** Application/Web layer: no DTOs, no service, no controller, no view, no test. `StandardReference` already exists as metadata-only (no copyrighted text), already referenced by `JobPlan`/`PMTemplate`, but lacks the Edition/EffectiveFrom/EffectiveTo/Status fields the mandate's §22 requires. `MaintenanceStrategy.StrategyType` already includes `"CBM"` as a literal value. A full ISO-14224-style failure taxonomy (`FailureMode`/`FailureCause`/`FailureMechanism`/`FailureEffect`/`FailureConsequence`) and a full reliability analytics layer (MTBF/MTTR/Availability/Pareto/BadActors/trend, from real WorkOrder/FailureRecord data) already exist and are already correct — nothing here should be rebuilt.

**This changes the shape of the work from "build 12 phases from scratch" to "wire up what exists, extend narrowly and precedentedly where real gaps remain."**

---

## 1. Architecture map — REUSE / EXTEND / NEW

| Capability | Verdict | Existing piece |
|---|---|---|
| Equipment master, taxonomy (`EquipmentClass`, self-referencing tree) | **REUSE** | `Equipment.cs`, `EquipmentClass.cs` |
| Asset, FunctionalLocation | **REUSE** | `Asset.cs`, `FunctionalLocation.cs` |
| Document attachment (datasheet, OEM manual, GA drawing, calibration cert) | **REUSE** | `EngineeringDocument`/`MaintenanceDocument`/`DocumentAttachment`, typed FK on the subtype |
| Mechanical/Rotating/Static/Electrical equipment specialization | **EXTEND** (wire up) | `MechanicalEquipment.cs`, `RotatingEquipment.cs`, `StaticEquipment.cs`, `ElectricalEquipment.cs` — Domain + DbContext only, zero app/web layer |
| Equipment datasheet (design pressure/temp, manufacturer, spec) | **EXTEND** (wire up) | `EquipmentDatasheet.cs` — same orphaned state |
| Standards reference metadata | **EXTEND** | `StandardReference.cs` — exists (`Code`, `Organization`, `StandardNumber`, `Title`/`TitleFa`, `Category`, `IsSystemDefault`), already FK'd from `JobPlan`/`PMTemplate`, already managed in `MaintenanceMasterDataController`. Missing: `Edition`, `PublishedDate`, `EffectiveFrom`, `EffectiveTo`, `Status` — exactly what §1.3/§22 need for "don't hardcode ISO 20816-1 as permanent." |
| Condition monitoring points + readings | **EXTEND** | `ConditionMonitoringPoint`/`ConditionReading` (`ConditionMonitoring.cs`) — has `VariableType`, `Tag`, `Unit`, `AlarmThresholdLow/High`; readings are append-only with `Source`/`Provenance`/`IsAlarm`/`RecordedBy`. Service `IConditionMonitoringService` has `GetFoundationStatusAsync`, `GetPointsForEquipmentAsync`, `CreatePointAsync`, `RecordReadingAsync`. UI: `ReliabilityController.ConditionMonitoring`/`CreateConditionMonitoringPoint`/`RecordConditionReading`. |
| Evaluation profile / configurable multi-context limits | **NEW** (precedented) | Does not exist for CM specifically — but `GasParameterLimit`/`GasReading` (`GasTest.cs` family) is a proven, working precedent for exactly this shape: a DB-driven limit table keyed by parameter(+context), separate from the reading, with a compute-and-freeze-`Result`-at-submit-time pattern. Model the new `EvaluationProfile`/`EvaluationLimit` on this, adding the versioning `ConditionMonitoringPoint` itself doesn't have. |
| Periodic CM scheduling | **REUSE** | `MaintenanceStrategy` (`StrategyType="CBM"` already a supported literal) + `MaintenancePlan` (Calendar/Meter/Both trigger, `NextDueDate`) + `PMTemplate` (equipment-class-level template) + `IPMGenerationService` (idempotent WorkOrder generation). `MaintenanceCalendar` shows a plan's due work **only once a WorkOrder is generated from it** — same as PM today, so CM plans get calendar visibility for free via the same path. |
| Inspection round checklist / steps | **REUSE** | `JobPlan`/`MaintenanceTask` (steps), `JobPlanRequiredSkill`, `JobPlanRequiredMaterial` |
| Single ad-hoc quantitative inspection | **partially overlaps, keep separate** | `InspectionController`/`InspectionRecord` already has `MeasurementValue`/`MeasurementUnit`/`LowerLimit`/`UpperLimit` and a `RaiseWorkRequest` action — a real precedent for the Finding→WorkRequest flow, but single-value/no-trend/no-measurement-point-reference. Left as-is; new CM Findings should follow its `RaiseWorkRequest` pattern, not replace it. |
| Failure taxonomy (ISO-14224-style) | **REUSE** | `FailureMode`/`FailureCause`/`FailureMechanism`/`FailureEffect`/`FailureConsequence` (RCM-FMEA, `FailureTaxonomy.cs`) — full coverage, including many-to-many `FailureModeCause`/`FailureCauseMechanism` |
| Failure event recording | **REUSE** | `FailureRecord` — has `DowntimeHours`, dual free-text + optional FK to the real `FailureMode`/`FailureCause` taxonomy |
| Reliability analytics (MTBF/MTTR/Availability/Pareto/trend/bad-actor) | **REUSE** | `ReliabilityService`/`ReliabilityAnalyticsService`, computed from real `WorkOrder`/`FailureRecord` data — do not rebuild |
| Equipment criticality | **REUSE (reference only)** | `EquipmentCriticality.CriticalityClass` (A/B/C) — `PMTemplate.ApplicableCriticalityClasses` already keys off it; a new evaluation profile can key off it the same way |
| Finding entity | **NEW** (small, precedented) | No entity plays this role today. Model on `InspectionController.RaiseWorkRequest`'s existing flow; link to `FailureMode`/`FailureCause`, optionally generate a `FailureRecord` |
| WorkRequest source attribution | **EXTEND** (small) | No `Source`/`Origin` field exists on `WorkRequest` today. Closest existing convention: `CriticalityAssessment.SourceType`/`SourceIdentifier`. Add the same string-pair to `WorkRequest` rather than inventing a new mechanism. |
| Work Order integration / workflow | **REUSE, untouched** | Existing `WorkRequest → WorkOrder → Workflow` governance (Issue/Release, Start Work, Execution, Completion, Verification, Approval, Closed) — a Finding creates a `WorkRequest` through the existing service exactly like a manual request does; no new workflow stage |
| Import Center | **EXTEND, exact template found** | `MaintenanceHistoryImportService` is a complete, working template: dedicated `XyzImportBatch`/`XyzImportRow` entities (no shared generic base), a 7-method service interface (`StageFromExcelAsync`, `GetBatchSummaryAsync`, `GetBatchAsync`, `GetRecentBatchesAsync`, `SetRowIncludeAsync`, `CommitBatchAsync`, `DiscardBatchAsync`), a matching controller, DI registration, and one `ImportCatalog.All` entry. Replicate exactly for a new Measurement-history import and a CM Plan/Point import. |
| Authorization | **REUSE, untouched** | `AccessControlService`/`RolePermissionGrant`/Permission Matrix — new permissions only (`MechanicalEquipment.*`, `CMPlan.*`, `CMInspection.*`, `CMFinding.*`), no new engine |
| Audit | **REUSE, untouched** | `AuthorizationAuditLog` (permission decisions) + entity-level `AuditLog` — new mutations wired into the existing pattern, no new audit mechanism |
| Dynamic/configurable attribute system | **does not exist, and should not be added** | Grepped `AttributeDefinition`/`CustomField`/`DynamicAttribute`/`AttributeTemplate` — zero hits anywhere in the codebase. PEMS' established convention for entity-specific fields is a flat, purpose-built satellite table keyed by `EquipmentId` (exactly what `Instrument`/`MechanicalEquipment`/`RotatingEquipment`/`EquipmentDatasheet` already are). New mechanical-design fields follow this same convention — no EAV system is introduced. |

---

## 2. What "Mechanical Equipment Passport" (§8) actually requires

Given §1, the passport is **not a new entity** — it's the missing Application/Web layer over `Equipment` + `MechanicalEquipment` + `RotatingEquipment`/`StaticEquipment`/`ElectricalEquipment` + `EquipmentDatasheet`, following exactly the delivery pattern the existing `Instrument` "Instrument Passport" already established (DTOs in `PEMS.Application/DTOs`, a service in `PEMS.Persistence/Servises`, a controller/views, wired into Equipment Details as a new tab per §50). The one real structural question to resolve before coding: `RotatingEquipment`/`StaticEquipment` currently FK to `MechanicalEquipment` (not directly to `Equipment`), and `MechanicalEquipment.EquipmentId` is nullable — meaning the chain `Equipment → MechanicalEquipment → RotatingEquipment` is optional today. This is a pre-existing modeling decision, not something this phase should silently "fix" by making the FK required (that would be a breaking, judgment-call schema change) — Phase 1 should confirm this is intentional (equipment can exist without a mechanical profile) and preserve it, only adding an Application-layer guard that creates the `MechanicalEquipment` row automatically the first time a Rotating/Static/Electrical profile is added for an Equipment that doesn't have one yet.

## 3. What "Condition Monitoring" (§27–§32) actually requires

Given §1, the CM core (points + readings) already exists and works. The real, additive scope is:
1. An `EvaluationProfile`/`EvaluationLimit` pair, modeled on the proven `GasParameterLimit` shape, adding `StandardReferenceId`, `EquipmentClassId`, `EffectiveFrom`/`EffectiveTo`, `Version` — so a measurement's interpretation is frozen at evaluation time (§26) instead of the current single static Low/High pair on the point.
2. Linking a `ConditionMonitoringPoint`'s scheduled collection to the existing `MaintenanceStrategy`(`StrategyType="CBM"`)/`MaintenancePlan`/`PMTemplate` chain — no new scheduler.
3. A trend/history query extending `ConditionMonitoringService` (bounded date-range queries per §30/§58) — today's service only returns a last-value rollup, not a series.
4. A `Finding` entity + a `RaiseWorkRequest`-equivalent action, modeled directly on `InspectionController.RaiseWorkRequest`.
5. `WorkRequest.SourceType`/`SourceIdentifier`, modeled on `CriticalityAssessment`'s existing convention, so a Finding-originated request is distinguishable from a manual one without inventing a new mechanism.

## 4. Risk register

- **Orphaned entities may have drifted from a clean migratable state.** `MechanicalEquipment`/`RotatingEquipment`/`StaticEquipment`/`ElectricalEquipment`/`EquipmentDatasheet` were migrated but never exercised by any service — Phase 1 must verify their EF configuration and current migration state still apply cleanly before building on them, not assume they're production-ready because they exist.
- **`GasParameterLimit` precedent is a pattern to imitate, not a table to share.** GasTest/GasReading/GasParameterLimit remain their own bounded domain (Permit-to-Work gas testing); the new `EvaluationProfile`/`EvaluationLimit` must be its own table family, referencing `StandardReference`/`EquipmentClass`, not a foreign borrow of the GasTest schema.
- **`StandardReference` extension is additive-only.** Adding `Edition`/`PublishedDate`/`EffectiveFrom`/`EffectiveTo`/`Status` to the existing table is a pure column addition — no existing `JobPlan`/`PMTemplate`/`MaintenanceMasterDataController` consumer needs to change.
- **No fabricated engineering limits.** Per §25, the new `EvaluationLimit` seed data (§62) will ship with zero populated numeric thresholds — only the `StandardReference` metadata rows (ISO 14224/17359/20816/18436 as *references*, not limits) and category/method master data. Any actual vibration/temperature/pressure limit is left "Requires Engineering Approval" / unconfigured until a real engineering source provides it.

---

## 5. Phase 0 verdict

**PASS.** Audit complete. Proceeding to Phase 1 (Domain/Database design) informed by the REUSE/EXTEND/NEW map above — no schema change will be made without direct textual confirmation against the actual current entity/migration state, matching the discipline already demonstrated in this session's earlier phases (e.g. the live-database check that caught the Equipment.View permission gap before it shipped as a regression).
