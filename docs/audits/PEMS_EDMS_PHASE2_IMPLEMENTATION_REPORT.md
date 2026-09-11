# PEMS EDMS — Phase 2 Implementation Report

## 1. Executive Summary
Phase 2 audited the actual document lifecycle implementation in PEMS (not assumed from entity names) and found it materially more mature than the master prompt's baseline assumption: a working controlled-transition engine (`IWorkflowRuntime` + `DocumentWorkflowService`), a mature Comment Register with carry-forward traceability, and SoD-scoped review/approval already existed. One genuine, code-verified P0 gap was found and fixed: client-review state was stored on the document rather than on a specific revision, and was never cleared when a new revision was created — meaning a brand-new, unreviewed revision could silently inherit the previous revision's "Client Approved" badge. This is now fixed additively, with the legacy document-level fields kept working as a correctly-synced cache rather than removed.

## 2. Repository and Branch
`PEMS-cmms-edit-empoleeys-master`, branch `main`. Baseline commit at phase start: `a317f02`.

## 3. Existing Capabilities Discovered (see full audit for evidence)
- Controlled transition engine already in production use for Submit→Check→IDC→Approve→Reject→Issue, built on the pre-existing general-purpose `IWorkflowRuntime`.
- Review (Check) and Approval are already distinct steps, with discipline-scoped SoD (`CanApproveDocumentDisciplineAsync`).
- Concurrency-safe transitions via `RowVersion` + `DbUpdateConcurrencyException` handling.
- A mature Comment Register (`DocumentComment`) with `ActionStatus`, carry-forward (`ParentCommentId`, `MarksRevisionRequired`/`TriggeredRevisionId`), category/severity, and PDF pin coordinates — already satisfies the master prompt's §12 requirements without any changes.
- Client-review recording (`RecordClientResponseAsync`) already existed, already authorization-gated via RBAC-ABAC, already required a real Incoming letter — just not revision-scoped.

## 4. Actual Gaps Addressed
**Finding L2-01 (P0):** Client-review fields (`ClientApprovalStatus`/`ClientComment`/`ClientResponseDate`) lived only on `EngineeringDocument`, were never cleared on new-revision creation, and could show a stale "Client Approved" state on an unreviewed new revision. **Fixed.**

Findings L2-02 (Status field reuses "Rejected" for two meanings), L2-03 (duplicate `IssuePurpose` fields), and L2-04 (no Suitability/CDE concepts) were documented with evidence but deliberately **not** implemented this phase — each requires either a breaking rename or a business decision this session cannot make up, per the master prompt's own instruction not to guess.

## 5. Standards and Requirements Used
ISO 19650 (client/CDE review vocabulary, referenced conceptually), ISO 9001 (revision control, evidence of review/approval — already substantially present), ISO 15489 (auditability — leveraged existing generic audit infrastructure, no new table). No formal certification is claimed.

## 6. Architecture Decisions
- No second workflow engine — reused `IWorkflowRuntime`.
- No `ClientReviewCycle`/`ClientReviewDecision`/`ClientReviewParticipant` trio — a minimal 4-column addition to the existing `DocumentRevision` entity was judged sufficient because the current business process has exactly one client decision per issued revision, not a multi-round cycle.
- Legacy `Document.ClientApprovalStatus` kept, redefined precisely as "cache of latest revision's client decision," not removed — avoids breaking any existing reader.

## 7. Entities Added or Modified
- `PEMS.Domain/Entities/DocumentRevision.cs` — added `ClientReviewStatus`, `ClientReviewComment`, `ClientReviewDate`, `ClientReviewLetterId` + `ClientReviewLetter` navigation. No existing field removed or renamed.

