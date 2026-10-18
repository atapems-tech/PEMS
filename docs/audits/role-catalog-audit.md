# PEMS — Enterprise Role Catalog: Audit, Gap Analysis & Decision Record

Status: audit complete (pre-implementation). Evidence = repository code + live dev DB (`PEMSDB`) read 2026-10-07.
Notification subsystem is a frozen baseline and was not touched by this task.

## 0. Regression gate (cleared before the audit)

| Check | Result |
|---|---|
| Build `PEMS.slnx` | 0 errors |
| Notification focused tests | 24/24 |
| Full suite | 2089/2094 |
| Authorization tests | all pass (none among the 5 failures) |
| 5 failures | 4 passed in isolation (env/host-timing: `VDRPagedListTests`, 2x `FileSecurityHttpTests`, `WorkOrderConcurrencyGateTests`). `MrsImportBatchCapacityTests` is a borderline perf test: 53–61 s on both a clean `HEAD` worktree and the working tree against a ~60 s threshold; its code path contains no Notification reference. **Not attributable to Notification or Role Catalog work.** |

## 1. Existing role inventory (19 roles, 4 users)

Authorization model facts: `Name` is already the stable language-neutral key; `ApplicationRole` has only `DisplayName` + `Description` (no domain/category/system/active/sort). Permissions are `Resource.Action` rows (505) granted via `RolePermissionGrants` (687); the legacy `RolePermissions` matrix (252 rows) is the admin-editable source that is re-synced into grants on **every startup**. No custom ASP.NET policies/handlers, no role inheritance, no per-user direct permissions (only scoped `RoleAssignments`, 4 rows, all Global). `SystemAdmin` bypasses the matrix in code (except `ExecutionGate.EmergencyOverride`, `WorkflowManagement.*`).

| Id | Name (stable key) | DisplayName | Users | Active grants | Domain (proposed) | Category (proposed) | Origin |
|---|---|---|---|---|---|---|---|
| 1 | SystemAdmin | System Admin | 2 | 6 (+code bypass) | Platform | Platform | system (SystemRoles) |
| 2 | EngineeringManager | Engineering Manager | 0 | 182 | Engineering | Domain | system |
| 3 | EngineeringSupervisor | Engineering Supervisor | 0 | 113 | Engineering | Domain | system |
| 4 | DisciplineEngineer | Discipline Engineer | 0 | 40 | Engineering | Functional | system |
| 5 | DocumentController | Document Controller | 0 | 35 | DocumentControl | Functional | system |
| 6 | MaintenanceEngineer | Maintenance Engineer | 0 | 114 | Maintenance | Functional | system |
| 7 | ContractManager | Contract Manager | 0 | 24 | Contracts | Domain | system |
| 8 | QAManager | QA Manager | 0 | 24 | Quality | Domain | system |
| 9 | Viewer | Viewer | 1 (test user, inactive) | 45 | General | ReadOnly | system |
| 10 | Legal | Legal | 0 | 2 | Legal | Functional | system |
| 11 | Finance | Finance | 0 | 4 | Finance | Functional | system |
| 12 | HSE | HSE | 0 | 2 | HSE | Functional | system |
| 13 | MNT.Manager | *(null)* | 0 | 6 | Maintenance | Domain | WorkOrderGovernanceSeeder |
| 14 | MNT.PlanningHead | *(null)* | 0 | 27 | Maintenance | Domain | WorkOrderGovernanceSeeder |
| 15 | MNT.PlanningExpert | *(null)* | 0 | 4 | Maintenance | Functional | WorkOrderGovernanceSeeder |
| 16 | MNT.DisciplineHead | *(null)* | 1 | 33 (+1 dead) | Maintenance | Domain | WorkOrderGovernanceSeeder |
| 17 | MNT.SeniorExpert | *(null)* | 0 | 5 | Maintenance | Functional | WorkOrderGovernanceSeeder |
| 18 | MNT.Expert | *(null)* | 0 | 4 | Maintenance | Functional | WorkOrderGovernanceSeeder |
| 19 | OPS.IssueAuthority | *(null)* | 0 | 4 | Operations | Functional | WorkOrderGovernanceSeeder |

