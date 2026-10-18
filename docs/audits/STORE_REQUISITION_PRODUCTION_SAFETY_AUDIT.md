# Store Requisition / Inventory — Production Safety Audit (Phase 6.5)

Scope: the completed Store Requisition + inventory slice only. Nothing in Tender / Bid / Award / MRQ / ProcurementPackage / PurchaseOrder was touched.
Evidence date: 2026-10-08. Database inspected: the **local dev database `PEMSDB` only**. **No production database was available to this audit.**

## 1. Verdict

**PHASE 6.5 BLOCKED** — code-side gates pass; two production-critical gates cannot be closed from here:

| Blocker | Why it blocks | How to close |
|---|---|---|
| B1 Production violation audit not run | the dev DB has **0 rows** in `MaterialStocks`, `StoreRequisitionLines`, `MaterialReservations`, `InventoryTransactions`, so a "clean" result there proves nothing | run `docs/sql/store_requisition_production_safety_checks.sql` (read-only) on production; attach the output |
| B2 Constraint trust on production unknown | the revalidation migration only trusts a constraint when zero violating rows exist; on a dirty production DB the constraints stay `is_not_trusted = 1` | after B1, remediate any violations, then re-run the revalidation block (it is idempotent) |
| B3 Full regression result | see section 8 | — |

## 2. Violation audit (dev database, read-only)

Script: `docs/sql/store_requisition_production_safety_checks.sql` (**24** checks: S1–S3 stock, R1–R5 lines, V1–V10 reservations, T1–T6 transactions; an earlier draft of this document said 28 in error). Result on `PEMSDB`: **0 violations for every one of the 24 checks** — vacuous, because the tables are empty. Per-check severity and remediation guidance for production:

| Check | Meaning | Severity | Automatic remediation safe? | Recommended remediation |
|---|---|---|---|---|
| S1/S2 negative OnHand/Reserved | stock corruption | P0 | **No** | stock-take, then an `Adjustment` transaction (never an UPDATE) |
| S3 Reserved > OnHand | over-reservation | P0 | **No** | release newest reservations through the service (Cancel/Close) or adjust stock with a transaction |
| R1–R5 line quantity violations | approved/issued inconsistent | P1 | **No** | correct the line from the reservation/transaction ledger (T3 helps), record in history |
| V1/V2 reservation quantity | issued > reserved | P1 | No | recompute from `Issue` transactions |
| V3–V6 reservation links | orphan/foreign line | P1 | No | re-link only when exactly one candidate; otherwise manual |
| V7 duplicate active reservation per line | double reservation | P1 | No | release the duplicate via a `ReservationRelease` transaction |
| V8 legacy reservation without line | informational | P2 | Yes (deterministic, already in migration for the unambiguous case) | none |
| **V9 legacy reservation ambiguous** | cannot be attributed to one line (0 or >1 matching lines) | P1 | **No — never guessed** | the lifecycle service refuses to issue it; resolve manually (release or re-reserve) |
| V10 active reservations exceed `ReservedQuantity` | ledger/stock mismatch | P0 | No | investigate with T-checks |
| T1–T5 issue transaction integrity | missing material / source / work order | P1 | No | trace via `ReferenceId`; legacy rows have no `StoreRequisitionLineId` (expected) |
| T6 duplicate issue transactions | retry duplicates | P1 | No | review before deleting anything; deletion is never automatic |

Unresolved legacy reservations (V8/V9) on dev: **0**. Production: unknown (B1).

## 3. Constraint trust

| Constraint | Dev `PEMSDB` | Fresh empty DB (migrated from scratch) |
|---|---|---|
| `CK_MaterialStocks_Quantities` | enabled, **trusted** (`is_disabled=0, is_not_trusted=0`) | same |
| `CK_StoreRequisitionLines_Quantities` | enabled, trusted | same |
| `CK_MaterialReservations_Quantities` | enabled, trusted | same |

How: migration `20261007215305_StoreRequisitionLineTraceability` adds them `WITH NOCHECK`; migration `20261008054745_RevalidateStoreRequisitionConstraints` runs `ALTER TABLE … WITH CHECK CHECK CONSTRAINT` **only if no violating row exists** (it never modifies or deletes data). Behaviour on a dirty database was verified manually on a scratch DB: with one over-reserved stock row the constraint stayed `is_not_trusted = 1` and the row was preserved. Production state: unknown until B1.

