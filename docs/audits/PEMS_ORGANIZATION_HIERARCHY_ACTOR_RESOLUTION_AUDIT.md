# PEMS — Organization Hierarchy & Workflow Actor Resolution: Phase 0 Audit

Status: **PHASE 0 ONLY — no code, schema or data changed.** Evidence from repository code and read-only SQL on the dev database (`PEMSDB`, 2026-10-08). Implementation is **stopped at a decision gate** (section 11) because stop conditions 10 and 12 of the directive are met: the repository holds **no data from which any reporting line can be inferred**.

## 1. Executive summary / root cause

1. **There is no authoritative reporting line anywhere in PEMS.** `Employee` has no manager field by explicit design ("manager is derived from the team supervisor", comment in `Employee.cs` / `EmployeeController`). `Position` has Team/Department but **no parent**; departments are flat; `Team.SupervisorEmployeeId` is operational supervision of a maintenance team.
2. **The org model exists but is empty.** Dev data: 63 departments (0 with a parent), 53 employees (49 active; **0** with `PositionId`; 2 with a free-text `Position`), **0 teams, 0 team members, 0 real positions** (6 inactive test rows I created earlier), 6 inactive test assignments.
3. **The "direct manager" that Part Catalogue uses today is a module-specific table I added** (`PartCatalogueOrgLinks`, employee → manager) because nothing authoritative existed. That is exactly the anti-pattern the directive forbids; it is now empty (0 rows), so *every* Part Catalogue submission has no resolvable direct manager. This is the root cause of "an employee can create a request but the workflow cannot find the manager".
4. **The workflow side is already centralized and correct to extend**: `EnterpriseActorResolver` (+ `ActorResolutionWorkflowHook`, candidate snapshots in `WorkflowInstanceStageCandidate`, JSON resolution details in `WorkflowInstanceStage.ResolutionDetails`, fail-closed, no admin fallback). It must be **extended**, not duplicated: only its `RequesterManager` source is wrong (reads the module table instead of an organization hierarchy).

## 2. AUDIT 1 — Organization / data model

| Question (directive §1) | Evidence |
|---|---|
| What represents an employee's position? | `Employee.Position` (free text, legacy) and `Employee.PositionId` → `Position` (added by the RBAC-ABAC doc, "optional link"); **both unused in practice** (0 / 2 rows) |
| Is Position authoritative? | No. Descriptive; no workflow/authorization code reads it (only `OrganizationStructureService`, `TeamController`, my resolver) |
| Position without employee / vacant? | **Yes**. `EmployeeAssignment` is separate; `PositionDto.IsVacant => CurrentAssignment == null`; `RemoveAssignmentAsync` returns the position to VACANT |
| Position parent? | **No** (`Position`: Title, Description, DepartmentId?, TeamId?, SortOrder, IsActive) |
| Multiple positions per employee? | Allowed (no unique index on employee) |
| Primary position? | **No** concept |
| Effective dates? | Partial: `EmployeeAssignment.StartDate/EndDate/IsActive`; index `IX_EmployeeAssignments_PositionId_ActiveOnly` = **at most one active holder per position** (filtered unique) |
| Acting/deputy? | **None** (the unique-holder index also forbids a second active assignee) |
| Team Supervisor used as manager? | Only for **display** on the employee profile (history of team membership). No routing/authorization uses it |
| Supervisor = operational or line? | Operational: `Team` belongs to a `MaintenanceGroup` (+ `Shift`, `NodeKind`, `ParentTeamId`), i.e. maintenance workforce structure |
| Departments hierarchical? | Column `ParentDepartmentId` exists; **0 of 63 rows use it**. `OrganizationDomain` (string) tags 3 departments and drives Maintenance/Operations *domain* checks in Work Orders, not reporting |
| Positions hierarchical? | No |
| Multiple holders per position? | No (index) |
| Multiple active assignments per employee? | Yes (no constraint, no primary marker) |
| Source of truth today | None for reporting lines. Teams (`ParentTeamId`) form a maintenance tree; `PartCatalogueOrgLinks` (module-local, empty) |

Org services/UI that exist and must be reused: `IOrganizationStructureService` (groups, team nodes, positions, assign/remove/move employee, `GetOrganizationTreeAsync`), `TeamController` (create/update/move/deactivate position, assign employee, remove assignment) — i.e. an **organization administration UI already exists** and edits Position/EmployeeAssignment.

## 3. AUDIT 2 — Authorization / workflow

