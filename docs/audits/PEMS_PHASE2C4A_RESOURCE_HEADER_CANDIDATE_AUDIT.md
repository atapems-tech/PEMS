# PEMS Phase 2C-4A — Repository-Grounded Resource Header Candidate Audit

**تاریخ:** 2026-09-14
**نوع فاز:** Audit + Design only. هیچ کد اپلیکیشن، View، Controller، ViewModel، CSS، یا
Migration تغییر نکرد.

---

## Repository Baseline

- **Repository:** `PEMS-cmms-edit-empoleeys-master`
- **Branch:** `main`
- **HEAD at start of this audit:** `6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251`
  (`feat: portal shell consistency, search security, authorization wiring, navigation fix,
  shared stage diagram`)
- **Working tree at start:** clean of application-code changes; only pre-existing,
  already-documented untracked items present (`.claude/Role/PEMS Help Governance
  Baseline...md`, `src/stitch-temp/` deletions, `PEMS.Web/filestore/*` and
  `wwwroot/uploads/*` runtime artifacts) — none of these are related to this session's work
  and none were touched.
- **Previous phase status (verified against actual files, not assumed):**
  - Phase 2C-2 (`docs/audits/PEMS_PHASE2C2_SHARED_RESOURCE_WORKSPACE_AUDIT.md`) — audited the
    resource workspace, sketched a conceptual `PemsResourceHeader` contract, explicitly
    deferred it as "ready for new/redesigned pages but migrating existing ones needs a
    per-resource pass."
  - Phase 2C-3 (`docs/audits/PEMS_PHASE2C3_STAGE_DIAGRAM_EXTRACTION_AUDIT.md`) —
    **confirmed complete by direct inspection**: `PEMS.Web/Views/Shared/_PemsStageDiagram.cshtml`
    and `PEMS.Web/Models/StageDiagramViewModel.cs` exist and are actually used by
    `EngineeringDocuments/Details.cshtml` (both call sites) and `VDR/Details.cshtml`;
    `_WorkflowStagesDiagram.cshtml`/`_VdrWorkflowStagesDiagram.cshtml` are confirmed deleted;
    `_WorkOrderStagesDiagram.cshtml` is confirmed **still present and unmigrated**, exactly as
    that phase's report states.

This audit treats the prior phases' findings as hypotheses and re-verifies them directly
against the current repository rather than citing them blindly — see per-candidate sections
below for what was re-confirmed versus newly discovered.

---

## Executive Summary

**Does the current PEMS repository contain a genuinely reusable Resource Header boundary?**

# PARTIALLY

A genuinely stable, low-risk, presentation-only header boundary **does exist for at least one
real resource (VDR)**, and very likely extends safely to Engineering Documents with modest
extra care. It does **not** exist uniformly across the CMMS resource set: Equipment embeds a
mutating action (Assign Functional Location, opened from a pencil button inside the header
card) directly inside what would be the header region, and its status badge is not even
dynamically computed today (a latent bug, detailed below) — extracting its header now would
either freeze that bug into the shared contract or require a silent behavior fix outside this
phase's scope. Work Order goes further: its "header" is inseparably interleaved with five
different mutating-action forms (Advance/Hold/Resume/Reject/Cancel) and is bound directly to
a `Domain.Entities.WorkOrder` object rather than a DTO. Permit's header card itself is clean,
but sits directly beside an eligibility-driven "what needs your attention" widget backed by
8+ separate `ExecutionGateResultDto` values, on a page that hosts four other sub-workflow
modules — the highest blast-radius page in the whole resource set.

The correct outcome, per the evidence, is a **narrow, single-resource pilot** (VDR), not a
repository-wide rollout, and not "no extraction at all" either — the VDR evidence is strong
enough to proceed.

---

## Resource Inventory (Actual, Verified)

