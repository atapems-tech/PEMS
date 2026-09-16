# PEMS Operations/CMMS Navigation Boundary Correction — Report

**Scope**: implements exactly the "required refactoring" identified in
`docs/audits/PEMS_OPERATIONS_ARCHITECTURE_AND_PERIOD_REPORTING_AUDIT.md`, Section 18, items
1-3 — nothing else. No domain entity, application service, controller *behavior*, state
machine, migration, Inventory/MaterialStock/InventoryTransaction/Batch/Quality/Barcode logic,
Packaging, Daily Operations, or Reporting code was touched.

---

## 1. Files Changed

| File | Change |
|---|---|
| `PEMS.Web/Security/MenuAccess.cs` | Added a new, independent `MenuAccess.Operations` role array |
| `PEMS.Web/Views/Shared/_SidebarMenu.cshtml` | Removed `"Production"`/`"ProductionOrder"`/`"ProductionRun"` from `cmmsControllers`; added a new `operationsControllers` array; removed the 2 Production nav-links from inside the `CMMS Integration` accordion block; added a new, independent `Operations & Production` accordion block (own `nav-section-title`, own icon, own `#sec-operations` collapse target), gated by `MenuAccess.Operations` |
| `PEMS.Web/Controllers/ProductionController.cs` | `ViewData["Section"]`: `"CMMS Integration"` → `"Operations & Production"` (1 occurrence) |
| `PEMS.Web/Controllers/ProductionOrderController.cs` | Same section-label change (3 occurrences: `Index`, `Details`, `Create`) |
| `PEMS.Web/Controllers/ProductionRunController.cs` | Same section-label change (3 occurrences: `Details`, `Create`, `BatchDetails`) |

**Nothing else was modified.** No entity, migration, permission-seed, SoD policy, or service
file was touched — confirmed by `git status` showing only these two modified files plus the
three already-untracked (never committed) controller files from the prior Phase 1 task.

---

## 2. Before/After Navigation Structure

**Before** (as built in Phase 1 — the actual bug this task fixes):
```
Sidebar
├── ... (Engineering, AI & Knowledge, Correspondence, Quality Control, Contract Management, Project Controls)
├── CMMS Integration                          [MenuAccess.CmmsIntegration]
│   ├── Equipment
│   ├── ... (all real CMMS entries) ...
│   ├── Barcode
│   ├── Production Dashboard                  ← wrong: Operations link inside CMMS accordion
│   └── Production Orders                     ← wrong: Operations link inside CMMS accordion
├── Inventory & Materials                     [MenuAccess.CmmsIntegration, shared gate]
├── Administration
```

**After**:
```
Sidebar
├── ... (Engineering, AI & Knowledge, Correspondence, Quality Control, Contract Management, Project Controls)
├── CMMS Integration                          [MenuAccess.CmmsIntegration]  — unchanged, no Production links
│   ├── Equipment
│   ├── ... (all real CMMS entries, untouched) ...
│   └── Barcode
├── Operations & Production                   [MenuAccess.Operations, NEW, independent]
│   ├── Dashboard              → ProductionController.Dashboard
│   └── Production Orders      → ProductionOrderController.Index
├── Inventory & Materials                     [MenuAccess.CmmsIntegration, unchanged — see Section 8]
├── Administration
```

Only the two routes that actually exist as standalone index/dashboard pages were linked
(`ProductionController.Dashboard`, `ProductionOrderController.Index`). No link was added for
"Production Runs" or "Production Batches" — neither controller has a standalone `Index`
action (a Run is only reached from an Order's Details page; a Batch is only reached from a Run
or from a resolved barcode scan) — and no "Operations Reports" link was added, since no
reporting controller/action exists yet. This follows the task's explicit "do not invent
routes" instruction.

---

## 3. Controller Section Changes

