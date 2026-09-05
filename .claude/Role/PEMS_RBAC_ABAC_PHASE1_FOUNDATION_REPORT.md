# PEMS Enterprise RBAC-ABAC Authorization — Phase 1 (Foundation) Report

**Date:** 2026-08-19
**Directive:** `.claude/Role/PEMS Enterprise Organization, Role & Permission Management — RBAC-ABAC Authorization Architecture.md`
**Scope of this report:** Phase 1 only (data model + centralized decision service, behavior-preserving). Phases 2-5 are scoped in the approved plan but not started.

---

## 1. Audit performed before implementation (directive §24)

Full audit via Explore + Plan subagents, every finding file:line-verified before design began. Confirmed PEMS already runs on real ASP.NET Core Identity (`ApplicationUser`/`ApplicationRole`, cookie auth, global `RequireAuthenticatedUser()` fallback), with **five coexisting authorization mechanisms** live in production:

- **(A)** `RolePermission` + `PermissionService.HasPermissionAsync` — role×module×fixed-boolean-action grid, ~9 controllers.
- **(B)** `RoleModuleAction` + `RoleModuleActionService.HasActionAsync` — role×module×free-text-action, the Permit-to-Work family.
- **(C)** `PermitAuthorityService` — layers on (B) for self-approval prevention + WebAuthn step-up (safety-critical).
- **(D)** Ad-hoc static role-array checks (`DocumentWorkflowService.ClientResponseRoles`, `UnrestrictedRoles` duplicated 3×) — the actual target of directive §27, untouched in Phase 1.
- **(E)** `WorkflowRuntimeService.RoleMatches` — workflow-stage gating, a different concern, left alone permanently.

Critical finding preserved exactly: `RoleModuleActionService.HasActionAsync` suppresses the SystemAdmin bypass specifically for `ExecutionGate.EmergencyOverride` (PEMS-SEC-PERMIT-AUTH-001 §12 — "a normal Admin role MUST NOT automatically constitute Break-Glass authority"). This carve-out is reproduced literally in the new service and covered by an explicit test.

---

## 2. What was built

| Directive requirement | Phase 1 implementation |
|---|---|
| Granular `<Resource>.<Action>` permission model (§6) | New `Permission` entity (`ResourceName`, `ActionName`, `Name` = `"Resource.Action"`, `IsDestructive` flag per §7) |
| Role → Permission grant | New `RolePermissionGrant` (flat, unscoped — a role's permission set doesn't vary per user) |
| Scope + temporary/delegated assignment (§11-13) | New `RoleAssignment` (`ScopeType`/`ScopeValue`/`EffectiveFrom`/`EffectiveTo`/`AssignmentType`/`DelegatedByUserId`) — data model only in Phase 1; scope/SoD evaluation itself is stubbed pass-through, wired for real in Phase 2 |
| Centralized `Authorize(User, Resource, Action, ResourceId, Context)` decision service (§16) | New `IAccessControlService`/`AccessControlService` — `AuthorizeAsync` (user-aware, audit-logged) and `AuthorizeForRolesAsync` (role-list only, for legacy-adapter parity) |
| Decision reasons (§16) | `AccessDecisionReason` enum: Allow/DeniedByRole/DeniedByScope/DeniedBySoD/DeniedByWorkflowState/DeniedByOwnership/DeniedByPolicy |
| Audit trail (§21) | New `AuthorizationAuditLog`, written on every `AuthorizeAsync` call |
| No hard-coded business roles (§27) | `PermissionService.HasPermissionAsync` and `RoleModuleActionService.HasActionAsync` now delegate their actual decision to `AccessControlService` — the ~16 controllers calling them get centralized enforcement with **zero call-site changes** |

### Deliberately not built in Phase 1 (documented, not silently skipped)

- **`Organization` entity** — PEMS is single-company on-prem with a dormant, always-null `BaseEntity.TenantId`; a literal Organization table above Department would be unnecessary ceremony. `Department` becomes the real hierarchy root via a self-referencing `ParentDepartmentId` in Phase 2.
- **`Position` entity, `SoDPolicy`, standalone `Delegation`/`EmergencyAuthorization` tables** — Phase 2/4. `RoleAssignment.AssignmentType` (`Direct|Delegated|Emergency`) lays the groundwork now.
- **~50 new baseline role names (directive §4)** — no real mapping exists from PEMS's actual employees to those titles; introducing them speculatively would itself be an unaudited privilege change. New roles get added per-module only when a real owner confirms the mapping.
- **`AspNetUserRoles` customization** — deliberately rejected. Role claims are baked into the 8-hour sliding auth cookie with no live revalidation; adding scope/expiry columns there would make every legacy `[Authorize(Roles=)]`/role-array check silently ignore that scope/expiry for up to 8 hours. Scope+temporality live on the new, separate `RoleAssignment` table instead, which `AccessControlService` reads live on every call.

---

## 3. Files changed

