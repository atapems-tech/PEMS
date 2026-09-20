# Workflow Platform — Phase 3A: Work Order Governance Baseline

**Classification: READY FOR PHASE 3B**

Definition/baseline phase only. Nothing in code, migrations, seeds, Identity roles, permissions, workflow seed data or business data was changed; only this document. **The approved organizational model below is the target baseline; the current repository model is recorded only as the current-state gap** (`CURRENT REPOSITORY MODEL ≠ TARGET PEMS ORGANIZATION`).
Convention: *Evidence* = verified in the repository/`PEMSDB` (read-only); *Baseline* = decided by the approved business baseline; *Derived* = follows directly from the baseline without a new business choice.

## 1. Executive summary
Work Order governance is now fully defined: four separate organizations (Engineering, Maintenance, Operations, Warehouse); a Maintenance hierarchy in which **Maintenance Planning is mandatory**; **Operations is the issue/release authority**; the **originating discipline verifies completion and gives final approval**; **originating discipline and responsible maintenance discipline are distinct concepts**; multi-discipline review stays a **separate process (Architecture A)** with its own workflow identity; the three existing Work Orders stay legacy; `WorkOrder.Status` becomes a projection of the workflow stage. The current lifecycle (`Open→Assigned→InProgress→Completed→Closed`) is **not** carried forward. All gaps in the repository are listed as Phase 3B implementation requirements (§20). No further business decision is required to start Phase 3B (§21).

## 2. Approved organizational baseline
Four separate organizational domains — **Engineering, Maintenance, Operations, Warehouse**. They are never merged into one hierarchy, and no domain's roles substitute for another's. The target model distinguishes six concepts that are **not interchangeable**:

| Concept | Meaning | Where it lives (today / target) |
|---|---|---|
| Organization | Engineering / Maintenance / Operations / Warehouse | today: not modelled (only `Department` free master data) → target: explicit organization attribute (§8) |
| Department / Discipline | `Discipline` master (Mechanical, Electrical, Instrumentation, Civil, Process …) | `Discipline` table (no organization attribute) |
| Position | business title in a hierarchy | not modelled → target: represented by role + scope (§8) |
| Role | Identity role assigned to a user | `AspNetRoles` |
| Permission | `Resource.Action` grant | `Permissions`/`RolePermissionGrant` |
| Workflow responsibility | who may act at a stage/transition | workflow participant (permission-based pseudo-role, Work Request reference) + SoD |

## 3. Engineering hierarchy (Baseline)
Complex Manager/Deputy → Senior Complex Shift Supervisor → Engineering Manager/Deputy → General Engineering Head/Deputy → Process Engineering Head/Deputy → Discipline Supervisor → Discipline Expert → General Engineering Planning.
Engineering authority in Work Order governance is **only** that of an *originating discipline* (§11): Engineering positions verify and give final approval for Work Orders that originated from an Engineering discipline — by virtue of origination, **not** through a module-wide Work Order permission and **not** as planning, issue, execution or maintenance-side approval.
**Current-state gap (Evidence):** `WorkOrder` module permissions are seeded to `EngineeringManager` (View/Create/Edit/Delete/Approve), `EngineeringSupervisor` (View/Create/Edit/Approve) and `DisciplineEngineer` (View/Approve → can close/cancel/reject any Work Order). Engineering roles therefore hold maintenance authority today. Not modified in this phase.

## 4. Maintenance hierarchy (Baseline)
Maintenance Manager/Deputy → **Maintenance Planning Head/Deputy → Maintenance Planning Experts** → Maintenance Discipline Head/Deputy → Senior Maintenance Expert → Maintenance Discipline Experts/lower levels. Maintenance Planning is a first-class organizational function with its own Head/Deputy/Experts and its own stage in the Work Order lifecycle (§10).
**Current-state gap (Evidence):** no maintenance positions exist. Identity roles: `MaintenanceEngineer` (all Work Order rights) and a custom `"maintenance planning "` (trailing space, no Work Order permission). `MaintenanceRole` rows (Technician / Senior Technician / Crew Leader) are a crew/skill model, not the authority hierarchy.

