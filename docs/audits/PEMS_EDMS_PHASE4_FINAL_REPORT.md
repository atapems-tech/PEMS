# PEMS EDMS — Phase 4 Final Report

*Updated after an independent post-implementation verification pass — see §11 for what that pass found and fixed.*

## 1. Phase 4 Verdict
**PASS WITH FINDINGS** (updated from the initial pass's `PASS` — see §11)

## 2. Repository Baseline
- **HEAD (start of phase):** `ec75ddb` — Phase 3 commit, confirmed present both locally and on `origin/main` (verified via `git reflog show refs/remotes/origin/main`, which shows an actual `update by push` event, not just a stale local ref).
- **Branch:** `main`.
- **Working tree:** clean at audit start except the same pre-existing untracked production files this session has never touched.
- **Build state:** succeeded before any Phase 4 change.
- **Test baseline:** 1100/1100 (Phase 3's final number).
- **Migration state:** clean, no pending model changes; live PEMSDB at `20260911205849_AddTransmittalHistoricalSnapshotAndAcknowledgement`.

## 3. Audit Findings
Full evidence-based findings for all 10 required audit areas (A-J) are in `docs/audits/PEMS_EDMS_PHASE4_AUDIT.md`, including the explicit old-assumption-vs-current-evidence reconciliation table. Headline: a fully-built, fully-tested, database-seeded Separation-of-Duties enforcement mechanism (`AccessControlService.AuthorizeAsync` + `SoDPolicy`) existed with **zero real callers anywhere in the codebase**, and `EngineeringDocumentsController.Create`/`Edit` had **no server-side permission check at all**. A secondary finding: the AI Document Intelligence pipeline's own classification-stage tracking (`AIWorkflowStage`) was directly overwriting the formal engineering `Document.Status` field, bypassing the controlled workflow engine's preconditions entirely (though never yet exercised in production — 0 historical occurrences).

## 4. Phase 4 Implemented Scope
One coherent initiative, three code changes, **zero database schema changes** (all supporting tables — `SoDPolicy`, `AuthorizationAuditLog`, `RolePermission` — already existed and were already correctly seeded):

- **`PEMS.Web/Controllers/EngineeringDocumentsController.cs`**
  - `Create` (POST): added `IPermissionService.HasPermissionAsync(..., "EngineeringDocuments", "Create")` gate → `Forbid()` on failure; added a post-save `IAccessControlService.AuthorizeAsync(...)` call to log the action into `AuthorizationAuditLog` for future SoD detection.
  - `Edit` (GET + POST): added the equivalent `"Edit"` permission gate on both, plus the equivalent post-save `AuthorizeAsync` logging call on POST.
  - `Approve` (POST): added an `AuthorizeAsync(..., "Approve", resourceId: documentId)` call before the existing discipline-scope check; if the decision is `DeniedBySoD` (i.e., this exact user already logged a `Create` or `Edit` `Allow` on this exact document), the approval is rejected with a clear Persian error message, alongside — not replacing — the pre-existing discipline-scope check.
- **`PEMS.Persistence/Servises/EngineeringDocumentIntelligenceService.cs`**
  - `AdvanceStageAsync`: removed the `if (newStage == "Approved") document.Status = "Approved";` branch entirely. The method now only ever updates `AIWorkflowStage`.
- **Tests added:**
  - `PEMS.Tests/Authorization/EngineeringDocumentSelfApprovalTests.cs` (4 tests) — proves the **real production-seeded** `SoDPolicy` (via `AccessControlSeeder.SeedPhase5GovernanceAsync`, not a hand-built test analog) correctly blocks Create→Approve and Edit→Approve self-approval, allows a different user to approve, and allows the same user to approve an unrelated document.
  - `PEMS.Tests/Engineering/AIWorkflowStageAuthorityBoundaryTests.cs` (3 tests) — proves `AdvanceStageAsync` updates `AIWorkflowStage` but never touches `Document.Status`, including the stricter case where the document was already `Checked`.
- **Authorization:** all four new/modified checks are server-side, in the controller/service body, independent of any UI hiding.
- **Auditability:** no new audit table — every `AuthorizeAsync` call already writes to the existing `AuthorizationAuditLog` table via the pre-existing mechanism; this phase's only change is that this mechanism is now actually invoked from production code paths.
- **UI:** no view files changed — this phase is entirely server-side.
- **Database:** no migration — purely additive code-level wiring of tables that already existed and were already correctly seeded.

## 5. Explicitly NOT Implemented
- **G-27 (MDR completeness / Information Requirements):** deferred — no `InformationRequirement`/`DocumentRequirement` entity exists, and building one is a genuinely large, multi-entity feature that deserves its own dedicated phase, not a rushed addition here.
- **Area B (Engineering workspaces):** not rebuilt — a substantial `EngineeringDashboardController` + prior internal E05A-D phases already exist; re-auditing them to full depth was out of this phase's time budget, and building a competing workspace without first proving the existing one insufficient would violate the "no visually disconnected dashboard" rule.
- **Area C / G-10 (`Document.Status` "Rejected" ambiguity):** unchanged since Phase 2's own deferral — a good, well-scoped candidate for the *next* phase, deliberately not bundled into this one to keep Phase 4's diff small and its review surface tight.
- **Area E (Handover Package):** deferred — the master prompt explicitly forbids creating this until proven the current system cannot represent the lifecycle; that proof requires a full equipment↔document relationship trace not undertaken this phase.
- **Area F (Retention/Legal Hold/Disposition):** deferred — no current auto-deletion feature exists that would need retention gating; building it speculatively would itself be a "no speculative abstractions" violation.
- **G-23 (recipient identity), G-24 (import-path snapshot):** reaffirmed as not-a-gap / deferred respectively, unchanged from Phase 3's own conclusions — no new evidence this phase changes either verdict.

## 6. Database Verification
No migration was created or needed this phase. `dotnet ef migrations has-pending-model-changes` → clean, both before and after all code changes. Live PEMSDB unchanged (no schema touched, no rows touched — this phase's entire effect is new `AuthorizationAuditLog` rows that will be written going forward by real user actions, not by this session).

## 7. Test Results
```
Focused tests:   10 (4 EngineeringDocumentSelfApprovalTests + 3 AIWorkflowStageAuthorityBoundaryTests
                     + 3 DocumentWorkflowControllerApprovalAuthorizationTests, added during verification)
Full regression: 1110 / 1110 passed, 0 failed, 0 skipped (5m49s)
Build:           Succeeded, 0 errors (only pre-existing unrelated nullable warnings)
```
(1110 = 1100 Phase 3 baseline + 10 new — exact match, no unexplained delta. See §11 for the one test
failure encountered and resolved *during* verification, before this final clean run.)

## 8. Security Verification
- **Controller authorization:** `Create`/`Edit` now check `HasPermissionAsync` before any state mutation; `Approve` retains its pre-existing discipline-scope check (`CanApproveDocumentDisciplineAsync`) and gains the new SoD check — both must pass.
- **Service authorization:** `AccessControlService.AuthorizeAsync`'s `DecideForUserAsync` independently re-resolves the user's role grants from the database on every call — it does not trust any value passed in from the controller, so a direct service call (bypassing the controller) is equally protected.
- **SoD/self-approval:** verified by test — the exact production-seeded policy now denies a user approving a document they created or edited.
- **Live self-approval history check:** `SELECT COUNT(*) FROM EngineeringDocuments WHERE PreparedById = ApprovedById` → 0 — no retroactive cleanup was needed; this is a forward-looking governance fix.
- **Concurrency:** unchanged — `DocumentWorkflowService.ApproveAsync`'s existing `RowVersion`/`DbUpdateConcurrencyException` handling is untouched, sitting downstream of the new checks.
- **Direct-request bypass resistance:** confirmed structurally — the new checks live in the controller action body (for Create/Edit) and are evaluated before any database write; a raw POST cannot skip them any more than it could skip the pre-existing Freeze/Unfreeze checks in the same file, which use the identical pattern.

## 9. UI Verification
Not applicable this phase — zero view files were changed. No new page, no new UI element, nothing to verify for bilingual/RTL/accessibility/density, since nothing user-facing changed.

## 10. Remaining Gap Matrix
See `docs/audits/PEMS_EDMS_GAP_MATRIX.md` (updated) for the complete table. Summary of what remains open:

| ID | Status | Risk | Priority | Reason | Recommended Future Phase |
|---|---|---|---|---|---|
| G-10 | PARTIAL | Low | P2 | `Document.Status` "Rejected" still ambiguous between internal/client rejection | Phase 5 — well-scoped, low-risk, ready to implement |
| G-13 | PARTIAL | Medium | P2 | No project-level Handover Package | Needs a full equipment↔document relationship trace first |
| G-14 | OPEN | Low (rising with any future deletion feature) | P3 | No Retention/Legal Hold model | Build before any auto-archival feature is ever added |
| G-23 | OPEN (not a gap) | Low | P3 | Free-text recipients intentional for external parties | No action needed unless a concrete internal-recipient reporting need arises |
| G-24 | OPEN | Low | P3 | Import-path Transmittal items lack the G-21 snapshot | Only if evidence of material dispute risk emerges |
| G-27 | OPEN | Medium | P2 | No Information Requirement model — MDR completeness cannot answer "what's missing" | Dedicated future phase — large, multi-entity |
| G-28 | OPEN (found during verification) | Medium | P2 | `MDRImportService` can set `Document.Status="Approved"` directly from an import row, bypassing the workflow engine and SoD entirely | Needs a business decision on whether bulk import should ever be authoritative for Approved |

## 11. Post-Implementation Verification Pass

An independent re-verification traced every claim in this report against the code again, specifically hunting for alternate endpoints/paths that could undermine them. It found and fixed one real defect, and surfaced one real, separate, out-of-scope finding:

- **Defect found and fixed (P4-04):** `DocumentWorkflowController.Approve` — a second, live, UI-reachable action (`DocumentWorkflow/Index.cshtml:54` renders an "Approve" button for every "Checked" document, visible to every logged-in user with no role check) — called the identical `DocumentWorkflowService.ApproveAsync` as `EngineeringDocumentsController.Approve`, but had **zero authorization of any kind**: no permission check, no discipline-scope check, no SoD check. This directly undermined the "self-approval is now blocked" claim, since a user denied via one controller could simply use the other. Fixed with the identical permission+SoD pattern already used in P4-01/P4-02. Confirmed via `grep -rn "\.AuthorizeAsync(" PEMS.Web PEMS.Persistence` before the fix (matched only the two already-known call sites) and the live view file (`asp-action="Approve"` at `DocumentWorkflow/Index.cshtml:54`) confirming this was a real, reachable, not-dead-code path.
- **Test-writing note (transparency, not a defect):** the first version of the SoD-denial test for this fix (`Approve_SameUserWhoCreatedDocument_IsDeniedBySoD_DocumentRemainsUnapproved`) initially **failed** — not because the fix was wrong, but because the test's own seed helper never granted the simulated "Create" action's permission to the test role, so `AccessControlService` correctly logged it as `DeniedByRole` rather than `Allow`, and the SoD engine correctly found no qualifying prior *allowed* action to conflict with. This is exactly the SoD engine behaving precisely as designed (only counting genuinely-authorized prior actions) and was fixed by correcting the test's seed, not the production code. Disclosed here per the instruction not to fabricate or hide a test failure encountered along the way.
- **New, separate finding (G-28, not fixed):** `MDRImportService.ComputeStatusFromCommentStatus` (`MDRImportService.cs:1038-1047`) sets `Document.Status = "Approved"` directly when an imported Excel row's comment-status code is `"AP"`/`"NC"` — a *third* path to the same authoritative value, bypassing the workflow engine, SoD, and any per-document permission check. This is **not** a defect in Phase 4's own work (Phase 4 never claimed to cover bulk import), and is **not fixed** in this pass — it is a fundamentally different kind of operation (historical/bulk data synchronization from an external spreadsheet, not a live per-document governance decision) and closing it requires a business decision (should import ever be authoritative for "Approved"? should it require the same SoD?) that this session cannot make unilaterally. This mirrors the exact reasoning already used for G-24 in Phase 3 ("do not modify MDR/VDR import machinery without evidence of material risk").
- **Confirmed NOT reachable:** `IEngineeringDocumentService.AddAsync`/`UpdateAsync` (the methods `EngineeringDocumentsController.Create`/`Edit` call) have no other callers anywhere in `PEMS.Web` — grep-confirmed. `MDRImportService`/`IntakeService` create `EngineeringDocument` rows directly via `_context.EngineeringDocuments.Add(...)`, entirely bypassing both the controller and its permission/SoD-logging calls — a real, disclosed boundary (documented in the audit addendum), not a silent gap.
- **Confirmed NOT re-broken:** the minor pre-existing gap that `EngineeringDocumentsController.Submit`/`Check`/`Reject` (on *both* controllers) also lack any permission check is a wider, pre-existing pattern predating Phase 4 — outside this phase's specific SoD/Approve/AI-boundary claim, and not touched.

## Recommended Commit Message
```
feat(edms): wire self-approval SoD enforcement and close document authorization gaps
```

Per instruction, **no commit has been made and nothing has been pushed** — this message is provided for review.
