# PEMS CMMS — Data Migration & Controlled Data Onboarding

**Status:** Foundation implemented for one entity (MaintenanceHistory) and verified with synthetic
test data. **No real legacy CMMS source dataset was available in this repository**, so no actual
production migration has been run — see "Known Limitations" below.

## 1. Repository-first audit findings

Before writing any code, the repository was searched for:

- **An actual legacy CMMS export** (Excel/CSV/Access/SQL backup) to migrate from. None exists.
  The only `.xlsx` files present belong to *other*, already-implemented PEMS features (EDMS
  document intake, MRS material import) — not a legacy CMMS dataset. `PEMS.Web/DATA/` contains
  leftover scaffold files (`ApplicationDbContext.cs`, `SeedData.cs`), not source data. No named
  legacy system (SAP PM, Maximo, eMaint, etc.) is referenced anywhere in the repo or its docs.
  **Conclusion: Source Format = the repository's own established convention (Excel), Source
  Dataset = NOT AVAILABLE.**
- **Existing import/staging patterns.** Five prior Stage → Review → Commit pipelines already
  exist: `EquipmentImportBatch/Row`, `FunctionalLocationImportBatch/Row`,
  `EquipmentFunctionalLocationAssignmentBatch/Row`, `MrsImportBatch/Row` (Inventory), and
  `MDRImportBatch/Row`/`VDRImportBatch/Row` (EDMS — out of scope for this workstream). All follow
  an identical shape: a Batch (file/date/status) + Row (raw JSON payload + resolution status),
  Excel parsed with ClosedXML using auto-detected headers, deterministic tag/code matching (no
  fuzzy/AI), explicit human review before commit, and a single `SaveChangesAsync` at commit time.
  This workstream **extends that exact shape** — no new architecture was created.
- **Which CMMS entities have no existing import path.** Of the entities listed in the task
  (Equipment, FunctionalLocation, MaintenanceStrategy, MaintenancePlan, MaintenanceHistory,
  WorkRequest, WorkOrder, FailureRecord, EquipmentCriticality, SparePart, Vendor…), Equipment,
  FunctionalLocation, and Equipment↔FunctionalLocation already have dedicated import pipelines.
  **MaintenanceHistory had no controller, no service, and no import path at all** — it was only
  ever read (one `ViewBag` lookup in `InstrumentController`), never written outside of test seed
  data. It is explicitly listed as CMMS-core in `.claude/CLAUDE.md`'s "append-only" convention,
  sits next in the dependency chain after Equipment↔FL, and — unlike MaintenanceStrategy — is not
  adjacent to the just-closed "Bulk Define Maintenance Strategy" workstream, so building it carries
  no risk of reopening that closed work.

## 2. Why MaintenanceHistory, and why only one entity

Per the task's own instruction ("do not attempt to import every CMMS entity in one uncontrolled
operation... identify the smallest useful migration slice"), this workstream implements **one**
new entity's onboarding path: historical maintenance/repair records. It was chosen because it is:
the next unimplemented slice in the stated dependency order, structurally simple (a flat record
referencing Equipment required and WorkOrder optional — no scheduling/PM-calculation logic), and
explicitly protected by the repository's own append-only convention, so a staged/reviewed import
is a natural fit rather than a risky addition.

MaintenanceGroup/Type, MaintenanceStrategy, MaintenancePlan, WorkRequest, WorkOrder, SparePart, and
Vendor still have no import path after this workstream — building them was out of scope for "the
smallest useful migration slice" and is left for a future, separately-scoped workstream.

## 3. Implemented flow

```
Excel file (Equipment Tag / Execution Date / optional Work Order Number,
Finding, Action Taken, Technician, Duration Hours, Cost, Description)
        │
        ▼  Upload (permission: Create)
Stage — MaintenanceHistoryImportService.StageFromExcelAsync
        │  header auto-detection, per-row deterministic resolution
        ▼
Validate + Resolve + Reference Resolution (all in one pass, in memory,
against a single pre-loaded Equipment/WorkOrder lookup - no per-row query)
        │
        ▼  Review — MaintenanceHistoryImportController.Review (permission: View)
        │  filter by status, toggle individual rows in/out (permission: Create)
        ▼
Approve — reviewer selects which rows to include
        │
        ▼  Commit (permission: Approve — highest-risk action in this pipeline)
MaintenanceHistoryImportService.CommitBatchAsync
        │  re-checks idempotency against the live DB at commit time (not just
        │  the staging-time snapshot), single SaveChangesAsync for all inserts
        ▼
MaintenanceHistory rows created (append-only)
        │
        ▼  Audit — automatic via PEMSDbContext.SaveChangesAsync's existing
           ChangeTracker-based AuditLog interceptor (no new audit code)
```

