# PEMS EDMS — Deep Repository Audit

**Repository:** `PEMS-cmms-edit-empoleeys-master` (local clone), branch `main`
**Audited:** 2026-09-11
**Method:** Direct inspection of the live repository (`Read`/`Grep`/`Glob` against real files, no assumptions from entity names alone). Every finding below is file-path-verified this session unless explicitly marked "carried from prior audit, not re-verified."
**Scope of this pass:** Phase 0 only, per the governing directive ("Do not begin broad implementation until the audit and roadmap are completed"). No production code was changed in this session.

## 0. Critical context: this is not a greenfield system

Two prior internal audits already exist in this repository and were read in full before writing any
finding below:

- `.claude/Role/PEMS_ENGINEERING_EDMS_THREE_PASS_AUDIT_REPORT.md` — a genuine three-pass audit (Business /
  EDMS / Software) performed before 2026-08-18, which found **two P0 correctness bugs** (document
  numbering race condition; Freeze not enforced) plus a near-zero test-coverage state and no pagination.
- `.claude/Role/PEMS_ENGINEERING_GM_FINAL_AUDIT.md` (2026-08-18/19) — implemented and tested Transmittal
  Governance and Hold/Clouded lifecycle, referencing Freeze enforcement as an *already-existing* precedent
  by that point.

**This session independently re-verified the P0/P1 items from the older report against the current
codebase.** Result: both P0 bugs are fixed, pagination is fixed, and test coverage has grown from ~1 file
to 116 test methods across 20 files in `PEMS.Tests/Engineering/` alone (plus more in `PEMS.Tests/Cmms/`,
`PEMS.Tests/Authorization/`, etc.). The codebase has continued evolving substantially since that report was
written — its findings must not be treated as current without re-verification, which is exactly what this
section does. Findings below are tagged **[RE-VERIFIED 2026-09-11]** where this session independently
confirmed current state, or **[CARRIED, NOT RE-VERIFIED]** where a finding is repeated from the prior
report without fresh evidence this session (time-boxed — flagged explicitly for follow-up rather than
silently assumed true).

---

## 1. Findings

### F-01 — Document numbering is atomic and race-safe [RE-VERIFIED — STRENGTH, supersedes prior P0]

- **Feature:** Engineering Document numbering
- **Business purpose:** Two engineers creating documents simultaneously must never receive the same
  document number.
- **Current implementation:** `PEMS.Persistence/Servises/DocumentNumberingService.cs:45-55` — delegates to
  `INumberingService.GetNextNumberAsync("EngineeringDocument", scopeKey)`
  (`PEMS.Persistence/Servises/NumberingService.cs:41`, registered in the `Defaults` dictionary), which uses
  an atomic SQL `UPDATE...OUTPUT` sequence allocator, not `COUNT(*)+1`. The code comment explicitly states:
  *"طبق رفع باگ P0 ممیزی: قبلاً توالی از COUNT(*) در حافظه محاسبه می‌شد... حالا از همان NumberingService
  اتمیک استفاده می‌شود"* ("per the P0 audit bug-fix: previously the sequence was computed from an in-memory
  COUNT(*)... now the same atomic NumberingService is used").
- **Existing tests:** `PEMS.Tests/Engineering/NumberingServiceTests.cs` (includes a real concurrent-call
  test).
- **Gap:** None — this was the prior audit's #1 P0 finding and is now closed.
- **Risk:** None remaining.
- **Priority:** N/A (already fixed).

### F-02 — Document Freeze is now server-enforced [RE-VERIFIED — STRENGTH, supersedes prior P0]

- **Feature:** Freeze / Unfreeze of a controlled Engineering Document.
- **Current implementation:** `PEMS.Persistence/Repositories/EngineeringDocumentRepository.cs:552-553` —
  `UpdateAsync` throws `DocumentFrozenException` if `existing.IsFrozen` before any field is written. Dedicated
  `FreezeAsync`/`UnfreezeAsync` methods (`:609-640`) capture `FrozenDate`/`FrozenById`/`FreezeReason` and
  `UnfrozenDate`/`UnfrozenById`/`UnfreezeReason` — full audit fields, history preserved (unfreeze does not
  erase the freeze record).
- **Existing tests:** `PEMS.Tests/Engineering/EngineeringDocumentFreezeTests.cs`.
- **Gap:** None — this was the prior audit's #2 P0 finding and is now closed.
- **Priority:** N/A (already fixed).

### F-03 — Pagination is implemented on the document list/register path [RE-VERIFIED — STRENGTH, supersedes prior P1]