Trace of "who performs a workflow action":

```
WorkflowDefinition → WorkflowStage (ResponsibleRole | ResponsibleEmployeeId | ResponsibilityKey+EntryCondition)
  → runtime EnterStageAsync → IWorkflowActorResolutionHook (ActorResolutionWorkflowHook)
  → IEnterpriseActorResolver (kinds: AssignmentPool | PositionScope | RequesterManager | CommodityPosition)
  → WorkflowInstanceStageCandidate snapshot → ExecuteActionAsync (candidate only, initiator denied, no role/admin bypass)
  → transition RequiredPermission via AccessControlWorkflowAuthorizationHook (RBAC/RoleModuleAction) → WorkflowHistory + AuditLog
```

Findings:
- **A centralized actor resolver already exists** (built earlier this project) — extend it. The platform engine itself stays role-string based for legacy stages.
- Other workflows resolve actors by **roles** (`ResponsibleRole`, e.g. MOC, VDR, Engineering Documents, Work Order governance seeds) — none by organization; none hard-code employees (`ResponsibleEmployeeId` seeded: 0 rows).
- Delegation: `RoleAssignment.DelegatedByUserId` / `AssignmentType` and the `IsDelegatable` stage flag exist, but **no runtime delegation/acting mechanism** is wired to actor resolution.
- No admin fallback in the resolver (verified by tests: `REQUESTER_ONLY`, `NO_ELIGIBLE_ACTOR`…).
- Duplication found: manager logic exists only in Part Catalogue (`PartCatalogueOrgLinks`, `PartCatalogueRoutingPlanner` legacy, resolver `RequesterManager`).

## 4. AUDIT 3 — Real Part Catalogue end to end

Actual chain: `PartCatalogueRequestController.Edit → PartCatalogueService.SubmitAsync → PreviewRouteAsync → EnterpriseActorResolver(DirectManager: Kind=RequesterManager, level 1) → IWorkflowRuntime.StartInstanceAsync → WorkflowInstanceStage(+candidates) → Details page / Execute → runtime ExecuteActionAsync → PartCatalogueRequestApproval + AuditLog`. Permissions: `PartCatalogue.Create/Review/Approve/Admin` (RoleModuleAction). Live-verified earlier with real users.

Scenarios A–J against the **current** implementation:

| | Scenario | Result today |
|---|---|---|
| A/B/C | Operations / Engineering / Maintenance employee submits | `Pc_Route_*` failure (`NO_ELIGIBLE_ACTOR`, detail `NoDirectManager`) — no link row, so nothing resolves; admin sees the state, user sees the safe message |
| D | Manager submits | Only skip rules exist (requester is CEO / manager is CEO / self); a normal manager follows the same lookup of *their own* link. No decision recorded for "top of hierarchy" → **business decision D4** |
| E | No valid position | Position is not consulted at all (module link only) |
| F | Manager position vacant | Not representable (links are employee→employee) |
| G | Manager inactive | `INACTIVE_ACTOR` (candidate filter) — works |
| H | Circular hierarchy | `A→B→A` links are rejected by `SetOrgLink` (cycle check) |
| I | Multiple active primary positions | No such concept |
| J | Acting manager | Not supported |

## 5. Reconciliation

| Area | Organization | Workflow | Part Catalogue | Root cause |
|---|---|---|---|---|
| Employee → Position | link exists, unused/empty, no primary | not consulted | not consulted | position never made authoritative |
| Position hierarchy | none | resolver cannot walk one | module table instead | **missing `ParentPosition`** |
| Manager resolution | derived "team supervisor" for display | `RequesterManager` reads module table | empty module table → fails | reporting line modeled outside the organization |
| Roles | roles are separate from positions | role/permission checked after actor resolution | PartCatalogue roles/actions fine | OK — keep Position ≠ Role ≠ Permission |
| Permissions | — | `AccessControlWorkflowAuthorizationHook` + candidate permission filter | enforced | OK |
| Workflow actor | — | central resolver + snapshot exist | uses it | OK, extend |
| Missing data | 0 positions/holders/hierarchy | fails closed correctly | submissions blocked | **no organization data was ever entered** |

Root cause (two parts): (1) the **architecture lacks a position hierarchy**, so the only way to express a manager was a module-local table; (2) **the organization data itself does not exist**, so even a perfect resolver has nothing to resolve.

## 6. Reusable components (do not rebuild)

