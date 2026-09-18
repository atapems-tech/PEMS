# Phase 4 — Help Increment 3C — Reliability Analytics Deep Audit — Final Report

MTBF, MTTR, Availability, Bad Actor, and the FailureRecord/WorkOrder data lineage that feeds all four.

## 1. Executive Summary

This increment was scoped as **audit-first**: establish exactly what PEMS Reliability Analytics currently calculates, what its data depends on, what users can safely infer from the results, and what remains unverified — not to add more Help content. The audit confirmed the four core metrics' formulas exactly as implemented, found two real, previously-undocumented gaps (Availability not bounded to 0–100%; Failure Mode/Cause Pareto and the Taxonomy Coverage KPI structurally non-functional), and four lower-severity findings. No code was changed to alter calculation behavior. The only functional change made is a **targeted, additive, idempotent correction** to two existing Help articles (Outcome B), adding the two most user-impacting caveats found. Ten new edge-case tests were added proving the gap conditions are real and reproducible. The full test suite (1535 tests) passes, including these new tests.

## 2. Scope & Methodology

Read-only code and live-schema audit of `ReliabilityService`, `ReliabilityAnalyticsService`, `ReliabilityDataQualityService`, and `WorkOrderLifecycleService` (the sole source of `FailureRecord` data). No business data was mutated at any point. Every claim below is anchored to a specific file/line or a specific query result, not inferred. Where a fact could not be verified (most notably: real-world prevalence of the gaps found, because the current dev database is effectively empty of failure history), it is explicitly marked `NotVerified` rather than assumed.

## 3. Files and Services Inspected

