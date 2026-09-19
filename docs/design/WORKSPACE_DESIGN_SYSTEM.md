# PEMS Workspace Design System (`pems-workspace.css`, `.vs-*`)

Stylesheet: `PEMS.Web/wwwroot/css/pems-workspace.css` (renamed from `pems-vdr.css`). Razor + CSS only; uses `--pems-*` theme tokens. Logical properties throughout (RTL/LTR).

| Pattern | Markup | Rule |
|---|---|---|
| Page shell | `.vs-page` | No route-path breadcrumb inside the page (the shell provides one). |
| Page header | `.vs-header` / `.vs-doc-head` | Title + one primary action; secondary actions are `.vs-btn`, dangerous ones `.vs-btn--dangerline` or inside a "⋯" menu. |
| Register (list) | `.vs-toolbar` + table | One search box; advanced filters in an offcanvas; active filters as removable `.vs-chip`s; KPI as a `.vs-chips` strip, not cards. |
| Status | `.vs-pill.vs-pill--icon` + tone | Always icon + text + colour. Tone/icon/label come from `DocumentStatusBadge.Tone/Icon/ResourceKey`. Stored status strings are never changed. |
| Revision | `.vs-pill--current`, `.vs-rev-current`, `.vs-rev-superseded` | Current revision is a filled pill with a highlighted row; previous revisions are muted. |
| Identifiers | `.vs-num` | Document numbers, revisions, dates, codes are LTR-isolated in RTL pages. |
| Detail sections | `.vs-section` + `.vs-info` grid | Flat sections and dividers instead of nested cards. |
| Bulk selection | `.vs-bulkbar` | Select rows, then the bar appears with the actions. |

Status vocabulary (Engineering Documents): Draft, Submitted, IDC Review, Checked, Approved, Issued, Rejected, Revise & Resubmit, Client Approved, Archived; revision: Superseded.
Localization keys: `Ed_*` and `St_*` in `SharedResource(.fa).resx` (parity guarded by `EngineeringDocumentsUiTests`).

## Engineering Documents redesign — known gaps (documented, not changed)
- List has no server-side sorting, column show/hide or saved views (needs backend/JS).
- Many workflow actions lack server-side permission checks (view-level buttons only).
- Revision reviewer/approver/effective fields are never populated.
- Client-review badge codes (A–D) do not match stored Approved/Rejected values.
- Workflow has no due dates/SLA.
- `DocumentRegister/Index` and the Create/Edit forms were not restyled in this pass.

## CMMS Integration — Digital Equipment Passport (`Equipment/Details`)
- **Hierarchy:** Tag number is the dominant heading (`.vs-doc-number.vs-num`), then status / criticality / calibration pills (icon + text), name, type · project · discipline · area, then a flat `.vs-info` grid (P&ID, manufacturer/model, functional location, parent, install date, description, child equipment). The old 8-card KPI grid is now a `.vs-chips` strip whose chips switch to the relevant tab.
- **Work Request vs Work Order (semantic rule):** *Maintenance History* = **Work Requests** (`EquipmentPassportDto.WorkRequests`, loaded from `WorkRequests` where `EquipmentId` = equipment or `AssetId` ∈ the equipment's assets, active only). *Executed Work Orders* is a separate **read-only** list (`WorkOrders`). Work Orders are never created from the passport; the only create action is **New Work Request** → `WorkRequest/Create?equipmentId=` (pre-fills equipment; the Work Request module's own permission check and lifecycle apply). A Work Request row links to its converted Work Order when one exists.
- **Permissions:** buttons/links are shown only if the backend `IPermissionService` grants `WorkRequest Create/View` or `WorkOrder View`; the target actions still enforce authorization themselves.
- **Status display:** Work Request statuses use pill + icon + text (`Draft…Converted/Rejected/Cancelled`); stored values are unchanged.
- **RTL:** identifiers/dates use `.vs-num` (LTR isolation). Known gap: most other passport tabs (Documents, Procurement, Inspections, Classification…) still contain English literals not yet localized.

## Page Address / Route Visibility (global rule)
> PEMS must not display raw technical URLs, controller/action routes, or route addresses as a standard page-header element. User-facing navigation uses concise human-readable labels only when navigation context is genuinely useful.

- **Single source:** `Views/Shared/_PageHeader.cshtml`, rendered once by `_Layout.cshtml`. It outputs ONE compact row: the page title (`ViewData["Title"]`, `h1.pems-page-title`) plus a muted module label (`ViewData["Section"]`, only when different from the title). The former `_Breadcrumb` partial ("Dashboard / Section / Title", ~100 px incl. the duplicate H1) was removed. Nothing reads `Request.Path`/`RouteData` for display.
- **Pages with their own header** (e.g. Engineering Documents detail, Help) set `ViewData["HidePageTitle"] = true`.
- **Do not repeat the title inside the page.** A view must not start with an `<h5>/<h2>` that repeats `ViewData["Title"]`; put only the page actions in the first row (`d-flex justify-content-end`). Where a parent link is genuinely useful, use a local link/back action in that page's action row (not a route-shaped breadcrumb). The sidebar is the module navigation.
- **No large decorative header blocks**; the header is one 28 px row. Guard tests: `GlobalPageHeaderTests`.