Navigation: 9 `NavigationVisibilityOverrides`, all for `MNT.DisciplineHead`. All other role menus come from hard-coded `MenuAccess` role arrays. "Custom roles": none exist in the DB today. Role-name hard-coding: `[Authorize(Roles=SystemAdmin)]` on 14 admin controllers, `SystemAdmin,EngineeringManager` on Department/Employee/Help, `SystemRoles.All` as the delete guard, `MenuAccess`, `ImportCatalog`, Hangfire filter.

## 2. Authorization surface (summary; per-module detail below)

Mechanisms: (A) `[Authorize(Roles)]` class gates, (B) inline `HasPermissionAsync(roles, module, action)` (~60 controllers), (C) `[RequirePermission]` (7 uses), (D) `IAccessControlService.AuthorizeAsync` (SoD/scope; ~10 callers), (E) workflow stage roles + `AccessControlWorkflowAuthorizationHook`. Global `[Authorize]` = authenticated only.

| Resource | Action | Current authorization | Responsible role(s) today | Current gap |
|---|---|---|---|---|
| WorkOrder | Create/Plan/Prepare | workflow perm + org-domain hook | MNT.PlanningHead/Expert | – |
| WorkOrder | Assign | perm + Maintenance org + same discipline | MNT.DisciplineHead only | single holder |
| WorkOrder | Issue | perm + Operations org + release gate | OPS.IssueAuthority | – |
| WorkOrder | Start/Execute | perm + Maintenance + discipline + SoD vs Plan/Prepare/Issue | MNT.DisciplineHead/SeniorExpert/Expert | – |
| WorkOrder | Hold | perm | **none active** (grant deactivated by legacy `CanHold` sync) | **defect** |
| WorkOrder | Verify/FinalApprove | perm + originating org/discipline + SoD vs Execute/Start/Verify | DisciplineHead, SeniorExpert, OPS.IssueAuthority, ES, EM, MNT.Manager | planner/issuer may also verify |
| WorkOrder | Close | perm | **no holder** (FinalApprove closes) | dead permission |
| WorkRequest | Create/Approve | perm via workflow + virtual roles | MaintenanceEngineer, ES, EM, PlanningHead / ES, EM, PlanningHead | review+approve merged; no creator≠approver |
| Correspondence | Approve/Return/Reject/Issue/Close | one `[RequirePermission]` | DocumentController, EM, PlanningHead | 5 authorities on 1 permission |
| EngineeringDocuments | Create/Edit/Approve/Freeze… | matrix + discipline scope + SoD (Create/Edit vs Approve) | per matrix | – |
| Contracts / ProcurementPackage / Tender | Approve | matrix + SoD (Contracts, Package) ; `ContractApprovalAuthorityRule` fail-closed | ContractManager, EM | rule table empty |
| PurchaseOrder | Approve | matrix | **no holder** | unused/dead |
| StoreRequisition | Create/Approve | matrix | Approve: ES, EM; **no warehouse role exists** | Warehouse domain has no role |
| IRN | Approve | matrix | QAManager | – |
| MaterialItem | Create (ReceiveStock) ; Adjust/TransferStock | Create only; **Adjust/Transfer: none** | EM/ES/ME | ungated state change |
| Permit/LOTO/GasTest/Simops/ShiftHandover | module actions | `RoleModuleActions` + WebAuthn step-up (Permit) | EM, ES, QAManager | controllers MOC/Permit/QC/Instrument/Asset/SparePart/Isolation/Meeting/TQ… have **0** permission checks |
| CMFinding | Create/Approve | controller checks Create | **no holder** (only SystemAdmin) | CBM findings unusable by non-admins |
| MOC / TechnicalQuery | workflow stage roles | stage roles only | SystemAdmin, ES, EM, QAManager | no controller permission |
| Roles/Users/PermissionMatrix/SoD/NavVis/Audit | admin | `[Authorize(Roles=SystemAdmin)]` | SystemAdmin | cannot delegate (hard-coded) |