- `PEMS.Persistence/Servises/ReliabilityService.cs` — MTBF/MTTR/Availability (`GetEquipmentReliabilityAsync`, `GetFleetReliabilityAsync`)
- `PEMS.Persistence/Servises/ReliabilityAnalyticsService.cs` — class rollup, Pareto, Bad Actor, monthly trend
- `PEMS.Persistence/Servises/ReliabilityDataQualityService.cs` — pre-existing data-quality diagnostics (not built this increment)
- `PEMS.Persistence/Servises/WorkOrderLifecycleService.cs` — the sole `WorkOrder.Status` mutator and the sole `FailureRecord` write path (`AdvanceStatusAsync`/`SyncFailureRecordAsync`)
- `PEMS.Domain/Entities/FailureRecord.cs`, `WorkOrder.cs`
- `PEMS.Persistence/Configurations/ReliabilityIndexConfiguration.cs` — confirms no unique constraint on `FailureRecord.WorkOrderId`
- Exhaustive grep across `PEMS.Persistence`/`PEMS.Web` for every `WorkOrder.Status =` and `FailureModeId =`/`FailureCauseId =` write path
- Live `PEMSDB` schema (read-only diagnostic queries, this report's §15)
- Existing test coverage: `PEMS.Tests/Cmms/ReliabilityAnalyticsTests.cs`, `PEMS.Tests/Help/ReliabilityHelpContentAccuracyTests.cs`

Full detail: [`phase4_reliability_analytics_audit.md`](phase4_reliability_analytics_audit.md).

## 4. Confirmed Data Path (FailureRecord/WorkOrder Lineage)

```
WorkOrder (Corrective/Emergency) → InProgress → Completed
  (WorkOrderLifecycleService.AdvanceStatusAsync — the ONLY code path that sets WorkOrder.Status)
  → if failureModeCode is non-blank → SyncFailureRecordAsync (upsert by WorkOrderId, in-memory only —
     no DB-enforced unique constraint on WorkOrderId; see §8)
  → FailureRecord.FailureMode/FailureCause/RootCause/DowntimeHours set
  → FailureRecord.FailureModeId/FailureCauseId NEVER set (no write path exists anywhere in the codebase)
→ ReliabilityService reads FailureRecords (+ linked WorkOrder Start/CompletionDate) → MTBF/MTTR/Availability
→ ReliabilityAnalyticsService reads the same FailureRecords → Bad Actor (string fields, unaffected)
   and Pareto (FK navigation fields, directly affected — see Gap G-2)
```

All four writes for a single Work Order completion (`WorkOrder`, `WorkOrderStatusHistory`, `FailureRecord`, `MaintenanceHistory`) are staged on one `DbContext` and committed by exactly one `SaveChangesAsync()` call — atomic by EF Core's implicit per-call transaction, not an explicit `BeginTransaction`.

## 5. Formula Definitions (Exact, as Implemented)

| Metric | Formula | Minimum data |
|---|---|---|
| MTBF | (LastFailureDate − FirstFailureDate) hours ÷ (FailureCount − 1) | ≥2 active FailureRecords |
| MTTR | average(WorkOrder.CompletionDate − StartDate) hours, over failures with a completed linked WO | ≥1 linked, dated WO |
| Availability | MTBF ÷ (MTBF + MTTR) × 100 | Both non-null, sum > 0 |
| Bad Actor | Top-N by FailureCount desc, then DowntimeHours desc, within optional date range | ≥1 failure in range |

Full field-by-field detail (22 columns): [`phase4_reliability_analytics_formula_matrix.csv`](phase4_reliability_analytics_formula_matrix.csv). Full source-to-output lineage (10 columns): [`phase4_reliability_data_lineage.csv`](phase4_reliability_data_lineage.csv).

**MTBF/MTTR measure calendar time and elapsed Work-Order duration respectively — not operating-time-normalized industry MTBF/MTTR.** This is stated explicitly in the corrected Help content (§10).

## 6. Null / Zero / Insufficient-Data Behavior (Verified in Code and by New Tests)

- 0 or 1 failure → `MTBFHours` is `null` (never a fabricated zero).
- No completed linked WO → `MTTRHours` is `null`.
- `FirstFailureDate == LastFailureDate` (duplicate timestamps) → `MTBFHours` is exactly `0.0`, a real value, not `null` — proven by the new test `MTBF_DuplicateFailureTimestamps_ReturnsExactZero_NotNull`.
- `StartDate == CompletionDate` on the linked WO → `MTTRHours` is exactly `0.0` — proven by `MTTR_ZeroDurationWorkOrder_CountsAsExactZero_NotNull`.
- MTBF and MTTR both exactly `0.0` → the `(MTBF + MTTR) > 0` guard prevents a `0/0` division; `AvailabilityPercent` stays `null` — proven by `Availability_MTBFAndMTTRBothExactlyZero_GuardPreventsZeroDivision_ReturnsNull`.
- Equipment with 0 failures never appears in `GetFleetReliabilityAsync` at all (list is built from `FailureRecords`, not `Equipments`) — see Gap G-6.

## 7. Known Limitations & Gaps

Six gaps identified and evidenced, full detail in [`phase4_reliability_analytics_gap_matrix.csv`](phase4_reliability_analytics_gap_matrix.csv):

| ID | Area | Severity | One-line finding |
|---|---|---|---|
| G-1 | Availability/MTTR | Medium | Not bounded to 0–100%; MTTR not guarded against negative WO duration |
| G-2 | Pareto/Taxonomy Coverage | High | `FailureModeId`/`FailureCauseId` never populated anywhere — charts/KPI structurally empty |
| G-3 | Bad Actor | Low | Only 2-level tie-break; 3-way ties fall back to unkeyed stable-sort order |
| G-4 | Class Availability | Low | Simple average of equipment percentages, not a weighted class recomputation |
| G-5 | MTBF | Low | Future-dated `FailureDate` not excluded from the span calculation |
| G-6 | Fleet list | Low | Equipment with 0 failures is absent from the list, not shown with zero metrics |

G-1 and G-2 are proven executable via the new tests in §11. Real-world prevalence of all six is `NotVerified` (current dev DB has 0 FailureRecords).

## 8. FailureRecord Data-Integrity Audit

- **Transaction boundary:** atomic per Work Order completion (§4) — no partial-write risk observed.
- **Retry behavior:** no Polly/execution-strategy logic in `WorkOrderLifecycleService`; whether the DbContext has SQL connection-resiliency configured elsewhere was not checked (`NotVerified`).
- **Logging:** `WorkOrderLifecycleService` has no `ILogger` and no log calls at all. A Corrective/Emergency WO completed with a blank `failureModeCode` skips `SyncFailureRecordAsync` with zero trace — not just no UI warning, no log line either.
- **User feedback:** `AdvanceStatusAsync`'s `(Success, Error)` return has no signal for "completed, but Failure Record not synced." The caller cannot distinguish this from a fully-synced completion.
- **Validation:** `failureModeCode`/`failureCauseCode` are only checked for non-blank — never validated against the `FailureMode`/`FailureCause` reference tables.
- **Idempotency:** upsert-by-`WorkOrderId` is application-level only — `ReliabilityIndexConfiguration.cs` confirms **no unique database constraint** on `FailureRecord.WorkOrderId`. Combined with the state machine (`Completed` → `Closed` only, no reopen), duplicate-row risk is a narrow, low-likelihood race, not a routinely reachable defect — documented, not elevated to a gap-matrix row.

## 9. UI Claims vs. Implementation

View files (`.cshtml`) were not exhaustively read this increment — any UI-layer wording beyond the Help content itself is `NotVerified`. The Help content itself (audited and now corrected, §10) does not overstate the formulas.

## 10. Standards & Terminology Review

Reviewed `reliability-overview`, `concept-mtbf-mttr-availability`, and `concept-bad-actor` for banned/overstated phrasing (`ISO-compliant`, `predictive`, `statistically valid`, `guaranteed`, `certified`, unqualified `best practice`). **None found.** One incidental match of "predictive" exists in the unrelated, pre-existing `concept-maintenance-strategy` article, used correctly as a generic industry taxonomy term ("preventive/predictive/corrective"), not as a claim about PEMS's own capability — out of this increment's scope, no correction needed. ISO 14224's classification remains `ConceptuallyMapped` (unchanged from Increment 3A/3B); this audit found no basis to change it.

## 11. Test Coverage Additions (Increment 3C)

10 new tests added to `PEMS.Tests/Cmms/ReliabilityAnalyticsTests.cs` (27 total in that file, up from 17):

- **MTBF:** three-failure division, duplicate-timestamp exact-zero, future-dated-failure inclusion (proves G-5)
- **MTTR:** unlinked-FailureRecord exclusion, negative-duration non-exclusion (proves G-1), zero-duration exact-zero
- **Availability:** negative-MTTR pushing the result above 100% (proves G-1), both-exactly-zero null-guard
- **Bad Actor:** three-way-tie call-to-call stability (documents G-3), top-N parameter limiting

All 27 tests in the file pass; see §14.

## 12. Help Content Outcome Decision: Outcome B (Targeted Correction)

No new article was created, per this increment's explicit instruction. Two existing articles were corrected, in both EN and FA:

- **`reliability-overview`** (shared by `Reliability.Overview` HelpKey, covering both the Index and Enterprise Dashboard pages): added an "Availability is not mathematically bounded to 0–100%" caveat to the Availability bullet, and a new paragraph documenting that the Failure Mode/Cause Pareto charts and the Failure Taxonomy Coverage indicator may show little or no data due to the unpopulated FK fields (G-2).
- **`concept-mtbf-mttr-availability`**: added the same Availability-bounding caveat.

**Implementation approach:** the project's seeders are insert-only-by-`Code` (never overwrite existing content, to protect admin edits). Consistent with the project's own existing precedent for non-destructive patches (`HelpContentSeeder`'s `operationsOverview` HelpKey backfill), a narrow, idempotent patch step was added to each seeder: it replaces a known exact pre-correction substring with the corrected text **only if that exact substring is still present** — so a fresh install gets the corrected text from the literal source directly, an already-seeded dev database gets patched once, and any future admin customization of this content is never silently overwritten (the patch simply no-ops if its anchor text is no longer found).

## 13. Verification — Build

`dotnet build PEMS.Tests/PEMS.Tests.csproj` (which transitively builds `Domain`/`Application`/`Persistence`/`Web`): **0 errors, 3 pre-existing warnings** (`Newtonsoft.Json` advisory, one pre-existing nullable-reference warning in `FilesControllerAccessTests.cs`) — none introduced by this increment. A separate `dotnet run --no-build` build of `PEMS.Web` alone: **0 warnings, 0 errors.**

## 14. Verification — Tests

- `ReliabilityAnalyticsTests` (27 tests, includes all 10 new Increment 3C tests): **27/27 passed.**
- All Help-related tests (`--filter FullyQualifiedName~Help`): **114/114 passed** — confirms the Outcome B content patch did not break `ReliabilityHelpContentAccuracyTests` or any other Help test.
- Full `PEMS.Tests` suite: **1535/1535 passed, 0 failed, 0 skipped**, 31m51s. No transient failures occurred in this run (unlike the isolated, unrelated `EquipmentCriticalityConcurrencyTests` flake noted in Increment 3B — not reproduced here).

## 15. Verification — Live Database & HTTP

**Database** (read-only queries against `PEMSDB`, after restarting `PEMS.Web` to trigger the corrected seeders):

| Check | Result |
|---|---|
| `reliability-overview` EN content contains the Availability caveat | Yes |
| `reliability-overview` EN content contains the Pareto/coverage caveat | Yes |
| `reliability-overview` FA content contains both caveats | Yes |
| `concept-mtbf-mttr-availability` EN/FA contain the Availability caveat | Yes (both) |
| Total `HelpArticles` | 83 (unchanged — confirms Outcome B added no new article) |
| `reliability-overview` / `concept-mtbf-mttr-availability` row count | 1 each (no duplicate insert) |
| `HelpStandardReferences` | 8 (unchanged) |
| `FailureRecords` / `WorkOrders` | 0 / 1 (dev DB remains effectively empty — G-1/G-2 real-world prevalence stays `NotVerified`) |

**HTTP:** `GET /Reliability`, `/Reliability/Dashboard`, `/` all returned `302` (authentication redirect). This confirms the routes are wired and the application responds — **it is not evidence of correct page rendering**, per this increment's explicit instruction not to treat a 302 as proof of a rendered page.

**Browser verification was not performed** — no browser automation tool is available in this environment. The database-level confirmation above (exact caveat text present in the translations actually served by `HelpService`) is the strongest verification available without one.

## 16. Final Status

**`TargetedCorrectionsImplemented`**

Two existing Help articles received evidence-based, additive, reversible corrections (Outcome B). No calculation code was changed. Six gaps were identified, evidenced, and either proven by new executable tests (G-1, G-2 indirectly via the Pareto audit) or documented (G-3 through G-6) without being fixed, per this increment's explicit instruction not to implement a new FailureRecord workflow or silently change formulas absent a severe, clearly-evidenced integrity defect — none of the six gaps met that bar. Real-world prevalence of G-1/G-2 remains `RequiresProductDecision` / `NotVerified` pending a populated environment. Recommended next steps (not undertaken this increment): guard MTTR/Availability against negative durations; decide and implement the FailureModeId/CauseId population path (or repoint the Pareto queries) to resolve G-2.
