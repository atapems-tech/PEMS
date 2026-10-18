# Phase 2 — P4 closure, inventory decisions and P5 readiness

Status: analysis and approved in-scope fixes only. P5 is **not** implemented. Part Catalogue was not touched. No migration was applied to any
shared database; nothing was committed.

## 1. What changed in this round

| Area | Change | Evidence |
|---|---|---|
| MRS import commit (performance) | `MrsImportService.CommitCoreAsync`: `MrsImportRows.Attach(row)` → `Entry(row).State = Unchanged` | 5 000-row commit 57–424 s → **0.97 s**; capacity class 10/10 in 20 s; regression test `MrsImportCommitTrackingTests` |
| Production input consumption | new `MaterialStockService.AdjustByAsync` (signed delta computed on the fresh row inside each attempt); `PostInputToInventoryAsync` uses it | SQL race test `AdjustBy_OnRowVersionConflict_...` (12, not 7) |
| Docs | this file; `INVENTORY_STOCK_ROW_INTEGRITY.md` updated | — |

Everything from the P4 acceptance audit (stock-row resolver, retry result, `WorkOrderSpareReturn`, `PostingScope`, store-requisition execution
strategy, migration) is unchanged and was re-verified (section 2).

## 2. P4 integrity verification (current working tree)

Verified by reading the code and by tests run on this tree:

* **Business key** — every writer/reader goes through `StockRowResolver` (`FindExactAsync`, `FindForWarehouseAsync`, `GetOrCreateAsync`) except
  `MrsImportService` (NULL-location candidate list with the same ambiguity rule) and `WorkOrderController.AddSpareUsage` (one chosen row + ambiguity
  guard). `MaterialStockService` (Receive/Adjust/AdjustBy/Return/Transfer), `IrnReceiptService.CompleteDispositionAsync`,
  `StoreRequisitionLifecycleService` (reserve/issue/release), `ProductionRunService` and `WorkOrderSpareReturn` never pick among conflicting rows.
* **Atomicity** — each posting is one `SaveChanges` (stock row + ledger row [+ usage row for spare returns]). Ambiguity is detected before any change.
* **Retries** — `PostWithRetryAsync` (≤ 3, RowVersion conflicts only) detaches the failed attempt's ledger rows and reloads stock rows; exhaustion
  returns `Success=false`. Tests: `StockRowIntegritySqlTests` (7 disposable-SQL tests).