All three controllers previously set `ViewData["Section"] = "CMMS Integration"`; all now set
`ViewData["Section"] = "Operations & Production"`, matching the new sidebar accordion's label
exactly (so the breadcrumb text a user sees on a Production page now agrees with the sidebar
section it lives under, rather than contradicting it as before).

`ViewData["Title"]` (the actual page title, e.g. `"Production Dashboard"`,
`"Production Order {number}"`) was **not** changed on any page — only the section/breadcrumb
label was corrected. Routes, action names, and view paths are unchanged (no route touched, no
`[Route]`/`asp-action`/`asp-controller` attribute was edited anywhere).

`_Breadcrumb.cshtml` (confirmed by re-reading it) renders `ViewData["Section"]` as a plain,
non-link text span between the Dashboard link and the page title — it has no logic that
branches on the literal string value, so this change is a pure text substitution with no
downstream effect on breadcrumb structure, links, or behavior.

---

## 4. Permission Compatibility Decision

**Decision: the permission module name stays `"Production"`. It was not renamed.**

Rationale:
- Identified all usages of the `"Production"` permission module string before deciding:
  `PermissionService.Modules` (the flat module-name array), `PermissionService`'s
  `SeedMissingRolesAsync("Production", ...)` seeding block, the SoD policy pair
  `Production.Create ↔ Production.Approve` / `Production.Edit ↔ Production.Approve` in
  `AccessControlSeeder.SeedPhase5GovernanceAsync`, and every `HasPermissionAsync(roles,
  "Production", "...")` call across the three controllers. All of these are **internal,
  persisted or seed-derived keys** — never shown to an end user — so there is no user-facing
  inconsistency in keeping them as `"Production"` while the UI-facing label reads
  "Operations & Production."
- Renaming the module string would require either (a) an `UPDATE RolePermissions SET
  ModuleName = 'Operations' WHERE ModuleName = 'Production'` data migration reaching into
  already-seeded rows (and the corresponding `Permission`/`RolePermissionGrant` rows the newer
  RBAC-ABAC layer auto-derives from them), or (b) maintaining both names simultaneously as a
  compatibility shim — both are unnecessary complexity for a change that is, by the prior
  audit's own finding, confined to labels and navigation, not permission semantics.
- This is the smallest safe change: the permission *key* is decoupled from the *display label*
  exactly the way `MenuAccess.CmmsIntegration` already is (it gates both the "CMMS Integration"
  section **and** the "Inventory & Materials" section under one key, despite their different
  display names) — so having a permission module named `"Production"` back a sidebar section
  displayed as "Operations & Production" is consistent with an existing pattern in this
  codebase, not a new inconsistency.
- If `"Operations"` is ever specifically required as the persisted key (e.g., because a future
  Operations sub-module needs its own distinct permission identity separate from
  `"Production"`), that is a deliberate, separately-scoped decision for a later phase — not
  something this contained UI-boundary task should force.

**No permission code was changed.** `PermissionService.cs`, `AccessControlSeeder.cs`, and every
`HasPermissionAsync` call site are untouched.

---

## 5. Confirmation: No Domain/Application Dependency on CMMS Introduced

This task added or changed:
- One new `static readonly string[]` array in `MenuAccess.cs` (a `PEMS.Web`-layer,
  UI-only construct — no reference to `PEMS.Domain`/`PEMS.Application`/`PEMS.Persistence`).
- Sidebar markup and array reshuffling in `_SidebarMenu.cshtml` (a Razor view — no C# service
  or domain code).
- Three string-literal assignments (`ViewData["Section"] = "..."`) in three controllers — no
  new dependency injected, no new `using`, no new method call.

`ProductionOrderService`/`ProductionRunService` (the actual Production domain/application
logic) were **not opened or modified** in this task. Their dependencies remain exactly what
they were after Phase 1: `PEMSDbContext`, `INumberingService`, `IMaterialStockService`
(Inventory) — confirmed unchanged since no file under `PEMS.Persistence/Servises/Production*`
or `PEMS.Domain/Entities/Production*` appears in this task's change list (Section 1). The
finding from the architecture audit — that Operations' domain/application layer was already
independent of CMMS, and only its navigation/breadcrumb layer was coupled — is unchanged and
now fully corrected at that layer too.