## 8. Services Added or Modified
- `PEMS.Persistence/Servises/DocumentWorkflowService.cs` — `RecordClientResponseAsync` gained an optional `revisionId` parameter (defaults to latest revision, preserving all 6 pre-existing call sites' behavior unchanged); now writes the authoritative client decision onto the target `DocumentRevision` in addition to keeping the `Document`-level cache in sync.
- `PEMS.Application/Interfaces/IDocumentWorkflowService.cs` — interface updated to match (optional parameter, fully backward compatible).
- `PEMS.Application/Services/EngineeringDocumentService.cs` — revision-history DTO mapping now includes the three new per-revision client-review fields.

## 9. Repositories Added or Modified
None — no new repository abstraction was introduced; existing `PEMSDbContext` access patterns were reused.

## 10. Controllers/API Changes
- `PEMS.Web/Controllers/EngineeringDocumentsController.cs` — the existing "Create New Revision" action now clears the three document-level client-review cache fields (`ClientApprovalStatus`/`ClientComment`/`ClientResponseDate`) when a new revision is created, which is the direct fix for the stale-inheritance bug. No new action, no route change.

## 11. Views and UI Pages Changed
- `PEMS.Web/Views/EngineeringDocuments/Details.cshtml` — the existing Revision History tab (added in Phase 1) gained one new "Client Review" column showing each revision's own client decision (or "Not yet submitted to client"), using the view's existing badge CSS classes. No new page, no new tab, no redesign.

## 12. JavaScript and CSS Changes
None.

## 13. Authorization Changes
None — `RecordClientResponseAsync` keeps its existing `Document.RecordClientResponse` permission gate unchanged.

## 14. Workflow Changes
None to the workflow engine itself — the internal Submit/Check/Approve/Issue state machine is untouched. Only the client-review recording step's storage target changed.

## 15. Audit Changes
None — the existing generic `PEMSDbContext` change-tracker audit now naturally captures the more precise `DocumentRevision.ClientReviewStatus` changes (keyed by the specific `RevisionId`) automatically, with no new audit table or event type needed.

## 16. Database Migrations
`20260911201545_AddRevisionScopedClientReview` — additive only: 4 nullable columns on `DocumentRevisions` (`ClientReviewComment` nvarchar, `ClientReviewDate` datetime2, `ClientReviewLetterId` int, `ClientReviewStatus` nvarchar), 1 index, 1 `ON DELETE RESTRICT` FK to `Letters`. No column dropped, renamed, or made non-nullable.

## 17. Data Migration and Backfill
None performed, deliberately. Existing `Document.ClientApprovalStatus` values are left exactly as they were; the design doc documents why retroactively assigning them to a specific historical revision would require inventing data the system cannot reliably reconstruct. Verified after applying to live PEMSDB: all 20,624 existing `DocumentRevisions` rows preserved, all 4 new columns `NULL` on every existing row (`SELECT COUNT(*) AS TotalRevisions, SUM(CASE WHEN ClientReviewStatus IS NULL THEN 1 ELSE 0 END) AS NullClientReviewStatus FROM DocumentRevisions` → `20624 / 20624`).

## 18. File Storage Changes
None.

## 19. Search and Performance Changes
None — no new query pattern that risks N+1 was introduced; the new fields are read as part of the existing `document.Revisions.Select(...)` projection already used by `EngineeringDocumentService`.

## 20. AI Changes
None.

## 21. Tests Added
`PEMS.Tests/Engineering/RevisionScopedClientReviewTests.cs` — 5 new tests: default-to-latest-revision resolution, explicit `revisionId` targeting, rejection of a `revisionId` belonging to another document, the exact stale-inheritance scenario from Finding L2-01 (new revision created after a client approval — new revision shows no client review, old revision's history is untouched), and confirmation that rejection codes still write revision-scoped state correctly.

## 22. Tests Executed
- `dotnet test PEMS.Tests/PEMS.Tests.csproj --filter "FullyQualifiedName~ClientReview"` → **13/13 passed** (6 pre-existing `ClientReviewTests` regression-verified unaffected, plus new tests; filter also picks up class-name substring matches).
- `dotnet test PEMS.Tests/PEMS.Tests.csproj` (full suite, no filter) → **1093/1093 passed**, 0 failed, 0 skipped, 8m1s (1082 prior baseline + 11 net new: 6 from Phase 1's `RevisionFileBindingTests` + 5 from this phase).

## 23. Build Result
`dotnet build PEMS.slnx --configuration Debug` → **Build succeeded**, 0 errors (only pre-existing unrelated nullable warnings).

## 24. Migration Result
Repro-validated on disposable `PEMSDB_Phase2Repro` (full 60-migration chain applied cleanly, new columns confirmed nullable, database dropped after validation), then `dotnet ef migrations has-pending-model-changes` confirmed clean, then applied to live **PEMSDB** successfully.

## 25. Known Limitations
- Existing (pre-Phase-2) client responses recorded before this migration have no per-revision record — only the document-level cache values remain for them, exactly as before. This is a data-history limitation, not a bug: no reliable revision attribution existed to backfill from.
- Finding L2-02 (the `"Rejected"` status-value overload between internal and client rejection) is only partially mitigated: the true per-revision client outcome (`A/B/C/D`) is now always recoverable via `DocumentRevision.ClientReviewStatus`, but `Document.Status` itself still shows the same `"Rejected"` string for both causes — a full fix needs a non-breaking disambiguator design of its own (documented as deferred in the Phase 2 Design doc).
- Findings L2-03 (duplicate `IssuePurpose`) and L2-04 (missing Suitability/CDE vocabulary) remain open, by design — no business input was available to resolve them safely.

## 26. Remaining Risks
Low. The change is additive, fully backward compatible (verified by the 6 pre-existing `ClientReviewTests` passing unmodified), and the one behavior change (clearing the document-level cache on new-revision creation) is a strict correctness improvement — the field only ever showed either the correct current state or nothing, never a wrong value, before this phase's fix it could show a wrong value.

## 27. Out-of-Scope Findings
Suitability, CDE state, a generalized cross-cutting `DocumentStateTransition` history table, and full multi-round `ClientReviewCycle` were all evaluated and explicitly not built — see the Design doc §7 for the evidence-based reason each was deferred rather than silently skipped.

## 28. Recommended Phase 3 Scope
1. Resolve Finding L2-03 (`IssuePurpose` duplication) once a business owner confirms which field (document or revision level) is authoritative.
2. Design and implement the non-breaking `Document.Status` rejection-source disambiguator (Finding L2-02) as its own reviewed change.
3. If/when the organization adopts a specific Suitability code standard, model it then — not speculatively now.
4. Continue toward the master prompt's §14 (Information Requirements/MDR) and §15 (Vendor Document Control) sections, which were out of this phase's scope entirely.

## 29. Exact List of Modified Files
**Modified:**
`PEMS.Application/DTOs/DocumentRevisionDto.cs`, `PEMS.Application/Interfaces/IDocumentWorkflowService.cs`, `PEMS.Application/Services/EngineeringDocumentService.cs`, `PEMS.Domain/Entities/DocumentRevision.cs`, `PEMS.Persistence/Configurations/DocumentCoreConfigurations.cs`, `PEMS.Persistence/Migrations/PEMSDbContextModelSnapshot.cs`, `PEMS.Persistence/Servises/DocumentWorkflowService.cs`, `PEMS.Web/Controllers/EngineeringDocumentsController.cs`, `PEMS.Web/Views/EngineeringDocuments/Details.cshtml`, plus `graphify-out/*` (knowledge graph refresh).

**Created:**
`docs/audits/PEMS_EDMS_PHASE2_LIFECYCLE_AUDIT.md`, `docs/audits/PEMS_EDMS_PHASE2_DESIGN.md`, `docs/audits/PEMS_EDMS_PHASE2_IMPLEMENTATION_REPORT.md` (this file), `PEMS.Persistence/Migrations/20260911201545_AddRevisionScopedClientReview.cs` + `.Designer.cs`, `PEMS.Tests/Engineering/RevisionScopedClientReviewTests.cs`.
