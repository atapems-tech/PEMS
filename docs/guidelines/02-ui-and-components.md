# 02 - UI and components

Read for any view, page, CSS, JS, form, table, modal, dashboard. Detailed standards this chapter points to:
[PEMS_UI_ENGINEERING_STANDARD.md](../design/PEMS_UI_ENGINEERING_STANDARD.md) (behaviour),
[WORKSPACE_DESIGN_SYSTEM.md](../design/WORKSPACE_DESIGN_SYSTEM.md) (look),
[COMPONENT_LIBRARY_PLAN.md](../design/COMPONENT_LIBRARY_PLAN.md) (the `PEMS.UI` plan), [UI_PATTERN_AUDIT.md](../design/UI_PATTERN_AUDIT.md) (current counts).

## 1. The core rule: call and modify, don't re-write [R26]

> Do **not** hand-write a new form control, table, modal, badge, empty state, KPI card or page header inside a page.
> **Find the existing component, call it, and modify it** (parameters, options, slots, a new variant) for the new need.
> If it cannot express the need, **extend the component first** (in the library, with docs and tests), then use it.

Decision order, every time:

1. Is there a component/partial/View Component/template for this? -> use it as is.
2. Does it almost fit? -> add a parameter/variant to the shared component (backwards-compatible for existing callers), update its catalog entry and tests, then call it.
3. Is it truly missing? -> add it to the library (`PEMS.UI` once it exists; until then the shared partials in `Views/Shared` and the shared CSS/JS) following section 4, then call it. Mention it in the Jira comment.
4. Only if 1-3 are impossible: page-local markup with a written reason in the task comment. CI ratchet counts raw markup; the numbers may only go down.

**Component inputs and names (owner decision 2026-10-27).** A component takes **one light-weight object with named properties**, never positional arguments: `new ButtonOptions { Label = "Save", Tone = Tone.Danger }`, not `new ButtonOptions("Save", Tone.Danger)`. Options are `sealed record` types with `init` properties (`required` for what must be given), so they are immutable, vary with `with`, deserialize from the same JSON a database row will hold, and cost the same as a constructor call (no performance effect; the render-time test in `PEMS.UI.Tests` stays green). Use the same property name for the same idea in every component:

| Idea | Name | Idea | Name |
| --- | --- | --- | --- |
| visible text of a button, badge, field, option | `Label` | heading of a state, view or form | `Title` |
| second line of explanation | `Description` | technical or secondary reason | `Detail` |
| help under a field / hint inside it | `HelpText` / `Placeholder` | link target | `Href` |
| colour meaning / size | `Tone` / `Size` | a call to action (link with `Label`, `Href`) | `Action` |
| icon-font class | `Icon` | on/off flags | `Required`, `ReadOnly`, `Disabled`, `Sortable` |
| what a field or column is | `Kind` | identifies a field / column / definition | `Name` / `Key` / `Id` |

**Component manifest (SCRUM-433).** Every registered component carries `@attribute [PemsComponent("<key>", Category = "...", Icon = "bi-...")]` and gets a machine-readable manifest generated from its code (`ComponentManifestGenerator`, `ComponentManifestCatalog`): key, category, icon, message key `component.<key>` (no visible text in the component), the JSON Schema of its options (from the options records; texts accept a string or a language map; unknown properties are rejected) and a sample. Stored options JSON is validated with `ComponentManifestCatalog.Validate(key, json)` (field errors with JSON Pointer and constraint code) and turned into parameters with `ToParameters`. A new component needs the attribute, a sample in `ComponentSamples`, and its approved snapshot in `PEMS.UI.Tests/Manifests` (record with `PEMS_GOLDEN_RECORD=1`); a test fails when any of them is missing.

