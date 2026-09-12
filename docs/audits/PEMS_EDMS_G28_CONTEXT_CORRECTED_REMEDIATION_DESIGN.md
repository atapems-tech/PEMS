# PEMS EDMS — G-28 Context-Corrected Remediation Design: External Approval Recognition

**Scope:** Documentation and design only. **No application code, migration, schema, or production data was changed in the course of this task.** This document corrects the framing of the original G-28 audit (`PEMS_EDMS_G28_MDR_IMPORT_APPROVAL_AUDIT.md`) — it does not retract that audit's technical trace, which remains accurate; it corrects the *interpretation* of what that trace means for a petrochemical EPC project being onboarded mid-lifecycle.

---

## 1. Corrected Executive Verdict

The original G-28 audit's technical findings stand unchanged and are re-confirmed in §3 below. What was wrong was the *framing*: treating "MDR import can set `Document.Status = Approved`" as a problem to be *prevented* rather than a problem to be *made honest*.

**Corrected verdict:** PEMS must be able to represent that a document was already, legitimately, approved by a client, consultant, or predecessor system before PEMS was ever introduced to the project — this is not an edge case, it is the *normal* condition for any EDMS adopted mid-project. The defect is not that imported approvals exist. **The defect is that the current implementation writes an imported approval into the exact same field, with the exact same semantics, as a document that was Checked and Approved by a named PEMS user through the native workflow this week — with no way, anywhere downstream, to tell the two apart.** Distinguishing them is the fix. Rejecting or blocking imported approvals is not, and was never, the correct target.

**Classification (unchanged from the original audit, now correctly justified):**
```text
BUSINESS DECISION REQUIRED
```
Not because imported approval is suspect, but because *how much authority* to grant a recognized external approval (full operational/Issue eligibility vs. a gated acceptance step) is inherently a project-policy decision, not a technical one — see §10.

---

## 2. Confirmation of Repository State

Verified directly (not assumed):
- `git status --short` shows the only tracked-file changes present are the ones already made and reported in **Phase 4** (`EngineeringDocumentsController.cs`, `DocumentWorkflowController.cs`, `EngineeringDocumentIntelligenceService.cs`, plus `graphify-out/` refreshes) and the three Phase 4 test files — **nothing new was added or modified by the original G-28 audit task**, which produced exactly one file: `docs/audits/PEMS_EDMS_G28_MDR_IMPORT_APPROVAL_AUDIT.md`.
- `git diff --stat` on the three Phase 4 code files shows the identical diff size reported at the end of Phase 4 (103 insertions / 5 deletions total across three files) — confirming zero additional code changes occurred during or after the G-28 audit.
- No new migration exists in `PEMS.Persistence/Migrations/` beyond Phase 3's `AddTransmittalHistoricalSnapshotAndAcknowledgement` (Phase 4 required none).
- No test file was added or modified beyond Phase 4's three.
- **Conclusion: the previous G-28 audit task was audit-only, exactly as instructed. The application's structure, behavior, and data are unchanged from the end of Phase 4.**

---

## 3. Current-State Technical Trace (re-confirmed, reframed)

All facts from the original audit are re-confirmed as accurate:

