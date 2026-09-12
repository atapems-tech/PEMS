# PEMS EDMS — G-28 Audit: MDR Import "Approved" Status Semantics

**Scope:** Focused audit only. **No application code, migration, schema, or production data was changed in the course of this audit.** Every claim below cites an exact file/line, or an executed read-only query.

---

## Executive Finding

`MDRImportService.CommitBatchAsync` can set `Document.Status = "Approved"` (and the same value on the new `DocumentRevision.Status`) directly from an imported spreadsheet's comment-status code, for both newly-created and existing (non-frozen) documents — entirely bypassing `DocumentWorkflowService`, the workflow engine, discipline-scope checks, and the SoD protections wired in Phase 4.

This was already known. What this audit adds is the **full downstream trace**, which shows the risk is **not confined to a cosmetic status label**:

1. **A role that structurally lacks formal approval authority can trigger it.** `DocumentController` has `MDRImport.Approve` (the permission gating `CommitBatchAsync`) but does **not** have `EngineeringDocuments.Approve` (the formal workflow approval permission) — confirmed against the live `RolePermissions` table. This is a genuine authority mismatch, not a hypothetical one.
2. **It fully propagates into real, functional workflow capability — automatically, with no additional gate.** `EngineeringDocumentWorkflowBackfillService.BackfillAsync` — which already runs on every application startup — finds any `EngineeringDocument` with `Status == "Approved"` (or `Submitted`/`Checked`) that has no active `WorkflowInstance`, and **unconditionally creates one**, placed at the `"Issue"` stage with a genuine `Pending` `WorkflowInstanceStage` assigned to that stage's responsible role. It does not, and cannot, distinguish "this document was really approved through Check→Approve" from "this document's status string was set by an Excel import." Once backfilled, the document is issuable through the real, formal `Issue` action exactly as if it had been legitimately approved.
3. **It pollutes real reporting.** `EngineeringReportService.cs:42` computes `ApprovedCount = documents.Count(x => x.Status == "Approved" || x.Status == "Issued")` — a project-level KPI that cannot distinguish an import-set value from a genuine one.
4. **Existing safeguards are real but narrow.** Frozen documents are correctly protected (`if (doc.IsFrozen) { frozenSkipped++; continue; }`), and the import does leave a human-readable trail (synthetic `DocumentWorkflow` rows explicitly commented `"MDR Import: ..."`). But there is no structured (queryable) distinction between an import-derived status and a workflow-derived one, no actor recorded on the individual synthetic workflow-history rows, and no check against a document's *current* workflow stage before overwriting `Status` — so an existing document already mid-review (e.g. genuinely at "Checked") can have its status forced backward/forward to "Approved" by a later import row, creating a real inconsistency between the legacy `Document.Status` field and the actual persisted `WorkflowInstance.CurrentStageId` until the next backfill run reconciles it (in the "Approved" direction, since backfill only ever reads the legacy `Status` and creates a matching instance — it does not detect or flag the inconsistency, it resolves it silently in favor of whatever `Status` says).

---

## 1. Exact Code Trace

### 1.1 Staging (no authoritative effect yet)
`MDRImportService.StageFromExcelAsync` (`MDRImportService.cs:76`) parses the uploaded Excel file into `MDRImportBatch`/`MDRImportRow` (`Status = "Staged"`) — no `EngineeringDocument`/`DocumentRevision` is touched at this point. A `CommentStatus` column (aliased from headers `"commentstatus"`/`"reviewstatus"`, `MDRImportService.cs:56`) is parsed into each row's JSON payload.

### 1.2 Commit — the authoritative write path
- **Controller:** `MDRImportController.Commit(int batchId)` (`MDRImportController.cs:104`), gated by `HasPermissionAsync(_currentUser.Roles, "MDRImport", "Approve")` (line 106) — the code's own comment explicitly acknowledges this is *"the riskiest action in this pipeline"* and deliberately mirrors `VDRImportController.Commit`'s existing pattern.
- **Live permission grant (`RolePermissions` where `ModuleName='MDRImport'`):**

  | Role | CanApprove (gates Commit) |
  |---|---|
  | `DocumentController` | **1** |
  | `EngineeringManager` | 1 |
  | `Viewer` | 0 |

  Compare to `RolePermissions` where `ModuleName='EngineeringDocuments'`: `CanApprove` is granted **only** to `EngineeringManager`/`EngineeringSupervisor` — `DocumentController` is explicitly **not** trusted to approve a document through the front door, yet **is** trusted to trigger the same end-state through MDR import commit.
