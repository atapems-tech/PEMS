# PEMS — Site-wide UI/UX unification: final report

Companion files: [`WORKSPACE_DESIGN_SYSTEM.md`](WORKSPACE_DESIGN_SYSTEM.md) (rules), [`PEMS_PAGE_INVENTORY.md`](PEMS_PAGE_INVENTORY.md) (every page + status).

## 1. Executive summary
The application has 227 user-facing pages (249 views incl. partials/layouts) across 97 modules. Hand-redesigning each one is neither feasible in a single pass nor desirable (the same defects repeated everywhere), so the work was done in this order:
1. **Audit** — a static heuristic scan of all 249 views, then a **rendered audit** of every parameterless GET page (163 pages × EN LTR + FA RTL × 8 widths = 2,608 measurements) in headless Chrome against an isolated database.
2. **Fix shared components once** — one global layer (`pems-workspace.css` + `pems-ui.js`, loaded by `_Layout`) that unifies cards, KPI, buttons, status, tables, forms, modals, tabs, focus, motion, and the shell; plus the admin parallel design system mapped onto the same tokens.
3. **Rebuild pages that were structurally wrong** (Equipment list, Engineering Document create form, and earlier: Engineering Documents list/detail, VDR/Vendor/Transmittal, Digital Equipment Passport).
4. **Re-measure** — same audit re-run after the fixes.

What this is **not**: 58 pages need a record id / non-GET flow and were audited statically only; the remaining pages were unified through the shared layer and verified by automated checks, not individually re-composed; ~205 pages still contain literal (non-localized) strings. See §16.

## 2. Complete page inventory
See `PEMS_PAGE_INVENTORY.md` (module, page, view, rendered?, status, automated flags, localization). Counts: **Redesigned (page) 20 · Page-adjusted + global layer 47 · Global layer (rendered, automated checks) 102 · Audited statically only 58** = 227.

## 3. Pages audited
All 227 (static scan) — 163 of them also rendered in EN and FA at eight widths (1440, 1024, 820, 768, 600, 480, 390, 360).

## 4. Pages redesigned (view rebuilt)
Engineering Documents (Index, Details, Create), Equipment (Index, Details = Digital Equipment Passport), Work Request (Index, Create), VDR / Vendor / Transmittal / VDR Import (earlier tasks), Import Center (quiet launch buttons). Plus 35 views whose title was repeated inside the page, Admin hero blocks (12), Notification, QC ITP, Relationship bulk, Production Order/ Shift Handover/ TQ raise/ Account passkeys headings.

## 5. Pages already compliant
The pages built earlier on the workspace system (`.vs-*`, 16 views) needed only regression checks; they pass all automated checks.

## 6. Pages requiring no change
Login/forgot/change-password/access-denied use the auth layout (no shell) — audited, unchanged. Help Center pages have their own header (`HidePageTitle`) — unchanged.

## 7. Global design-system changes
- **`_PageHeader`** (earlier task) — one 28px row, no route/breadcrumb chain.
- **Unified industrial layer** (`pems-workspace.css`, loaded after `admin.css`): flat cards/KPI, one primary colour, quiet outline buttons in table rows, one badge tone map for `bg-*`/`text-bg-*`/`badge-status-*`, compact tables/forms/modals/tabs, focus ring, reduced motion, `:root` tokens for `--vs-*`.
- **`pems-ui.js`** (new, ~110 lines, no dependency): status icons on badges, accessible names for icon-only controls, `aria-label` for unlabeled fields, auto scroll wrapper for un-wrapped tables.
- **Shell fixes:** collapsed-rail sub-heading leak; **tablet/phone hamburger did not open the sidebar in LTR** (legacy CSS keyed `.show`/margin-right while the layout script toggles `.pems-opened`) — replaced by one direction-aware transform implementation; topbar fits ≤390px.
- **Admin area:** `administration-design-system.css` (blue primary, 12px radius, shadows, gradient hero, light-only colours) now uses the shared tokens, so admin follows the same accent and all themes.

## 8. UI/UX Pro Max principles applied
Skill: `.claude/skills/ui-ux-pro-max/SKILL.md` (read and applied). Applied in code: color-not-only (status icon + text), primary-action (one filled action; row actions outlined), focus-states (global `:focus-visible`), reduced-motion, form-labels/aria-labels, table density + tabular numbers, touch/keyboard reachability of the hamburger, no decorative gradients/shadows, progressive disclosure (filters/advanced), consistent icon set (Bootstrap Icons). PEMS-specific decisions (not from the skill): industrial density, chip strip instead of KPI card grids, identifier LTR isolation, Work Request vs Work Order separation, single Import Center entry.