- `MDRImportService.ComputeStatusFromCommentStatus` (`MDRImportService.cs:1038-1047`) maps spreadsheet comment codes `"AP"`/`"NC"` directly to the string `"Approved"`, written to both `Document.Status` and new `DocumentRevision.Status` rows, for both newly-created and existing (non-frozen) documents, with no distinction from a workflow-derived value.
- `EngineeringDocumentWorkflowBackfillService` (runs on every app startup) treats any `Document.Status == "Approved"` identically regardless of origin, creating a genuine `WorkflowInstance` `Pending` at the `"Issue"` stage.
- `EngineeringDocumentsController.Issue`'s only precondition is the string `Status == "Approved"`.
- `EngineeringReportService.ApprovedCount` counts the same string without origin awareness.
- `RolePermissions`: `DocumentController` holds `MDRImport.Approve` but not `EngineeringDocuments.Approve`.
- Existing, real, in-repo precedents for keeping an *external party's* review/approval code in its **own field**, separate from a document's native lifecycle status, already exist and are proven in production:
  - **`VDRSubmission.ReviewCategory`** (`PEMS.Domain/Entities/VDRSubmission.cs:41`) — stores the exact same code vocabulary (`AP/NC/AC/CO/RE`) as MDR's `CommentStatus`, as its own field on the vendor-document submission cycle, **never** written into a shared authoritative status field. This is the single most directly relevant existing pattern in the codebase, and it already handles this exact scenario correctly for vendor documents.
  - **`DocumentRevision.ClientReviewStatus`/`ClientReviewComment`/`ClientReviewDate`/`ClientReviewLetterId`** (added in Phase 2 of this same session) — a revision-scoped, clearly-named field for the client's post-issue response, entirely separate from `DocumentRevision.Status`/`Document.Status`.
  - **`AIDocumentClassification.ReviewStatus`** (`Draft/Approved/Rejected`) — a precedent for "a suggested/external value stays non-authoritative until a named human explicitly confirms it," structurally the same shape Option C below would need.
  - **`MDRImportBatch`** (`ImportedById`, `ImportDate`, `FileName`) — existing batch-level provenance, not currently linked forward to the individual `Document`/`DocumentRevision` rows it touches.

**Reframing:** the codebase already knows how to do this correctly (VDR proves it) — the MDR import path is the one place that didn't follow the pattern the rest of the system already established.

---

## 4. Mid-Project Onboarding Scenario

A petrochemical EPC project onboarding PEMS mid-lifecycle will, as a matter of routine, have:
- A pre-existing MDR spreadsheet (often client-maintained or consultant-maintained) recording years of genuine transmittal/review history, including documents already **Approved for Construction**, already **Issued**, or already subject to a full client review cycle (`AP`/`AC`/`CO`/`RE`) that happened entirely outside PEMS, in the client's or a predecessor EDMS's own system.
- Documents whose *only* evidence of approval is that external spreadsheet plus (if available) the actual transmittal/comment-sheet correspondence — not a PEMS `WorkflowInstance`, because PEMS did not exist yet when the approval happened.
- A live, ongoing set of documents mid-revision, where only the *latest* revision's status matters going forward, but the *history* (who approved revision 0, when, under what transmittal) must be preserved for traceability, audits, and handover.
- Cases where the imported evidence is incomplete (a status code with no attached comment sheet, no transmittal reference, or a date gap) — these must be visibly flagged as **weaker evidence**, not silently treated identically to a fully-evidenced import.