- **Service:** `MDRImportService.CommitBatchAsync` (line 309) wraps `CommitBatchInternalAsync` (line 332) in a real DB transaction (`BeginTransactionAsync`/`CommitAsync`/`RollbackAsync` via `CreateExecutionStrategy`) — the write is atomic per batch.
- **Per-row logic** (`CommitBatchInternalAsync`, lines 395-608):
  - **New document** (line 441): created with `Status = "Draft"` initially (line 452), then immediately overwritten at line 467 (see below) if a `CommentStatus` is present on the latest revision.
  - **Existing document** (line 416): `if (doc.IsFrozen) { frozenSkipped++; continue; }` (lines 423-427) — **frozen documents are correctly protected**, the only such guard in this method.
  - **Status assignment** (line 467): `doc.Status = ComputeStatusFromCommentStatus(latestRevision.CommentStatus, doc.Status);` — applies to both new and existing (non-frozen) documents, unconditionally, with **no check of the document's current workflow stage or `WorkflowInstance` state**.
  - **Freeze assignment** (lines 471-474): `if (payload.IsFrozen && !doc.IsFrozen) { doc.IsFrozen = true; doc.FrozenDate ??= DateTime.Now; }` — sets `IsFrozen` directly too, **without** `FrozenById`/`FreezeReason` (both left null), unlike the formal `Freeze` action which requires a reason and records who froze it.
  - **Per-revision status** (lines 500-513): each newly-created `DocumentRevision` also gets `Status = ComputeStatusFromCommentStatus(rev.CommentStatus, "Draft")` — so historical revisions imported from the spreadsheet also carry `"Approved"` directly.
  - **Synthetic audit trail** (lines 532-572): the method **does** proactively create `DocumentWorkflow` rows with `Comment` fields explicitly prefixed `"MDR Import: ..."` (e.g. `"MDR Import: Client Status {code}. Comment Sheet: {no}"`) — a real, human-readable provenance trail exists in the same table real workflow actions use, but **none of these synthetic rows set `ActionById`** (the actor field is left null on all three blocks), so "who" is not captured at the individual-action level (only `MDRImportBatch.ImportedById` captures who ran the *overall* import, and that FK is not linked forward from the `Document`/`DocumentRevision`/`DocumentWorkflow` rows it produces).
  - A synthetic `Transmittal` is also created inline (`From = "MDR Import"`, `Status = "Sent"`, lines 585-599) — a separate, parallel code path from `TransmittalLinker.cs` (used elsewhere for the same purpose), not part of G-28's core concern but noted for completeness.

### 1.3 The `Status` mapping function
```csharp
private static string ComputeStatusFromCommentStatus(string? commentStatus, string currentStatus) =>
    commentStatus switch
    {
        "AP" or "NC" => "Approved",
        "CO" or "AC" or "RE" => "Rejected",
        "W.F.C" => "Submitted",
        _ => currentStatus
    };
```
(`MDRImportService.cs:1038-1047`) — a direct, unconditional string mapping from the spreadsheet's own comment-status code. No confidence score, no source-trust flag, no distinction from a real internal `DocumentWorkflowService.ApproveAsync` outcome.

