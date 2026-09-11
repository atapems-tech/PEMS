# PEMS EDMS — Phase 2 Lifecycle Design

Companion to `PEMS_EDMS_PHASE2_LIFECYCLE_AUDIT.md`. Scope is intentionally narrow: fix the one P0 gap the audit verified with code evidence (Finding L2-01 — client-review state is document-level and goes stale across revisions), disambiguate its worst side effect (part of L2-02), and explicitly defer everything the audit found already-implemented or not yet justified by real evidence.

## 1. Target Model

**No new workflow engine, no new generic `DocumentStateTransition` table, no `Suitability`/`CDEState` fields.** The audit found a working controlled-transition engine (`IWorkflowRuntime` + `DocumentWorkflowService`) already covering Submit/Check/IDC/Approve/Reject/Issue, and found no real (non-speculative) consumer for Suitability/CDE today. Building either would violate "do not duplicate" / "do not add fields simply because they sound useful."

What Phase 2 adds, on `DocumentRevision` (additive columns only):

```
DocumentRevision
  + ClientReviewStatus     string?   // "A" | "B" | "C" | "D" — same code vocabulary as
                                      // today's Document.ClientApprovalStatus, so existing
                                      // client decision semantics are not reinvented
  + ClientReviewComment    string?
  + ClientReviewDate       DateTime?
  + ClientReviewLetterId   int?      // FK -> Letters, same "must come from a real Incoming
                                      // letter" rule the document-level version already enforces
  + ClientReviewLetter     Letter?
```

