# PEMS Engineering Documents — IDC Ownership & Lifecycle Correction: Audit & Implementation Report

## 1. Deep Audit Report

### 1.1 Document domain model
`PEMS.Domain/Entities/Document.cs` (base, `BaseEntity`) carries the single whole-lifecycle
`Status` field (`Draft/Submitted/IDC Review/Checked/Approved/Issued/Rejected/Revise & Resubmit`,
plus client-side `Client Approved`), and `DisciplineId` (the originating discipline).
`EngineeringDocument : Document` adds `PreparedBy/CheckedBy/ApprovedBy` + Id/Date, and
`ClientApprovalStatus` (a cache of the latest revision's client review). `DocumentRevision` is a
separate, standalone entity (own `Status`, `SupersededRevisionId` chain, revision-scoped client
review fields) — not derived from `Document.Status`.

### 1.2 IDC / discipline review model
`PEMS.Domain/Entities/DocumentIdcReferral.cs` recorded only *which* disciplines were referred —
it had **no result field at all**. The actual per-discipline decision lived only transiently in
the shared workflow engine's `WorkflowInstanceStage`/`WorkflowHistory` tables
(`PEMS.WorkflowPlatform`), never surfaced back onto the document-domain model.

### 1.3 The core bug — state-machine / transition logic
`EngineeringDocumentWorkflowSeeder.cs` (workflow version 2) defined the IDC stage's `Reject`
transition as `ToStage = null` — a terminal transition. `WorkflowRuntimeService.
DetermineStageOutcome`'s `AllApprovalsRequired` strategy resolves a parallel stage's outcome to
`"Reject"` if **any** participant rejects. `ExecuteActionAsync` then executes the terminal
transition: `instance.Status = "Rejected"`. `DocumentWorkflowService.IdcRespondAsync` copied
this straight onto `document.Status`, cleared `Checked*/Approved*` metadata, and auto-generated
an official internal rejection letter — all from a single discipline reviewer's own HTTP
request, with no originator/approver step in between. This was confirmed as intentional-as-
implemented (not merely latent) by the existing test
`IdcRespondAsync_Reject_RejectsWholeDocument_AndClearsCheckMetadata`, which asserted this exact
behavior as correct.

### 1.4 Comments / Comment Sheet
`DocumentComment` (shared with VDR) is document-level, not revision-scoped (no `RevisionId`
FK), and has no `DisciplineId` attribution field. It has a disposition-like `ActionStatus`
lifecycle (`OpenComment/NoActionRequired/ResponsePending/Verified/Closed`) and a
`ParentCommentId` carry-forward hook — but nothing auto-populates the carry-forward link on
revision creation; it's a manual, single-comment, opt-in mechanism today
(`CreateRevision`'s `triggeringCommentId` parameter).

### 1.5 Revision model
`CreateRevision` marks the previous revision `Superseded`, resets the document-level cache
fields (`Status="Draft"`, `ClientApprovalStatus=null`, etc.), and does **not** touch
`DocumentIdcReferral` or `DocumentComment` rows — old IDC/comment data is preserved untouched,
not reset, but also not linked to the new revision (no revision FK exists on either entity).

### 1.6 Controllers / 1.7 Permissions
`EngineeringDocumentsController.IdcRespond` had essentially no authorization beyond "your
employee record's discipline matches a pending referral" — materially weaker than the formal
`Approve` action (role + own-discipline + SoD check). This asymmetry was a *symptom* of the
core bug (IDC had too much power for how lightly it was gated), not a missing-permission bug in
itself — discipline reviewers legitimately should be lightly gated, since IDC input is meant to
be advisory.

### 1.8 Views
Both the header action bar and the IDC tab in `Details.cshtml` rendered `Approve`/`Comment`/
`Reject` buttons for IDC, visually and lexically indistinguishable from the document's own
formal Approve/Reject buttons shown at other stages. The reject modal's own copy claimed
"the document goes back to the originator" — which was not what the code actually did (it
finalized `Rejected` immediately).

### 1.9 Audit trail
The legacy `DocumentWorkflow` table is written only once (at `Create`) and not used for later
transitions. The real, actually-used audit trail is the shared, append-only
`WorkflowHistory` table, which is comprehensive at the engine level but does not expose
discipline/comment/responder cleanly to the document-domain layer without parsing role strings.

### 1.10 Existing tests
`PEMS.Tests/Engineering/EngineeringWorkflowTests.cs` had a test whose name and assertions
*encoded the bug as the correct contract*. No test exercised the "multiple disciplines, mixed
Approve/Reject" scenario central to the reported business requirement.

### 1.11 Shared workflow infrastructure
Engineering Documents shares `IWorkflowRuntime`/`WorkflowRuntimeService` with VDR, MOC, Permit,
TechnicalQuery, and WorkOrderReview — but each module owns independent `WorkflowDefinition`/
`WorkflowVersion`/stage/transition rows (own `Module` string). `DocumentIdcReferral` is also
reused by VDR's own referral mechanism. This meant the fix could be scoped entirely to
Engineering-Document-specific files (seeder, adapter service, controller/view, tests) without
touching the shared engine or any other module's seeded workflow graph.

---

## 2. Standards Alignment Assessment

ISO 19650 does not prescribe a specific IDC UI or workflow — it governs **information
management principles**. The relevant alignment:

| ISO 19650 principle | PEMS mechanism it governs | Alignment before fix | Alignment after fix |
|---|---|---|---|
| Originating task team retains responsibility for its information container | `Document.DisciplineId` (originating discipline), `EngineeringDocument.PreparedBy/CheckedBy` | **Violated** — a reviewing discipline could unilaterally finalize the container's status | **Aligned** — only the Approval-stage actor (a role tied to the originating team's management chain) can set a terminal status |
| Check / Review / Approve are distinct transitions, not one collapsed step | Check stage → IDC stage → Approval stage | Partially aligned (stages existed) but IDC's outcome bypassed Approval | Aligned — IDC outcome always routes through Approval |
| Interdisciplinary coordination informs, but does not replace, the originator's decision | IDC parallel stage, `DocumentIdcReferral` | **Violated** | **Aligned** — `DocumentIdcReferral` is now the durable, consolidated review record the originator/approver consults |
| Traceability of review/approval history | `WorkflowHistory` (engine, append-only), `AuditLog` | Aligned at engine level, weak at domain level (no durable discipline-level record) | Strengthened — `DocumentIdcReferral` now durably records discipline/decision/comment/responder/date |
| Revision control / audit trail | `DocumentRevision`, `SupersededRevisionId` | Aligned (revision chain itself is sound) — not touched by this fix | Unchanged (see §9, deferred work) |

PEMS's own "IDC" procedure (parallel multi-discipline review, `Approve`/`Comment`/`Reject`
vocabulary) is a PEMS-specific organizational implementation choice, not something ISO 19650
mandates — this fix does not change that vocabulary or invent a new one; it only corrects which
actor's action is authoritative for the container's status.

