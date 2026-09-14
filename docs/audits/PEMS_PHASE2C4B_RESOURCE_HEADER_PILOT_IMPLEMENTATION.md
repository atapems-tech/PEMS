# PEMS Phase 2C-4B — Safe VDR Resource Header Pilot (Implementation)

**تاریخ:** 2026-09-14

---

## A. Baseline

- **HEAD before implementation:** `6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251`
- **Branch:** `main`
- **Working tree before implementation:** clean of application-code changes; only
  pre-existing, already-documented untracked items present (`.claude/Role/PEMS Help
  Governance Baseline...md`, `src/stitch-temp/` deletions, `PEMS.Web/filestore/*` and
  `wwwroot/uploads/*` runtime artifacts, plus the two Phase 2C-4A documents) — none touched
  by this phase.

## B. Audit Basis

Per `docs/audits/PEMS_PHASE2C4A_RESOURCE_HEADER_CANDIDATE_AUDIT.md`: VDR was selected because
its header (i) is fully DTO-based (`VDRItemDto`), (ii) contains zero embedded actions (all
workflow actions live inside the Workflow tab, not the header), (iii) has `Details` left
intentionally ungated (no authorization surface to preserve or leak), and (iv) already has one
proven, low-risk extraction precedent on this exact same page (`_PemsStageDiagram`, Phase
2C-3). Equipment and Work Order were explicitly rejected in that audit — this phase does not
revisit that decision.

The abstraction was kept narrow by design: exactly one new C# model file, one new partial,
one consumer. No framework, no `IResource`, no generic `Actions[]`/`Workflow`/`Permissions`
field was introduced — see Section D for the exact contract actually shipped.

## C. Files Changed

**Created:**
- `PEMS.Web/Models/ResourceHeaderViewModel.cs`
- `PEMS.Web/Views/Shared/_PemsResourceHeader.cshtml`

**Modified:**
- `PEMS.Web/Views/VDR/Details.cshtml` (header block only — see Section D/G)

**Not touched:** every other controller, view, service, entity, migration, CSS file, and JS
file in the repository, including Equipment, Work Order, Permit, and every other resource
Details page named in the audit.

## D. Contract (as actually implemented)

```csharp
// PEMS.Web/Models/ResourceHeaderViewModel.cs
public enum ResourceHeaderTone { Neutral, Info, Success, Warning, Danger }

public class ResourceHeaderBadge
{
    public string Text { get; set; } = "";
    public ResourceHeaderTone Tone { get; set; } = ResourceHeaderTone.Neutral;
}

public class ResourceHeaderViewModel
{
    public string Identifier { get; set; } = "";
    public string? SecondaryText { get; set; }
    public string MetaLine { get; set; } = "";
    public string StatusText { get; set; } = "";
    public ResourceHeaderTone StatusTone { get; set; }
    public List<ResourceHeaderBadge> ConditionalBadges { get; set; } = new();
}
```

### Deviation from `docs/design/PEMS_RESOURCE_HEADER_PILOT_SPEC.md`

The design spec's own section 3 sketched a single `LeftBadges`/`RightBadges` pair of badge
lists after noticing mid-document that "Rev N" sits beside the identifier, not beside
Status. Re-validating that sketch against the live file (per this phase's explicit
instruction to re-check the design document against the repository before building anything)
found it was **still not quite right**: `Rev N` in VDR's actual markup —

```html
<h5 class="mb-0">@Model.DocumentNumber <span class="text-muted small">Rev @(Model.LatestRevisionNo ?? 0)</span></h5>
```

— is plain `text-muted small` text, **not** a Bootstrap `badge` at all. There is no
left-side badge in VDR's header; there is exactly one plain secondary-text element next to
the identifier, and the badges (`Status`, optionally `Overdue`) are all on the right. The
shipped contract reflects this: `SecondaryText` (a plain nullable string, no tone, no badge
styling) instead of a `LeftBadges` list. This is a **smaller** contract than the design spec
proposed, consistent with "keep the shared header contract minimal" — the deviation removes
a field rather than adding one.

`Title` was not added (VDR has none in its header — `DocumentDescription` lives in the
Overview tab, never the header), matching the design spec's own reasoning for leaving it out
of the pilot.

## E. Behavioral Preservation

- **Authorization:** unchanged. `VDRController.Details` was not touched; it remains
  ungated, exactly as before. The new partial injects no authorization service and consumes
  no permission state — there was none to preserve.