`Position`, `EmployeeAssignment`, `IOrganizationStructureService`, `TeamController` org UI, `EnterpriseActorResolver` + hook + snapshot tables, `IRoleModuleActionService`/AccessControl, `AuditLog` (automatic before/after), Workflow history, Help seeder (`HelpContentSeeder`), existing Import Center (Department/Employee imports).

## 7. Gaps

G1 no parent position · G2 no primary-assignment marker/constraint · G3 no acting/temporary assignment (index forbids it) · G4 no organization validation service (cycles, vacancy, inactive holders, multiple primaries) · G5 no "organization resolution" diagnostic list · G6 Part Catalogue manager link is module-local · G7 employee profile labels team supervisor as "manager" (display) · G8 no Position/Parent import · G9 Help lacks organization/manager explanation · G10 no organization chart over positions.

## 8. Proposed target (additive, awaiting approval)

```
Position (+ ParentPositionId?, nullable, self-FK Restrict; hierarchy type implicit = Managerial)
EmployeeAssignment (+ AssignmentType: Primary|Secondary|Acting|Temporary, IsPrimary; filtered unique: one active PRIMARY per employee;
                    holder uniqueness per position relaxed to "one active Primary holder + optional Acting")
IOrganizationValidationService (cycle, self-parent, vacant/inactive parent, inactive holder, multiple primaries, expired, missing department)
EnterpriseActorResolver: RequesterManager source := Employee → active primary assignment → Position → ParentPosition → active holder (acting preferred when effective)
   result already persisted per stage (candidates + ResolutionDetails JSON) → add explicit ResolutionPath, Position ids, IsActing, DelegatedFrom
Part Catalogue: unchanged call (DirectManager/SuperiorManager responsibilities) — only the resolver's source changes; PartCatalogueOrgLinks kept read-only for history, never consulted once the hierarchy is enabled
Admin: extend TeamController org page (parent position, primary/acting), add an Organization Validation / Resolution Diagnostics page (read-only queue), chart from the same data
```
Role model: **no new roles** are required (existing `PartCatalogue.*` actions suffice); positions are never encoded as roles.
Migration: additive columns/indexes only; `Employee.Position`/`PositionId` untouched; **no reporting line is inferred or back-filled** (every employee starts as "organization setup required").

## 9. Security / audit model

Server resolves and validates the actor at stage entry; the browser never supplies approver/employee/position ids for authorization (already true: Execute takes only the request id). Resolution is audited through `WorkflowInstanceStage.ResolutionDetails`, `WorkflowHistory`, `AuditLog`.

## 10. Risks / rollback

Relaxing the "one active holder per position" index must be done as drop+recreate of one filtered index (additive semantics, no data loss; rollback = recreate old index if no acting rows exist). New parent column nullable → rollback = ignore. Legacy workflows unaffected.

## 11. STOP — decisions required before Phase 1

| # | Decision |
|---|---|
| D1 | Add `ParentPositionId` to the existing `Position` (recommended) or a separate hierarchy table? |
| D2 | Confirm **Team Supervisor ≠ Direct Manager**, and that the employee profile "Manager" label should become "Team supervisor" |
| D3 | **Who supplies the reporting lines?** The database has none. Options: enter via the existing org UI; or an import template (Position, Parent Position, Holder, effective dates). I will not back-fill or guess |
| D4 | A manager (or the top position) submits a request: route to their own direct manager; if they have none → "organization resolution required" (no CEO shortcut)? Or use the existing CEO list? |
| D5 | Acting manager: implement now (relax holder index, effective-dated Acting assignment) or defer? |
| D6 | `SuperiorManager` stage = grandparent position? |
| D7 | Snapshot at stage entry (current behaviour) — confirm no dynamic re-resolution of open approvals |
| D8 | "Controlled blocked state": today Submit is **refused** with the diagnostic (no instance is created). Is a refusal acceptable, or must an instance be created in a pending-resolution state? |
| D9 | Departments stay flat (63 roots, unused hierarchy) and are not used for routing — confirm |

Until D1–D3 are answered nothing can be implemented without inventing organizational truth (directive stop conditions 10 and 12).

---

# Phase 1 implementation record (decisions D1–D9 approved)

## Delivered

