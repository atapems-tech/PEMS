# PEMS Operations Phase 2D — Packaging Decision & Specification Audit Report

## 1. Executive Summary

Phase 2D was a documentation-only decision-and-specification gate for Packaging, following
Phase 2B's design audit and Phase 2C's production safety correction + decision baseline. This
phase re-verified the current Production/Inventory/Barcode/Quality/RBAC/Reporting
implementation directly against the live repository, resolved every technical (non-business)
question that could be resolved from evidence, and produced a complete, implementation-ready
conceptual specification plus a business-facing decision register. **No code, entity,
migration, controller, view, permission, or test was created or modified in this phase.**

## 2. Phase Objective (as given)

Produce three artifacts — a 47-section technical specification, a business decision register,
and this audit report — that together resolve or explicitly flag every open Packaging question
carried forward from Phase 2B/2C, without implementing any of it.

## 3. Scope

In scope: Packaging domain modeling (conceptual), Package/Gross-Net-Tare/genealogy/split-merge/
HandlingUnit/quality/permissions/inventory-boundary/barcode/lifecycle/mass-balance/reporting
specification; a business decision register; this audit report. Out of scope (explicitly
forbidden): any Packaging code, entity, migration, controller, view, API, permission, workflow,
seed data, Daily Operations, or Rework implementation.

## 4. Method

1. Re-audited the live repository (not solely prior reports) for: Production entities/services,
   Inventory entities/services, `BarcodeIdentity`/`BarcodeService`, quality-related entities
   (`ProductionBatch.QualityStatus`, `InspectionRecord`), RBAC/SoD (`PermissionService`,
   `SoDPolicy`), and reporting infrastructure (`ReportingPeriod`, `OperationsReportService`).
2. Searched the full solution for any existing Packaging-related class/table
   (`Package`, `PackagingOrder`, `PackagingRun`, `HandlingUnit`) — zero matches, confirming no
   partial/stray implementation exists.
3. Applied the same Reuse > Extend > Create discipline used in every prior phase to the
   Packaging boundary question (Section 9 of the technical specification).
4. For each open question from Phase 2B, determined whether it is technically decidable from
   the codebase's existing conventions (and decided it, with justification) or is a genuine
   business judgment call (and routed it to the decision register, never guessed).
5. Verified `git status` before and after all writes to confirm zero implementation surface
   area was touched.

## 5. Current State Re-Verification

| Area | Re-verified finding | Consistent with prior phases? |
|---|---|---|
| Production entities/services | `ProductionOrder/Run/Batch/Input/Output/Loss/Event`, explicit dictionary state machines in `ProductionOrderService`/`ProductionRunService`, unchanged | Yes |
| Inventory | `InventoryTransaction` (ledger) → `MaterialStock` (projection), `ProductionBatchId` nullable FK present (Phase 2A), unchanged grain | Yes |
| Barcode | `BarcodeIdentity` generic `TargetType`/`TargetId`, `SupportedTargetTypes` = Equipment/MaterialItem/Warehouse/StorageLocation/ProductionBatch | Yes |
| Quality | `ProductionBatch.QualityStatus` 5-state machine; **Phase 2C's inventory-posting gate confirmed present and unchanged** in `ProductionRunService.PostOutputToInventoryAsync` | Yes, and the Phase 2C fix remains intact |
| RBAC/SoD | `"Production"` module, fixed 7-action vocabulary, `Create↔Approve` SoD pair confirmed | Yes |
| Reporting | `ReportingPeriod`, `OperationsReportService.GetSummaryAsync` unchanged | Yes |

No drift from Phase 2B/2C's findings was found. No new defect was discovered during this
re-audit (see Section 6).

## 6. New Defects Discovered This Phase

**None.** The re-audit specifically re-checked the Phase 2C fix's continued presence (it is
intact) and found no new correctness issue in Production, Inventory, Barcode, Quality, RBAC, or
Reporting code. This phase's own "STOP and report separately" rule was therefore not triggered.

