# Workflow Platform — Phase 3: Work Order Audit

**Classification: BLOCKED** (audit complete; implementation intentionally not started — hard-stop rules 3, 4 and 6 of the phase charter were triggered, §K/§M).
No production code, schema, seed or data was changed in this phase. Two temporary probes were run against throw-away databases and removed.

## A. Executive summary

**The Work Order lifecycle is not on the Workflow Platform.** Work Order uses the platform in exactly one place: an optional **multi-discipline review sub-process** (`Module=WorkOrder`, `Category=MultiDisciplineReview`, one Parallel stage, dynamic `Discipline:{id}` participants, started through the *legacy* path — no binding, no version pinning policy beyond "latest Published"). The main lifecycle **Open → Assigned → InProgress → Completed → Closed (+ OnHold, Cancelled, Rejected)** is a hard-coded state machine in three places (controller `StatusFlow`, `WorkOrderLifecycleService.LegalTransitions`, Razor `Details.cshtml`/`_WorkOrderStagesDiagram`) with module-level permissions only.

Beyond the workflow question, the audit found that **the maintenance organization described in the charter is not represented in the repository** (§F), so migrating the lifecycle would either copy today's behavior (which conflicts with that organization) or require role/stage decisions that must not be invented. Per the charter's hard-stop rules, work stopped after the audit.

Verified baseline: `dotnet build PEMS.slnx --configuration Debug` → 0 errors (21 warnings); full suite **1704 passed / 0 failed / 0 skipped**; `PEMS.Web` not running; `PEMSDB` untouched.

## B. Before / after architecture

```
BEFORE (current, unchanged)
WorkOrder/Details.cshtml  (status switches, StageDiagram(Status, hasPendingReview), button rules)
        ↓ POST AdvanceStatus/Hold/Resume/Cancel/Reject
WorkOrderController       (StatusFlow[], GetNextStatus, per-action HasPermission("WorkOrder", View|Edit|Approve))
        ↓
WorkOrderLifecycleService (LegalTransitions{}, ReleaseGate(Permit/LOTO/Gas) on InProgress,
                           ISO 14224 fields + FailureRecord + MaintenanceHistory on Completed)
        ↓ one SaveChanges (RowVersion protected) → WorkOrders + WorkOrderStatusHistories (business audit only)

Side process (only platform use):
RequestDisciplineReview → WorkOrderReviewService → WorkflowRuntime.StartInstance("WorkOrder","WorkOrder",id,"MultiDisciplineReview")
        → Parallel stage "DisciplineReview" → Approve/Comment/Reject → WorkflowHistory + WorkOrderDisciplineFindings
```
"After" was not produced (blocked). A proposed target is in §M.

## C. State-machine audit (current)

| # | From → To | UI action | Endpoint / service | Permission | Role/discipline restriction | Business rule | Platform transition | WF history | Business audit | Concurrency |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Open → Assigned | "Advance to Assigned" (+team/employee) | `AdvanceStatus` → `AdvanceStatusAsync` | WorkOrder.Edit | none | sets AssignedTeam/Employee | NO | NO | YES `WorkOrderStatusHistory` | RowVersion (row); **loser gets raw `DbUpdateConcurrencyException`** |
| 2 | Assigned → InProgress | "Advance to InProgress" | same | Edit | none | **Release gate**: Permit Active / LOTO verified when JobPlan requires; sets StartDate | NO | NO | YES | same |
| 3 | InProgress → Completed | "Complete" form | same | Edit | none | blocked while a *Running* discipline review exists; ISO 14224 fields (Corrective/Emergency); ContractorCost; creates `FailureRecord`, `MaintenanceHistory` | NO | NO | YES | same |
| 4 | Completed → Closed | "Advance to Closed" | same | **Approve** (controller) | none | — | NO | NO | YES | same |
| 5 | Assigned/InProgress → OnHold | Hold | `Hold` | Edit | none | — | NO | NO | YES | same |
| 6 | OnHold → InProgress | Resume | `Resume` | Edit | none | release gate again | NO | NO | YES | same |
| 7 | Open/Assigned/InProgress/OnHold → Cancelled | Cancel | `Cancel` | **Approve** | none | — | NO | NO | YES | same |
| 8 | Open → Rejected | Reject | `Reject` | Approve | none | — | NO | NO | YES | same |
| 9 | (any, no Running review) → discipline review Running | "Request review" | `RequestDisciplineReview` → `WorkOrderReviewService` | Edit | none | replaces previous referrals/findings (deletes them) | **YES** (Start) | YES | findings table | instance-stage RowVersion; start not covered by the unique index (legacy path) |
| 10 | review stage → Approve/Reject/Comment | "Respond" | `RespondDisciplineReview` | **View** | user's employee discipline must be referred | writes `WorkOrderDisciplineFinding` | YES | YES | findings | RowVersion |

