# Phase 4 — Reliability Analytics Deep Audit (Increment 3C)

## Scope

MTBF, MTTR, Availability, Bad Actor, and the FailureRecord/WorkOrder data lineage that feeds all four. Read-only code and live-schema audit; no business data was mutated.

## Files and services inspected (full read, not excerpt)

- `PEMS.Persistence/Servises/ReliabilityService.cs` (`GetFleetReliabilityAsync`, `GetEquipmentReliabilityAsync` — MTBF/MTTR/Availability)
- `PEMS.Persistence/Servises/ReliabilityAnalyticsService.cs` (`GetClassReliabilityAsync` ×2, `GetFailureModeParetoAsync`, `GetFailureCauseParetoAsync`, `GetBadActorsAsync`, `GetMonthlyTrendAsync`)
- `PEMS.Persistence/Servises/ReliabilityDataQualityService.cs` (full read — an existing, real data-quality diagnostic service, not built this increment)
- `PEMS.Persistence/Servises/WorkOrderLifecycleService.cs` (full read — the sole path to `WorkOrder.Status="Completed"` and to `FailureRecord` creation/update)
- `PEMS.Domain/Entities/FailureRecord.cs`, `WorkOrder.cs` (relevant fields)
- Grep across `PEMS.Persistence` and `PEMS.Web` for every `WorkOrder.Status = "Completed"` write path and every `FailureModeId =`/`FailureCauseId =` write path (exhaustive, not sampled)
- Live `PEMSDB` schema (read-only diagnostic queries — see §12 of the increment report)

## Data sources (confirmed)

`FailureRecord` → optionally `WorkOrder` (via `WorkOrderId`) → `Equipment`. `FailureRecord.FailureModeId`/`FailureCauseId` are FK references to a separate reference taxonomy (`FailureMode`/`FailureCause` entities) that exist for FMEA analysis (`FMEAService.cs`) — **confirmed by exhaustive grep that no code path ever sets these two FK fields on `FailureRecord`**, only on the unrelated `FMEAItem` entity.

## Confirmed data path

```
WorkOrder (Corrective/Emergency type) → InProgress → Completed
  (WorkOrderLifecycleService.AdvanceStatusAsync, the ONLY code path that sets WorkOrder.Status)
  → if failureModeCode is non-blank → SyncFailureRecordAsync (idempotent upsert by WorkOrderId)
  → FailureRecord.FailureMode/FailureCause/RootCause/DowntimeHours (free-text/decimal fields) set
  → FailureRecord.FailureModeId/FailureCauseId NEVER set (confirmed, no write path exists)
→ ReliabilityService reads FailureRecords (+ linked WorkOrder Start/CompletionDate) → MTBF/MTTR/Availability
→ ReliabilityAnalyticsService reads the same FailureRecords → Bad Actor (uses FailureMode/Cause STRING fields — unaffected by the FK gap) and Pareto (uses FailureModeRef/FailureCauseRef FK NAVIGATION — directly affected by the FK gap)
```

## Formula definitions (exact, as implemented — see formula matrix CSV for full detail)

