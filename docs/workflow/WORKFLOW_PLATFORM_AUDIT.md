# PEMS Workflow Platform — Architecture Audit (no implementation)

Scope: `PEMS.WorkflowPlatform` and every workflow / approval / lifecycle implementation reachable from the repository. Evidence = code that was read and traced (file names below). **Nothing was modified**: no source, schema, migration, seed, UI or data. This document is the only artefact.

---

## A. Complete Workflow Inventory

### A1. Workflows that already run on the central platform (7 seeded definitions)

| # | Module (`Module`/`Category`) | Definition | Stages (type) → actions | Versions in code | Consumer service |
|---|---|---|---|---|---|
| 1 | `EngineeringDocument` / `Approval` | Check → IDC → Approval → Issue | Check (Seq, fixed roles) → IDC (**Parallel, participants injected at runtime as `Discipline:{id}` pseudo-roles**) → Approval (Seq: SystemAdmin, EngineeringManager) → Issue (Seq). Actions `Approve`, `Reject`, `Issue` | **v1, v2, v3 all published from seeder code** (v3: IDC Reject now routes to Approval) | `DocumentWorkflowService` (625 lines) |
| 2 | `VendorDocument` / `Review` | VDR review | DisciplineReview (Parallel, dynamic) → LeadReview (Seq) → ReturnToVendor (Seq). Actions `Approve`, `Reject`, **review codes `AP/NC/AC/CO/RE` used as ActionTypes**, `Issue` | v1 | `VdrWorkflowService` |
| 3 | `MOC` / `ChangeApproval` | Change approval | RiskAssessment (`Assess`) → Approval → SeniorApproval (roles null = dynamic) → Closure (`Close`) | v1 | `MOCService` |
| 4 | `NCR` / `CriticalApproval` | Critical NCR approval | QAApproval (single stage) `Approve`/`Reject` | v1 | (QCController) |
| 5 | `Permit` / `PermitApproval` | Permit-to-work approval | HSEReview → MaintenanceApproval → OperationsApproval | v1 | `PermitService` (+ `PermitAuthorityService`, competency rules) |
| 6 | `TechnicalQuery` / `Response` | TQ/TSR/RFI | PendingResponse (`Respond`) ↔ PendingConfirmation (`Confirm`, `Reopen`) | v1 | `TechnicalQueryService` |
| 7 | `WorkOrder` / `MultiDisciplineReview` | Multi-discipline review sub-flow | DisciplineReview (Parallel, dynamic) `Approve`/`Reject` | v1 | `WorkOrderReviewService` |

### A2. Workflows implemented outside the platform (hand-coded lifecycles)
Nine services declare their own `LegalTransitions` dictionary; more use ad-hoc status checks.

| Module | Statuses / flow | Where the rules live | History store |
|---|---|---|---|
| **Work Request** | Draft → Submitted → UnderReview → Approved → Planned → Converted; Rejected/Cancelled | `WorkRequestService.LegalTransitions` + `WorkRequestController.NextStatusMap` (linear "advance") | `WorkRequestStatusHistory` |
| **Work Order** | Open → Assigned → InProgress ⇄ OnHold → Completed → Closed; Cancelled/Rejected; release gate | `WorkOrderLifecycleService.LegalTransitions`, `WorkOrderReleaseGateService` | `WorkOrderStatusHistory` |
| **Letter (Correspondence)** | Draft → PendingApproval → Approved / ReturnedForRevision / Rejected → Issued → Closed; Registered → Referred | `LetterService` (`RequiredFromStatus` map, owner-only submit rule) | `LetterReview`, `LetterReferral` |
| **Transmittal** | Draft → Sent → Acknowledged; Cancelled → Reissue | `TransmittalService` (inline status checks) | none dedicated |
| Material Requisition | Draft → Submitted → Under Technical Review → Approved → Issued to Procurement → Closed | `MaterialRequisitionService.LegalTransitions` | `ProcurementApproval` |
| Store Requisition | lifecycle service | `StoreRequisitionLifecycleService` | `StoreRequisitionStatusHistory` |
| Permit (lifecycle, not approval) | isolation/gas/SIMOPS gates | `PermitService`, `IsolationService`, `GasTestService`, `SimopsService` (each own `LegalTransitions`) | `PermitStatusHistory`, `IsolationStatusHistory`, `GasTestStatusHistory`, `SimopsConflictStatusHistory` |
| Production Order | Draft → Planned → Released → InProgress ⇄ OnHold → Completed → Closed | `ProductionOrderService.LegalTransitions` | — |
| Shift Handover | Draft → Prepared → Acknowledged → Completed | `ShiftHandoverService.LegalTransitions` | `ShiftHandoverStatusHistory` |
| Referral sub-flows | `DocumentIdcReferral`, `LetterReferral`, `WorkOrderDisciplineReferral` | module tables | own tables |