Discard (permission: Delete) removes an un-committed staging batch entirely; it never touches
already-committed `MaintenanceHistory` rows.

## 4. Source-to-PEMS mapping

| Source column (header, case/spacing-insensitive) | Target field | Required | Resolution rule |
|---|---|---|---|
| Equipment Tag / Tag / TagNumber / Equipment | `MaintenanceHistory.EquipmentId` | Yes | Exact match on `Equipment.TagNumber` (case-insensitive). 0 matches → `UnresolvedEquipment`. >1 matches → `ConflictEquipment` (schema has no uniqueness constraint on `TagNumber`, same fact already established by the Equipment↔FL Assignment workstream). |
| Execution Date / Date / Maintenance Date / Service Date | `MaintenanceHistory.ExecutionDate` | Yes | Parsed as a real Excel date cell when the cell is date-typed, else `DateTime.Parse` on the text. Unparseable/empty → `ValidationError`. |
| Work Order Number / Work Order / WO / WONumber | `MaintenanceHistory.WorkOrderId` | No (soft reference) | Exact match on `WorkOrder.WorkOrderNumber`. Unresolved or ambiguous → **not an error** — the row still commits as `New`, `WorkOrderId` is left `null`, and a `Warning` explains why (the field is nullable in the schema; leaving it blank is safer than guessing). |
| Finding | `MaintenanceHistory.Finding` | No | Passed through as-is. |
| Action Taken / Action | `MaintenanceHistory.ActionTaken` | No | Passed through; also part of the idempotency key (see §5). |
| Technician / Performed By / Executed By | `MaintenanceHistory.Technician` | No | Passed through; also part of the idempotency key. |
| Duration Hours / Duration | `MaintenanceHistory.DurationHours` | No | Parsed as `decimal`; unparseable non-empty values are dropped to `null` (never silently coerced to `0`) — no dedicated error status, since this is a non-identity informational field. |
| Cost | `MaintenanceHistory.Cost` | No | Same rule as Duration Hours. |
| Description / Remarks / Notes | `MaintenanceHistory.Description` | No | Passed through as-is. |

No AI/fuzzy matching anywhere — every resolution is an exact, case-insensitive string match against
a field the repository already treats as authoritative (`Equipment.TagNumber`,
`WorkOrder.WorkOrderNumber`).

## 5. Identity / idempotency rule