## 9. PEMS industrial UX rules applied
Operational lists prioritise tag/number/status/priority/dates; engineering screens prioritise document number/revision/status; management screens avoid decorative KPI. Cards only where they add hierarchy.

## 10. RTL / LTR findings
- Every rendered page was loaded in `dir=ltr` and `dir=rtl`; no document-level overflow in either.
- Fixed: Manufacturer/Model and frequency cells scrambled by bidi (`.vs-num`), `.vs-num` breaking row borders on `<td>`, tablet menu in RTL.
- Known: pages with literal strings show English/Persian text in the "wrong" language (localization gap, not a layout fault).

## 11. Responsive findings
Measured at 8 widths. Before the first fix pass: 6 pages had overflowing elements (2 with document-level horizontal scroll: MRS Import @600, CMMS report @768; Admin Help/Permission Matrix tables, Project Intelligence text, WO stage diagram). After: **0 pages** with overflowing elements or document-level scroll at any width (tables get an automatic scroll wrapper; media is width-capped; scroll regions are recognised). No `overflow-x: hidden` hack was used on the page.

## 12. Accessibility findings
Automated (rendered, both languages): unlabeled inputs 96 → 1 pages; icon-only controls without a name 15 → 2 pages; status badges without an icon 8 → 0; multi-`h1` 3 → 0. Global focus ring and reduced-motion added. Not covered: full keyboard walk-through, contrast measurement per theme, screen-reader testing.

## 13. Interaction QA (real Chrome, EN + FA)
Sidebar slides open on hover (64 → 260px) and closes on leave; labels visible when open; sub-headings do not leak into the rail; topbar dropdown opens; create-equipment modal opens/closes; search field focusable; tablet hamburger opens the sidebar (LTR and RTL). Earlier tasks: passport tabs, chips→tabs, assign dialog. **Not done:** interaction tests on every page (search submit/sort/pagination/validation) — static renders cannot submit forms; only representative pages were exercised.

## 14. Visual QA
Screenshots inspected (EN/FA): Home, Users, Equipment index, Discipline workspaces, Reliability dashboard, Import Center, Engineering Documents, Work Requests, passport, tablet menu. Consistent language confirmed on those; the other pages were verified by the automated metrics only.

## 15. Before / after (rendered audit, 163 pages)
| Metric (pages affected) | First audit pass | Final |
|---|---|---|
| Document-level horizontal overflow | 2 | **0** |
| Any element overflowing the viewport | 6 | **0** |
| Status badges without icon | 8 | **0** |
| Icon-only controls without accessible name | 15 | 2 |
| Inputs without label/placeholder/title | 96 | 1 |
| More than one `h1` | 3 | **0** |
| Route/URL text in page content | 0 | **0** |
| Page header height | ≤28px | ≤28px |
"First audit pass" is measured after the first shared-layer version, so the true original numbers are worse (e.g. all 290 Bootstrap badges were colour-only originally).

## 16. Remaining issues (honest)
- **Not rendered:** 58 pages (need ids/POST flows). Statically audited only.
- **Localization:** ~205 of 227 pages still contain literal strings (English/Persian mixed). Only pages touched in this and previous tasks were localized. This is the largest remaining item and needs its own project.
- **Not re-composed:** ~102 pages are unified by the shared layer but keep their original structure (e.g. KPI-card dashboards are flattened, not turned into chip strips; table rows with several buttons are outlined, not converted to action menus; search boxes on other list pages were not consolidated).
- 8 pages still contain a *hidden* duplicate `h2` (admin hero title hidden by CSS); Letter/Create still has 15 icon-only buttons and 7 unlabeled fields; Relationship/GapReport 1 icon button.
- Persian help article added (`ui-conventions`); other Help articles were not re-written.
- Export Center: the repository has no Export Center module; the centralized **Import Center** was preserved untouched (no import buttons reintroduced).

## 17. Backend issues discovered, intentionally not changed
- Equipment / Asset / Instrument controllers have no module-permission check (any signed-in role can open them) — carried over from the previous task.
- `GET /AI/Test` returns HTTP 500 for the stub identity (not investigated).
- Home dashboard charts load Chart.js from a public CDN (breaks offline; a static render raced the script once).
- Many actions in Engineering Documents lack server-side permission checks (earlier report).

