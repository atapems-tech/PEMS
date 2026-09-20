# PEMS Workflow Platform — Phase 2 Closure Audit (Work Request)

**Final classification: ACCEPTED WITH DOCUMENTED LIMITATIONS** (§22).

Scope: Work Request only. No other module was touched, migrated or redesigned; no Workflow Designer; no new engine.

## 1. Executive summary

The audit re-read the Phase 1/Phase 2 documents, inspected the real code, re-ran the build and the full suite, and — for the first time — exercised the Work Request pages in a real browser (headless Chrome driven over CDP against the real Kestrel application and a real SQL Server database, EN/LTR and FA/RTL, 1440/820/390 px).

Result: the architecture claims hold. Work Request submits and progresses through the central runtime via an explicit binding; UI structure, actions, participants and history come from the read model; a "Reliability Review" stage published by configuration appeared for new requests only, while in-flight requests stayed on version 1 — demonstrated in the browser against a running application that was **not** rebuilt or restarted between publishing version 2 and using it.

The audit also found and fixed 9 defects (§19–20), none architectural: a leftover duplicate state map, a heuristic "newest instance" lookup, Persian text leaking into the English UI (and vice-versa), a stale hard-coded workflow chain on the list page, a misleading cancelled-stage display, and a resource-key collision.

## 2. Build / test gate

| Item | Result |
|---|---|
| `dotnet build PEMS.slnx --configuration Debug` | 0 errors (3 pre-existing warnings) |
| Full suite before the closure fixes (Phase 2 baseline) | 1695 passed / 0 failed / 0 skipped |
| Full suite after the closure fixes | **1698 passed / 0 failed / 0 skipped** (1695 + 3 new tests) |

Test-assertion audit:
- `HelpContentSeederTests.SeedAsync_TotalArticleCount_MatchesPhase1Through4`: `Assert.Equal` 55 → 56 in Phase 2 (new Help article `work-request-workflow`). Against git `HEAD` the value reads 54 → 56 because an earlier, still-uncommitted UI phase had already raised it to 55 (`ui-conventions`). This is the **only** pre-existing assertion that changed in Phase 2.
- The four Phase 1 fixtures (`HelpWorkflowDiagramServiceTests` ×3, `HelpVisualServiceTests` ×1): diff re-inspected — only setup lines changed (create as Draft, then set Published); no `Assert` line added, removed or edited. (An encoding side effect — a BOM on line 1 — was reverted during this audit.)
- No Work Request/workflow assertion was weakened. Phase 2's own new tests were adjusted in this audit only to accept the (now bilingual) message text.

Side effect to be aware of: some tests boot the real application (`WebApplicationFactory`) against the configured local database, which runs the startup seeders. `PEMSDB` therefore now contains the Work Request definition + binding (1 definition, 1 binding, **0 workflow instances**; its 2 existing Work Requests have no instance). This is exactly what the first real application start would have done.

## 3. Remaining state-machine audit

Every status/workflow decision left in the Work Request code, classified (A = workflow orchestration → must be platform; B = Work Request business rule; C = legacy compatibility; D = duplicate logic → remove):

| Location | Logic | Class | Disposition |
|---|---|---|---|
| `WorkRequestService.LegalTransitions` | legacy transition table | C | kept; used only when the request has **no** instance; documented in code |
| `WorkRequestService.LegacyNextStatus` / `GetLegacyNextStatus` | next status for legacy records | C | now the **only** legacy map (was duplicated in the controller = D, removed) |
| `WorkRequestService.AdvanceStatusAsync` | applies the legacy map | C | refuses instance-governed requests and Draft→Submitted while a binding exists |
| `SubmitAsync` `wr.Status != "Draft"` | Draft is outside the workflow | B | kept |
| `StageBusinessChangeAsync` (`UnderReview`→Reviewed*, `Approved`→Approved*, `Rejected`→reason) | Work Request business fields keyed by the projected business status | B | kept |
| `ResolveNewStatusAsync` | stage `BusinessStatus` (configuration) → status; terminal `Reject/Cancel/Complete` → `Rejected/Cancelled/Converted` | B/C | the mapping of platform outcomes to the legacy `Status` column that dashboards read; documented |
| `ConvertToWorkOrderAsync` `Status != "Planned"`, equipment required, once only | conversion rule | B | kept; additionally requires the workflow `Complete` action to be *allowed* (platform) |
| `ExecuteWorkflowActionAsync` `actionType == "Complete"` refused | conversion is a business action | B | kept |
| Controller `IsLegacyWithoutWorkflow` (`Status != Draft`, not terminal, no instance) | classify legacy records for the view | C | kept |
| `Details.cshtml` legacy `@if (Model.Status …)` blocks | old buttons for instance-less records | C | isolated inside the `IsLegacyWithoutWorkflow` branch only |
| `Details.cshtml` badge colour by `Model.Status`, rejection-reason alert | presentation of business status | B | kept |
| `Index.cshtml` status filter buttons and status pills | business-status filter (a stage that maps to `UnderReview` stays filterable) | B/C | kept |
| **`Index.cshtml` paragraph "Draft → Submitted → UnderReview → Approved → Planned → WorkOrder"** | hard-coded description of the workflow | **D** | **removed** (would have gone stale with version 2) |
| **Controller `NextStatusMap`** | duplicate of the service's legacy map | **D** | **removed**; controller asks `GetLegacyNextStatus` and only for legacy records |

