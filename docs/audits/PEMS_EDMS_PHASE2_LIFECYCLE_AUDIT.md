# PEMS EDMS — Phase 2 Lifecycle Audit

**Repository:** `PEMS-cmms-edit-empoleeys-master` · **Branch:** `main` · **Baseline commit:** `a317f02`
**Scope:** Controlled document lifecycle, revision status, review/approval separation, client-review state, transition engine, authorization, audit trail — per the Phase 2 master prompt.
**Method:** Direct file inspection (no assumption from names alone). Every claim below cites an exact file and, where useful, a line number.

---

## 0. Phase 1 State Check (mandatory pre-check)

Before starting Phase 2, git state was inspected as instructed:

- `git log --oneline -3` → `a317f02` (Phase 0 audit + Phase 1 revision-file binding) is the tip of `main`.
- `git status --short --branch` → `## main...origin/main [ahead 2]`, working tree otherwise clean except the pre-existing untracked production files this session has never touched (`filestore/*`, `wwwroot/uploads/*`).
- **Conclusion:** Phase 1 was already fully **committed**, not mixed with unrelated changes. It was **not yet pushed** to `origin/main` at the start of this phase (network to github.com was intermittently failing across the prior turns). It has since been pushed (`703cb6f..a317f02 main -> main`) as a separate, non-destructive operation — no reset, no force-push, no discarded work.

---

## 1. Status Ownership (§6.1)

| Field | Entity | File:Line | Authoritative for |
|---|---|---|---|
| `Status` | `Document` (base, TPT) | `PEMS.Domain/Entities/Document.cs:45` | Overall document workflow stage: `Draft, Submitted, IDC Review, Checked, Approved, Issued, Rejected, Client Approved, Revise & Resubmit` (enumerated in `PEMS.Web/Views/EngineeringDocuments/Index.cshtml:15`) |
| `CommentStatus` | `Document` | `Document.cs:49` | Comment-sheet aggregate status (W.F.C/NC/AC/CO/AP/RE) — already separate from `Status` per the code's own comment "طبق قانون تفکیک Status/Action" |
| `IsFrozen` + `FrozenDate/FrozenById/FreezeReason` | `Document` | `Document.cs:54-58` | Freeze — independent boolean, enforced (not just displayed) in `EngineeringDocumentRepository` per Phase E-01 |
| `IsOnHold` + `DocumentHold` history | `Document` | `Document.cs:68-70` | Hold — independent of Freeze; multi-cycle history in `DocumentHold` |
| `AIWorkflowStage` | `Document` | `Document.cs:94` | AI intake pipeline stage — unrelated business dimension |
| `Status` | `DocumentRevision` | `DocumentRevision.cs:27` | Per-revision status: `Draft/Superseded` (only two values ever assigned in code — see §3) |
| `ClientApprovalStatus`, `ClientComment`, `ClientResponseDate` | `EngineeringDocument` | `EngineeringDocument.cs:65-67` | Client decision code (`A/B/C/D`) — **stored on the document, not on a revision** |
| `IssuePurpose` | both `EngineeringDocument.cs:42` and `DocumentRevision.cs:31` | Issue purpose — duplicated on two entities (see Finding L2-03) |

**Answer to "is status stored in multiple places, can it become inconsistent":** Yes — `Document.Status` and `DocumentRevision.Status` are two independently-writable strings. They are kept *reasonably* in sync by `DocumentWorkflowService` for the internal review/approve/issue path (§2), but **not** for the client-review path (Finding L2-01) and **not** on manual `IssuePurpose` edits (Finding L2-03).

**Single authoritative service:** `PEMS.Persistence/Servises/DocumentWorkflowService.cs`, implementing `IDocumentWorkflowService`, is the only place that drives the internal Submit→Check→Approve→Issue transition chain. It is a thin adapter over the pre-existing general-purpose `IWorkflowRuntime` engine (`PEMS.WorkflowPlatform`) — **a controlled transition engine already exists and is already reused here**; this session does not need to build a new one for the internal path.

---

## 2. Existing Controlled Transition Engine (§9 — already substantially implemented)

`DocumentWorkflowService` (`PEMS.Persistence/Servises/DocumentWorkflowService.cs`) exposes `SubmitAsync/CheckAsync/ApproveAsync/RejectAsync/ReviseAndResubmitAsync/IssueAsync/IdcRespondAsync/RecordClientResponseAsync`. Each of the internal-workflow methods:

- Loads the document, calls `_runtime.ExecuteActionAsync(instanceId, actionName, actorEmployeeId, actorRoles, comment, ipAddress, userAgent, ...)` on the shared `IWorkflowRuntime` (`GetActiveInstanceIdOrThrowAsync` at line 502 requires a `Running` `WorkflowInstance` — an already-existing transition/state-machine record, not a bare string field).
- Persists the resulting stage via `MapStageToLegacyStatus` (line 522) back onto `Document.Status`, so the legacy string field stays a readable *projection* of the real state machine, not the source of truth itself.
- Records `IPAddress`/`Browser` (`EngineeringDocumentsController.cs` → `GetClientIp()/GetUserAgent()`, passed straight into `_runtime.ExecuteActionAsync`), satisfying the "digital approval trail" requirement of §13/§20 for the internal path.
- Is wrapped in `SaveWithConcurrencyCheckAsync()` (`DocumentWorkflowService.cs:537`), which catches `DbUpdateConcurrencyException` — `Document`/`BaseEntity` carries a `RowVersion` (`PEMS.Domain/Common/BaseEntity.cs:24`), so concurrent double-approval is already blocked at the database level, not just assumed away.

**Controller-level guards are pre-checks only, not the enforcement boundary.** `EngineeringDocumentsController.Approve/Reject/Issue/...` (lines 1052-1400) each re-check `document.Status` before calling the service — this is UX (fail fast with a friendly message), not the actual authorization boundary, because `DocumentWorkflowService` itself is unreachable except through the runtime's `GetActiveInstanceIdOrThrowAsync`/`ExecuteActionAsync`, which independently validates the instance is `Running` and the action is legal for the current stage. Bypassing the controller (e.g. calling the service directly, or via a future API) cannot skip transition validation.

**Separation of duties, already present:**
- `CanApproveDocumentDisciplineAsync(document.DisciplineId)` (`EngineeringDocumentsController.cs:1204`) — an approver must be in the document's own discipline.
- `RecordClientResponseAsync` gates on the data-driven permission `Document.RecordClientResponse` via `IAccessControlService.AuthorizeForRolesAsync` (`DocumentWorkflowService.cs:326`) — confirmed by the dedicated authorization test `ClientReviewTests.RecordClientResponseAsync_UnauthorizedRole_Throws`.
- IDC (inter-discipline coordination) responses are scoped to the responding discipline's own role (`IdcRespondAsync`, line 371-402) — a discipline cannot approve on behalf of another.

**Verdict:** §9's "Controlled Transition Engine" requirement is **already fully implemented** for the internal Submit/Check/IDC/Approve/Reject/Issue path via the pre-existing `IWorkflowRuntime`. No second engine is needed or was built. Gaps found are narrower and listed below.

---

## 3. Findings

### Finding L2-01 (P0) — Client-review state is document-level, not revision-level, and is not cleared when a new revision is created
**Evidence:**
- `EngineeringDocument.ClientApprovalStatus/ClientComment/ClientResponseDate` are fields on the **document**, not `DocumentRevision` (`EngineeringDocument.cs:65-67`).
- `DocumentWorkflowService.RecordClientResponseAsync` (lines 313-368) writes these three fields directly onto the `EngineeringDocument`, keyed only by `documentId` — there is no `revisionId` parameter at all, and no code path associates the client decision with `document.Revisions.OrderByDescending(...).First()` or any other specific revision.
- The "Create New Revision" action (`EngineeringDocumentsController.cs:820-918`) resets `document.Status = "Draft"` (line 897) when a new revision is created, but **never touches** `ClientApprovalStatus`, `ClientComment`, or `ClientResponseDate` — confirmed by `grep -n "ClientApprovalStatus\|ClientComment\|ClientResponseDate" PEMS.Web/Controllers/EngineeringDocumentsController.cs` returning zero matches outside the field's own read-only usages.
- `EngineeringDocumentService.cs:234` maps `document.ClientApprovalStatus` straight into the document details DTO shown to the UI, with no revision qualifier.
- Existing test coverage (`PEMS.Tests/Engineering/ClientReviewTests.cs`, 6 tests) verifies `RecordClientResponseAsync`'s authorization/validation/status-transition behavior in isolation, but **no test exercises the revision-creation interaction** — the gap is real and untested, not hypothetical.

**Concrete failure scenario:** Revision 00 is issued, the client responds with code `A` (`ClientApprovalStatus = "A"`, `Document.Status = "Client Approved"`). A comment later marks the document `MarksRevisionRequired = true` and Revision 01 is created — `Document.Status` correctly resets to `"Draft"`, but `Document.ClientApprovalStatus` still reads `"A"` and `ClientComment`/`ClientResponseDate` still show the Revision-00 client feedback. Any view or report reading `ClientApprovalStatus` in isolation (which is the only way it is exposed today) will report the **new, unreviewed Revision 01 as client-approved**, which is exactly the false-inheritance failure mode Phase 2 exists to close.

