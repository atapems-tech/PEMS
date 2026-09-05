# PEMS Enterprise RBAC-ABAC Authorization — Phase 2 (Org Hierarchy, Position, Scope, SoD) Report

**Date:** 2026-08-20
**Directive:** `.claude/Role/PEMS Enterprise Organization, Role & Permission Management — RBAC-ABAC Authorization Architecture.md`
**Scope of this report:** Phase 2 only, per the plan approved at the end of Phase 1. Phases 3-5 not started.

---

## 1. What was built

| Directive requirement | Phase 2 implementation |
|---|---|
| Configurable organization hierarchy (§2) | `Department.ParentDepartmentId` (self-referencing, nullable FK, `Restrict` on delete) — `Department` becomes the real hierarchy root, as decided in the Phase 1 report (no separate `Organization` entity; documented simplification, PEMS is single-company on-prem) |
| Real Position, separate from Role (§3) | New `Position` entity (`Title`, `Description`, optional `DepartmentId`). `Employee.PositionId` (nullable FK) added **alongside** the existing free-text `Employee.Position` string — the old column is untouched, no backfill/drop this phase |
| Scope-Based Access (§11) | `AccessControlService.AuthorizeAsync` now performs **real** scope evaluation: each of the user's role grants carries its own `ScopeType`/`ScopeValue` (from `RoleAssignment`, or `"Global"` for a direct `AspNetUserRoles` membership); a request is Allowed only if at least one qualifying grant is `Global` or its `ScopeValue` matches the caller-supplied `Context[ScopeType]` — otherwise `DeniedByScope` |
| Separation of Duties (§10) | New `SoDPolicy` entity (`PermissionAId` conflicts with `PermissionBId`). `AuthorizeAsync` checks, for any resource-scoped request, whether the same user already received an `Allow` for the conflicting Permission A **on the same `ResourceId`** (queried from `AuthorizationAuditLog`, not the generic entity-change `AuditLog` — see §2 below) — if so, `DeniedBySoD` |

### Why `AuthorizationAuditLog` and not the generic `AuditLog` for SoD

The generic `AuditLog` (existing, auto-populated by `PEMSDbContext.SaveChangesAsync`) only records entity-level Insert/Update/Delete — it cannot distinguish "Approve" from "Edit" on the same row, and its `EntityName` doesn't reliably map 1:1 to a `Permission.ResourceName`. `AuthorizationAuditLog` (built in Phase 1) already records exactly `(UserId, ResourceName, ActionName, ResourceId, Decision)` for every `AuthorizeAsync` call — precisely what SoD needs. This is a refinement over the original Phase 2 sketch (which had suggested reusing the generic log); reusing the purpose-built log instead avoids inventing a fuzzy entity-name mapping.

### Design boundary preserved from Phase 1

`AuthorizeForRolesAsync`/`DecideAsync` (the legacy-parity adapter path used internally by `PermissionService.HasPermissionAsync` and `RoleModuleActionService.HasActionAsync`) was **not touched**. All new scope/SoD logic lives in a new private `DecideForUserAsync`, used only by `AuthorizeAsync` — a path that had zero real callers before this phase (per the Phase 1 report). This means the change is additive by construction, not just by testing: the ~16 controllers wired to the legacy adapters in Phase 1 cannot be affected by Phase 2's logic at all.

---

## 2. A real, pre-existing bug found (not fixed — out of scope, documented per this session's standing discipline)

`PermissionService.SeedDefaultsAsync()` has a latent ordering bug: five module-specific guarded blocks (`WorkOrder`, `StoreRequisition`, `WorkRequest`, `Inspection`, `Transmittal`) run first and leave rows in `RolePermissions`; the method then has one final unconditional guard — `if (await _context.RolePermissions.AnyAsync()) return;` — before seeding `EngineeringDocuments`, `VDR`, `Contracts`, `MaterialRequisition`, `Equipment`, `DisciplineWorkspace`, `Correspondence`. On a **genuinely fresh, never-before-seeded database**, that guard is already true by the time it's reached (because the five module blocks above already inserted rows), so those seven modules are silently never seeded. This is invisible on the real dev database (seeded long before the five module-specific blocks existed in the code) and invisible in Phase 1's parity tests (which only assert seeded rows are non-empty, satisfied by the five modules that do get seeded). Confirmed by direct trace, not fixed — it is unrelated to the RBAC-ABAC directive and fixing it would be an unrelated behavior change. Phase 2's own tests were designed to be independent of this (see below).

---

## 3. Files changed