Not exhaustively traced (no approval semantics found in a first pass): calibration, maintenance plan/strategy activation, criticality assessment review, RCM/FMEA "Approved" states. They should be re-inspected in the migration wave that touches CMMS master data.

---

## B. Workflow Execution Map (traced)

### B1. Engineering Document (platform-backed)
```
EngineeringDocuments/Details.cshtml  (buttons chosen by Model.Status string, lines ~96-185)
 → EngineeringDocumentsController / DocumentWorkflowController (HasPermissionAsync "EngineeringDocuments"/"Approve"; SoD via AccessControlService)
 → DocumentWorkflowService.Submit/Check/Approve/Reject/ReviseAndResubmit/Issue/IdcRespond
      • module pre-checks (status string, revision rules, freeze/hold, client response)
      • IWorkflowRuntime.StartInstanceAsync("EngineeringDocument","Document",id,"Approval",…)
      • IWorkflowRuntime.ExecuteActionAsync(instanceId, "Approve", employeeId, actorRoles, comment, ip, ua, idcParticipantRoles)
      • writes EngineeringDocument.Status back via MapStageToLegacyStatus(stageName)   ← hard-coded stage-name→status map
      • writes legacy DocumentWorkflow row (second history)
 → WorkflowRuntimeService: picks active definition (Module,Category), latest Published version, Start transition, creates WorkflowInstanceStage rows, evaluates parallel strategy, finds transition (from stage + action), inserts WorkflowHistory
 → tables: WorkflowInstances / WorkflowInstanceStages / WorkflowHistories (+ DocumentWorkflows, DocumentIdcReferrals)
```
### B2. Work Request (NOT platform-backed)
```
WorkRequest/Details.cshtml (Advance button label "Advance to {NextStatus}", Convert form when Status=="Planned")
 → WorkRequestController.AdvanceStatus/Reject/Cancel/ConvertToWorkOrder
      permission: module-level HasPermissionAsync("WorkRequest", Create|Approve) — NOT per stage, no assignee
 → WorkRequestService.AdvanceStatusAsync: LegalTransitions check; writes ReviewedBy/ApprovedBy/RejectionReason on the entity
 → WorkRequestStatusHistories (module-specific table)
```
No step timeline exists on the WR page; there is no "Technical Review" step, no reviewer assignment, no versioning.
### B3. Work Order, Letter, Transmittal, MR, others
Same shape as B2: controller (module permission or `[RequirePermission]`) → service with `LegalTransitions`/inline status checks → status column + module history table. Letter adds owner-only submit and `LetterReview` decision rows; WO adds `WorkOrderReleaseGateService` (domain rules) and the platform-backed multi-discipline review sub-flow.
### B3a. Cross-cutting facts about the execution path
- **Instances are looked up by `(BusinessObjectType, BusinessObjectId)` newest-first**; module entities store **no FK** to `WorkflowInstance`.
- **No notifications** are sent from any workflow service (no `INotificationService` use in workflow paths).
- **No SLA / escalation execution** (`WorkflowStage.TimeoutHours`, `IsEscalatable` are stored, never read).
- **Home dashboard** pulls "my tasks" from `IWorkflowRuntime.GetPendingTasksAsync` (role match only).
- `HelpWorkflowDiagramService` already **reads definitions dynamically** to draw Help diagrams (only read-side consumer of definitions besides the runtime).

---

## C. WorkflowPlatform Assessment