| Area | Implementation |
|---|---|
| Schema (additive, 2 migrations) | `Positions.PositionCode` (unique when set), `Positions.ParentPositionId` (self-FK, Restrict), `EmployeeAssignments.IsPrimary` / `AssignmentType` (Primary \| Secondary), unique filtered index `IX_EmployeeAssignments_EmployeeId_ActivePrimary`, `PartCatalogueRequests.RoutingBlockReason`. Migration back-fill is deterministic only: an employee with exactly one active assignment gets it as primary; everyone else is Secondary. **No reporting line is created or inferred (`ParentPositionId` is NULL everywhere).** |
| Central rule | `OrganizationManagerChain.WalkAsync` — Employee → effective primary position → `ParentPositionId` (×level) → effective holder. Used by `EnterpriseActorResolver` (RequesterManager kind) and the profile page. No CEO shortcut, no Admin/Department/Team-Supervisor/first-available fallback, cycle-safe, explainable `ResolutionPath`. |
| Failure mode (D8) | `ORGANIZATION_RESOLUTION_REQUIRED` with an issue code (`OrganizationIssueCodes`). Part Catalogue `Submit` accepts the request, sets status `OrgResolution` + `RoutingBlockReason`, writes approval history, creates **no** workflow instance and approves nothing. `RetryRoutingAsync` (requester or PartCatalogue.Admin) re-runs the central resolver after repair; the workflow initiator remains the requester. Other (non-organizational) configuration failures still refuse the submit. |
| Snapshot (D7) | `WorkflowInstanceStageCandidate` + `WorkflowInstanceStage.ResolutionDetails` JSON now also carry `OrganizationIssue`, requester/resolved position ids, `ResolutionPath`, `ResolvedAt`. Open instances never re-resolve. |
| Validation | `IOrganizationValidationService`: `ValidateAllAsync` (self-parent, cycle, inactive/vacant parent, inactive holder, no/multiple primary, expired/future assignments, position without department, inactive department, roots) and `ValidateParentChangeAsync` (used by UI and import). |
| Admin UI (D3) | `Admin/Organization` (positions, code, parent, holder, primary flag, effective dates, end assignment, hierarchy tree) and `Admin/Organization/Diagnostics` (issues + requests waiting for organization resolution). SystemAdmin only. |
| Import (D3) | `Admin/OrganizationImport` registered in the Import Center (`ImportCatalog` key `OrganizationStructure`): Upload → whole-file validation → server-held token → Confirm (single transaction, all-or-nothing). Never guesses/creates departments, employees or managers; does not replace an existing holder or duplicate an existing primary. |
| Labels (D2) | Employee profile "Manager" → "Team Supervisor"; new "Direct Manager" row resolved from the hierarchy (with reason when unresolved). |
| PartCatalogueOrgLinks | **Deprecated, non-destructive.** Former purpose: module-local manager chain only (no other purpose found). No resolver/service/UI reads or writes it any more; admin UI sections (org links, CEO list) removed; table, entity and data are untouched pending a data-owner decision. `PartCatalogueApproverAssignments` rows with StepKey `CEO` are likewise unused. `PartCatalogueRoutingPlanner` (legacy requests only, already deprecated) still contains CEO logic for old rows. |
| Help | New article `organization-hierarchy-direct-manager` (EN/FA) and updated employee-profile article. |

## Assumptions flagged

1. Superior Manager (level 2) when the direct-manager position is itself the root → stage `NOT_APPLICABLE (NoSuperiorManager)` (skipped), not blocked. Needs owner confirmation.
2. A holder of the manager position that is the requester themself is excluded; if that leaves nobody → `HOLDER_IS_REQUESTER` (organization resolution required).
3. Acting/temporary managers are **not** implemented (D5). `EmployeeAssignment.AssignmentType` already allows adding them; the "one active holder per position" index is unchanged.
4. Import rejects historical (already-ended) rows; closing assignments is done in the admin UI.

## Remaining data problems (not fixable by code)

* The database contains **no reporting lines**. Until an administrator enters the hierarchy (UI or Organization Import) every Part Catalogue submit by an employee without a resolvable Direct Manager lands in *Organization Resolution Required*.
* Existing positions have no department/code; users with accounts but no primary position appear as `EMPLOYEE_NO_PRIMARY_POSITION` in diagnostics.

## Follow-up module migration matrix (not done in Phase 1)

| Module | Manager source today | Next step |
|---|---|---|
| Part Catalogue | central hierarchy (done) | — |
| Work Request / Work Order / Permit | Role / discipline / team based | adopt `RequesterManager` responsibility where a line-manager step is wanted |
| Store Requisition | role/permission only | optional line-manager approval via same resolver |
| Contract / Package approvals | `ContractApprovalAuthorityRule` (role by value) | unchanged |