**Applicable requirement:** Master prompt §10 ("Client-review state must be tied to a specific revision... A new revision must not inherit client approval as if it were independently approved").
**Risk:** High — a false "Client Approved" badge on an unreviewed revision is a real document-control integrity failure in a regulated engineering environment.
**Compatibility impact:** None if fixed additively (see Design doc §2) — the legacy `Document.ClientApprovalStatus` field stays and continues to work for existing readers, it is just kept correctly in sync instead of stale.
**Priority:** P0.

### Finding L2-02 (P1) — `Document.Status` conflates workflow stage with client-review outcome, and reuses `"Rejected"` for two unrelated meanings
**Evidence:**
- `Document.Status` values actually assigned in code: `Draft, Submitted, IDC Review, Checked, Approved, Issued` (internal workflow stages, from `MapStageToLegacyStatus`, `DocumentWorkflowService.cs:522-535`) plus `Rejected` (internal rejection, `RejectAsync`/`IdcRespondAsync`) **and** `Client Approved` / `Rejected` again (client-review outcome, `RecordClientResponseAsync:355-360`).
- `"Rejected"` is written both when an internal reviewer rejects (`RejectAsync`) and when a client returns code B/C/D (`RecordClientResponseAsync:358`) — there is no way to tell from `Document.Status` alone which actor rejected the document. `ClientReviewTests.RecordClientResponseAsync_RejectionCodes_RejectDocument_AndClearApprovalMetadata` only asserts `doc.Status == "Rejected"`, confirming the collision is by design, not an oversight the tests caught.
- The document register's status filter (`PEMS.Web/Views/EngineeringDocuments/Index.cshtml:15`) lists all eight values in one flat array, so a document controller filtering by "Rejected" cannot distinguish an internally-rejected draft from a client-rejected issued revision.

**Applicable requirement:** Master prompt §7.1 ("Do not use one generic status field to represent... whether a client has accepted it... whether it is approved internally").
**Risk:** Medium — reporting/filtering ambiguity, not a data-loss risk (the underlying `ClientApprovalStatus` code A/B/C/D is preserved separately, so the raw truth is recoverable, just not filterable/reportable cleanly).
**Compatibility impact:** Fixing this by renaming existing `Status` values would break the register filter, saved views, and any external report keyed on the current strings — **out of scope for an additive Phase 2 pass**. The Design doc proposes an additive disambiguator instead of a rename.
**Priority:** P1 — documented and partially mitigated by Finding L2-01's fix (see Design §2), full resolution deferred.

### Finding L2-03 (P2) — `IssuePurpose` is duplicated on `EngineeringDocument` and `DocumentRevision`, kept in sync in only one direction
**Evidence:**
- `EngineeringDocument.IssuePurpose` (`EngineeringDocument.cs:42`) and `DocumentRevision.IssuePurpose` (`DocumentRevision.cs:31`) are two independent nullable strings.
- `DocumentWorkflowService.IssueAsync` (line 255) writes `document.IssuePurpose` at issue time; it never writes `DocumentRevision.IssuePurpose`. The "Create New Revision" controller action (`EngineeringDocumentsController.cs`) does not set `DocumentRevision.IssuePurpose` either — grepping the codebase shows `DocumentRevision.IssuePurpose` is defined but never assigned anywhere outside test fixtures.
**Applicable requirement:** §6.1 ("Are status values duplicated? Are status values synchronized?").
**Risk:** Low — dead/unused field, not actively causing incorrect display today, but a latent trap for a future developer who assumes the revision-level field is populated.
**Priority:** P2 — documented, not fixed this phase (no evidence of active harm; fixing would require a business decision on which field is authoritative, which the master prompt explicitly says to flag rather than guess at — §6.2 "Requires business clarification").

### Finding L2-04 (P2) — No `Suitability` or `CDE state` concept exists anywhere in the domain model
**Evidence:** `grep -r "Suitability\|CDEState\|CdeState" PEMS.Domain` returns no matches in any `Document*` entity (the only `Suitability`-adjacent hits across the whole solution are in unrelated files: `VDRItem.cs`, `MrsImportRow.cs`, `EngineeringRelationship.cs`, none of which represent the ISO 19650 concept).
**Classification (§6.2):** **Missing** for both concepts, project-wide.
**Applicable requirement:** §8.6/§8.8.
**Risk:** Low today (no consumer of these concepts exists yet), but blocks any future ISO-19650-style CDE reporting.
**Priority:** P2 — explicitly **not** implemented this phase per the master prompt's own instruction ("Do not introduce these values blindly... Requires business clarification" — no project stakeholder has defined which Suitability code set this organization uses). Recorded as a Phase 3+ candidate in the roadmap, not implemented speculatively.