## 4. What was verified in code (not assumed)

- **State machine below the controller:** `StoreRequisitionStatuses.CanTransition` is enforced inside `StoreRequisitionLifecycleService`; 13 legal and 13 illegal transitions are tested as a table, and `DirectServiceCalls_CannotSkipOrRevive_AnyStatus` calls the service directly (Draft→Reserved/Issued/Closed/Approved, Submitted→Issued, Approved→Issued, Cancelled→anything).
- **Idempotent reservation:** a second `Reserved` is an illegal transition and, independently, reserving is computed as *target − already reserved*; stock is reserved exactly once.
- **Partial issue:** the exact scenario requested 100 / approved 70 / issue 50 → remaining 20 / issue 20 → `Issued` / issue 1 more fails with no change is a test.
- **Approved quantity:** 0 ≤ approved ≤ requested; only settable in `Submitted`; after approval/reservation/issue it is immutable (rule: *changing it later is always refused*, which also covers 40 and 60 after 50 issued). Documented and tested.
- **Identity:** `EmployeeId` of `null`, `0`, negative, unknown or inactive is refused explicitly for every sensitive action; a test asserts no persisted reservation/transaction/history row ever carries employee id ≤ 0. (Previously `ReservedById = changedBy ?? 0` — fixed in Slice 1.)
- **SoD:** requester cannot approve, set approved quantities or issue; tested on direct service calls.
- **Permissions:** nine fine-grained `RoleModuleAction` actions (`StoreRequisition.Submit/Approve/Reject/SetApprovedQuantity/Reserve/Issue/Close/CloseShort/Cancel`) are checked **in the service** together with lifecycle state and SoD; the controller only hides buttons. Defaults are derived once from the existing legacy `RolePermission` grid (CanCreate → Create+Submit, CanApprove → all decision actions) plus `WH.Manager` (all) and `WH.Storekeeper` (View, Create, Submit, Reserve, Issue, Close). `Close` and `CloseShort` are separate permissions.
- **Audit:** the existing `AuditLog` (written by `PEMSDbContext.SaveChanges` for every entity change with before→after values, user and time) covers stock quantity changes, reservations, line approved/issued quantities and status; the test asserts e.g. `ReservedQuantity: 0 -> 8`, `OnHandQuantity: 100 -> 95`. Business context is in `StoreRequisitionStatusHistory` (reason, per-line quantities, `ApprovedQuantity:` records, `CloseShort:` with shortfall) and in the transactions. No second audit framework was added.
- **Traceability:** `InventoryTransaction` now carries `StoreRequisitionLineId` and `MaterialReservationId` (plus the existing `ReferenceId` and `WorkOrderId`). Forward and reverse chains Work Order ↔ Requisition ↔ Line ↔ Reservation ↔ Issue transaction are tested using only stored keys. **Legacy** transactions (before this migration) have these two columns NULL — only the requisition/work-order link exists for them.
- **Idempotency:** optional `requestKey` stored on the history row with a unique filtered index `(StoreRequisitionId, RequestKey)`; retried issue/approve/reserve with the same key has no second effect, including two requests racing with the same key (real SQL test). The Details page generates a new key per rendered form.
- **Atomicity:** operations run in one transaction (relational) and clear the change tracker on failure; tests show a failed over-issue on the second line rolls back the first line, and a failed reservation leaves reservations/transactions/history unchanged.
- **Concurrency (real SQL Server, 3 consecutive passes):** two reservations for the last stock → exactly one succeeds, no over-reservation, one reservation/transaction; two concurrent partial issues (50+50 against 70 reserved) → exactly one succeeds, final issued 50, on-hand 50, reserved 20; same-key concurrent issue → applied once. Concurrency exceptions are retried (3 attempts) and surfaced as a normal error message, not an HTTP 500.
- **Migrations:** empty database migrates from scratch (100 migrations); current dev DB migrated; all changes additive (columns, FKs, indexes, `CHECK` constraints); the only index replaced is `IX_StoreRequisitionStatusHistories_StoreRequisitionId` by a composite index with the same leading column. No data deleted.

## 5. Findings fixed in this phase