No category-A logic remains outside the platform.

## 4. Controller audit (answers to the six questions)

`NextStatusMap` was used in two places: `Details` (to pick a "next status" button) and the legacy `AdvanceStatus` action.
1. Only legacy compatibility? — Yes for governed requests it was dead data (the view ignores it when a workflow view exists), but it also existed for legacy ones.
2/3/4. Did it determine transitions, availability or progression? — For **legacy records** yes (that *is* the legacy machine); for workflow-governed requests no.
5. Did it bypass `WorkflowRuntimeService`? — It could not change a governed request: `AdvanceStatusAsync` refuses instance-governed records (unit test `Legacy_status_path_cannot_be_used_to_bypass…`; not re-run through the browser).
6. Could it go stale / make UI and runtime disagree? — The map itself could not disagree for governed requests, but it was a second copy of the legacy table (drift risk with `LegalTransitions`).

Fix: the map is deleted; the single legacy map lives in the service; the controller computes the legacy next status only when `IsLegacyWithoutWorkflow`. A test asserts the controller has no private status dictionary.

## 5. Binding audit

Verified in code, unit tests and the real database: `Module=WorkRequest`, `EntityType=WorkRequest`, `Context=Standard`, definition "Work Request Approval", pinned published version, `IsActive=true` (real SQL Server, seeded at real application start). Missing / inactive / ambiguous binding → `SubmitAsync` returns a clear error and changes nothing (tests). `AdvanceStatusAsync(Draft→Submitted)` is refused while a binding exists (no silent legacy fallback). The runtime's legacy path (Module+Category lookup) is not reachable from Work Request submit. No "first active workflow" lookup exists in the Work Request path (`WorkflowRuntimeService` legacy fallback throws on more than one candidate).

## 6. Workflow-instance association

Association is the explicit key `WorkflowInstance.EntityKey = "WorkRequest|WorkRequest|{id}"` plus `WorkflowBindingId`/`WorkflowVersionId` on the instance. The Phase 2 code resolved the instance with "Running first, else newest" — a heuristic. **Fixed**: `CurrentInstanceAsync` now returns the single instance or, if more than one exists, fails with an explicit error (no guessing) in submit/execute/convert/view. The invariant "at most one instance per Work Request" is protected by (a) `SubmitAsync` refusing when any instance exists and (b) `WorkRequest` inherits a `RowVersion` concurrency token, so two simultaneous submits should make the second `SaveChanges` fail (now reported as a friendly message). This last point follows from the token and the single atomic save; it was **not** exercised with real concurrent requests (InMemory tests and sequential browser runs cannot show it). No new column/relationship was added.

## 7. Dynamic workflow proof (repeated, in a real browser)

Setup (real app on port 5099, isolated database `PEMS_QA_TMP`, migrated with the real migrations, seeded by the real seeders): version 1 = Submitted → Under Review → Approved → Planned, bound.
1. Request X created, submitted, moved to Under Review through the UI → instance pinned to v1.
2. Version 2 published **by configuration only** (Phase 1 services: clone v1 → add stage "Reliability Review" (order 25, `BusinessStatus=UnderReview`, Persian name) → replace the Under Review "Approve" edge → validate → publish → re-pin binding). The running web application was not rebuilt or restarted.
3. Request Y created and submitted → v2. Browser results (EN and FA): timeline shows 5 stages in order with Reliability Review third; reviewer sees the v2 action "Send to Reliability → Reliability Review"; after it, current stage = Reliability Review, business status stays `UnderReview`, actions "Approve Reliability", Reject, Cancel; participants line shows the permissions; history records Under Review → Reliability Review.
4. Request X (v1): still 4 stages, header "Version 1", actions unchanged, "Approve" goes straight to Approved.
Database check: instances `Completed v1: 2`, `Cancelled v1: 2`, `Running v1: 1`, `Cancelled v2: 2`, `Running v2: 4`.