## 18. Build / test results
`dotnet build PEMS.slnx --configuration Debug`: 0 errors. Full suite: **1579 passed, 0 failed, 0 skipped** (previous baseline 1574; +5 new tests in `SiteWideUiSystemTests`; the help-article count test was updated 54 → 55 for the new `ui-conventions` article).

## 19. Graphify
`graphify update .` run after the code changes (project rule); graph, report and html refreshed.

## 20. Acceptance matrix
| Criterion | Status |
|---|---|
| Complete page inventory | ✅ `PEMS_PAGE_INVENTORY.md` |
| Every page audited | ✅ static (227) + rendered (163) |
| Every page redesigned where required | ⚠ shared-layer unification for most; 20 pages rebuilt; not all pages re-composed |
| UI/UX Pro Max applied | ✅ (see §8) |
| Global shell unified | ✅ hover rail + tablet menu fixed |
| Page header unified / no route or duplicate title | ✅ |
| Search / filter patterns unified | ⚠ Equipment + Engineering Documents; others not |
| Table / status / button / form / dialog patterns unified | ✅ via shared layer |
| Localization reviewed | ⚠ reviewed, **not** completed |
| RTL + LTR reviewed | ✅ automated on all rendered pages |
| Responsive reviewed | ✅ 8 widths on all rendered pages |
| Accessibility reviewed | ⚠ automated checks + global fixes; no manual AT testing |
| CMMS semantics (WR vs WO) preserved | ✅ |
| Import architecture preserved | ✅ |
| Business logic / permissions unchanged | ✅ |
| Real browser rendering + interaction | ✅ rendering (163 pages); ⚠ interactions on representative pages only |
| Build + full test suite | ✅ 0 build errors; 1579 passed / 0 failed / 0 skipped |

---

# Phase 2 — True Page-by-Page Redesign

Phase 1 (above) is preserved unchanged as the baseline. Phase 2 tried to close the gap between "shared styling" and "individually reviewed pages". **It did not close it for all 227 pages.** The numbers below are what was actually achieved.

## Coverage (see `PEMS_PAGE_INVENTORY.md` for the per-page table)
| Metric | Count |
|---|---|
| Total user-facing pages | 227 |
| Pages rendered in EN + FA (automated LTR, RTL, 8 widths) | 182 (includes record-id pages rendered with temporary records) |
| Pages with rebuilt structure (any phase) | 24 |
| Pages I looked at in screenshots | 25 |
| Pages with a real interaction / POST flow exercised | 6 (plus shell interactions on the Equipment index) |
| **Accepted** (all gate criteria met) | **1** (Letter/Create) |
| Redesigned but pending acceptance | 20 |
| Shared styling only (not individually redesigned) | 161 |
| Blocked / not rendered | 45 (need records that could not be created safely: VDR items, permits, MOC, MDB, NCR/ITP, production runs, import-batch reviews, ...) |

Phase 2 did **not** meet the criterion "every page has an explicit final status of Accepted": by the gate's own rules almost no page qualifies (localization, visual review and interaction tests are missing for most pages).

