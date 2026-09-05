# PEMS Engineering Module — Three-Pass Audit & Gap Matrix

**Status:** Audit only — per user decision, no implementation in this session. This report is the
INSPECT → PASS 1 → PASS 2 → PASS 3 → GAP MATRIX deliverable required before implementation begins
(see directive `Engineering Management  EDMS — Three-Pass Audit & Implementation Directive.MD`, §60/FINAL COMMAND).

**Method:** 4 parallel read-only agents covering (A) domain model/revision/numbering/freeze/comments/
attachments, (B) workflow engine/lifecycle/IDC/gates/client review/security/audit trail, (C) MDR/
dashboard/document-360/transmittal/traceability/AI, (D) performance/UX states/navigation/RTL/tests.
Every finding below is file:line-verified against the live repository, not inferred from the directive's
own claims. Related prior audit: `.claude/Role/PEMS_ENGINEERING_INTELLIGENCE_AUDIT_REPORT.md` (AI-pipeline
focused, complementary — not duplicated here).

---

## PASS 1 — BUSINESS AUDIT: **FAIL**

*Can Engineering Management actually operate the module from portfolio down to discipline level?*

- Portfolio counts (total/active/pending) exist as single KPI numbers on the home dashboard
  (`HomeController.cs:48-54,76-85,115-117`), including a `DocumentsByStatus` breakdown.
- **No discipline-level breakdown exists anywhere** — no Discipline × Status heatmap, no per-discipline
  pending/overdue/approved/rejected counts, no average review time. A manager cannot answer "which
  disciplines are behind" from the software; they'd have to eyeball the MDR list manually.
- No vendor performance view, no "which vendors are late" answer, no MDR Health calculation
  (`Healthy/Watch/At Risk/Critical`) anywhere in the codebase (zero grep hits).
- The Master Document Register (MDR) list itself is missing several directive-required columns
  (Contractor/Vendor, Comment Status, Planned/Actual/Forecast Date, Issue Purpose, Responsible Person,
  Next Action, Next Action Due Date) even though most of the backing DTO fields already exist — they're
  just not rendered as grid columns.

**Verdict:** the data exists at the row level but not at the management-reporting level. Engineering
Management cannot currently answer most of the directive's Pass-1 portfolio/discipline questions from
the UI.

---

## PASS 2 — EDMS / MDR / EPC WORKFLOW AUDIT: **FAIL**

*Can Document Control and Engineering actually control the complete document lifecycle?*

### Strong, genuinely real (not a gap)
- The workflow engine is **`PEMS.WorkflowPlatform`** — the same generic engine used by Permit-to-Work —
  wired to Engineering via `DocumentWorkflowService` + `EngineeringDocumentWorkflowSeeder.cs:76-176`
  (stages Check → IDC → Approval → Issue). Not ad-hoc status-flipping.
- **IDC is a real, server-enforced parallel-review gate**, not UI decoration: per-discipline referrals in
  `DocumentIdcReferral.cs`, and `WorkflowRuntimeService.DetermineStageOutcome`
  (`PEMS.WorkflowPlatform/Services/WorkflowRuntimeService.cs:373-408`) explicitly blocks stage completion
  while `pendingCount > 0`. A document cannot be considered reviewed while a required discipline hasn't
  responded — matches directive §15 exactly.
- **Workflow gates are server-side**, not button-hiding: `DocumentWorkflowService.SubmitAsync`,
  `IssueAsync`, `GetActiveInstanceIdOrThrowAsync` all validate preconditions before flipping status.
- **Discipline scoping and direct-URL bypass protection are real**:
  `EngineeringDocumentsController.cs:317-320` explicitly `Forbid()`s a `Details()` request for a document
  outside the caller's discipline scope — the code comment confirms this was written specifically to
  close the direct-URL bypass.
- **Audit trail is real**: IP + user-agent are captured (`GetClientIp()`/`GetUserAgent()`,
  `EngineeringDocumentsController.cs:87-89`) and persisted into an append-only `WorkflowHistory` table
  with prior/new stage — confirms the directive's own claim in §53.