Notes: the controller's `AdvanceStatus` uses `GetNextStatus` (linear), so OnHold has no "next" (Resume only); `Rejected/Cancelled/Closed` are terminal. A *Rejected* review does not block completion (only a *Running* one does). Workflow state (review instance) and business status (`Status` column) are independent today; `Status` is consumed by list filters/KPI counts (`Index`), `RelationshipService`, dashboards.

## D. Authorization matrix (current, from code + `PEMSDB`)

| Actor (Identity role) | View | Create | Edit (advance, hold, resume, request review, tasks, labor, spares) | Approve (Close, Cancel, Reject) |
|---|---|---|---|---|
| MaintenanceEngineer | ✔ | ✔ | ✔ | ✔ |
| EngineeringSupervisor | ✔ | ✔ | ✔ | ✔ |
| EngineeringManager | ✔ | ✔ | ✔ (+Delete) | ✔ |
| DisciplineEngineer | ✔ | ✘ | ✘ | **✔ (can Close / Cancel / Reject any Work Order)** |
| Viewer | ✔ | ✘ | ✘ | ✘ |
| custom role `"maintenance planning "` (trailing space, created by an administrator) | no WorkOrder permission row | — | — | — |

Server-side: every endpoint calls `IPermissionService.HasPermissionAsync(roles,"WorkOrder",action)` (role-only; parity with `AccessControlService.AuthorizeForRolesAsync`). Roles come from the authenticated session, so a *forged role* over HTTP is not possible; but the check is **module-level only**:
- no discipline ownership check on advance/complete/close (any Edit holder can act on any Work Order);
- SoD policies `WorkOrder.Create↔Close` and `WorkOrder.Edit↔Close` are seeded (`AccessControlSeeder`) but `WorkOrderController` never calls `AuthorizeAsync` (0 occurrences) → **not enforced; one MaintenanceEngineer can create, run, complete and close the same Work Order alone**;
- no Operations role, no planning step, no originating-discipline verification anywhere.

## E. Concurrency results (measured now, throw-away SQL Server, forced barrier before first `SaveChanges`)

- Lifecycle, two simultaneous identical `AdvanceStatus(Open→Assigned)`, 20 iterations: **0 double successes; history exactly 1 row in 20/20** (RowVersion protects the data) — **but the losing caller receives a raw `DbUpdateConcurrencyException` in 20/20** (no deterministic bilingual message; in the web layer this is an unhandled error). Gap (workflow-action concurrency handling).
- Not measured (would follow the Work Request methodology once the design is approved): simultaneous discipline-review *start* (legacy path, `WorkflowBindingId` null → excluded from the filtered unique index by construction, so duplicate Running review instances are theoretically possible; the service only does an in-memory "already running" check); HTTP-level double POST; Work Order *creation* (controller `Create` has no idempotency token — two POSTs create two Work Orders with different numbers, by design of the current form).
- The existing filtered unique index `UX_WorkflowInstances_Binding_EntityKey_Running` covers only **binding-started** instances; it does **not** cover Work Order today (no binding exists).

## F. Maintenance-organization audit (charter §8)