## 7. Packaging Boundary Decision

Confirmed: **Option B — Packaging as an independent bounded process reusing the Production
Order/Run/Input/Output/Loss/Event *pattern* (not the same tables)**. Full evidence-based
comparison in Technical Specification Section 9. This confirms, rather than overturns, Phase
2B's directional recommendation, and additionally verifies every mechanism the option depends
on already exists and works in the live codebase today.

## 8. Domain Model Summary

`Package`, `PackagingOrder` (tier optional, see D8), `PackagingRun`, `PackagingInput`,
`PackagingOutput`, `PackagingLoss`, `PackagingEvent`, `HandlingUnit` (absorbing Pallet/Carton/
etc. as a `Type`, not separate entities), and a new `PackageGenealogy` join table for split/
merge lineage. Full definitions in Technical Specification Sections 10-18.

## 9. Gross/Net/Tare Resolution

Two technically valid models identified (calculate-on-read vs. frozen-Net-snapshot); the choice
between them is a genuine business/compliance question (whether any Net figure must remain
fixed once printed/certified), not a technical one — routed to Decision Register D4. Decimal
precision (`decimal(18,3)`) and append-only correction-on-reweigh are **decided outright** in
this phase, since both follow directly and unambiguously from conventions already used
everywhere else in this codebase.

## 10. Genealogy & Traceability Model

Batch→Package (one-to-many) is straightforward; Package→Batch is conditionally many (blending,
D3) and handled via multiple `PackagingInput` rows rather than a single FK, avoiding a
premature cardinality assumption. Split/merge lineage requires a bidirectional join table
(`PackageGenealogy`), the one genuinely new mechanism this specification introduces beyond
directly-reused existing patterns — justified because a single FK cannot represent both
one-to-many (split) and many-to-one (merge) directions.

## 11. Split/Merge Model

Both modeled as: source package(s) transition to a terminal "Superseded" state (never deleted);
child/result package(s) are new rows with new barcode identities (not `Replace`, since the
logical thing itself changes) and new `PackageGenealogy` entries. Quality status is **not**
automatically inherited across a split/merge — re-confirmation is required by default,
reflecting that the physical handling event itself is a risk point — routed to Decision
Register D5 for final confirmation and for the exact re-release process (full re-inspection vs.
fast-track).

## 12. HandlingUnit / Pallet Model

Recommended: a single `HandlingUnit` entity with a `Type` lookup (absorbing Pallet, Carton,
Cage, etc.), not one entity per physical form factor — avoids the entity-proliferation pattern
Phase 2B already flagged as a risk. Nesting (units within units) is deferred to Decision
Register D9, not designed further absent confirmed need.

## 13. Quality Model for Packages

**Model D (inherit initial status from source batch at creation, then fully independent
thereafter)** is recommended — the only one of four evaluated models that is simultaneously
safe against retroactive batch-status changes after shipment, supports package-specific
inspection findings, and preserves audit accuracy. Full comparison table in Technical
Specification Section 25.

## 14. Permission & SoD Model

Recommended: reuse the existing `"Production"` module and its `Create↔Approve` SoD pair for
Package release authority, unless the business confirms a genuinely separate packaging QC role
is needed (Decision Register D7) — avoids unnecessary new RBAC surface area absent a stated
need, consistent with this codebase's existing module-reuse precedent.

## 15. Inventory Integration Model

Recommended flow mirrors the already-proven opt-in posting pattern
(`PostOutputToInventoryAsync`) exactly: `PackagingOutput` optionally posts a new
`InventoryTransaction` (`ReferenceType="PackagingOutput"`), never a second parallel ledger. The
double-counting risk between bulk and packaged stock is resolved by construction: if the source
batch was never itself posted to inventory (fully legitimate under the existing opt-in model),
no double-availability risk exists; if it was posted first, `PackagingInput`'s consumption must
issue a corresponding deduction, mirroring how `ProductionInput.SourceProductionBatchId`
already handles the analogous case for raw-material consumption today. This is a **resolved
technical design**, not a business decision.