- **MTBF** = (LastFailureDate − FirstFailureDate) in hours ÷ (FailureCount − 1). Requires ≥2 active `FailureRecord`s for that equipment. This is a **time-between-events indicator over calendar time span**, not an operating-time-normalized MTBF — it does not account for planned shutdowns, idle periods, or actual runtime exposure.
- **MTTR** = arithmetic mean of (`WorkOrder.CompletionDate − WorkOrder.StartDate`) in hours, over only the failures whose linked WorkOrder has both dates set. This measures **elapsed Work-Order duration**, not pure hands-on repair time — it includes any waiting/logistics/permit time that happened between `StartDate` and `CompletionDate` inside that Work Order.
- **Availability** = MTBF ÷ (MTBF + MTTR) × 100, calculated only when both MTBF and MTTR have values and their sum is >0. This is the textbook **Inherent Availability** formula, but built from the calendar-time MTBF and elapsed-duration MTTR above — so it inherits both of their scope limitations. It is **not bounded to 0–100%** (see Gap G-1).
- **Bad Actor** = top-N equipment (default 20, or 10 on the Dashboard) ranked by `FailureCount` descending, then `TotalDowntimeHours` descending, within an optional date range applied to the failure events (not to the equipment's whole history). `RepeatFailureCount` = number of distinct `FailureMode` (string) groups where at least two occurrences fall within a rolling 90-day window. No configurable threshold exists — it is always top-N, not "any equipment exceeding X failures."

## Null / insufficient-data / zero-division behavior (verified in code)

- 0 or 1 failure → `MTBFHours` stays `null` (no value shown, not zero, not an exception).
- No failures with a completed linked WorkOrder → `MTTRHours` stays `null`.
- `MTBFHours`/`MTTRHours` both present but sum to exactly 0 → `AvailabilityPercent` stays `null` (explicit `> 0` guard prevents a `0/0` division). This can only happen if MTBF and MTTR are both exactly 0.
- Equipment with 0 failures never appears in `GetFleetReliabilityAsync` at all (the fleet list is built by starting from `FailureRecords`, not from `Equipments`) — it is absent from the list rather than shown with zero/null metrics. A user cannot distinguish "no failure history" from "equipment not in this query" without checking the Equipment register separately.

## Known limitations (confirmed, not assumed)

1. **MTBF/MTTR use calendar time, not operating time.** No `Runtime`/`OperatingHours` field is read anywhere in `ReliabilityService.cs`. Equipment that was shut down for an extended planned outage between two failures will show an inflated MTBF that reflects "time elapsed," not "time actually running."
2. **MTTR includes non-repair elapsed time.** `WorkOrder.StartDate`→`CompletionDate` is whatever duration the Work Order was open, not pure wrench-time. Waiting for spares, permits, or approvals inside that window inflates MTTR.
3. **Class-level Availability is an average of individual equipment percentages** (`GetClassReliabilityAsync`), not a class-level recomputation from aggregated MTBF/MTTR. An equipment with a 100% value from a single low-confidence data point carries equal weight to one with many failures. `MTBFSampleSize`/`MTTRSampleSize` are computed and present in the DTO, but this audit did not confirm whether the Razor views actually surface them to the user (view-level confirmation deferred — `NotVerified`).
4. **Availability is not bounded to 0–100%** (see Gap G-1).
5. **Failure Mode/Cause Pareto charts and `FailureTaxonomyCoveragePercent` rely on a field that is never populated** (see Gap G-2) — they are effectively non-functional in the current build, independent of how much real failure data exists.
6. **`GetEquipmentReliabilityAsync`/fleet ranking have no dedicated final tie-breaker** beyond `FailureCount`→`DowntimeHours`; true 3-way ties fall back to LINQ's stable-sort-preserved input order rather than an explicit key like `EquipmentId`. Low-severity determinism gap.

## UI claims vs. actual implementation

This audit did not find any UI-layer text (view files were not exhaustively read this increment — `NotVerified`) that overstates the formulas beyond what is implemented. The Help content added in Increment 3B (`reliability-overview`, `concept-mtbf-mttr-availability`, `concept-bad-actor`) already described the formulas accurately and hedged appropriately (e.g., explicitly stated the FailureRecord/WorkOrder sync dependency). This audit's corrections (§ below) tighten precision on **calendar-vs-operating time** and add the **not-bounded-to-100%** caveat, which the prior content did not state.

## Standards references

No standards claims beyond what Increment 3A/3B already established (ISO 14224 = `ConceptuallyMapped`, not compliance-verified). This audit found no basis to change that classification.

## Recommended Help wording changes

See §10 "Help Content Changes" in the final report — **Outcome B (targeted correction)**, not a new article batch.

## Recommended product/backlog items (not implemented this increment — evidence-based recommendations only)

1. Guard MTTR/Availability against negative-duration Work Orders (clamp or exclude) — addresses G-1.
2. Either populate `FailureModeId`/`FailureCauseId` during `SyncFailureRecordAsync` (via a lookup against the `FailureMode`/`FailureCause` reference tables by code) or change the Pareto queries to group by the string fields instead — addresses G-2.
3. Surface `MTBFSampleSize`/`MTTRSampleSize` in the Class Reliability view if not already shown, so low-confidence averages are visibly qualified.
4. Consider excluding future-dated `FailureRecord.FailureDate` values from MTBF span calculation (currently only tracked as a data-quality metric, not excluded from the calculation itself).

## FailureRecord data-integrity audit (transaction boundaries, retry, logging, user feedback, validation)

Full re-read of `WorkOrderLifecycleService.AdvanceStatusAsync`/`SyncFailureRecordAsync` (lines 37-140), confirming the items not yet covered narratively above:

- **Transaction boundary:** `AdvanceStatusAsync` makes exactly one `SaveChangesAsync()` call (line 111), after the `WorkOrder` status/fields, the new `WorkOrderStatusHistory` row, the `FailureRecord` upsert, and the new `MaintenanceHistory` row have all been staged on the same `DbContext`. EF Core wraps a single `SaveChangesAsync()` call in an implicit database transaction, so these four writes are atomic - either all commit or none do. There is no explicit `BeginTransactionAsync`/`ExecutionStrategy` wrapping, which is not a defect (implicit per-`SaveChanges` transactions are the EF Core default and sufficient here), but it does mean no cross-request retry policy is configured *in this service* for transient SQL failures.
- **Retry behavior:** No retry/resilience logic (no Polly, no explicit `IExecutionStrategy` usage) exists in `WorkOrderLifecycleService` itself. Whether the configured `DbContext` has a SQL Server connection-resiliency execution strategy registered globally (e.g., `EnableRetryOnFailure`) was **not checked this increment** - `NotVerified`. If `SaveChangesAsync` throws, the exception propagates unhandled to the caller; there is no catch/retry/dead-letter path in this service.
- **Logging:** `WorkOrderLifecycleService` has **no `ILogger` field and no log calls anywhere in the file** (confirmed by full read - no `_logger`, no `ILogger<T>` constructor parameter). This means the "silent" behavior already documented in the `reliability-overview` Help article is confirmed precise: when a Corrective/Emergency Work Order is completed with a blank `failureModeCode`, the `SyncFailureRecordAsync` call is skipped (line 90's `if` guard) with **no log entry of any kind**, not just no UI warning.
- **User feedback:** `AdvanceStatusAsync` returns `(bool Success, string? Error)`. `Error` is populated only for an illegal state transition or a blocked release gate (Permit/LOTO/GasTest). There is no equivalent signal - no warning, no partial-success flag - for "this Work Order completed successfully, but no Failure Record was created/updated because the failure-coding fields were left blank." A technician/caller cannot distinguish this case from a fully-synced completion by inspecting the return value.
- **Validation:** `failureModeCode`/`failureCauseCode` are only checked for `IsNullOrWhiteSpace` (line 90) before syncing - their content is never validated against the `FailureMode`/`FailureCause` reference tables (the same tables whose IDs are never linked - see Gap G-2). Any non-blank free-text value is accepted and stored as-is on both `WorkOrder.FailureModeCode` and the synced `FailureRecord.FailureMode`.
- **Idempotency / duplicate-creation risk:** Re-confirmed (see also §"Confirmed data path" above) - `SyncFailureRecordAsync` looks up the existing `FailureRecord` by `WorkOrderId` and updates it in place if found, so repeated syncs for the same Work Order cannot create duplicate `FailureRecord` rows *in the normal single-request case*. **Confirmed by reading `ReliabilityIndexConfiguration.cs`: there is no database-enforced unique constraint/index on `FailureRecord.WorkOrderId`** - the two indexes defined there (`IX_FailureRecords_IsActive_FailureDate`, `IX_FailureRecords_EquipmentId_FailureModeId_FailureDate`) are non-unique, query-performance indexes only. The upsert-by-`WorkOrderId` behavior is purely an application-level convention, not a database guarantee - under a theoretical race (two near-simultaneous `AdvanceStatusAsync` calls completing the same Work Order) duplicate `FailureRecord` rows for one `WorkOrderId` are not structurally prevented by the schema. Combined with `LegalTransitions` having no path back into `"Completed"` once a Work Order leaves it (`Completed` → `Closed` only), this is a low-likelihood, narrow race window, not a routinely reachable defect - documented here as a minor finding, not elevated to a full gap-matrix entry.

## Explicit unresolved questions

- Real-world prevalence of negative-duration Work Orders and FailureRecords without FailureModeId **cannot be measured against live data** — the configured dev database currently contains 0 FailureRecords and 1 WorkOrder. The code-path findings (G-1, G-2) are structurally certain; their real-world *frequency/impact* is `NotVerified` and `RequiresProductDecision` on whether/when to re-check against a populated environment.
- Whether Razor views expose `MTBFSampleSize`/`MTTRSampleSize`/low-confidence warnings to end users — `NotVerified`, view files not read this increment.
- Whether "Availability > 100%" has ever actually occurred in any environment — `NotVerified`, no data available to check.