- **Workflow:** unchanged. `Model.WorkflowStatus`/`Model.IsOverdue` are still read directly
  from the same `VDRItemDto` populated the same way by the same controller action. The
  status→tone mapping is a straight re-expression of the pre-existing `sbTop` switch (kept,
  untouched, because the Overview tab at line 142 still uses it independently) into an enum
  instead of a raw CSS class string — same four cases, same fallback, verified by manual
  trace (Section F).
- **Actions:** unchanged. Zero actions existed in the header before or after — every VDR
  action (SubmitForReview, DisciplineRespond, LeadReview, ReturnToVendor,
  GenerateContractorNumber, GenerateVendorNumber, UploadToFtp, comment/reply actions) remains
  exactly where it was, in its original tab, untouched.
- **Stage diagram:** unchanged. `_PemsStageDiagram` is still called immediately after the new
  header partial, inside the same `content-card`, with the identical `wfStageDiagramModel`
  computation (untouched). The two partials remain siblings, never merged, per the explicit
  instruction in Section 7/16 of this phase's own prompt.
- **Navigation/routes:** unchanged. No controller, no route, no `asp-action`/`asp-controller`
  attribute anywhere in the file was touched.
- **Localization:** unchanged mechanism — no `IStringLocalizer`, no resx lookup was present
  in the original header and none was introduced. All rendered text is still the same
  already-resolved DTO values, verbatim.
- **RTL/LTR:** one intentional, disclosed addition — `Model.Identifier` now renders inside
  `<span dir="ltr">...</span>`. This was flagged explicitly in the design spec (Section 6) as
  a deliberate minimal addition, not a silent one. It has no visible effect in the
  English-default rendering (dir="ltr" matches the ambient default) and only affects the
  Persian/RTL rendering path, where it prevents the alphanumeric document number from being
  treated as part of the surrounding RTL run. No existing test or documented behavior depends
  on the identifier *not* having this attribute.

## F. Verification

**Build:**
```
dotnet build PEMS.Web/PEMS.Web.csproj   → Build succeeded, 0 Errors, 19 Warnings
dotnet build PEMS.Tests/PEMS.Tests.csproj → Build succeeded, 0 Errors, 3 Warnings
```

One new warning was introduced: `VDR/Details.cshtml(70,22): warning CS8601: Possible null
reference assignment` on the `StatusText = Model.WorkflowStatus` line. `VDRItemDto.WorkflowStatus`
is declared as a non-nullable `string` with a default value (confirmed by direct inspection of
`PEMS.Application/DTOs/VDRDtos.cs:27`), so this does not reflect an actual null-reference risk
at runtime — it is consistent with the ~18 other pre-existing CS8601-family warnings already
present across the codebase before this phase (e.g. `DocumentWorkflowController.cs`,
`Areas/Admin/Views/Users/Index.cshtml`, `Views/Intake/Index.cshtml`, `Views/Letter/Index.cshtml`),
none of which have ever been treated as build-blocking. It is reported here rather than
silently suppressed.