- **Current implementation:** `EngineeringDocumentRepository.cs:78` (`GetPagedAsync`) and `:413`
  (`GetRegisterPagedAsync`), both using `.Skip((page-1)*pageSize).Take(pageSize)` and returning the
  project's own `PagedResult<T>` — not a full in-memory load. `DocumentRegisterController`'s query
  (`:328-334`) was also deliberately rewritten to project only the columns the view renders, instead of
  `.Include()`-ing Attachments/Revisions/Workflows on every row.
- **Gap:** None — this was the prior audit's #4 P1 finding and is now closed.
- **Priority:** N/A (already fixed).

### F-04 — Test coverage on the Engineering module is substantial, not near-zero [RE-VERIFIED — STRENGTH, supersedes prior P0]

- **Evidence:** `PEMS.Tests/Engineering/` contains 20 files and 116 `[Fact]`/`[Theory]` test methods
  (counted this session via `grep -c "\[Fact\]\|\[Theory\]"` across the directory), covering numbering,
  revision quality, hold/workflow interaction, transmittal governance and numbering concurrency, client
  review, first-pass-approval metrics, reverse traceability, working-day SLA calculation, VDR paging, and
  document-register queries. Full-suite regression for the whole repository stood at 1082/1082 passing as
  of this session's most recent run (a different module, but demonstrates the project's overall test
  discipline is real, not aspirational).
- **Gap:** The prior audit's "essentially zero test coverage" finding is obsolete. Coverage gaps DO remain
  for the genuinely new capabilities this document identifies as missing (see F-06, F-07 below) — those
  cannot have tests because the features don't exist yet.
- **Priority:** N/A for the general claim; specific new-feature test gaps are captured under their own
  findings.

### F-05 — Reverse object traceability exists for Equipment, unconfirmed for Contract/Vendor/MR [PARTIAL, RE-VERIFIED for Equipment only]

- **Current implementation:** `PEMS.Domain/Entities/Equipment.cs:103` —
  `public ICollection<EngineeringDocument> EngineeringDocuments { get; set; }` — a genuine reverse
  navigation now exists (this closes the prior audit's #5 P1 finding for Equipment specifically), consistent
  with the existing `PEMS.Tests/Engineering/ReverseTraceabilityTests.cs` file.
- **Gap:** `Contract.cs` and `Vendor.cs` reverse-navigation collections back to `EngineeringDocument`/`VDRItem`
  were not re-checked this session — flagged for follow-up rather than assumed fixed or broken.
- **Compatibility impact:** None either way (additive).
- **Priority:** P3 (verification task, not necessarily an implementation task).

### F-06 — Files are bound to the Document, never to a specific Revision [CONFIRMED GAP — matches directive §9 exactly]

- **Feature / Capability:** Revision-file binding.
- **Business purpose:** A file uploaded against Revision B of a document must never be presentable as the
  official file for Revision A.
- **Current implementation:** `PEMS.Domain/Entities/DocumentAttachment.cs` — has `DocumentId` (nullable FK
  to `Document`) but **no `RevisionId`/`DocumentRevisionId` field anywhere** (confirmed via
  `grep -n "RevisionId" PEMS.Domain/Entities/DocumentAttachment.cs` → no matches). `DocumentRevision.cs`
  likewise has no collection of attachments. There is no `IsPrimary`/`IsOfficial`/`FileRole` concept.
- **Current runtime flow:** A document with 5 revisions and 5 uploaded attachment rows has no way, from the
  data model alone, to say which attachment is "the file for revision 03" versus "the file for revision
  05" — the UI/service layer would have to infer this from `UploadDate` ordering relative to
  `DocumentRevision.RevisionDate`, which is fragile (an attachment uploaded late for an old revision, e.g.
  a corrected scan, would misattribute).
- **Existing authorization:** Download authorization (`FilesController.Download`) is per-`AttachmentId`,
  independent of this gap.
- **Existing tests:** None found for revision-attachment binding (searched `PEMS.Tests/Engineering/` —
  no file/test name references attachment-to-revision binding).
- **Applicable engineering requirement:** ISO 19650 revision/issue control; ISO 9001 "obsolete document
  prevention" (a superseded revision's file must not be ambiguous with the current one).
- **Actual gap:** Real and current — not fixed by any of the prior audits' remediation work (which focused
  on Freeze/Hold/Transmittal, not attachments).
- **Risk:** Medium-high. In a real EPC document-control operation, ambiguity about "which file is the
  official Rev-C file" is a genuine compliance and rework risk.
