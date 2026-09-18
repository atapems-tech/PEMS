# PEMS Operations Phase 2C — Production Safety Correction & Business Decision Baseline

**Scope**: (A) audit and correct the confirmed production-inventory quality-gate defect
identified in Phase 2B; (B) produce a formal business-decision baseline for Packaging. No
Packaging, Daily Operations, QualityTestResult, Gross/Net/Tare, batch split/merge, rework, or
barcode-extension code was implemented — confirmed by the file list in Section 8.

---

## 1. Executive Summary

The Phase 2B-reported defect was verified against the live code exactly as described:
`ProductionRunService.PostOutputToInventoryAsync` checked only the double-post guard
(`InventoryTransactionId.HasValue`) and never read `ProductionBatch.QualityStatus`. The
correction implemented is a **hard gate with no override**: only a batch whose `QualityStatus`
is exactly `"Released"` may post output to inventory — `Produced`, `AwaitingQC`, `Blocked`, and
`Rejected` are all rejected, not only the two negative statuses. This was the minimal,
smallest-safe-change reading of Phase 2B's own recommendation, confirmed correct by checking
that `IMaterialStockService.ReceiveAsync` has exactly one other caller in the entire codebase
(`MaterialItemController`, which never passes a batch ID), so gating only inside
`PostOutputToInventoryAsync` closes the gap completely with no bypass path today.

Fixing the defect required correcting 4 pre-existing tests that had encoded the buggy
behavior as expected (posting an unreleased, default-`"Produced"`-status batch and asserting
success) — this is disclosed explicitly in Section 9, not silently done.

**Final classification (Section 24): A. SAFETY CORRECTION COMPLETE — PACKAGING STILL BLOCKED
BY BUSINESS DECISIONS.**

---

## 2. Repository and Commit Reviewed

`D:\PEMS\03-SOURCE\PEMS-cmms-edit-empoleeys-master\PEMS-cmms-edit-empoleeys-master`, branch
`main`, working tree at commit `2cb1c43` (the tip at the start of this session) plus the
uncommitted Phase 2B report addition. All code evidence below was read directly from the
current working tree during this session, not carried forward from prior report text.

## 3. Phase 2B Findings Re-Verification

| Phase 2B claim | Re-verified this pass |
|---|---|
| `PostOutputToInventoryAsync` checks only `InventoryTransactionId.HasValue` | ✅ Confirmed — read the full method body (`ProductionRunService.cs`, pre-fix lines 454-475): the only guard before calling `ReceiveAsync` was the double-post check |
| It never reads `ProductionBatch.QualityStatus` | ✅ Confirmed — `output.ProductionBatch` was loaded via `.Include()` (needed only for the `BatchNumber` used in the `Reason` string) but `QualityStatus` was never referenced anywhere in the method |
| `ProductionBatch.QualityStatus` values are `Produced/AwaitingQC/Released/Blocked/Rejected` | ✅ Confirmed, re-read `ProductionBatch.cs` |
| No other caller passes a batch ID to `ReceiveAsync` | ✅ Confirmed via grep — `MaterialItemController.cs` is the only other caller, and it never supplies `productionBatchId` |

No discrepancy found between the Phase 2B report and the live code — the defect was exactly as
described, verified independently in this session before any change was made.

## 4. Confirmed Production Inventory Safety Gap

**Call chain, as it existed before this fix**:
```
ProductionRun → ProductionOutput (required ProductionBatchId) → ProductionBatch.QualityStatus
    → PostOutputToInventoryAsync   [checked ONLY: output.InventoryTransactionId.HasValue]
    → IMaterialStockService.ReceiveAsync  [no quality parameter existed or was needed by the caller]
    → InventoryTransaction (Receipt)
    → MaterialStock.OnHandQuantity += quantity   [immediately available via AvailableQuantity]
```

A `ProductionBatch` in `Blocked` or `Rejected` status — i.e., a batch a QC Inspector had
explicitly stopped or failed — could have its output posted to `MaterialStock`, becoming
indistinguishable from released, available stock. A `Produced` or `AwaitingQC` batch (the
default state immediately after recording output, before any QC decision at all) could be
posted just as easily. File: `PEMS.Persistence/Servises/ProductionRunService.cs`, method
`PostOutputToInventoryAsync` (pre-fix).

## 5. Current Quality Status State Machine