## 8. Version isolation proof
Same run: A/X on v1 never shows the new stage before or after v2 is published; v1 stays immutable (4 stages, 11 transitions; guard tests); new requests use v2 through the re-pinned binding; publishing v2 *without* re-pinning does not affect new requests (test).

## 9. Evidence that no UI code was added for Reliability Review
- `grep` for "Reliability" in `WorkRequest/Details.cshtml`, `WorkRequest/Index.cshtml`, `WorkRequestController.cs`, `WorkRequestService.cs`, and all of `PEMS.WorkflowPlatform`: **0 occurrences** (other "Reliability" hits are the unrelated Reliability module views).
- Test `Details_view_contains_no_hard_coded_workflow_stage_names` asserts the view (and now the list view) contains none of the stage names / the workflow chain, and does contain `wf.Stages` / `wf?.AvailableActions`.
- The application process that served the v2 requests had been started before v2 existed (§7).

## 10. Browser QA (method and coverage)
Real Chrome (headless) over CDP against the running application; users `qa_admin` (SystemAdmin), `qa_req` (View+Create), `qa_rev` (View+Create+Approve), `qa_viewer` (View); real login form, real form posts, real anti-forgery tokens. Pages: list, create, details in every state (Draft, Submitted, Under Review as requester and as reviewer, Reliability Review, Planned, Converted, Cancelled, legacy), workflow history, business audit, toasts. No JavaScript exceptions were captured in any run that logged them (Details flows in EN and FA, legacy, dynamic-version runs).

## 11. RTL (Persian) QA
`dir=rtl`, `lang=fa`; no horizontal page overflow at 1440/820/390 px; timeline runs right-to-left (first stage at the right); stage names, action labels, history stage/action names and the "Workflow"/"Version"/"Running" wording come from data or resources in Persian; inputs, toasts, and buttons mirror correctly. Defects found and fixed: history stage/action names and action-target stage in English; English header labels ("Requested by", "Asset", …); permission code and date order scrambled by bidi (fixed with `<bdi>` / isolates); "To" table header. Remaining Persian-mode English (by design/known): the business status value (`UnderReview`, `Approved`, …) in the header badge, list pills and Business Audit table; the workflow definition **name** "Work Request Approval" (data); "N/A" placeholders; the list's status-filter buttons, priority pills and column formatting (untouched by Phase 2); tag/name data.

## 12. LTR (English) QA
Same checks in `dir=ltr`. No Persian text nodes in visible content (a Persian comment inside the pre-existing shared stage-diagram `<style>` block is not rendered). Defects found and fixed: Persian error/success toasts and the Persian disabled-action reason (from `AccessControlService`) appearing in the English UI.

## 13. Action / security QA (browser)
- Requester (Create only) at Under Review: Approve and Reject rendered **disabled**, Cancel enabled; the reason is shown inline ("Permission "WorkRequest.Approve" is required." / Persian equivalent), not only as a tooltip.
- **Forged request**: the same requester posted `WorkflowAction` with `actionType=Approve` and a valid anti-forgery token straight from the page (`fetch`). Server response: error toast; request remained `UnderReview`; the forged comment appears nowhere in workflow history or audit; instance still Running. (Unit test additionally forges a participant pseudo-role directly against the runtime and is rejected by the hook.)
- Reviewer: Approve → Approved → Plan → Planned all executed and recorded (history rows, business status, Reviewed/Approved fields).
- Legacy-path call against a governed request: rejected in the unit test; not exercised through the browser.

## 14. Cancel QA
Cancel from Submitted and from Reliability Review (both languages): instance `Cancelled`, request business status `Cancelled`, history has the `Cancel` row, no actions remain. **Defect fixed**: the stage where the request stopped was shown as a completed (green) stage; the read model now reports it as `Cancelled` and the page shows the red stop marker on that stage with later stages pending. Remaining ambiguity, documented: two vocabularies exist — workflow instance status (`Cancelled`) and the Work Request `Status` column (`Cancelled`); they agree for Cancel, but for the legacy column the `Complete` outcome is projected as `Converted` and `Reject` as `Rejected`; the mapping lives in `ResolveNewStatusAsync`.

