# PEMS Navigation Visibility Architecture Baseline — Phase 6

**Date:** 2026-09-29
**Status:** Implemented, tested, live-validated. This is the authoritative baseline for how sidebar navigation visibility works in PEMS going forward.
**Builds on:** [PEMS Authorization Architecture Baseline — Phase 1-5 — 2026-09-29](PEMS%20Authorization%20Architecture%20Baseline%20—%20Phase%201-5%20—%202026-09-29.md). This document does not modify, replace, or duplicate anything in that baseline.

---

## 1. The core distinction (read this first)

PEMS now has three separable concerns, each with exactly one owner:

| Concern | Question it answers | Owner |
|---|---|---|
| **Permission** | What is the user allowed to do? | `AccessControlService` / `RolePermissionGrant` (Phase 1-5) |
| **Scope** | Where / on what data? | Existing ABAC (`RoleAssignment`, ScopeType/ScopeValue) |
| **Visibility** | What does the user see in the UI? | `NavigationVisibilityResolver` + `NavigationVisibilityOverride` (this phase) |

**Navigation Visibility is a presentation/navigation concern and is never an authorization authority.**

Two rules follow directly and are enforced by tests (§7):

- **Hidden ≠ Unauthorized.** Hiding a sidebar item never denies a request. Every controller/action keeps exactly the `[Authorize]`/`HasPermissionAsync`/`HasActionAsync` check it had before this phase. A hidden-but-authorized page is still reachable by direct URL.
- **Visible ≠ Authorized.** Nothing about being visible in the sidebar grants access. The sidebar link existing was never a security boundary before this phase, and remains not one now.

If a future change makes `NavigationVisibilityOverride` participate in an `[Authorize]` decision, a permission check, or a workflow gate in any way, that change violates this baseline and must be rejected or escalated, not implemented quietly.

---

## 2. What existed before this phase (audit findings)

- **Sole navigation rendering surface:** `PEMS.Web/Views/Shared/_SidebarMenu.cshtml`, referenced exactly once, from `_Layout.cshtml:166`. No Area-specific or duplicate sidebar exists — the Admin area renders through the same partial.
- **Sole pre-existing visibility mechanism:** `PEMS.Web/Security/MenuAccess.cs` — eleven hardcoded `string[]` role arrays, checked via `allowedRoles.Any(user.IsInRole)`. Entirely independent of `AccessControlService`/`RolePermissionGrant`; a textbook "hardcoded role list" pattern, but a pre-existing one, not introduced by this phase.
- **Two pre-existing exceptions already avoided `MenuAccess`:** Quick Setup (`canSeeQuickSetup`, permission-derived via `PermissionService.HasPermissionAsync(roles, "Setup", "View")`) and the Import Center entry (`canSeeImportCenter`, via `ImportCatalog.VisibleByRole`). Both preserved as-is; this phase layers overrides on top of them rather than re-deriving them.
- **No stable item identity existed.** Every sidebar link was a bare inline Razor `<a>` keyed only by controller/action. No `NavigationVisibility`/`SidebarVisibility`/`MenuVisibility` table existed anywhere in the repo (confirmed by repo-wide grep, zero hits).
- **Existing security-test precedent:** three navigation-exposure test files (`CmmsOperationsNavigationExposureTests`, `ImportCenterNavigationTests`, `ShiftHandoverNavigationExposureTests`) already proved Hidden ≠ Unauthorized for the pre-existing `MenuAccess` mechanism. This phase's own tests follow the same shape for the new override mechanism.

---

## 3. What this phase added — the minimum additive layer

### 3.1 Schema (one table, additive only)

`NavigationVisibilityOverride` (`PEMS.Domain/Entities/NavigationVisibilityOverride.cs`, migration `20260929064419_AddNavigationVisibilityOverride`):

```
RoleId int (FK → AspNetRoles, cascade)
ModuleKey string(100)
ItemKey string(150) nullable      -- null = module-level row, non-null = item-level row
IsVisible bool
Reason string(500) nullable
CreatedBy / CreatedDate / ModifiedBy / ModifiedDate
```

Zero rows = zero behavior change. This was the explicit design bar: the table only ever *narrows or widens what is shown*, on top of the pre-existing defaults, never replacing them.