---

## 3. Current vs. Target Workflow

```text
BEFORE (bug):
Check → IDC (parallel) --Reject(any 1 discipline)--> [TERMINATED: Document.Status = "Rejected"]
                        --Approve(all)-------------> Approval → Issue

AFTER (fixed, workflow version 3):
Check → IDC (parallel, advisory) --Approve or Reject(any outcome)--> Approval (Document.Status = "Checked")
                                                                            │
                                                            Approve / Reject / Revise & Resubmit
                                                            (existing, correctly-gated actions)
                                                                            │
                                                                          Issue
```

The Approval stage's `Approve`/`Reject`/`ReviseAndResubmit` actions already existed, already had
correct role + SoD gating — no new controller action or role was required. The correction was
entirely about *routing* (which stage IDC's outcome leads to) and *durability* (recording each
discipline's own result permanently), not about inventing a new decision layer.

---

## 4. Entity / Model Impact

| Entity | Change | Type |
|---|---|---|
| `DocumentIdcReferral` | Added `Status` (default `"Pending"`), `Comment`, `RespondedById`, `RespondedDate` | Additive, nullable except `Status` (has a safe default) |
| `IdcStatusItemDto` | Added `Comment`, `RespondedByName`, `RespondedDate` | Additive |
| `WorkflowVersion` (data, via seeder) | New version 3 for the `EngineeringDocument`/`Approval` workflow definition | Additive — versions 1/2 untouched, existing running instances unaffected |

No entity was deleted, renamed, or had a column removed. No duplicate Review/Workflow entity
was created — `DocumentIdcReferral` (already existing) was extended to become the durable
discipline-review record.

## 5. Permission Impact

None. `IdcRespond`'s authorization (discipline membership) is unchanged and is now *correctly
scoped* to its now-corrected, advisory-only power. The Approval stage's existing role + SoD
gating (`CanApproveDocumentDisciplineAsync`, `IAccessControlService.AuthorizeAsync`) is
unchanged and is now the sole path to a terminal document decision reached via the IDC route.

## 6. UI Impact

`Details.cshtml`: IDC action buttons relabeled to disambiguate from formal decision buttons
("My IDC Review: No Comment / Approve with Comments / Revision Required" instead of bare
"Approve/Comment/Reject"); the IDC tab table extended with Comment/Responded By/Responded Date
columns and an explanatory note that IDC results are advisory; both IDC response modals'
copy corrected to state accurately that the action does not itself reject/return the document.
No route, no permission check, and no other page was changed.

## 7. Migration Impact

One additive migration, `20260916232627_AddIdcReferralOutcomeTracking`: four `AddColumn`
statements on `DocumentIdcReferrals` (`Status` non-null with default `'Pending'` — historically
honest, since old rows never had a recorded outcome; `Comment`/`RespondedById`/`RespondedDate`
nullable). No column removed, no data transformed, no other table touched. Applied to the local
database; verified via `sys.columns`.

The workflow graph correction itself required **no migration** — it is seeded data (workflow
version 3), added via `EngineeringDocumentWorkflowSeeder.SeedVersion3Async`, following this
file's own pre-existing versioning law (published versions are never mutated).

## 8. Test Plan / Results

- Focused: `PEMS.Tests/Engineering/EngineeringWorkflowTests.cs` — rewrote the test that had
  encoded the bug as correct behavior (`IdcRespondAsync_Reject_DoesNotRejectDocument_
  MovesToApprovalInstead`), and added: durable per-discipline result persistence, mixed
  Approve/Reject across multiple disciplines proceeding to Approval intact, and the formal
  `RejectAsync` at the Approval stage remaining a distinct, separately-authorized action after
  IDC. **16/16 passed.**
- Full regression: `dotnet test PEMS.slnx` — see final chat report for the run captured
  alongside this document (executed after these changes; VDR/MOC/Permit/TechnicalQuery/
  WorkOrderReview tests, which also depend on the shared `WorkflowRuntimeService`, are included
  in that run to confirm no cross-module regression, since `DocumentIdcReferral` is shared with
  VDR).
- `dotnet build PEMS.slnx` — 0 errors both before and after the view/test edits.

## 9. Exact Files Changed

- `PEMS.Domain/Entities/DocumentIdcReferral.cs` — added outcome-tracking fields.
- `PEMS.Persistence/Configurations/DocumentIdcReferralConfiguration.cs` — column constraints for the new fields.
- `PEMS.Persistence/Migrations/20260916232627_AddIdcReferralOutcomeTracking.cs` (+ `.Designer.cs`, snapshot) — new additive migration.
- `PEMS.Persistence/Seed/EngineeringDocumentWorkflowSeeder.cs` — added workflow version 3 (corrected IDC→Approval routing); versions 1/2 untouched.
- `PEMS.Persistence/Servises/DocumentWorkflowService.cs` — `IdcRespondAsync` no longer lets IDC outcome set `Document.Status = "Rejected"`; persists the discipline's result on `DocumentIdcReferral`; `GetIdcStatusAsync` reads from the durable referral record instead of transient engine tables.
- `PEMS.Application/Interfaces/IDocumentWorkflowService.cs` — extended `IdcStatusItemDto`; corrected stale XML doc comments.
- `PEMS.Web/Views/EngineeringDocuments/Details.cshtml` — IDC tab shows consolidated comment/responder/date; button labels and modal copy corrected to not imply IDC finalizes the document.
- `PEMS.Tests/Engineering/EngineeringWorkflowTests.cs` — rewrote the bug-encoding test; added 3 new tests.

## 10. Final Implementation Report

**Classification: Corrected.** The reported defect — a single discipline's IDC decision
unilaterally terminating the whole document's lifecycle — has been fixed at its root (the
seeded workflow transition graph) with a version-bump (not a mutation of the existing published
graph), backed by a durable, auditable per-discipline review record, and reflected in
accurate UI copy. The formal approval/rejection authority remains exactly where it already
correctly existed (the Approval stage, gated by role + SoD) — no new role, no new permission,
no duplicate workflow engine, no deleted history.

**Deferred, explicitly not implemented in this pass** (flagged, not silently dropped): revision-
scoped comments (`DocumentComment` has no `RevisionId`/`DisciplineId` FK), a formal comment-
disposition state machine distinct from the existing `ActionStatus`, and systematic carry-
forward linkage on revision creation (today's `ParentCommentId`/`triggeringCommentId` are
manual, opt-in mechanisms). These are real, audited gaps (Phases 5-6 of the requesting brief)
but are materially separate, larger bodies of additive schema/entity work from the reported
critical defect (IDC terminating document ownership), and implementing them here would have
exceeded "the minimum clean change required." Recommend a dedicated follow-up phase if the
business wants revision-scoped comment traceability and a formal disposition workflow.
