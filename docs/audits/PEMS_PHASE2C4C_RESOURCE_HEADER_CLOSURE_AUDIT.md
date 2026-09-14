# PEMS Phase 2C-4C — Final Resource Header Closure Audit

**تاریخ:** 2026-09-14
**HEAD:** `6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251` (branch `main`) — unchanged by this phase.
**Working tree:** `VDR/Details.cshtml` modified + `ResourceHeaderViewModel.cs`/
`_PemsResourceHeader.cshtml` untracked, all from Phase 2C-4B, confirmed unchanged since; no
other application-code drift; pre-existing unrelated untracked items (`.claude/Role/...`,
`src/stitch-temp/` deletions, `filestore`/`uploads` runtime artifacts) present and untouched.

---

## 1. Executive Decision

```
RESOURCE HEADER WORKSTREAM:
CLOSED — VDR-ONLY
```

---

## 2. Evidence Used

- Phase 2C-4A (`PEMS_PHASE2C4A_RESOURCE_HEADER_CANDIDATE_AUDIT.md`) — full candidate audit,
  not repeated here.
- Phase 2C-4B (`PEMS_PHASE2C4B_RESOURCE_HEADER_PILOT_IMPLEMENTATION.md`) — VDR pilot,
  confirmed complete: `_PemsResourceHeader.cshtml` and `ResourceHeaderViewModel.cs` exist and
  are consumed by `VDR/Details.cshtml`; full regression 1212/1212 (not rerun this phase — no
  application code changed, per the phase's own instruction not to manufacture a new run).
- Current VDR implementation re-confirmed present and unchanged (`git status` above).
- **Limited, targeted inspection of `EngineeringDocuments/Details.cshtml`, lines 1-65 only**
  (not a full re-read — the header/command-bar shape was already fully documented in Phase
  2C-4A's "Engineering Documents — Runner-up" section and is not re-derived here, only
  re-verified for drift). No drift found.

## 3. Engineering Documents Compatibility

| Criterion | Result | Evidence |
|---|---|---|
| A. Presentation similarity | PASS | Identifier (`Model.DocumentNo`), a dynamic status badge (already via the existing `DocumentStatusBadge` helper), a revision badge, conditional Frozen/Hold badges, and one metadata line all map cleanly onto existing `ResourceHeaderViewModel` fields. |
| B. Contract stability | PARTIAL | EDMS's header carries a genuine clamped `Model.Title` line (`<div class="text-muted pems-doc-title-clamp" title="@Model.Title">@Model.Title</div>`) between the identity row and the metadata line. VDR's header has no equivalent — the shipped contract has no field for it. Supporting EDMS needs one new nullable `Title` property, a real (if small) contract expansion. |
| C. Authorization isolation | PASS | `ViewBag.CanFreeze`/`ViewBag.CanHold` are already resolved booleans from the controller today; a header consuming pre-resolved values changes nothing about where authorization lives. |
| D. Workflow isolation | PASS | `Model.Status` is already fully resolved before the view renders; no workflow computation would move into the header. |
| E. Action isolation | **FAIL** | The primary command bar (the `if/else if` chain over `Model.Status` driving Submit/Check/Reject/Approve/Issue/Revise, plus the "More" dropdown) is not adjacent to the identity block — it is the **other half of the same flex row** (`<div class="d-flex justify-content-between align-items-start flex-wrap gap-3">` wrapping both `pems-doc-identity` and `pems-doc-actions` as siblings). VDR's header had zero actions to begin with, so extraction there only ever lifted static content. Here, isolating actions requires restructuring a row that today treats identity and actions as one coupled layout, not two already-separate regions. |
| F. No architecture expansion | PARTIAL | The C# contract itself would not need generic `Actions`/`Workflow`/`Permissions` fields — the command bar would stay entirely in `EngineeringDocuments/Details.cshtml`. But the *markup* requires real restructuring (splitting one flex row into independently-laid-out siblings) to achieve that separation — a larger, riskier change than "lift an already-isolated block into a partial," which is what VDR was. |
| G. Low implementation cost | **FAIL** | Combines: (1) the flex-row restructuring above, (2) one new contract field, and (3) the already-known, still-unresolved duplicate-breadcrumb bug sitting in the same region of this exact file (re-confirmed present, unchanged, in the lines read this phase) — any change here would force a decision about that bug too, whether or not it's fixed. This is a second real pilot, not a small surgical follow-on. |

Two criteria fail outright (E, G), two are partial rather than clean passes (B, F). Per the
phase's own decision rule, this is "one or more criteria materially fail" — not ambiguous, not
a judgment call requiring escalation. The rule's own fallback for ambiguity (KEEP VDR-ONLY)
would produce the same answer regardless.

## 4. Final Boundary

```
The Resource Header abstraction is validated as a presentation-only
primitive through the VDR pilot only.

It is not a universal Resource framework, and this audit finds that
extending it to Engineering Documents is not currently a small, safe,
surgical step — it would require restructuring EDMS's header/command-bar
markup, not merely relocating already-isolated content.
```

## 5. Explicit Non-Goals (Confirmed Still True)

- Equipment — not migrated. Rejected in Phase 2C-4A for a hardcoded, non-dynamic status badge
  and a mutating action embedded inside its header region. Not revisited here.
- Work Order — not migrated. Rejected in Phase 2C-4A for its domain-entity binding (no DTO)
  and the highest action/header coupling of any candidate. Not revisited here.
- Permit — not migrated. Deferred in Phase 2C-4A pending validation elsewhere; this phase adds
  no new reason to reconsider it, and does not reconsider it.
- Vendor / Transmittal / Material Requisition — not migrated; never proposed as near-term
  targets.
- No generic `Actions`/`Workflow`/`Permissions`/`Resource` model was created at any point in
  this workstream.
- No broad Details-page refactor was performed or proposed.
- `EngineeringDocuments/Details.cshtml` was **not modified** in this phase — inspection only.

## 6. Deferred Findings

```
Equipment hardcoded-success status presentation
    <span class="badge bg-success">@eq.Status</span>
Deferred to a dedicated Equipment UI correctness/remediation phase.
Not fixed here. Not fixed in Phase 2C-4B. Not fixed in this phase.
```

Also carried forward, unresolved, out of scope for this closure: the Engineering Documents
duplicate-breadcrumb rendering (own inline `<nav>` at the top of the file, in addition to the
shared `_Breadcrumb.cshtml` that `_Layout.cshtml` already renders) — re-confirmed present at
lines 61-65 during this phase's limited inspection, unchanged, undecided, not this phase's
concern to resolve.

## 7. Recommendation

```
STOP SPENDING DEVELOPMENT TIME ON RESOURCE HEADER ABSTRACTION.
```

The VDR pilot (Phase 2C-4B) is complete, verified, and stable — that is the deliverable this
entire workstream (2C-3 → 2C-4A → 2C-4B → 2C-4C) set out to produce: proof that a small,
presentation-only, authorization-free, workflow-free shared primitive is safe in PEMS, without
inventing a universal Resource framework. That proof stands on its own regardless of whether
any second resource ever adopts it. Further analysis of Engineering Documents, Equipment, Work
Order, or Permit would not add confidence to that proof — it would just be more design
exercise against candidates already shown (2C-4A) or now confirmed (this phase, for EDMS) to
carry real behavioral coupling. Effort belongs elsewhere in PEMS.

---

## PHASE 2C-4C STATUS

```
Repository:
PEMS-cmms-edit-empoleeys-master

Branch:
main

HEAD:
6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251

Working Tree:
Unchanged from end of Phase 2C-4B (VDR/Details.cshtml modified, ResourceHeaderViewModel.cs
and _PemsResourceHeader.cshtml present, untracked) plus this phase's one new document;
pre-existing unrelated untracked items untouched

Application Code Changes:
NONE

Documentation:
docs/audits/PEMS_PHASE2C4C_RESOURCE_HEADER_CLOSURE_AUDIT.md

Engineering Documents Compatibility:
FAIL

Resource Header Decision:
CLOSED — VDR-ONLY

Future Resource Header Pilots:
NOT RECOMMENDED

Equipment:
REMAINS OUT OF SCOPE

Work Order:
REMAINS OUT OF SCOPE

Regression:
1212/1212 previously verified in Phase 2C-4B

Phase 2C-4C:
COMPLETE

Next Action:
MOVE TO NEXT PEMS WORKSTREAM
```