## What was done in Phase 2
1. **Letter Create (special repair).** The 15 "icon-only" buttons and 7 "unlabeled" fields were the Quill rich-text editor's toolbar (icon buttons, hidden selects, link tooltip input), not the form. Fixed at the source: every toolbar control, picker, hidden select, tooltip input and the editing area now has a localized accessible name (`Rte_*`); every form label is bound to its field (`for`/`id`; previously none were); the page and option texts are localized (`Lt_*`; stored option *values* unchanged); "More details" exposes `aria-expanded`. Two real layout defects were found while rendering and fixed: the editor was rendered **detached below the card**, and Quill's `height:100%` overflowed onto the buttons below. Verified in EN/FA at 1440/1024/820/390. The same shared partial serves Letter Details (edit) and Reply.
2. **Admin hidden duplicate `h2`.** The 12 admin heroes contained a decorative icon and an `<h2>` repeating the page title (hidden by CSS in Phase 1). Both were removed from the markup (the layout `h1` is the single title; the hero keeps its description/actions) and the hiding CSS was removed. Nothing there carried accessibility semantics the `h1` does not already provide.
3. **Localization (shared point).** `_PageHeader` and `<title>` now localize every controller-set page title and module label (218 strings added to `SharedResource.fa.resx`, guarded by a test). Persian pages no longer show English page titles. Fully localized views: Letter Create, Engineering Document Create, Equipment index, Work Order index, Asset index (list part), Letter index (top), passport. **Hard-coded UI strings remaining: about 4950 literal text nodes/attributes across 198 pages** (estimated by a text-node scan); 29 pages have <=3 literals; 24 views use `Localizer`. Localization is *partially* done, not "substantially completed".
4. **Structural re-composition.** Work Order index (KPI cards -> chip strip whose status chips are real filters; filters + one search + primary action on one toolbar row), Letter index (quick-search card + five KPI cards + stray button -> chip strip + one toolbar; selected folder no longer a second filled primary), Asset index (KPI box containing the create button -> chips + toolbar). Searches consolidated: Equipment (Phase 1), Letter, Work Order, Asset. KPI structures redesigned: 4 (Work Order, Letter, Asset, Equipment). Forms redesigned: 2 (Engineering Document create, Letter create). Not re-composed: the other ~150 pages (dashboards such as Reliability / CMMS Operations / KPI, master-data pages, detail pages).
5. **Record-id pages.** The isolated database was seeded (equipment, work requests, work order) and records were created through the real forms (work request, engineering document, vendor, letters). About 30 more pages (Work Order / Work Request details, Letter details / reply, Employee details, Vendor details / edit, Engineering Document details / edit, Users edit, reliability details, relationship explorer, ...) were rendered and audited in EN/FA at 8 widths: no overflow, no missing labels/names, no route text.
6. **POST flows exercised** (real HTTP with antiforgery and stub identities):

| Flow | Result |
|---|---|
| Work Request create, valid | 302 -> Details |
| Work Request create, empty title | 302 back to Create (service error via TempData) |
| Work Request create as Viewer | 403 |
| Engineering Document create, valid | 302 -> list |
| Engineering Document create, empty | 200 + "The Title field is required." |
| Engineering Document create as Viewer | 403 |
| Vendor create, valid | 302 -> list |
| Vendor create, empty name | **500** (backend, see below) |
| Letter create, valid | 302 -> Details |
| Letter create, empty subject/sender | **500** (backend, see below) |
| Equipment create as Viewer | 302 to list (gated) |

## Visual QA: real findings
- Letter Create: editor detached below the card and then overlapping "More details" / Save (both fixed and re-rendered).
- Correspondence: two filled amber blocks competing (Compose + the selected folder); the selected folder is now a quiet marker.
- Admin Users / Roles: hero block, card-in-card and blue accents (Phase 1); verified compact and amber in EN.
- Work Order details (viewed, **not** redesigned): stacked cards with English and Persian text mixed in one language mode, and a Persian stage diagram on the English page. Needs a dedicated localization + composition pass.
- Topbar at 820px: the search box slightly overlaps the brand (pre-existing, not fixed).

## RTL / LTR, responsive, interaction results
- Automated for all 182 rendered pages, EN and FA, at 1440/1024/820/768/600/480/390/360: **0 pages with document-level or element overflow, 0 unlabeled inputs, 0 icon-less status badges, 0 duplicate titles, 0 route text, 0 multi-h1**; 1 icon-only control without a name remains (Relationship gap report).
- Shell (Phase 1 fixes re-verified in Chrome, EN and FA): sidebar hover slide-open/close, tablet hamburger, no 390px overflow.
- Interactions exercised: shell / dropdown / modal / search focus (Equipment index), passport tabs / chips / dialog, and the POST flows above. Not exercised: sorting, pagination, filter submit, workflow buttons on detail pages.

## Backend / Infrastructure Issues Discovered (not changed)
| Issue | Evidence | Page | Severity | Blocks UI acceptance? | Follow-up |
|---|---|---|---|---|---|
| Vendor create has no server-side validation | POST with empty `Name` -> HTTP 500 (`VendorController.Create` calls the service without a `ModelState` check) | Vendor/Create | Medium (browser `required` normally prevents it) | No | Validate the model and re-show the form |
| Letter create has no server-side validation | POST with empty subject/sender -> HTTP 500 (`LetterController.Create`) | Letter/Create | Medium | No | Same |
| Asset / Instrument controllers have no module-permission check | Any signed-in role can open `/Asset`, `/Instrument` (Phase 1 evidence); Equipment *create* is gated (302), Equipment index/details are open | Asset, Instrument, Equipment | High (authorization) | Not for UI; yes for a security sign-off | Add `HasPermissionAsync` to the actions |
| `GET /AI/Test` returns 500 for the stub identity | Harness probe | AI/Test | Low | No | Investigate |
| Runtime assets loaded from public CDNs | see next section | many | High for offline use | Yes for offline acceptance | Vendor into `wwwroot/lib` |
| Engineering Documents actions lack server-side permission checks | Phase 1 audit | ED | Medium | No | Add checks |