Project size: ~1,060 lines (7 entities, `IWorkflowRuntime`, `WorkflowRuntimeService` 520 lines, EF configs). Independent of Persistence via `IWorkflowDbContext` (implemented by `PEMSDbContext`).

| Capability | Status | Evidence / notes |
|---|---|---|
| Workflow Definition | **EXISTS AND REUSABLE** | `WorkflowDefinition` (Name, Category, Module string, IsActive). Unique index (Module, Category, Name). |
| Workflow Version | **EXISTS BUT INCOMPLETE** | `WorkflowVersion` (Draft/Published/Retired, dates, PublishedById). Instances bind `WorkflowVersionId` ✓. **No code enforces immutability** of Published versions and there is **no service to create/clone/publish** — versions are inserted by seeders. |
| States | **EXISTS (as Stage)** | `WorkflowStage`: name, order, Sequential/Parallel, ResponsibleRole (comma string), ApprovalStrategy, mandatory/skippable/delegatable/escalatable flags, Color, Icon, Description. |
| Transitions | **EXISTS BUT INCOMPLETE** | `WorkflowTransition` (from,to,ActionType,Priority). `ConditionExpression` **stored but never evaluated**. First match by Priority wins. |
| Instance / Instance stage / History | **EXISTS AND REUSABLE** | `WorkflowInstance` (status Running/Completed/Rejected/Cancelled/Suspended), `WorkflowInstanceStage` (per-participant row, `RowVersion` concurrency), `WorkflowHistory` (append-only, IP/browser). |
| Approval strategies | **EXISTS BUT INCOMPLETE** | AllApprovalsRequired, FirstResponse, MinimumCount; `MandatoryRolesOnly` = same as All (documented shortcut). |
| Assignment | **EXISTS BUT INCOMPLETE** | Role-only (`AssignedToRole`); `AssignedToEmployeeId` exists on the instance stage but the runtime never sets it; pseudo-roles (`Discipline:{id}`) smuggle dynamic participants. |
| Authorization | **EXISTS BUT INCOMPLETE** | Runtime matches actor roles to `AssignedToRole`; **no permission/SoD/scope inside the platform** — modules do those checks before calling. |
| Conditions | **MISSING (hook only)** | field present, no evaluator. |
| Actions | **EXISTS BUT INCOMPLETE** | Free-string `ActionType`; **no action registry, no "available actions" query** (the UI cannot ask the runtime what it may do). |
| Module / Entity binding | **EXISTS BUT INCOMPLETE** | String `Module` + `BusinessObjectType`; definition chosen by `(Module, Category, IsActive)` with `FirstOrDefault` — **ambiguous if two active definitions share module+category**; **no per-entity-type/project/condition binding**; no unique "one running instance per object" constraint. |
| Notifications | **MISSING** | none. |
| SLA / Escalation | **MISSING (fields only)** | `TimeoutHours`, `IsEscalatable` unused. |
| Cancel / Reopen / Suspend | **MISSING in API** | statuses exist; `IWorkflowRuntime` has no Cancel/Reopen/Reassign/Delegate. TQ "Reopen" is modelled as a *transition*. |
| Runtime read model for UI | **EXISTS BUT INCOMPLETE** | `WorkflowInstanceDto` (current stage name, pending stages) and `GetHistoryAsync`; **no stage list of the version, no per-step state, no allowed actions, no assignee display, no SLA**. |
| Validation of definitions | **MISSING** | nothing prevents unreachable stages, missing start transition, duplicate transitions. |
| Config audit trail | **MISSING** | runtime history exists; **no log of who created/edited/published a definition** (only `CreatedById`, `PublishedById`). |
| Admin / Designer UI | **MISSING** | no controller or view edits workflow tables (`WorkflowDefinitions` etc. appear only in runtime, Help diagram service, and the two stage-diagram consumers). |

---

