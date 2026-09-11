# PEMS EDMS — Implementation Roadmap

Companion to `PEMS_EDMS_DEEP_REPOSITORY_AUDIT.md` and `PEMS_EDMS_GAP_MATRIX.md`. Phases are sequenced by
dependency and risk, matching the governing directive's own phasing philosophy: fix and harden what exists
before adding new surface area, and never touch AI-assisted features before the underlying data model is
reliable.

**Important:** per the governing directive's own instruction ("Do not begin broad implementation until the
audit and roadmap are completed"), **no code in this roadmap has been implemented yet.** This document is
the Phase 0 planning deliverable. Each phase below is scoped and ready to execute on explicit direction, one
phase at a time, with full build/migration/test verification before moving to the next.

---

## Phase 1 — Revision-File Binding & Integrity (addresses G-06, G-07)

**Business objective:** Eliminate ambiguity about which uploaded file is official for which revision, and
give every new upload a verifiable integrity hash — the one finding in this audit that directly contradicts
the Definition-of-Done requirement "Files are correctly bound to revisions."

- **Entities affected:** `DocumentAttachment` (additive: nullable `DocumentRevisionId` FK, `IsOfficial` bool,
  `FileRole` string, nullable `FileHash` string). No existing column removed or renamed.
- **Services affected:** the document upload path (`DocumentUploadController`/`EngineeringDocumentsController`
  upload actions) — extended to accept an optional revision context; `HashingStream` (already built this
  session for Enterprise File Storage Management) reused, not duplicated, to compute the hash while
  streaming to disk.
- **Controllers/API affected:** upload actions gain an optional `revisionId` parameter (backward-compatible
  — omitting it behaves exactly as today, attachment stays document-level only).
- **UI affected:** Document Details "Files"/"Attachments" tab gains a revision-filter and an "Official file"
  indicator per revision.
- **Database changes:** one additive EF Core migration — new nullable columns only, `DeleteBehavior.Restrict`
  on the new FK to avoid cascade-cycle risk (established convention this session).
- **Authorization changes:** none required — reuses existing upload/download permission checks.
- **Audit changes:** none required — the generic `AuditLog` change-tracker automatically covers the new
  columns.
- **Tests:** binding correctness (attachment correctly scoped to one revision, not visible as official for
  another), hash computed correctly for a real byte stream without double-reading, backward-compatibility
  test (existing document-level-only attachments remain valid and queryable with `DocumentRevisionId =
  null`).
- **Migration strategy:** disposable repro database validation (established pattern this session) before
  applying to live PEMSDB; migration is purely additive, zero data touched.
- **Acceptance criteria:** matches G-06/G-07 acceptance criteria in the Gap Matrix.
- **Rollback considerations:** the migration's `Down()` simply drops the new nullable columns — no data loss
  on rollback since nothing pre-existing was altered.

---

## Phase 2 — Review/Approval Separation & Comment Traceability (addresses G-09, G-10)

**Business objective:** Make internal engineering review and client review fully independent, auditable
tracks, and strengthen who-did-what traceability on comments.

- **Entities affected:** `DocumentComment` (additive `RaisedById`/`ClosedById` nullable Employee FKs,
  alongside — not replacing — the existing free-text `RaisedByName`/`ClosedByName`, for backward
  compatibility with any existing rows/imports that only ever had a name).
- **Services affected:** `DocumentWorkflowService.RecordClientResponseAsync` — stop writing the client
  decision into the shared `Document.Status`; drive it exclusively through `ClientApprovalStatus` (already
  a dedicated field, just not exclusively used today). Requires checking every other read-path that
  currently expects `Document.Status` to reflect a client decision (e.g. `"Client Approved"`/`"Rejected"`
  after client response) and repointing those reads to `ClientApprovalStatus` instead — this is the one
  genuinely non-additive change in this roadmap and needs careful regression testing before it ships.
- **Controllers/API affected:** any controller/view reading `document.Status == "Client Approved"` (to be
  located via a full-repo grep before implementation, not assumed from this document alone).
- **Database changes:** one additive migration (two nullable FK columns).
- **Authorization changes:** none required.
- **Audit changes:** none required (generic AuditLog covers new columns).
- **Tests:** regression tests proving `Document.Status` no longer changes on client response while
  `ClientApprovalStatus` does; every existing test that currently asserts `Status == "Client Approved"`
  after a client response must be found and updated in the same change, not left silently broken.
- **Migration strategy:** repro-validated additive migration, then a careful data/behavior compatibility
  pass over existing call sites before flipping the write behavior.
- **Acceptance criteria:** Client review and internal review are readable as two independent fields with no
  shared mutation; all pre-existing tests that reference client-response status are updated and passing.
- **Rollback considerations:** the FK columns roll back cleanly; the `Status`-write-path change should be
  guarded behind careful review since it changes existing behavior (not purely additive) — recommend
  shipping this as its own sub-step with full regression before merging.

---

## Phase 3 — MDR Completeness Investigation & Reporting (addresses G-11, G-17, G-18, G-19)

**Business objective:** Confirm what MDR completeness reporting exists today (unverified per this audit),
then close any genuine gap with a requirements-driven completeness view, not a rebuild of the existing
import-and-pairing pipeline.

- **Scope:** Begins with a focused re-audit (read the MDR-facing controller(s)/view(s) fully) to convert
  G-11/G-17/G-18/G-19 from **UNVERIFIED** to a concrete finding before any code is written — consistent with
  this audit's discipline of never proposing a fix for an unconfirmed gap.
