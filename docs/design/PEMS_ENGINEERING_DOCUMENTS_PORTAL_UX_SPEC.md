# PEMS Engineering Documents — Portal UX Specification

*Companion to `docs/audits/PEMS_PORTAL_UX_AZURE_INSPIRED_AUDIT.md`. Wireframe-level textual spec only — no new visual design system. Every section states what already exists today (keep as-is) versus what is genuinely new in this spec.*

## 1. Global Shell

**Already exists, keep as-is:** persistent topbar (`_Layout.cshtml`) — brand, global search, theme, language, notifications, user menu; global sidebar (`_SidebarMenu.cshtml`) grouped by module.

**New in this spec:** a Help icon and a Quick Create icon added to the topbar, both already mandated by the pre-existing Enterprise UI/UX Baseline but never built. Help icon opens a small popover pattern (see §7); Quick Create opens a short permission-filtered list of "New Engineering Document / New Letter / New Transmittal / New Work Request" links reusing each module's existing Create action — no new creation logic, just a shortcut menu.

## 2. Engineering Documents Service Menu

**New in this spec**, but built as a **shared, reusable component** (not EDMS-only) since no per-module secondary-nav pattern exists yet anywhere in the app. Rendered as a slim horizontal strip directly under the breadcrumb on every Engineering-Documents-family page (Index, Details, Create, Register, Revisions, Workflow, Transmittal — i.e. everything already grouped under "Engineering Management" in the sidebar):

```
Engineering Documents  |  Overview  ·  Documents  ·  My Documents  ·  Pending Actions  ·  Revisions  ·  Transmittals  ·  Reports
```

- "Overview" → existing `EngineeringDocumentsController.Index` (the KPI+grid page as-is).
- "Documents" → same Index, explicit label for clarity when arriving from elsewhere.
- "My Documents" → **new**, a filtered Index view (documents where `RegisteredById`/discipline == current user), reusing the existing Index action + a new query parameter, not a new controller.
- "Pending Actions" → **new**, surfaces the EDMS "My Tasks" query from §9 below.
- "Revisions" → existing `DocumentRevisions` area/controller (confirmed to exist in the sidebar under Engineering Management).
- "Transmittals" → existing `TransmittalController.Index`, filtered to Engineering-Document-sourced transmittals if that filter already exists, otherwise unfiltered (no new backend query invented here beyond what Transmittal already supports).
- "Reports" → existing `IEngineeringReportService`-backed report page.

Each item is permission-aware using the new `PermissionTagHelper` (§14) — an item simply doesn't render if the user has no access to that destination, rather than rendering and then redirecting to Access Denied.

## 3. Document List (Index)

**Already exists, keep as-is:** cross-module quick-nav row, 6 KPI cards, search+filter toolbar with offcanvas advanced filters, resizable-column grid, status badge via `DocumentStatusBadge`.

**New in this spec:** the cross-module quick-nav row is superseded by the new Service Menu (§2) — remove the duplicate row rather than keeping both. Otherwise unchanged; this page is already close to the target model.

## 4. Document Detail

**Already exists, keep as-is — this page is the most mature in the app for this pattern:** breadcrumb, header with document number/title/discipline/status/current revision, state-dependent command bar with a "More ▼" dropdown (§5), tabs (Overview / Attachments & AI / IDC Review / Revisions / Workflow History / Correspondence), Frozen/Hold banners, equipment cross-reference chips, workflow-stage diagram.

**New in this spec:**
- A **Transmittal tab** (or a section within Correspondence, renamed "Correspondence & Transmittals") listing transmittals referencing this document, mirroring exactly how the existing Correspondence tab lists related Letters — reuses `TransmittalController`/`Transmittal` entity, no new backend concept.
- A **right-hand context panel** (collapsible on narrower viewports, matching §16 responsive rules) surfacing at-a-glance: current status, current revision, responsible discipline, document controller, last updated, and — once §9 exists — the document's own "next action" line (e.g. "Awaiting your IDC response" if the current user's discipline has an open referral). This is new markup only; every value it shows is already computed and displayed somewhere else on the page today (this just repeats the highest-value subset compactly).
- A **contextual Help affordance** (§7) next to the page's H1/title.

## 5. Command Bar