## D. Duplication Analysis
1. **Nine parallel `LegalTransitions` dictionaries** (WorkRequest, WorkOrder, MaterialRequisition, Permit, Isolation, GasTest, Simops, ProductionOrder, ShiftHandover) — the same "from → allowed next" concept as `WorkflowTransition`.
2. **Eight module status-history tables** duplicate `WorkflowHistory` (`WorkRequestStatusHistory`, `WorkOrderStatusHistory`, `PermitStatusHistory`, `IsolationStatusHistory`, `GasTestStatusHistory`, `SimopsConflictStatusHistory`, `ShiftHandoverStatusHistory`, `StoreRequisitionStatusHistory`) plus `LetterReview`, `ProcurementApproval`, legacy `DocumentWorkflow`.
3. **Dual state for platform-backed modules**: `EngineeringDocument.Status` (string) *and* `WorkflowInstance.CurrentStage`, kept in sync by `MapStageToLegacyStatus` (and the ED history exists in both `WorkflowHistory` and `DocumentWorkflow`).
4. **Three stage-diagram implementations**: shared `_PemsStageDiagram` (stateless), `_WorkOrderStagesDiagram` (hard-coded 5–6 stages), plus per-page code that builds the stage list (ED, VDR) — not driven by the version's stages.
5. **Approval-role checks repeated per module** (controller permission + service role arrays + SoD in `EngineeringDocumentsController`).

---

## E. Hard-Coded UI Workflow Analysis
Counts are lines that compare `Status` to literals or post to a workflow action, measured on the Details views:

| View | Hard-coded status conditions | Workflow action forms | What is hard-coded |
|---|---|---|---|
| `EngineeringDocuments/Details.cshtml` (1,453 lines) | 23 | 8 | stage order + label map (`Submitted→Check`, `IDC Review→IDC`, `Checked→Approval`, `Approved→Issue`, …) at lines ~27-50; whole action block chosen by `Model.Status` (Draft→Submit, Submitted→Check, IDC Review→…, Checked→Approve, Approved→Issue) at ~96-185; IDC tab logic keyed on `"IDC Review"` |
| `VDR/Details.cshtml` | 5 | 2 | `wfStageOrder = Planned, Check, DisciplineReview, LeadReview, ReturnToVendor, Closed` + icons + role labels at lines 16-53 |
| `WorkOrder/Details.cshtml` + `_WorkOrderStagesDiagram.cshtml` | 10 | 7 | stage array/labels/icons for `Open…Closed` (+ `DisciplineReview` when a review is active) |
| `WorkRequest/Details.cshtml` | 6 | 4 | badge class by status, `Status=="Planned"` shows Convert, `NextStatus` from controller `NextStatusMap`, reject/cancel forms |
| `Letter/Details.cshtml` (596 lines) | 13 | 7 | status-conditional Submit/Approve/Return/Reject/Issue/Close blocks |
| `Permit/Details.cshtml` (793 lines) | 32 | 9 | status-conditional approval/isolation/gas/SIMOPS blocks |
| `Transmittal/Details`, `MOC/Details`, `TechnicalQuery/Details`, `QC/NcrDetails`, `ShiftHandover`, `ProductionOrder`, `StoreRequisition` | 4 / 2 / 1 / 4 / 6 / 10 / 3 | 2 / 3 / 3 / 0 / 3 / 5 / 3 | same pattern |
| Index / list pages | many | – | status filter dropdowns and badge colours per module (`WorkRequest/Index` etc.) |

Important: **even the platform-backed modules' UIs do not read the workflow definition** — they reconstruct it from the module's own status string. Adding a stage to the definition today changes the runtime but **not** the ED/VDR timeline, buttons, or the `Status` value (`MapStageToLegacyStatus` falls to `_ => "Submitted"` for unknown stages).

---

## F. Dynamic UI Feasibility
Feasible, because the platform already stores everything the timeline needs (stage order, name, icon, colour, type, responsible role, per-instance participant rows, history). What is missing is a **read model** and a small set of **runtime queries**:

| Needed by UI | Source today | Gap |
|---|---|---|
| Ordered steps of the instance's *own version* | `WorkflowStages` (by `WorkflowVersionId`, `StageOrder`) | no query/DTO |
| Per-step state (done / current / pending / rejected / skipped) | derive from `WorkflowHistory` + `CurrentStageId` | no query/DTO |
| Current step, assignee(s), pending participants | `WorkflowInstanceStage` (role only) | employee assignment unused |
| Available actions for *this user now* | outgoing `WorkflowTransition`s of current stage filtered by role match | **no runtime API**; needs SoD/permission hook |
| History | `GetHistoryAsync` | needs user display names, comment, decision |
| SLA / warnings | `TimeoutHours` | not evaluated |