### 1.4 Downstream propagation — the critical chain
`EngineeringDocumentWorkflowBackfillService.BackfillAsync` (`PEMS.Persistence/Seed/EngineeringDocumentWorkflowBackfillService.cs:27`) — **already wired to run on every application startup** (per its own doc comment, "هر بار برنامه بالا بیاید اجرا می‌شود" / "runs every time the app starts"), idempotently:
```csharp
var legacyToStageName = new Dictionary<string, string> {
    ["Submitted"] = "Check", ["Checked"] = "Approval", ["Approved"] = "Issue"
};
var candidates = await db.EngineeringDocuments
    .Where(d => d.IsActive && (d.Status == "Submitted" || d.Status == "Checked" || d.Status == "Approved"))
    .ToListAsync();
// ... for any candidate with no existing Running WorkflowInstance:
var instance = new WorkflowInstance { ..., CurrentStageId = stage.WorkflowStageId, Status = "Running", ... };
db.WorkflowInstanceStages.Add(new WorkflowInstanceStage { ..., Status = "Pending", AssignedToRole = stage.ResponsibleRole, ... });
```
This reads **only** the legacy `Document.Status` string — it has no way to know, and does not attempt to determine, whether that string reflects a real Check→Approve history or an import overwrite. A freshly-import-created document with `Status="Approved"` gets a genuine `WorkflowInstance` at the `"Issue"` stage, `Pending`, on the very next app restart — after which `EngineeringDocumentsController.Issue`'s only guard (`if (document.Status != "Approved")`, line 1380) passes, `DocumentWorkflowService.IssueAsync` finds a real active instance (`GetActiveInstanceIdOrThrowAsync` succeeds), and `IWorkflowRuntime.ExecuteActionAsync` finds a genuinely-matching `Pending` stage record for the assigned role — **so a formal, official Issue (with real outgoing/internal letter generation) can succeed for a document that never had a real Check or Approve.**

For an **existing** document whose status is later overwritten mid-review by a subsequent import (already has a `Running` `WorkflowInstance` from a real prior `Submit`), the backfill service's `alreadyMigratedIds` check (line 63-69) means it will **not** touch that document (an active instance already exists) — but the legacy `Document.Status` field and the real `WorkflowInstance.CurrentStageId` can now silently disagree (e.g., `Status="Approved"` while the instance is still genuinely sitting at the `"Approval"` stage) until whatever stage-transition eventually resolves it. Whether `IWorkflowRuntime.ExecuteActionAsync`'s own stage/role/pending-record matching (`WorkflowRuntimeService.cs:195-204`) would allow an `Issue` action to succeed *while the instance is still genuinely at the Approval stage* depends on `DetermineStageOutcome`'s exact logic, which was not traced to full certainty in this audit — flagged here as a specific open question for the recommended follow-up phase, not asserted either way.

### 1.5 Answers to the explicit trace questions
| Question | Answer |
|---|---|
| Creates new documents or updates existing? | Both (lines 416-459 branch on `existingDocs.TryGetValue`) |
| Affects frozen documents? | **No** for existing docs — explicitly skipped (line 423-427) |
| Affects controlled/in-workflow documents? | **Yes** — no check against `WorkflowInstance` state before overwriting `Status` |
| Can overwrite a more advanced status? | **Yes** — `ComputeStatusFromCommentStatus` unconditionally overwrites whenever the code is `"AP"/"NC"/"CO"/"AC"/"RE"/"W.F.C"`, regardless of current value |
| Transaction boundary | One DB transaction per whole batch commit (`CreateExecutionStrategy`/`BeginTransactionAsync`) |
| Actor identity on the write | `MDRImportBatch.ImportedById` (batch-level only); individual `DocumentWorkflow` rows created by this path have `ActionById = null` |
| Audit behavior | Real, human-readable, but free-text-only (`"MDR Import: ..."` comment prefix) — no structured/queryable origin field, no FK back to the batch |

---

## 2. Status Semantics

`Document.Status = "Approved"` is used, verified by direct trace, to mean **exactly one thing structurally**: "this document is in the stage immediately preceding Issue" — a single overloaded value with no sub-classification. There is no separate "externally approved" or "imported historical status" value anywhere in the schema. Consumers:

- **Workflow:** `EngineeringDocumentsController.Issue` (`line 1380`) — the sole precondition for the formal Issue action.
- **Reports:** `EngineeringReportService.cs:42` — `ApprovedCount` KPI, directly polluted.
- **Backfill/workflow-instance creation:** `EngineeringDocumentWorkflowBackfillService.cs:54` — maps `"Approved"` straight to the `"Issue"` stage, fully trusting the string.
- **SoD (Phase 4):** `DocumentWorkflowService.ApproveAsync`/`EngineeringDocumentsController.Approve` treat reaching `"Approved"` as the single authoritative event the SoD policy protects — the import path produces the identical value through a route the SoD policy cannot see (it only inspects `AuthorizationAuditLog`, which the import path never writes to).
- **Client review, VDR, Transmittal creation:** none of these gate or filter specifically on `Document.Status == "Approved"` in the code paths reviewed (Transmittal item creation resolves by `RevisionNo` match, not by status; client review — `RecordClientResponseAsync` — gates on `Status == "Issued"`, one step further along, so it is only reachable if the (potentially improperly) approved document is also (potentially improperly) issued).
- **Revision creation / Document Register filtering:** `EngineeringDocumentListFilter.Status` allows filtering the register by `"Approved"` directly — a document controller browsing "show me Approved documents" would see import-forced rows indistinguishably from real ones.

**Conclusion: this is a single, mixed, overloaded value.** There is no existing code-level distinction between "formal PEMS workflow approval" and "imported/historical/external approval" — they are, today, the exact same string in the exact same column, trusted identically by every downstream consumer traced.

---

## 3. Governance Risk — Explicit Answers

1. **Can imported status make a document appear formally approved?** Yes — identical string, identical field, identical downstream trust.
2. **Can it bypass required approval permissions?** Yes, demonstrated concretely: `DocumentController` holds `MDRImport.Approve` but not `EngineeringDocuments.Approve`.
3. **Can it bypass SoD?** Yes — the import path never calls `AccessControlService.AuthorizeAsync`, so no `AuthorizationAuditLog` entry is ever written for it, meaning Phase 4's SoD engine has no visibility into it at all (it can neither approve-then-deny nor detect a conflict, because from the SoD engine's point of view, nothing happened).
4. **Can it bypass freeze or issue rules?** Freeze: **no** for updates to existing documents (explicitly guarded). Issue: **yes** — Issue's only gate is the polluted `Status` string itself.
5. **Can it trigger downstream actions?** Yes — real reporting KPI pollution (proven), and real formal-Issue eligibility after the next app restart via the backfill service (proven).
6. **Can it overwrite a previously more advanced or controlled state?** Yes, structurally possible — no current-state check exists before the overwrite.
7. **Is the import actor distinguishable from a human approver?** Only at the coarse `MDRImportBatch.ImportedById` level, not per-document/per-revision, and not exposed to any consumer that reads `Document.Status`.
8. **Is source-system provenance retained?** Partially — a free-text `"MDR Import: ..."` comment convention exists in `DocumentWorkflow.Comment`, but it is not structured, not queryable, not linked to the batch by FK, and not surfaced anywhere in the UI/reports that consume `Status`.
9. **Is the change captured by the existing audit system?** Yes, via the generic `PEMSDbContext` change-tracker (same mechanism used everywhere else) and the synthetic `DocumentWorkflow` rows — the *fact* of the change is captured; its *classification* as import-derived vs. workflow-derived is not.
10. **Is there an existing distinction between workflow-approved and externally-approved?** **No** — confirmed absent by this trace.

---

## 4. Existing Reusable Architecture

- **Import batch identity:** `MDRImportBatch` (`MDRImportBatchId`, `ImportedById`, `ImportDate`, `FileName`, `Status`, `CommittedDate`) — already exists, already used, but **not linked forward** from the `Document`/`DocumentRevision`/`DocumentWorkflow` rows it produces.
- **Free-text provenance convention:** the `"MDR Import: ..."` comment prefix already used in `DocumentWorkflow.Comment` — a real, reusable *pattern*, though not a structured field.
- **Frozen-document protection:** the `if (doc.IsFrozen) continue;` guard is a real, working precedent for "the import path already knows how to defer to a stronger existing state" — the same pattern of thinking could extend to "defer to an existing `WorkflowInstance`'s current stage," it simply hasn't been.
- **No existing "status origin" or "workflow-approved vs. externally-approved" field or enum exists anywhere** — confirmed by repo-wide inspection of `Document`/`DocumentRevision`/`DocumentWorkflow`.