## 5. Operations authority (Baseline)
Operations is a separate authority. **Operations is the Work Order issuer/release authority** (mandatory): a Work Order cannot start execution without an Operations issue/release, and neither Engineering nor Maintenance (including Planning) can substitute for it.
Operations also: may **decline** an issue request (returning the Work Order to Planning or rejecting it), and is the party the existing Permit/LOTO release gate (`WorkOrderReleaseGateService`) belongs to — the gate remains a Work Order business rule evaluated at issue time. Position names within Operations are an organizational configuration item (§8), not a rule.
**Current-state gap (Evidence):** no Operations department/role exists in `PEMSDB` (departments: Maintenance, Planning & Control, Engineering, HSE, QA/QC, Construction, Project Management, Procurement, Finance, IT, HR, Document Control). Not created in this phase.

## 6. Warehouse separation (Baseline)
Warehouse is a separate organization. In Work Order governance it has **no workflow authority**: it provides material availability and performs material issue/return against a Work Order (existing inventory business logic: `MaterialStockService`, spare usage, `WorkOrderMaterial`). Warehouse roles must never be used as Maintenance roles. **Current-state gap (Evidence):** Warehouse exists as inventory entities (`Warehouse`, `StorageLocation`), not as an organizational/role model.

## 7. Maintenance disciplines (Baseline)
Mechanical, Electrical, **Instrumentation**, Civil. Canonical discipline name for Instrumentation is **`Instrumentation`** (the future implementation must use it).
**Seed gap (Evidence, not fixed here):** `MaintenanceRoleSeeder` looks up `"Instrument"` while the discipline is `"Instrumentation"`, so Instrumentation has **no** `MaintenanceRole` rows (Mechanical, Electrical, Civil have 3 each = 9). No "Instrument" discipline is to be invented.
**Discipline-identity gap (Evidence):** `Discipline` and `Employee` carry no *organization* attribute, so "Mechanical" in Engineering and "Mechanical" in Maintenance are the same row. Phase 3B must therefore express *responsible maintenance discipline* as **(DisciplineId, Organization = Maintenance)** and *originating discipline* as **(DisciplineId, OrganizationDomain of the originator)** — otherwise the Process Engineering → Mechanical Maintenance example (§12) cannot be represented without ambiguity.

## 8. Role vs position vs permission model (target)
| Target position (Organization) | Role code (design only) | Work Order permissions (design) | Scope |
|---|---|---|---|
| Maintenance Manager / Deputy (Maintenance) | `MNT.Manager` | Cancel, Reject (any stage), Close-override per §13, View | Global (Maintenance) |
| Maintenance Planning Head / Deputy | `MNT.PlanningHead` | Plan, Assign discipline, Approve plan, Reschedule, Cancel/Reject (pre-issue), View | Global (Maintenance) |
| Maintenance Planning Expert | `MNT.PlanningExpert` | Plan, Schedule, Modify while Planning, View | Global (Maintenance) |
| Maintenance Discipline Head / Deputy | `MNT.DisciplineHead` | Accept assignment, assign executors, Hold/Resume, View | Discipline scope (Mechanical/Electrical/Instrumentation/Civil) |
| Senior Maintenance Expert | `MNT.SeniorExpert` | Execute, Record completion, Hold, View | Discipline scope |
| Maintenance Discipline Expert / lower levels | `MNT.Expert` | Execute, record labor/material/completion data, View | Discipline scope |
| Operations issue authority (Operations) | `OPS.IssueAuthority` | Issue/Release, Decline (return/reject at issue), View | Operations |
| Originating-discipline verifier (originating org) | granted per discipline position | Verify completion | Originating discipline scope |
| Originating-discipline final approver | granted per discipline position | Final approval, Close | Originating discipline scope |
| Engineering positions (§3) | existing Engineering roles, **reduced** | View; verify/approve **only as originating discipline** | Originating discipline scope |
| Warehouse | existing/warehouse roles | none in workflow; material issue only | Warehouse |
Role codes are design placeholders; creating them is Phase 3B configuration, not a business decision. Positions map to a role plus an RBAC-ABAC scope (`RoleAssignment.ScopeType/ScopeValue`, already supported on the user-aware `AuthorizeAsync` path). *Creation/issue permission is separate from discipline membership* (§9).

