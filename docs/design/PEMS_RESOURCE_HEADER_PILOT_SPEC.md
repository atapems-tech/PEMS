# PEMS Resource Header — VDR Pilot Design Specification

**تاریخ:** 2026-09-14
**وضعیت:** Design-only. No `Views/Shared/_PemsResourceHeader.cshtml`, no
`PEMS.Web/Models/ResourceHeaderViewModel.cs`, and no change to `VDR/Details.cshtml` exist yet.
This document specifies exactly what Phase 2C-4B would build, so that phase can execute
without repeating this discovery.

This spec is grounded entirely in the evidence in
`docs/audits/PEMS_PHASE2C4A_RESOURCE_HEADER_CANDIDATE_AUDIT.md` — see that document for why
VDR was selected and Equipment/Work Order were rejected.

---

## 1. Naming (precedent-derived, not invented)

Phase 2C-3 established the exact pairing convention this spec follows:

| Layer | Phase 2C-3 precedent | This spec |
|---|---|---|
| C# view model (no "Pems" prefix) | `PEMS.Web/Models/StageDiagramViewModel.cs` | `PEMS.Web/Models/ResourceHeaderViewModel.cs` |
| Shared partial (`Pems`-prefixed) | `PEMS.Web/Views/Shared/_PemsStageDiagram.cshtml` | `PEMS.Web/Views/Shared/_PemsResourceHeader.cshtml` |

`ResourceHeaderViewModel`/`ResourceHeaderBadge`/`ResourceHeaderTone` are used below — not
`IResource`, not `GenericResourceHeader`, per the explicit prohibition on inventing generic
domain abstractions for a UI extraction.

## 2. Contract (derived from VDR's actual header markup, not a template)

```csharp
namespace PEMS.Web.Models;

public enum ResourceHeaderTone { Neutral, Info, Success, Warning, Danger }

public class ResourceHeaderBadge
{
    public string Text { get; set; } = "";
    public ResourceHeaderTone Tone { get; set; } = ResourceHeaderTone.Neutral;
}

public class ResourceHeaderViewModel
{
    public string Identifier { get; set; } = "";        // Model.DocumentNumber
    public string MetaLine { get; set; } = "";            // "VendorName · ProjectName"
    public string StatusText { get; set; } = "";          // Model.WorkflowStatus
    public ResourceHeaderTone StatusTone { get; set; }    // from the existing local sbTop switch
    public List<ResourceHeaderBadge> ConditionalBadges { get; set; } = new();
    // e.g. [{Text="Rev 3"}, {Text="Overdue", Tone=Danger}] — only when IsOverdue is true
}
```

**Why no `Title` field (unlike the conceptual sketch in the Phase 2C-2 design spec):** VDR's
header has no separate descriptive title distinct from the identifier — `DocumentDescription`
is shown later, in the Overview tab, never in the header. Including an unused `Title` field
in the pilot's real contract would violate the "derive the actual contract from repository
evidence" instruction. If Engineering Documents is migrated in a later phase, its header does
carry a title-like line (`Model.Title`, clamped) — `Title` can be added then, when a second
real consumer actually needs it, not speculatively now.

**Why `ConditionalBadges` instead of separate `RevisionBadge`/`OverdueBadge` fields:** VDR's
header has exactly two conditional badge-like elements (`Rev N`, `Overdue`), and Phase 2C-2's
`PemsHeaderBadge` sketch already anticipated more than one; a list avoids adding a third named
field the next time a resource needs a third badge (matches the `StageDiagramItem.RoleLabel`
precedent of using nullable/collection fields instead of resource-specific properties).

## 3. Rendering Boundary — Exactly What Moves

From `VDR/Details.cshtml` lines 63-73 (current):

```html
<div class="content-card mb-3">
    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
        <div>
            <h5 class="mb-0">@Model.DocumentNumber <span class="text-muted small">Rev @(Model.LatestRevisionNo ?? 0)</span></h5>
            <span class="text-muted small">@Model.VendorName &middot; @Model.ProjectName</span>
        </div>
        <div class="d-flex align-items-center gap-2">
            <span class="badge @sbTop fs-6">@Model.WorkflowStatus</span>
            @if (Model.IsOverdue) { <span class="badge bg-danger fs-6">Overdue</span> }
        </div>
    </div>

    <hr class="my-2" />
    <partial name="_PemsStageDiagram" model="wfStageDiagramModel" />
</div>
```

**Moves into `_PemsResourceHeader.cshtml`:** only the inner `d-flex justify-content-between...`
block (identifier, Rev badge, meta line, status badge, overdue badge) — rendered as:

```html
<div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
    <div>
        <h5 class="mb-0">@Model.Identifier
            @foreach (var b in Model.ConditionalBadges) { <span class="badge @ToneClass(b.Tone) fs-6">@b.Text</span> }
        </h5>
        <span class="text-muted small">@Model.MetaLine</span>
    </div>
    <div class="d-flex align-items-center gap-2">
        <span class="badge @ToneClass(Model.StatusTone) fs-6">@Model.StatusText</span>
    </div>
</div>
```

Wait — placement correction from direct markup comparison: in the current file, the Rev badge
is inline with the identifier (`h5`) while Overdue sits with Status on the right. The pilot
must preserve this exact left/right split, not merge all badges into one place. Revised:

```html
<div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
    <div>
        <h5 class="mb-0">@Model.Identifier @RenderBadges(Model.LeftBadges)</h5>
        <span class="text-muted small">@Model.MetaLine</span>
    </div>
    <div class="d-flex align-items-center gap-2">
        <span class="badge @ToneClass(Model.StatusTone) fs-6">@Model.StatusText</span>
        @RenderBadges(Model.RightBadges)
    </div>
</div>
```

This means the contract needs **two** badge lists, not one — `LeftBadges` (next to the
identifier; VDR's `Rev N`) and `RightBadges` (next to status; VDR's `Overdue`), not a single
`ConditionalBadges` list as sketched in section 2. **This correction itself is evidence of why
the contract must be finalized against the real markup during implementation, not frozen here
in the abstract** — Phase 2C-4B must re-verify this exact split against the live file before
writing the partial, not copy this document's badge-list shape uncritically if the file has
changed since this audit.

**Stays exactly where it is, untouched:** the `<hr class="my-2" />` and the
`<partial name="_PemsStageDiagram" ...>` call immediately after — per the explicit instruction
that the Stage Diagram is a separate primitive the header must not own. The header partial and
the stage-diagram partial remain siblings inside the same `content-card`, not nested.

**Non-migrated markup (everything else in the file):** all 5 tabs (Overview, Submissions,
Comments, Workflow, Correspondence), every form, every modal, the `sbTop` status-tone switch
(replaced by an equivalent switch that produces `ResourceHeaderTone` instead of a raw CSS
string — still lives in `VDR/Details.cshtml`'s own `@{ }` block, not moved anywhere).

## 4. Authorization Boundary

`VDRController.Details` has no permission gate today (confirmed in the audit) and the header
has zero actions. The pilot introduces **zero** authorization surface: the partial takes a
fully-resolved `ResourceHeaderViewModel` and injects nothing. No `IPermissionService`, no
`ICurrentUserService`, no `IAccessControlService` reference anywhere in the new files.

## 5. Workflow Boundary

`Model.WorkflowStatus` and `Model.IsOverdue` are already fully resolved by the time the View
renders (they are properties on `VDRItemDto`, populated by `VDRController.Details` →
`_service.GetByIdAsync(id)`). The pilot's `@{ }` block in `VDR/Details.cshtml` computes
`ResourceHeaderTone` from `Model.WorkflowStatus` using the exact same switch expression that
exists today (`sbTop`), just re-typed to return an enum instead of a CSS class string. No new
workflow logic, no new query, no new service call.

## 6. Localization / RTL

No new localization mechanism. `Identifier`/`MetaLine`/`StatusText`/badge `Text` are all
already-resolved strings from the DTO (`DocumentNumber`, `VendorName`, `ProjectName`,
`WorkflowStatus` — none of these are translated via `IStringLocalizer` today; they are raw
data values, not UI labels, exactly as they render today). `Identifier` must render with
`dir="ltr"` explicitly (per the Phase 2C-2 design spec's finding that document numbers should
not be subject to RTL reversal) — this is a **new, additive** attribute not present in the
current markup, and is the one place this pilot changes rendered output beyond a pure
refactor. It must be called out explicitly to the reviewer of Phase 2C-4B as an intentional,
minimal behavior addition (not a silent one) — no existing test or visual behavior depends on
`Identifier` NOT having `dir="ltr"`, since VDR document numbers are already LTR-shaped
alphanumeric strings and Persian-locale rendering was never verified either way (a documented,
not fabricated, verification gap per the parent audit).

## 7. Responsive Behavior

No new CSS. Reuses `d-flex justify-content-between align-items-center flex-wrap gap-2` exactly
as it exists in the file today — already responsive (wraps on narrow viewports) by virtue of
existing Bootstrap utility classes, unchanged.

## 8. Accessibility

No ARIA attributes exist on this markup today (confirmed in Phase 2C-2/2C-3 audits for the
adjacent stage diagram, and directly re-confirmed for this header block in this phase — no
`role`, no `aria-label` anywhere in lines 63-77). The pilot does not add any, consistent with
"no unrelated UI cleanup." If accessibility improvements are desired, that is a separate,
explicitly-scoped future phase, not a side effect of this extraction.

## 9. Performance Constraints

Zero. The partial takes a plain C# object; it makes no database calls, no service calls, no
HTTP calls. `VDRController.Details`'s existing query count (already documented in the parent
audit as roughly 6 calls) does not change — the pilot only changes which `.cshtml` file emits
a given block of HTML, not what data is fetched.