## 16. Inventory Quality Gate for Packaging

Recommended: **duplicate** Phase 2C's exact check pattern (source-status must be "Released"
before posting) at a new, separate call site (`PackagingRunService.PostOutputToInventoryAsync`,
checking `Package.QualityStatus` rather than `ProductionBatch.QualityStatus`) — not a shared
policy object. This directly answers Phase 2C's open `D-ReceiveGuard` question: with only two
prospective call sites checking two different entity types, a shared abstraction would be
speculative generalization; extract one only if a third caller genuinely emerges.

## 17. Barcode Integration Model

Zero schema change required — `Package`/`HandlingUnit` become two new entries in
`BarcodeService.SupportedTargetTypes`, reusing `GenerateAsync`/`RecordPrintAsync`/`ReplaceAsync`
unchanged. Split/merge results get fresh barcode identities (not `Replace`), consistent with
Section 11's finding that split/merge produce new logical things, not relabeled old ones.

## 18. Lifecycle Model

Four distinct, deliberately un-collapsed lifecycles specified: process (`PackagingRun`),
physical (`Package`), quality (`Package.QualityStatus`), and inventory (unchanged, reused).
Every state included is justified by a specific business behavior found or reasoned during this
audit; states considered and rejected (e.g., a separate `Quarantined` state) are explicitly
recorded as rejected-for-now, not silently omitted.

## 19. Mass Balance & Reporting Model

Invariant specified: Input = Output (Net) + Loss ± tolerance (tolerance undefined, Decision
Register D11). Reports must never sum Production and Packaging output totals as if
representing the same physical event, and must never silently force an imbalance to zero —
both are carried-forward disciplines from Phase 1's own reconciliation reporting, applied
consistently to Packaging.

## 20. Offline/Mobile Constraints

Not designed in this phase; noted only that PEMS's PWA is confirmed (repeatedly, across every
prior audit) to be network-only by deliberate design, so any future barcode-scan-driven
packaging capture would require a sync-queue mechanism that does not exist anywhere in PEMS
today — flagged as a future concern, not a Phase 2D blocker.

## 21. Business Decisions Identified

Twelve items in the Decision Register; **six (D1-D6) are hard blockers** to any Phase 3 coding
start (Package definition, mixed-product support, blended-batch support, Gross/Net/Tare model,
split/merge quality & inventory rules, packaged-vs-bulk product identity). Two (D7-D8) have
safe, recommended defaults that allow Phase 3 to proceed if unopposed. Three (D9/D10/D12) are
genuinely deferrable with no cost. One (D11) does not block starting the build but must be
resolved before packaging variance reports are used for real operational decisions.

## 22. Hard vs. Soft Blockers — Technical Classification

**Hard blockers** (schema/domain shape depends on the answer): D1, D2, D3, D4, D5, D6 — see
Technical Specification Section 41.
**Soft blockers** (recommended default is usable without further input): D7, D8 — see Technical
Specification Section 42.
**Non-blocking**: D9, D10, D11 (reporting-only), D12.

## 23. Validation

- `git status --porcelain` re-run at the end of this phase confirms the only changes introduced
  by this phase are the three documentation files (this report plus the two files under
  `docs/specifications/`, the latter directory newly created by this phase for documentation
  only).
- Confirmed via repository-wide search (`class Package\b`, `PackagingOrder`, `PackagingRun`,
  `HandlingUnit`) that **zero** Packaging-related code, entity, or table exists anywhere in
  `PEMS.Web`, `PEMS.Domain`, `PEMS.Application`, or `PEMS.Persistence`.
- Confirmed no new EF Core migration was added (`PEMS.Persistence/Migrations/` unchanged by
  this phase).
- Confirmed no existing entity, service, controller, view, permission, or test's behavior was
  altered by this phase.