## 9. Work Order responsibility matrix (Baseline + Derived)
R = responsible/authorized to perform; C = may originate/be consulted (cannot decide); I = informed; — = no authority.

| Activity | Engineering | Maintenance Planning | Maintenance Discipline | Operations | Warehouse | Originating Discipline |
|---|---|---|---|---|---|---|
| Create Work Request | C (as originator) | C | C | C | — | **R** (originator) |
| Create Work Order | — | R | R **only with Create authority** (§10) | — | — | R via conversion of its own Work Request (§11) |
| Plan Work Order | — | **R** | C (technical input) | — | — | I |
| Assign responsible discipline | — | **R** | I | — | — | I |
| Assign executors within discipline | — | I | **R** (Discipline Head/Deputy) | — | — | I |
| Issue / Release WO | — | I (request) | I | **R** | — | I |
| Execute WO | — | — | **R** (assigned discipline personnel) | C (coordination on site) | C (material) | — |
| Record completion | — | I | **R** (executor / Senior Expert) | I | I | I |
| Verify completion | — | — | — | I | — | **R** |
| Final approval | — | — | — | — | — | **R** |
| Close WO | — | — | — | — | — | **R** (closure follows final approval) |
| Hold / Resume | — | C | **R** (Discipline Head/Deputy) | C | — | I |
| Cancel | — | R (before issue) | — | — | — | I |
| Reject | — | R (at Planning) | — | R (decline at issue) | — | I |
Manager/Deputy retain cancel/reject **override** authority for any stage (Baseline: Manager/Deputy ownership of the Maintenance organization); an override is always recorded with actor and reason.

## 10. Maintenance Planning rules (Baseline)
- Planning is **mandatory** and always precedes issue/release: `Created → Maintenance Planning → Preparation/Assignment → Operations Issue/Release → Execution`.
- Planning owns: scope/plan/schedule preparation, choosing the **responsible maintenance discipline**, resources/materials/skills planning, and rescheduling before issue. Planning may modify a Work Order only while it is in Planning (or after being returned to Planning by Operations); once issued, changes require a return transition (audited).
- Planning never becomes executor by access: a planner may not execute the Work Order he/she planned (§13).
- **Creation authority:** a Maintenance discipline may create a Work Order only with the explicit *create* authority; discipline membership alone is neither create nor issue authority. Issue authority belongs to Operations only.
- Preventive/Predictive Work Orders generated from a `MaintenancePlan`/`JobPlan` still pass through Planning (the plan is planning input, not a substitute for the stage).

## 11. Originating-discipline rules (Baseline)
- Chain: *Originating Discipline → Work Request → Work Order → Maintenance Planning → Responsible Maintenance Discipline → Execution → Completion → Originating-Discipline Verification → Final Approval/Closure.*
- The originating discipline is **never lost**: at Work Request conversion the Work Order records the requester, the requester's discipline **and its organization domain**; a Work Order created directly records the creator's discipline/domain as originating. (Evidence gap: conversion currently copies only `SourceWorkRequestId`.)
- The originating discipline **verifies completion** and gives **final approval**; closure follows final approval. This is enforced as a workflow governance rule (transition participants + SoD), not a UI label.

## 12. Responsible maintenance discipline rules (Baseline)
- Distinct from the originating discipline; **never overwrite one with the other**. Examples: originating *Process Engineering* → responsible *Mechanical Maintenance*; originating *Electrical Engineering* → responsible *Electrical Maintenance*.
- Set by Maintenance Planning (Preparation/Assignment); validated against the equipment's/plan's discipline where available; executors must belong to that discipline (Team→`MaintenanceGroup.Discipline` / employee discipline); a Discipline Head/Deputy assigns individuals inside their own discipline only.
- Both concepts are stored on the governed Work Order (§20).