This is the minimal structure the master prompt itself allows ("assess whether a minimal structure is sufficient" — §10) instead of the heavier `ClientReviewCycle`/`ClientReviewDecision`/`ClientReviewParticipant` trio, because:
- There is exactly one client decision per revision-submission today (no multi-round client review cycle exists in the current business process — confirmed by `RecordClientResponseAsync`'s single-call shape), so a cycle/participant model would model a process that does not exist yet.
- The existing "response must come from a real Incoming Letter" governance rule is preserved by keeping the `ClientReviewLetterId` FK, not discarding it for a free-text field.

`Document.ClientApprovalStatus/ClientComment/ClientResponseDate` **stay** (removing them would break the ~unknown number of existing external readers of `EngineeringDocumentService`'s DTO and the Index/Details views) but change meaning slightly: they become a **denormalized cache of the latest revision's client-review fields**, kept correct by two rules instead of one:
1. `RecordClientResponseAsync` writes to *both* the target `DocumentRevision` (source of truth) and the parent `Document` (cache), same as before.
2. **New:** "Create New Revision" clears the `Document`-level cache fields to `null` the moment a new revision is created, because the new revision has no client decision yet. This is the direct fix for Finding L2-01's concrete failure scenario.

Old revisions' own `ClientReviewStatus/Comment/Date` are never touched by a later revision's creation — historical truth is preserved exactly as §7.4 requires, satisfiable simply because each revision now owns its own copy of the fields.

## 2. Transition Rule Added

`RecordClientResponseAsync` gains an optional `revisionId` parameter (default: `null`):

- If `revisionId` is supplied, it must belong to the target document and must not already be superseded by a newer revision that itself has active client-review data — reject with a clear message otherwise ("client response can only be recorded against the current revision").
- If omitted (all 6 existing `ClientReviewTests` call sites, and the existing controller action), it resolves to `document.Revisions.OrderByDescending(x => x.RevisionId).First()` — **the exact same "latest revision" resolution the "Create New Revision" flow already uses for `SupersededRevisionId`**, so no new resolution rule is invented.
- This keeps the method's existing 8-parameter call sites (`ClientReviewTests.cs`, `EngineeringDocumentsController.RecordClientResponse`) **fully backward compatible** — they compile and behave identically, because the new parameter is optional and defaults to today's implicit behavior.

Guard preserved unchanged: `document.Status != "Issued"` still gates the call (a client can only respond to something that was actually issued) — this rule is untouched, only *where* the response is stored changes.

## 3. Migration Strategy

Additive only:
- `ALTER TABLE DocumentRevisions ADD ClientReviewStatus nvarchar(max) NULL, ClientReviewComment nvarchar(max) NULL, ClientReviewDate datetime2 NULL, ClientReviewLetterId int NULL`.
- FK `DocumentRevisions.ClientReviewLetterId -> Letters.LetterId`, `ON DELETE RESTRICT` (same cascade-safety convention used for every other new FK this session — never `Cascade`, to avoid SQL Server's multi-cascade-path failure and to avoid a letter deletion silently erasing client-review history).
- No column is dropped, renamed, or made non-nullable. No existing row's `Document.ClientApprovalStatus` value is touched by the migration itself (only future code paths change how it's kept in sync going forward) — validated by re-running the migration on a disposable repro database first, per this session's established discipline.
- **Backfill:** none required or performed. Existing `Document.ClientApprovalStatus` values (if any exist in the live database) are left exactly as-is; they simply become the "last known cache" going forward rather than being retroactively copied onto a specific historical `DocumentRevision` row (there is no reliable way to know *which* revision an existing client response belonged to without inventing data, which §2.3/§15 explicitly forbid — "do not use... unrecoverable transformation" / "do not infer... do not falsely mark"). This is documented as a known limitation, not silently glossed over.

## 4. Authorization — unchanged

`RecordClientResponseAsync` keeps its existing `IAccessControlService.AuthorizeForRolesAsync("Document", "RecordClientResponse")` gate exactly as-is. No new permission is introduced — recording a client response against a specific revision is the same authorized action as recording it against "the document" was before; only the storage target changes.

## 5. Audit — unchanged mechanism, more precise data

No new audit table. The existing generic `PEMSDbContext` change-tracker audit (used across the whole codebase, see `PEMSDbContextModelSnapshot` / `PrepareAuditEntries`) will now show the *revision's* `ClientReviewStatus` field changing (with the specific `DocumentRevisionId` as `EntityKeyValue`) instead of only the document's, which is strictly more precise than today, not a regression.

## 6. UI Change

`Details.cshtml`'s existing revision-history table (already present from Phase 1's revision-file-binding work) gains one additional column showing each revision's own client-review outcome, where set — so a user looking at Revision 00 sees its actual client decision, and Revision 01 correctly shows "Not yet submitted to client" rather than inheriting Revision 00's badge. No new page, no new navigation entry, no redesign — a single column added to an existing table, using the existing badge CSS classes already in that view.

## 7. Explicitly Deferred (with reason, not silently dropped)

| Item | Why deferred |
|---|---|
| Suitability codes | No business-confirmed code set exists in this project; master prompt explicitly forbids guessing standards codes (§8.8) |
| CDE state (WIP/Shared/Published/Archive) | No current consumer; would be a speculative field with no reader, violating §2.5 "no fake implementation" |
| Renaming `Document.Status`'s overloaded `"Rejected"` value (full Finding L2-02 fix) | Breaking change to the register filter, saved searches, and any external report keyed on the literal string; needs a dedicated additive-disambiguator design (e.g., a computed `RejectionSource` read-only property derived from the workflow instance's last action name) as its own reviewed change, not bundled into this pass |
| `IssuePurpose` deduplication (L2-03) | Needs a business decision on which of the two fields is authoritative; no active-harm evidence found that would justify guessing |
| Generalized `DocumentStateTransition`/history table | `WorkflowInstance`/`WorkflowInstanceStage` already serves this role for the internal path; no gap was found that a new table would close |
| Full `ClientReviewCycle`/multi-round client review | No evidence the business process has more than one round today; over-building this would be exactly the "invent architecture that conflicts with the current implementation" the master prompt forbids |

## 8. Rollback

If the migration needs to be rolled back: `DROP` the four new nullable columns and the new FK — no data loss beyond the new columns themselves, since nothing pre-existing was altered. `Document.ClientApprovalStatus` continues to work exactly as it did before this phase if the code changes are also reverted (it is still written by `RecordClientResponseAsync`, just no longer additionally cleared on new-revision creation).