Per-module approach: (1) Work Request/Order/Letter would show a shared `_WorkflowPanel` partial fed by `WorkflowUiModel`; (2) ED/VDR keep their pages but replace the hard-coded stage arrays and status→button logic by the same model; (3) generic actions (Approve/Reject/Return/Clarify) render from `AvailableActions`; module-specific actions (Convert to WO, Issue with recipient selection, IDC referral picker, permit isolation) stay in module UI and are **declared via an action registry** the definition can reference (name → handler key), never arbitrary code.

For the requested example (insert "Reliability Review" between Technical Review and Supervisor Approval): once WR runs on the platform, publishing a new version changes `WorkflowStages`/`WorkflowTransitions`; the panel and actions update with **no Work Request view change** — provided the module's own persisted `Status` is either derived from the workflow or restricted to coarse business states (see G).

---

## G. Configuration vs Code Boundary

**Configuration-driven (platform):** stage list/order, stage names/icons/colours, responsible role(s)/user(s), parallel strategy, transitions (action → next stage, end), reject/return routing, timeouts/SLA, escalation targets, notification templates & recipients, condition expressions (once an evaluator exists), module/entity/version binding, activation.

**Must remain module code (domain rules):** work-order release gate, permit isolation/gas/SIMOPS/competency checks, ED revision/freeze/hold/client-review rules and numbering, WR→WO conversion (needs equipment, creates a WO atomically), letter numbering and owner-only submit, transmittal recipient acknowledgement, IDC discipline-referral selection UI, SoD/permission decisions (`AccessControlService`), any field-level validation.

**Design consequence:** module entities should keep only *business* status (e.g. Draft / InWorkflow / Approved / Rejected / Converted) and derive the *stage* from the workflow instance; today's fine-grained `Status` strings mix the two.

---

## H. Versioning Strategy
- Already true today: `WorkflowInstance.WorkflowVersionId` pins an instance to its version; new instances take the latest `Published` version by number; `WorkflowHistory` is insert-only; ED shows the pattern (v1→v3 published by seeder, old instances untouched).
- To make it a guarantee: (1) state machine `Draft → Published → Retired`; (2) **immutability**: Published versions reject stage/transition edits (service-level + DB `ON UPDATE` guard via row-version/trigger optional); (3) "New version" = deep clone of stages+transitions into a new Draft; (4) publish validates (see K) and stamps `PublishedById/Date`; (5) runtime resolves *instance* → its version for **all** later decisions (`ExecuteActionAsync` already reads transitions by `instance.WorkflowVersionId` ✓); (6) UI model built from the instance's version, never the latest; (7) optional explicit, audited **migrate-instance** operation (never automatic).
- Result: WR-000101 stays on v1, WR-000201 starts on v2, editing v2 cannot touch v1.

## I. Module Binding Strategy
Replace `(Module, Category)` lookup with an explicit **binding**: `WorkflowBinding(Module, EntityType, Category/Purpose, optional Condition, DefinitionId, IsActive, Priority)` so that e.g. WorkRequest by priority or project can pick different definitions, and "which definition is active for this entity" has exactly one answer. Modules call `StartAsync(definitionKey/binding, entityType, entityId, context)`; the platform returns the instance id; the module stores `WorkflowInstanceId` (nullable FK) on its entity. Constraint: at most one **Running** instance per `(BusinessObjectType, BusinessObjectId, purpose)` (filtered unique index) — currently only a non-unique index exists.