Re-confirmed, unchanged by this phase (`ProductionRunService.BatchQualityTransitions`):
```
Produced → AwaitingQC
AwaitingQC → Released | Blocked | Rejected
Blocked → AwaitingQC
```
Terminal-for-practical-purposes states: `Released` (positive), `Rejected` (negative, no
further transition defined). `Blocked` is recoverable back to `AwaitingQC`. This state machine
was **not modified** by this phase — the correction only adds a read-only check against it at
the inventory-posting boundary.

## 6. Quality Gate Decision

**Which statuses may post to inventory**: only `"Released"`. This was chosen over a narrower
"just block `Blocked`/`Rejected`" reading because `Produced` and `AwaitingQC` are equally
*not yet decided* — allowing them to post would mean inventory availability outruns the
quality decision, not just tolerating a known-bad batch. This directly matches Phase 2B
Section 11's recommended direction ("quality release required before inventory availability")
and Section 4.2's explicit instruction not to assume only `Released` is valid without
checking the state machine — the state machine confirms `Released` is the only state that
represents a completed, positive quality decision; every other state is either pending or
negative.

**Conceptual separation preserved** (per Section 4.2's required distinction):

| Concept | Where it lives | Affected by this fix? |
|---|---|---|
| Physically produced | `ProductionOutput` row exists | No — recording output is unaffected |
| Posted to inventory | `InventoryTransaction` row exists | **Yes — now gated** |
| On-hand | `MaterialStock.OnHandQuantity` | Indirectly — only reachable via a successful post |
| Available for allocation | `MaterialStock.AvailableQuantity` | Same as on-hand — this codebase has no separate blocked-quantity bucket, so "posted" and "available" are the same event, which is exactly why the gate has to live at the posting boundary, not a later step |
| Quality-released | `ProductionBatch.QualityStatus == "Released"` | Now the precondition for posting |
| Blocked/quarantined | `ProductionBatch.QualityStatus == "Blocked"` | Now explicitly rejected at posting |

## 7. Implemented Safety Correction

**Enforcement location**: inside `ProductionRunService.PostOutputToInventoryAsync` only
(Section 4.3's option 1). Justified because `IMaterialStockService.ReceiveAsync` — the shared
method this calls — has exactly one other caller in the whole codebase
(`MaterialItemController.cs`), and that caller never passes a `productionBatchId`, so it is
structurally unaffected and there is no bypass path today. Adding a second guard inside
`ReceiveAsync` itself was considered (Section 4.3's option 3, defense-in-depth) and **not
done**, per "choose the smallest change" — it would mean `ReceiveAsync` reaching back into
`ProductionBatch`, a concept it otherwise has no reason to know about, for no current benefit.
This is noted as a **documented design note**, not a gap: if a future caller (e.g., a
Packaging module) calls `ReceiveAsync` directly with a batch ID, it will need this same check
replicated or centralized at that time — flagged in Section 21.

**Override**: **none implemented, deliberately**. Section 4.4 explicitly allows "the override
should be avoided entirely" as a valid answer; building an override path would mean adding a
new permission-gated action, a new audit requirement, and a new business process, none of
which are confirmed as needed — that is exactly the kind of new-scope decision this phase's
Objective B defers to the business owner (Section 21, D-Override).

**Exact code change** (`PEMS.Persistence/Servises/ProductionRunService.cs`,
`PostOutputToInventoryAsync`):
```csharp
if (output.ProductionBatch == null || output.ProductionBatch.QualityStatus != "Released")
{
    var currentStatus = output.ProductionBatch?.QualityStatus ?? "نامشخص";
    return (false, $"این خروجی قابل ارسال به موجودی نیست - بچ مربوطه هنوز Released نشده " +
        $"(وضعیت فعلی: {currentStatus}). فقط بچ‌های Released قابل ارسال به موجودی هستند.");
}
```
Inserted immediately after the existing double-post guard, before any call to
`IMaterialStockService.ReceiveAsync` — so a rejected gate creates **zero** side effects (no
`InventoryTransaction`, no `MaterialStock` change, `output.InventoryTransactionId` remains
null), verified by dedicated tests (Section 9).

**Behavior defined precisely**:
- `Released` → posts normally, unchanged behavior from before this fix.
- `Produced`/`AwaitingQC` → rejected with an actionable error naming the current status.
- `Blocked`/`Rejected` → rejected, same error shape.
- No override exists (Section above).
- Returns a business-rule tuple `(false, message)`, **not an exception** — matching this
  method's existing convention for every other validation (double-post, already covered).
- **Idempotent**: a rejected attempt leaves the output row untouched (`InventoryTransactionId`
  still null), so the exact same call can be retried later once the batch is actually released
  — verified by `PostOutputToInventoryAsync_GateFailure_OutputRemainsNotPosted_
  CanBeRetriedAfterRelease`.
- **No existing posted transaction is touched** — the gate only runs before a *new* post; it
  does not scan or revisit any `InventoryTransaction` row already created before this fix.
- **Reversal/return paths (`ReturnAsync`, `TransferAsync`) are untouched** — this fix is scoped
  exclusively to the output-posting entry point, per Section 4.3's "smallest safe change"
  instruction; these methods were not part of the confirmed defect.
- This correction affects **only future posting attempts** — it cannot retroactively change
  any inventory state that already exists from before this fix (there were none in this
  environment's dev database in any case — see Section 11).

## 8. Files Changed

**Modified (3 files)**:
```
PEMS.Persistence/Servises/ProductionRunService.cs   — the gate itself (1 method, ~6 new lines)
PEMS.Tests/Cmms/ProductionRunServiceTests.cs         — 2 existing tests corrected (Section 9)
PEMS.Tests/Cmms/InventoryBatchTraceabilityTests.cs   — 2 existing tests corrected (Section 9)
```

**New (2 files)**:
```
PEMS.Tests/Cmms/ProductionInventoryQualityGateTests.cs   — 8 new focused tests (Section 10)
docs/audits/PEMS_OPERATIONS_PHASE_2C_PRODUCTION_SAFETY_AND_DECISION_BASELINE_REPORT.md (this file)
```

No entity, DTO, interface, migration, controller, view, or permission was created or modified.
No Packaging, Daily Operations, or quality-entity code was added, confirmed by this exact file
list.

## 9. Tests Added or Modified

**2 existing tests in `ProductionRunServiceTests.cs` were corrected**, not merely re-run:
`PostOutputToInventory_ReusesExistingInventoryTransaction_AndIsNotAutomatic` and
`PostOutputToInventory_Twice_IsRejected_NoDuplicatePosting` both previously posted a batch
straight from its default `"Produced"` status and asserted success — this was the exact
scenario the defect allowed. Both now call `SubmitBatchForQcAsync` then `ReleaseBatchAsync`
before posting, so they now assert the **corrected** behavior. This is disclosed transparently
per the task's own instruction not to silently weaken or hide test changes — these are fixes
*to* the tests, required *by* the fix, not a workaround of it.

**2 existing tests in `InventoryBatchTraceabilityTests.cs` corrected identically**:
`ProductionOutput_PostedToInventory_CreatesBatchLinkedTransaction` and
`PostOutputToInventory_Twice_StillRejected_WithBatchLink`.

**8 new tests** in `ProductionInventoryQualityGateTests.cs`, covering the Section 4.5
checklist directly:

| # | Task's requirement | Test |
|---|---|---|
| 1 | Released batch can post | `PostOutputToInventoryAsync_ReleasedBatch_Succeeds` |
| 2 | `AwaitingQC` behavior | `PostOutputToInventoryAsync_AwaitingQcBatch_IsRejected` |
| 3 | Blocked batch cannot post | `PostOutputToInventoryAsync_BlockedBatch_IsRejected` |
| 3b | Rejected batch cannot post | `PostOutputToInventoryAsync_RejectedBatch_IsRejected` |
| — | Original defect scenario (`Produced`, the default state) | `PostOutputToInventoryAsync_ProducedBatch_IsRejected_OriginalDefectScenario` |
| 5 | Duplicate posting remains prevented | Re-verified by the corrected existing tests (Section 9) |
| 6 | Gate failure creates no `InventoryTransaction` | `PostOutputToInventoryAsync_GateFailure_CreatesNoInventoryTransaction` |
| 7 | Gate failure does not alter `MaterialStock` | `PostOutputToInventoryAsync_GateFailure_DoesNotAlterMaterialStock` |
| 8 | Existing valid posting unchanged | Re-verified by the corrected existing tests |
| 9 | Return/reversal unchanged | Not modified by this fix (Section 7) — no new test needed since no behavior changed |
| 10 | Audit behavior intact | Automatic via the existing `PEMSDbContext.SaveChangesAsync` interceptor — no new code, no new test needed (same reasoning as every prior phase) |
| — | Idempotency / retry-after-release | `PostOutputToInventoryAsync_GateFailure_OutputRemainsNotPosted_CanBeRetriedAfterRelease` |

## 10. Regression Results

**Targeted run** (`ProductionInventoryQualityGateTests` + `ProductionRunServiceTests` +
`InventoryBatchTraceabilityTests` + `ProductionOrderServiceTests`):
```
Passed! Failed: 0, Passed: 41, Skipped: 0, Total: 41, Duration: 8s
```

**Full regression**:
```
dotnet build PEMS.slnx --configuration Debug
→ Build succeeded, 0 errors, 22 warnings (all pre-existing, none in any file this phase touched)

dotnet test PEMS.Tests/PEMS.Tests.csproj --no-build
→ Passed! Failed: 0, Passed: 1401, Skipped: 0, Total: 1401, Duration: 10m 46s
```
1401 = 1393 pre-existing (confirmed at the end of Phase 2A/2B) + 8 new tests from this phase.
**Zero regressions, zero skipped.**

## 11. Existing Data and Migration Impact

**No migration was created or applied in this phase** — the correction is pure application-code
logic (a new `if` check), not a schema change; no EF model change occurred. **No live data was
manually modified.** The dev database's `ProductionOrders`/`Runs`/`Batches`/`Outputs` tables
were confirmed empty in the prior Phase 2A report (never exercised through a live logged-in
session in this environment), so there is no existing production data this fix could have
retroactively affected even in principle — confirmed by re-checking `git status` shows no
migration file added by this phase.

---

## 12. Quantity Semantics Decision Baseline

*(No fields implemented in this phase — decision-only, consistent with Phase 2B Section 4.1,
restated here as the formal baseline this phase's brief requires.)*

| Item | State |
|---|---|
| Gross quantity | Undefined — no field exists |
| Tare quantity | Undefined — no field, no owning entity decided |
| Net quantity | Undefined — currently `ProductionOutput.Quantity` is a single, unlabeled number |
| Quantity basis (mass/volume/count/etc.) | Implicit via `MaterialUOM.Code` (free text), not enforced |
| UOM conversion | `MaterialUOM.ConversionFactor` exists, confirmed unused by any caller |
| Decimal precision | `decimal(18,3)` uniformly across every existing quantity column — any future field must match |
| Rounding | Not addressed — no rounding policy exists anywhere in the codebase |
| Measurement correction | No correction/adjustment concept exists for quantity facts specifically (the general `InventoryTransaction` "Adjustment" type exists but is not quantity-fact-specific) |
| Immutable transactional facts | **Recommended** or any future Gross/Tare/Net capture, matching the append-only philosophy already used throughout Production |
| Container-type tare vs. per-instance | **Business decision required** — not resolvable from code |

**Classification**: Business decision required. Blocking for Packaging.

## 13. Product Master Decision Baseline

`MaterialItem` **confirmed, again, as the sole authoritative Product/Material master** — no
new master should be created. A future `ProductionProductProfile` (1:1, optional) remains the
recommended extension point for `ProductRole`/`Family`/`Version`/`PackagingType`, not yet built.

| Item | State |
|---|---|
| Product role (Raw/Intermediate/Finished/etc.) | Undefined — `MaterialType` is free text |
| Bulk vs. packaged identity | Undefined |
| By-product/Waste/Rework material | Undefined |
| Packaging material/Container | Undefined — likely just another `MaterialItem` row (consumable), not a new concept, but not confirmed |
| Product grade | Already exists (`MaterialItem.MaterialGrade`) |
| Product form/packaging profile | Undefined, overlaps with the Package-vs-attribute boundary question (Section 15) |

**Classification**: Technical direction settled (reuse `MaterialItem`); specific field
taxonomy is a deferrable business decision, not blocking Packaging's core entity shapes.

## 14. Packaging Boundary Decision Baseline

**Recommended** (carried from Phase 2B, not re-litigated): Packaging as its own bounded
process, reusing the Production Order/Run/Input/Output/Loss/Event *pattern*, not a subtype of
`ProductionOrder`/`ProductionRun`. A transformation from `ProductionBatch` (bulk) to `Package`
(new). Repacking/relabeling recommended as a `PackagingRun` whose input can reference either a
`ProductionBatch` or an existing `Package` — this dual-reference shape is an explicit,
unresolved technical decision.

**Classification**: Recommendation exists; needs explicit sign-off before Phase 3 coding
begins. Blocking.

## 15. Package / Handling Unit / Pallet Decision Baseline

**Recommended hierarchy**: `MaterialItem` → `ProductionBatch` → `Package` → `HandlingUnit`
(with a `Type` field absorbing "Pallet" rather than a separate entity) → future
Shipment/Delivery Unit (out of scope).

| Item | State |
|---|---|
| Package split | Not supported by any existing pattern; recommended design (immutable identity, `ParentPackageId` on split) not built |
| Package merge | Recommended via `HandlingUnit`-to-`Package` many-to-many, not built |
| Repacking | See Section 14 |
| Package identity | Recommended immutable, matching `BarcodeIdentity`'s existing philosophy |
| Label replacement | **Already solved** by existing `BarcodeIdentity.Replace`/`PrintCount` mechanism — no new design needed once `Package` exists |
| Package contents change | Recommended: never in place — a new `Package`/new `BarcodeIdentity` instead |
| Package quantity | Undefined until Section 12 (Gross/Net/Tare) is resolved |
| Package status | **Recommended snapshot-and-own** (Section 17), independent of source batch after creation |
| Package location | Would follow the same transaction-level pattern already proven for batches (Phase 2A), not stock-level |
| Package ownership | Not addressed — no concept of "which department/cost-center owns this package" exists anywhere in PEMS |

**Classification**: Blocking for Packaging (D6 equivalent from Phase 2B, restated).

## 16. Batch Genealogy Decision Baseline

Unchanged from Phase 2B, re-confirmed: `ProductionBatch` has no parent/child structure; only
`ProductionInput.SourceProductionBatchId` (single pointer, partial-consumption-capable, not
split-capable). One batch → many packages: natural. One package ← multiple batches
(blending): achievable via multiple `PackagingInput` rows per run, **if built that way from the
start** — this cardinality decision is the actual blocker, not the concept itself. Batch
split/merge: not supported, deferred unless a concrete business case for independent sub-batch
quality tracking is shown. Expiration/retest date: absent, open question on need.

**Classification**: Package-to-batch cardinality is blocking for Packaging; split/merge is
deferrable unless proven needed.

## 17. Quality and Release Decision Baseline

Batch-level `QualityStatus` remains authoritative and **now correctly gates inventory
availability** (Section 7's fix). Package-level status recommended as an independent,
snapshot-at-creation field (not a live reference to the batch) — for the same reason the batch
itself doesn't reference anything live upstream: a package already shipped must not
retroactively change status just because its source batch's status changes later.
`QualityTestResult` confirmed absent; if built, belongs as a new entity linked to
`ProductionBatch`/`Package`, not as an extension of the Equipment-scoped `InspectionRecord`.

The five-way distinction (physical on-hand / available / quality-released / blocked /
rejected) is now **partially resolved for batches** by this phase's fix (released = available,
everything else = not postable) but remains **entirely undefined at the package level**, since
`Package` doesn't exist yet.

**Classification**: Batch-level gate now correct (this phase). Package-level model is a
blocking decision for Packaging (D11 equivalent from Phase 2B).

## 18. Rework / Loss / Yield Decision Baseline

Unchanged from Phase 2B: `"Rework"` is a pure, permanent `ProductionLoss` category with no
re-entry mechanism, regression-tested as the current baseline. Recommended (not built): a
later `ProductionOutput` referencing `SourceProductionLossId`, potentially into a different
`MaterialItem` (downgraded SKU), tagged via a new `OutputClassification` value
(`"ReworkRecovery"`) so primary-yield reporting can exclude it without a schema change.

**Classification**: Deferrable — does not block basic Packaging, only mass-balance/yield
reporting accuracy.

## 19. Operational Calendar Decision Baseline

Unchanged from Phase 2B's five-calendar analysis (Storage/Display/Operational/Reporting/
Shift). `ReportingPeriod.cs` remains Day/Month-deterministic, Week deliberately throwing
without an explicit `weekStartDay`. No operational-day-start-time, no multi-plant calendar
support, no midnight-crossing-shift attribution rule exists anywhere.

**Classification**: Blocks reliable daily/period reporting; does not block Packaging's core
entity existence.

## 20. Daily Operations Decision Baseline

Unchanged recommendation from Phase 2B: a controlled submission/reconciliation layer over
existing facts (`Draft→Submitted→UnderReview→Approved→ReturnedForCorrection/Rejected→Locked`),
storing **zero quantity columns of its own** — every number computed live from
`ProductionOutput`/`ProductionLoss`/(future `PackagingOutput`) within its scope. This is a hard
design constraint (double-counting prevention), not a preference.

**Classification**: Blocking for Daily Operations specifically; independent of Packaging.

---

## 21. Blocking Decisions Before Packaging

Consolidated from Sections 12-20, matching Phase 2B's register (same IDs, for continuity):

| ID | Decision | Blocking? |
|---|---|---|
| D1/D2 | Gross/Net/Tare definition and ownership | **Blocks Packaging** |
| D5 | Packaging process boundary sign-off | **Blocks Packaging** |
| D6 | Package/HandlingUnit/Pallet shape sign-off | **Blocks Packaging** |
| D8 | Package-to-batch genealogy cardinality (blending) | **Blocks Packaging** |
| D11 | Package-level quality status model + release permission scope | **Blocks Packaging** |
| D-Override (new this phase) | Should an authorized override path for posting non-`Released` output ever exist, and under what permission/audit? | Not blocking (a hard "no" was chosen for this phase); revisit only if a genuine business need is raised |
| D-ReceiveGuard (new this phase) | Should the `QualityStatus` check be duplicated inside `IMaterialStockService.ReceiveAsync` itself once a second caller (e.g., Packaging) passes a batch ID? | Not blocking now; **must be revisited when Packaging's own inventory-posting path is designed** |
| D7, D9, D13, D14, D16, D19 | (Batch split/merge, Rework, Operational day, Week start, Daily Operations shape, Offline/mobile) | Deferrable, not blocking Packaging's core existence |

## 22. Explicitly Deferred Scope

Unchanged from every prior phase: `PackagingOrder`/`Run`/`Input`/`Output`/`Loss`/`Event`,
`Package`, `HandlingUnit`, `Pallet`, `QualityTestResult`, `DailyOperationsSubmission`, Gross/
Tare/Net fields, batch split/merge, rework re-entry, new barcode `TargetType`s, Sales/Dispatch,
EPCIS/GS1. Nothing in this phase expands that list — Objective A closed one narrow, confirmed
gap; Objective B only formalized the existing decision register.

## 23. Recommended Next Phase

Resolve D1/D2/D5/D6/D8/D11 (Section 21) with the business owner before Phase 3 (Packaging
Foundation) begins coding. D-ReceiveGuard should be explicitly revisited at that time, not
forgotten. Everything else in Phase 2B's roadmap (Sections 15-16 of that report) remains
accurate and is not restated here.

---

## 24. Final Readiness Classification

# A. SAFETY CORRECTION COMPLETE — PACKAGING STILL BLOCKED BY BUSINESS DECISIONS

The confirmed defect was verified against the live code (not assumed from the prior report),
corrected with the smallest change that closes the gap with no bypass path (verified by
confirming `ReceiveAsync`'s only other caller never passes a batch ID), and proven by 8 new
focused tests plus 4 corrected pre-existing tests that had encoded the bug as expected
behavior. This is not classification C — nothing in this fix or its surrounding audit
surfaced a *broader* architectural issue beyond the one already identified and now closed;
the two new open questions it raised (D-Override, D-ReceiveGuard) are narrow, well-understood
follow-ups, not signs of deeper unsoundness. Packaging itself remains blocked by the same
business decisions Phase 2B identified — this phase did not resolve them (it was not asked
to), only formalized them into a baseline register.

## 25. Compliance Confirmation

- The confirmed defect was verified against actual code before any change was made (Section 3-4).
- The smallest safe correction was made — one method, one new check, no new entities, no new
  permissions, no new migration.
- No existing posted inventory data was altered (none existed in this environment; the fix is
  forward-only in any case).
- No Packaging, Daily Operations, `QualityTestResult`, or Gross/Net/Tare code was created.
- Only focused tests required to prove the correction were added or corrected (Section 9-10).
- Build succeeded; targeted and full regression results are in Section 10.
- No existing functionality was removed.
- No migration was created (none was needed).
- No live data was manually changed.
- No commit or push was performed as part of this task.