- **Compatibility impact:** Additive fix is safe — add a nullable `DocumentRevisionId` FK to
  `DocumentAttachment` (nullable preserves all existing rows, which stay un-linked-to-a-revision exactly as
  today), plus `IsOfficial`/`FileRole` flags. No existing column needs to change or be removed.
- **Recommended fix:** See Roadmap Phase 1.
- **Priority:** P1.
- **Acceptance criteria:** A new `DocumentRevisionId` (nullable) FK exists on `DocumentAttachment`; the
  upload flow for a document that already has an active revision requires (or defaults to) associating the
  new file with a specific revision; the Document Details "Files" tab can show, per revision, which
  attachment(s) are official for it; existing (pre-migration) attachment rows remain valid with
  `DocumentRevisionId = null` and are not silently reassigned.

### F-07 — No file hash / integrity check anywhere in the document upload path [CONFIRMED GAP — matches directive §10]

- **Current implementation:** `DocumentAttachment.cs` has `FileName`, `FileExtension`, `FilePath`,
  `FileSize`, `MimeType` — **no `FileHash`/`Sha256` field.** Grepped the whole
  `PEMS.Domain/Entities/DocumentAttachment.cs` for `FileHash|Sha256|ComputeHash|SHA256` — zero matches.
  (Note: this session's own earlier Enterprise File Storage Management work added a `HashingStream`
  streaming-SHA256 helper in `PEMS.Persistence/Servises/HashingStream.cs`, but it is wired only into the
  new, separate `IEnterpriseFileStorageService` path used by Storage Providers — not into the
  Engineering Document upload path, which still goes through the older `DocumentUploadController`/
  `EngineeringDocumentsController` upload actions independent of that new service.)
- **Gap:** No duplicate-file detection, no tamper-evidence, no integrity verification on download for
  engineering document attachments.
- **Risk:** Medium. Without a hash, there is no automated way to prove a downloaded file matches what was
  originally uploaded/issued, and no cheap way to detect duplicate uploads.
- **Compatibility impact:** Additive — add nullable `FileHash` column to `DocumentAttachment`; compute it
  for new uploads only; do not attempt to backfill existing files (would require re-reading every file on
  disk, out of scope and risky).