- New: `PEMS.Domain/Entities/Permission.cs`, `RolePermissionGrant.cs`, `RoleAssignment.cs`, `AuthorizationAuditLog.cs`
- New: `PEMS.Persistence/Configurations/AccessControlConfigurations.cs` (4 `IEntityTypeConfiguration<T>` classes)
- New: `PEMS.Application/DTOs/AccessControlDtos.cs` (`AccessControlRequest`, `AccessDecision`, `AccessDecisionReason`)
- New: `PEMS.Application/Interfaces/IAccessControlService.cs`
- New: `PEMS.Persistence/Servises/AccessControlService.cs`
- New: `PEMS.Persistence/Seed/AccessControlSeeder.cs`
- New: `PEMS.Tests/Authorization/AccessControlParityTests.cs` (5 tests)
- Modified: `PEMS.Persistence/Servises/PermissionService.cs` — `HasPermissionAsync` body now delegates to `IAccessControlService`; `GetMatrixAsync`/`SetPermissionsAsync`/`SeedDefaultsAsync` untouched
- Modified: `PEMS.Persistence/Servises/RoleModuleActionService.cs` — `HasActionAsync` body now delegates to `IAccessControlService`; `SeedDefaultsAsync` untouched
- Modified: `PEMS.Persistence/Context/PEMSDbContext.cs` — 4 new `DbSet<T>` properties added
- Modified: `PEMS.Web/Program.cs` — DI registration for `IAccessControlService`; `AccessControlSeeder.SeedFromLegacyAsync` call added after the existing `RoleModuleActionService.SeedDefaultsAsync()` call
- Migration: `20260819200510_AddAccessControlCoreModel` — purely additive (4 new tables, zero changes to `AspNetUsers`/`AspNetRoles`/`AspNetUserRoles`/`RolePermissions`/`RoleModuleActions`) — reviewed line-by-line before applying

---

## 4. Verification (all actually executed, not claimed)

- **Build**: `PEMS.Web` and `PEMS.Tests` both build with 0 errors, 0 warnings from new code (recurring dev-server file-lock false-positive encountered and cleared, not a compile error).
- **Migration**: reviewed in full before `dotnet ef database update`; applied cleanly against the real dev SQL Server.
- **Tests**: **114/114 passed, 0 failed, 0 skipped** (109 pre-existing + 5 new `AccessControlParityTests`).
  - `RolePermissions_EveryRoleModuleActionCombo_AccessControlAgreesWithRawBooleanGrid` — for every real seeded `(Role, Module)` row × 10 fixed actions, compares the new engine's decision against an independently-computed expected value read directly from the raw boolean columns (not against `HasPermissionAsync` itself, which would be tautological now that it delegates).
  - `RoleModuleActions_EveryRow_AccessControlAgreesWithRawTable` — same independent-oracle check for every real seeded Permit-family row.
  - `SystemAdmin_BypassesEveryModuleAction_ExceptExecutionGateEmergencyOverride` — confirms the PEMS-SEC-PERMIT-AUTH-001 §12 break-glass carve-out survived the refactor exactly.
  - `NoOrphanGrants_EveryGrantedCombinationResolvesToARealPermission` — catches seeder gaps.
  - `AuthorizeAsync_WritesAuthorizationAuditLogRow` — confirms the audit trail actually writes.
- **Live smoke test against the real dev database** (not InMemory): started `PEMS.Web`, confirmed the login page returns HTTP 200 (proves the entire startup pipeline — including the new seeder — completed without an unhandled exception; a seeder failure would have crashed startup), then queried `PEMSDB` directly:

  | Table | Row count after seed |
  |---|---|
  | Permissions | 55 |
  | RolePermissionGrants | 167 |
  | RoleAssignments | 1 (backfilled from the one existing `AspNetUserRoles` row) |
  | AuthorizationAuditLogs | 0 (expected — no gated action was exercised during the smoke test, only the anonymous login page) |

---

## 5. Definition of Done — Phase 1 accounting

| Item (directive checklist) | Status |
|---|---|
| Permissions are granular (Resource.Action) | ✅ |
| Resource/action permissions exist | ✅ |
| Deny-by-default is enforced | ✅ (no `Permission` row or no `RolePermissionGrant` → Deny) |
| Backend authorization enforced for existing call sites | ✅ (via adapter, zero call-site changes) |
| Role/permission decision is centralized, not hard-coded per-controller | ✅ for mechanisms (A)/(B); (D)'s hard-coded arrays are Phase 3 |
| Effective Permissions / Simulator / Admin UI | ⬜ Phase 4 |
| Scope-based access | ⬜ data model ready, evaluation stubbed pass-through — Phase 2 |
| Separation of Duties | ⬜ Phase 2 |
| Delegation / Emergency Authorization (full flow) | ⬜ data model ready (`RoleAssignment.AssignmentType`) — Phase 4 |
| Organization/Department/Position hierarchy | ⬜ Phase 2 |
| Full audit trail | ✅ decision-level (`AuthorizationAuditLog`); entity-change audit already existed (`AuditLog`) |
| Full regression suite passes | ✅ 114/114 |
| Graphify updated | ⬜ deliberately deferred to end of Phase 5 (graph should reflect the finished model, not each intermediate phase) |
| No unrelated production behavior changed | ✅ — proven by parity tests against real seeded data, not asserted |

---

## 6. Next steps (not started, per the approved phased plan)

- **Phase 2**: `Department.ParentDepartmentId` (real hierarchy), `Position` entity, real scope evaluation in `AccessControlService.AuthorizeAsync`, `SoDPolicy` + conflict evaluation.
- **Phase 3**: Replace mechanism (D)'s hard-coded `UnrestrictedRoles`/`ClientResponseRoles` arrays with real `AuthorizeAsync` calls — the literal target of directive §27.
- **Phase 4**: Full Delegation/Emergency Authorization flows, Admin UI additions (Positions, Role Assignments editor, Delegations, SoD policy editor, Effective Permissions view, Permission Simulator, Authorization Audit viewer — Users/Roles/Department/PermissionMatrix/AuditLog admin pages already exist and get extended, not rebuilt).
- **Phase 5**: Full sweep of remaining unprotected sensitive endpoints (§28), full positive/negative security test matrix including direct-API-attack 403 tests (§26), `graphify update`, final audit report (§30).