| # | Finding | Fix |
|---|---|---|
| 1 | service accepted `int? changedBy` (null allowed) → unattributed approvals | `StoreRequisitionActor(EmployeeId?, Roles)`; explicit refusal; employee must exist and be active |
| 2 | permissions only in controller (`Approve` for everything) | fine-grained actions enforced in the service; UI uses `IsAllowedAsync` for visibility only |
| 3 | no way to tell a normal close from a short close | separate `Close`/`CloseShort` permission; history reason `CloseShort: … [per-line shortfall]` |
| 4 | set-approved-quantity left no record | history row `ApprovedQuantity: line n: old -> new` |
| 5 | forward trace issue → line/reservation impossible | two nullable FK columns on `InventoryTransaction` |
| 6 | retried POST could double-issue | request-key idempotency (unique index) |
| 7 | CHECK constraints stayed untrusted forever | conditional revalidation migration |

## 6. Known limits / unresolved risks (not blockers for the code gate)

1. **Workflow Platform integration (Phase 13) is not done for Store Requisition approval**; lifecycle legality and invariants live in the service, which is the intended owner of stock mutations. Routing/assignment of approvers through the workflow engine is future work.
2. **Users without an employee link (e.g. the dev `admin` account) cannot perform any sensitive action** — by design. Operations that previously worked for such accounts now return an explicit message.
3. Fine-grained permission defaults are derived from the legacy matrix **once**; later edits to the legacy matrix do not propagate (manage via the module-action matrix). Review the seeded rows in production.
4. Pre-migration transactions have no line/reservation id (cannot be backfilled without guessing).
5. Hard-coded Persian/English strings on the Details page were **not** converted to resource keys (page mixes both today; converting cleanly needs the Supply Chain Help/localization pass).
6. Idempotency covers requests that carry a key (the Details forms do); API callers that omit the key are only protected by lifecycle state.

## 7. Help coverage required for this slice (documentation only; Help itself not built)

What a Store Requisition is (internal indent, Work Order link) · how to submit · how approval works and why the requester cannot approve/issue · what *approved quantity* is and that it is locked after approval · what reserving does (stock stays on hand, becomes unavailable) · partial issue and remaining quantity · Close vs **Close short** (releases the remaining reservation) · cancel/reject releasing reservations · Work Order traceability (Work Order → requisition → line → reservation → issue) · common messages: "no employee file linked", "insufficient reservable stock", "issued quantity exceeds approved", "transition not allowed", "concurrent change, retry".

## 8. Regression

See the Phase 6.5 section appended to `PEMS_SUPPLY_CHAIN_PROCUREMENT_BASELINE_AUDIT.md` for the final counts.


---

## 9. PHASE 6.5 — FINAL CLOSURE GATE (2026-10-08)

### 9.1 Final code state
Service: `StoreRequisitionLifecycleService` (actor/permission/identity/SoD/state table/quantity invariants/idempotency/atomic transaction). Migrations (all additive): `StoreRequisitionLineTraceability`, `StoreRequisitionTraceabilityIdempotency`, `RevalidateStoreRequisitionConstraints`. Last edits in this gate: `Reserved → Closed` (full short close) and localization keys (`SR_*`) for the new Details labels (the hard-coded-string ratchet had grown by 2; fixed, not re-baselined).

### 9.2 Close vs Close Short (final definition)
* **Close** = administrative closure of a requisition that is **fully issued** (`Issued → Closed`); permission `StoreRequisition.Close`; nothing is released; no shortfall.
* **Close Short** = closure while the approved quantity is **not fully issued** (`Reserved → Closed` or `PartiallyIssued → Closed`); permission `StoreRequisition.CloseShort`; **all remaining reservation is released** (one `ReservationRelease` transaction per reservation); history row `CloseShort: <reason> [line n: approved A, issued I]`, so shortfall = A − I.

Verified: approved 70 / issued 0 → Closed, shortfall 70, released 70, stock 200/0. Approved 70 / issued 50 → Closed, shortfall 20, released 20, stock 150/0. A Storekeeper can Close but cannot Close Short; WH.Manager can.

### 9.3 Dev safety check (exact result)
`docs/sql/store_requisition_production_safety_checks.sql` executed on `LAPTOP-DV0OQ25R\SQLEXPRESS`, database `PEMSDB`, 2026-10-08 09:31: **24 checks, 0 violations each, 0 affected ids.** The script prints server/database/timestamp/user, one row per check, the affected ids and the constraint-trust query. **DEV = clean (the tables are empty). This says nothing about production.**

