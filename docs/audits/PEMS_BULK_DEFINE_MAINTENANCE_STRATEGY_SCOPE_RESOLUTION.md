# PEMS — Bulk Define Maintenance Strategy: Scope & Location/Equipment Resolution

**Status:** Implemented and verified. Extends the existing `MaintenanceStrategyController.BulkCreate`
(GET) action only. No new architecture, no new migration, no parallel assignment relationship.

## 1. What changed

`BulkCreate` (GET) already resolved a candidate Equipment set from Discipline / EquipmentType /
Functional Location (exact) / Tag partial-search filters ("Phase 7.5"/"Phase 7.6"). This work extends
that same resolution step — the same `IQueryable<Equipment>` filter chain — with:

- **Plant / Area / Unit** filters (exact string match against `FunctionalLocation.Plant/Area/Unit`).
- **Functional Location Exact vs Descendants** mode, using a new
  `IFunctionalLocationService.GetDescendantIdsAsync(ids, includeSelf)` method that walks the real
  `FunctionalLocation.ParentId`/`Children` tree (BFS, cycle-guarded) — not a string-prefix assumption
  on `Code`.
- **Tag List** — an exact, multi-value, newline/comma/semicolon-separated tag box, distinct from the
  pre-existing partial-text `tagSearch` box.

Nothing else in the controller, the `BulkCreate` POST (commit) action, `Deactivate`, `Create`, or
`Index` was touched. No new entity, no new table, no new permission module.

## 2. Supported scope types and how each resolves

| Scope | Field(s) | Match rule |
|---|---|---|
| Plant | `FunctionalLocation.Plant` (flat string) | Exact string match, multi-select OR |
| Area | `FunctionalLocation.Area` (flat string) | Exact string match, multi-select OR |
| Unit | `FunctionalLocation.Unit` (flat string) | Exact string match, multi-select OR |
| Functional Location | `FunctionalLocation.ParentId`/`Children` (real tree) | Exact (selected node only) or Descendants (selected node + all real descendants) |
| Equipment | `Equipment.EquipmentId` | Direct checkbox selection (already existing UI, unchanged) |
| Tag | `Equipment.TagNumber` (authoritative identity field — confirmed 100% populated on live data, unlike `FunctionalLocationId`) | Exact match only, multi-value list, case-insensitive dedup |

**Why Plant/Area/Unit have no Exact/Descendants distinction:** direct schema inspection confirmed
`FunctionalLocation.Plant`/`Area`/`Unit` are flat string attributes on the row, not separate lookup
entities and not part of the parent/child tree. There is nothing to descend into — only the
`FunctionalLocation` self-reference (`ParentId`) is a real hierarchy, so only it gets a
Exact/Descendants mode. This is a repository fact, not a design choice.

**Tag identity:** `Equipment.TagNumber` was confirmed (not assumed) as the authoritative tag field —
it is the field already used for the pre-existing `tagSearch` partial filter and is populated on all
live Equipment rows, unlike `FunctionalLocationId` (sparse).

## 3. Filter combination rule

- Discipline, EquipmentType, Functional Location (Exact-or-Descendants), Plant, Area, Unit, and the
  existing partial `tagSearch` are **narrowing filters** — they combine with **AND** across dimensions
  and **OR** within a dimension's multiple selected values. This is the pre-existing Phase 7.6
  convention, unchanged and simply extended to the three new dimensions.
- The new **Tag List** is a **UNION-add**, not an AND-narrowing filter: equipment whose `TagNumber`
  exactly matches a requested tag are always included in the result, regardless of what the other
  filter dimensions are set to. Rationale (repository did not define this case, so the smallest
  deterministic rule was established): a user pasting known tags expects those exact assets back, not
  to have them silently dropped by an unrelated Discipline/Type filter they did not intend to apply to
  that specific list. This is documented in-line in the controller.
- An empty scope (no filter of any kind, including an empty Tag List) is the pre-existing "no filter →
  show nothing" guard (Phase 7.6, unchanged) — never the full ~40,809-row fleet.
- A Tag List with no matching equipment is a **safe zero result**, not an error and not treated as
  "no filter applied" (there was a filter — it just matched nothing).
- The 200-row result cap (`maxResults`) and the `MatchingCount` total (now the union of narrowing-match
  count + non-overlapping tag-match count) are both unchanged in mechanism, just computed over the
  extended candidate set.

## 4. Duplicate / overlap resolution

All scope dimensions resolve into a single `HashSet<int>` of `EquipmentId`s before the final
equipment list is loaded. Selecting overlapping scopes (e.g., a parent Functional Location in
Descendants mode plus one of its own children, or a Tag List entry that also matches a narrowing
filter) never produces duplicate rows and never double-counts in `MatchingCount` — verified by
dedicated tests (see §6).

## 5. Assignment / conflict / authorization behavior (unchanged, reused)

The `MaintenanceStrategy` domain has no separate "master strategy" to assign against — each row is a
per-Equipment strategy definition. The existing (unmodified) `BulkCreate` POST action already:

- Re-validates that submitted Equipment IDs are still active server-side (does not trust the GET's
  candidate list).