## 13. Separation of duties (Baseline + Derived; enforced, not advisory)

| Rule | Decision | Rationale |
|---|---|---|
| Creator vs Executor | **ALLOWED** | creation is not an approval; independent controls are Planning and Operations issue |
| Creator vs Final Approver / Closer | **NOT ALLOWED** | no self-approval |
| Planner vs Executor (same Work Order) | **NOT ALLOWED** | planning must not silently become execution |
| Planner vs Operations issuer | **NOT ALLOWED** | different organizations; issue is an independent control |
| Operations issuer vs Executor | **NOT ALLOWED** | issue/release authority is distinct from maintenance execution |
| Executor vs Verifier | **NOT ALLOWED** | no self-verification of one's own completion |
| Executor vs Final Approver | **NOT ALLOWED** | same |
| Verifier vs Final Approver | **NOT ALLOWED** (same individual) — both belong to the originating discipline, at different positions (verification at Discipline Expert level or above, final approval at supervisor/head level of that discipline) | verification and final approval follow the originating-discipline rule with four-eyes |
| Manager/Deputy override | **ALLOWED** for cancel/reject only; a Manager/Deputy who executed or verified may not give final approval | override cannot be used to self-approve |
Mechanism (Phase 3B): prior-actor sets derived from `WorkflowHistory` per Work Order, evaluated on the user-aware authorization path; violations are rejected without state change, history, or business audit and return a deterministic bilingual message.

## 14. Emergency rules (Baseline)
Emergency keeps its semantics (priority handling, ISO 14224 failure data mandatory at completion — existing Work Order rule). **It is not an authorization bypass.** Mandatory for Emergency: authorization checks, discipline ownership, Maintenance Planning, Operations issue/release, completion verification by the originating discipline, final approval, and all SoD rules of §13. **No emergency exception is defined**: the default is *all controls apply*; a shortcut may only be introduced later as an explicitly documented exception (not needed to start Phase 3B).

## 15. Work Order type matrix (repository types only)
Types in code: **Preventive, Corrective, Emergency, Predictive**. No Shutdown/Turnaround type exists and none is introduced.

| Type | Planning | Operations issue | Execution | Originating-discipline verification | Final approval |
|---|---|---|---|---|---|
| Preventive | Required | Required | Required | Required | Required |
| Corrective | Required | Required | Required | Required | Required |
| Predictive | Required | Required | Required | Required | Required |
| Emergency | Required (no documented exception) | Required (no documented exception) | Required | Required | Required |
Type-specific business rules stay in Work Order code: Corrective/Emergency ISO 14224 fields + `FailureRecord`; Preventive `DueDate`/plan lineage; Permit/LOTO gate at issue where the JobPlan requires it; `MaintenanceHistory` at completion.

## 16. Multi-Discipline Review architecture
**Selected Architecture: A — separate process.** It stays a distinct workflow (`WorkOrderMultiDisciplineReview`) that may relate to the lifecycle but is never a stage of it. Rules: the review may be requested while the Work Order is in Planning, Preparation/Assignment or Execution; a Running review **blocks Completion** (existing behavior kept); **cancelling** the Work Order cancels an open review; **closing** is blocked while a review is Running; findings/history remain on the review's own instance. Rationale (Evidence): optional, discipline-owned, independently versioned, dynamic Parallel participants — a poor fit for a linear lifecycle.