---

## 4. §6.2 Lifecycle Concept Classification (evidence-based)

| Concept | Classification | Evidence |
|---|---|---|
| Document lifecycle status | Present but semantically overloaded | `Document.Status` mixes workflow stage + rejection source (Finding L2-02) |
| Revision status | Partially implemented | `DocumentRevision.Status` only ever set to `Draft`/`Superseded` in code — never `Approved`/`Issued`/`Rejected` at the revision level (those live only on `Document.Status`) |
| Review status | Fully implemented | `Submit → Check → IDC Review` stages, tracked via `IWorkflowRuntime` instance/stage records, independent of Approval |
| Approval status | Fully implemented | `ApproveAsync`, distinct from Check, gated by discipline scope (`CanApproveDocumentDisciplineAsync`) |
| Issue status | Fully implemented | Dedicated `IssueAsync`, only reachable from `Approved`, writes `DocumentRevision.IssuedDate` |
| Client review status | **Partially implemented — the Phase 2 focus** | `RecordClientResponseAsync` exists and is authorized/audited, but is document-level not revision-level (Finding L2-01) |
| CDE state | Missing | Finding L2-04 |
| Suitability | Missing | Finding L2-04 |
| Issue purpose | Present but duplicated | Finding L2-03 |
| Acceptance status | Not applicable | No separate "acceptance" concept beyond client approval exists in this codebase's terminology; would duplicate Client review status if added |
| Archive status | Partially implemented | `Document.IsActive` (soft-delete/archive flag) exists; no distinct `Archived` lifecycle stage separate from soft-delete — out of scope for this pass |

---

## 5. Comment Register (§12) — audited for completeness, found materially more mature than assumed

`DocumentComment` (`PEMS.Domain/Entities/DocumentComment.cs`) already implements, independently of this phase's work:
- Sequential `CommentNo`, `Category`, `Severity`, `RequiredAction`, `DueDate`.
- `ActionStatus` (`OpenComment/VendorResponsePending/Verified/Closed`) — already a field separate from `Document.Status`, per the class's own comment "Action Status مستقل از Review Status".
- Carry-forward traceability: `ParentCommentId` (links a reopened comment to its ancestor) and `MarksRevisionRequired` + `TriggeredRevisionId` (links a comment directly to the revision it caused, once that revision exists) — this **is** the §12 "carry-forward comments must be traceable to the next revision" requirement, already built.
- PDF pin coordinates (`PageNumber`, `PinX`, `PinY`) for on-document comment placement.

**No action taken here** — rebuilding this would violate the "extend, don't duplicate" rule. The one minor gap (a comment is not explicitly stamped with which `DocumentRevision` it was raised against, only inferable via `TriggeredRevisionId` after the fact) is noted for a future phase but is not blocking and not part of this pass's verified-gap scope.

---

## 6. Authorization & Audit (§13/§20) — spot-checked, not rebuilt

- `RecordClientResponseAsync` uses the RBAC-ABAC `IAccessControlService.AuthorizeForRolesAsync("Document", "RecordClientResponse")` data-driven permission (not a hardcoded role array) — confirmed current by the code comment at `DocumentWorkflowService.cs:323-327` describing the Phase 3 RBAC-ABAC migration away from a static role list.
- Every internal transition passes `ipAddress`/`userAgent` through to the workflow runtime for the digital-approval-trail requirement (§13).
- No existing audit event distinguishes "client rejected" from "internally rejected" (same downstream consequence as Finding L2-02) — the workflow instance/stage history does record which action name was executed, so the underlying truth is recoverable from `WorkflowInstanceStages`, just not surfaced as a clean `Document.Status` filter.

---

## 7. Out of Scope This Phase (explicitly deferred, not silently skipped)

Per the master prompt's own instruction not to add speculative fields: **Suitability, CDE state, and a full generalized `DocumentStateTransition` history table are NOT implemented this phase.** The existing `WorkflowInstance`/`WorkflowInstanceStage` records already serve as the transition history for the internal path (§2), and no verified gap justifies a parallel table. `IssuePurpose` deduplication (L2-03) is deferred pending a business decision on authoritative source. These are carried into the Phase 2 Design doc's "Deferred" section with justification, not dropped silently.