## J. Database Impact (additive only)
Existing tables are reusable as-is: WorkflowDefinitions, WorkflowVersions, WorkflowStages, WorkflowTransitions, WorkflowInstances, WorkflowInstanceStages, WorkflowHistories (all FK `Restrict`, history append-only).
Additive candidates (each justified by a gap in C):
1. `WorkflowBindings` (module/entity/purpose/condition → definition).
2. `WorkflowConfigAudit` (who/when/what: created, edited, cloned, published, activated, deactivated, binding changed) — separate from runtime history.
3. `WorkflowStage`: `AssignedUserId` / `AssignmentMode`, `NotifyTemplateKey`, `EscalateToRole`, `EscalationHours` (columns, nullable).
4. `WorkflowTransition`: `ActionLabelKey`, `RequiresComment`, `HandlerKey` (registry key), `IsReturn`/`Kind` (Approve|Reject|Return|Clarify|Custom).
5. `WorkflowVersion`: `ContentHash` / `IsLocked` (immutability), `ClonedFromVersionId`.
6. `WorkflowInstance`: filtered unique index for one running instance per object; `SlaDueDate`.
7. Module entities: nullable `WorkflowInstanceId` (WR, WO, Letter, Transmittal, …) — replaces "latest instance by object id" lookups.
8. Notification/SLA job tables only if the existing notification service cannot be reused.
No column removals, no data rewrites; legacy history tables stay readable.

## K. Workflow Validation (pre-publish rules)
Exactly one start transition; every stage reachable from start; every non-terminal stage has ≥1 outgoing transition; no duplicate (from, action, condition); each stage has a responsible role/user or is a declared dynamic stage; roles must exist in `AspNetRoles`/`SystemRoles`; users must map to active employees; actions must exist in the controlled registry; conditions must parse in the safe evaluator; terminal states reachable on every path; Parallel stages have a valid strategy (`MinimumCount` ≥ 1 ≤ participants); binding target module/entity known. The runtime should re-validate (defence in depth). Note today's platform performs none of this.

## L. Security Impact
Current pattern: controller `HasPermissionAsync(module, action)` (+ SoD via `AccessControlService.AuthorizeAsync`) **before** calling the runtime; runtime then matches role strings. Findings: (1) Work Request/Order/Letter check only module-level permission — not "am I the assigned reviewer of *this* step"; (2) runtime trusts the caller-supplied `actorRoles`; (3) ED dynamic IDC uses fabricated `Discipline:{id}` roles built by the module; (4) many Engineering Documents actions lack server-side checks (Phase 2 audit) — a central runtime check would close this class. Target: `ExecuteAsync` re-derives roles from the authenticated principal (never from a parameter), checks stage assignee, then calls a **pluggable authorization hook** (permission + SoD + scope) — no hard-coded user/role ids, no bypass. Designer/publish/activate endpoints need their own permission (`WorkflowAdmin`) and SoD (author ≠ publisher recommended).