~22 state-changing controllers have login-only access (Instrument, MOC, Permit, QC, MDB, Isolation, MechanicalEquipment, Meeting, TechnicalQuery, ShiftHandover, Asset, Projects, SparePart, ChatAssistant, DashboardSettings, ExecutionGate, GasTest, Intake, Tag, DCC, DocumentUpload, Notification). `Notification` is login-only **by design** (scoped to the caller). Verified by grep: Instrument/MOC/Permit/QC/SparePart/Asset/TechnicalQuery controllers contain 0 permission markers.

## 3. Gap analysis & role classification

Principle: **Role = responsibility/authority boundary; Organization (Department/Discipline/Domain/Team) = scope.** Organization scope already lives in `Department.OrganizationDomain`, `Employee.DisciplineId`, `RoleAssignment.Scope*`, `EmployeeAreaScope`; the catalog never encodes it into roles except where the repo already uses domain-prefixed roles (`MNT.*`, `OPS.*`).

| Candidate | Class | Evidence / reason |
|---|---|---|
| All 19 current roles | **EXISTING — keep** | never renamed/removed; metadata backfilled only where empty |
| **WH.Storekeeper** | **REQUIRED** | `OrganizationDomains` declares *Warehouse*; Warehouse domain has **zero** roles. Inventory/StoreRequisition/MaterialItem/IRN workflows have no warehouse actor (Create side held only by Engineering/Maintenance roles); Inventory menu unreachable for a warehouse user |
| **WH.Manager** | **REQUIRED** | `StoreRequisition.Approve` is held only by EngineeringSupervisor/EngineeringManager — engineering managers approve store issues. Approve ≠ Create separation (Storekeeper creates, Manager approves) needs a distinct authority holder |
| Maintenance Manager / Planner / Supervisor / Technician | DUPLICATE | = `MNT.Manager` / `MNT.PlanningExpert`+`PlanningHead` / `MNT.DisciplineHead` / `MNT.Expert` |
| Reliability Engineer, Inspection Engineer, Corrosion/Electrical/Instrumentation/Mechanical/Process Engineer | DUPLICATE | covered by `DisciplineEngineer`/`MaintenanceEngineer`; discipline is **scope** (`Employee.DisciplineId`), not a role |
| Engineering Document Controller, Correspondence Officer, EDMS Administrator, Document Controller | DUPLICATE | `DocumentController` (admin of EDMS config is `SystemAdmin`-only by hard-coded attribute) |
| Workflow Approver / Workflow Administrator | DUPLICATE | approvers are stage roles; admin = `SystemAdmin` (`WorkflowManagement.*` explicit grants) |
| Management / Executive Viewer, Audit Viewer (read-only) | DUPLICATE | `Viewer` (45 View grants, Operations/Engineering/ImportCenter menus) covers read-only; a separate role would hold the same grants |
| Procurement Manager / Contracts Specialist / Procurement Specialist | DUPLICATE | `ContractManager` holds Contracts/Package/Tender approve; PO workflow has no actor yet (see Pre-existing Findings) |
| QC Inspector | OPTIONAL | QC controller is ungated; nothing to hold until gating exists |
| Operations Manager/Supervisor/Operator | OPTIONAL | `OPS.IssueAuthority` is the only Operations authority in the repo; Permit/LOTO use `RoleModuleActions` (EM/ES/QAManager) — no further operations workflow stage found |
| Security Administrator / Application Administrator / IT Administrator / Audit Administrator | OPTIONAL (blocked) | cannot be made real without replacing `[Authorize(Roles=SystemAdmin)]` on 14 controllers with permission checks — a platform change out of scope; creating them now would be nominal roles with no authority |
| HR Administrator | REJECTED | Employee/Department access is the hard-coded `SystemAdmin,EngineeringManager` attribute; role would hold nothing |
| Project Manager / Project Controls | REJECTED | `Projects`/`Report` controllers ungated; no project permission vocabulary exists |
| Finance Manager/Specialist, HSE Manager/Officer, Quality Manager/QC | DUPLICATE | `Finance`, `HSE`, `QAManager` exist |