- **Priority:** P2.
- **Acceptance criteria:** New attachment uploads compute and persist a SHA-256 hash without buffering the
  full file in memory (the existing `HashingStream` pattern can be reused directly — this is "extend, don't
  duplicate" since the class already exists); existing attachment rows are untouched.

### F-08 — File download is genuinely secure (not a gap — recorded as a verified strength) [RE-VERIFIED — STRENGTH]

- **Current implementation:** `PEMS.Web/Controllers/FilesController.cs:44-61` — the only input is an
  internal numeric `AttachmentId` (never a user-supplied filename or path, which structurally prevents path
  traversal); a denied request returns `Forbid()` (403), not `NotFound()` (404) — deliberately avoiding
  leaking whether a given attachment ID exists to an unauthorized caller (explicit code comment confirms
  this was a conscious audit-driven decision). Authorization is resolved per actual parent entity
  (`CheckAccessAsync`), not a single blanket permission.
- **Gap:** None found this session.
- **Priority:** N/A.

### F-09 — Comment Register is rich and mostly complete, but "raised/closed by" is free text, not an identity link [PARTIAL]

- **Current implementation:** `DocumentComment.cs` already has `CommentNo`, `Category`, `Severity`,
  `RequiredAction`, `DueDate`, `ActionStatus` (OpenComment/ResponsePending/Verified/Closed/
  NoActionRequired), `AttachmentId`/`ReplyAttachmentId` (users can attach the actual comment/reply sheet
  file, not just type text), `ParentCommentId` (links a re-opened comment to its origin in a prior
  revision), `PageNumber`/`PinX`/`PinY` (PDF pin-comment coordinates), and — critically for §12's
  carry-forward requirement — `MarksRevisionRequired` (bool) + `TriggeredRevisionId` (FK to
  `DocumentRevision`), which the prior G/M audit confirmed is wired into
  `EngineeringDocumentsController.CreateRevision` via an optional `triggeringCommentId` parameter. This is
  a materially complete implementation of §12's carry-forward requirement, already built.
- **Gap:** `RaisedByName`/`ClosedByName` are free-text `string` fields, not `EmployeeId` foreign keys —
  weakens traceability of exactly who owns/closed a comment (an Employee record cannot be reliably joined
  back from a free-text name). This matches the prior audit's finding #12, independently re-confirmed this
  session by direct field inspection.
- **Priority:** P3 (traceability quality improvement, not a functional gap).

### F-10 — Client Review still shares the same `Status` column as internal engineering review [RE-VERIFIED — STILL OPEN]

- **Current implementation:** `EngineeringDocument.cs` does carry a genuinely separate
  `ClientApprovalStatus`/`ClientComment`/`ClientResponseDate` set of fields — but
  `DocumentWorkflowService.RecordClientResponseAsync` (`:349-360`) writes the client's decision into
  **both** `document.ClientApprovalStatus` **and** the shared `document.Status` (`"Client Approved"` or
  `"Rejected"`), and further mutates `ClearCheckApproveMetadata` when rejected. Verbatim: `document.Status =
  normalizedCode switch { "A" => "Client Approved", "B" or "C" or "D" => "Rejected", ... }`.
- **Gap:** Internal engineering status and client-response status are not two fully independent tracked
  workflows — a client rejection overwrites the same field an internal reviewer's decision would have set.
  This directly matches directive §7's requirement to separate Review Status / Approval Status / client
  acceptance as distinct concepts.
- **Risk:** Low-medium — functionally works today (the dedicated `ClientApprovalStatus` field does exist
  and is populated), but the shared `Status` mutation means "what does Status mean right now" requires
  knowing whether the last transition was internal or client-driven.
- **Priority:** P2.

### F-11 — MDR import already links import rows to real documents; a dedicated completeness dashboard is unconfirmed [PARTIAL, needs follow-up]

- **Current implementation:** `PEMS.Persistence/Servises/MDRImportService.cs:392` builds
  `pendingRowDocPairs` — a `List<(MDRImportRow Row, EngineeringDocument Doc)>` — confirming the MDR import
  pipeline does more than store a flat Excel-derived list; it actively pairs required rows against real
  `EngineeringDocument` records.
- **Gap:** Whether a dedicated MDR **completeness/gap report** (required-but-not-created, overdue,
  missing-approval, missing-file, etc., per directive §14) is surfaced anywhere in the UI was not confirmed
  this session — this needs a follow-up read of the MDR-facing controller/view before Phase 3 work begins.
  Not claiming this is either present or absent; explicitly flagged as unverified.
- **Priority:** Investigation task before Phase 3 (see Roadmap).

### F-12 — Vendor Document Control (VDR) is a genuinely mature, SLA-aware implementation [RE-VERIFIED — STRENGTH]

- **Current implementation:** `VDRItem : Document` + `VDRSubmission` (`PEMS.Domain/Entities/VDRSubmission.cs`)
  carry real SLA logic: `ContractorCommentDueDate`/`IsContractorCommentOverdue` (10 working days from
  vendor issue, computed via the shared `WorkingDayCalculator` — not duplicated),
  `VendorReturnDueDate`/`IsVendorReturnOverdue` (15 working days from comment receipt), and
  `RequiresResubmission` (`ReviewCategory is "CO" or "RE"`) — a real, previously-audited-and-fixed bug
  (`AC` used to be incorrectly bundled with `CO`/`RE`, inflating vendor risk scores; fixed per the entity's
  own code comment). Comment/reply sheets can carry actual attached files
  (`CommentSheetAttachmentId`/`ReplySheetAttachmentId`), not just reference numbers.
- **Gap:** None found this session for the submission/review/return lifecycle itself.
- **Priority:** N/A.

### F-13 — Manufacturer Data Book (MDB) is a real, partial Handover model — scoped to equipment vendor documentation only [PARTIAL]

- **Current implementation:** `PEMS.Domain/Entities/ManufacturerDataBook.cs` implements Part A/B/C vendor
  final-documentation tracking per equipment: `PlannedFinalIssueDate`, `IndexOfContentsDueDate`/
  `IsIndexOverdue` (computed, 28 days before final issue per the referenced procedure), hard-copy and
  soft-copy delivery dates, and a real status lifecycle (`Planned → IndexSubmitted → InProgress →
  MasterReview → FinalIssued → Delivered`).
- **Gap:** This is a genuinely real implementation of "Handover of Final Documentation" (per the entity's
  own doc comment, citing "VENDOR DOCUMENTATION MANAGEMENT PROCEDURE" §6) — but it is scoped strictly to
  per-equipment vendor data books. There is no project-level `HandoverPackage`/`AssetInformationRecord`
  covering certificates, commissioning records, inspection records, or final approved-design documents in
  general (i.e., a broader EPC-to-Owner turnover package, not just vendor MDBs).
- **Recommendation:** Per the directive's own "extend, don't duplicate" rule — a general Handover Package
  concept should be built as a sibling/superset that can **reference** existing `ManufacturerDataBook`
  records as one deliverable type among several, not a parallel reimplementation of MDB tracking.
- **Priority:** P2 (Phase 4).

### F-14 — Records Retention, Legal Hold, and Disposition do not exist for documents [CONFIRMED GAP]

- **Current implementation:** Searched `PEMS.Domain/Entities/` for `Handover|RetentionPolicy|LegalHold|
  AsBuilt|As-Built|Disposition` — real hits exist only in `ShiftHandover*.cs` (an unrelated CMMS
  operational-shift-handover concept, not document/asset handover), `Permit.cs`, `NonConformanceReport.cs`,
  and `ManufacturerDataBook.cs` (covered in F-13). **No `DocumentRetentionPolicy`, `DocumentLegalHold`, or
  `DocumentDisposition` entity exists anywhere in the repository.**
- **Gap:** Confirmed absent, not partially implemented.
- **Risk:** Low today (no regulatory retention/legal-hold obligation is currently being violated, because
  no automatic deletion exists either — there is simply no governance layer at all). Becomes higher-priority
  the moment any data-cleanup/archival process is ever introduced without this in place first.
- **Priority:** P3 (Phase 4/5 — build only once the foundation above is stable, per the directive's own
  phasing).

### F-15 — AI outputs on documents are structurally non-authoritative (intake-gated) [RE-VERIFIED — STRENGTH]

- **Current implementation:** `DocumentAttachment.cs` carries `AIStatus`, `AISummary`, `AIKeywords`,
  `AITags`, `AIConfidence`, `AIError`, `AIProcessedDate` — and, critically, an `IntakeStage` field
  (`Uploaded, Classifying, PendingEngineeringReview, PendingManualClassification, Routed, Rejected`) plus
  `RoutedModule`/`RoutedEntityId`. The doc comment on the entity states explicitly: *"Upload دیگر خودکار یک
  EngineeringDocument نمی‌سازد. فایل ابتدا این‌جا... می‌نشیند تا AI آن را دسته‌بندی کند و بعد به ماژول درست
  هدایت شود"* ("Upload no longer automatically creates an EngineeringDocument. The file first sits here...
  for AI to classify, then gets routed to the correct module") — meaning an uploaded file does not become an
  authoritative `Document`/`EngineeringDocument` row until routed, which is a real architectural gate against
  AI silently creating or approving controlled records.
- **Gap:** Not independently verified this session whether any code path allows AI to directly flip
  `Document.Status`/`IsFrozen` post-routing (i.e., after a document already exists) — flagged for follow-up,
  not claimed either way.
- **Priority:** Verification task, P3.

### F-16 — Authorization is enforced server-side via a real ABAC layer, not UI-only [RE-VERIFIED — STRENGTH]

- **Current implementation:** `DocumentWorkflowService.RecordClientResponseAsync` (`:326`) calls
  `_accessControlService.AuthorizeForRolesAsync(actorRoles, "Document", "RecordClientResponse")` and throws
  `UnauthorizedAccessException` on denial — a genuine data-driven permission check (not a hardcoded role
  array; the code comment confirms this replaced a hardcoded `ClientResponseRoles` array during an earlier
  RBAC-ABAC hardening phase, consistent with `.claude/Role/PEMS_RBAC_ABAC_PHASE1_FOUNDATION_REPORT.md` and
  `PHASE2_SCOPE_SOD_REPORT.md`, which document a dedicated multi-phase authorization hardening effort for
  this exact domain).
- **Gap:** None found this session for this specific action; a full enumeration of every document-related
  permission string was not completed this session (time-boxed) — flagged for the Phase 1 implementation
  pass to confirm full SoD coverage (e.g., preparer-cannot-approve-own-document) before extending the
  authorization surface.
- **Priority:** Verification task, P2.

---

## 2. What this audit deliberately did not re-verify this session (explicit, not silent)

The following items from the prior Three-Pass audit were **not** independently re-checked this session
(time-boxed effort) and should not be assumed either fixed or still-broken without a follow-up pass:
discipline-level dashboard/heatmap reporting, vendor-performance view, SLA/review-time aggregate metrics,
MDR register missing-columns claim, first-class navigation items (My Discipline/Pending Review/IDC as
dedicated nav entries), Document 360° tab count/structure, `Details.cshtml` localization consistency,
`aria-label` accessibility coverage, and Contract/Vendor reverse-navigation collections. These are carried
into the Gap Matrix as **"CARRIED, NOT RE-VERIFIED"** rather than omitted or silently trusted.

See `PEMS_EDMS_GAP_MATRIX.md` for the consolidated, priority-ordered list, and
`PEMS_EDMS_IMPLEMENTATION_ROADMAP.md` for the phased plan.