Registry and JSON keys are the component name without the `Pems` prefix, in kebab-case (`empty-state`, `error-state`, `loading-state`, `button`, `badge`). A new component reuses these names; add a new name here only when no existing one fits. (The Titan project's component names are not in this repository; if the owner supplies that list, align the table to it before the next component.)

Where components live **today**: `PEMS.UI` (registry below; `PEMS.Web` references it and finds its views and `_content/PEMS.UI/` assets automatically), plus the `_PageHeader` adapter, `.vs-*` workspace CSS and `pems-ui.js` in `PEMS.Web`; the Light and Dark `--pems-*` tokens live in `PEMS.UI/wwwroot/tokens.css` (Turquoise: still `site.css`/`pems-glass.css`, task SCRUM-476).
Search `PEMS.Web/Views/Shared`, `wwwroot/css`, `wwwroot/js` before building anything. Once `PEMS.UI` ships (plan #319-#334), it is the only place; this chapter then shrinks to "use PEMS.UI".

### Component registry (check this before writing any markup)

Components of `PEMS.UI` ([ADR-0007](../../plan/adr/0007-component-technology-static-razor-components.md), SCRUM-23). **Status: spike** means it exists and is
tested but `PEMS.Web` does not reference `PEMS.UI` yet (SCRUM-29); until then pages still use the shared partials above, but **extend the
component listed here instead of writing a second one**. When SCRUM-29 lands, the status becomes **ready** and pages must use it.
Add a row in the same PR as every new component or parameter.

| Component (key) | Use it for | Inputs | Status |
|---|---|---|---|
| `PemsButton` (`button`) | button or link-button | `ButtonOptions(Label: TranslatableText, Tone, Size, Disabled, Type, Href)` | spike |
| `PemsBadge` (`badge`) | status pill | `BadgeOptions(Text: TranslatableText, Tone)` | spike |
| `PemsSkeleton` (`skeleton`) | loading state | `Lines`, `Label` (accessible name, TranslatableText) | spike |
| `PemsField` (`field`) / `<pems-field asp-for>` | label, help, error and one input (text, textarea, number, date, select, checkbox, file, **richText**: a textarea that `pems-rte.js` turns into the editor; the server must pass the posted value through `FormValueSanitizer`, SCRUM-59) | `FieldDescriptor` (Label, HelpText, Placeholder, Options: TranslatableText), `Value`, `Error: TranslatableText` | spike |
| `PemsTable` (`table`) | read-only table; rows rendered in this one component | `ViewDescriptor` (Title, EmptyText, `ColumnDescriptor` Header/Kind/TrueText/FalseText), `Rows` (values of any type, formatted by the locale) | spike |
| `PemsForm` (`form`) | form from a `FormDefinition` with antiforgery and upload | `FormDefinition` (Title, SubmitLabel), `FormState` (values, error texts), `RequestToken` | spike |
**Alerts in views (SCRUM-481):** write `<pems-alert tone="danger" css="py-2 px-3 small">page markup</pems-alert>` (tones: info, success, warning, danger, primary, secondary; `tone` may be a Razor expression). Never write `class="alert alert-*"` by hand. Not yet moved: `asp-validation-summary` blocks, `alert-light`/`role="note"` blocks, the layout toasts and the `alert(` calls (follow-up).

**Skin (SCRUM-480):** alert, modal, drawer, confirm-dialog, tabs and breadcrumb take `Skin` in their options. The default `ComponentSkin.Bootstrap` renders the Bootstrap markup the pages use today (`alert`, `nav nav-tabs`, `modal fade`, `offcanvas`, `breadcrumb`, with `data-bs-*` behaviour), so replacing a hand-written block changes nothing visible; `ComponentSkin.Pems` renders the library's own `pems-*` classes and native `<dialog>`. Adopt with the default.

| `PemsAlert` (`alert`) | toast or inline notice; danger/warning announce as alert, trace id + copy button | `AlertOptions(Title, Message, Tone, TraceId, CopyLabel, DismissLabel)` | spike |
| `PemsModal` (`modal`) / `PemsDrawer` (`drawer`) / `PemsConfirmDialog` (`confirm-dialog`) | native `<dialog>`: one modal for many rows (`ButtonOptions.Opens` + `RowKey`), side panel, confirm with an optional required reason | `ModalOptions`, `DrawerOptions`, `ConfirmDialogOptions(Id, Action, Title, Message, ConfirmLabel, CancelLabel, ReasonLabel)`; script `pems-overlay.js` | spike |
| `PemsTabs` (`tabs`), `PemsBreadcrumb` (`breadcrumb`), `PemsStepper` (`stepper`), `PemsMenu` (`menu`), `PemsSidePanelNav` (`side-panel-nav`) | navigation parts | `TabsOptions`, `BreadcrumbOptions`, `StepperOptions`, `MenuOptions`, `SidePanelNavOptions` | spike |
| `PemsFormSection` (`form-section`), `PemsActionBar` (`action-bar`), leave guard | titled field group; button row (sticky option); `FormDefinition.WarnOnLeave` + `pems-form.js` | `FormSectionOptions`, `ActionBarOptions` | spike |
| `PemsDescriptorHost` | render any registered component by key from data (BPMS, previews) | `Key`, `Parameters` | spike |
| `_KpiCard` partial | KPI tile (`<partial name="_KpiCard" model="kpi" />`) | `KpiCardViewModel` (Title, Count, IconClass, ColorClass, Url, Hint) | **ready**, lives in `PEMS.UI/Views/Shared` |
| `_Pagination` partial (adapter) → `_PemsPagination` (PEMS.UI) | Footer of every list: range text, rows-per-page selector (only with `GridKey`), page links, RTL arrows. Call `<partial name="_Pagination" model="..." />` with a `PaginationViewModel` | `PaginationModel` (Page, TotalPages, TotalCount, BaseUrl, ExistingQueryString, GridKey, PageSize, AllowedPageSizes) in `PEMS.UI.Abstractions`; texts are `PaginationTexts` (`TranslatableText`s). `PEMS.Web.ViewModels.PaginationViewModel` derives from it and defaults the sizes | **ready**; markup lives in `PEMS.UI/Views/Shared/_PemsPagination.cshtml`, `_Pagination` only fills the texts from `SharedResource`. Persian pages show Persian digits in the range text, page numbers and page-size buttons (`IPemsLocale.Digits`); hrefs and aria-labels stay Latin, English output is unchanged (SCRUM-426) |
| `_PageHeader` partial (adapter) → `_PemsPageHeader` (PEMS.UI) | One compact title row: page title + muted module label (only when it differs). `<partial name="_PageHeader" />` reads `ViewData["Title"]`, `["Section"]`, `["HidePageTitle"]` and localizes them | `PageHeaderModel` { `Title`, `Section` } (`TranslatableText`) in `PEMS.UI.Abstractions`; no URL, route or breadcrumb (guard: `GlobalPageHeaderTests`) | **ready** (SCRUM-425). The layout does NOT render it (removed on owner request: pages use the whole content area); a page that wants the row calls the partial itself (`@await Html.PartialAsync("_PageHeader")` after its `@{ }` block). Callers so far (owner: add where a page shows no heading): Finding/Index, ImportCenter/Index, ShiftHandover/Create, CriticalityAssessment/Create, Barcode/Verify |
| `_PemsState` partial / components `PemsEmptyState`, `PemsErrorState`, `PemsLoadingState` | Nothing to show yet (empty), a failed load (error, role=alert, optional retry) and a pending load (busy status, optional placeholder lines). Page: `<partial name="_PemsState" model="new EmptyStateOptions(...)" />`; component/JSON keys `empty-state`, `error-state`, `loading-state` | `EmptyStateOptions`, `ErrorStateOptions`, `LoadingStateOptions` (+ `StateAction` link) in `PEMS.UI.Components`; all texts `TranslatableText` | **ready** (SCRUM-424). A page with no data, a failed load or a pending load uses these - never hand-written markup, never its own wording. No page uses them yet; adoption follows with the data-grid work (#323) |
| `_PemsLoadingOverlay` partial | global loading overlay (in the layout; driven by `pems-loading.js`) | `ViewData["LoadingLabel"]` | **ready**, lives in `PEMS.UI/Views/Shared` |
| `<pems-list-page>` and `<pems-list-*>` tag helpers (SCRUM-337) | **every record list page**: full-window frame, one icon toolbar (search over the title, filters dropdown with badge, KPIs on demand, Excel, reset columns, New, a page menu `<pems-list-menu>`; New can open the page's modal: `modal="id"`), chips, bulk bar for ticked rows `<pems-list-bulk>`, scrolling table, empty state, pager slot. The page keeps its own columns, filters, rows, actions and permissions | `PemsListTexts` (localised once by the host: `ListPageTexts.From(Localizer)`); page texts as attributes; CSS/JS `_content/PEMS.UI/pems-list.css` / `pems-list.js` | **ready** (wave 0: Engineering Documents, VDR; wave 1: Document Register, Document Revisions, Transmittal, TQ/TSR/RFI, MDB, MOC, Document Workflow; wave 2: 14 CMMS lists - Work Orders, Work Requests, Equipment, Functional Locations, Instruments, Letter Tags, Maintenance Plans and Strategies, Job Plans, Spare Parts, Permits, Inspections, Findings, Assets); usage and waves: [LIST_PAGE_COMPONENT.md](../design/LIST_PAGE_COMPONENT.md) |
| Rich-text editor (`pems-rte.js` / `pems-rte.css`, SCRUM-59) | **every formatted-text input**: a field of kind `RichText`, or `<textarea data-pems-rte>`; never a second editor. Quill 1.3.7 (vendored, BSD-3-Clause) loads only when such a field is on the page. Direction follows the page (fa RTL, en LTR) with a direction button per paragraph. `data-pems-rte-upload="<url>"` adds the image button (answer `{ url }`), `data-pems-rte-label` names the editing area (default: the field's label). Pages that cannot mark the textarea use `data-pems-rte-for="<id>"` on another element (the Letter partial `_RichTextEditorScripts` does). The textarea keeps the value and raises `rte-change`; `textarea.quillEditor` is the instance; `PemsRte.init(root)` for content added later. **The HTML is untrusted**: save it only through `IRichTextSanitizerService` (form values: `FormValueSanitizer`) | texts from the layout (`data-pems-rte-texts` on `<body>`, `RichTextEditorTexts.Json`, `Rte_*` keys), Quill folder `data-pems-rte-lib` | **ready**, loaded by `_Layout` |
| Searchable select (`pems-select.js`, SCRUM-57) | **every single select**: a `<select class="form-select">` with 8 or more options becomes searchable (Tom Select, vendored); write a plain `<select>`, never a hand-made dropdown. Large lists (vendors, part numbers, employees, equipment): `data-pems-select-source="<SearchApi type>"` loads from the permission-aware `/SearchApi` as you type. `data-pems-select` forces it on a small list, `data-pems-select="off"` keeps a plain select (a page that drives it in code); `data-pems-select-url="<endpoint>"` loads from a page endpoint (`?q=` → `{ items: [{ value, text, sub, ... }] }`, extra fields stay on the option), `data-pems-select-create` allows a typed value (`data-create-text` with `{0}`); after replacing options in code nothing is needed, after setting `.value` call `PemsSelect.refresh(select)` | texts from the layout (`Sel_*` keys on `<body>`); `data-placeholder` per select | **ready**, loaded by `_Layout` (`_content/PEMS.UI/pems-select.js` / `.css`); multi-selects keep `pems-multiselect.js` |
| Command palette (`pems-command-palette.js`, SCRUM-64) | **the one global search**: Ctrl+K / Cmd+K, the top-bar box (`data-pems-command-palette-trigger`) or any button with `data-pems-command-palette-open`. Menus from `#pemsSidebar`, actions from `/SearchApi/PaletteActions` (`CommandPaletteActions`, each with a `SearchAccessRule`), records from `/SearchApi/Palette` (`IGlobalSearchService.SearchEverywhereAsync`). Never build a second search box or result popup; add a record type as an `ISearchableEntityProvider` with its `Access` rule (the rule of the page it opens) and a `Palette_Group_<EntityType>` text, an action as a row of `CommandPaletteActions` | texts as `data-*` on the script tag (`Palette_*` keys) | **ready**, loaded by `_Layout` (`_content/PEMS.UI/pems-command-palette.js` / `.css`); `/GlobalSearch` shows the same results |

Still in `PEMS.Web/Views/Shared` (none: `_PageHeader` is now an adapter, see the registry). Planned and not yet built: modal, tabs, stepper, empty/error states, data grid (SCRUM-352 and following).

**Hydration:** descriptors serialise with `PemsJson` (`FormDefinition`/`ViewDescriptor` as one JSON document), so a definition can come from code, from a JSON file or, later, from a database form row (#387). **Shape of every component** (inputs / state / body): inputs are an immutable `*Options` record or a descriptor; state is a plain C# `*State`
class that computes CSS classes, ids and ARIA from the inputs (unit-test it without rendering); the `.razor` body only prints that state
- no queries, no service calls, no business rules in markup. A table renders all its rows in one component, never one component per cell
(measured 5 to 8 times slower). Extension packs register components with `[PemsComponent("key")]`.

## 2. Rules for new and changed UI [R26, D6-D8]

- No inline `style=`, no inline `<script>`/`<style>`, no inline `onclick`, no CDN or external URL, no hex colours: use `var(--pems-*)` tokens.
- Logical CSS only (`margin-inline-start`, `inset-inline-end`, `text-align:start`) so one stylesheet serves RTL and LTR; handle directional icons/transforms explicitly.
- **Texts in `PEMS.UI` components are inputs** ([ADR-0008](../../plan/adr/0008-component-texts-are-inputs-translatable-text.md)): label, help, placeholder, option, header, title, empty text, button text and error are `TranslatableText` parameters (JSON map of language slug to text, e.g. `{"fa":"ذخیره","en":"Save"}`), never literals or defaults in the component. A test fails the build when a literal word appears in a component. Resolve and format only through `IPemsLocale`. **Data is never translated, only formatted** (owner decisions 2026-10-27: Persian pages show Jalali dates and Persian digits, English pages ISO dates and Latin digits; nothing else for now). Formatting is for display only: a value a user types, posts or stores is never rewritten (`DataEntryUnaffectedTests`), and input fields carry ISO dates and Latin digits. Numbers and dates are **bound** by one binder (`PEMS.UI.Binding`, registered in `PEMS.Web` Program.cs): a Persian page accepts `12.5` as a browser posts it and Persian digits typed by the user; never add per-page parsing of numbers or dates (SCRUM-421). Pages in `PEMS.Web` that do not use a component yet keep localization keys (`SharedResource`); dates are Jalali in the UI (stored UTC/Gregorian).
- Component contract is technology-neutral: describe fields/columns/views as **descriptors** so the same component can be rendered from code today and from metadata tomorrow (dynamic forms/modules). Do not hard-wire a screen to one entity when a descriptor can express it.
- Use standard behaviours: loading state, empty state, error state, validation messages next to the field, state preserved on row actions (filters, sort, page, tab) - see the UI engineering standard sections 3-5.
- Popups/dropdowns/lookups: follow the portal and stacking rules; never patch `z-index` per page.
- Accessibility: labels bound to inputs, keyboard reachable, visible focus, `aria-*` on custom widgets, sufficient contrast.
- Responsive: verify at 375 / 600 / 768 / 992 / 1366 px in EN and FA when touching layout or positioning.
- JavaScript: initialise idempotently (`dataset.pemsInit`), unbind global listeners on close, namespace everything, `fetch` with antiforgery header and visible error handling.

## 3. Modifying an existing component safely

1. Find all callers (grep the partial/tag/CSS class). Note how many pages depend on it.
2. Add the new behaviour behind an optional parameter or variant; default output must stay byte-identical for old callers. **Prove it with a golden-output test** (`PEMS.Tests/Ui/SharedPartialsGoldenTests.cs`, files in `PEMS.Tests/Ui/Golden/`): record the HTML of the unchanged code with `PEMS_GOLDEN_RECORD=1`, make the change or move the file, and the test must pass without re-recording. Moving a partial between projects follows the same rule (`git mv`, keep the view-model namespace, no caller edits).
3. Update the component's documentation (states, RTL/LTR example, accessibility note) and tests.
4. Check at least two existing callers in a browser after the change.
5. If the change alters defaults for everyone, that is a separate Jira issue with the user's approval.

## 4. Adding a component (when 1.3 applies)

A component is done only with: documented states (default, hover, focus, disabled, loading, error, empty), RTL + LTR example, accessibility statement, localization keys, token-only styling, tests, a catalog entry, and no dependency on the page it came from. Fix shared components once - never patch page by page.

## 5. Verifying UI work

Run the app, open the page in a browser, exercise the golden path and one edge case, in `fa` and `en`, desktop and mobile width; read the console (no errors) and network (no external hosts). Report what you saw. If you could not run it, say so - do not claim it works.

## 6. Dynamic builders (forward look)

Form/module/workflow designers are embedded JavaScript components inside Razor pages [D1]. Anything the designers produce renders through the **same** components as hand-built pages. Therefore: any component you build today should accept data-driven configuration, not just compile-time Razor parameters.