Found in the repository / `PEMSDB`:
- Identity roles (10): SystemAdmin, EngineeringManager, EngineeringSupervisor, DisciplineEngineer, DocumentController, MaintenanceEngineer, ContractManager, QAManager, Viewer + the ad-hoc `"maintenance planning "`.
- `MaintenanceRole` (9 rows: per discipline *Technician / Senior Technician / Crew Leader*) — a crew/skill model, not authority roles.
- Disciplines present: Mechanical, Electrical, **Instrumentation**, Civil, Inspection, Piping, Process, Structural, HSE, Fire & Gas, Corrosion, Reliability, Utilities, Architecture, Other (+ many test-pollution rows in `PEMSDB` created by the test suite).
- **Not represented:** Maintenance Manager / Deputy, Maintenance Planning Head / Deputy / Experts, Maintenance Discipline Head / Deputy, Senior Maintenance Expert as authority levels, Operations as a Work-Order participant, Warehouse organization for Work Order material flows.
- The Engineering organization (EngineeringManager/Supervisor/DisciplineEngineer) currently holds Work Order authority — exactly the Engineering/Maintenance confusion the charter warns about.
- Discipline lookup for a user = `Employee.DisciplineId` (a single discipline); there is no maintenance-organizational scope. The RBAC-ABAC layer (`RoleAssignment` scopes) exists and could carry discipline scope but is not used by Work Order.

## G. Work Request → Work Order responsibility chain (actual behavior)