### Hard gaps
- **Document Numbering has a real concurrency bug (P0).** Two competing systems exist: the project's
  proper atomic sequence generator (`NumberingService.cs:82-89`, backing the `CentralNumberingSequence`
  migration) does **not** register EngineeringDocument in its `Defaults` dictionary. The number generator
  actually wired into the controller is `DocumentNumberingService.cs:34-39`, which does
  `COUNT(*) + 1` with no DB uniqueness constraint on `DocumentNo`. Two engineers submitting simultaneously
  in the same project/discipline/type can be issued the **same document number**. This is the exact
  anti-pattern the directive calls out by name in §22 ("Two users creating documents simultaneously must
  never receive the same number") and it is currently violated.
- **Document Freeze is not enforced at all (P0).** `IsFrozen`/`FrozenDate` exist on the entity, but
  `EngineeringDocumentService.UpdateAsync` and the `Edit` POST action never check `IsFrozen` before
  saving — a "frozen" document can be silently edited. There is no Freeze/Unfreeze controller endpoint
  anywhere in the codebase; the only code path that ever sets `IsFrozen = true` is the MDR bulk-import
  service, one-way, with no reason/user/timestamp captured. Directive §23 requires this to be
  server-enforced with a full audit trail — currently it's a display-only badge.
- **Lifecycle states are incomplete.** Actual status literals in use today (`DocumentWorkflowService.cs`):
  Draft, Submitted, IDC Review, Checked, Approved, Issued, Rejected, Client Approved. **Missing**:
  "Approved With Comments" (only binary Approve/Reject exists at IDC/Approval stages), "Revise & Resubmit"
  (no such literal anywhere), a distinct "Issued for Construction"/IFC status, and "Superseded"/"Archived"
  (present only as aspirational comments on `Document.cs:43-44`, never actually set).
- **Client Review is not a truly separate state machine.** `ClientApprovalStatus`/`ClientComment`/
  `ClientResponseDate` are real dedicated fields, but `RecordClientResponseAsync`
  (`DocumentWorkflowService.cs:308-313`) writes the client's decision directly into the shared
  `Document.Status` column — internal and client review share one status field rather than two
  independently tracked workflows, contrary to directive §20's intent.
- **Transmittal exists and is properly linked** to EngineeringDocument (`Transmittal.cs` +
  `TransmittalItem`, real many-to-many, wired through `TransmittalController`) but is missing three
  directive-required fields: `Revision`, `Response Required`, `Due Date`.
- **Cross-module traceability is one-directional.** `EngineeringDocument` has real FKs to Equipment,
  Contract, Vendor, MaterialRequisition — but **none of those entities have a reverse navigation back to
  EngineeringDocument**. `Equipment.cs` has zero collection of related documents/drawings — this is the
  exact CMMS-integration gap the directive flags by name in §41/§42 as "critical for future CMMS
  integration," and it is confirmed missing today.

**Verdict:** the workflow *engine* and IDC gate are genuinely production-grade. But a numbering race
condition and a non-enforced freeze are correctness/data-integrity bugs, not polish items — Pass 2 fails
on those two alone, independent of the softer gaps (lifecycle states, client-review separation,
traceability).

---

## PASS 3 — SOFTWARE AUDIT: **FAIL**

*Architecture, security, performance, database integrity, UX, RTL, accessibility, testing, auditability.*

| Dimension | Verdict | Evidence |
|---|---|---|
| Architecture | PASS | Extends `Document`→`EngineeringDocument` as directed, reuses `PEMS.WorkflowPlatform` (no second engine built), reuses existing knowledge-base service for AI indexing (no parallel AI stack) |
| Security (discipline scoping, direct-access) | PASS | See Pass 2 — server-enforced, verified |
| Audit trail | PASS | IP/UA/prior-new-state persisted, append-only |
| **Performance** | **FAIL** | `EngineeringDocumentRepository.GetAllAsync()` (`:22-39`) loads **every active document with 5 `.Include()`s** into memory, no `.Skip()/.Take()`. The project's own `PagedResult<T>` pattern (used elsewhere, e.g. `IntakeService`) is never referenced anywhere in the Engineering path. Filtering happens client-side in JS. This will not hold up on a large EPC project's document count. |
| **Tests** | **FAIL** | `PEMS.Tests/` contains exactly one file, `UnitTest1.cs`, with a single empty scaffold test. **Zero** of the directive's ~20 required scenarios (numbering/duplicate, revision transitions, workflow valid/invalid/unauthorized, discipline access, IDC pending/overdue/missing-reviewer, comment lifecycle, freeze/unfreeze, client review) have any coverage. |
| SLA / review-performance metrics | MISSING | No average review/approval/client-response time, no first-pass approval rate, no revision rate computed anywhere. |
| UX empty/error/unauthorized states | PARTIAL | Empty-list state and `Forbid()` on unauthorized access both present and correct; no loading-state markup, no styled error/not-found panel on Details (falls through to framework 404). |
| Navigation | PARTIAL | Present: Documents, Document Register, MDR Import, Document Revision, Transmittals, AI Intelligence section. **Missing as first-class nav items**: Engineering Dashboard (only generic Dashboard), My Discipline, Pending Review, Pending Approval, IDC, Comments, Client Review, Engineering Analytics, Engineering Settings — several of these have no controller at all, only inline actions buried in the Details page. |
| RTL / localization | PARTIAL | RTL is correctly handled at the layout level (`_Layout.cshtml:23`, `dir` follows current culture) — not a gap. But `EngineeringDocuments/Details.cshtml` is almost entirely hardcoded English strings, inconsistent with the Index view's partial use of `@Localizer[...]`. |
| Accessibility | PARTIAL | Table markup is semantic (`<th>` used correctly); icon-only action buttons have no `aria-label` anywhere in Index/Details. |

**Verdict:** architecture and security are sound and were correctly *not* rebuilt from scratch (matches
the directive's own "do not treat this as a CRUD rebuild" instruction). But near-zero test coverage on a
module with server-enforced approval/safety-adjacent logic, plus a full-table-scan list query, are real
production-readiness blockers.

---

## GAP MATRIX (priority-ordered)

| # | Gap | Directive § | Severity | Why it matters |
|---|---|---|---|---|
| 1 | Document numbering: in-memory `COUNT+1`, no DB uniqueness constraint, not using the project's own atomic `NumberingService` | §22 | **P0 — data integrity bug** | Concurrent submissions can produce duplicate document numbers today |
| 2 | Document Freeze not enforced; no Freeze/Unfreeze endpoint; import-only, no audit trail | §23 | **P0 — control bypass** | A "frozen" (issued/controlled) document can be silently edited |
| 3 | Zero meaningful test coverage of workflow/IDC/freeze/numbering/discipline access | §55 | **P0 — regression risk** | Any future change to this module has no safety net |
| 4 | Full in-memory list load (no pagination) on the Engineering Document query path | §45 | P1 — will break at scale | Every `Include()`d row for every active document loads on every list view |
| 5 | Cross-module traceability is one-directional (Equipment/Contract/Vendor have no reverse link to documents) | §41-42 | P1 — blocks future CMMS integration, explicitly flagged by directive | Equipment page can't show its P&IDs/datasheets/manuals |
| 6 | Missing lifecycle states: Approved With Comments, Revise & Resubmit, distinct IFC, Superseded, Archived | §3 Pass 2 | P1 | Real EPC document lifecycles need these distinctions; currently collapsed into binary approve/reject |
| 7 | Client Review shares the same `Status` column as internal engineering status, not a separate tracked workflow | §20 | P2 | Muddies "who owns the next action" |
| 8 | MDR list missing directive columns (Vendor, Comment Status, Planned/Actual/Forecast Date, Issue Purpose, Responsible/Next Action) despite DTO already carrying most of them; no export/saved-filters on the main Engineering list (DocumentRegister has export, EngineeringDocumentsController doesn't); no MDR Health calc | §7-8 | P2 | Undermines MDR as a true register of record |
| 9 | No discipline-level dashboard/heatmap, no vendor performance view, no SLA/review-time metrics | §9-11, §34-37 | P2 | Blocks the Pass-1 management questions |
| 10 | Missing first-class nav/controllers: Engineering Dashboard, My Discipline, Pending Review, Pending Approval, IDC, Comments, Client Review, Analytics, Settings | §49 | P2 | These actions exist buried in Details but aren't discoverable as workflows in their own right |
| 11 | Transmittal missing Revision, Response Required, Due Date fields | §18 | P3 | Otherwise correctly modeled and linked |
| 12 | DocumentComment has no Employee FK for reviewer/responsible person (free-text names only); status set lacks Accepted/Rejected distinction | §16 | P3 | Weakens traceability of who owns a comment |
| 13 | DocumentAttachment: `UploadedById` declared but never populated; no checksum; uploads aren't linked to a specific revision | §26 | P3 | Minor but real data-completeness gaps |
| 14 | Document 360° workspace has 5 tabs (Overview/Attachments/Revisions/Workflow/Correspondence) vs. the ~13 the directive recommends — IDC/Comments/Related-entities data exists but is folded into other tabs rather than dedicated sections | §12, §51 | P3 | Findable but not information-architected as directed |
| 15 | `Details.cshtml` largely hardcoded English (inconsistent with Index's partial `@Localizer` use); no `aria-label` on icon-only buttons | §48 | P3 | Polish, not correctness |

---

## THREE-PASS FINAL QA SUMMARY

```
PASS 1 — BUSINESS AUDIT:      FAIL  (no discipline/vendor-level reporting; MDR incomplete)
PASS 2 — EDMS AUDIT:          FAIL  (numbering race condition; freeze not enforced; lifecycle gaps)
PASS 3 — SOFTWARE AUDIT:      FAIL  (no pagination; near-zero test coverage)

Engineering Dashboard:        FAIL  (single-number KPIs only, no discipline breakdown)
MDR:                          PARTIAL
Document 360:                 PARTIAL
Revision Control:             PARTIAL
IDC:                          PASS
Comments:                     PARTIAL
Client Review:                PARTIAL
Transmittal:                  PARTIAL
Workflow Gates:                PASS
Document Numbering:           FAIL
Freeze Control:                FAIL
Permissions / Discipline Scoping: PASS
Audit Trail:                  PASS
AI Intelligence:              PARTIAL (indexes title/description only; confidence tracked on attachments, not merged into authoritative fields — correctly non-authoritative)
Semantic Search:               PASS (real, reused, not duplicated)
Equipment Traceability:       FAIL (one-directional)
Contract Traceability:        FAIL (one-directional)
MR Traceability:               PARTIAL (one-directional, single-link only)
VDR Integration:               not audited this pass (separate entity chain, not covered by the 4 agents — flag for follow-up)
Letter Integration:            PARTIAL (single FK, one document per letter)
CMMS Traceability:              FAIL
RTL:                            PASS (layout-level)
Responsive:                    not specifically audited — flag for follow-up
Accessibility:                  PARTIAL
Performance:                    FAIL
Tests:                          FAIL

Critical Findings:
1. Document numbering can produce duplicates under concurrent submission (P0)
2. Document Freeze exists on the entity but is not enforced anywhere (P0)
3. Engineering module has essentially zero automated test coverage (P0)
4. Engineering Document list loads the full active-document set into memory with no pagination (P1)
5. No reverse navigation from Equipment/Contract/Vendor to their engineering documents — blocks future CMMS document discovery (P1)

Implemented Changes:
None — this session was audit-only by explicit user choice.

Remaining Gaps:
See Gap Matrix above (15 items, P0–P3).

Database Changes:
NO (this session)

Migration Created:
NO

Unrelated Modules Modified:
NO
```

---

## Recommended next step

The directive's own priority note (embedded at the end of the source document) already points at
Workflow/MDR/IDC as the next area of focus — this audit confirms IDC and the workflow gate are actually
in good shape, so the highest-value next slice is narrower than that: **fix the two P0 correctness bugs
(numbering race condition, freeze enforcement) plus stand up a baseline test suite around the existing
workflow/IDC/discipline-scoping logic** before adding any new surface area (dashboards, analytics,
traceability). That mirrors how the CMMS-10 series and the Permit UI-05 pass were sequenced — fix and
harden what exists before building on top of it. Awaiting direction on which slice to implement next.