## Offline dependencies (external runtime assets)
Scan of views, CSS and JS for `http(s)://` runtime assets:

| Asset | Where | Offline impact |
|---|---|---|
| Bootstrap Icons CSS (cdnjs 1.11.3) | `_Layout`, `_AuthLayout` | **Every icon in the app disappears offline**, including the status icons added in Phase 1 |
| SignalR client (cdnjs 8.0.7) | `_Layout` | Live notifications stop |
| Quill 1.3.7 CSS + JS (cdnjs) | `_RichTextEditorScripts` (Letter create / details / reply) | Rich-text editor fails |
| Chart.js 4.4.4 / 4.4.1 (cdnjs) | Home, 5 Report pages | Charts fail |
| pdf.js 4.0.379 + worker (cdnjs) | VDR Details, Engineering Document Details | PDF viewer fails |
| External web fonts / images / other CSS | none found | none |

Bootstrap, jQuery and jquery-validation are served locally from `wwwroot/lib`. Not changed in this phase (infrastructure decision).

## Remaining
- About 198 pages still contain hard-coded UI strings (localization is the biggest gap); 45 pages were not rendered; about 161 pages have only shared styling; dashboards (Reliability, CMMS Operations, KPI, Reports) were not re-composed; Work Order details, Employee details and other long detail pages were reviewed visually but not redesigned; per-page acceptance is not complete. Acceptance-grade coverage is **1 page**; structural redesign covers about 24 pages.
- No Export Center exists (documented as a product gap); the Import Center is unchanged.
- Commit: NOT CREATED. Push: NOT PERFORMED.

---

# Phase 3 — Complete Acceptance, Localization and Language Purity

## Executive Summary
Phase 3 did **not** reach its definition of done. Of 227 pages, **1 is Accepted, 45 are Blocked (not renderable without fixtures I could not create safely), and 181 are Incomplete** with the missing gates listed per page in `PEMS_PAGE_INVENTORY.md`. The user's rule "every page Accepted or Blocked" is therefore **not satisfied**. What Phase 3 did achieve is a measurable, repeatable language-purity baseline, bulk localization of the UI text that could be converted safely, real offline hardening, the 820px topbar fix, and a ratchet test that stops literal UI strings from growing.

## 227-Page Coverage
| Item | Count |
|---|---|
| Total user-facing pages | 227 |
| Accepted | 1 |
| Blocked | 45 |
| Incomplete (explicit missing gates) | 181 |
| Remaining incomplete (= not Accepted and not Blocked) | 181 |
| Structurally redesigned (any phase) | 24 |
| Visually inspected (screenshots) | 25 |
| Runtime rendered in EN + FA (LTR/RTL, 8 widths) | 182 |
| Interaction / POST flow exercised | 6 |
| Language-pure in English (measured) | 111 |
| Language-pure in Persian (measured) | 23 |
| Language-pure in both | 19 |

## Localization Coverage
- Bulk conversion of literal text to `Localizer` keys: **about 3,170 replacements across roughly 215 view files** (three passes), 700+ English keys given Persian values, 300 Persian-literal keys given English values (plus self-mappings), page titles/section labels (218), shell strings (theme names, language/passkey/profile menu), sidebar (65 items).
- Hard-coded UI strings, same estimator as Phase 2: **~4,950 -> 1972** (across 150 pages). This is a real reduction of about 60 percent, not completion. Pages with <=3 literals: 77.
- Not converted: multi-line paragraphs and long developer-authored Persian/English explanatory notes, text mixed with Razor expressions, text inside `@{ }` blocks, JavaScript-generated strings, controller `TempData`/validation messages. Those need per-string human translation.