## 15. Conversion QA
Reviewer at Planned: the Convert form (business action, Work Order type dropdown) is shown only there; submitting it created the Work Order, set `Converted` + `LinkedWorkOrderId`, wrote the audit row, and completed the workflow instance (history `Complete` with the Work Order number) — atomically (single `SaveChanges`; unit tests cover the rollback cases: missing equipment leaves Planned/Running and creates no Work Order; a user without Approve is refused). In the QA run the redirect to the Work Order page landed on Access Denied because the QA reviewer role has no `WorkOrder.View` — a QA-role artefact, the conversion itself was verified. Two Work Orders with `SourceWorkRequestId` exist in the QA database.

## 16. Legacy request QA
Record `WR-LEGACY-0001` (status Under Review, no instance): shows the "Legacy record - no workflow instance" badge (EN/FA), **no timeline**, legacy buttons (Advance / Reject / Cancel Request); "Advance" worked (→ Approved) and afterwards it still had no instance and no timeline. Database: 0 instances for that request. New requests always use the binding. Limitation unchanged: legacy records finish on the old path; there is no backfill (deliberately, per scope).

## 17. Participant model
Participants are **permission-based pseudo-roles** ("Permission: WorkRequest.Create / WorkRequest.Approve"), not persons or teams. The UI now labels them explicitly with the word "Permission" (Persian "مجوز") and isolates the code; it never presents a permission as a person. Per-person/team assignment is not implemented (known limitation).

## 18. Database / migration audit
`20260919234615_WorkflowPlatformPhase2StageBusinessMapping`: `Up` = 4 nullable `AddColumn` (`WorkflowStages.NameFa`, `.BusinessStatus`; `WorkflowTransitions.Label`, `.LabelFa`); `Down` = the same 4 `DropColumn`; no table drop, no data change, no Work Request table touched. Verified up→down→up on a disposable database (earlier) and again by creating a fresh database from all migrations for the QA run. Local `PEMSDB` is at this migration (`__EFMigrationsHistory` head). `PEMSDB` was not reset; no business data was deleted; the QA database `PEMS_QA_TMP` was created and dropped by this audit.

## 19. Help documentation
`work-request-workflow` (English + Persian) exists in `HelpContentSeeder`, is linked to `WorkRequests.Details` (the Details page now sets that help key), is created in the QA database at first start, and was re-read against behaviour: statements about the timeline, disabled-but-rechecked actions, versions and legacy records match the verified behaviour. `WORKFLOW_PLATFORM_PHASE2_WORK_REQUEST.md` was corrected (map location, test count, browser verification, cancelled-stage behaviour).

## 20. Defects found and fixes made

| # | File(s) | Problem | Change | Reason | Verification |
|---|---|---|---|---|---|
| 1 | `WorkRequestController.cs`, `WorkRequestService.cs`, `IWorkRequestService.cs` | Duplicate legacy status map in the controller (D) | Removed `NextStatusMap`; single `GetLegacyNextStatus` in the service; used only for legacy records | No second copy of workflow logic | test `Controller_has_no_private_status_map`, `Legacy_next_status_map_…` |
| 2 | `WorkRequestService.cs` | Instance found via "Running first, else newest" | Strict single-instance lookup; >1 = explicit error; view also uses it; concurrency exceptions become friendly errors | Deterministic association | test `More_than_one_instance_…`; browser flows |
| 3 | `WorkflowPlatformAdapters.cs` | Disabled-action reason / auth error was the internal Persian `AccessControlService` text even in English UI | Short culture-aware reason ("Permission "X" is required.") | English UI purity; meaningful reason | browser EN/FA reason text |
| 4 | `WorkRequestService.cs`, `WorkRequestController.cs` | New workflow error/success messages hard-coded in Persian (toasts) | Culture-aware messages for the workflow paths and the controller's messages | Bilingual UI | browser: EN toasts English, FA toasts Persian; tests accept either language |
| 5 | `Details.cshtml`, resources | History/target stage names and action names English in Persian UI; English header labels; scrambled permission code/dates/tag in RTL; "To" header | Stage name via `NameFa`, actions/labels via resources, `<bdi>`/isolates, new keys | RTL/LTR QA | screenshots + text scans |
| 6 | `Details.cshtml`, `Index.cshtml` | In-page TempData alerts duplicated the layout toast; participant text unlabeled; denied reason only in a tooltip | Removed duplicates; "Permission:" prefix; inline denied-reason lines | Minimal UI, clarity | screenshots |
| 7 | `WorkflowReadModelService.cs`, `Details.cshtml` | Cancelled stage displayed as completed (green) | New stage status `Cancelled` (red stop marker), later stages pending | Cancel QA | test in `Reject_and_cancel…`; browser |
| 8 | `Index.cshtml` | Static text "Draft → Submitted → … → WorkOrder" hard-coded the workflow and would be stale with version 2 | Removed; "New Request" localized | No hard-coded workflow in UI | view guard test |
| 9 | `SharedResource(.fa).resx` | My new key "Requested by" collided (case-insensitively) with the existing "Requested By", so the Persian value was dropped at build time | Removed the duplicate; used the existing key | Correct Persian text | satellite assembly inspected; browser |