Why a new table was necessary (not reused from Phase 1-5): `RolePermissionGrant` answers "may this role do X"; visibility needs to answer a structurally different question ("does this role see item Y in the sidebar"), including for items with no 1:1 permission at all (Instrument, Asset, SparePart — see §4). Reusing `RolePermissionGrant` for this would have made a permission grant double as a UI-visibility flag, which is exactly the conflation this phase was told to prevent.

### 3.2 Resolution logic — `NavigationVisibilityResolver` (`PEMS.Web/Security/`)

For every module, and for every item inside the one drill-down module (CmmsIntegration, see §4), visibility resolves in this order:

1. **Explicit override**, if a row exists for that role + module (+ item).
2. **Default** — permission-derived (via `IAccessControlService.AuthorizeForRolesAsync`, the same lightweight, non-audit-writing path already used by Phase 4's `EffectiveRole`/`EffectiveUser` pages) where a real backing permission exists, else `MenuAccess`'s existing role array unchanged.
3. Nothing else. No new decision engine, no second `RolePermission`-style table, no duplicated audit log.

**Deterministic multi-role union (no arbitrary deny-override):** for a user with several roles, visibility is computed **per role** (that role's own override if present, else its own default) and then unioned with OR. A hide-override on Role A never hides an item for a user whose Role B still defaults to visible. This mirrors how `MenuAccess.CanSee` already behaved (`.Any(IsInRole)`), so it introduces no new surprising semantics — it just makes the union explicit and override-aware. Proven by `Multi_role_union_...` in `PEMS.Tests/Authorization/NavigationVisibilityTests.cs`.

### 3.3 Scope decision: full item-level drill-down for one module only

The brief explicitly asked for full item-level visibility for **one representative real module**, not a rewrite of all ~100 sidebar links. **CmmsIntegration** was chosen because the brief's own worked examples (Work Order, Maintenance Plan, Work Request) are children of it. All other modules (EngineeringManagement, AIIntelligence, CorrespondenceDocControl, QualityControl, ContractManagement, ProjectControls, Inventory, Operations, Administration, plus QuickSetup/ImportCenter) get module-level override capability only — identical in shape to what `MenuAccess` already provided, now with an admin-configurable exception layer on top.

### 3.4 Item→resource mapping (`NavigationCatalog.CmmsIntegrationItemResourceMap`)

Each CmmsIntegration sidebar item maps to the real `ResourceName` its own controller already uses in `HasPermissionAsync`/`HasActionAsync` (confirmed by reading each controller, not guessed). Three items — **Instrument, Asset, SparePart** — map to `null` because their controllers have no backend permission gate at all today; for these, no permission-derived default is computed and they remain always-visible when the module is visible, identical to pre-Phase-6 behavior. Inventing a permission signal for them would have been exactly the kind of fabrication the audit was told to avoid.

**Equipment is also intentionally left unmapped**, despite `EquipmentController` having a real `Equipment.View` gate. A live query against the dev database (not just the code-level seed, which only covers 5 of the ~20 real modules — the rest were configured through the Permission Matrix UI over the project's real history) found `Equipment.View` granted only to `MaintenanceEngineer`, not to `EngineeringManager` or `EngineeringSupervisor` — both of whom see the CmmsIntegration section, and Equipment inside it, today via the coarse `MenuAccess` gate. Mapping Equipment would have silently hidden it for those two roles the moment this phase shipped. This was caught and fixed **before** merge, not discovered in review — see §6 for the general lesson.

**Do not add a permission-derived default for any item without first querying the real `RolePermissionGrants` table for the roles that currently see it.** The code-level seed methods (`PermissionService.SeedDefaultsAsync`, `RoleModuleActionService.SeedDefaultsAsync`) are not a complete picture of what's actually granted in a working deployment.

---

## 4. Admin surfaces

`PEMS.Web/Areas/Admin/Controllers/NavigationVisibilityController.cs`, `SystemAdmin`-only, no new authorization logic — reads through `NavigationVisibilityResolver` (visibility) and writes through `INavigationVisibilityOverrideService` (the one override table).

**Update 2026-09-29 (same day, hierarchy correction):** the first version of this admin UI rendered a flat table — one selected role, one row per module/item — with the item's underlying capability shown as a "Source" column. That put a permission and a navigation node visually on the same footing and made "which roles see this section" impossible to answer without switching roles one at a time. It was corrected before any real use to a **navigation-first tree**, built by `PEMS.Web/Security/NavigationVisibilityTree.cs` (`NavigationVisibilityTreeBuilder`), which pivots per-role `DescribeForRoleAsync` output into `Module → [Subgroup →] Item → Role/Discipline` — the exact hierarchy `_SidebarMenu.cshtml` renders, read from `NavigationCatalog`, never invented from the permission catalog. The absolute rule this enforces: **Navigation Node ≠ Permission ≠ Role ≠ Discipline** — a permission is metadata on its node (`Capability: WorkOrder.View`, or `None - not permission-gated` for Instrument/Asset/SparePart/Equipment), never a sibling node; a role's access is always nested *under* its node as a checkmark pill, never a node of its own.

- **`Index` (Role/Discipline visibility explorer):** an expandable tree of every module — and, for CmmsIntegration, every item and its "Maintenance Planning" subgroup, exactly as the real sidebar groups them. Expanding an item shows every real role as a pill (✓/✕, override indicator) with inline Force-visible / Force-hidden / Clear actions — reading from *all* roles at once (`DescribeForRoleAsync` called once per role, pivoted), not one role picked from a dropdown.
- **`EffectiveUser` (User-level Effective Visibility + Preview As User):** the same tree shape, collapsed to one resolved ✓/✕ per node for a single chosen user (union of all their roles, via the exact same `ResolveAsync` the live sidebar calls) — **read-only, never touches the acting admin's own session, cookie, or identity.**

Both pages are linked from `_SidebarMenu.cshtml` under **Administration → User & Access → Navigation Visibility**, and are themselves subject to the same visibility layer as everything else (no special-casing).

**Corrected diagnostic accuracy bug (found by the correction's own Test 8):** `DescribeForRoleAsync` previously skipped computing CmmsIntegration item rows entirely whenever the module's own default+override was not visible for that role — so a brand-new role with a real `WorkOrder.View` grant but no `MenuAccess.CmmsIntegration` membership yet would show every CmmsIntegration item as hidden, hiding real signal from the admin. Since this method is diagnostic-only (never on the production `ResolveAsync` hot path the live sidebar uses), the module gate was removed from it — items are now always described accurately, regardless of the parent module's own current default.

---

## 5. Immediate reflection (no restart)

Every read goes through `NavigationVisibilityResolver`, which reads `RolePermissionGrant` (via `AccessControlService`) and `NavigationVisibilityOverride` live on every request — there is no caching layer to invalidate. A permission change made through the Phase 4 Permission Matrix, or a visibility override saved through §4's admin UI, is reflected on the very next sidebar render, consistent with the Phase 4 startup-drift fix (`AccessControlSeeder.SyncRoleFromRolePermissionsAsync`).

---

## 6. The general lesson this phase reinforced

**A code-level seed method is not the source of truth for what's actually granted in a real database.** `PermissionService.SeedDefaultsAsync` seeds exactly 5 modules (WorkOrder, StoreRequisition, WorkRequest, Inspection, Transmittal); everything else in the live `RolePermissionGrants` table for CmmsIntegration's ~20 other modules got there through real admin configuration via the Permission Matrix UI over the life of the project. Any future work that computes a *default* from "what a role should have" must query the live grants table for the roles actually affected, not infer it from the C# seeder. This is the same discipline the Phase 1-5 "Pre-Implementation Verification" step already established for permission work; this phase shows it applies equally to anything that reads permission state to compute a *different* derived default (here, visibility).

---

## 7. Test coverage

`PEMS.Tests/Authorization/NavigationVisibilityTests.cs` (12 tests, real seeded `PEMSDbContext`, no mocks — same convention as `AuthorizationArchitectureCompletionTests`):

- Default module visibility reproduces `MenuAccess` with zero overrides.
- Explicit hide/show overrides at module level; clearing an override reverts to default.
- Multi-role union semantics (a hide-override on one role never hides it for a user whose other role still defaults visible).
- CmmsIntegration item visible with a real grant; hidden for a role with zero grant for the mapped resource.
- Unmapped items (Instrument/Asset/SparePart) always visible when the module is visible.
- Item-level override hides one item without touching the rest of the module.
- `DescribeForRoleAsync` reports Default / Override / Effective as genuinely independent values.
- **`Hiding_an_item_via_NavigationVisibilityOverride_never_changes_the_real_authorization_decision`** — the direct Hidden ≠ Unauthorized proof: hides a module and item via override, then asserts `AccessControlService.AuthorizeForRolesAsync` returns the *exact same* `Allowed`/`Reason` before and after.
- **`Showing_an_item_via_NavigationVisibilityOverride_never_grants_the_real_authorization_decision`** — the complementary Visible ≠ Authorized proof: force-shows a module and item for a role with zero real grant, then asserts the decision is still Denied.

If either of the last two tests ever fails, visibility has leaked into the authorization decision path and must be reverted, not patched.

`PEMS.Tests/Authorization/NavigationVisibilityHierarchyTests.cs` (7 more tests, added with the hierarchy correction) — proves the absolute rule directly: no `ModuleKey`/`ItemKey` ever looks like a permission name (`Resource.Action`); `Capability` metadata never becomes a sibling navigation item; the real `Module → Subgroup → Item` sidebar hierarchy survives the tree build exactly; role access is always nested under its item, never a node of its own; the item-node count matches `NavigationCatalog` exactly regardless of how many roles/permissions exist (no permission-generated duplicates); Instrument/Asset/SparePart/Equipment stay unmapped; and — the one real bug this correction's own tests caught — changing Permission Matrix data through `PermissionService.SetPermissionsAsync` never adds or removes a navigation node, only flips a role's `EffectiveVisible` on the node that already existed.

Full regression: `PEMS.Tests/Authorization` folder — **532/532 passing** (19 of them Navigation Visibility). Full solution suite — **2035/2040 passing**; the 5 failures (`VDRImportServiceVendorResolutionTests`, `MrsImportBatchCapacityTests`, two `WorkOrderConcurrencyGateTests`, one `WorkRequestConcurrencyGateTests`) share no file, class, or code path with navigation/`MenuAccess`/`AccessControlService`; the 4 non-SQL ones were re-run in isolation with zero code change and passed 21/21, and the 5th failed on a raw SQL Server TDS/SNI connection error (infrastructure, not an assertion) — none are attributable to this work.

Live validation: Playwright, against the running dev server — login, sidebar render, Role-editor override save/clear round-trip, Preview-As-User, Persian/RTL rendering, 375px mobile width, plus (hierarchy correction) expanding the real tree and confirming no bare permission name renders as its own node. 9/9 passed on the hierarchy-specific pass.

---

## 8. Explicitly out of scope / deferred

- Item-level drill-down for modules other than CmmsIntegration (EngineeringManagement, ContractManagement, etc.) — module-level override only, by explicit scope decision (§3.3), not an oversight.
- A stable per-item key catalog for the ~80 sidebar links outside CmmsIntegration — not created; those items remain identified only by controller/action, exactly as before this phase.
- Automated empty-module suppression at the top module level (only the "Maintenance Planning" sub-heading inside CmmsIntegration gets empty-subgroup suppression) — a module becoming fully empty via admin override is a reachable but pathological configuration (an admin explicitly hiding every item in a module they left visible), not exercised by any real workflow, and left as a known, low-priority gap rather than added speculative logic.

---

## 9. Prohibited future additions (mirrors §22 of the Phase 1-5 baseline)

Do not, under any future PEMS work:

- Add a second authorization/RBAC engine, Role system, or permission table for navigation.
- Let `NavigationVisibilityOverride` or `NavigationVisibilityResolver` participate in an `[Authorize]` decision, a `HasPermissionAsync`/`HasActionAsync` check, or a workflow transition gate.
- Duplicate `MenuAccess`'s role arrays into a second, parallel visibility source instead of extending the one override table.
- Compute a permission-derived visibility default without first checking the live `RolePermissionGrants` table for the roles it will affect (§6).
- Treat "the sidebar link is visible" as evidence that an endpoint is safe to leave unauthorized, or "the sidebar link is hidden" as a reason to skip adding a real `[Authorize]`/permission check to a new controller.