| Candidate | `Details` action exists? | View model | Full read performed |
|---|---|---|---|
| Engineering Documents | Yes | `EngineeringDocumentDto` | Header + command bar re-verified (already fully read in earlier phases this session) |
| VDR | Yes | `VDRItemDto` | **Full file (775 lines) read in this phase** |
| Equipment | Yes | `EquipmentPassportDto` (wraps `EquipmentDto` + optional Instrument/Asset) | **Full file (720 lines) read in this phase** |
| Work Order | Yes | `Domain.Entities.WorkOrder` (**not a DTO**) | **Full file (614 lines) read in this phase** |
| Permit | Yes | `PermitDetailDto` | Header + controller action read in this phase (full 790-line body previously surveyed in Phase 2C-2, header/action-gate re-verified directly here) |
| Vendor | Yes | `VendorDto` | **Full file (37 lines) read in this phase** |
| Transmittal | Yes | `TransmittalDto` | Header region (70 lines) read in this phase |
| Material Requisition | Yes | `MRDto` | Header region (60 lines) read in this phase |
| Work Request | Yes | (not re-read this phase; structurally surveyed in Phase 2C-2: 129 lines, 3 cards, no tabs) | Not re-read — no new claims made beyond Phase 2C-2's structural count |
| Asset | Yes | (not re-read this phase; structurally surveyed in Phase 2C-2: 202 lines, 10 cards, no tabs) | Not re-read — no new claims made beyond Phase 2C-2's structural count |
| Functional Location | **No** | — | Confirmed via `grep` — no `Details` action on `FunctionalLocationController` |
| Maintenance Strategy | **No** | — | Confirmed via `grep` — no `Details` action |
| Maintenance Plan | **No** | — | Confirmed via `grep` — no `Details` action |
| RCM / FMEA | **No** (`ClassDetail` exists instead — a per-EquipmentClass analysis view, not a per-record resource page) | — | Confirmed via `grep` |
| Equipment Criticality | **No** | — | **New finding this phase** — confirmed via `grep`, not previously checked |

**11 resource candidates actually inspected** (7 with real Details pages given real evidentiary
weight, 4 non-candidates confirmed absent/miscategorized).

---

## Candidate Matrix

| Resource | View | ViewModel | Structural Similarity | Data Similarity | Behavioral Similarity | Risk | Recommendation |
|---|---|---|---:|---:|---:|---|---|
| VDR | `VDR/Details.cshtml` | `VDRItemDto` | High | High | Low coupling (header has zero actions) | Low | **Pilot** |
| Engineering Documents | `EngineeringDocuments/Details.cshtml` | `EngineeringDocumentDto` | Medium (command bar lives in header) | High | High (workflow actions embedded in header) | Medium | Runner-up |
| Permit | `Permit/Details.cshtml` | `PermitDetailDto` | Medium-High (header card itself is clean) | High | Medium (action-items widget adjacent, 4 sub-workflows on page) | Medium-High (blast radius) | Not selected this pass |
| Equipment | `Equipment/Details.cshtml` | `EquipmentPassportDto` | Medium (mutating action embedded in header) | Medium (nested sub-DTOs) | Medium-High (Assign Functional Location action + hardcoded status badge) | Medium-High | **Rejected for this pilot** |
| Work Order | `WorkOrder/Details.cshtml` | `Domain.Entities.WorkOrder` (no DTO) | Low (5 action forms interleaved with header) | Low (no DTO to map from) | High (Advance/Hold/Resume/Reject/Cancel all in header) | High | **Rejected** |
| Vendor | `Vendor/Details.cshtml` | `VendorDto` | High (trivially simple) | High | Low | Low | Too thin to validate the abstraction |
| Transmittal | `Transmittal/Details.cshtml` | `TransmittalDto` | High | High | Low-Medium (one `ViewBag.CanIssue`-gated action) | Low | Reasonable secondary candidate, not selected |
| Material Requisition | `MaterialRequisition/Details.cshtml` | `MRDto` | Medium (inline status-change `<select>` next to header) | Medium | Medium | Low-Medium | Not selected |
| Work Request | `WorkRequest/Details.cshtml` | (unread this phase) | Unknown — requires full read | Unknown | Unknown | Unknown | Not evaluated further |
| Asset | `Asset/Details.cshtml` | (unread this phase) | Unknown — requires full read | Unknown | Unknown | Unknown | Not evaluated further |

---

## Detailed Findings

### VDR — Recommended Pilot

**File:** `PEMS.Web/Views/VDR/Details.cshtml` (775 lines, full file read). **ViewModel:**
`PEMS.Application.DTOs.VDRItemDto`. **Controller action:** `VDRController.Details(int id)`
(`PEMS.Web/Controllers/VDRController.cs:63-92`, re-read in full this phase).

**What is common (matches the header hypothesis almost exactly, verified directly):**
```csharp
<h5 class="mb-0">@Model.DocumentNumber <span class="text-muted small">Rev @(Model.LatestRevisionNo ?? 0)</span></h5>
<span class="text-muted small">@Model.VendorName &middot; @Model.ProjectName</span>
<span class="badge @sbTop fs-6">@Model.WorkflowStatus</span>
@if (Model.IsOverdue) { <span class="badge bg-danger fs-6">Overdue</span> }
```
Identifier, a secondary badge-like qualifier (Rev N), one metadata line, one status badge with
a locally-computed tone switch, one conditional badge. **Nothing else is in this header block.**