### 9.3.1 Production evidence package (what the operator must run)
`sqlcmd -S <server> -d <PEMSDB> -E -i docs\sql\store_requisition_production_safety_checks.sql -o production_safety_<date>.txt` (read-only; only a #temp table is created). Required evidence: the output showing server, database, execution time, executed-by, **all 24 per-check rows** and the three constraint-trust rows. No credentials or production data belong in the repository. Until this exists: **Production Data Gate = BLOCKED.**

### 9.4 Constraint trust
Dev: all three `CK_*` have `is_disabled = 0`, `is_not_trusted = 0`. Fresh empty database migrated from scratch: same. With a violation (scratch DB, one row Reserved 9 > OnHand 5): the revalidation SQL did **not** trust the constraint and **preserved the row**. The migration only issues `WITH CHECK CHECK CONSTRAINT` inside `IF NOT EXISTS (violating row)`; it never updates or deletes data. **Production constraint trust = UNKNOWN.**

### 9.5 Tests (final build)
Focused (`StoreRequisition` + `LanguagePurity`): **101 passed, 0 failed.** Real SQL Server concurrency (included): last-unit reservation → exactly one succeeds, no over-reservation; two concurrent partial issues (50+50 vs 70 reserved) → one succeeds, issued 50, on-hand 50, reserved 20; same request key concurrently → applied once (one Issue transaction, one history row).
Authorization (real seeded roles, service level): WH.Manager, WH.Storekeeper, Viewer, role-less user, requester (approve/reserve/issue refused by SoD), employee null/0/unknown/inactive (every action refused, nothing persisted). Inactive **user** is not a service concept: `AccountController` refuses login for `IsActive = false` users, so such a user cannot reach the service; employee deactivation is enforced inside the service.
Audit: the existing `AuditLog` (automatic before→after via `PEMSDbContext.SaveChanges`) verified for Submit, Approve, Reject, Cancel, Reserve, partial issue, final issue, release/close short and approved-quantity changes; no second audit framework exists (reflection test).
Traceability (stored keys only): Work Order → requisition → line → reservation → issue transaction, and the reverse from the transaction's own keys. A legacy-style issue transaction without line/reservation stays explicitly **unresolved**.

### 9.6 Full regression (final build, run 6)
Total 2300 · **Passed 2299 · Failed 1 · Skipped 0** · duration 59 m 56 s (10:10:30 → 11:10:29).
Failed: `MrsImportBatchCapacityTests.Five_thousand_row_file_stages_and_commits_as_one_operation_within_a_practical_time` (10 m 34 s) — **pre-existing, timing-sensitive, not caused by this phase**; re-run alone on the same build: `MrsImportBatchCapacityTests` 10/10 passed. Earlier `LanguagePurity.HardCodedUiStrings` failures in this gate were caused by this phase and are fixed. An aborted partial run (693 tests) is not used.

### 9.7 Graph
`graphify update .` re-run after the final code state. The graph shows `StoreRequisitionLifecycleService` → `PEMSDbContext` (hosts the AuditLog interception), → `IRoleModuleActionService` (authorization), its methods (`AdvanceCoreAsync`, `IssueCoreAsync`, `SetApprovedCoreAsync`, `AuthorizeAsync`) and the tests calling it. It is a code-level graph: entity relations (line ↔ reservation ↔ transaction ↔ work order) live in the EF model/migrations, not as separate edges. No unexpected dependency found.

### 9.8 Deferred (accepted, not complete)
| Item | Status | Reason |
|---|---|---|
| Store Requisition approval through WorkflowPlatform | **DEFERRED** | separate workflow-integration phase; service-level rules remain authoritative |
| Details page localization | **DEFERRED** (new labels use `SR_*` keys; legacy hard-coded text remains) | UI localization phase |
| Help content | **DEFERRED** | Supply Chain Help phase (coverage listed in section 7) |

### 9.9 Final status
```
Code Implementation: PASS            Focused Tests: PASS
Concurrency: PASS                    Authorization: PASS
Traceability: PASS                   Database Migration: PASS
Constraint Trust (DEV): PASS         Constraint Trust (PRODUCTION): UNKNOWN
Production Data Audit: UNKNOWN       Full Regression: PASS (2299/2300; the one failure is the pre-existing timing test, green when re-run alone)
Graph: PASS                          Documentation: PASS
OVERALL PHASE 6.5: BLOCKED — PRODUCTION EVIDENCE REQUIRED
```
