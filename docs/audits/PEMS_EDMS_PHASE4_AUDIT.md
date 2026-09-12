# PEMS EDMS — Phase 4 Audit: Post-Phase-3 Reconciliation & Governance Wiring

**Repository:** `PEMS-cmms-edit-empoleeys-master` · **Branch:** `main` · **Baseline commit:** `ec75ddb` (Phase 3, confirmed present both locally and on `origin/main` via `git reflog show refs/remotes/origin/main`)
**Method:** Direct file inspection plus live-database spot-checks. Every claim cites an exact file/line or an executed query. Old-assumption vs. current-evidence is called out explicitly per the mandatory instruction.

> **Addendum (post-implementation verification pass):** An independent re-verification of this phase's own claims found a second, real defect the initial pass missed — `DocumentWorkflowController.Approve` (a separate, UI-reachable action from `EngineeringDocumentsController.Approve`) had zero authorization of any kind and called the same `DocumentWorkflowService.ApproveAsync`. This has been fixed with the identical pattern (permission + SoD check) — see Finding P4-04 and the Phase 4 Final Report's verification section for full evidence. The verification pass also surfaced a **new, separate, out-of-scope finding**: `MDRImportService` can set `Document.Status = "Approved"` directly from an imported Excel row's comment-status code, bypassing the workflow engine entirely — documented as G-28, deliberately not fixed here (same "do not touch MDR import machinery" boundary already established for G-24).

---

## 0. Headline Finding

The single most valuable, evidence-backed finding this phase is **not** a missing feature — it is a **fully-built, fully-tested, but completely disconnected governance mechanism**. `AccessControlService.AuthorizeAsync` (the user-aware, Separation-of-Duties-enforcing authorization path) and its seeded policy `SoDPolicy: "EngineeringDocuments.Create/Edit conflicts with EngineeringDocuments.Approve"` have existed in the codebase since an earlier RBAC-ABAC phase, are covered by 14 passing unit tests (`AccessControlScopeAndSoDTests.cs`), and are seeded into the live database (`AccessControlSeeder.SeedPhase5GovernanceAsync`) — **yet had zero real callers anywhere in the production codebase.** A repo-wide grep for `.AuthorizeAsync(` outside test files returned no matches before this phase. The method's own doc comment admits this directly: *"مسیری که در فاز ۱ هیچ فراخوان واقعی نداشت"* ("a path that had no real caller in Phase 1").

Compounding this, a direct trace of `EngineeringDocumentsController.Create`/`Edit` found **no server-side permission check of any kind** on either action — any authenticated PEMS user, regardless of role, could create or edit any engineering document. This is not a hypothesis carried from an old report; it is a fresh read of the current file with zero `HasPermissionAsync`/`AuthorizeAsync` calls present in either method body.

## 1. Repository Baseline (§4/§28.2)