## 17. Workflow instance identity model
- **Current (Evidence):** `IWorkflowRuntime.GetInstanceAsync(type,id)` = newest instance of that `BusinessObjectType` and id, ignoring module/definition; `WorkOrderReviewService` uses it 3 times with type `"WorkOrder"`. Other modules each have their own type and are unaffected.
- **Target:** identity = *(Module, EntityType = process type, EntityId)* + definition + instance, with **distinct process types**: `WorkOrderLifecycle` and `WorkOrderMultiDisciplineReview` (distinct `EntityKey`s). One Running lifecycle instance per Work Order is guaranteed by the existing filtered unique index on binding-started instances; the review is bound too and gets the same guarantee. No "newest by type" lookups for governed Work Orders.
- **Scope:** Work Order scope; no shared-platform change required (the shared `GetInstanceAsync` signature is unchanged; only Work Order consumers change). **Compatibility:** the one existing unbound review instance keeps `BusinessObjectType="WorkOrder"`; its lookups must keep resolving by `(Module=WorkOrder, Category=MultiDisciplineReview)` for legacy records.

## 18. Legacy Work Order strategy (Evidence: `PEMSDB`, read-only)
| ID | Number | Type | Status | From Work Request | Lifecycle instance | Decision |
|---|---|---|---|---|---|---|
| 1 | WO-2026-0001 | Preventive | InProgress | No | none | **legacy** — finishes on the old path; its running review (instance 74, unbound, `DisciplineReview`) continues on its current version and is **not** attached to any new lifecycle |
| 2 | WO-2026-0002 | Corrective | Cancelled | No | none | legacy, stays Cancelled |
| 3 | WO-2026-0003 | Preventive | Closed | No | none | legacy, stays Closed |
No backfill, no status migration, no lifecycle instance creation, "Legacy record" marking in the UI, never assigned a workflow version. Only Work Orders created after the governed workflow is enabled use it.

## 19. Status projection model
Target: **workflow stage = the only transition machine; `WorkOrder.Status` = projection** (like Work Request `BusinessStatus`), retained for reports, dashboards, list filters/KPIs, `RelationshipService` and other consumers. No second controller/service transition machine may remain for governed Work Orders; the legacy `LegalTransitions`/`StatusFlow` remain only for legacy records.

Target lifecycle (existing states become transitions; `Open→Assigned→InProgress→Completed→Closed` is not copied):

| Stage | Entry | Exit actions | Authorized actor | Status projection |
|---|---|---|---|---|
| Created | WO created (originating snapshot stored) | Submit to Planning / Cancel | creator (with create authority), planner | `Open` |
| Maintenance Planning | submitted | Plan complete → Preparation / Reject / Cancel | Planning Experts/Head/Deputy | `Open` |
| Preparation / Assignment | plan approved | Request issue / Return to Planning / Cancel | Planning Head (discipline selection), Discipline Head/Deputy (executors) | `Assigned` |
| Operations Issue / Release | issue requested; Permit/LOTO gate evaluated | Issue → Execution / Decline → Return to Planning or Reject | Operations issue authority | `Assigned` |
| In Execution | issued | Complete / Hold | responsible-discipline personnel | `InProgress` |
| On Hold | held from Execution | Resume → Execution / Cancel | Discipline Head/Deputy; Cancel by Planning Head/Manager | `OnHold` |
| Completion recorded (awaiting verification) | completion data recorded (ISO 14224, cost, `FailureRecord`, `MaintenanceHistory` — Work Order business logic) | Send to verification | executor / Senior Expert | `Completed` |
| Originating-Discipline Verification | completion recorded | Verify → Final Approval / Return → Execution | originating-discipline verifier | `Completed` |
| Final Approval | verified | Approve → Closed / Return → Verification or Execution | originating-discipline final approver | `Completed` |
| Closed (terminal) | approved | — | — (closure follows final approval) | `Closed` |
| Cancelled / Rejected (terminal) | per §9 | — | per §9 (+ Manager/Deputy override) | `Cancelled` / `Rejected` |
Stage/transition names use existing PEMS terminology (Planning, Issue/Release, Execution, Completion, Verification, Final Approval); exact bilingual labels are configuration data (`NameFa`/`LabelFa`), as in Work Request. Existing consumers keep working because every stage maps onto an existing status value.