**Full regression:**
```
Passed!  - Failed: 0, Passed: 1212, Skipped: 0, Total: 1212, Duration: 9 m 19 s
```
Exact match to the Phase 2C-3 baseline (1212) — zero tests broken, zero tests added (per this
session's established precedent of not manufacturing Razor-rendering tests where no such test
harness exists — see Phase 2C-3's report for the same reasoning, applied identically here).

**Static verification (Section 17, Step 9 of this phase's instructions):**

| Check | Result |
|---|---|
| Old duplicated header markup | None found — `grep` for `@Model.DocumentNumber <span` in `VDR/Details.cshtml` returns no matches |
| Old partial references | Not applicable — no prior VDR-header partial existed to reference |
| Unused model references | None — `ResourceHeaderViewModel`/`ResourceHeaderTone`/`ResourceHeaderBadge` appear in exactly 3 files: the model, the partial, and `VDR/Details.cshtml` |
| Duplicate CSS | None introduced — the new partial adds zero `<style>` block, reuses only pre-existing Bootstrap utility/badge classes |
| Duplicate JS hooks | None — the header region had zero `id`/`data-*` attributes before and after; all JS in the file (`pdfCanvasWrapper`, `pdfCanvas`, etc.) is confirmed to live in the Comments tab, untouched |
| Dead IDs | None |
| Broken partial references | None — build succeeded, confirming `<partial name="_PemsResourceHeader">` resolves correctly |

**Manual markup-equivalence trace** (same method as Phase 2C-3, since no Razor-rendering test
harness or browser is available in this environment):

| Scenario | Original output | New output | Match |
|---|---|---|---|
| `WorkflowStatus="Closed"`, `IsOverdue=false` | `<span class="badge bg-success fs-6">Closed</span>` | `<span class="badge bg-success fs-6">Closed</span>` | ✅ exact |
| `WorkflowStatus="UnderReview"`, `IsOverdue=true` | `bg-info text-dark` badge + `bg-danger` Overdue badge | identical two badges, identical classes | ✅ exact |
| `WorkflowStatus="Planned"` (unmapped/default) | `bg-secondary` | `bg-secondary` | ✅ exact |
| Identifier/meta line | `VDR-001 Rev 2` / `Vendor &middot; Project` | `VDR-001` (now `dir="ltr"`-wrapped) `Rev 2` / `Vendor · Project` | ✅ visually identical (`&middot;` entity and raw `·` character render identically; `dir="ltr"` is the one disclosed, intentional addition) |

**Browser verification:** **NOT AVAILABLE** in this environment (CLI-only, no browser/UI
automation tool present). Stated explicitly rather than claimed.

## G. Diff Scope

Every changed line is accounted for:
- `ResourceHeaderViewModel.cs` (new): the contract itself — necessary, is the shared model.
- `_PemsResourceHeader.cshtml` (new): the shared partial — necessary, is the shared artifact.
- `VDR/Details.cshtml`: exactly two changes — (1) a new `@{ }` block computing
  `vdrHeaderModel` from data already on `Model`/already-loaded (no new query, no new service
  call — `VDRController.Details` was not touched), placed next to the pre-existing
  `wfStageDiagramModel` computation for locality; (2) the inner header `<div>` block replaced
  with one `<partial>` call. No other line in this 785-line file changed. The pre-existing
  `sbTop` variable was deliberately left in place (not deleted, not refactored) because the
  Overview tab (line 142) still depends on it independently — touching it would have been an
  unrelated, un-asked-for cleanup.

## H. Deferred Items

- The Equipment hardcoded-status-badge finding (`<span class="badge bg-success">@eq.Status</span>`)
  remains **explicitly deferred** — Equipment was not touched in any way during this phase.
- Migration of Engineering Documents (runner-up), Permit, Work Order, or any other resource to
  `_PemsResourceHeader` was **not performed** — this phase is a single-resource pilot only.
- No broad Resource Header migration, no generalized migration framework, and no new
  ViewComponent/TagHelper infrastructure were introduced.
- The pre-existing duplicate-breadcrumb bug on Engineering Documents (unrelated to VDR) was
  re-confirmed present in Phase 2C-4A and remains untouched here, as it should be.

## I. Recommendation

```
APPROVED FOR FUTURE RESOURCE PILOTS
```

**Evidence:** the extraction preserved VDR's behavior exactly (one disclosed, intentional,
non-breaking RTL addition aside), required zero changes to the controller/service/
authorization/workflow layers, added zero new queries, broke zero existing tests, and left a
clean, single-consumer, trivially-revertible diff. This is the outcome the Phase 2C-4A audit
predicted for VDR specifically — it does **not** by itself justify moving to Equipment, Work
Order, or Permit, whose headers were independently found to have real behavioral coupling that
VDR's does not share. The recommended next step, per the Phase 2C-4A audit's own ranking,
remains Engineering Documents as runner-up — attempted only in a future, separately-scoped
phase, following this same audit-validate-implement-verify discipline.

---

## PHASE 2C-4B STATUS

```
Repository:        PEMS-cmms-edit-empoleeys-master
Branch:             main
HEAD Before:        6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251
HEAD After:          6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251 (uncommitted working-tree changes only)
Working Tree:        VDR/Details.cshtml modified; 2 new files created; pre-existing unrelated
                      untracked items unchanged and untouched

Pilot:               VDR

Shared Artifact:     PEMS.Web/Views/Shared/_PemsResourceHeader.cshtml

Model:               PEMS.Web/Models/ResourceHeaderViewModel.cs

Application Files Changed:
                      PEMS.Web/Views/VDR/Details.cshtml

Documentation Files:
                      docs/audits/PEMS_PHASE2C4B_RESOURCE_HEADER_PILOT_IMPLEMENTATION.md

Database Changes:     NONE
Schema Changes:       NONE
Authorization Changes: NONE
Workflow Changes:     NONE

Build:                PASS (0 errors, PEMS.Web and PEMS.Tests)

Targeted Tests:       N/A — no test manufactured (no Razor-rendering test harness exists;
                      same precedent as Phase 2C-3)

Full Regression:      1212/1212

Browser Verification: NOT AVAILABLE

Behavioral Preservation: PASS

Phase 2C-4B:          COMPLETE

Generalization Recommendation:
                      APPROVED FOR FUTURE RESOURCE PILOTS (VDR-specific evidence only —
                      does not extend to Equipment/Work Order/Permit, per Phase 2C-4A)
```
