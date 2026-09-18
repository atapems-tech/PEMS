# PEMS Operations Phase 2B — Business Semantics & Packaging Design Gate

**Status: AUDIT AND DESIGN-GATE ONLY. No entity, migration, service, controller, API, view,
test, or permission was created or modified to produce this document. No commit or push was
made.**

---

## 1. Executive Summary

Phase 1 and Phase 2A built a genuinely solid, extensible Production Core — and this deeper
audit found nothing in that existing code requiring rework. But it surfaces **more unresolved
business semantics than the prior audits captured**, several of them newly identified in this
pass by reading the actual current entities line-by-line rather than summarizing prior
conclusions:

- **A real, previously-unflagged gap in already-shipped Phase 1 code**: `ProductionRunService.
  PostOutputToInventoryAsync` does **not** check `ProductionBatch.QualityStatus` before posting
  to inventory — a `Blocked` or `Rejected` batch's output can physically be received into
  `MaterialStock` today. This is not a Packaging problem; it is a live gap in the existing
  Production Core, surfaced by asking Section 11's inventory-boundary question against the
  actual code rather than the prior report's description of it. (Section 11, Section 19.)
- **`ProductionBatch` has no split/merge/parent-child structure at all** — only a single
  `SourceProductionBatchId` genealogy pointer on `ProductionInput` (one input references one
  source batch; a batch can be the source for many inputs). This is sufficient for
  *partial-quantity consumption* of a batch but **not** for a batch itself acquiring
  independent quality sub-statuses (e.g., half a batch released, half blocked) or for two
  batches merging into one. (Section 6.)
- **Package-level quality status is architecturally undecided** — should a package snapshot
  its source batch's quality status at creation (mutable afterward, independent of the batch)
  or reference it live? This audit recommends snapshot-and-own, for the same reason
  `ProductionBatch.QualityStatus` itself is not a live reference to anything — but this is a
  new design decision, not yet made anywhere in the codebase. (Section 5.2, Section 8.)
- **Gross/Net/Tare remains completely undefined**, confirmed again by direct inspection —
  `ProductionOutput.Quantity` and `MaterialStock.OnHandQuantity` are both single,
  undifferentiated numbers with no basis, tare, or multi-UOM concept attached. (Section 4.1.)
- **`BarcodeIdentity`'s existing Replace/reprint lifecycle (already built in Phase 1) is
  directly reusable for package labels with zero new design work** — a genuinely positive,
  concrete finding this audit contributes: reprinting was already solved generically. (Section
  12.)

**Final classification (Section 22): ARCHITECTURE READY WITH REQUIRED BUSINESS DECISIONS.**

---

## 2. Audit Scope and Repository Version

Repository: `D:\PEMS\03-SOURCE\PEMS-cmms-edit-empoleeys-master\PEMS-cmms-edit-empoleeys-master`,
branch `main`, at commit `2cb1c43` (the tip after the Phase 2A `chore(graphify)` commit — the
most recent commit in the repository at the time of this audit). All file/line evidence below
was read directly from this commit's working tree during this audit session, not carried
forward unverified from prior reports.

## 3. Files and Modules Inspected

Re-read directly in this pass (not assumed from prior reports): `ProductionBatch.cs`,
`InventoryTransaction.cs`, `MaterialStock.cs`, `BarcodeService.cs`
(`SupportedTargetTypes`), `InspectionRecord.cs`. Confirmed absent by repository-wide search in
this pass: any `Package`, `HandlingUnit`, `Pallet`, `QualityTestResult`, `TareQuantity`,
`GrossQuantity`, `NetQuantity` class anywhere in `PEMS.Domain`. Findings from the three prior
audits and the Phase 1/2A implementation reports (`PEMS_OPERATIONS_PRODUCTION_ARCHITECTURE_
SPEC_v1.md`, `PEMS_OPERATIONS_PRODUCTION_PHASE1_IMPLEMENTATION_REPORT.md`,
`PEMS_OPERATIONS_ARCHITECTURE_AND_PERIOD_REPORTING_AUDIT.md`,
`PEMS_OPERATIONS_CMS_BOUNDARY_CORRECTION_REPORT.md`,
`PEMS_OPERATIONAL_DATA_AND_PERIOD_REPORTING_ARCHITECTURE_AUDIT.md`,
`PEMS_OPERATIONS_PHASE_2A_FOUNDATIONAL_HARDENING_REPORT.md`) are cited and marked **(carried
forward, re-confirmed)** where this pass re-verified them, and marked **(new this pass)**
where this audit found something the prior documents did not state.

---

## 4. Current Production and Inventory Semantics

### 4.1 Gross/Tare/Net Quantity Analysis

**Verified, not inferred**: `ProductionOutput.Quantity` (`PEMS.Domain/Entities/
ProductionOutput.cs`) is a single `decimal` with no companion `Gross`/`Tare`/`Net` field.
`MaterialStock.OnHandQuantity` (re-read this pass) is likewise a single `decimal`. `MaterialUOM.
ConversionFactor` exists but is confirmed unused anywhere in application code (carried forward,
re-confirmed — no caller reads it). There is no concept anywhere of a quantity's *basis* (mass
vs. volume vs. count vs. length vs. area) beyond whatever `MaterialUOM.Code` implies as a
free-text label.