**Already exists, keep as-is:** the state-dependent primary-action chain + "More ▼" dropdown in `Details.cshtml` lines 45-152 is exactly the pattern the directive asks for (`[Edit] [Submit for Review] [More ▼] [?]`).

**New in this spec:** extract this markup into a reusable `_CommandBar` partial (inputs: a list of `(label, formAction, style, visible, tooltip)` primary-action descriptors + a list of secondary-action descriptors for the dropdown) so the same pattern is available to VDR, Transmittal, and WorkOrder detail pages later without copy-pasting. The EDMS page becomes the first, reference consumer of this partial — behavior does not change, only where the markup lives. Add the `[?]` contextual-help slot (§7) as a fixed trailing element of the bar.

## 6. Revision View

**Already exists, keep as-is:** the "Revisions" tab (`Details.cshtml` lines 510-570) already shows a full table (Revision/Status/Client Review/Reason/Created By/Reviewer/Approved/Created/Issued/Effective/Supersedes) with an expandable row for change description/AI summary. No changes proposed — this already matches the directive's "Revision Detail" requirement in substance; a separate standalone Revision Detail *page* is not needed when the tab already carries the same information at equal or better density.

## 7. Help Pattern (new — see audit §H, flagged for confirmation before building the schema)

A `[?]` icon appears in three fixed places: next to a page's H1, in the command bar, and next to any field/section the module owner deems non-obvious. Clicking it opens a small popover (not a modal, not a new page):

```
┌─────────────────────────────────────────┐
│ Engineering Documents                     │
│ مدارک مهندسی                              │
│                                            │
│ Manage controlled engineering documents,  │
│ revisions, reviews, approvals and         │
│ transmittals.                             │
│                                            │
│                              Learn more → │
└─────────────────────────────────────────┘
```

