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

Where components live **today** (the audit found `PEMS.UI` does not exist yet): `_PageHeader`, `_Pagination`, `_KpiCard`, `_PemsLoadingOverlay`, `.vs-*` workspace CSS, `pems-ui.js`, tokens `--pems-*`.
Search `PEMS.Web/Views/Shared`, `wwwroot/css`, `wwwroot/js` before building anything. Once `PEMS.UI` ships (plan #319-#334), it is the only place; this chapter then shrinks to "use PEMS.UI".

### Component registry (check this before writing any markup)

Components of `PEMS.UI` ([ADR-0007](../../plan/adr/0007-component-technology-static-razor-components.md), SCRUM-23). **Status: spike** means it exists and is
tested but `PEMS.Web` does not reference `PEMS.UI` yet (SCRUM-29); until then pages still use the shared partials above, but **extend the
component listed here instead of writing a second one**. When SCRUM-29 lands, the status becomes **ready** and pages must use it.
Add a row in the same PR as every new component or parameter.

| Component (key) | Use it for | Inputs | Status |
|---|---|---|---|
| `PemsButton` (`button`) | button or link-button | `ButtonOptions(Label, Tone, Size, Disabled, Type, Href)` | spike |
| `PemsBadge` (`badge`) | status pill | `BadgeOptions(Text, Tone)` | spike |
| `PemsSkeleton` (`skeleton`) | loading state | `Lines` | spike |
| `PemsField` (`field`) / `<pems-field asp-for>` | label, help, error and one input (text, textarea, number, date, select, checkbox, file) | `FieldDescriptor`, `Value`, `Error` | spike |
| `PemsTable` (`table`) | read-only table; rows rendered in this one component | `ViewDescriptor`, `Rows` | spike |
| `PemsForm` (`form`) | form from a `FormDefinition` with antiforgery and upload | `FormDefinition`, `FormState`, `RequestToken` | spike |
| `PemsDescriptorHost` | render any registered component by key from data (BPMS, previews) | `Key`, `Parameters` | spike |

Planned and not yet built: page header, pagination, KPI card (move from `Views/Shared`, SCRUM-29), modal, tabs, stepper, empty/error states, data grid (SCRUM-352 and following).

**Shape of every component** (inputs / state / body): inputs are an immutable `*Options` record or a descriptor; state is a plain C# `*State`
class that computes CSS classes, ids and ARIA from the inputs (unit-test it without rendering); the `.razor` body only prints that state
- no queries, no service calls, no business rules in markup. A table renders all its rows in one component, never one component per cell
(measured 5 to 8 times slower). Extension packs register components with `[PemsComponent("key")]`.

## 2. Rules for new and changed UI [R26, D6-D8]

- No inline `style=`, no inline `<script>`/`<style>`, no inline `onclick`, no CDN or external URL, no hex colours: use `var(--pems-*)` tokens.
- Logical CSS only (`margin-inline-start`, `inset-inline-end`, `text-align:start`) so one stylesheet serves RTL and LTR; handle directional icons/transforms explicitly.
- All text through localization keys. Dates Jalali in the UI (stored UTC/Gregorian).
- Component contract is technology-neutral: describe fields/columns/views as **descriptors** so the same component can be rendered from code today and from metadata tomorrow (dynamic forms/modules). Do not hard-wire a screen to one entity when a descriptor can express it.
- Use standard behaviours: loading state, empty state, error state, validation messages next to the field, state preserved on row actions (filters, sort, page, tab) - see the UI engineering standard sections 3-5.
- Popups/dropdowns/lookups: follow the portal and stacking rules; never patch `z-index` per page.
- Accessibility: labels bound to inputs, keyboard reachable, visible focus, `aria-*` on custom widgets, sufficient contrast.
- Responsive: verify at 375 / 600 / 768 / 992 / 1366 px in EN and FA when touching layout or positioning.
- JavaScript: initialise idempotently (`dataset.pemsInit`), unbind global listeners on close, namespace everything, `fetch` with antiforgery header and visible error handling.

## 3. Modifying an existing component safely

1. Find all callers (grep the partial/tag/CSS class). Note how many pages depend on it.
2. Add the new behaviour behind an optional parameter or variant; default output must stay byte-identical for old callers.
3. Update the component's documentation (states, RTL/LTR example, accessibility note) and tests.
4. Check at least two existing callers in a browser after the change.
5. If the change alters defaults for everyone, that is a separate Jira issue with the user's approval.

## 4. Adding a component (when 1.3 applies)

A component is done only with: documented states (default, hover, focus, disabled, loading, error, empty), RTL + LTR example, accessibility statement, localization keys, token-only styling, tests, a catalog entry, and no dependency on the page it came from. Fix shared components once - never patch page by page.

## 5. Verifying UI work

Run the app, open the page in a browser, exercise the golden path and one edge case, in `fa` and `en`, desktop and mobile width; read the console (no errors) and network (no external hosts). Report what you saw. If you could not run it, say so - do not claim it works.

## 6. Dynamic builders (forward look)

Form/module/workflow designers are embedded JavaScript components inside Razor pages [D1]. Anything the designers produce renders through the **same** components as hand-built pages. Therefore: any component you build today should accept data-driven configuration, not just compile-time Razor parameters.