**Role explosion check:** 2 new roles (+0 per-controller, +0 per-department). Net catalog = 21 roles. Disciplines/departments stay as scope.

## 4. Proposed new roles — full decision record

| | WH.Storekeeper | WH.Manager |
|---|---|---|
| Reason | Warehouse actor for requisition/receipt/stock record keeping | Approval authority for store requisitions (Warehouse domain) |
| Domain / Category | Warehouse / Functional | Warehouse / Domain |
| Permissions (legacy matrix → grants) | StoreRequisition V,C,E · MaterialItem V,C,E · InventoryMasterData V,C,E · IRN V,C,E · Barcode V,C | StoreRequisition V,**Approve** · MaterialItem V,E · InventoryMasterData V,E · IRN V · Barcode V |
| Deliberately NOT granted | any Approve; IRN.Approve (QAManager); WorkOrder.*; Admin | StoreRequisition.Create; IRN.Approve; WorkOrder.*; Admin |
| Workflow authority | creates requisitions, records receipts | approves/reserves/issues requisitions (`StoreRequisition.Approve` gate) |
| SoD impact | no engine rule invented. Separation is by role design: Create (Storekeeper) vs Approve (Manager); receipt (Storekeeper) vs IRN QC approval (QAManager). Existing engine SoD policies unchanged | same |
| Navigation | `Inventory` module via explicit `NavigationVisibilityOverride` (reason recorded); `CmmsIntegration`, `Administration` stay hidden | same |
| Backward compatibility | additive: new role rows + RolePermission rows created **only when the role is first created**; no existing grant changes; no user auto-assigned | same |
| Cross-domain check | no `WorkflowManagement.*`, no user/role/permission administration (those are `SystemAdmin` code gates, not matrix permissions, so a matrix role *cannot* acquire them); not in `SystemAdmin` | same |

## 5. Cross-domain (privilege) check

`Maintenance Manager` (`MNT.Manager`): 6 grants (WorkOrder View/Reject/CancelPreIssue/CancelExecution/FinalApprove + legacy Approve). No `WorkflowManagement.*`, no Users/Roles/PermissionMatrix/SoD/NavVisibility (those controllers are `SystemAdmin`-only attributes, unreachable by any matrix role). Verified: no non-SystemAdmin role holds `WorkflowManagement.*` (only the 6 explicit SystemAdmin grants). `EmployeeController` additionally blocks non-admins from assigning SystemAdmin. A test pins this for all catalog roles.

## 6. Traced chain (Role → Permission → Policy → Workflow → Scope → Navigation → SoD → Audit)

| Link | Mechanism | Finding |
|---|---|---|
| Role | `AspNetRoles` (+ new metadata) | metadata was missing; added additively |
| Permission | `RolePermissions` (legacy, editable) → synced to `RolePermissionGrants` at startup | legacy flag is source of truth and **overwrites** grants every boot (Hold defect) |
| Policy | none custom; `HasPermissionAsync` → `AuthorizeForRolesAsync` | role-name only: no scope/SoD/audit on this path |
| Workflow | stage `ResponsibleRole` / perms; WO hook (org+discipline+SoD) | WO fully traced; Letters merged; MOC/TQ stage-only |
| Scope | `OrganizationDomain`, `Employee.DisciplineId`, `RoleAssignment.Scope*` (callers never pass Context ⇒ only Global works) | Warehouse domain had no role |
| Navigation | override > permission default (CMMS items) > `MenuAccess` arrays | MNT.*/OPS roles hidden (pre-existing) |
| SoD | 10 `SoDPolicies` (history-based, needs `ResourceId`); WO hook; Letter/Criticality hard-coded | policies 3–5 never evaluated |
| Audit | EF interceptor writes field-diff `AuditLogs` for Role/Grant/RolePermission/SoD/NavOverride/UserRole | no semantic events or reasons (pre-existing) |

## 7. Approval-gate checklist (all satisfied before implementation)

1. every existing role — §1 ✔ 2. every proposed new role — §4 ✔ 3. reason for each — §3/§4 ✔ 4. permissions for each — §4 ✔ 5. workflow authority — §4 ✔ 6. SoD impact — §4 ✔ 7. navigation impact — §4 ✔ 8. backward-compat impact — §4 ✔