**What is different / VDR-specific (must stay local):** the entire workflow-decision UI
(SubmitForReview, DisciplineRespond, LeadReview, ReturnToVendor) lives **inside the Workflow
tab**, gated only by `currentStageName`, not by any header-adjacent logic. Document-number
generation forms (CONTRACTOR/SUB-CONTRACTOR formats) live in the Overview tab. None of this
touches the header region at all.

**What is reusable:** the entire header block, verbatim, maps onto the `PemsResourceHeaderModel`
concept already sketched (but not built) in Phase 2C-2's design spec — see contract section
below.

**What must remain local:** the `sbTop` status→tone switch expression itself (5 lines,
locally computed exactly like the pre-extraction stage-diagram logic in Phase 2C-3), the
Overdue-badge condition, and obviously the entire Workflow tab.

**Authorization boundary:** `VDRController.Details` has **no permission check at all** (a
deliberate, already-documented decision from the earlier CMMS/Vendor authorization phase —
Index/Details were left open). This means the header, if extracted, would receive data that
was never gated to begin with — there is no authorization state to preserve or leak. **Note,
re-confirmed by re-reading the whole file this phase:** the Workflow tab's action buttons
(SubmitForReview/DisciplineRespond/LeadReview/ReturnToVendor) are **not** wrapped in any
`ViewBag.Can...` visibility check — they render unconditionally whenever the workflow is at
that stage, and rely entirely on the controller's `EnsurePermissionAsync` (added in the
CMMS/Vendor authorization phase) to reject an unauthorized POST after the fact. This is a
genuinely new finding (not previously written down) and is **irrelevant to the header itself**
(the header has zero actions) but is recorded here because it is exactly the kind of
authorization-shape difference this audit was asked to trace.

**Workflow boundary:** the header's `StatusText`/`StatusTone` is workflow-derived
(`Model.WorkflowStatus`), but is a **pure display of an already-resolved value** — the header
never computes it, never calls `IWorkflowRuntime`, never branches on stage names. Safe.

### Engineering Documents — Runner-up

**File:** `PEMS.Web/Views/EngineeringDocuments/Details.cshtml`. **ViewModel:**
`EngineeringDocumentDto`. Re-verified (not re-read cover-to-cover this phase, since Phase
2C-2 and 2C-3 both already read this file in full within the same session and the header
region has not changed since).

**What is common:** identifier (`DocumentNo`), a real dynamic status→tone mapping via the
already-extracted `DocumentStatusBadge.CssClass()` helper (the *only* resource in the whole
inventory with a dedicated, named helper for this — reuse candidate in its own right), a
revision badge, conditional Frozen/Hold badges, one metadata line.

**What is different:** the **entire primary command bar** (the `if/else if` chain over
`Model.Status` driving Submit/Check/Reject/Approve/Issue/Revise) is rendered **inside the same
header `content-card`**, immediately after the identity block, with no markup boundary
separating "header" from "actions" today. This is the reason Engineering Documents is runner-up
and not pilot: extracting a header here means drawing a new line through markup that has never
been separated before, and the actions section itself has already been explicitly ruled out as
a shared-component target (Phase 2A, reaffirmed in Phase 2C-2's "no generic command bar"
finding). A pilot on this file would need this line-drawing to be done carefully in a follow-up
iteration, which is exactly why it is a *runner-up*, not this pass's target.

**Known outstanding issue on this file, re-confirmed:** the duplicate-breadcrumb bug
identified in Phase 2C-2 (this View renders its own `<nav aria-label="breadcrumb">` in addition
to the one `_Layout.cshtml` renders via `_Breadcrumb.cshtml`) is still present — confirmed by
`grep` in this audit. It remains out of scope for this phase (documentation-only correction
was not "absolutely required" to complete this audit) but is flagged again since any future
header-extraction pass on this file will touch the same region.

### Equipment — Explicitly Rejected (Not Merely Deferred)

**File:** `PEMS.Web/Views/Equipment/Details.cshtml` (720 lines, full file read this phase).
**ViewModel:** `EquipmentPassportDto` (wraps `EquipmentDto` as `.Equipment`, plus optional
`.Instrument`/`.Asset`). **Controller:** `EquipmentController.Details(int id)`
(`PEMS.Web/Controllers/EquipmentController.cs:91-111`, re-read this phase).

