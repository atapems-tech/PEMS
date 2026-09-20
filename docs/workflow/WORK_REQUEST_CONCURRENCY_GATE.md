# Work Request Concurrency Gate

**Final status: ACCEPTED WITH DOCUMENTED LIMITATIONS** (§12). One production hardening was made (§9).

## 1. Objective
Prove that two simultaneous Submit operations on one Work Request cannot start the workflow twice, and that a repeated Submit is deterministically rejected. Scope: Work Request Submit path, the runtime's instance creation, and the concurrency infrastructure it depends on. No other module, no UI redesign, no data reset.

## 2. Existing Submit architecture (before this gate)
`WorkRequestController.Submit` → `WorkRequestService.SubmitAsync`:
1. loads the Work Request (tracked, with its `RowVersion` token);
2. refuses unless `Status == "Draft"`; refuses if an instance already exists (read of `WorkflowInstances` by `EntityKey`);
3. checks the Create permission; resolves the binding's start status;
4. stages the business change on the tracked entity (`Status = Submitted`, `WorkRequestStatusHistory` row);
5. `WorkflowRuntimeService.StartInstanceAsync`: resolves the binding, checks "no Running instance" (`AnyAsync`), creates instance + instance stage + `Start` history row, then **one** `SaveChangesAsync` (a single transaction) persisting the instance, history, the Work Request update and the business audit.
Concurrency tokens: `WorkRequest.RowVersion` (`[Timestamp]`, SQL `rowversion`) is included in the `UPDATE ... WHERE RowVersion = @old`. Unique constraints: none on `WorkflowInstances` (only a non-unique index on `EntityKey`).

## 3. Race analysis
Both callers read `Draft` and "no instance", and both pass the runtime's in-memory `AnyAsync` check before either saves. The application-level checks therefore cannot stop the race; correctness depends on the save.
- **Protection A (existing):** both saves update the same `WorkRequests` row; the second `UPDATE` matches 0 rows → `DbUpdateConcurrencyException`, the whole transaction (including its instance INSERT) rolls back.
- **Weakness found:** protection A only works because the Work Request row happens to be modified in the same save. Measured probe (below): when the runtime is called without touching the Work Request row, the database **does** accept two Running instances for one entity (20/20 forced races). No physical constraint existed.

## 4. Test methodology
`PEMS.Tests/Cmms/WorkRequestConcurrencyGateTests.cs` — real SQL Server (a disposable database `PEMS_CONC_<guid>` is created by running **all real migrations**, seeded with the real `WorkRequestWorkflowSeeder`, and dropped at the end; `PEMSDB` is never used), independent `DbContext`/`WorkRequestService` per operation, real `AccessControlService`.
- **Forced interleaving:** an EF `SaveChangesInterceptor` holds each operation on a `Barrier` at its first `SaveChanges`, i.e. after all reads/validation and immediately before writing — the worst-case ordering, no sleeps.
- **Free-running:** both operations start from a shared gate (readiness confirmed with a `CountdownEvent`, no delays), no interference afterwards.
- Every iteration uses a new Work Request and re-reads the database with a fresh context.
No `Thread.Sleep`/`Task.Delay`/retry loop is used in tests or production.

## 5. Database behavior
Measured (see §8): with the pre-existing schema, the Work Request row's `RowVersion` alone produced exactly one winner in 50/50 forced races. Without the Work Request row in the transaction, duplicates occurred in 20/20 (probe run before the fix). After the fix (§9), the database itself rejects the second Running instance regardless of the entity row.

## 6. Transaction behavior
Instance, instance stage, `Start` history, Work Request status change and business audit are one `SaveChanges` (one SQL transaction). A losing caller's rows are rolled back entirely — verified: history and business-audit counts are exactly 1 after every race.