- **Entities affected:** likely none new if `MDRImportRow`↔`EngineeringDocument` pairing (already
  confirmed to exist via `pendingRowDocPairs`) is sufficient as a foundation — a completeness *view/report*
  can likely be built as a read-only query over existing data rather than a new requirements entity.
- **Services affected:** a new read-only reporting method on the MDR service, computing
  required-but-missing / overdue / missing-approval / missing-file per the already-linked rows.
  Discipline-level and vendor-performance dashboards (G-17) would extend the same read-only reporting
  layer.
- **Controllers/API affected:** MDR-facing controller gains a completeness/dashboard endpoint; navigation
  gains first-class entries for it if confirmed missing (G-19).
- **Database changes:** none anticipated unless the re-audit finds the pairing data insufficient.
- **Tests:** completeness-calculation correctness tests (given a known set of required rows and actual
  document states, the report classifies each row correctly).
- **Acceptance criteria:** matches G-11's Gap Matrix entry once converted from investigation to a concrete
  finding.

---

## Phase 4 — Vendor Handover, Engineering Object Traceability, Retention (addresses G-05, G-13, G-14)

**Business objective:** Extend the already-real `ManufacturerDataBook` model into a general, project-level
Handover Package (without duplicating MDB tracking), confirm/complete reverse traceability for
Contract/Vendor, and introduce a records retention/legal-hold governance layer before any future archival
automation is built.

- **Entities affected:**
  - `HandoverPackage` (new, additive) — references `ManufacturerDataBook` records and other document types
    as deliverable line items, rather than reimplementing Part A/B/C tracking.
  - `DocumentRetentionPolicy`, `DocumentLegalHold` (new, additive) — a document under legal hold must block
    any future disposal/archival action; no automatic deletion is introduced by this phase (governance
    layer only, consistent with the directive's explicit prohibition on unauthorized automatic deletion).
  - Verify (and add only if genuinely missing) reverse navigation collections on `Contract`/`Vendor` back to
    their related `EngineeringDocument`/`VDRItem` rows (G-05).
- **Services affected:** new `HandoverPackageService`, new `DocumentRetentionService` (or equivalent),
  reusing existing document-query patterns rather than a parallel data-access layer.
- **Controllers/API + UI affected:** new Handover workspace (reusing the existing design system, per the
  directive's UI rules — no visually disconnected pages); a legal-hold indicator surfaced anywhere a
  held document could otherwise be archived/deleted.
- **Database changes:** additive migrations only, `DeleteBehavior.Restrict` on new FKs.
- **Authorization changes:** new permissions for `ManageHandover`/`ManageRetention` (per the directive's
  §19 permission list), following the existing `IPermissionService`/ABAC pattern (not a new auth mechanism).
- **Audit changes:** `HoldPlaced`/`HoldReleased`-equivalent events for legal hold, mirroring the already-
  proven `DocumentHold` append-only pattern (reuse the pattern, not the entity — legal hold is a distinct
  business concept from the existing operational `DocumentHold`).
- **Tests:** handover completeness evaluation, legal-hold blocking disposal, retention policy application.
- **Migration strategy:** standard repro-validate-then-apply, fully additive.
- **Acceptance criteria:** matches G-13/G-14's Gap Matrix entries; a document under legal hold cannot be
  disposed of by any ordinary process in the codebase (verified by test, not just by convention).

---

## Phase 5 — AI Provenance Verification & Extension (addresses G-15, gated behind Phases 1-4)

**Business objective:** Confirm (per G-15's open question) that no code path lets AI output silently change
`Document.Status`/`IsFrozen` post-routing, then only extend AI capability (revision comparison, MDR
completeness suggestions, relationship discovery) once that structural guarantee is confirmed — per the
directive's own explicit instruction: "Do not implement advanced AI features before the underlying data
model is reliable," and this phase deliberately comes last, after Phases 1-4 have made the underlying model
more reliable (revision-file binding, retention governance, clean status separation).

- **Scope:** Begins with a targeted grep/read pass confirming G-15, converting it from a verified-strength-
  with-one-open-question into a fully closed finding.
- **Entities affected:** none required for the verification step; any new AI feature (e.g. AI-assisted
  revision comparison building on the already-existing `DocumentRevision.AIRevisionSummary` field, which is
  presently unpopulated by any service found this session) is additive.
- **Tests:** a structural test asserting that no AI-facing service can set `Document.Status`/`IsFrozen`
  without a human-invoked workflow action in between.
- **Acceptance criteria:** AI outputs remain clearly labeled, reviewable, and non-authoritative, consistent
  with the directive's §24 requirement — verified by test, not asserted by convention.

---

## Sequencing rationale

Phase 1 is prioritized first because it is the only finding in this audit that constitutes an outright
contradiction of a stated Definition-of-Done item (revision-file binding), and because it is purely additive
with no risk to existing behavior. Phase 2 is second because it touches one existing (non-additive) write
path and needs careful regression coverage — better done early while the change is small, rather than
layered under Phase 3/4 features that would depend on it. Phases 3-4 build genuinely new capability on a
now-hardened foundation. Phase 5 (AI) is deliberately last, matching the directive's own explicit sequencing
instruction.

**No phase begins without explicit direction to proceed** — this roadmap is a plan, not a commitment to
implement.