- Skips (does not duplicate, does not silently overwrite) any Equipment that already has an **active**
  strategy with the same `FailureModeCode` — the pre-existing conflict key established since
  "RcmOperationalBridgeService" (Phase 6.1).
- Reports skip counts back to the user (existing-active-strategy count, inactive/invalid count).

This scope-resolution work only changes which Equipment IDs get **offered** in the GET picker; it does
not change how the POST commits or resolves conflicts, so no new conflict logic was needed or added.

Authorization is unchanged: `BulkCreate` (GET and POST) still requires
`"MaintenanceStrategy"`/`"Approve"` (Supervisor/Manager tier, not MaintenanceEngineer) — the
highest-risk gate in this controller, per the pre-existing Phase 7.5/7.6 audits. The new scope
parameters are validated and resolved entirely server-side before the permission-gated view renders;
there is no client-side-only enforcement.

## 6. Audit

No new audit code was added. `PEMSDbContext.SaveChangesAsync`'s existing `ChangeTracker`-driven
`AuditLog` override automatically records every `MaintenanceStrategy` insert made by the (unmodified)
POST action, same as before this work.

## 7. Database / schema

**No migration was created or required.** `FunctionalLocation.Plant`/`Area`/`Unit` and the
`ParentId`/`Children` self-reference already existed in the schema before this work. Only C# code was
added: one new interface method (`IFunctionalLocationService.GetDescendantIdsAsync`), its
implementation, and the controller/view extensions consuming it.

## 8. Performance

- `GetDescendantIdsAsync` loads only `{FunctionalLocationId, ParentId}` projections from the
  `FunctionalLocation` table (confirmed 32 rows on live data) once per request, builds an in-memory
  parent→children map, then does a single BFS pass — no per-equipment or per-node repeated query.
- Equipment resolution still uses a single `IQueryable` chain per narrowing pass (`CountAsync` +
  capped `Take(200)` projection to IDs), and a single additional query for exact tag matches — no
  per-row `SaveChanges`, no N+1, no full 40,809-row Equipment load. The GET action never writes to the
  database at all.

## 9. Test coverage

Added:
- `PEMS.Tests/Cmms/FunctionalLocationDescendantResolutionTests.cs` (8 tests) — `GetDescendantIdsAsync`
  directly: empty input, leaf node with/without `includeSelf`, multi-level tree (all depths returned),
  exclude-self at a mid-level node, multiple seed nodes unioned without duplication, overlapping
  parent+child seeds (no duplication/no infinite loop), a nonexistent seed ID (no crash).
- `PEMS.Tests/Cmms/BulkDefineMaintenanceStrategyTests.cs` (+11 tests) — Plant filter, Area filter, Unit
  filter, Functional Location Exact mode (no child leakage), Functional Location Descendants mode
  (multi-level, includes all depths, excludes unrelated equipment), overlapping FL selections
  (dedup), Tag List union-add across an unrelated narrowing filter, Tag List exact-match-only (no
  partial match), Tag List/narrowing-filter overlap counted once in `MatchingCount`, Tag List with a
  nonexistent tag (safe zero result, not treated as "no filter"), and Plant+Area+Unit combined as AND
  dimensions.

Also fixed (required for the test project to even compile after the `IFunctionalLocationService`
interface change): a pre-existing `ThrowingFunctionalLocationService` test fake in
`EquipmentControllerAuthorizationTests.cs` was missing the new interface member; added a
`throw new NotImplementedException()` stub consistent with the rest of that fake.

**Results:**
- Targeted run (`BulkDefineMaintenanceStrategyTests` + `FunctionalLocationDescendantResolutionTests` +
  `MaintenanceStrategyControllerAuthorizationTests`): **40/40 passed**.
- Full regression (`dotnet test PEMS.Tests/PEMS.Tests.csproj`): **1258/1258 passed** (prior baseline
  was 1239; 19 new tests added by this work — 11 + 8 — account for the full delta, no other test
  count changed).

## 10. Files changed

- `PEMS.Application/Interfaces/IFunctionalLocationService.cs` — added `GetDescendantIdsAsync`.
- `PEMS.Persistence/Servises/FunctionalLocationService.cs` — implemented `GetDescendantIdsAsync`.
- `PEMS.Web/Controllers/MaintenanceStrategyController.cs` — extended `BulkCreate` GET only (new
  optional parameters with defaults so existing callers/tests keep compiling unchanged).
- `PEMS.Web/Views/MaintenanceStrategy/BulkCreate.cshtml` — added Plant/Area/Unit/FL-mode/Tag-List
  controls; fixed a stale hardcoded "21,625" equipment count in the "no filter applied" message.
- `PEMS.Tests/Cmms/FunctionalLocationDescendantResolutionTests.cs` — new.
- `PEMS.Tests/Cmms/BulkDefineMaintenanceStrategyTests.cs` — extended.
- `PEMS.Tests/Authorization/EquipmentControllerAuthorizationTests.cs` — one-line fake-interface fix.

Not created: no `GenericScopeEngine`, no `GenericBulkAssignmentFramework`, no new Stage→Review→Commit
batch (not needed — this feature only narrows a picker, it does not stage/commit anything new), no
Resource Header changes, no changes to the Equipment↔Functional Location Assignment workstream.