## Language Purity Results (rendered DOM, 191 page files per language)
Method: every page was rendered in English and Persian; a headless-Chrome scan checks every element's own text in the main area, topbar and sidebar. English pages: any Persian character is a violation. Persian pages: an English word (>=3 letters, acronyms excluded) is a violation. Excluded as legitimate: table data cells, identifiers, emails, role tokens, language names in their own script, inputs, code, and master data that appears as option/data values (project and discipline names, statuses).
- **English:** 111 pages clean; the baseline before Phase 3 was 2 clean pages and 1,508 violations. Remaining ~286 violations (about 200 distinct strings) are almost all Persian help/developer notes hard-coded in views (e.g. "طبق CMMS Production Baseline ...") and Persian field labels such as "(فارسی)" on the Help admin forms.
- **Persian:** 23 pages clean; the baseline before Phase 3 was 1 clean page and about 30,000 flagged texts (mostly shell strings on every page). Remaining ~2,200 flagged texts (about 850 distinct) are long English descriptions/notes, KPI captions, table headers and form labels in views not yet converted, status values displayed raw (Draft, Approved, ...), and mixed strings.
- A page counts as language-pure only when both scans are 0; 19 pages qualify, but only 1 of them also has the other acceptance evidence.

## English LTR / Persian RTL Results
All 182 rendered pages were loaded in both languages at 1440/1024/820/768/600/480/390/360 px: 0 document-level or element overflow, 0 unlabeled inputs, 0 icon-less status badges, 0 duplicate titles, 0 route text, 0 multi-h1, 0 JavaScript errors (the earlier Home "Chart is not defined" error was the CDN dependency; Chart.js is now local). One icon-only control without a name remains (Relationship gap report).

## Responsive Results
Topbar: measured brand / search / tools rectangles at 8 widths in EN and FA - no overlap and no page overflow (the 820px overlap is fixed at its cause: the three groups now share the row with a flexing, capped search box). Sidebar hamburger and hover behavior were re-verified in Phase 2.

## Interaction QA
Exercised this phase: real POST flows (work request, engineering document, vendor, letter create) and rendered record pages created through them; shell interactions from Phase 2. Not exercised: sorting, pagination, filter submit, workflow buttons, drawers on most pages.

## Dashboard / Form / Table / Search Review
No new dashboard was re-composed in Phase 3 (Reliability, CMMS Operations, KPI, Reports still only inherit shared styling and the localization above); Work Order Details was localized in its visible chrome (labor/spares/skills/materials, action button) but not re-composed and still contains long Persian notes in English mode. No additional forms/tables/searches were redesigned beyond Phase 2. These reviews remain **incomplete**.

## Offline Dependency Audit
Moved to local files (same versions): Bootstrap Icons CSS + fonts 1.11.3, Quill 1.3.7 (CSS+JS), Chart.js 4.4.4 (Home) and 4.4.1 (5 report pages), SignalR 8.0.7. **Remaining external: pdf.js 4.0.379 (VDR Details and Engineering Document Details).** Finding: the URL those pages request (`.../pdf.js/4.0.379/pdf.min.js`) returns **HTTP 404** - pdf.js 4.x is ESM-only - so the in-page PDF viewer is broken today even online (pre-existing defect); fixing it needs an ESM rewrite of two viewer scripts and was not attempted. No external fonts or images. Bootstrap, jQuery and jquery-validation were already local.

## Security Findings
Unchanged from Phase 2 (Asset/Instrument controllers lack module permission checks; Engineering Document action gaps). Not changed here; documented as backend issues.

## Blocked Pages
45 pages could not be rendered because they need records or flows I could not create safely in the isolated database (VDR items, permits, MOC, MDB, NCR/ITP, production runs, import-batch reviews, transmittals, meetings, store requisitions, technical queries, ...). Required fixture per page = the entity that page displays plus its parent (see inventory). This is a fixture-effort limit, not a design decision.

## Remaining Defects
- 181 pages Incomplete (missing gates listed per page); 45 Blocked.
- Persian/English text still hard-coded in views (~1972 literals; ~200 distinct Persian and ~850 distinct English UI strings flagged in rendered pages).
- Master-data names (disciplines, projects) and status values are stored in English and displayed raw in Persian mode (needs localized master data / a status-label resource layer).
- pdf.js viewer 404 (pre-existing).
- Backend issues from Phase 2 unchanged.

## Build Result / Test Result / Graphify Result / Commit Status
See the final response of this phase. Commit: NOT CREATED. Push: NOT PERFORMED.
