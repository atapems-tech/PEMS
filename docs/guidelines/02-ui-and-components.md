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

Where components live **today**: `PEMS.UI` (registry below; `PEMS.Web` references it and finds its views and `_content/PEMS.UI/` assets automatically), plus `_PageHeader`, `.vs-*` workspace CSS, `pems-ui.js` and the tokens `--pems-*` in `PEMS.Web`.
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
| `PemsField` (`field`) / `<pems-field asp-for>` | label, help, error and one input (text, textarea, number, date, select, checkbox, file) | `FieldDescriptor` (Label, HelpText, Placeholder, Options: TranslatableText), `Value`, `Error: TranslatableText` | spike |
| `PemsTable` (`table`) | read-only table; rows rendered in this one component | `ViewDescriptor` (Title, EmptyText, `ColumnDescriptor` Header/Kind/TrueText/FalseText), `Rows` (values of any type, formatted by the locale) | spike |
| `PemsForm` (`form`) | form from a `FormDefinition` with antiforgery and upload | `FormDefinition` (Title, SubmitLabel), `FormState` (values, error texts), `RequestToken` | spike |
| `PemsDescriptorHost` | render any registered component by key from data (BPMS, previews) | `Key`, `Parameters` | spike |
| `_KpiCard` partial | KPI tile (`<partial name="_KpiCard" model="kpi" />`) | `KpiCardViewModel` (Title, Count, IconClass, ColorClass, Url, Hint) | **ready**, lives in `PEMS.UI/Views/Shared` |
| `_Pagination` partial (adapter) → `_PemsPagination` (PEMS.UI) | Footer of every list: range text, rows-per-page selector (only with `GridKey`), page links, RTL arrows. Call `<partial name="_Pagination" model="..." />` with a `PaginationViewModel` | `PaginationModel` (Page, TotalPages, TotalCount, BaseUrl, ExistingQueryString, GridKey, PageSize, AllowedPageSizes) in `PEMS.UI.Abstractions`; texts are `PaginationTexts` (`TranslatableText`s). `PEMS.Web.ViewModels.PaginationViewModel` derives from it and defaults the sizes | **ready**; markup lives in `PEMS.UI/Views/Shared/_PemsPagination.cshtml`, `_Pagination` only fills the texts from `SharedResource`. Counts keep Latin digits for now (golden-proven identical); Persian digits there is a separate owner decision |
| `_PemsLoadingOverlay` partial | global loading overlay (in the layout; driven by `pems-loading.js`) | `ViewData["LoadingLabel"]` | **ready**, lives in `PEMS.UI/Views/Shared` |
| `<pems-list-page>` and `<pems-list-*>` tag helpers (SCRUM-337) | **every record list page**: full-window frame, one icon toolbar (search over the title, filters dropdown with badge, KPIs on demand, Excel, reset columns, New), chips, scrolling table, empty state, pager slot. The page keeps its own columns, filters, rows, actions and permissions | `PemsListTexts` (localised once by the host: `ListPageTexts.From(Localizer)`); page texts as attributes; CSS/JS `_content/PEMS.UI/pems-list.css` / `pems-list.js` | **ready** (wave 0: Engineering Documents, VDR); usage and waves: [LIST_PAGE_COMPONENT.md](../design/LIST_PAGE_COMPONENT.md) |

Still in `PEMS.Web/Views/Shared` (`_PageHeader` has no callers; the owner decides whether it moves or goes to `legacy-trash/`). Planned and not yet built: modal, tabs, stepper, empty/error states, data grid (SCRUM-352 and following).

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