**Direct answers to the task's questions**:

| Question | Answer |
|---|---|
| Is `Quantity` gross, net, or undefined? | **Undefined** — no field or comment anywhere states which |
| Is the basis fixed or variable per product? | Implicit via `MaterialUOM`, but no enforcement that a given `MaterialItem` always uses a compatible basis |
| Can the same product use multiple UOMs? | Technically yes (every quantity field carries its own optional `UomId`), but with no conversion — two `ProductionOutput` rows for the same batch in different UOMs would sum incorrectly if naively added |
| Is conversion supported/authoritative? | **No** — confirmed again, `ConversionFactor` is dead data |
| Is tare an attribute of packaging, container, vehicle, or transaction? | **Not modeled anywhere yet** — this is a genuine design choice for Phase 3, not an existing convention to discover |
| Can gross/net be captured at different stages? | Architecturally yes (nothing prevents adding fields at the point of capture), but no stage currently does |
| Should gross/net be immutable transactional facts? | **Recommended yes** — matches the append-only philosophy already used for `ProductionOutput`/`InventoryTransaction`; a weighing correction should be a new fact, not an edit to the original |
| Can net be calculated reliably, or must it be measured? | Depends entirely on whether tare is a known constant (container type) or a per-instance measurement (a specific truck's actual tare) — **this is exactly the open business decision, not resolvable from code** |
| Rounding/precision? | All existing quantity columns use `decimal(18,3)` uniformly (confirmed via every EF configuration touched across Phases 1-2A) — any new quantity field should match this convention, not introduce a different scale |
| Can a production output contain multiple quantity bases? | Not today (one `Quantity` + one `UomId` per row); if genuinely required (e.g., a liquid product reported in both mass and volume), this needs either multiple output rows or a multi-basis extension — **open question** |

**Recommended conceptual model options** (none implemented):

1. **Status quo, contextually interpreted** — cheapest, but the ambiguity risk in Section 19 (double-counting/misreporting) is real and already flagged as this audit's top risk.
2. **Explicit `GrossQuantity` + `TareQuantity` + stored (not recomputed) `NetQuantity`** on the relevant fact table(s) — most traceable, matches the append-only-fact philosophy, but requires the business to define *whose* tare (container type default vs. actual instance).
3. **Tare as a property of a future `Package`/`HandlingUnit` *type*** (a drum type's standard tare weight), with an optional override on the specific instance if re-weighed — most physically accurate, but requires the Package/Container-type master data (Section 5.2) to exist first, so it cannot be built before that decision.

**Risk of implementing Packaging before this is resolved**: every Packaging KPI that involves
weight/quantity reconciliation (Section 15's mass-balance reporting) would be built on an
undefined basis, and retrofitting Gross/Net after real transactional data exists is far more
disruptive than deciding it first.

### 4.2 Product Master and Product Identity Analysis

**(Carried forward, re-confirmed)**: `MaterialItem` remains the correct authoritative Product/
Material master — re-verified it still has no `ProductRole`/`ProductForm`/`PackagingType`
field. `MaterialType` is a free string (e.g., "Chemical"/"Valve"), not a controlled Raw/
Intermediate/Finished/Bulk/Packaged/By-product/Waste/Rework/Packaging-component/Container/
Pallet/Label taxonomy.

**Evaluated options**:

| Option | Problem solved | Duplication introduced | Verdict |
|---|---|---|---|
| 1. `MaterialItem` as sole master | Already true today | None | **Recommended base** |
| 2. Production-specific profile linked to `MaterialItem` (1:1, e.g. future `ProductionProductProfile`) | Adds `ProductRole`/`Family`/`Version`/`PackagingType` without polluting the 50k-row spare-parts catalog | None if genuinely 1:1 and optional | **Recommended extension**, same conclusion as the prior audit, now explicitly including a `ProductRole` field to answer this task's Raw/Intermediate/Finished/etc. question |
| 3. Separate product master | — | Recreates the exact `SparePart`-vs-`MaterialItem` mistake this codebase has already paid for once | **Rejected** |
| 4. Aliases/variants | Handles alternate codes (customer-facing vs. internal) | Low, if scoped to a thin alias table | Only if a real business need is confirmed (unresolved, Section 18) |
| 5. Product-form/packaging-profile layer | Distinguishes bulk vs. packaged identity of "the same" product | Could be the *same* thing as option 2's `PackagingType` field, or could require a genuinely separate `Package` entity (Section 5.2) — the boundary between "a product attribute" and "a physical unit" is the real question | See Section 5.2 |

Every downstream system (inventory, production, packaging, quality, reporting, barcode) should
reference `MaterialItemId` as today — no change to that reference pattern is proposed.

---

## 5. Packaging Domain Design Audit

### 5.1 Packaging Process Boundary

**Recommendation, carried forward and sharpened from the prior operational-data audit**:
Packaging should be its own bounded process, reusing the exact Order/Run/Input/Output/Loss/
Event *pattern* Phase 1 established for Production — not a subtype of `ProductionOrder`/
`ProductionRun`, and not a warehouse operation. Reasoning:

- It genuinely is **a transformation from bulk product (a `ProductionBatch`) to packaged
  product (a future `Package`)** — structurally the same shape as
  `ProductionInput.SourceProductionBatchId`'s existing genealogy pointer, just on the
  *packaging* side of the boundary instead of the production side.
- Packaging has different actors, different loss categories (damaged package vs. process
  loss), and different output shape (package count/size, not just a quantity) than Production
  — conflating them into one state machine would either pollute `ProductionRun` with
  packaging-only fields or force packaging facts through a table shaped for a different
  concept.
- Repacking/relabeling is the one genuinely ambiguous case: is it a new `PackagingRun` whose
  input is an *existing* `Package` rather than a `ProductionBatch`? Recommended: yes, reuse the
  same `PackagingRun` concept, but this requires `PackagingInput` to optionally reference
  *either* a `ProductionBatch` *or* an existing `Package` — an explicit design decision, not
  resolved here (Section 18, decision register).

### 5.2 Packaging Hierarchy

```
MaterialItem (Product)
   └── ProductionBatch (bulk, existing)
          └── Package (new — one batch → many packages, natural; one package ← multiple
                        batches, i.e. blending — supported by the same "many fact rows per
                        run, each with its own batch reference" pattern ProductionInput
                        already uses, not a blocker)
                 └── HandlingUnit (new — aggregates Packages; recommend HandlingUnit with a
                                    Type field, e.g. "Pallet"/"Carton", rather than a
                                    separate Pallet entity, to avoid entity proliferation for
                                    what is fundamentally the same concept — an aggregation
                                    container)
                        └── Shipment/Delivery Unit (out of scope — Sales/Dispatch not yet
                                                      designed at all, per every prior audit)
```

**Direct answers**:

| Question | Answer |
|---|---|
| One batch → many packages? | Yes, structurally natural |
| One package ← multiple batches (blending)? | Yes, achievable via multiple `PackagingInput` rows per run, each with its own batch reference — not a blocker if designed this way from the start |
| One pallet ← packages from multiple batches? | Yes, same reasoning at the `HandlingUnit` level |
| Package split? | **Not supported by any existing pattern** — recommend, if needed, an immutable-identity model: splitting creates new child `Package` rows with a `ParentPackageId`, the original marked consumed/closed, mirroring the batch-genealogy pointer already used elsewhere. Not designed in full here. |
| Package merge? | Via the `HandlingUnit`-to-`Package` many-to-many join — packages keep their own identity, they are not merged into one row |
| Repacking creates a new package identity? | Recommended yes, for traceability — an old package doesn't silently become a new one |
| Package identity immutable? | **Recommended yes** — matches `BarcodeIdentity`'s own existing "never mutate, only Replace" philosophy |
| Labels reprintable, audited? | **Already solved** — see Section 12; `BarcodeIdentity`'s existing `Replace`/`PrintCount`/`LastPrintedDate` mechanism is directly reusable, no new design needed |
| Package status separate from batch status? | **Recommended yes, snapshot-and-own** — a package should copy its source batch's `QualityStatus` at creation time, then be independently mutable afterward. This is a *new* decision (Section 8), not yet made anywhere in the codebase, and is the correct pattern for the same reason `ProductionBatch.QualityStatus` itself is not a live reference to anything upstream — if a package's status stayed live-linked to its batch, releasing the batch after some packages already shipped would retroactively (and wrongly) "release" packages no one re-inspected. |
| Empty packaging materials tracked? | Not addressed by this audit in depth — if empty containers/labels are themselves `MaterialItem` rows consumed as `ProductionInput`-equivalents by a `PackagingRun`, no new architecture is needed; flagged as an open question for Phase 3 design, not a blocker |

---

## 6. Batch, Lot, and Genealogy Audit

**Re-verified this pass, not assumed**: `ProductionBatch.cs` has no `ParentProductionBatchId`,
no `ChildBatches` collection, no `ExpirationDate`/`RetestDate`. The only genealogy mechanism in
the entire codebase is `ProductionInput.SourceProductionBatchId` — a single, optional FK,
meaning "this input's material came from this one specific prior batch." It supports **partial
consumption** of a batch (the input records a quantity, deducted conceptually but not
physically split from the batch) but **not** batch splitting into independently-tracked
sub-lots with their own quality status.

**Phase 2A's batch-aware inventory, evaluated directly against this task's questions**:

| Phase 2A element | What it actually provides | What it does not provide |
|---|---|---|
| `InventoryTransaction.ProductionBatchId` (nullable) | **Transaction-level** traceability — "this specific movement was tied to this batch" | Stock-level balance by batch (see next row) |
| `GetBatchBalancesAsync` | A `GROUP BY` read-model over the ledger — batch-level *balance*, derived, not stored | Package-level balance (Packages don't exist yet); physical/location *current state* caching (it's always re-derived from the full transaction history, correct but not indexed for point-in-time lookups at scale) |
| No automatic batch inference | Confirmed still true — a caller must know and supply the batch | — |
| No dedicated batch-stock projection table | Deliberate (Phase 2A's own stated reasoning: avoid changing `MaterialStock`'s grain) | If batch-level balance queries become a hot path at real volume, this remains a future performance question, not a correctness one |

**Clear distinctions, as requested**:
- **Transaction-level batch traceability**: ✅ exists (Phase 2A).
- **Batch-level stock balance**: ✅ exists as a derived query (Phase 2A), not a stored/cached value.
- **Package-level stock balance**: ❌ does not exist (Package doesn't exist).
- **Physical location tracking**: partial — `StorageLocation` exists and every transaction carries it, but there is no "current location of batch X" cache; it's reconstructable, not indexed.
- **Warehouse availability**: ✅ `MaterialStock.AvailableQuantity` (existing, unchanged).
- **Quality availability**: ❌ — `MaterialStock`/`GetBatchBalancesAsync` do not filter by `QualityStatus` automatically; a consumer must explicitly join, and (per Section 19's new finding) the existing `PostOutputToInventoryAsync` doesn't even gate on it.

**Batch expiration/retest date**: not present on `ProductionBatch` today. If the business's
products require shelf-life tracking (common in chemical/pharma-adjacent industries), this is
a small, additive gap (two nullable `DateTime` columns) — but is an open question whether it's
needed at all, not assumed here.

---

## 7. Rework, Loss, and Yield Semantics

**(Carried forward, re-confirmed)**: `ProductionLoss.LossCategory` includes `"Rework"` as one
of six controlled values, but a Rework loss is recorded as a pure, permanent loss — there is no
mechanism anywhere for reworked material to re-enter production as a later `ProductionOutput`.
This was deliberately left unimplemented in Phase 2A and is regression-tested
(`RecordLoss_RworkCategory_IsRecordedAsPureLoss_NoAutomaticReEntryIntoProduction` in
`ProductionRunServiceTests.cs`) as the documented current baseline, not silently glossed over.

**Decision matrix (not implemented)**:

| Option | Shape | New architecture required | Verdict |
|---|---|---|---|
| A. Loss + a later `ProductionOutput` referencing `SourceProductionLossId` | Reuses existing fact tables, one new optional FK | Minimal — one nullable column | **Recommended baseline** |
| B. Rework recovers into a *different* `MaterialItem` (a downgraded/reprocessed SKU) | Reuses `MaterialItem` (no new master) via a different `MaterialItemId` on the recovery output | None beyond A | **Recommended when applicable** — combine with A |
| C. Dedicated `ReworkOrder` bounded process | Full parallel Order/Run pattern for rework specifically | Significant — a fourth process family alongside Production/Packaging | **Only if rework has genuinely distinct actors/approvals** — not justified by current evidence, deferred |

**Mass balance / double-counting guard**: if Option A+B is eventually built, a rework recovery
`ProductionOutput` must **not** also be counted in "primary production" totals without an
explicit flag distinguishing it (e.g., reusing the existing `OutputClassification` field —
`"Primary"`/`"ByProduct"` already exists on `ProductionOutput`; a third value such as
`"ReworkRecovery"` would let reporting exclude it from primary-yield calculations without a
schema change). This is a concrete, low-risk extension point already present in the codebase,
not a new concept.

---

## 8. Quality Integration Audit

**Re-verified this pass**: `InspectionRecord.EquipmentId` is a **required** `int` (not
nullable), confirming — beyond the prior audits' conclusion — that this entity is structurally
Equipment-scoped and cannot represent a product/batch/package quality test without a breaking
schema change. No `QualityTestResult` entity exists anywhere (repository-wide search, this
pass, zero matches). `ProductionBatch.QualityStatus` remains the only quality concept in the
Operations domain.

**Direct answers**:

| Question | Answer |
|---|---|
| Evaluated at product, batch, package, or all three level? | Today: batch only. Recommended for Packaging: batch **and** package, independently (Section 5.2) |
| Can a batch be produced but not released? | ✅ Already true — `Produced`/`AwaitingQC` states exist before `Released` |
| Can individual packages be blocked while the batch is partially available? | Not today (no Package); **achievable** under the recommended snapshot-and-own package status design |
| Can package status differ from source batch status? | Same as above — yes, by design, once built |
| Is `QualityTestResult` available? | **No, confirmed absent.** If built, it belongs as a new entity 1-to-many from `ProductionBatch` (and future `Package`) — **not** as an extension of `InspectionRecord`, which is a different bounded context (asset condition monitoring) |
| Should Packaging output be auto-blocked pending QC? | Recommended: packages should default to the batch's status at creation time (which, per the existing quality gate, is already never `Released` before `AwaitingQC`→`Released` — so a freshly-created package's default status is already conservative by construction, no new "auto-block" rule needed beyond reusing the existing default) |
| Who can release a batch or package? | Batch release already reuses the existing `"Production"` permission module's `Approve` action + the `Production.Create↔Approve` SoD pair (Phase 1). Whether package release reuses the same module or needs its own is an **open decision** (Section 18) |
| Can release be partial? | Yes, by construction, once package-level status is independent (Section 5.2) — no fractional-quantity mechanism needed on the batch itself |
| How are test results linked to the exact material/batch/package? | Not resolved — depends entirely on whether `QualityTestResult` is built, and its FK shape (Section 18) |
| Can quality status changes affect inventory availability? | **Not today** — see Section 19's new finding: `PostOutputToInventoryAsync` does not check `QualityStatus` at all currently |

---

## 9. Operational Day, Calendar, and Shift Semantics

**(Carried forward, re-confirmed, with the five-calendar separation this task explicitly
requests)**:

| Calendar type | Current state |
|---|---|
| **Storage calendar** | Server-local, naive `DateTime` everywhere (no UTC layer) — the unanimous convention across 100% of the codebase, not something Operations should deviate from alone |
| **Display calendar** | Gregorian only — confirmed again, zero real Jalali/Persian-calendar conversion code anywhere in the application (only in unrelated design-mockup scaffolding) |
| **Operational calendar** | **Undefined** — no operational-day-start-time concept exists anywhere; `ReportingPeriod.Resolve(PeriodType.Day, ...)` uses plain calendar-day (midnight-to-midnight) boundaries by necessity, not by business confirmation |
| **Reporting calendar** | `ReportingPeriod.cs` — Day/Month deterministic; Week **intentionally throws** without an explicit `weekStartDay` (no guessed default shipped); `CustomRange` deterministic |
| **Shift calendar** | `ShiftDefinition.StartTime`/`EndTime` are `TimeSpan`-of-day only — **not linked to any operational-day concept**, so a 22:00–06:00 shift has no defined date attribution rule |

**New answers this pass**: multiple plants with different calendars are **not supported** —
the entire date-handling convention implicitly assumes one shared server-local timezone/
calendar for the whole deployment; holidays/shutdown days are **not modeled anywhere**; a
production run crossing two shifts has **no attribution rule** (its `ProductionOutput.
TransactionDate` rows are simply timestamped whenever each entry was made, with no
"which shift does this row belong to" derivation beyond whatever `ShiftDefinitionId` was
manually selected on the `ProductionRun`, which is Run-level, not per-output-row — a
previously-noted limitation, re-confirmed).

---

## 10. Daily Operations Design Analysis

Evaluating the four framings this task poses directly:

1. **Transactional source of production facts** — **Rejected**, as in the prior audit: this
   would duplicate `ProductionOutput`/`ProductionLoss`, creating exactly the double-counting
   risk Section 19 elevates to the top concern.
2. **Controlled reporting/submission layer over existing facts** — **Recommended baseline**: a
   scope (date/shift/work-center), a lifecycle (`Draft→Submitted→UnderReview→Approved→
   ReturnedForCorrection/Rejected→Locked`), and zero quantity columns of its own — every number
   it displays is computed from `ProductionOutput`/`ProductionLoss`/(future `PackagingOutput`)
   within its scope, at read time.
3. **Reconciliation and approval layer** — Not a separate concept from (2); the approval *is*
   what makes a period's reconciliation official. Recommend folding this into the same entity
   as (2) rather than inventing a second container.
4. **Hybrid** — What is actually recommended: option (2)/(3) combined, i.e., the record's *own*
   new data is purely the submission/approval metadata; all quantities remain sourced live from
   existing facts.

**Explicit double-counting risk assessment**: if Daily Operations is ever built as option 1 (or
as a hybrid that also stores redundant quantity snapshots "for performance"), any report
summing both `ProductionOutput` and a Daily Operations quantity for the same scope would
overcount. This risk is structurally avoided only if the record never stores a quantity column
of its own — a hard design constraint, not a soft preference.

**Correction/locking**: a "Locked" submission should not freeze the underlying
`ProductionOutput`/`ProductionLoss` rows (which are already immutable-by-construction, per
Phase 1's append-only design) — it should only prevent a *report* from silently including
facts posted *after* the lock without an explicit, visible re-open action. Historical report
reproducibility is preserved automatically as long as the submission is reference-only.

---

## 11. Inventory Receipt Boundary Analysis

**New finding, not present in any prior report**: re-reading `ProductionRunService.
PostOutputToInventoryAsync` in full during this audit confirms it checks only
`output.InventoryTransactionId.HasValue` (the double-post guard) before calling
`IMaterialStockService.ReceiveAsync` — **it does not check `ProductionBatch.QualityStatus` at
all**. A `Blocked` or `Rejected` batch's output can be posted to inventory today, physically
indistinguishable in `MaterialStock` from a `Released` batch's output once posted. This is a
gap in already-shipped Phase 1 code, surfaced by this audit's inventory-boundary questions, not
a hypothetical Packaging concern.

**Evaluating the task's 6 options against this finding**:

| Option | Analysis |
|---|---|
| 1. Direct post, no gate | **This is what exists today** — confirmed above, and confirmed to be a real gap |
| 2. Pending stock record, then separate warehouse confirmation | More steps, doesn't by itself solve the quality-gate gap unless the confirmation step also checks status |
| 3. Batch/package record + separate physical-location confirmation | Conflates two different concerns (quality gate vs. physical receipt) |
| 4. Atomic production-fact + inventory-transaction in one operation | Directly **contradicts** Phase 1's own AD-Phase1-01 decision (operational recording and inventory posting are deliberately two separate, explicit steps) — not recommended |
| 5. **Quality release required before inventory availability** | **Recommended direction** — but requires deciding *where* the gate lives: inside `PostOutputToInventoryAsync` itself (simplest, closes the existing gap directly) vs. a separate pre-check the caller must perform (more flexible, easier to bypass by accident) |
| 6. Physically produced but blocked/quarantined | Compatible with option 5 — a `Blocked`/`Rejected` batch's output could still be *physically* received (it exists in the warehouse) while remaining *unavailable* for allocation; this requires `MaterialStock` (or the future batch-aware balance) to distinguish on-hand-but-blocked from on-hand-and-available, which circles back to the Section 6 gap (no quality filter on stock queries today) |

**Recommendation (design only, not implemented)**: extend `PostOutputToInventoryAsync` (and,
when built, its Packaging equivalent) to check `QualityStatus` before posting, with an explicit
override path requiring the same `Approve`-level permission already used for batch release —
this closes the existing gap using infrastructure that already exists (the `"Production"`
permission module's `Approve` action, already SoD-paired) rather than inventing a new gate
mechanism.

---

## 12. Barcode and Traceability Analysis

**(Carried forward, re-confirmed via fresh read of `BarcodeService.SupportedTargetTypes`)**:
currently `{"Equipment", "MaterialItem", "Warehouse", "StorageLocation", "ProductionBatch"}`.
Adding `"Package"`/`"HandlingUnit"` requires zero schema change — the same four code
locations (the hash set + three switch methods + `BarcodeController`'s routing switch)
identified in every prior audit.

**New, concretely positive finding this pass**: `BarcodeIdentity`'s existing lifecycle already
solves the reprint/traceability requirements this task asks about, without any new design:

| Requirement | Already solved by |
|---|---|
| Labels reprintable | `IBarcodeService.RecordPrintAsync` + `PrintCount`/`LastPrintedDate` (existing) |
| Reprinting audited | Every print increments a counter and timestamp on the existing entity; combined with the automatic `AuditLog` interceptor, this is already a full audit trail |
| Codes immutable | `BarcodeIdentity.Code` is never mutated after creation (existing) |
| A barcode identifies exactly one target, never ambiguous | The `TargetType`/`TargetId` pair + filtered unique index (existing) |
| Old label superseded, not silently changed | `ReplacedByBarcodeIdentityId`/`ReplacesBarcodeIdentityId` chain (existing) — directly answers "can package contents change after labeling": **no**, a changed package should get a *new* `BarcodeIdentity` via `ReplaceAsync`, not a mutated old one |

**Confirmed gaps**: no GS1/GTIN/SSCC compliance (deliberately, per the original architecture
spec's AD-6 decision — an optional future carrier layer, not retrofitted); no offline/mobile
scanning (the PWA is deliberately network-only, confirmed again); duplicate-scan prevention is
not a meaningful concept at the *resolve* layer (resolving the same code twice is idempotent by
design) — any "duplicate scan" risk belongs to whatever state-changing *action* a scan
triggers (e.g., "confirm receipt"), which does not exist yet for Packaging and should get its
own idempotency guard when built, following the same `InventoryTransactionId == null`
double-post-guard pattern `PostOutputToInventoryAsync` already uses.

---

## 13. Reuse / Extend / New Architecture Matrix

| Concept | Existing Component | Current Capability | Reuse/Extend/New | Reason | Risks | Required Decision |
|---|---|---|---|---|---|---|
| Product master | `MaterialItem` | Full catalog, no packaging/role fields | **Reuse + Extend** (future profile) | Avoid a second master | Low | `ProductRole` field shape |
| Production Order/Run/Batch/Input/Output/Loss/Event | Existing (Phase 1) | Fully implemented, tested | **Reuse unchanged** | Proven pattern | None | — |
| Work Center / Shift | `ProductionWorkCenter`/`ShiftDefinition` | Existing | **Reuse directly** for Packaging too | No parallel master data | None | — |
| Production Target / Downtime | Existing (Phase 2A) | Implemented, no UI | **Reuse** | — | None | — |
| Inventory ledger/projection | `InventoryTransaction`/`MaterialStock` | Batch-aware ledger (Phase 2A), unchanged projection grain | **Reuse + Extend** (quality-gate check, Section 11) | Do not fork a second ledger | **Existing gap** (Section 11) | Where the quality gate lives |
| Warehouse/StorageLocation | Existing | Unchanged | **Reuse** | — | None | — |
| Quality (batch-level) | `ProductionBatch.QualityStatus` | Implemented, no live inventory link | **Reuse**, extend the gate (Section 11) | — | Existing gap | Gate placement |
| Quality (test results) | — | **None exists** | **New**, if needed | `InspectionRecord` is the wrong bounded context (Equipment-scoped) | Building it inside the wrong entity would require a breaking schema change later | Is `QualityTestResult` actually required, and its FK shape |
| Workflow (`IWorkflowRuntime`) | Existing, generic | Used by 6 modules, not by Production's core lifecycle | **Reuse only if a genuine multi-role approval need is proven** | Matches Phase 1's own deliberate choice | Over-engineering if adopted without need | Does Packaging release need multi-role approval |
| Audit (`AuditLog`) | Existing, automatic | Covers every `BaseEntity` `DbSet` | **Reuse, zero new code** | — | None | — |
| Permissions/SoD (`"Production"` module) | Existing | Independent, SoD-paired | **Reuse**, extend action set if needed | Avoid a second RBAC model | Low | Does Packaging share the `"Production"` module or need its own |
| `ReportingPeriod` | Existing (Phase 2A) | Day/Month solid, Week gated | **Reuse** | — | None (by design) | Week-start-day business decision (still open) |
| Barcode/QR (`BarcodeIdentity`) | Existing, generic | Reprint/audit lifecycle already solved | **Extend** (new `TargetType`s only) | Zero schema change | None | — |
| Daily Operations | — | **None exists** | **New**, per Section 10's recommended shape | — | Double-counting if built wrong | Shape (submission-only vs. hybrid) |
| Packaging (Order/Run/Input/Output/Loss/Event) | — | **None exists** | **New**, mirroring the Production pattern | — | Medium — genuinely new entity family | All of Section 18 |
| `Package` | — | **None exists** | **New** | — | Medium | Split/merge, quality-status model |
| `HandlingUnit` | — | **None exists** | **New** (recommend absorbing "Pallet" as a type, not a separate entity) | — | Low if scoped as recommended | Confirm Pallet-as-type is acceptable |
| `Pallet` | — | **None exists** | **Do not create as a separate entity** — see `HandlingUnit` | Avoid entity proliferation for the same aggregation concept | — | — |

---

## 14. Business Decision Register

| ID | Decision | Why It Matters | Current State | Options | Recommended Next Action | Blocking? |
|---|---|---|---|---|---|---|
| D1 | Gross/Net/Tare definition | Every Packaging reconciliation KPI depends on it | Undefined (Section 4.1) | 3 options in 4.1 | Business owner defines; then extend `ProductionOutput`/future `PackagingOutput` additively | **Blocks Packaging** |
| D2 | Tare ownership (container type vs. instance) | Determines whether Net is computable or must be measured | Undefined | Container-type default vs. per-instance override | Business decision | **Blocks Packaging** |
| D3 | Quantity basis / multi-UOM support | Determines whether one product can be tracked in 2 bases simultaneously | `ConversionFactor` unused | Single-basis (status quo) vs. multi-basis | Business decision; implement conversion only if confirmed needed | Blocks accurate reporting, not Packaging's existence |
| D4 | Product master extension shape | Avoids inventing a duplicate master | `MaterialItem` sufficient with extension | Profile entity fields | Design `ProductionProductProfile` once D1-D3 area is otherwise settled | Not blocking, can proceed in parallel |
| D5 | Packaging process boundary | Determines the whole Phase 3 entity shape | Recommended (Section 5.1) | Production-subtype vs. own bounded process | Adopt the recommendation unless a concrete objection is raised | **Blocks Packaging** (needs sign-off) |
| D6 | Package vs. HandlingUnit vs. Pallet | Avoids entity proliferation | Recommended (Section 5.2: Pallet = HandlingUnit type) | As stated vs. separate Pallet entity | Adopt recommendation or override with reason | **Blocks Packaging** |
| D7 | Batch split/merge rules | Determines whether sub-batch quality tracking is possible | Not supported today | Build split/merge vs. defer | Defer unless a concrete business case exists | Blocks only if sub-batch quality tracking is required |
| D8 | Package-to-batch genealogy shape (blending) | Determines `PackagingInput` cardinality | Recommended: many rows per run | As stated | Adopt | **Blocks Packaging** |
| D9 | Rework representation | Prevents a permanent, silent architecture gap from persisting into Packaging too | Not implemented (Section 7) | Option A+B recommended | Defer implementation, adopt the design direction | Not blocking Packaging directly |
| D10 | Quality gate at inventory posting | **Closes an existing, live gap** in Phase 1 code | Missing (Section 11, 19) | Gate inside `PostOutputToInventoryAsync` vs. caller-side | **Recommend fixing regardless of Packaging** | **Should be treated as urgent, independent of Packaging** |
| D11 | Batch/package release permission scope | Determines whether Packaging reuses `"Production"` module | Undecided | Reuse vs. new module | Reuse recommended unless a real separation-of-duties need is shown | Blocks Packaging RBAC design |
| D12 | `QualityTestResult` necessity and shape | Determines whether Packaging needs a new quality entity | Does not exist | Build vs. defer | Defer until a concrete test-recording need is confirmed | Blocks only if quality tests must be recorded, not blocking basic Packaging |
| D13 | Operational day start time | Determines Daily Operations/period-report correctness | Undefined | Business-specific | Must be answered by the business owner | **Blocks reliable daily reporting**, not Packaging's existence |
| D14 | Week start day | Blocks `PeriodType.Week` from ever being usable | Undefined, deliberately throws | Saturday (regional default) vs. other | Business decision | Blocks Week reporting only |
| D15 | Multi-plant calendar support | Determines if a single global calendar assumption is safe | Assumed single-plant today | Confirm scope | Confirm with business | Not blocking for a single-plant deployment |
| D16 | Daily Operations shape | Prevents double-counting | Recommended (Section 10) | 4 options evaluated | Adopt hybrid (submission-only) recommendation | **Blocks Daily Operations**, not Packaging |
| D17 | Barcode identity for Package/HandlingUnit | Low-risk, already-solved pattern | Not yet registered | Extend `SupportedTargetTypes` | Trivial once Package/HandlingUnit entities exist | Not blocking (follows automatically) |
| D18 | Package stock granularity | Determines reporting precision | No Package yet | Derived-only (like batch) vs. dedicated projection | Derived-only, matching Phase 2A's own precedent | Not blocking |
| D19 | Offline/mobile requirement | Determines Phase 6 scope | Confirmed absent, deliberate | In/out of scope | Business decision, later phase | Not blocking Packaging |

---

## 15. Blocking Risks

1. **The existing Phase 1 inventory-posting quality gap (D10/Section 11, 19)** is the single
   most concrete, actionable risk this audit surfaces — it is not hypothetical and not
   Packaging-dependent; it affects Production Core as shipped today.
2. **Gross/Net/Tare (D1/D2)** blocks Packaging most directly, since packaging inherently deals
   in physical units (drums, bags, pallets) where gross/tare/net is the industry-standard
   framing, not an edge case.
3. **Package-to-batch genealogy cardinality (D8)** must be decided before `PackagingInput`'s
   shape is finalized — a wrong initial cardinality (one batch per package instead of many)
   would require a breaking change later if blending is ever needed.
4. **Package quality-status model (part of D6/D11)** determines whether partial release is
   achievable at all — building `Package` without an independent status field would make
   Section 8's "can individual packages be blocked while the batch is partially available"
   requirement unimplementable without a later redesign.

## 16. Recommended Implementation Roadmap

Per the task's own phase framing, with dependencies made explicit:

- **Phase 3 — Packaging Foundation**: blocked on D1, D2, D5, D6, D8 being resolved; D10 (the
  inventory quality gate) should be fixed **independently and before** Phase 3, since Packaging
  will inherit the same gap otherwise.
- **Phase 4 — Quality and Release Integration**: blocked on D11, D12; depends on Phase 3's
  `Package` entity existing if package-level quality is in scope.
- **Phase 5 — Daily Operations and Approval**: blocked on D16; independent of Phases 3-4 and
  could proceed in parallel once D13 (operational day) is answered.
- **Phase 6 — Warehouse, Barcode, and Mobile Traceability**: barcode extension (D17) is
  low-risk and can follow Phase 3 immediately; mobile/offline (D19) is a separate, larger scope
  decision.
- **Phase 7 — Advanced Reporting and Analytics**: depends on D1/D2 (mass balance needs Gross/
  Net), D9 (yield needs rework resolved), and D13/D14 (period reports need calendar decisions).

This roadmap does not imply any of these phases are approved — it only orders them by their
actual dependency chain, as evidenced above.

## 17. Explicitly Deferred Scope

Everything already deferred by the three prior audits and Phase 2A remains deferred: Sales/
Dispatch/Customer, EPCIS, GS1 Digital Link, full GTIN/SSCC compliance, offline/mobile sync,
`ProductionAdjustment` (correction entity), `MaterialItemAlias`, dedicated Production Operator/
QC Inspector roles. Nothing in this audit expands that list except by naming its dependencies
more precisely (Section 16).

---

## 18. Verified vs. Inferred — Summary

Every finding in Sections 4, 6, 8, 11, and 12 marked "re-verified" or "new finding" above was
confirmed by direct file inspection during this audit session (cited by file/method name), not
carried forward from memory of the prior reports. Sections 5, 7, 9, 10, 13-16 are primarily
design analysis and recommendation — clearly framed as such throughout, not presented as
existing capability. No entity, service, table, workflow, or permission was fabricated; every
"does not exist" claim was backed by a repository-wide search executed in this session.

---

## 19. Final Readiness Classification

# B. ARCHITECTURE READY WITH REQUIRED BUSINESS DECISIONS

The existing Production Core (Phase 1) and its hardening (Phase 2A) require **no rework** —
every pattern this audit examined (state machines, inventory posting, batch genealogy hooks,
barcode identity, audit, permissions/SoD) remains sound and directly extensible for Packaging.
What blocks proceeding is not architectural weakness but a specific, enumerable set of business
decisions (Section 14, D1/D2/D5/D6/D8/D11 most directly) that Packaging's entity shapes
genuinely cannot be finalized without — deciding them wrong now would cost far more than
deciding them explicitly before writing the `PackagingOrder`/`Package`/`HandlingUnit` schema.
This is not classification C: nothing found here suggests the *existing* foundation is unsafe
or needs major redesign — the one concrete safety gap found (D10, the inventory quality-gate
check) is a small, well-understood, independently fixable issue in already-shipped code, not a
sign of foundational unsoundness.

---

## 20. Confirmation of Compliance with Task Rules

- Only this single report file was added to the repository.
- No source code, entity, DTO, interface, database configuration, or migration was modified.
- No database schema or data was changed.
- No tests were added or modified.
- No existing functionality was removed.
- No commit or push was performed as part of this task.