`MaintenanceHistory` has no pre-existing natural business key (unlike `Equipment.TagNumber` or
`FunctionalLocation.Code`). The repository does define an analogous precedent, though:
`MaintenanceStrategy` treats `Equipment + FailureModeCode` as its de facto duplicate-detection key.
Following that precedent (task instruction: "where the repository does not define a rule,
establish the smallest deterministic rule necessary and document it"), this workstream uses:

```
(EquipmentId, ExecutionDate.Date, NormalizedActionTaken, NormalizedTechnician)
```

(`Normalized` = trimmed, case-folded — deterministic normalization only, never fuzzy matching.)

- **First import:** no existing row matches this key → `New`, committable by default.
- **Repeated same import:** the second occurrence — whether in the same file or a later
  re-upload — resolves to `Duplicate` at staging time and is **excluded from commit by default**.
  Re-verified again at commit time against the live database (not just the staging-time snapshot),
  so a duplicate created by a *different* import between staging and commit is still caught and
  skipped, never double-inserted.
- **Existing PEMS record:** same as "repeated import" — flagged `Duplicate`, not silently merged
  or overwritten (nothing is ever overwritten; `MaintenanceHistory` is append-only).
- **Reviewer override:** unlike `UnresolvedEquipment`/`ConflictEquipment`/`ValidationError` (which
  can never be force-included), a `Duplicate` row *can* be explicitly re-included by an authorized
  reviewer — because two genuinely distinct maintenance events can coincidentally share this key
  (e.g., two different but identically-described inspections), and only a human reviewer can make
  that call. This is a deliberate, documented exception, not an accidental gap.
- **Known limitation:** because there is no stable source-record identifier, two *legitimately
  different* history entries with blank/identical Action Taken and Technician on the same day for
  the same equipment will collide on this key and require manual reviewer judgment. This is called
  out explicitly rather than hidden.

## 6. Conflict handling

| Situation | Status | Committable by default | Blockable by reviewer override |
|---|---|---|---|
| Tag resolves to exactly one active Equipment | — | — | — |
| Tag resolves to zero Equipment | `UnresolvedEquipment` | No | No (defensively enforced in both `SetRowIncludeAsync` and `CommitBatchAsync`) |
| Tag resolves to more than one Equipment | `ConflictEquipment` | No | No |
| Required field missing/invalid (Tag or Date) | `ValidationError` | No | No |
| Composite key already exists (DB or same file) | `Duplicate` | No | Yes, explicit |
| Work Order Number given but unresolved/ambiguous | `New` (Warning only) | Yes | — |
| Everything resolves cleanly | `New` | Yes | — |

## 7. Authorization

New, independent permission module `"MaintenanceHistoryImport"` — same role tier as every sibling
import (`EquipmentImport`, `FunctionalLocationImport`, `EquipmentFunctionalLocationAssignment`):
`MaintenanceEngineer` and `EngineeringManager` get Create/Delete/Approve, `Viewer` gets read-only
View. `Commit` (the only action that writes real `MaintenanceHistory` rows) requires `Approve` —
the same "highest-risk action needs the highest gate" convention already established for every
other import in this repository. All checks are server-side in the controller; there is no
client-side-only enforcement, and a crafted direct POST to `Commit`/`ToggleRow` without the
`Approve`/`Create` permission is rejected with `403 Forbid` (proven by
`MaintenanceHistoryImportControllerAuthorizationTests`).

## 8. Audit

No new audit code. `PEMSDbContext.SaveChangesAsync`'s existing `ChangeTracker`-driven `AuditLog`
override automatically records every `MaintenanceHistoryImportBatch`/`Row`/`MaintenanceHistory`
insert made by this pipeline, exactly as it already does for every other entity in PEMS.

## 9. Database changes

**One migration, purely additive:** `20260914110633_AddMaintenanceHistoryImport` — creates
`MaintenanceHistoryImportBatches` and `MaintenanceHistoryImportRows` only. No existing table is
altered, no column is dropped, no data is touched. Verified by direct inspection of the generated
migration file: two `CreateTable` calls, two `CreateIndex` calls, no `DropTable`/`DropColumn`/
`Sql(...)` of any kind. FKs: `Batches.ImportedById → Employees` (Restrict), `Rows.BatchId →
Batches` (Cascade — deleting a staging batch removes its own rows, never anything else).
`ResolvedEquipmentId`/`ResolvedWorkOrderId`/`DuplicateOfMaintenanceHistoryId`/
`CommittedMaintenanceHistoryId` intentionally have no DB-level FK — matching the exact existing
convention already used by `EquipmentImportRow.CreatedEquipmentId` and
`EquipmentFunctionalLocationAssignmentRow.ResolvedEquipmentId`. Migration applied to the local dev
database (`dotnet ef database update`) and confirmed successful.

## 10. Performance and scale

- Equipment and WorkOrder lookups are each loaded **once per Stage call** (not per row), matching
  the exact existing convention in `EquipmentFunctionalLocationAssignmentService`.
- The idempotency-key lookup against existing `MaintenanceHistory` rows is deliberately **scoped
  to only the Equipment IDs that actually appear in the uploaded file**, not the whole
  `MaintenanceHistories` table — this table has no known upper bound (the task itself notes
  historical data "may be substantially larger" than the 40,809-row Equipment table), so an
  unscoped full-table load would be a real anti-pattern at true migration scale, even though the
  sibling importers can safely preload their entire (bounded, ~40K-row) Equipment table.
  `CommitBatchAsync` re-runs this same scoped lookup at commit time.
- Commit uses at most two `SaveChangesAsync` calls for the entire batch (one to insert all new
  `MaintenanceHistory` rows and generate their identities, one to stamp
  `CommittedMaintenanceHistoryId` back onto the staging rows) — never one call per row.
- The Review page is server-side paginated (`GetBatchAsync(batchId, page, pageSize, statusFilter)`),
  same as every other import review page in this repository.

## 11. Existing data protection

- No existing `Equipment`, `FunctionalLocation`, `MaintenanceStrategy`, `MaintenancePlan`,
  `WorkOrder`, or `AuditLog` row is read-write touched by this pipeline at all — it only ever
  *creates* new `MaintenanceHistory` rows.
- No existing `MaintenanceHistory` row is ever updated or deleted by this pipeline — matching the
  append-only convention `.claude/CLAUDE.md` explicitly mandates for maintenance/calibration
  history.
- Discarding a staging batch only removes its own `MaintenanceHistoryImportBatch`/`Row` staging
  data — never a committed `MaintenanceHistory` record.

## 12. Test coverage

- `PEMS.Tests/Cmms/MaintenanceHistoryImportServiceTests.cs` (19 tests) — valid input staging,
  missing Tag, invalid date, duplicate rows within one file, blank rows silently skipped, an
  existing DB record with the same key flagged as `Duplicate`, ambiguous Tag flagged
  `ConflictEquipment`, unresolved Equipment, resolved Work Order, unresolved Work Order (warning
  only, still committable), a `ValidationError` row that cannot be force-included, a `Duplicate`
  row that *can* be explicitly force-included by a reviewer, a successful commit, a repeated
  full-file import creating nothing the second time, a record that became a duplicate between
  staging and commit (caught and skipped, not duplicated), an excluded row not committed, partial
  approval (only approved rows committed), no orphan `WorkOrderId` when unresolved, and batch
  discard removing only staging data.
- `PEMS.Tests/Authorization/MaintenanceHistoryImportControllerAuthorizationTests.cs` (9 tests) —
  every action's permission gate, including proof that a role with no module permission at all is
  rejected on `Commit` (the highest-risk action) and that `MaintenanceEngineer` (which does have
  `Approve`) passes the gate and reaches real business logic.

**Results:**
- Targeted run (`MaintenanceHistoryImport*`): **28/28 passed**.
- Full regression (`dotnet test PEMS.Tests/PEMS.Tests.csproj`): **1286/1286 passed** (prior
  baseline was 1258; the 28 new tests above account for the full delta, no other test count
  changed).

## 13. Known limitations (explicit, not hidden)

- **No real legacy CMMS source file was available anywhere in this repository or project
  context.** Everything above was built and verified against synthetic, hand-built test workbooks
  (via ClosedXML in the test project), not a real production export. No claim is made that an
  actual customer/legacy-system migration has been run. Before this pipeline is used against real
  legacy data, someone with access to that export should run a small pilot batch through Stage →
  Review and manually inspect the resolution results before ever clicking Commit.
- Only `MaintenanceHistory` was implemented. MaintenanceGroup/Type, MaintenanceStrategy,
  MaintenancePlan, WorkRequest, WorkOrder, SparePart, FailureRecord, EquipmentCriticality, and
  Vendor still have no dedicated import/migration path — each would need its own separately-scoped
  workstream, following this same pattern.
- The composite idempotency key (Equipment + Date + Action + Technician) is a best-effort
  deterministic rule in the absence of a real source identifier (see §5's limitation note) — it is
  not a guarantee against every conceivable false-positive/false-negative duplicate, only against
  exact re-imports and exact-content collisions.
- Numeric parsing (`DurationHours`/`Cost`) silently drops unparseable values to `null` with no
  dedicated error status — this was a deliberate choice (these are non-identity, informational
  fields) but means a badly-formatted number in an otherwise-good row will not block that row from
  importing; the value is simply left blank.
- `SaveChangesAsync` transactional failure behavior (e.g., a mid-commit DB outage) was not tested —
  EF Core's InMemory provider used by the test suite cannot simulate this; the existing
  `SaveChangesAsync` semantics (single atomic call per phase) are relied on, same as every sibling
  import in this repository.

## 14. Files changed

- `PEMS.Domain/Entities/MaintenanceHistoryImportBatch.cs` — new.
- `PEMS.Domain/Entities/MaintenanceHistoryImportRow.cs` — new.
- `PEMS.Application/DTOs/MaintenanceHistoryImportDtos.cs` — new.
- `PEMS.Application/Interfaces/IMaintenanceHistoryImportService.cs` — new.
- `PEMS.Persistence/Servises/MaintenanceHistoryImportService.cs` — new.
- `PEMS.Web/Controllers/MaintenanceHistoryImportController.cs` — new.
- `PEMS.Web/Views/MaintenanceHistoryImport/Index.cshtml` — new.
- `PEMS.Web/Views/MaintenanceHistoryImport/Review.cshtml` — new.
- `PEMS.Persistence/Context/PEMSDbContext.cs` — two new `DbSet`s + FK/delete-behavior config.
- `PEMS.Persistence/Migrations/20260914110633_AddMaintenanceHistoryImport.cs` (+ `.Designer.cs`) —
  new, additive-only.
- `PEMS.Persistence/Migrations/PEMSDbContextModelSnapshot.cs` — updated by `dotnet ef migrations
  add` (includes this workstream's model plus the prior, still-uncommitted Equipment↔FL Assignment
  workstream's model).
- `PEMS.Persistence/Servises/PermissionService.cs` — new `"MaintenanceHistoryImport"` module +
  role seeding.
- `PEMS.Web/Program.cs` — DI registration.
- `PEMS.Web/Views/Shared/_SidebarMenu.cshtml` — one new nav link under the existing Import Center
  group.
- `PEMS.Tests/Cmms/MaintenanceHistoryImportServiceTests.cs` — new.
- `PEMS.Tests/Authorization/MaintenanceHistoryImportControllerAuthorizationTests.cs` — new.

Not created/changed: no `GenericMigrationEngine`/`GenericETLFramework`, no changes to
MaintenanceStrategy's `BulkCreate` (the just-closed workstream), no Resource Header changes, no
EDMS/VDR/MDR changes, no changes to any already-committed CMMS business logic.