* **Return provenance** — `WorkOrderSpareReturn.ResolveOriginAsync` reads the `Issue` ledger row; refuses when none/several. Tests: `WorkOrderSpareReturnTests`.
* **Production idempotency** — `PostingScope` (applock inside `CreateExecutionStrategy().ExecuteAsync`) + ledger heal. Tests: `ProductionPostingIdempotencyTests`
  (run with the application's real `EnableRetryOnFailure()` configuration).
* **Store requisition under the retry strategy** — `StoreRequisitionRetryStrategyTests`.
* **Migration** — `20261010141504_MaterialStockNoLocationUniqueness`: index-only; the pre-flight `THROW` leaves data and history untouched when duplicate
  NULL-location rows exist (`Migration_WithPreExistingDuplicates_...` on a disposable DB). Not applied to `PEMSDB`/`PEMSDB_PcatClone`.

## 3. Store requisition multi-location design decision

### 3.1 Facts (verified)
* `MaterialReservation` has `MaterialItemId`, `WarehouseId`, `Quantity`, `IssuedQuantity`, `Status`, SR/line links — **no stock row, no location**.
* Reserve (`ReserveLinesAsync`), issue (`IssueCoreAsync`) and release (`ReleaseRemainingAsync`) each resolve the stock row **independently** by
  (item, warehouse) via `StockOfAsync`. With one active row this is exact; with two or more it is ambiguous, so P4 refuses (nothing changes).
* Today multi-row situations can hardly arise: `StorageLocations` has **0 rows** in `PEMSDB`; the warehouse UI hard-codes a NULL location for receive, adjust and
  transfer (`MaterialItemController`); no `.cshtml` file contains a storage-location field; MRS import creates NULL-location rows; the production-run post
  actions accept a `storageLocationId` parameter but no view posts one; `IrnReceiptLine.StorageLocationId` has no UI source either. So duplicates (the
  concurrency/import races P4 closed) are the realistic cause of a second row, not deliberate multi-location use.
* Other readers: `UsableStockService` (P1) **sums** all active locations of a (item, warehouse) and flags `StockMultipleLocationRows` as non-blocking;
  `WorkOrderController.AddSpareUsage` works per row. So a multi-location warehouse is *usable* for the net-demand estimate but *not reservable* by SRs.

### 3.2 Options
| | A — row/location-specific reservations | B — warehouse-level aggregation | C — keep the safe restriction |
|---|---|---|---|
| Idea | each reservation records the stock row it reserved (split a line over several rows = several reservations, which the code already supports per line) | keep reservations warehouse-level; allocate/deallocate across rows by a deterministic rule | refuse when a (item, warehouse) has ≠ 1 active row |
| Schema | `MaterialReservations.MaterialStockId` nullable FK (Restrict) + index; additive | none **if** per-row `ReservedQuantity` is allocated by rule — but see correctness | none |
| Backfill | reservations with exactly one active row for (item, warehouse) → set; the rest stay NULL and keep the warehouse-level lookup (legacy path). `PEMSDB`: 0 reservations | n/a | n/a |
| Correctness | exact: issue/release decrement the very row that was reserved; no ambiguity ever | only correct while the allocation state is unchanged between reserve and release. A transfer, a spare usage (picks a row by availability) or another SR in between changes row balances, so "reverse the allocation" can hit a different row → `Reserved` drift, `Reserved > OnHand`, or blocked issues | exact (it refuses what it cannot do exactly) |
| Concurrency | per-row `RowVersion` already exists; `RunAsync` retries; reserve picks rows in a fixed order (e.g. highest available, then `MaterialStockId`) and re-evaluates on conflict | needs a warehouse-wide serialisation (lock on item+warehouse) or accepts drift; negative stock is only prevented if every row is re-read inside the attempt | unchanged |
| Negative stock / double allocation | prevented by per-row `Reserved ≤ OnHand` check inside the same `SaveChanges` | per-row checks still needed, plus a warehouse-level invariant Σ`Reserved` = Σ remaining reservations that nothing enforces today | prevented (single row) |
| Auditability | ledger rows (`Reservation`, `Issue`, `ReservationRelease`) already carry `MaterialReservationId` → row and location become traceable | a deallocation order cannot be proven from the data | unchanged, but multi-row stock simply cannot be reserved |
| Compatibility | `NetDemandCollector` (covered-by-reservation sums by SR line), `UsableStockService`, WO flows unaffected | same | none |
| Cost | medium: 1 migration, `ReserveLinesAsync`/`IssueCoreAsync`/`ReleaseRemainingAsync` changes, tests (multi-row reserve/issue/release, legacy NULL reservations, concurrency on disposable SQL) | medium-high and unsafe (needs A's data anyway to be auditable) | none (done in P4) |

### 3.3 Recommendation
* **Keep Option C now** (it is what runs today and it is exact). Document the operational consequence: if a second active row appears for an
  item+warehouse (a duplicate, or the first receipt into a named location), reserve/issue/release for that item **in that warehouse** fail with the
  ambiguity message until the rows are reconciled by the warehouse owner.
* **Adopt Option A, not B**, if the business intends to use storage locations. B without per-reservation row data is not provably correct; with that data it *is* A.
* Conditions to remove the restriction: owner confirms locations will be used; Option A migration approved; the duplicate diagnostic is clean on every real database.
* **Do not implement A or B until the owner approves.** Owner questions: (1) will locations be used within the next release? (2) allocation order
  across rows (highest available, FIFO by `MaterialStockId`, location priority)? (3) may one SR line be split across several rows?

## 4. MRS 5 000-row performance

### 4.1 Measurements (InMemory unless stated; isolated runs on this machine)
| Variant | 5 000-row commit | Tracked entities at each `SaveChanges` |
|---|---|---|
| HEAD (pre-P4), test alone | 83.6 – 93.7 s | — |
| P4 tree before this fix | 78 – 99 s (alone), up to 424 s (full suite) | 35 101 (same context as staging), 10 101 (fresh context) |
| probe, `Attach`, same context | 66.5 s | 35 101 (rises +100/chunk) |
| probe, `Attach`, fresh context (request-like) | 39.2 s | 10 101 |
| probe, `Entry().State = Unchanged`, same context | **2.85 s** | **300** |
| probe, `Entry().State = Unchanged`, fresh context | **2.67 s** | **300** |
| repo after the fix: `MrsImportBatchCapacityTests` | **0.97 s** commit, class 10/10 in 20 s | — |

GC evidence before the fix: gen0 collections 73 / 275 / 1 539 for 1 000 / 2 000 / 5 000 rows (≈ quadratic allocation); after: 27 for 5 000 rows.

### 4.2 Root cause
`CommitCoreAsync` calls `ChangeTracker.Clear()` per chunk and then re-attaches each row with `MrsImportRows.Attach(row)`. `Attach` is a **graph** attach: it
follows `row.Batch → Batch.Rows` (every pending row of the batch) and `row.Metadata` (≈ 5 per row staged in the same context). So *every chunk of 100 rows
re-tracked the whole batch* (5 000 rows, or 30 000 entities with metadata), and every `SaveChanges` ran `DetectChanges`, `ApplyCreatedDate` (reflection per
entry), audit preparation and `ChangeTracker` scans over all of them: O(N) work per chunk, O(N²) overall. The earlier comment in the method
describes this symptom (SQL, 124 s) but the `Clear()` it added does not help because the next `Attach` rebuilds the graph.
The per-stage split inside the SaveChanges override (probe, cumulative at the last chunk, ms): immutability guard 14 649, `ApplyCreatedDate` 6 431, audit-prepare 6 441,
base save 13 117, audit finalise + second save 6 992 (total commit 66 451). The three tracker-walking steps alone were ~27.5 s (≈ 42 %); the two save phases were inflated by
EF's own `DetectChanges` over the same tracked set, so the tracked-set size, not the database, was the dominant cost.

### 4.3 Fix and why it is safe
`Entry(row).State = EntityState.Unchanged` tracks **only that entity** (documented EF Core behaviour: setting `State` does not traverse the graph). The
method only changes scalar properties of the row, so tracking state/semantics are the same; validation, error messages, chunk transactions, idempotency
and SQL behaviour are untouched. Verification: all 61 MRS tests (metadata, concurrency on real SQL, capacity, description length, tracking) pass.
InMemory vs SQL: the quadratic work is in the change tracker (provider independent); on SQL the database round-trips add to it but the commands/saves per
commit are identical before/after (1 000 rows: 19 reads, 122 writes, 42 saves), so the improvement carries over; the SQL number was not re-measured at 5 000 rows.

## 5. Production posting concurrency

* **Document-level double posting** (3 parallel posts → 3 receipts, also on pre-P4 HEAD): fixed by `PostingScope` (applock) + ledger heal — tested.
* **Read-then-absolute-write race in `PostInputToInventoryAsync`**: the consumption was computed from a balance read earlier and written as an absolute
  `Adjust`; on a RowVersion conflict the retry re-applied the stale absolute value and would erase a concurrent receipt. **Fixed** with
  `AdjustByAsync` (delta evaluated on the fresh row inside each attempt, same ledger shape: `Adjustment` with a signed `Quantity`). It uses the existing
  posting service, RowVersion on `MaterialStock` and the existing ledger — no second inventory architecture, no schema change.
* **Proposed safeguards, evaluated**
  * *RowVersion on `ProductionInput`/`ProductionOutput`*: additive column (`rowversion`) on two tables, no backfill. Useful as defence in depth; **not needed** for
    correctness now (applock + ledger heal). Needs owner/DBA approval (migration). Not implemented.
  * *Unique ledger index*: **a plain `(ReferenceType, ReferenceId)` unique index is wrong** — `WorkOrder`, `StoreRequisition` and `IrnReceipt` references
    legitimately repeat. A **filtered** unique index `WHERE ReferenceType IN ('ProductionInput','ProductionOutput')` is feasible: only `ProductionRunService`
    writes those types, and both shared databases have 0 production ledger rows and 0 duplicate groups. Requires: duplicate scan on every real database,
    owner approval, a separate migration. Not implemented.
  * *Application lock + retry strategy*: `PostingScope` runs inside `CreateExecutionStrategy().ExecuteAsync`, so a transient retry restarts the whole unit
    (transaction rolled back, tracker cleared, document re-read, ledger heal). Verified with `EnableRetryOnFailure()` contexts.

## 6. Shared database diagnostics (read-only)

Target confirmed from `PEMS.Web/appsettings.json`: server `localhost\SQLEXPRESS`, database `PEMSDB`, Windows authentication (no secret printed);
`PEMSDB_PcatClone` is the clone used by earlier Part Catalogue work. Only `SELECT`s were run.

| Check | `PEMSDB` | `PEMSDB_PcatClone` |
|---|---|---|
| `MaterialStocks` rows | 0 | 0 |
| NULL-location duplicate groups (would block the migration) | 0 | 0 |
| Active multi-row (item, warehouse) groups | 0 | 0 |
| `InventoryTransactions` / `MaterialReservations` | 0 / 0 | 0 / 0 |
| Production ledger duplicate groups | 0 | 0 |
| `ProductionInputs` / `ProductionOutputs` | 0 / 0 | 0 / 0 |
| `StorageLocations` / `Warehouses` / `MaterialItems` | 0 / 9 / 0 | not queried |
| Last migration / P4 index | `20261008075704_OrganizationAssignmentEmployeeIndex` / absent | same / absent |

Empty tables prove nothing about the future. Before applying the migration anywhere, the owner runs the two queries in
`INVENTORY_STOCK_ROW_INTEGRITY.md` (section 4) against the **actual target** database and the migration must be separately authorised.

## 7. Phase 2 P0–P3 status (verified in code and tests, not inferred)

| Package | Present | Evidence |
|---|---|---|
| P0 frozen policies | yes | design doc §16 |
| P1 read-only usable stock | `UsableStockService` (126 lines) | 10 test methods; `AsNoTracking`; duplicate NULL rows → `AmbiguousRows`; negatives reported never altered |
| P2 pure calculator | `NetDemandCalculator` (161 lines, no EF) | 23 test methods; shortage never negative; incoming supply "unknown" ⇒ `StockOnlyShortageEstimate`; incompatible/unknown units excluded |
| P3 collectors | `NetDemandCollector` (253 lines) + `NetDemandService` (98 lines) | 18 + 9 test methods; SR (Approved/Reserved/PartiallyIssued), WO (Assigned/InProgress/OnHold), reservation coverage, WO↔SR overlap excluded, MR/PM/PartCatalogue not read; masking by `StoreRequisition.View`/`WorkOrder.View` |
| Real SQL | `NetDemandSqlTests` | 2 test methods (disposable DB) |
| Read-only | grep of the three services and the application layer: **no** `SaveChanges`, `Add`-to-DbSet, `Remove`, `Execute*`; `Calculation_IsReadOnly_AndRepeatedRunsAreIdentical` | — |
| Consumers | registered in `Program.cs` (3 `AddScoped`); **no controller or view uses them yet** | grep |

Totals: 75 executed tests (`NetDemand*` + `UsableStockServiceTests`, theory cases counted), 75 passed.
Open gaps recorded in the design doc and still true: no PO lines (incoming unknown), `HoldQuantity` never resolves, no quarantine bucket, no unit conversion.
New interaction found in this round: P1 treats a multi-location (item, warehouse) as usable at warehouse level, while SR reservation (Option C) refuses it.

## 8. P5 readiness — proposed scope (for owner approval; nothing implemented)

### 8.1 Approved existing requirements (design doc §9–§12 and the frozen v1 policies)
* Persist recommendations: runs, recommendations, sources, history (S1–S4); idempotent upsert; one open recommendation per (item, warehouse).
* Statuses `Open / Dismissed / Converted / Superseded / Closed`; reason codes and issue codes from the calculator; per-row snapshots of usable stock, unreserved
  demand, shortage and the calculator version.
* Traceability to each demand source (typed nullable FKs with a CHECK of exactly one) and to the stock snapshot used.
* Optimistic concurrency (`RowVersion`), request-key idempotency, stale-input refusal (fingerprint) — conversion itself belongs to P6.
* Permissions seeded **without grants**, existing `IPermissionService`/`IRoleModuleActionService`; origin masking as in P3; SystemAdmin has no implicit authority.
* Audit: append-only history table plus existing `AuditLog`; one row per denied attempt via the Phase 1 pattern.
* UI in the Warehouse area following the existing workspace conventions (`.vs-*`, FA/EN resx, sidebar entry gated by permission).
* **Excluded**: MR/PO creation or issue, reservations, stock/ledger writes, workflow changes (enforce with a source-scan test, as for PO creation).

### 8.2 Decisions requiring owner approval (block P5 design details)
1. **D9** — who may View / Review (dismiss, reopen) / Convert; whether SystemAdmin needs an explicit grant.
2. **D15** — dismissal semantics: permanent until the shortage changes by a threshold? time-boxed? who may reopen?
3. **D14** — run cadence (manual only in P5, scheduled in P8?), staleness limit after which a recommendation is `Superseded`.
4. **D1-attribution** — Work Order demand has no warehouse; keep it as a warehouse-less group excluded from netting (current), or add an attribution rule.
5. **D3** — keep `Open` WOs as *potential* (current) and PM forecast off.
6. **D4/D5** — keep reorder policy "present, not evaluated" and no unit conversion/rounding in P5 (current), or decide the semantics now.
7. **Multi-location stock** — treat `StockMultipleLocationRows` as blocking for recommendations until Option A exists (recommended), or keep it as a warning.
8. Whether P5 may ship **before** the P4 migration is applied (it does not need the index, but the duplicate diagnostic must be clean).

### 8.3 Technical dependencies
* S1–S4 migrations (additive new tables) — one migration, applied only to disposable databases until authorised; tests use `PemsDbContextFactory`/`MigrateAsync` on `PEMS_TEST_*` databases.
* `INetDemandService` (P3) as the only data source; its result carries the input needed for the fingerprint (demand line ids + quantities, stock rows' ids and `RowVersion`).
* Recalculation determinism: recommendation identity = (material, warehouse); a run upserts by that key inside a transaction using `CreateExecutionStrategy()`; a
  second run with an unchanged fingerprint inserts nothing and writes no history.
* Concurrency: two simultaneous runs serialise per key (`PostingScope`-style applock or the unique open-recommendation filtered index), stale updates fail on `RowVersion`.
* Execution strategy compatibility (the app uses `EnableRetryOnFailure()`): every user transaction inside `ExecuteAsync`.

### 8.4 Deferred (not P5)
P6 conversion to MR/package (needs the PO-issuer decision and MR item link S6), P7 PO lines and incoming supply, P8 scheduling/alerts, quarantine/blocked stock,
legacy `SparePart` reconciliation, PM/engineering/MR demand, unit conversion, reorder-point evaluation, Option A/B reservation changes.