- Pre-existing, unrelated repository state — carried over from before this phase, not caused by
  it, and unchanged by it — remains present: the Phase 2C code fix and its two corrected test
  files (`ProductionRunService.cs`, `InventoryBatchTraceabilityTests.cs`,
  `ProductionRunServiceTests.cs`) and the new `ProductionInventoryQualityGateTests.cs` are still
  uncommitted (expected — Phase 2C explicitly forbade commit/push); the two Phase 2B/2C report
  docs remain untracked (same reason); a third-party VS Code extension's modifications to
  `.gitignore`/`CLAUDE.md` remain present and are still not mine; and **the previously-flagged
  17 deleted files under `PEMS.Web/filestore/` remain unexplained and unresolved** — this has
  now been surfaced multiple times across Phase 2B, the start of Phase 2D, and again here, with
  no user action taken yet. It is flagged again here for visibility and should not be silently
  dropped from any future status update.

## 24. Final Classification

**Classification B — Business decisions required before Phase 3.**

Justification: no foundational architecture gap was found (Classification C does not apply —
every mechanism Packaging needs already exists and works in the current codebase, confirmed by
direct re-audit); the specification is complete and internally consistent, but six genuine
business decisions (D1-D6) remain open and must be resolved by business/quality/commercial
ownership before Phase 3 implementation can safely begin, since each changes the basic shape of
the records to be built. This is not a readiness gap in the engineering work — it is exactly the
kind of decision that must come from business ownership, not be inferred by engineering.

---

## Required Final Response Summary

1. **Classification**: B — Business decisions required before Phase 3.
2. **Files created**: `docs/specifications/PEMS_OPERATIONS_PHASE_2D_PACKAGING_TECHNICAL_SPECIFICATION.md`, `docs/specifications/PEMS_OPERATIONS_PHASE_2D_BUSINESS_DECISION_REGISTER.md`, `docs/audits/PEMS_OPERATIONS_PHASE_2D_PACKAGING_DECISION_AND_SPECIFICATION_AUDIT_REPORT.md` (and the new, empty-of-code `docs/specifications/` directory itself).
3. **Files modified**: none.
4. **Confirmation no implementation was performed**: confirmed — no entity, migration, controller, view, API, permission, workflow, or test was created or changed in this phase.
5. **Hard blockers**: D1 (Package definition), D2 (mixed-product packages), D3 (blended-batch packages), D4 (Gross/Net/Tare model), D5 (split/merge quality & inventory rules), D6 (packaged-vs-bulk product identity).
6. **Soft blockers**: D7 (package release authority — safe default available), D8 (Packaging Order tier — safe default available).
7. **Business decisions required**: all twelve in the Decision Register; six are hard blockers (above), one (D11, loss/variance tolerance) affects reporting reliability but not the build start, three (D9/D10/D12) are freely deferrable.
8. **Recommended Phase 3 scope** (once D1-D6 are resolved): `PackagingRun`/`PackagingInput`/`PackagingOutput`/`PackagingLoss`/`PackagingEvent`, `Package` with its independent quality lifecycle, opt-in inventory posting with its own duplicated quality gate, and barcode `TargetType` registration — explicitly excluding `HandlingUnit`/Pallet, split/merge, repacking, and rework unless confirmed needed immediately.
9. **Validation result**: passed — see Section 23.
10. **`git status` result**: only the three documentation files (and the new `docs/specifications/` directory) are new as a result of this phase; all other working-tree changes pre-date this phase and are unrelated to it.
11. **Was any migration created?**: No.
12. **Did any live data change?**: No.

**Outstanding item requiring user attention (not part of Phase 2D's own scope, but repeatedly
surfaced and still unresolved)**: 17 files under `PEMS.Web/filestore/` show as locally deleted
in `git status`. This was not caused by any task in this session, has been flagged at the end of
Phase 2B and again during Phase 2D's baseline check and final validation, and remains
unaddressed. The files are still recoverable via `git checkout -- <path>` if the deletions were
unintended.