## 21. Remaining limitations

- **Architectural blockers: none.**
- **Functional limitations**: legacy in-flight requests (no instance) remain on the old path with no backfill; participants are permission-based (no per-person/team assignment); no return action, SLA execution, escalation execution or notifications; the mapping between platform outcomes and the legacy `Status` column (§14) lives in Work Request code; a stage that has no `BusinessStatus` keeps the current status; workflow "Action" column shows the action type (e.g. "Approve") rather than the transition label.
- **UI limitations**: raw business status values (`UnderReview`, `Approved`, …) and English workflow-definition names in Persian mode; other untouched list-page literals (filter buttons, pills, "N/A"); on a 390 px phone the stage timeline and the history table scroll horizontally inside their own cards (page itself has no overflow); toasts still come from the layout.
- **Future enhancements**: backfill/adoption of legacy records (needs a "start at stage" runtime capability); per-user assignment; localized status labels; showing transition labels in history; a DB-level guarantee for "one instance per request" if other modules adopt the platform.
- **Test-environment note**: the full suite starts the real app against the local database, so `PEMSDB` gets the Work Request definition/binding without anyone starting the app.

## 22. Final classification

**ACCEPTED WITH DOCUMENTED LIMITATIONS.** Every acceptance criterion in the closure charter is met; the limitations above are functional/UI gaps that are documented, none of which weakens the reference-implementation claim. Checklist: architecture ✔, code ✔ (D-class duplicates removed), dynamic behaviour ✔ (browser-proven), security ✔, browser RTL/LTR ✔, data ✔, quality ✔ (1698/0/0, no assertion weakened).

## 23. Exact files changed in this closure audit

`PEMS.Persistence/Servises/WorkRequestService.cs`, `PEMS.Application/Interfaces/IWorkRequestService.cs`, `PEMS.Web/Controllers/WorkRequestController.cs`, `PEMS.Web/Views/WorkRequest/Details.cshtml`, `PEMS.Web/Views/WorkRequest/Index.cshtml`, `PEMS.Persistence/Workflow/WorkflowPlatformAdapters.cs`, `PEMS.WorkflowPlatform/Services/WorkflowReadModelService.cs`, `PEMS.WorkflowPlatform/Interfaces/WorkflowPlatformContracts.cs` (comment), `PEMS.Web/Resources/SharedResource.resx`, `PEMS.Web/Resources/SharedResource.fa.resx`, `PEMS.Tests/Cmms/WorkRequestWorkflowTests.cs` (+3 tests, message assertions accept both languages), `PEMS.Tests/Help/HelpWorkflowDiagramServiceTests.cs` and `HelpVisualServiceTests.cs` (BOM/indentation revert only), `PEMS.Persistence/Context/PEMSDbContext.cs` (BOM restored), `docs/workflow/WORKFLOW_PLATFORM_PHASE2_WORK_REQUEST.md`, this document. Temporary QA harnesses were built outside the repository's test project and removed; the QA database was dropped.

## 24. Recommended next step

Do not start another module yet. Suggested order for the next authorized phase: (1) commit this work as reviewed; (2) decide policy for legacy in-flight Work Requests (drain vs. an adoption capability); (3) then choose the next module — `WorkOrder / MultiDisciplineReview` remains the lowest-risk candidate (already on the platform, parallel participants). Waiting for separate authorization.