## 8. Implementation plan (additive only)

* `ApplicationRole` +`Domain`, +`Category`, +`IsSystemRole`, +`SortOrder` (migration `RoleCatalogMetadata`, nullable/defaulted). **Not added:** `RoleKey` (Name already is the stable key), `IsDefaultRole` (= `IsSystemRole` for catalog roles), `IsActive` (role resolution reads `AspNetUserRoles`/`RoleAssignments` by name; an inactive flag would be cosmetic without changing authorization — recorded, not faked).
* `RoleCatalog` (definitions) + `RoleCatalogSeeder` (idempotent; creates missing roles; backfills **empty** metadata only; seeds permissions/nav overrides **only when the role row is first created**; never deletes/overwrites; reports possible duplicates of custom roles without merging).
* Localization via existing `IStringLocalizer<SharedResource>` keys `Role_<Name>_Name/_Desc`, `RoleDomain_*`, `RoleCategory_*` (EN+FA); DB `DisplayName`/`Description` remain the English/custom fallback.
* Roles admin UI: domain/category columns, localized names, system flag from DB, delete guard uses `IsSystemRole`, possible-duplicates notice.
* Tests: fresh/idempotent/existing-DB seeding, custom-role and user-assignment preservation, permission preservation, no cross-domain grants, authorization allow/deny for new roles, navigation, catalog invariants.

## 9. Pre-existing findings (recorded, **not** fixed here)

1. **`WorkOrder.Hold` dead**: seeded grant for `MNT.DisciplineHead` is `IsActive=0`; every `MNT.*` `RolePermissions.CanHold=0`, and the startup legacy sync deactivates it. Only SystemAdmin can hold/resume.
2. `WorkOrder.Close` has no holder (FinalApprove closes); granular WO permissions (Plan/Prepare/Assign/Issue/Start/Execute/Verify/FinalApprove/Reject/Cancel*) have no admin UI — matrix edits only the 10 legacy flags.
3. `MNT.*` (except DisciplineHead via 9 overrides) and `OPS.IssueAuthority` are absent from `MenuAccess`: CmmsIntegration/Operations menus hidden although they hold WO permissions.
4. ~22 state-changing controllers have login-only access (§2). `MaterialItemController.AdjustStock/TransferStock` ungated (code comment acknowledges).
5. `CMFinding.Create/Approve` and `PurchaseOrder.Approve` have no non-admin holder: CBM Findings are unusable by non-admins.
6. SoD policies for `WorkOrder.Create/Edit vs Close` and `Permit.Create vs Issue` are never evaluated; SoD is audit-history based, needs `ResourceId`, bypassed by SystemAdmin; `HasPermissionAsync` path skips scope/SoD/audit; `RoleAssignment` scope never matches (callers pass no Context).
7. Admin gating is hard-coded to `SystemAdmin` (14 controllers) — blocks any delegated Security/Audit/Application administrator role.
8. Letters: Approve/Return/Reject/Issue/Close share one permission. MOC/TQ authority is stage-roles only. `ContractApprovalAuthorityRules` is empty (value-based approval inactive).
9. Role admin: no Edit action; role Delete does not clean grants; no semantic audit events or reasons for role/permission changes (only EF field diffs).
10. `UsersController.Create` without a Discipline crashes on FK `FK_Employees_Disciplines_DisciplineId`.
11. Module-name inconsistencies (`Contracts`/`Contract`, `Vendors`/`Vendor`); ~130 published versions of the EngineeringDocument workflow suggest startup re-publishing.
12. Dev-DB hygiene: test user `nt_userb` (inactive) created during Notification validation remains.
13. Startup seeding: legacy matrix overwrites grants on every boot, so grants created directly in `RolePermissionGrants` for roles that also have legacy rows are at risk.

---

# Implementation record (post-audit)