| Question | Current behavior |
|---|---|
| Who can create a WO? | `WorkOrder.Create` holders directly (controller `Create`, comment in code: direct path control is "a later phase"); or Work Request conversion (`WorkRequest.Approve`) — creates `Status=Open`, type chosen at conversion, `SourceWorkRequestId` set. |
| Maintenance Planning | No step. Plan link (`MaintenancePlanId`) and JobPlan exist as data only. |
| Discipline assignment | Optional at `Open→Assigned` (team/employee free choice; no check that assignee's discipline owns the equipment/WR). |
| Who issues/releases? | `Assigned→InProgress` by any `Edit` holder, subject to the Permit/LOTO gate. No Operations authority. |
| Who executes/completes? | Any `Edit` holder. |
| Who verifies completion? | Nobody; no originating-discipline verification. |
| Final approval | `Completed→Closed` needs `Approve` (incl. DisciplineEngineer); no SoD enforcement. |
| Originating discipline / Operations | `SourceWorkRequestId` exists but is not used by any WO authorization or stage. |
| Direct bypass | Not possible with a forged role (session roles) — but a coarse permission suffices to bypass every intended step. |

## H. Hard-coded workflow logic inventory

| Where | Logic | Class |
|---|---|---|
| `WorkOrderController.StatusFlow`, `GetNextStatus` | linear order | A (must become platform) |
| `WorkOrderController.AdvanceStatus` | Closed needs Approve; Completed blocked by pending review | A/B |
| `WorkOrderLifecycleService.LegalTransitions` | transition table | A (duplicated with StatusFlow) |
| `WorkOrderLifecycleService` InProgress gate | Permit/LOTO release gate (`WorkOrderReleaseGateService`) | **B** (Work Order/maintenance rule) |
| Completed: ISO 14224 fields, `FailureRecord`, `MaintenanceHistory`, contractor cost | | **B** |
| `Details.cshtml` (lines ~13–160) | badge colour, "Advance to …", Hold/Resume/Cancel/Reject visibility by `Status` | A |
| `_WorkOrderStagesDiagram.cshtml` | static stage list from `Status` | A (a third, independent stage diagram implementation) |
| `Index.cshtml` / KPI counts | status filters and counts on the `Status` column | C (consumers; must keep working) |
| `RelationshipService`, reports | read `Status` | C |
Nothing was removed.

## I. Legacy data strategy (proposal; no data changed)

`PEMSDB` today: **3 Work Orders** (Cancelled 1, Closed 1, InProgress 1); none from a Work Request; **0** Work Order bindings; **1 Running** legacy discipline-review instance (`WorkOrder/MultiDisciplineReview`, unbound). Classification: all 3 = *Legacy / no lifecycle instance*; 0 governed; 0 broken/ambiguous found. Proposed behavior after a future migration (same as Work Request): legacy records keep the old path/statuses, are marked "Legacy record", are never assigned a workflow version, no backfill. Not implemented.

## J. Database changes / tests / browser QA
- Database: none. Migrations: none.
- Tests: baseline 1704 passed / 0 failed / 0 skipped; no tests added or changed (nothing implemented).
- Concurrency evidence: the lifecycle probe in §E (temporary test, removed; throw-away database dropped).
- Browser QA: not performed (no UI changes).
- Help: not changed.

## K. Why the phase is BLOCKED (charter §25)

1. **(Rule 4) Existing behavior conflicts with the intended maintenance organization.** Engineering roles hold Work Order authority; DisciplineEngineer can close/cancel/reject; SoD is seeded but unenforced; Planning, Operations issuance, originating-discipline verification and Maintenance Head/Deputy roles do not exist.
2. **(Rule 3) Ambiguous semantics for the target workflow.** The intended chain (Planning → discipline assignment → issuance/release → execution → completion → originating-discipline verification → final approval) is not the current lifecycle; which stages/roles/permissions exist, which are mandatory per Work Order type, and where Operations participates are business decisions that the charter forbids me to invent ("do not solve these questions by inventing new roles").
3. **(Rule 6) A business rule cannot be safely preserved unchanged** if the intended organization is implemented: enforcing SoD/discipline ownership changes who can currently close or run a Work Order (DisciplineEngineer, EngineeringManager/Supervisor).
4. Two workflows would coexist on one entity (lifecycle + review). `IWorkflowRuntime.GetInstanceAsync(type,id)` (used by `WorkOrderReviewService` for start/respond/pending checks) returns the **newest instance of that `BusinessObjectType`, ignoring module/definition** — a lifecycle instance for `WorkOrder` would be picked up by the review service. This is solvable inside Work Order scope (distinct `EntityType` keys, review service querying by definition) and does **not** require a platform change, but it shows the migration is not a mechanical copy.

Not triggered: no shared-platform architectural change is required (rule 1/8), no mass backfill (rule 2), no destructive migration (rule 5), no other module must change (rule 7).

## L. Known limitations of this audit
- Concurrency was measured only for the lifecycle advance (§E); creation, HTTP-level and review-start races were analysed from code, not measured.
- `PEMSDB` contains many test-generated disciplines (pre-existing pollution from the test suite); the real discipline list in §F was read ignoring them.
- The work of the previous gate (Work Request concurrency: filtered unique index migration, `WorkflowConcurrencyConflictException`, tests, docs) is still **uncommitted** in the working tree.

## M. Decisions required, and proposed path (for your authorization)

**D1 — Organization/role model.** Name the Work Order authority roles and map them to existing Identity roles or new ones: Maintenance Manager/Deputy, Planning Head/Deputy/Experts, Discipline Head/Deputy (per Mechanical/Electrical/Instrumentation/…), Senior Expert, Operations (issuance), originating-discipline verifier. Confirm whether discipline scope should use the existing RBAC-ABAC `RoleAssignment` scopes (recommended: reuse) and that Engineering roles lose Work Order authority (or keep it explicitly).
**D2 — Target stages.** Confirm the exact stage list and mandatory steps by Work Order type (e.g. is Operations issuance required for all types or only when a Permit/LOTO is required; is originating-discipline verification mandatory for Work-Request-originated orders only), plus OnHold / Cancel / Reject semantics.
**D3 — Review sub-process.** Recommended: keep it a separate definition and give the lifecycle its own `EntityType` (e.g. `WorkOrder` vs `WorkOrderReview`), with the review service keyed by definition. Alternative: fold it into the lifecycle as a Parallel stage.
**D4 — Legacy.** Recommended: the 3 existing records stay legacy (no backfill).
**D5 — Behavior changes.** Approve enforcing SoD and discipline ownership (changes who can currently close/run a Work Order).

**Option A (available immediately, behavior-preserving):** mirror today's statuses and module-level permissions as a published definition + binding, runtime-driven UI, business rules (release gate, ISO 14224, FailureRecord, MaintenanceHistory) kept in the service, dynamic-stage proof, concurrency tests, browser QA. It would *not* fix §F/§G and would codify Engineering roles as Work Order authority — not recommended before D1/D2.
**Option B (recommended):** answer D1–D5, then implement the migration with the intended chain, following the Work Request reference (binding, read model, hook permissions per transition, SoD via `ForbidInitiator`/hook, conflict-safe actions, bilingual messages, filtered unique index by binding, legacy fallback, Help, browser QA).

Also independent of the decision, one small, low-risk fix is already justified by §E: catch `DbUpdateConcurrencyException` in `WorkOrderLifecycleService` and return a deterministic bilingual conflict message — awaiting authorization because it changes Work Order code.

**Final classification: BLOCKED** — waiting for D1–D5 (or an explicit choice of Option A).
