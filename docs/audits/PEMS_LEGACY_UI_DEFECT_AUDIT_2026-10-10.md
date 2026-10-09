# PEMS — Legacy UI defect audit (2026-10-10)

Method: real browser (Playwright's Chromium driven over CDP by the new dependency-free harness `tools/UiAudit`), clone database `PEMSDB_PcatClone`, TEST-PCAT accounts. Every defect below was **reproduced in a running page**; static searches were used only to find candidates. Nothing here was verified against production data. Nothing was committed or pushed.

## 1. Browser-testing and UI skills available
* Repo guidance: `docs/design/WORKSPACE_DESIGN_SYSTEM.md` (rule: *fix a shared component once*), `PEMS_SITE_WIDE_UIUX_REDESIGN_FINAL_REPORT.md`, `PEMS_PAGE_INVENTORY.md`, `.claude/skills/{ui-ux-pro-max,design,design-system,ui-styling}`, `.claude/Role/…Role-Based E2E Testing Framework…` (names Playwright; no suite exists in the repo).
* Tooling found: Playwright's Chromium (`%LOCALAPPDATA%\ms-playwright\chromium-1243`), Node 24. **No Playwright npm package installed** (and none was needed). New: `tools/UiAudit/{cdp,probes,audit}.mjs` — a zero-install crawler that judges popups by real hit-testing (`elementFromPoint`) and takes the page inventory from the signed-in role's own sidebar.
* Coverage limits: the existing TEST-PCAT accounts reach 74 pages (EngineeringManager), 10 (WH.Manager), 3 (MNT.Manager). **Admin-only pages (SystemAdmin) were not audited**: a clone-only SystemAdmin audit account was proposed and refused by the auto-mode classifier; no account was created. Decision needed from the owner if those pages should be audited.

## 2. Internal reference implementations
* **Portal lookup** (`pc-lookup`, `pems-part-catalogue.js/.css`): list moved to `<body>`, `position:fixed`, coordinates from the input's rect, scroll/resize listeners added on open and removed on close, ARIA combobox, Escape does not close a parent modal. Verified correct in an earlier task; it is page-scoped and should be promoted to shared code when a second page needs it.
* `.vs-*` workspace layer (`pems-workspace.css`) and `pems-ui.js` (global enhancement layer).
* Native `<select>`/`<datalist>` (admin Designer, Users/Details, Authorization/Simulate) are browser-rendered and cannot clip.
* Do **not** assume the references are universally correct: the Engineering Documents list is named a reference in the design system and contains defect D1.

## 3. Inventory (static)
306 views · 15 CSS and 14 JS files in `wwwroot` · 20 shared partials · Bootstrap dropdown menus in 7 views (6 inside tables) · 66 views with a POST `<form>` inside a table-row template (row actions with full reload) · 14 views/scripts using fetch/AJAX/innerHTML · `.vs-panel{overflow:hidden}` (all cards) · `pems-ui.js` auto-wraps unwrapped tables in `.table-responsive` (overflow container) · z-index map: topbar 1030, sidebar 1035–1050, offcanvas backdrop 1040, offcanvas 1045, modal 1055, lookup 1056, search panel 1060, loading overlay 2000 · no code sets Bootstrap's Popper strategy.

## 4. Confirmed defects (reproduced)

| ID | Class | Page / route | Observed vs expected | Root cause (evidence) | Other pages potentially affected | Severity | Shared fix | Regression check |
|---|---|---|---|---|---|---|---|---|
| **D1** | A/C | `/EngineeringDocuments` (row "⋯" menu) | menu hit-test visible at 2/6 points; covered by next row (`td.text-end`) or the pagination footer; on mobile the last row's menu is entirely off-screen (0/0). Expected: fully visible | Bootstrap positions the menu `absolute` inside `.pems-grid-scroll` (overflow auto) inside `.vs-panel` (overflow hidden); no Popper strategy configured anywhere. **Proven fix in the live page:** `bootstrap.Dropdown.Default.popperConfig = {strategy:'fixed'}` → 9/9 visible on first/second/last row at 1366 px and 375 px (screenshots checked) | every dropdown inside a card/scroll container (Letter, DocumentRegister, Team details, any future list) | High (hides row actions incl. Delete) | one default in `pems-ui.js` | UiAudit `dropdown-clipped-or-obscured`; static guard test |
| **D2** | C/G | every page, **Persian/RTL only** — contextual Help drawer | right 64 px of the drawer is covered by the sidebar rail (hit 6/9; topmost = `nav.pems-sidebar`). English unaffected (sidebar on the other side) | `#pemsHelpDrawer` is rendered **inside `<header class="pems-topbar">`** (`z-index:1030` ⇒ stacking context); the drawer's `z-index:1045` only counts inside it, so the sidebar (1035) paints above it. In the RTL build `offcanvas-start` sits on the right, where the sidebar is. **Proven:** moving the element to `<body>` → 9/9 visible | any offcanvas/modal rendered inside the header; Help is the only one found | Medium (covers the drawer edge/close control area in Persian) | render the drawer outside `<header>` (layout) | UiAudit `offcanvas-obscured` (fa); static guard test |
| **D3** | E/G | any page that inserts content after load (14 views/scripts use fetch/AJAX/innerHTML) | inserted icon-only button has no accessible name; inserted badge gets no status icon; **an inserted wide table is not wrapped and causes page-level horizontal scroll** | `pems-ui.js` enhances the DOM once at load; nothing calls `PemsUi.refresh` (grep) | all AJAX-driven partials | Medium | `MutationObserver` in `pems-ui.js` (debounced) | browser probe above; static guard |
| **D4** | F | `/MyWork` (new page of the previous task) | two `<h1>` (global header + my section title) | my own view used `<h1>` | — | Low | `<h2>` | UiAudit `h1-count`; unit guard (one h1 per view) |
| **D5** | F/G | `/Warehouse/PartCatalogueRequest/Details/18` (and `Edit`) | two `<h1>` ("Part Catalogue Request" from the global header + the request number) | the views render their own `<h1 class="pems-page-title">` without `ViewData["HidePageTitle"]` (found by UiAudit `h1-count`; the CSS keys on the class, not the tag) | only other view with its own `<h1>` is `EngineeringDocuments/Details` (already sets `HidePageTitle`) | Low | `<h2>` (no visual change) | UiAudit `h1-count`; guard test "a view with its own h1 must hide the global title" |
| **D6** | D | `/MaintenanceMasterData` (13 tabs, every row action) | act on the **Skills** tab (toggle) -> page reloads on the **first** tab (`#tab-groups`) although the action succeeded; the user loses their place after every action | row forms POST, controller `RedirectToAction(nameof(Index))`, no tab state. Reproduced in the browser (toggled twice, data unchanged) | every tabbed page with row/POST actions (66 views post row forms) | Medium | remember the active tabs on POST submit and restore after the redirect (same path, 20 s window, never over a `#hash`) in `pems-ui.js` | browser before/after + guard test; negative checks: a fresh visit opens the first tab, blocked `sessionStorage` does not break the page |
| **D7** | G | every page, 576-767 px (reproduced at 600 px; 375/768/992/1366 fine) | page-level horizontal scroll: the topbar's user-menu group is pushed past the right edge | the name/role block uses `d-none d-sm-flex` (visible from 576 px) and a long name does not fit the topbar; present with the old `absolute` strategy too (not caused by the dropdown change) | all pages (shared layout) | Medium | show the name block only from `md` (768 px) | UiAudit over 7 widths x 5 pages x EN/FA: 0 findings; guard test |
| **D1b** | A/G | topbar Notifications menu, discipline filter menu (`/DocumentRegister`) at 375-768 px | **side effect of the Popper `fixed` default, found by the before/after comparison**: Notifications menu grew to ~583 px (no width of its own) and started at x = -55 / -126; the 585 px discipline menu was wider than a phone (with `absolute` it silently widened the page to 614 px) | `fixed` sizes menus against the viewport, `absolute` against their narrow container | every menu wider than its container | Medium | viewport cap on `.dropdown-menu`, defined width for `#notifDropdownBody`, full-width sheet for topbar menus < 768 px (`pems-workspace.css`) | measured at 375/600/768/992/1366 px, EN+FA: every menu 9/9 visible; 1366 px unchanged; guard test |

## 5. Suspected / not reproduced as defects
* `/Equipment` took 22 s on desktop and hit a 30 s SQL command timeout (HTTP 500) in the mobile pass; `/Instrument` timed out once. The machine was running a ~60-minute SQL-heavy test suite at the same time → **needs re-measurement on a quiet machine**; if it persists it is a query-performance defect, not UI.
* 66 views post row-level forms with full-page reload: **state loss not yet reproduced** (needs per-page checks of filter/page preservation after the redirect). Priority candidates: `MaintenanceMasterData/Index` (13 row forms), `InventoryMasterData`, `PartCatalogueAdmin`, `Employee/Department/Finding` index pages.
* Admin area (`WorkflowManagement/Designer` etc.) — not audited (see §1).

## 6. Reported by the harness but **not** defects
* Modal "focus not moved inside" / "Escape does not close" on `/Instrument` and `/Asset`: reproduced individually — focus lands in the modal, `aria-modal=true`, Escape closes. Caused by fixed sleeps on a loaded machine; harness now polls (`waitFor`).
* "Unlabeled input `q`" on 72 pages: the topbar search has a placeholder (valid accessible-name fallback); probe corrected.
* Persian `offcanvas-obscured` ×33: all one defect, **D2**.

## 7. Fix status (this task) and plan

| Batch | Defect | Change | Verified |
|---|---|---|---|
| 1 | D1 | `pems-ui.js`: Bootstrap Dropdown default `popperConfig.strategy = 'fixed'` (idempotent) | browser: `/EngineeringDocuments` first/second/last row, EN + FA, 1366 px + 375 px -> 9/9 visible, **0 audit findings** (was 7 findings per pass) |
| 2 | D2 | `_ContextualHelpDrawer` split: the button stays in `<header>` (`_ContextualHelpButton`), the drawer renders after `</header>` | browser: Persian drawer 9/9 visible (was 6/9); English unchanged |
| 3 | D3 | `pems-ui.js`: debounced `MutationObserver` that enhances inserted nodes; its own nodes carry `data-pems-auto` | browser: dynamically inserted icon button named, badge iconised, table wrapped, no page h-scroll |
| 4 | D4, D5 | `<h1>` -> `<h2>` in `MyWork/Index`, `PartCatalogueRequest/Details`, `/Edit` | UiAudit `h1-count`: no finding on these pages |
| 5 | D6 | `pems-ui.js`: tab state across POST -> redirect | browser: the Skills tab stays active; negative checks above |
| 6 | D1b | dropdown viewport cap, notifications width, topbar sheet < 768 px (`pems-workspace.css`) | the regression of batch 1 was found by comparing menu geometry with `absolute` vs `fixed` at five widths; verified after the change |
| 7 | D7 | `_Layout`: user name block from `md` | 7 widths x 5 pages x EN/FA: 0 findings |

Regression protection: `PEMS.Tests/Web/UiStandardTests` (12 structural guards), `tools/UiAudit` before/after crawls (EN + FA), 215 neighbouring Web/UI/My Work/Part Catalogue tests green. The guards are structural: they were written against the structure of the original files (no Popper default anywhere, drawer call inside `<header>`, no observer, no tab state, two `<h1>`), but were not executed against the unfixed tree; the browser before/after evidence is the proof of the behaviour.

Remaining plan:
1. **Batch 1 — D1** `pems-ui.js`: Popper `fixed` default. Test: guard + UiAudit on `/EngineeringDocuments` (en/fa, 1366/375).
2. **Batch 2 — D2** `_Layout.cshtml`: render `_ContextualHelpDrawer` after `</header>`. Test: guard + UiAudit (fa).
3. **Batch 3 — D3** `pems-ui.js`: debounced `MutationObserver` calling the existing enhancers. Test: dynamic-insert probe.
4. **Batch 4 — D4** `MyWork/Index.cshtml` `<h2>`; add "one `<h1>` per view" guard.
5. Next: reproduce state-loss on the priority row-action pages; re-measure Equipment/Instrument; decide on admin-page audit.
Regression strategy: `tools/UiAudit` (browser, en+fa, two widths) + `PEMS.Tests/Web/UiStandardTests` (static structural guards) + the standard `docs/design/PEMS_UI_ENGINEERING_STANDARD.md`.

## 8. Missing infrastructure
Nothing must be installed. Optional: a Playwright npm project if CI needs retries/traces; a dedicated SystemAdmin audit account on the clone (needs owner approval).