Per the explicit instruction not to treat Equipment as automatically safe merely because it is
visually important, the header region was inspected line-by-line:

```csharp
<span class="badge bg-success">@eq.Status</span>
```

**This is hardcoded, not a dynamic switch.** Every other resource inspected in this audit
(VDR's `sbTop`, Work Order's `statusClass`, Permit's `badgeClass`, Vendor's `riskBadge`,
Transmittal's `statusBadge`) computes a status→tone mapping from the actual status value.
Equipment's header badge is always green regardless of what `eq.Status` actually contains —
this is a **previously-undocumented latent display bug**, not a design choice. Extracting a
header contract from this file today would either (a) silently bake the bug into the shared
`StatusTone` value passed to the partial, or (b) require fixing the bug as a side effect of
the extraction — which is a real behavior change, explicitly out of scope for an
audit/design-only phase and inconsistent with "extract only what is demonstrably common."

Second, and independently disqualifying: the header card contains a real mutating-action
affordance —
```csharp
<button type="button" class="btn btn-sm btn-outline-secondary" data-bs-toggle="modal" data-bs-target="#assignFoundationModal" title="Assign Functional Location / Parent Equipment">
    <i class="bi bi-pencil"></i>
</button>
```
— a pencil button, positioned inside the same `row g-2 small` block as the Functional
Location/Parent Equipment/Installation Date metadata, opening a modal that posts to
`AssignFoundation` (the exact action gated in the earlier CMMS/Vendor authorization phase).
This means Equipment's "header region" is not presentation-only today — it already contains a
resource-specific business action, mid-metadata, with no visual separation from the rest of
the identity block. Moving this markup into a shared, presentation-only partial would either
strip a real, working feature (a genuine behavior change) or force the shared contract to grow
an "optional action slot" — precisely the anti-pattern Phase 2C-2 rejected ("Do not create a
generic Actions[] just because several headers contain buttons").

Also relevant: Equipment's `Details` action performs 6 separate service calls building 5
distinct `ViewBag` entries (`FunctionalLocations`, `Reliability`, `Inspections`,
`MaintenanceIntelligence`, `PendingClassificationSuggestion`, `ClassificationExplanation`) and
the View renders 9 tabs — the largest, richest workspace of any resource examined. This raises
rollback/testability risk for touching this file at all, even for a presentation-only change,
independent of the two disqualifying findings above.

**Verdict:** rejected for this pilot, not merely deferred — the evidence is that Equipment's
current header is not actually a stable presentation boundary, and treating it as one would
require fixing a latent bug and relocating a real feature, both outside this phase's mandate.

### Work Order — Explicitly Rejected

**File:** `PEMS.Web/Views/WorkOrder/Details.cshtml` (614 lines, full file read this phase).
**ViewModel:** `@model PEMS.Domain.Entities.WorkOrder` — **the only candidate in the entire
inventory bound directly to a Domain entity rather than a DTO.** **Controller:**
`WorkOrderController.Details(int id)` (`PEMS.Web/Controllers/WorkOrderController.cs:220-273`,
re-read this phase).

This is the most heavily action-laden header of any candidate: within the same header
`content-card`, the markup contains — in sequence — a status-driven "Advance to @nextStatus"
form (with a conditional team-assignment `<select>` embedded inline), a Hold form, a Resume
form, a Reject form (native-`confirm()`-gated), and a Cancel form (also native-`confirm()`-
gated), each with its own `@if (Model.Status is ...)` condition. There is no line in this
markup that separates "identity/status" from "actions" — they are the same block.

Additionally, `WorkOrderController.Details` runs roughly 15 separate raw `_context.X...`
queries directly in the controller (not behind a service layer), the most of any candidate
examined — confirming this page carries the highest presentation-and-data coupling risk in the
inventory.

**Precedent already set in this session:** Phase 2C-3 explicitly declined to migrate Work
Order's stage-diagram partial for materially similar reasons (distinct CSS/shape, unverifiable
visual-equivalence risk). This audit extends the same caution to the header, consistent with
that precedent rather than contradicting it.

**Verdict:** rejected. Not a "maybe later with more care" — the domain-entity binding alone
means there is no existing DTO to map header fields from, and the header/action interleaving
is the worst of any candidate.

### Permit — Considered, Not Selected This Pass

**File:** `PEMS.Web/Views/Permit/Details.cshtml`. **ViewModel:** `PermitDetailDto` (a real
DTO — architecturally more disciplined than Work Order). **Controller:**
`PermitController.Details(int id)` (`PEMS.Web/Controllers/PermitController.cs:200-233`, read
in full this phase).

The header card itself (icon + `PermitNumber`/`Title`/status badge, `PermitType`/requester
line, a metadata row of Equipment/FunctionalLocation/WorkOrder/Priority) is structurally close
to Equipment's shape but **without** an embedded mutating action — a genuine point in its
favor. `Details` is permission-gated (`IRoleModuleActionService.HasActionAsync(roles,
"Permit", "View")`), same mechanism confirmed for Shift Handover in Phase 2C-1.

What disqualifies Permit from being *this* phase's pilot is not the header card itself, but
its immediate neighborhood: an "Action Items" summary widget (a genuinely good, previously-
unique UX pattern documented in Phase 2C-2) computed from **8 separate**
`ExecutionGateResultDto`/eligibility ViewBag values, on a page that hosts four independent
sub-workflow modules (`ExecutionGate`, `GasTest`, `Isolation`, `Simops`). The blast radius of
touching this file — for any reason — is the highest of any resource examined. A future
iteration that has already validated the header pattern on VDR (and ideally Engineering
Documents) is a much safer time to attempt Permit than now.

### Vendor / Transmittal / Material Requisition — Too Thin to Validate the Abstraction

All three are small (37, ~195, ~138 lines), single- or few-card, DTO-based, tab-less pages.
Vendor in particular has **no workflow/status-badge concept at all** (only a `RiskLevel`
badge, structurally different from a workflow status). Extracting a header from any of these
would be low-risk but would prove little about whether the contract generalizes to the
pages that actually carry real workspace complexity — exactly the "sufficiently representative"
criterion in the pilot-selection rule. Material Requisition additionally exposes a sixth,
independent status-change UI pattern (an auto-submitting `<select>` right next to the header,
distinct from every other resource's badge-only display) — a real finding for the "repeated
UI patterns" record, but not disqualifying for a *future*, non-pilot migration.

---

## Candidate Ranking

Weighted evidence-based scores (1-5 per criterion × stated weight, summed to 100):

| Resource | Structural (20) | Data (20) | Low Auth Coupling (15) | Low Workflow Coupling (15) | Visual Stability (10) | Testability (10) | Rollback Safety (5) | Reuse Potential (5) | **Total** |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| **VDR** | 5 | 5 | 5 | 4 | 4 | 3 | 5 | 4 | **90** |
| Permit | 4 | 4 | 3 | 3 | 3 | 3 | 2 | 3 | 67 |
| Engineering Documents | 3 | 4 | 3 | 2 | 3 | 3 | 4 | 5 | 64 |
| Equipment | 3 | 3 | 3 | 3 | 2 | 3 | 3 | 3 | 58 |
| Work Order | 2 | 1 | 2 | 1 | 3 | 2 | 3 | 2 | 36 |

**This ranking is not used mechanically.** Permit outscores Engineering Documents numerically,
but Engineering Documents is named runner-up instead, for a stated reason: Engineering
Documents already has half the supporting infrastructure in place (`DocumentStatusBadge`, an
already-migrated `_PemsStageDiagram`) and its one real blocker (command bar embedded in the
header) is a well-understood, already-analyzed problem with a known resolution path (separate
the command bar from the header markup, which the compositional design in Phase 2C-2's spec
already recommends). Permit's blocker — deep entanglement with four sub-workflow modules and
8 eligibility DTOs — is a harder, less-understood problem and the highest-blast-radius file in
the inventory; it is better attempted after the pattern has been proven once, not as the
runner-up for an unproven pattern.

1. **Recommended pilot: VDR**
2. **Runner-up: Engineering Documents**
3. **Explicitly rejected: Equipment** (and, more emphatically, Work Order — both detailed above)

---

## Existing Reusable Infrastructure (Verified, Not Assumed)

| Item | Status |
|---|---|
| `_Breadcrumb.cshtml` | Exists, used by `_Layout.cshtml` unconditionally. EDMS's Details page still duplicates it locally (known bug, unresolved). |
| `_KpiCard.cshtml` + `KpiCardViewModel` | Exists, used on Home/Index and Equipment's Overview tab. Direct precedent for the `PEMS.Web/Models/*ViewModel.cs` + `Views/Shared/_X.cshtml` pairing convention. |
| `_Pagination.cshtml` | Exists (not inspected further this phase — out of scope for header work). |
| `_PemsStageDiagram.cshtml` + `StageDiagramViewModel`/`StageDiagramItem`/`StageItemState` | Exists, confirmed live in EDMS and VDR. **Direct naming/shape precedent used for this phase's proposed contract** — see design spec. |
| `window.PemsTooltips` | Exists (`wwwroot/js/site.js`), unrelated to header work but confirmed still the only shared tooltip mechanism. |
| ViewComponents | **Zero** in the entire `PEMS.Web` project (re-confirmed via `find`, unchanged since Phase 2C-2). |
| TagHelpers | **Zero** (re-confirmed). |
| `DocumentStatusBadge` (`PEMS.Web/Helpers/DocumentStatusBadge.cs`) | Exists, EDMS-only, maps status strings to custom CSS classes (`badge-status-*`) requiring matching CSS rules — a different strategy from every other resource's inline `switch` to Bootstrap `bg-*` classes. |
| `LetterStatusBadge` | Exists, Letter-only, same pattern as above. |

No duplicate of anything proposed below already exists.

---

## Anti-Patterns Explicitly Rejected for This Phase's Recommendation

Consistent with Phase 2C-2's findings and this phase's own instructions:

- No generic `Actions[]` on the header contract — Equipment and Work Order's headers prove
  action placement is not uniform even where visually similar.
- No `IResource`/`GenericResource*` abstraction — none is warranted by the evidence; a plain
  presentation-only view model (matching the `StageDiagramViewModel` precedent) is sufficient.
- No authorization logic inside the proposed header partial — every candidate's header data is
  already fully resolved before it reaches the view (confirmed for all five candidates
  examined in depth).
- No workflow calculation inside the header partial — `StatusText`/`StatusTone` are always
  values the caller already computed, never something the header would derive.
- No forced migration of Equipment or Work Order — explicitly rejected, not "later."

---

## Implementation Readiness Assessment

**Pilot: VDR — Ready for a narrowly-scoped Phase 2C-4B**, using exactly the same
audit-then-extract-then-verify discipline already proven in Phase 2C-3 (build, full regression,
manual markup-equivalence trace, no browser available — same disclosed limitation as 2C-3).

**Engineering Documents: needs another design iteration** before it can be a migration target —
specifically, a decision on how the command bar and header markup separate without becoming a
generic command-bar abstraction (already rejected) or losing the header's `DocumentStatusBadge`
integration.

**Equipment, Work Order: not appropriate for shared header abstraction at this time** — not a
"needs more design," but a "the current page has real behavior baked into what would be the
header" finding, mirroring Phase 2C-3's treatment of Work Order's stage diagram.

**Permit: not evaluated as a near-term target** — deferred until the pattern is validated
elsewhere, given its blast radius.

---

## Deferred Items

- Full reads of `WorkRequest/Details.cshtml` and `Asset/Details.cshtml` (only structurally
  surveyed, per Phase 2C-2 — not re-verified in depth this phase since neither was flagged for
  particular attention and neither scored competitively against VDR on the dimensions that
  matter).
- Any decision on migrating `DocumentStatusBadge`/`LetterStatusBadge` onto a shared status-tone
  model — noted as a design question for whichever phase eventually touches Engineering
  Documents' header.
- The Equipment hardcoded-status-badge bug — documented here as required, **not fixed**
  (fixing it was not necessary to complete this audit, and the phase's own rules require
  documenting rather than fixing unless completion of the audit demanded it).
- The Engineering Documents duplicate-breadcrumb bug — re-confirmed present, still unresolved,
  still out of scope.
- Six independent status-badge-tone implementations now catalogued across VDR, Work Order,
  Permit, Equipment (broken), Vendor (risk-badge, not status), Material Requisition (dropdown,
  not badge) — a candidate for the eventual `StatusTone` contract's design, not resolved here.

---

## Verification

```bash
git status --short
git diff --stat
git diff --name-only
```

Confirmed before finishing this phase: **zero** files under `PEMS.Web/Controllers`,
`PEMS.Web/Views` (other than the two new documents under `docs/`), `PEMS.Application`,
`PEMS.Domain`, `PEMS.Persistence`, `PEMS.Infrastructure`, or `PEMS.Tests` were modified. No
migration was created. No CSS/JS file was touched. The only new files on disk are this
document and its accompanying design spec under `docs/`.