- Modified: `PEMS.Domain/Entities/Department.cs` — `ParentDepartmentId`/`ParentDepartment`
- New: `PEMS.Domain/Entities/Position.cs`
- Modified: `PEMS.Domain/Entities/Employee.cs` — `PositionId`/`PositionRef`
- New: `PEMS.Domain/Entities/SoDPolicy.cs`
- Modified: `PEMS.Persistence/Configurations/DepartmentEmployeeConfigurations.cs` — self-ref FK on Department, `PositionRef` FK on Employee, new `PositionConfiguration`
- Modified: `PEMS.Persistence/Configurations/AccessControlConfigurations.cs` — new `SoDPolicyConfiguration`
- Modified: `PEMS.Persistence/Context/PEMSDbContext.cs` — `DbSet<Position>`, `DbSet<SoDPolicy>`
- Modified: `PEMS.Application/DTOs/AccessControlDtos.cs` — `AccessDecision.MatchedScopeType`/`MatchedScopeValue`
- Modified: `PEMS.Persistence/Servises/AccessControlService.cs` — new `DecideForUserAsync`/`ResolveUserRoleGrantsAsync` (real scope + SoD), `AuthorizeAsync` rewired to use them; `DecideAsync`/`AuthorizeForRolesAsync` untouched
- New: `PEMS.Tests/Authorization/AccessControlScopeAndSoDTests.cs` (9 tests)
- Migration: `20260820012152_AddOrgHierarchyPositionAndSoD` — purely additive (2 nullable columns, 2 new tables, all FKs `Restrict`) — reviewed line-by-line before applying

---

## 4. Verification (all actually executed, not claimed)

- **Build**: `PEMS.Web` and `PEMS.Tests` both build with 0 errors; only pre-existing warnings in files not touched this phase.
- **Migration**: reviewed in full before `dotnet ef database update`; applied cleanly against the real dev SQL Server.
- **Tests**: **123/123 passed, 0 failed, 0 skipped** (114 from before + 9 new `AccessControlScopeAndSoDTests`):
  - `Department_ParentDepartmentId_SupportsRealHierarchy`, `Position_LinksToEmployeeAndDepartment_WithoutTouchingLegacyStringField` — data model roundtrips.
  - `AuthorizeAsync_GlobalRoleAssignment_AllowsRegardlessOfContext` — direct Identity role membership stays unrestricted.
  - `AuthorizeAsync_ScopedRoleAssignment_MatchingContext_Allows` / `_MismatchedContext_DeniesByScope` / `_NoContextProvided_DeniesByScope` — real scope enforcement, including the case where the caller passes no context at all (must not silently bypass).
  - `AuthorizeAsync_SoDPolicy_SameUserSameResource_SecondActionDenied` / `_DifferentResource_BothAllowed` / `AuthorizeAsync_NoSoDPolicyDefined_BothActionsAllowed` — SoD correctly scoped to the same resource and to an active policy only.
- **Live smoke test against the real dev database**: started `PEMS.Web`, confirmed login page returns HTTP 200 and no error/exception appeared in the startup log (proves the migration and the touched `Employee`/`Department` entities didn't break app startup). Queried `PEMSDB` directly — `Positions`/`SoDPolicies` tables exist with 0 rows (expected: Phase 2 adds no data seeding, only schema + mechanism).

---

## 5. Definition of Done — Phase 2 accounting

| Item | Status |
|---|---|
| Organization hierarchy configurable | ✅ via `Department.ParentDepartmentId` |
| Positions separate from Roles | ✅ data model; Admin UI to manage them is Phase 4 |
| Permission scopes exist and are enforced | ✅ real evaluation in `AuthorizeAsync`, proven with positive **and** negative tests |
| Separation of Duties exists | ✅ real evaluation, policy-driven, not hard-coded |
| Full regression suite passes | ✅ 123/123 |
| No unrelated production behavior changed | ✅ — `AuthorizeForRolesAsync`/legacy adapters untouched; pre-existing seeder bug found but deliberately not touched |

---

## 6. Next steps (unchanged from the Phase 1 plan)

- **Phase 3**: Replace mechanism (D)'s hard-coded `UnrestrictedRoles`/`ClientResponseRoles` arrays with real `AuthorizeAsync` calls (now that scope evaluation actually works) — the literal target of directive §27.
- **Phase 4**: Delegation/Emergency Authorization flows, Admin UI (Positions, Role Assignments editor, SoD policy editor, Effective Permissions view, Permission Simulator, Authorization Audit viewer).
- **Phase 5**: Full sweep of remaining unprotected sensitive endpoints (§28), full positive/negative security test matrix including direct-API-attack 403 tests (§26), `graphify update`, final audit report (§30).