## Files changed (Role Catalog only; Notification files untouched)
| File | Why |
|---|---|
| `PEMS.Persistence/Identity/ApplicationRole.cs` | +`Domain`, `Category`, `IsSystemRole`, `SortOrder` (additive metadata) |
| `PEMS.Persistence/Identity/RoleCatalog.cs` (new) | catalog of 21 roles: domain/category/description, default permissions + visible modules for the 2 new roles, duplicate detector |
| `PEMS.Persistence/Seed/RoleCatalogSeeder.cs` (new) | idempotent, additive seeder (see rules below) |
| `PEMS.Persistence/Migrations/*RoleCatalogMetadata*` (new) | 4 `AddColumn` operations, no drops/alters (Down only drops them) |
| `PEMS.Web/Program.cs` | runs the seeder after `SeedDefaultsAsync` and before `SeedFromLegacyAsync`; logs possible duplicates |
| `PEMS.Web/Areas/Admin/Controllers/RolesController.cs`, `Models/RoleModels.cs` | Domain/Category/SortOrder in the list, `IsSystemRole` from DB, delete guard also honours `IsSystemRole`, duplicate notice |
| `PEMS.Web/Areas/Admin/Views/Roles/Index.cshtml` | localized name/description (catalog roles only), Domain/Category columns, duplicate notice |
| `PEMS.Web/Resources/SharedResource(.fa).resx` | 61 keys each (`Role_*`, `RoleDomain_*`, `RoleCategory_*`) |
| `PEMS.Tests/Authorization/RoleCatalogTests.cs` (new) | 18 tests |

## Seeder rules (how idempotency / data protection work)
* Missing baseline role → created (role + its default `RolePermissions` rows in **one** `SaveChanges`; nav override right after).
* Existing role (matched **case-insensitively** on `Name`/`NormalizedName`) → only *empty* DisplayName/Description/Domain/Category/SortOrder are filled; `IsSystemRole` may only go false→true. Nothing else is touched. No grants/permissions of an existing role are ever modified.
* Default permissions/nav overrides are written **only when the role row is created by that run**; later admin edits/deletions are never reverted or resurrected.
* No user is ever assigned to a role. Custom look-alikes are reported (log + Roles page notice), never merged.

## Live verification (dev DB, two consecutive boots)
21 roles; 709 grants (687 + 22 for WH roles); the 19 pre-existing roles' active grants unchanged (674 total, identical per role); `WorkOrder.Hold` still inactive (pre-existing, untouched); users-in-WH-roles 0; boot 2 identical to boot 1.

## Independent post-implementation audit — dispositions
| Finding | Verdict | Action |
|---|---|---|
| Case-sensitive role match could INSERT a duplicate `NormalizedName` and crash startup (e.g. custom `wh.manager`) | **Confirmed defect** | fixed (case-insensitive match) + 3 regression tests |
| Localizer key derived from role name lets custom `MNT_Manager` borrow a baseline's localized text | confirmed (display only) | fixed: only exact catalog names are localized |
| Role insert and defaults in two saves | confirmed (fails safe: less access) | fixed: role + permission rows now atomic; nav override follows |
| "fa resx has no Role_* keys" | **incorrect** | both files hold the same 45 `Role_*` keys; Persian page verified rendering |
| MNT.*/OPS now undeletable | intended behaviour change | documented |
| Delete has no assigned-users check | pre-existing | recorded below |
| Notification files modified by this task? | no | grep of diffs: no role/catalog references |

## Additional pre-existing findings (not fixed)
14. `Roles/Index.cshtml` still has hard-coded English strings ("Define role boundaries…", "roles configured", "Custom", "No roles configured") visible in the Persian UI.
15. `RolesController.Delete` does not check for assigned users or clean up the role's grants/overrides.
16. The uncommitted tree mixes Notification files, Role Catalog files and `launchSettings.json` — split into separate commits.

## Remaining risks needing business decisions
Orphan/dead permissions (`WorkOrder.Hold/Close`, `PurchaseOrder.Approve`, `CMFinding.*`), ~22 login-only state-changing controllers, hard-coded `SystemAdmin` admin gating (blocks delegated Security/Audit admin roles), MNT/OPS navigation gap, SoD policies 3–5 never evaluated. None are changed by this task; each needs an owner decision because it alters who can do what.