## M. Migration Plan (module by module; never all at once)
Per module: audit → map to definition → create v1 (Draft) → validate → shadow-run/compare → activate → cut UI.
Wave 0 (platform hardening, no module change): version state machine + immutability, validator, clone/publish service, config audit, runtime read model (`WorkflowUiModel`, `GetAvailableActions`), `WorkflowBinding`, action registry, unique running-instance index, unit tests.
Wave 1: **Work Request** (simplest; 8 statuses; user's motivating example) — stages Submitted → Technical Review → Supervisor Approval → Approved → Planned → Converted; Convert-to-WO stays module code triggered as a registry action. Keep `WorkRequestStatusHistory` read-only for old rows; backfill instances only for *active* requests, mapping status→stage; closed ones stay legacy (no synthetic history).
Wave 2: Letter (approval part only; referrals stay), Transmittal (Sent/Acknowledged is recipient-driven → keep partly module).
Wave 3: Work Order lifecycle (keep release gate + labor/spare rules as pre-transition validators); unify with existing multi-discipline review.
Wave 4: ED & VDR — already on the platform: only switch UI to the model and replace `MapStageToLegacyStatus`; keep dual history until UI is confirmed; highest UI risk (1,453-line view).
Wave 5: MR/Store Requisition, Permit lifecycle, Production Order, Shift Handover, Isolation/GasTest/Simops (small state machines; some may deliberately stay code — e.g. GasTest Passed→Invalidated is a data rule, not a workflow).
Existing instances: continue on their pinned version; status/history untouched; new binding applies only to newly started instances.

## N. Testing Plan
- **Definition:** create, edit Draft, clone → new Draft, publish (valid/invalid fixtures for every rule in K), activate/deactivate, published-version edit rejected.
- **Runtime:** start (binding resolution incl. ambiguous/missing), transition, approve/reject/return/cancel/reopen, parallel strategies, auto-skip, concurrency (`RowVersion`), one-running-instance guard.
- **Versioning:** instance A on v1, publish v2, new instance B on v2, A still shows v1 steps and executes v1 transitions; editing v2 leaves v1 rows byte-identical.
- **UI model:** given a definition with N stages the model contains N steps in order; inserting a stage in v2 changes B's model and not A's; `AvailableActions` differs by role/assignee; module business fields untouched.
- **Security:** unauthorised user cannot execute a configured transition (403 + no history row); actor roles are taken from the principal; designer endpoints require `WorkflowAdmin`; SoD author≠approver.
- **Audit:** every config change writes `WorkflowConfigAudit`; runtime history unchanged in shape.
- **Regression:** existing ED/VDR/MOC/TQ/Permit tests (`EngineeringWorkflowTests`, `HoldBlocksWorkflowTests`, `FirstPassApprovalPrototypeTests`, `DocumentWorkflowControllerApprovalAuthorizationTests`, `VDRControllerAuthorizationTests`, `LetterControlledLifecycleTests`, `FakeWorkflowRuntime` support) must stay green in every wave; add a "same-behaviour" characterization test per module before its cut-over.

## O-1. Risk Register
| Risk | Type | Severity | Mitigation |
|---|---|---|---|
| Module `Status` string diverges from workflow stage (ED `MapStageToLegacyStatus`) | Architecture/UI | High | derive stage from instance; keep coarse business status |
| Editing a Published version silently changes live behaviour | Data | High | enforce immutability + clone-to-edit |
| Ambiguous definition lookup (`FirstOrDefault` on module+category) | Architecture | High | explicit binding + uniqueness |
| Re-implementing WR/WO on the platform loses domain rules | Migration | High | rules stay as pre/post-transition validators (registry), characterization tests first |
| Big-bang migration | Migration | High | wave plan; shadow mode; per-module feature switch |
| Designer allows unsafe workflows / code execution | Security | High | validator + controlled action registry + safe expression evaluator, no eval |
| Runtime trusts caller-supplied roles | Security | Medium | derive from principal; assignee check; auth hook |
| ED page (1,453 lines) regression | UI | Medium | migrate last; component-by-component; visual QA in EN/FA |
| Old instances after definition changes | Compatibility | Medium | pinned versions; explicit audited migrate-instance only |
| No notifications/SLA today → users expect them once "configurable" | Product | Medium | ship Wave 0 with notification hook, SLA optional |
| Two histories (legacy + platform) drift | Data | Medium | freeze legacy tables (read-only), single write path |
| Performance of per-request stage queries | Runtime | Low | version graph cache (immutable ⇒ cacheable) |

## O-2. Final Recommendation
1. **Do not build a second engine.** `PEMS.WorkflowPlatform` is a sound runtime core (versions pinned to instances, append-only history, per-participant stage rows, parallel strategies) and already runs seven definitions.
2. **Finish the platform before migrating modules (Wave 0):** version lifecycle + immutability, clone/publish, validator, `WorkflowBinding`, config audit, runtime read model + `GetAvailableActions`, controlled action registry, principal-derived authorization hook, unique running-instance constraint. These are additive.
3. **Then migrate one module at a time, Work Request first** (it is the smallest, has no platform dependency, and matches the requested example); prove the dynamic panel there, then extend to ED/VDR UIs (which are already engine-backed but not engine-driven).
4. **Keep domain rules in module code** behind registry-declared pre/post validators; keep module business fields out of the dynamic UI — only the workflow panel (state, timeline, actions, assignees, history, SLA) is model-driven.
5. **Designer last** (after validator + audit): draft editing, clone, validate, publish, bind, activate, with `WorkflowAdmin` permission and SoD.

## Implementation gate
Stopped here as instructed. No code, schema, migration, UI or data was changed. Implementation needs separate authorization; recommended first authorization = **Wave 0 (platform hardening) only**.