- **HEAD:** `ec75ddb` — `feat(edms): preserve transmittal revision and acknowledgement integrity`
- **Branch:** `main`
- **Phase 3 commit presence:** confirmed both locally (`git log --oneline -1`) and on `origin/main` (`git log --oneline origin/main -3` shows the same hash; `git reflog show refs/remotes/origin/main` confirms an actual `update by push` event at `2026-09-12 01:10:57`, i.e. it was genuinely pushed, not merely a stale local ref).
- **Working tree at audit start:** clean except the same pre-existing untracked production files this session has never touched (`filestore/*`, `wwwroot/uploads/*`) and one unrelated `.claude/Role/` file.
- **Build state:** `dotnet build PEMS.slnx --configuration Debug` → Build succeeded, 0 errors, prior to any Phase 4 change.
- **Migration state:** `dotnet ef migrations has-pending-model-changes` → clean; live PEMSDB's last applied migration is `20260911205849_AddTransmittalHistoricalSnapshotAndAcknowledgement` (Phase 3).
- **Test baseline:** 1100/1100 passing (Phase 3's final number), reconfirmed at the start of this phase.

## 2. Audit Findings By Area

### Area A — MDR Completeness & Register
**Old assumption (implicit in the master prompt):** MDR completeness reporting might be substantially built.
**Current evidence:** `find . -iname "*MDR*"` shows the only "MDR" code is `MDRImportService`/`MDRImportController` — a **staging-and-confirm Excel import tool**, not a requirements-driven register. `EngineeringDocumentListFilter` (`PEMS.Application/DTOs/EngineeringDocumentListFilter.cs`) supports `Search/ProjectId/DisciplineId/Status/Area/MinRevisionCount` but has **no** `OverdueOnly` filter — its own code comment explicitly says this was deliberately deferred to "a different phase." A repo-wide search for `InformationRequirement`/`DocumentRequirement`/`DeliverablePlan`/`MIDP`/`TIDP` returns zero entities anywhere in `PEMS.Domain`.
**Gap:** The system can report on documents that **exist** (status, dates, revisions — via `Document.PlannedDate`/`ActualDate`/computed `DelayDays`), but cannot answer "what SHOULD exist but doesn't," because no Information/Document Requirement concept exists at all.
**Decision:** **VERIFIED OPEN, but out of Phase 4 scope.** Building a requirements-driven MDR is a genuinely large, multi-entity feature (exactly the "Information Requirement/Deliverable Planning" scope the master prompt itself flags as high-effort) with no existing partial implementation to extend safely in one pass. Recommended for its own dedicated future phase, not squeezed into Phase 4's "smallest complete solution" discipline.

### Area B — Engineering Workspaces & Navigation
**Old assumption:** Dedicated work-queues ("My Discipline," "Pending Review," etc.) might be missing or buried in Details pages.
**Current evidence:** A dedicated `EngineeringDashboardController.cs` + `EngineeringDashboardViewModel.cs` already exist, referenced by prior internal phase docs (`.claude/Role/PEMS_ENGINEERING_DASHBOARD_E05A_DATA_CONTRACT.md` through `E05D_IMPLEMENTATION.md`) — a substantial, already-completed multi-phase effort. `EngineeringDocumentIntelligenceController.Index` also already provides a "pending AI review" queue (`GetPendingReviewAsync`).
**Decision:** **VERIFIED PARTIAL — pre-existing, not re-audited to full depth this session** given time constraints and the master prompt's own instruction to avoid low-value re-verification of areas with clear existing investment. No new workspace was built; building a competing one without fully auditing the existing E05A-D dashboard first would risk exactly the "visually disconnected dashboard" anti-pattern the master prompt forbids. Recommended: a focused re-audit of the existing dashboard's actual query coverage before any new workspace work, in a future phase.

### Area C — Client Review vs. Internal Review Status Semantics
**Old assumption (carried from Phase 2's own Finding L2-02):** `Document.Status` reuses `"Rejected"` for both an internal reviewer's rejection and a client's rejection, and mixes `"Client Approved"` into the same value set as workflow stages.
**Current evidence:** Re-confirmed unchanged — `DocumentWorkflowService.RejectAsync` and `RecordClientResponseAsync` both still write `"Rejected"` to `Document.Status` for two different real-world events. Phase 2 already designed (but explicitly deferred) a non-breaking fix: a computed/derived disambiguator rather than renaming the persisted value.
**Decision:** **VERIFIED OPEN, unchanged since Phase 2.** Not selected for Phase 4 implementation — after finding the far higher-severity Area D/G issues (below), and given Phase 4's own "max 1-4 initiatives" discipline, this is documented as the top candidate for the *next* phase rather than diluting this phase's focus.

### Area D — SoD / Self-Approval — **THE HEADLINE FINDING**
**Old assumption:** Unknown/unverified — the master prompt's own framing treated this as an open question requiring investigation.
**Current evidence (exhaustively traced, not assumed):**
- `PEMS.Domain/Entities/SoDPolicy.cs` — a fully-modeled, data-driven "Permission A conflicts with Permission B" entity, with a doc comment giving the *exact* self-approval example as its own design rationale.
- `PEMS.Persistence/Seed/AccessControlSeeder.cs:81-82` — the real production seed already contains `EngineeringDocuments.Create ↔ EngineeringDocuments.Approve` and `EngineeringDocuments.Edit ↔ EngineeringDocuments.Approve` as active `SoDPolicy` rows.
- `PEMS.Persistence/Servises/AccessControlService.cs:39-59,109-136` (`AuthorizeAsync`/`DecideForUserAsync`) — a complete, working implementation: on every call it writes an `AuthorizationAuditLog` row, and for any resource-scoped request it checks whether the same user already has a prior `Allow` log entry for a conflicting permission on the *same* `ResourceId`, denying with `AccessDecisionReason.DeniedBySoD` if so.
- `PEMS.Tests/Authorization/AccessControlScopeAndSoDTests.cs` — 14 tests proving this mechanism works correctly in isolation (using a hand-built `"MaterialRequisition"` example, not the real `"EngineeringDocuments"` seed).
- **The gap:** `grep -rn "\.AuthorizeAsync(" PEMS.Web PEMS.Persistence` (excluding tests) returned **zero matches**. `EngineeringDocumentsController.Approve` only calls a private `CanApproveDocumentDisciplineAsync` method that checks discipline membership via **hardcoded role name comparisons** (`SystemRoles.SystemAdmin`/`EngineeringManager`/`EngineeringSupervisor`), never consulting `SoDPolicy` at all.
- **Compounding gap:** `EngineeringDocumentsController.Create` (POST, line ~230) and `Edit` (GET+POST, line ~450+) have **no permission check whatsoever** — confirmed by reading both method bodies in full; the only permission check anywhere near `Create` is for the unrelated `"ManualNumber"` action.
- **Live-data check:** `SELECT COUNT(*) FROM EngineeringDocuments WHERE PreparedById IS NOT NULL AND PreparedById = ApprovedById` → **0**. No historical self-approval has occurred — the org's actual usage has been disciplined so far, but the *system* provided no enforcement, only luck.
- **Seeded permission check:** `RolePermissions` for module `EngineeringDocuments` already correctly grants `CanCreate`/`CanEdit` to `DisciplineEngineer`/`DocumentController`/`EngineeringManager`/`EngineeringSupervisor`, and `CanApprove` only to `EngineeringManager`/`EngineeringSupervisor` — the *policy design* was already correct in the database; only the *controller wiring* was missing.
**Decision:** **VERIFIED OPEN — IMPLEMENTED THIS PHASE (P4-01, P4-02).** Textbook "reuse, don't rebuild": zero new tables, zero new services, zero new tests infrastructure — only wiring already-tested, already-seeded infrastructure into its intended callers for the first time.

### Area E — Handover
**Old assumption (Phase 0's G-13):** `ManufacturerDataBook` covers vendor-side final documentation; no project-level `HandoverPackage` exists.
**Current evidence:** Re-confirmed unchanged this phase (no new Handover-related entity found).
**Decision:** **VERIFIED OPEN, unchanged.** Per the master prompt's own explicit instruction ("do NOT automatically create HandoverPackage until you prove the current system cannot represent this lifecycle" — and building the proof requires a full trace of equipment↔document↔vendor-doc relationships that was not undertaken this phase given the Area D finding's priority), this is **not** implemented and is recommended as a dedicated future-phase investigation, not a Phase 4 deliverable.

### Area F — Retention / Legal Hold / Disposition
**Old assumption (Phase 0's G-14):** Fully absent.
**Current evidence:** `grep -r "RetentionPolicy|LegalHold|Disposition" PEMS.Domain` → one unrelated hit in `NonConformanceReport.cs`; still genuinely absent.
**Decision:** **VERIFIED OPEN, unchanged.** The master prompt's own critical rule ("do NOT implement deletion/disposition automation merely because retention is absent... there must be no irreversible deletion in this phase unless separately justified") combined with the absence of any current auto-deletion feature that would need retention gating means there is no urgent trigger. Deferred, as instructed.

### Area G — AI Authority Boundaries — **SECOND FINDING, IMPLEMENTED**
**Old assumption:** Unknown/unverified.
**Current evidence:** `PEMS.AI` itself contains no authoritative-state writes at all (clean). However, `PEMS.Persistence/Servises/EngineeringDocumentIntelligenceService.cs:225-240` (`AdvanceStageAsync`) — reachable via `EngineeringDocumentIntelligenceController.AdvanceStage`, itself correctly gated by `HasPermissionAsync(..., "EngineeringDocuments", "Approve")` — previously set `document.Status = "Approved"` directly whenever the *AI intake pipeline's own, separate* `AIWorkflowStage` (a 9-stage classification pipeline: `Uploaded→...→Approved→KnowledgeBase`) reached its own terminal `"Approved"` value. This is a **second, independent code path** capable of writing the exact same authoritative field as `DocumentWorkflowService.ApproveAsync`, with none of that method's guarantees: no `Status != "Checked"` precondition, no discipline-scope check, no `IWorkflowRuntime` state-machine validation, no `WorkflowInstance` transition record.
**Important nuance:** this is *not* "AI silently approving documents unsupervised" — a human with the correct `Approve` permission must click through the AI Intelligence Review UI to trigger it. But it is a genuine authority-boundary violation in spirit: reaching "AI classification confirmed" and reaching "formally engineering-approved per ISO 9001 Prepared/Checked/Approved" are different real-world events that were writing to the same field through two inconsistent paths.
**Live-data check:** `SELECT COUNT(*) FROM Documents WHERE AIWorkflowStage = 'Approved'` → **0**. This path has never actually fired in production — zero current-data impact from fixing it.
**Decision:** **VERIFIED OPEN — IMPLEMENTED THIS PHASE (P4-03).**

### Area H — TransmittalLinker / MDR-VDR Import Historical Integrity (G-24 revisited)
**Current evidence:** `SELECT COUNT(*) FROM TransmittalItems WHERE DocumentRevisionId IS NOT NULL` → 0 of 7,290 (expected — every existing row predates the Phase 3 migration entirely, so this query cannot distinguish "came from `TransmittalLinker`" from "came from the main service before Phase 3"). No dispute-history or production complaint evidence was found suggesting material risk from the import path's narrower snapshot.
**Decision:** **VERIFIED OPEN, unchanged from Phase 3's own conclusion.** Per the master prompt's explicit instruction ("do not modify the MDR/VDR architecture merely to close G-24... only implement if evidence supports material risk"), no such evidence was found. Deferred.

### Area I — Transmittal Recipient Identity (G-23 revisited)
**Current evidence:** Unchanged since Phase 3's own audit — `TransmittalRecipient.Name` remains free text with `RecipientType` (`Internal/External/Vendor/Client`).
**Decision:** **NOT A GAP.** Re-affirmed: most recipients are external parties with no PEMS account; forcing an FK would misrepresent the common case. No new evidence this phase changes that conclusion.

### Area J — UI / UX / Localization
**Current evidence:** Not re-audited in depth this phase — no UI changes were made (Phase 4's implementation is entirely server-side authorization wiring, with zero new or materially changed views), so there is nothing new to UI-verify beyond re-confirming existing pages still render (covered by the full regression pass, which exercises view compilation via the solution build).
**Decision:** **UNVERIFIED this phase, unchanged from prior phases.** No claim of UI improvement or regression is made.

---

## 3. Reconciliation Table: Old Assumption vs. Current Evidence

| Area | Old Assumption / Prior Report | Current Evidence | Reconciled Status |
|---|---|---|---|
| G-06/G-07 (Revision-file binding) | Phase 0: OPEN | Phase 1 implemented, re-confirmed present | FIXED |
| G-10 (Client review conflation) | Phase 0: OPEN | Phase 2 fixed revision-scoping; `Document.Status` overload narrowed but not eliminated | PARTIAL (unchanged since Phase 2) |
| G-13 (Handover) | Phase 0: PARTIAL | No change found | PARTIAL (unchanged) |
| G-14 (Retention) | Phase 0: OPEN | No change found | OPEN (unchanged, deferred by design) |
| G-21 (Transmittal historical file identity) | Phase 3: OPEN → FIXED | Re-confirmed implemented and tested | FIXED |
| G-22 (Per-recipient acknowledgement) | Phase 3: OPEN → FIXED | Re-confirmed implemented and tested | FIXED |
| G-23 (Recipient identity) | Phase 3: deferred, "not a gap" | No new evidence | NOT A GAP (reaffirmed) |
| G-24 (Import-path snapshot) | Phase 3: deferred, documented | No new evidence of material risk | OPEN, deferred (reaffirmed) |
| **SoD self-approval** (Area D) | **Never previously investigated** | **Fully-built infrastructure found completely unwired** | **OPEN → FIXED this phase** |
| **AI authority boundary** (Area G) | **Never previously investigated** | **A real, zero-impact-so-far bypass path found and closed** | **OPEN → FIXED this phase** |

---

## 4. PHASE 4 AUDIT VERDICT (Decision Gate)

### A. Verified Fixed (this phase, including the post-implementation verification pass)
- **P4-01:** `EngineeringDocumentsController.Create`/`Edit` now enforce `IPermissionService.HasPermissionAsync(..., "EngineeringDocuments", "Create"/"Edit")` server-side — closing a genuine zero-authorization gap.
- **P4-02:** `AccessControlService.AuthorizeAsync`'s SoD enforcement is now wired into `EngineeringDocumentsController`'s `Create`→`Approve` and `Edit`→`Approve` paths, activating the already-seeded `SoDPolicy` for the first time in production.
- **P4-03:** `EngineeringDocumentIntelligenceService.AdvanceStageAsync` no longer writes `Document.Status`; the AI intake pipeline's own `AIWorkflowStage` and the formal engineering `Document.Status` are now fully independent, as they should always have been.
- **P4-04 (found during verification):** `DocumentWorkflowController.Approve` — a second, UI-reachable action (`DocumentWorkflow/Index.cshtml` has a live "Approve" button for every "Checked" document, shown to every logged-in user regardless of role) calling the identical `DocumentWorkflowService.ApproveAsync` — had **zero authorization of any kind**, not even a base permission check, let alone SoD. Fixed with the identical permission+SoD pattern as P4-01/P4-02. Verified by 3 new tests (`DocumentWorkflowControllerApprovalAuthorizationTests.cs`), including a genuine self-approval-denial scenario proven against the real controller with a real `WorkflowInstance`.

### B. Verified Open (real, current, not selected for this phase)
- Area A: MDR completeness (no Information Requirement model exists) — large, needs its own phase.
- Area C: `Document.Status` "Rejected" ambiguity (Phase 2's L2-02) — well-scoped, low-risk, good next-phase candidate.
- Area E: No project-level Handover Package — needs a full equipment↔document relationship trace first.
- Area F: No Retention/Legal Hold model — no urgent trigger; must not be built as deletion-automation-first.
- Area H (G-24): Import-path Transmittal snapshot narrower than the main path — no evidence of material risk.
- **G-28 (found during verification pass):** `MDRImportService.ComputeStatusFromCommentStatus` (`MDRImportService.cs:1038-1047`) sets `Document.Status = "Approved"` directly whenever an imported Excel row's comment-status code is `"AP"` or `"NC"` — a **third** path (alongside the two now-fixed controller actions) capable of setting the exact same SoD-sensitive authoritative value, entirely bypassing `DocumentWorkflowService`, the workflow engine, and any SoD/discipline check. The same import method also sets `IsFrozen = true` directly (`MDRImportService.cs:471-474`) without populating `FrozenById`/`FreezeReason`, unlike the formal `Freeze` action. **Not fixed** — this is a fundamentally different kind of operation (bulk historical-data synchronization from an external MDR spreadsheet, not a live governance decision) and touching it requires a business decision this session cannot make unilaterally (should bulk import ever be authoritative for "Approved"? should it require the same SoD?) — exactly the same "do not touch MDR import machinery without evidence of material risk" boundary already established for G-24 in Phase 3.

### C. Verified Partial
- Area B: `EngineeringDashboardController` + prior E05A-D phases already provide substantial workspace/navigation capability — not re-audited to full depth, not rebuilt.

### D. Business Decisions Required
- None block independently-implementable work. Areas A/E/F each require either new business input (retention periods, handover package definition, requirement-tracking process) or a scoping decision beyond this session's authority — each is documented above rather than guessed at.

### E. Recommended Phase 4 Scope (implemented)
Exactly one coherent initiative, three code changes: **close the missing Create/Edit authorization gate and wire the pre-existing, pre-tested SoD/AI-authority-boundary infrastructure into its intended callers.** Chosen over every other candidate because it uniquely satisfies all six weighting criteria simultaneously: highest verified security/governance impact, zero data-integrity risk, zero new architecture (100% reuse), lowest implementation risk (additive-only, no schema change at all), and the clearest, most direct evidence trail of any finding this phase.

### F. Explicitly Deferred
- G-23 (recipient identity) — not a gap, external recipients are valid as free text.
- G-24 (import-path snapshot) — MDR/VDR redesign is outside Phase 4 scope; no material-risk evidence found.
- Area A (MDR completeness / Information Requirements) — genuinely large net-new feature, needs its own phase.
- Area C (Status value disambiguation) — well-scoped for a future phase, deliberately not bundled in to keep this phase's diff small and its review surface tight.
- Area E (Handover Package) — must not be built before proving the current system's actual limits via a full relationship trace.
- Area F (Retention/Legal Hold) — no automation exists yet that would need it; building it speculatively would itself violate "no speculative abstractions."