"Learn more" navigates to a specific `HelpController` article resolved by the current route/module (never a generic help homepage, per the directive's §10). If no article exists yet for a given context, the popover simply omits the "Learn more" link rather than showing a broken/placeholder link.

Backing entity (proposed, not yet built): `HelpTopic(TopicId, Module, RouteKey, TitleEn, TitleFa, SummaryEn, SummaryFa, ContentEn, ContentFa, RelatedTopicIds, Keywords)`. `RouteKey` maps a controller/action (or a stable UI element id) to a topic; resolution falls back to `Module`-level content if no exact `RouteKey` match exists, and renders nothing (not an error) if neither exists — so shipping the UI ahead of full content coverage is safe.

## 8. Tooltip Pattern

**Already exists and already follows good practice, keep as-is:** short `title="..."` + `data-bs-toggle="tooltip"` on icon buttons (View/Edit/Delete, SHA-256 hash, "official file" marker, PDF-viewer controls), initialized via the existing `PemsTooltips.init()` in `site.js`.

**New rule going forward (process, not code):** tooltips must never carry the kind of content that belongs in the new Help popover (§7) — the two are now clearly separated. A tooltip explains what an icon/button *is*; the Help popover explains what a *page or workflow* is for and links to more detail. No existing tooltip needs to change; this rule governs new ones.

## 9. My Tasks

**New in this spec, no schema change** — extends `HomeController`'s existing "My Work" section (currently Permit + WorkOrder + Letters only) with an Engineering-Documents query: documents in `Submitted`/`Checked`/`IDC Review` status where the current user's discipline has an open referral, or where the current user holds an approval-eligible role for that document's discipline (reusing the exact eligibility check `CanApproveDocumentDisciplineAsync` already used by `EngineeringDocumentsController.Approve`, so "can I act on this" in the task list is guaranteed to agree with "can I actually click Approve" on the detail page). Each row links straight to the document's Details page, landing the user on the exact tab/action they need — no intermediate list-then-click-then-search step.

## 10. Favorites (flagged for confirmation — see audit §O/§P)

Spec, not yet built: a star icon next to the document number in both Index rows and the Details header, calling a generalized `IFavoriteService.ToggleAsync(entityType: "EngineeringDocument", entityId)` — the same service Letters' existing star feature would be migrated onto (see audit §P). A "Favorites" item appears in the new Service Menu (§2) once this exists, listing only documents the current user can still access (re-checked at render time, never trusting a stale favorite row to imply access).

## 11. Recent (flagged for confirmation — see audit §O/§P)

Spec, not yet built: viewing a Document Details page records a capped `RecentItem` row for the current user; the Home "Continue Working" widget and a "Recent" item in the Service Menu both read from it, filtered through the same live authorization check as Favorites (a document the user has since lost access to must not appear, per the directive's §26).

## 12. Search

**Already exists (typeahead picker), keep as-is** for its current use as a form-field picker.

**New in this spec, no schema change:** the topbar global search box (currently backed by the separate, cruder `GlobalSearchController`) is repointed at a new thin aggregator built on top of the *existing* 19 `ISearchableEntityProvider` implementations, so an Engineering Document result renders as:

```
DOC-INS-00123
Engineering Document · Rev 04 · Instrumentation
```

**Precondition, not optional:** the permission/project-scoping gap found in 18 of 19 providers (audit §G) must be fixed first — otherwise the new, more prominent unified search would surface unauthorized document titles more visibly than the current, more obscure implementation does today.

## 13. Breadcrumb

**Already exists, keep as-is as the mechanism**, extended with real depth for EDMS pages specifically (the shared `_Breadcrumb.cshtml` already supports this via `ViewData["Section"]`/`["Title"]` — the EDMS controllers simply need to set a third level for Details):

```
Engineering  →  Engineering Documents  →  DOC-INS-00123  →  Revision 04
```

## 14. User Preferences (flagged for confirmation — see audit §O/§P)

Spec, not yet built: a `UserPreference` row per user covering (at minimum) `Theme`, `SidebarCollapsed`, `TableDensity`, `DefaultPageSize`. Nothing EDMS-specific here — this is shell-wide infrastructure that Engineering Documents pages simply consume like every other page once it exists (e.g., the Documents grid honors the user's saved page size instead of always defaulting to 50).

## 15. Dashboard Integration

Covered by §9 (My Tasks) — no separate EDMS-specific dashboard widget is proposed beyond feeding into the existing Home "My Work" section and the existing `UserDashboardWidget` catalog (the already-broken `MyDisciplineTasks` catalog entry, which currently silently renders `0`, is the natural home for this — fixing its missing `switch` case rather than inventing a ninth widget key).

## 16. RTL / LTR

Documents, revisions, and equipment identifiers (`DOC-INS-00123`, `REV-04`, tag numbers) must render LTR even inside an RTL page, exactly as `cmms-rtl.css` already attempts via `unicode-bidi:plaintext` for CMMS tag numbers — the EDMS pages should adopt the same rule (once `cmms-rtl.css` is either wired in globally or its relevant rule is folded into `site.css`, per audit §J's recommendation to resolve the orphaned-file situation before building on top of it). The new Service Menu (§2) and command-bar partial (§5) must use logical CSS properties (`margin-inline-start`, not `margin-left`) so they flip correctly with no separate RTL override needed, consistent with how `administration-design-system.css` already does this in places.

## 17. Responsive Behavior

The new right-hand context panel (§4) collapses into a top-of-page summary strip below 992px width, matching the existing sidebar's own breakpoint behavior (`_Layout.cshtml`'s `pems-opened` mobile class already uses 992px as its threshold — reused, not reinvented). The Service Menu (§2) becomes horizontally scrollable rather than wrapping, matching how the existing Index page toolbar already behaves on narrow viewports.

## 18. Accessibility

All new interactive elements (Service Menu items, Help `[?]` icon, Favorites star, Quick Create menu) must be reachable by keyboard and carry an `aria-label` where the visible content is icon-only — matching the existing standard already applied to the command bar's icon buttons in `Details.cshtml`. The Help popover must be dismissible via `Escape` and must not trap focus, consistent with how the existing Bootstrap dropdowns/offcanvas panels in this app already behave (no new accessibility pattern to invent — reuse Bootstrap's built-in handling throughout).

## 19. Permission-Aware UI

Every new element in this spec (Service Menu items, command-bar entries, Favorites star, Quick Create entries, task-list rows) is rendered through the new `PermissionTagHelper` (audit §P) wrapping the existing `IPermissionService.HasPermissionAsync`/`MenuAccess` — an unauthorized action is never rendered-then-blocked; it is simply absent, and the backend remains the sole source of truth exactly as it is today (this spec adds no new authorization surface, it only makes the existing one easier to check consistently from Razor).