## 7. Idempotency behavior
- Repeated sequential Submit (same context, then a fresh context — a proxy/browser retry): second call returns a deterministic "only a draft can be submitted" result; 1 instance, 1 history row, 1 business audit row.
- Same actor, two simultaneous requests (double click): exactly one success, one deterministic conflict message (20 forced iterations).
- Real HTTP pipeline: two simultaneous `POST /WorkRequest/Submit` with the same anti-forgery token through the real MVC pipeline (in-process test host, isolated database): 20/20 iterations → 1 instance, 1 history row, status Submitted (both HTTP responses were the normal redirect; the loser's outcome is a toast message). The server is authoritative; disabling the button in the browser is only UX. (This HTTP-level run was a one-off, temporary test — not kept in the repository, unlike the six SQL-backed tests in `WorkRequestConcurrencyGateTests`.) (A rapid double-click in a headless real browser was not separately scripted; the HTTP-level simultaneous POST is the stricter equivalent.)
- Bonus: two simultaneous identical workflow actions (Approve at Submitted) advance exactly once (20 forced iterations).

## 8. Repeated race results

| Test | Iterations | Invariant held | Notes |
|---|---|---|---|
| Forced interleaving, two different actors, Submit | 50 | 50/50 | 1 instance, 1 `Start` row, status `Submitted`, 1 business `Draft→Submitted`, exactly 1 success; loser gets a friendly message |
| Free-running, two different actors, Submit | 50 | 50/50 | same assertions |
| Forced, same actor (double click) | 20 | 20/20 | same assertions |
| Forced, two identical `Approve` actions | 20 | 20/20 | 1 `Approve` history row, stage "Under Review", 1 `UnderReview` audit row |
| Runtime-level forced race (Work Request row untouched) | 20 | **before fix: 0/20 (20 duplicate instances); after fix: 20/20** | loser gets `WorkflowConcurrencyConflictException` |
| HTTP double-POST | 20 | 20/20 | real MVC pipeline |

Unexpected outcomes after the fix: 0. Duplicate transitions: 0. Duplicate business transitions: 0. Raw SQL/EF exception surfaced to a caller: 0.

Concurrent-caller outcomes: caller A → success; caller B → `(false, "…changed by another user at the same time; reload the page.")` (Work Request row conflict) or `(false, "…submitted at the same time by another user; reload the page.")` (unique-index conflict) — both bilingual, no raw exception. Which of the two protections fires first depends on statement ordering; both roll the loser back.

## 9. Production change (defect: missing physical guarantee)
Root cause: the "one Running instance per entity" rule for binding-started instances existed only as an in-memory check; the sole physical protection was the Work Request row's `RowVersion`, an indirect coupling.
Fix (smallest, additive, module-neutral):
- Filtered unique index `UX_WorkflowInstances_Binding_EntityKey_Running` on `WorkflowInstances(EntityKey)` where `EntityKey IS NOT NULL AND WorkflowBindingId IS NOT NULL AND Status = 'Running'` (migration `WorkflowInstanceRunningUniquePerEntity`; `Down` drops the index). Only binding-started, running instances are covered — Legacy instances (no binding) and finished instances are excluded, so unmigrated modules keep their behavior. Existing data checked before applying: 0 Work Request instances, 0 violating rows in `PEMSDB`.
- `WorkflowRuntimeService.StartInstanceAsync`: when the save fails with `DbUpdateException` for a binding start and a Running instance now exists, it throws the new `WorkflowConcurrencyConflictException` (subclass of `InvalidOperationException`); other errors are re-thrown unchanged.
- `WorkRequestService.SubmitAsync`: maps that exception to a bilingual message and clears the tracker.

## 10. Regression results
Baseline before any change: build 0 errors, **1698 passed / 0 failed / 0 skipped**. After the change (6 new tests): build 0 errors, **1704 passed / 0 failed / 0 skipped**. No existing test was modified or removed. `PEMSDB` is at migration `20260920105149_WorkflowInstanceRunningUniquePerEntity`; no temporary `PEMS_CONC_*`/`PEMS_QA_TMP` database remains.

## 11. Remaining limitations
- The Work Request status-transition race (two different actions on the same stage) is protected by optimistic concurrency (Work Request row, instance-stage `RowVersion`); only the identical-action case was stress-tested here.
- The unique index covers `Running` binding-started instances only; the legacy runtime path (no binding) intentionally still allows multiple instances (other modules, unchanged).
- A real-browser rapid double-click was not scripted separately (HTTP-level simultaneous POST was used).
- Concurrency tests need a local SQL Server (like the existing SQL-backed tests) and create/drop a temporary database.

## 12. Final acceptance status
**ACCEPTED WITH DOCUMENTED LIMITATIONS.** Invariant "one logical Submit ⇒ one workflow start" holds in 50/50 forced, 50/50 free-running, 20/20 double-click, 20/20 HTTP and 20/20 runtime-level races, and is now enforced by the database as well as by optimistic concurrency. Legacy path unchanged; no other module touched; no data reset.