**Forcing every one of these through a full internal Draft→Submit→Check→Approve replay inside PEMS is operationally indefensible** — it would require re-litigating years of already-closed client review cycles, waste real engineering review capacity on documents the client has already accepted, and in some cases would be **impossible** (the original preparer/checker/approver may no longer be assigned to the project, or the review criteria PEMS's own workflow enforces did not exist at the time of the original approval).

**What PEMS must preserve, per document/revision, for such records:**
1. The **origin** of the approval (which of the categories in §5).
2. The **source system or party** (client name, consultant name, predecessor EDMS identifier, or "MDR import" as a fallback).
3. The **revision** the approval applies to (never ambiguous — Phase 3's revision-scoped snapshot pattern already proves this is achievable).
4. The **date** of the original approval (not the date it was imported into PEMS — these are different facts).
5. The **evidence** available (transmittal number, comment-sheet number/date, attached file if any) — and an honest flag when evidence is thin.
6. Whether it has been **operationally recognized** in PEMS (i.e., is PEMS treating it as sufficient to allow further downstream action like Issue) — this is the one fact that is a genuine *PEMS decision*, not an imported fact, and must be visibly separate from the imported data itself.

---

## 5. Approval-Origin Model (Conceptual — no fields proposed yet)

The following categories are evaluated, not yet implemented as fields/entities, per the task's explicit constraint:

| Category | Meaning | Existing partial precedent |
|---|---|---|
| **Internal PEMS Workflow Approval** | Passed through `DocumentWorkflowService.ApproveAsync`, backed by a real `WorkflowInstance`, subject to SoD | Fully implemented today (`Document.Status`/`WorkflowInstance`) |
| **External Client Approval** | The project's client formally approved the document, evidenced by a transmittal/comment sheet, outside PEMS's own workflow | `VDRSubmission.ReviewCategory` (vendor-facing analog), `DocumentRevision.ClientReviewStatus` (post-issue client-response analog) |
| **External Consultant Approval** | A third-party consultant (not the client, not the EPC contractor) approved the document | No direct precedent; conceptually the same shape as Client Approval, distinguished only by *which party* |
| **Imported Legacy Approval** | Recorded in an MDR/VDR spreadsheet with no further identifiable source system — the current MDR-import scenario | `MDRImportBatch` (batch provenance only, not linked forward) |
| **Migrated Approval from Previous System** | A predecessor EDMS (not a spreadsheet) whose own audit trail is being carried over | No precedent — would need a "source system identifier" concept if ever pursued |
| **Controlled Administrative Recognition** | A named, authorized PEMS user explicitly reviewed the imported evidence and formally accepted it as sufficient for PEMS purposes (this is *not* the same as performing the original engineering approval — it is accepting that the approval already happened elsewhere) | `AIDocumentClassification.ReviewStatus` (Draft/Approved/Rejected human-confirmation pattern) is the closest structural precedent |
| **Unknown / Unverified** | A status string exists with no evidence attached at all | No precedent — this is the honest "we don't actually know" bucket that the current implementation silently doesn't have, because everything defaults to looking identical to a real approval |

**Key structural insight, reusing existing architecture rather than duplicating it:** the shape of "an external/imported signal that is not authoritative on its own but can be explicitly, auditably promoted into an operationally-recognized state by a named accountable person" **already exists twice** in this codebase (VDR's `ReviewCategory` + separate promotion logic, and AI classification's `ReviewStatus` Draft→Approved-by-a-human pattern). Whatever is eventually built for MDR import approval-origin recognition should follow the **same shape**, not invent a third one.

---

## 6. `MDRImport.Approve` Semantic Analysis

**Can `MDRImport.Approve` legitimately mean "approval/acceptance of imported MDR information" rather than "engineering approval through the native workflow"?**

**Yes — and this reframing is actually more accurate to what the permission is already protecting.** The permission gates `MDRImportController.Commit`, i.e., "commit this batch of parsed spreadsheet rows into the authoritative register." That is fundamentally a **data-acceptance** action (do I trust this spreadsheet enough to load it), not an **engineering-approval** action (do I, as a qualified reviewer, approve this document's technical content). The current defect is not that this permission exists or that `DocumentController` holds it — a document controller accepting a client's MDR spreadsheet as data is entirely reasonable. **The defect is that accepting the *data* currently, silently, also grants the *engineering-approval* consequence** (Issue-eligibility, KPI counting, SoD-invisible "Approved" status) that the permission was never named or scoped to grant.

**Controls needed so `MDRImport.Approve` cannot silently impersonate `EngineeringDocuments.Approve`:**
1. Committing a batch should be able to **load and preserve** the imported comment-status/approval evidence without that alone flipping the authoritative `Document.Status`/`DocumentRevision.Status` to `"Approved"` (see Option B/C in §10) — i.e., decouple "I accept this data is now in the register" from "this document is now Issue-eligible."
2. If a project policy genuinely wants `MDRImport.Approve` holders to also be able to *operationally recognize* an external approval as sufficient, that should be an **explicit, separate, auditable action** (§5's "Controlled Administrative Recognition"), not an automatic side effect of loading a spreadsheet row — this keeps the permission's blast radius honest and reviewable.
3. Whether `DocumentController` should hold recognition authority at all, or whether recognition should require the same tier as `EngineeringDocuments.Approve` (`EngineeringManager`/`EngineeringSupervisor`), is itself one of the required business decisions (§13) — this analysis does not assume either answer.

**Not recommended:** removing or merging `MDRImport.Approve` — it serves a real, distinct purpose (data acceptance) that should continue to exist, just without the current unintended side effect.

---

## 7. `EngineeringDocumentWorkflowBackfillService` Reassessment

The service's actual, legitimate job — bridging documents that were genuinely mid-workflow *before* the Workflow Platform existed inside PEMS — is sound and should **continue**. The problem is narrower than "disable backfill": it currently cannot distinguish *why* a document's `Status` says `"Approved"`.

**Recommended target behavior (not implemented here):**
- Continue to backfill documents whose status genuinely reflects PEMS's own historical Draft→Submit→Check→Approve progression (this is its original, correct purpose).
- For a document whose `"Approved"`/`"Checked"`/`"Submitted"` status originates from **import** rather than native progression, backfill should **not** silently create a full, indistinguishable `WorkflowInstance` at the `"Issue"` stage. Instead — depending on which option (§10) the business selects — it should either: (a) not create an instance until an explicit recognition action has occurred (Option B/C), or (b) create the instance but tag it as originating from a recognized-external-approval, so downstream consumers *can* choose to treat it differently if a future need arises (Option A, if fully built out).
- **Backfill should depend on approval provenance and documented project policy, not on the status string alone** — this requires the provenance concept from §5 to exist first; backfill itself would then read it, rather than being redesigned as a parallel decision-maker.
- The desired outcome, restated: backfill remains a legitimate, necessary migration/recognition mechanism — it should not remain the *unconditional* mechanism it is today for every possible origin of that status string.

---

## 8. Issue Eligibility Analysis

Today, `EngineeringDocumentsController.Issue`'s only gate is `Status == "Approved"` — origin-blind. For a mid-project-onboarded document, **Issue eligibility for an externally-approved document is a legitimate, desirable outcome** (a client-approved-for-construction document should not need to be artificially re-issued through PEMS before site work can reference it) — the corrected question is not "should imported approval ever allow Issue" but **"should it allow Issue automatically and silently, or only after the project has explicitly decided that recognized external approvals are Issue-eligible, and after a named person has confirmed the specific evidence is sufficient?"** That distinction is exactly what recognition (§5, §10 Option C) is for. Once a document carries a recognized (however that ends up being modeled) external approval, `Issue`'s precondition could legitimately be satisfied by *either* a native `"Approved"` *or* a recognized external approval — the point is that the recognition step, not the raw import, is what should carry the authority.

---

## 9. KPI / Reporting Analysis

`EngineeringReportService.ApprovedCount` (`x.Status == "Approved" || x.Status == "Issued"`) should evolve toward a breakdown, not a single number, once origin is modeled:

```text
PEMS Workflow Approved      — passed through DocumentWorkflowService, backed by a real WorkflowInstance history
Client / External Approved  — recognized external approval, evidence attached
Imported Historical Approved — comment-status present but not yet formally recognized
Controlled Recognized Approved — explicitly accepted via a named administrative recognition action
Pending Acceptance          — imported evidence exists, awaiting a recognition decision
Unknown / Unverified        — a status string exists with no attached evidence at all
```

**Avoiding double-counting across document revisions:** a project-level "total approved" figure should count **at most one row per `Document.DocumentId`**, driven by that document's **current/latest active revision's** resolved status (the same "latest revision" concept Phase 2/3 already establish for client review and Transmittal snapshots) — never by summing across all historical `DocumentRevision` rows, which would over-count documents with many superseded revisions. This requires no new counting mechanism, only that the eventual origin-aware status be evaluated per-document against its current revision, consistent with how `EngineeringReportService` already scopes its query to `EngineeringDocuments`, not `DocumentRevisions`.

---

## 10. Architecture Options (Corrected)

### Option A — Native PEMS Approval Only
All imported approvals require full internal re-approval before being treated as Issue-eligible or counted as Approved.
- **Why unsuitable for mid-project onboarding:** forces re-litigating years of closed client review cycles; may be organizationally impossible (original approvers no longer on the project); actively discards legitimate, already-paid-for engineering/client review effort; likely to be rejected by the project team as impractical on day one of adoption.
- **Operational cost:** high and ongoing — every mid-project document import would need a full manual re-approval queue before becoming usable in PEMS at all.
- **Verdict:** not recommended for this business context.

### Option B — Trusted External Approval Recognition
Imported approvals (with sufficient evidence — source, revision, date, authority) are recognized as valid per documented project policy, while remaining permanently distinguishable from native PEMS approval in the data model, reports, and UI.
- Preserves full operational value of legitimate external approvals immediately on import.
- Requires the provenance/origin concept (§5) to be real, not just conceptual — this is real, if moderate, implementation work.
- Risk is concentrated in "sufficient evidence" being a judgment call the system must support (e.g., a status code with no transmittal/comment-sheet reference is weaker evidence than one with both) rather than a binary gate.

### Option C — Controlled Acceptance / Transfer
Imported approval evidence is retained and displayed, but becomes *operationally* recognized only after an explicit, authorized, auditable acceptance action by a named person — without ever claiming the original approval happened inside PEMS.
- Combines Option B's respect for legitimate external approval with an explicit accountability checkpoint (mirrors `AIDocumentClassification`'s Draft→human-confirmed→Approved shape, and VDR's own submission-then-review shape).
- The acceptance action is itself a real, auditable PEMS event (who accepted this, when, on what evidence) — closing the SoD/audit gap the original G-28 audit identified, without pretending the *engineering* review happened in PEMS.
- Slightly more process overhead than Option B (a person must click "accept"), but that overhead is exactly the accountability the original audit found missing.

### Recommendation for a petrochemical project entering PEMS mid-lifecycle
**Option C, converging toward Option B's data richness.** Reasoning: the project needs immediate operational continuity (ruling out A), but a completely silent, automatic recognition (pure B) reproduces the same "who is accountable for this becoming Issue-eligible" gap the original audit flagged — just with better labels. An explicit, lightweight, per-document (or per-batch, for efficiency) acceptance action, performed by someone with the same authority tier the project decides is appropriate (see §13), gives the project the operational speed of trusting the client's own MDR data while keeping a real, named, auditable PEMS decision-maker in the loop — exactly mirroring how `AIDocumentClassification` suggestions become authoritative only once a human confirms them.

---

## 11. Interim Safeguards (documentation-only, no implementation performed)

Until a future phase implements the chosen option, the following are recommended as **process-level**, non-code safeguards the project team can apply today with zero PEMS changes:
1. When committing an MDR batch that will set any document to `"Approved"`, document controllers should record (outside PEMS, e.g. in the commit's own change-request ticket) which rows carry a real client comment-sheet/transmittal reference vs. a bare status code with no evidence — this is the exact "sufficient evidence" judgment §5/§10 will eventually need to formalize.
2. Treat `EngineeringReportService.ApprovedCount` as **not yet a trustworthy compliance metric** for documents originating from MDR import until origin-awareness is built — this is an honest caveat to give report consumers now, not a code change.
3. Be aware that `EngineeringDocumentWorkflowBackfillService` will, on the next application restart, make any imported `"Approved"` document genuinely Issue-eligible — if the project does not want that yet for a specific batch, do not commit it until the team is ready for that consequence.

---

## 12. Business Decisions Still Required

1. Does this project's governance require a distinct, visible "externally approved / not yet PEMS-recognized" state at all, or is immediate full recognition (Option B, no acceptance checkpoint) acceptable?
2. Who should hold the authority to perform a "Controlled Administrative Recognition" action (Option C) — the same tier as `EngineeringDocuments.Approve` (`EngineeringManager`/`EngineeringSupervisor`), or is `DocumentController`'s current `MDRImport.Approve` tier considered sufficient by the organization for this specific, narrower "accept external evidence" responsibility?
3. What counts as "sufficient evidence" for a project — is a bare comment-status code enough, or is a referenced transmittal/comment-sheet number mandatory before recognition is even offered?
4. Should Issue eligibility for a recognized-but-not-natively-approved document require anything beyond recognition itself (e.g., a secondary confirmation), or is recognition alone sufficient per this project's document-control procedure?
5. Should `EngineeringReportService`'s KPI breakdown (§9) be a project-configurable choice (some projects may not care about the distinction) or a permanent, always-on feature?

None of these were answered unilaterally in this document, per the task's explicit instruction.

---

## 13. Future Implementation Phases

A future, explicitly-scoped phase (after the business decisions in §12 are answered) should, in order:
1. Model the minimal origin/provenance concept needed for whichever of Option B/C is chosen — reusing the existing `VDRSubmission.ReviewCategory`/`DocumentRevision.ClientReviewStatus`/`AIDocumentClassification.ReviewStatus` shapes as direct precedent rather than inventing a new pattern.
2. Link `MDRImportBatch` provenance forward to the specific `Document`/`DocumentRevision` rows it touches (currently batch-level only).
3. Make `EngineeringDocumentWorkflowBackfillService` provenance-aware per §7.
4. If Option C, build the explicit acceptance action (permission, controller action, audit event) as its own reviewed unit of work, following the exact "reuse DocumentWorkflowService, don't duplicate it" discipline established in every prior phase of this session.
5. Update `EngineeringReportService` and the Document Register's filters last, once the underlying data is trustworthy enough to report on.
6. Resolve the still-open technical question from the original G-28 audit (§1.4 there): whether `IWorkflowRuntime.ExecuteActionAsync`'s stage-matching can be exploited when `Document.Status` and `WorkflowInstance.CurrentStageId` disagree for an existing, mid-review document whose status was overwritten by a later import — this needs a dedicated trace of `WorkflowRuntimeService.DetermineStageOutcome` regardless of which option is chosen, since it is a correctness question independent of the origin-model design.

---

## 14. Data-Preservation Requirements

Whatever is eventually built must, at minimum:
- **Never discard** the raw imported `CommentStatus`/comment-sheet/transmittal reference data already captured (`Document.CommentStatus` already retains this today, per `MDRImportService.cs:466` — this must continue).
- **Never retroactively alter** a document's or revision's already-recorded history to "fix" past imports without an explicit, reviewed, and separately-authorized data-correction action — consistent with this entire session's standing rule against fabricating or silently rewriting historical records.
- **Preserve revision-level distinctness** — an approval (of any origin) must always be attributable to a specific `DocumentRevision`, never left ambiguous across a document's full history, consistent with the precedent already established in Phase 1 (revision-file binding) and Phase 3 (Transmittal historical snapshot).
- **Preserve the existing `DocumentWorkflow` audit rows** the import path already creates (with their `"MDR Import: ..."` comment convention) — any future structured-origin field should be **additive alongside** this existing trail, not a replacement for it.

## 15. Explicit Non-Goals

This document does **not**:
- Propose any new database field, entity, enum, or migration.
- Recommend rejecting, invalidating, or blocking any currently-imported "Approved" document or historical data.
- Decide which of Option B or Option C the project should adopt — that is the business decision in §12.
- Change, remove, or restrict `MDRImport.Approve`, `DocumentController`'s role grants, or any existing permission.
- Modify `EngineeringDocumentWorkflowBackfillService`, `MDRImportService`, `EngineeringDocumentsController`, or `EngineeringReportService` in any way.
- Assume mid-project MDR data is untrustworthy — the opposite is asserted: it is very often the *only* legitimate record of a document's real-world approval history, and PEMS's job is to represent that honestly, not to discard or distrust it.

## 16. Explicit Statement

**No application code was modified. No migration was created. No database schema or production data was changed. No status value was renamed. No new entity or property was introduced. No commit or push was performed.** This document is corrected analysis and design guidance only, superseding the *interpretation* (not the technical facts) of the original G-28 audit.