---

## 6. Build Result

```
dotnet build PEMS.slnx --configuration Debug
→ Build succeeded, 0 errors, 12 warnings (all pre-existing, none in any file this task touched)
```

One issue was found and fixed during this task before the build was run: the first edit to
`_SidebarMenu.cshtml` left a Razor comment (`@* ... *@`) unclosed around the new section's
explanatory comment, which would have silently swallowed the entire new `@if` block (and
everything after it in the file) as commented-out text. This was caught by re-reading the file
immediately after the edit (not by the build, which would not have failed — an unclosed Razor
comment does not produce a compiler error, it just silently removes markup) and corrected
before building. Re-verified by reading the corrected file and by the clean build result above.

---

## 7. Test Result

**Directly relevant tests** (Production/Barcode — the only test classes that exercise any code
adjacent to this change):
```
dotnet test PEMS.Tests/PEMS.Tests.csproj --no-build --filter "FullyQualifiedName~ProductionOrderServiceTests|FullyQualifiedName~ProductionRunServiceTests|FullyQualifiedName~BarcodeServiceTests"
→ Passed! Failed: 0, Passed: 47, Skipped: 0, Total: 47, Duration: 10s
```

**Full regression suite**:
```
dotnet test PEMS.Tests/PEMS.Tests.csproj --no-build
→ Passed! Failed: 0, Passed: 1354, Skipped: 0, Total: 1354, Duration: 11m 33s
```
(Launched as a background process because it exceeded this task's interactive command window;
confirmed completed with exit code 0 before this report was finalized — no result was
fabricated or assumed ahead of the actual run finishing.) 1354 = the same total confirmed at
the end of the prior Phase 1 task. **Zero regressions, zero new failures, zero skipped.**

---

## 8. Remaining Architectural Risks

1. **`Inventory & Materials` still shares `MenuAccess.CmmsIntegration`** with the actual CMMS
   Integration section (a pre-existing condition, not introduced or worsened by this task) —
   the architecture audit flagged this as an explicit open question (Section 4.3 of the audit)
   deliberately left unresolved, since fixing Operations does not require fixing Inventory's
   longer-standing, separate coupling. Not addressed here, per the task's own scope limits.
2. **Permission module `"Production"` vs. display label "Operations & Production"** is a
   deliberate, documented mismatch (Section 4 above) — acceptable and precedented, but worth
   knowing about if a future engineer greps for `"Operations"` in permission code and finds
   nothing; the mapping is `"Production"` (permission key) ↔ `"Operations & Production"` (UI
   label), not a 1:1 name match.
3. **`MenuAccess.Operations` visibility vs. actual controller-level permission** were aligned
   deliberately (both include exactly `SystemAdmin`/`MaintenanceEngineer`/`QAManager`/
   `EngineeringManager`/`Viewer` — the same roles seeded with any `"Production"` permission
   grant) but this alignment is manual, not enforced by any compile-time or test-time check —
   if a future change alters `PermissionService`'s `"Production"` role grants without a
   matching update to `MenuAccess.Operations`, the sidebar link and the actual permission could
   drift out of sync (a link visible to a role that then gets `Forbid()`, or a role with real
   access that never sees the link). This same risk already exists for every other
   `MenuAccess` array in this codebase (it is a pre-existing pattern limitation, not something
   this task introduced), noted here for completeness.
4. Everything else the original architecture audit flagged (Sections 5-17 of that document —
   Product Master extension, period-based reporting, Packaging, `MaterialStock` batch/quality
   visibility, Daily Operations record design, calendar/shift semantics) remains fully
   unaddressed, as this task's scope explicitly excluded them.

---

No commit or push was made as part of this task.