## 10. Extensibility Rules

- `ResourceHeaderTone` stays fixed at 5 values. A resource that believes it needs a 6th tone
  should use `ConditionalBadges`/`LeftBadges`/`RightBadges` instead, not extend the enum.
- Do not add a `Title` field until a second real consumer (Engineering Documents, per the
  runner-up recommendation) actually needs one — confirmed against its real markup at that
  time, not assumed from this document.
- Do not add an `Actions`/`Buttons` field ever, per the audit's explicit rejection of that
  pattern (Equipment and Work Order are the direct evidence why).

---

## 11. Phase 2C-4B Implementation Plan (specification only — not executed in this phase)

### Exact candidate
`VDR` (`PEMS.Web/Views/VDR/Details.cshtml`).

### Exact files to create
- `PEMS.Web/Models/ResourceHeaderViewModel.cs` (contains `ResourceHeaderViewModel`,
  `ResourceHeaderBadge`, `ResourceHeaderTone` — mirrors `StageDiagramViewModel.cs`'s file
  layout, all three types in one file).
- `PEMS.Web/Views/Shared/_PemsResourceHeader.cshtml`.

### Exact files to modify
- `PEMS.Web/Views/VDR/Details.cshtml` — replace the identity/status block (lines 65-72 in the
  version read for this audit — **must be re-verified against HEAD at the time Phase 2C-4B
  actually runs**, since line numbers drift) with a `<partial name="_PemsResourceHeader" ...>`
  call and a small `@{ }` block computing the `ResourceHeaderViewModel` the same way
  `wfStageDiagramModel` is computed today, immediately above it in the same file.

### Exact files NOT to touch
`PEMS.Application/*`, `PEMS.Domain/*`, `PEMS.Persistence/*`, `PEMS.Infrastructure/*`,
`VDRController.cs` (no controller change is needed — `VDRItemDto` already carries every field
the header needs), any authorization service, any workflow service, any other resource's View
(Equipment/WorkOrder/Permit stay untouched, per the audit's explicit rejection).

### Rendering boundary
Exactly section 3 above — the inner `d-flex justify-content-between` block only. The
surrounding `content-card`, the `<hr>`, and the `_PemsStageDiagram` call stay in
`VDR/Details.cshtml`.

### Non-migrated markup
Everything else in the 775-line file — all 5 tabs, all forms, all modals, the PDF-pin-comment
JavaScript block.

### Tests
Following the established pattern from every prior phase this session (`PEMS.Tests/Authorization/*`
real-seeded-permission style is not applicable here since there is no authorization surface to
test). The appropriate test, if any is added, would be a narrow one verifying
`ResourceHeaderTone` mapping logic — but per this session's own precedent (Phase 2C-3 explicitly
did **not** manufacture a test for the equivalent per-item `State` computation, since it is
pure presentation logic living in a `.cshtml` `@{ }` block with no existing test harness for
Razor rendering), Phase 2C-4B should make the same call and document it the same way, rather
than inventing new Razor-rendering test infrastructure for one small partial.

### Verification
1. `dotnet build PEMS.Web/PEMS.Web.csproj` — must be 0 errors.
2. `dotnet build PEMS.Tests/PEMS.Tests.csproj` — must be 0 errors.
3. Full regression: `dotnet test PEMS.Tests/PEMS.Tests.csproj` — must match the pre-change
   count exactly (no test manufactured, none should break, since no controller/service/
   authorization code changes).
4. Manual markup-equivalence trace (same method used in Phase 2C-3): reconstruct the exact
   HTML the new partial produces for a representative `VDRItemDto` and diff it mentally
   against the current file's output — document this trace in the Phase 2C-4B report exactly
   as Phase 2C-3's report did.
5. No browser/UI automation is available in this environment — state that explicitly in the
   2C-4B report rather than claiming visual verification occurred.

### Rollback
Single-file, two-new-file change. Rollback is: delete
`PEMS.Web/Views/Shared/_PemsResourceHeader.cshtml` and `PEMS.Web/Models/ResourceHeaderViewModel.cs`,
revert `VDR/Details.cshtml` to its pre-change state via `git checkout` on that one file. No
other file depends on the new files (VDR is the only consumer in this pilot), so no cascading
rollback is possible — this is the safest possible rollback shape, consistent with why VDR was
selected as pilot in the first place.