## 20. Phase 3B implementation requirements (carried gaps)
**Organizational:** no Operations department/role; no Warehouse organization model; incomplete Maintenance role model; Engineering roles hold Work Order authority; Maintenance hierarchy not represented; no organization attribute on `Discipline`/`Employee`.
**Discipline:** Instrumentation seeding mismatch (`"Instrument"` vs `"Instrumentation"`); responsible maintenance discipline not persisted on Work Order.
**Responsibility:** no planner, originating-discipline (with organization domain), verifier or approver fields (or equivalent workflow-history records); Work Request conversion loses the originating discipline.
**Workflow:** lifecycle not on the Workflow Platform; review process unbound; lifecycle/review identity conflict; lifecycle logic in controller/service/views (`StatusFlow`, `LegalTransitions`, `Details.cshtml`, `_WorkOrderStagesDiagram`).
**Authorization:** discipline scope exists but Work Order does not use it; SoD policies seeded but unenforced; `DisciplineEngineer` authority too broad; role-only permission checks.
**Concurrency:** lifecycle loser receives a raw `DbUpdateConcurrencyException` (measured 20/20); one Running lifecycle instance per Work Order must be enforced; deterministic bilingual conflict responses; no duplicate history/instances.
These are implementation requirements for Phase 3B, not reasons to revisit the approved model.

## 21. Remaining unresolved business decisions
**None.** Everything previously marked *BUSINESS DECISION REQUIRED* (D-01…D-22) is now defined by the baseline or derived from it:

| Former item | Resolved by |
|---|---|
| D-01/D-03 positions, roles | §3, §4, §8 |
| D-02 Engineering authority | §3 (originating-discipline authority only) |
| D-04…D-07 Operations | §5, §9 (issue/release authority; decline; on-site coordination) |
| D-08 ownership attributes | §11, §12, §20 (stored originating + responsible discipline; actors from workflow history) |
| D-09/D-10 SoD, emergency | §13, §14 |
| D-11/D-15 chain, verification | §11, §15 (mandatory for all four types) |
| D-12/D-13/D-14 planning, shutdown type, planned-WO changes | §10, §15 (planning mandatory; no new type; Planning modifies/reschedules) |
| D-16/D-17/D-18 review, legacy, identity | §16, §18, §17 |
| D-19 create idempotency | out of scope of this governance baseline (Work Order create is not a workflow-controlled action; handled as ordinary Phase 3B hardening if needed) |
| D-20/D-21 authorization model, status mapping | §13 mechanism + §19 (permission ∧ organization ∧ discipline scope ∧ SoD; stage→status table) |
| D-22 Instrumentation data gap | §7, §20 |
Implementation choices that remain for Phase 3B are configuration/engineering work under the baseline (concrete role names/codes, the per-position assignment of people, exact bilingual labels), not business decisions.

## Phase 3B readiness gate
- [x] Engineering hierarchy defined (§3)
- [x] Maintenance hierarchy defined (§4)
- [x] Operations authority defined (§5)
- [x] Warehouse kept separate (§6)
- [x] Maintenance Planning explicitly defined (§10)
- [x] Mechanical, Electrical, Instrumentation, Civil accounted for (§7; Civil is supported in the current `MaintenanceRole` seed)
- [x] Work Request originating discipline preserved conceptually (§11)
- [x] Responsible Maintenance Discipline distinguished (§12)
- [x] Originating-discipline verification defined (§11, §19)
- [x] Final approval defined (§11, §19)
- [x] Multi-discipline review remains separate (§16, Architecture A)
- [x] Legacy Work Orders remain legacy (§18)
- [x] `WorkOrder.Status` becomes workflow projection (§19)
- [x] Existing four Work Order types preserved (§15)
- [x] No Shutdown/Turnaround type invented (§15)
- [x] SoD governance defined (§13)
- [x] Emergency governance defined (§14)
- [x] Workflow identity separation defined (§17)

**PHASE 3B STATUS = READY**

## Validation
`dotnet build PEMS.slnx --configuration Debug`: 0 errors. Full test suite: result recorded in the final message (documentation-only change). `PEMSDB` and the 3 Work Orders unchanged; `PEMS.Web` was not started.

## Final classification
**READY FOR PHASE 3B**