**No new entity or property is proposed by this audit** — per the task's explicit rule, and because §5 below can be evaluated using what already exists (Option B/C need no schema change at all; Option A would need a genuinely new field, which is exactly why this audit stops short of recommending it outright).

---

## 5. Options

### Option A — Imported "Approved" is authoritative
Treat the spreadsheet's comment-status code as a fully trusted, real approval signal.
**Required safeguards (none of which exist today):** a trusted-integration identity distinct from an ordinary user role (today `DocumentController`'s ordinary permission grant is doing this job); provenance carried on the `Document`/`DocumentRevision` row itself, not just a batch-level FK and a comment string; a status-origin distinction so reports/Issue/backfill can tell workflow-approved from externally-approved apart; explicit overwrite rules (must not silently regress a document already further along in real internal review); a dedicated authorization policy separate from the general `MDRImport.Approve` permission, at minimum requiring the same role tier as `EngineeringDocuments.Approve`.
- **Architectural impact:** Moderate — requires a genuine new concept (status origin), not just wiring.
- **Data model impact:** New field(s) needed (e.g., an origin/provenance marker) — explicitly out of this audit's implementation scope.
- **Workflow impact:** The backfill service would need to become origin-aware.
- **Audit impact:** Needs the synthetic `DocumentWorkflow` rows to carry a real actor/batch link, not just free text.
- **Security impact:** Requires closing the `DocumentController`/`EngineeringDocuments.Approve` mismatch specifically for this path.
- **Migration impact:** Additive only if done correctly (new nullable field); no existing data need be touched.
- **UI/reporting impact:** Register/reports would need an "externally approved" badge or filter to remain honest.
- **Operational impact:** Preserves the current EPC use case (reflecting a client's own MDR tracking) without disruption, if built correctly.
- **Risk:** High if implemented without every safeguard above — this is exactly today's unsafe state.
- **Recommendation:** Only viable with a full, deliberate build-out — not a quick fix.

### Option B — Imported "Approved" is non-authoritative
The imported status is stored as external/historical reference data and never sets the real, authoritative `Document.Status`/triggers backfill/Issue-eligibility.
- **Architectural impact:** Low — the import path stops writing to the authoritative field for this specific value; the "external" value could still be retained as-is on the same field for informational display, or simply not applied (falls back to `currentStatus`, i.e. treat "AP"/"NC" like the unmapped default case).
- **Data model impact:** None required — no new field, `ComputeStatusFromCommentStatus`'s mapping for `"AP"`/`"NC"` would simply stop being applied to `Document.Status`/`DocumentRevision.Status` for the authoritative fields (informationally, the raw `CommentStatus` column already exists and is retained regardless — `doc.CommentStatus = latestRevision.CommentStatus ?? doc.CommentStatus`, line 466 — so the *information itself* is not lost, only its authority over the workflow field is removed).
- **Workflow impact:** None — the backfill service and Issue action are unaffected because they never see an import-forced "Approved" in the first place.
- **Audit impact:** Simplifies — no need to reconcile import-vs-workflow provenance since the import path no longer sets the authoritative value.
- **Security impact:** Closes the entire chain traced in this audit immediately.
- **Migration impact:** None — no schema change.
- **UI impact:** None required, though the register could optionally surface the raw imported `CommentStatus` value alongside (already stored) as a distinct, clearly-labeled column for reference.
- **Reporting impact:** `EngineeringReportService.ApprovedCount` becomes trustworthy again without any change to it.
- **Operational impact:** Document controllers doing MDR import would need a separate, explicit path (real `Submit`→`Check`→`Approve`, or Option C) to formally progress a document that a client's spreadsheet says is approved — a process change, not just a code change.
- **Risk:** Low technically; the only risk is organizational — if the actual business process genuinely relies on import-set "Approved" today to drive real Issue eligibility, removing that silently would break a used workflow. This is precisely why this remains a business decision, not a unilateral technical call.
- **Recommendation:** The safest technical default; requires confirming with the business that nothing currently depends on import-driven auto-issuability.

### Option C — Controlled status transfer
Imported status is captured as-is (already true today via `CommentStatus`), but transferring it into the authoritative `Document.Status`/triggering a real `WorkflowInstance` requires an explicit, separate, human-in-the-loop confirmation step — e.g., a document controller reviews a post-import "these N documents were marked Approved by the import — confirm to actually approve them" screen, which then calls the *real* `DocumentWorkflowService` path (so SoD, discipline-scope, and audit all apply normally) instead of writing the field directly.
- **Architectural impact:** Moderate — a new confirmation step/action, but reuses `DocumentWorkflowService.ApproveAsync` entirely rather than duplicating it.
- **Data model impact:** None required beyond what Option B already needs (retain `CommentStatus` as informational, as it already is).
- **Workflow impact:** Documents transferred this way go through the *real* workflow engine exactly like any other approval — meaning they'd need a real `WorkflowInstance` already at the right stage, or the confirmation action would need to also drive `Submit`/`Check` first, which raises the process question of what "confirm this import-flagged approval" actually re-enacts.
- **Audit impact:** Best of all three options — every resulting approval is a genuine `DocumentWorkflowService.ApproveAsync` call, fully SoD-checked and fully logged like any other.
- **Security impact:** Closes the bypass while still letting the business use externally-recorded approval as a *trigger* for a real, controlled action, rather than as the action itself.
- **Migration impact:** None.
- **UI impact:** Needs a new confirmation screen/action in the MDR import flow.
- **Reporting impact:** Trustworthy, same as Option B.
- **Operational impact:** Preserves the business value of "the client already told us this is approved" as a prompt, while keeping a real human accountable for the actual approval action — likely the best balance of safety and usefulness, at the cost of the most implementation effort of the three.
- **Risk:** Low, but requires the most new work (a real confirmation UI/flow) to realize.
- **Recommendation:** The best long-term answer if the organization wants to preserve "import reflects real-world approval" as a genuine feature rather than discard it.

---

## 6. Final Classification

```text
BUSINESS DECISION REQUIRED
```

**With an explicit, evidence-backed lean:** the current, as-implemented state (Option "A with no safeguards," which is what exists today) is **not** defensible to leave unchanged — it fails on authority-mismatch (a role without formal Approve authority can trigger it), on audit (no structured provenance), and on downstream propagation (an already-running, unrelated backfill service silently converts it into full formal Issue-eligibility). This audit does not classify it as `SAFE AS-IS`, and does not recommend leaving it there while a decision is pending if that can be avoided. However, a full `DEFECT — MUST BLOCK AUTHORITATIVE APPROVAL` classification would presume the answer to a question only the business can answer: **does PEMS need to trust a client/contractor's own MDR spreadsheet as evidence of real approval at all?** If yes, Option A (built properly) or Option C is the path. If no, Option B is a same-day, zero-schema-change fix. Either way, the decision — not a unilateral code change — is the correct next step, per this task's own explicit rule.

---

## Required Follow-Up Phase

A dedicated phase (call it Phase 5 or later, at the organization's discretion) should:
1. Confirm with the actual MDR/document-control business process owner whether real operational workflows currently depend on import-set "Approved" driving real Issue-eligibility.
2. Based on that answer, select Option B (fast, safe default) or Option C (preserves the business value with full governance) — Option A is not recommended without very strong justification, given how much safeguard-building it requires to reach parity with B/C's safety at greater cost.
3. Separately and regardless of the above: resolve the `DocumentController` role's `MDRImport.Approve` vs. `EngineeringDocuments.Approve` authority mismatch, since it exists independent of which option is chosen.
4. Separately, complete the specific open question flagged in §1.4 — whether `IWorkflowRuntime.ExecuteActionAsync`'s stage-matching can be exploited when `Document.Status` and `WorkflowInstance.CurrentStageId` disagree for an *existing* document whose status was overwritten mid-review — this needs a dedicated trace of `WorkflowRuntimeService.DetermineStageOutcome` before it can be closed either way.

## Explicit Statement

**No application code was modified. No migration was created. No production data was changed. No status value was renamed. No new entity or property was introduced.** This document is evidence and analysis only, per the task's explicit constraints.
