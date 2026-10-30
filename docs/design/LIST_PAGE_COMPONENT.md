# List page component (SCRUM-337)

Status: 2026-10-30, waves 0 and 1. The look comes from the correspondence list and the Engineering Documents list (SCRUM-408).
It lives in `PEMS.UI` (ADR-0007: Tag Helper facade for MVC pages). Texts are inputs (SCRUM-416).

## What the component gives every list page

- The table fills the window. The header is sticky, the rows scroll inside, and the pager stays at the bottom. On
  narrow screens the page flows normally.
- One toolbar: the title with its count, then icon tools. Search opens over the title. Filters open in a dropdown
  with a count badge. A KPI toggle remembers its state per browser. There are buttons for Excel and for resetting
  the columns (hidden on phones), and a "New ..." action.
- KPI chips are hidden until the user asks for them, and active filters show as removable chips.
- An empty state with "clear all" when the list is filtered.
- The same CSS and JS for every page: `_content/PEMS.UI/pems-list.css` and `_content/PEMS.UI/pems-list.js`.

## What stays with the page (its own rules)

Each page keeps its own:
- columns, cells and row colouring;
- row actions;
- filters, and which ones count as active;
- search target (`search-type` for the shared autocomplete);
- permissions (show a tool only when the user may use it);
- export link, paging and query-string handling;
- KPI content;
- sub-navigation.

A tool that the page does not render does not appear.

## Skeleton

```cshtml
@using PEMS.Web.Helpers
@section Styles { <link rel="stylesheet" href="~/_content/PEMS.UI/pems-list.css" asp-append-version="true" /> }

<pems-list-page texts="@ListPageTexts.From(Localizer)">
    @* optional module sub-navigation (vs-subnav) *@
    <pems-list-panel>
        <pems-list-toolbar heading="@Localizer["..."]" heading-title="optional tooltip" total="@Model.TotalCount">
            <pems-list-search id="xSearch" action="@Url.Action("Index")" input-id="xQ" name="search" value="@search"
                              placeholder="@Localizer["..."]" search-type="OptionalAutocompleteType">
                @* hidden inputs that keep the active filters *@
            </pems-list-search>
            <pems-list-tools search-for="xSearch">
                <pems-list-filters form-id="xFilter" action="@Url.Action("Index")" count="@activeCount" reset-href="@Url.Action("Index", new { search })">
                    <input type="hidden" name="search" value="@search" />
                    <div><label class="lp-lbl" for="...">...</label><select ... class="form-select form-select-sm">...</select></div>
                </pems-list-filters>
                <pems-list-kpi-toggle target="xKpis" storage-key="pems.<page>.kpis" />
                <pems-list-export href="@Url.Action("Export", new { ... })" />
                <pems-list-reset-columns grid-key="X" />
                <pems-list-new href="@Url.Action("Create")" text="@Localizer["..."]" />
            </pems-list-tools>
        </pems-list-toolbar>

        <pems-list-kpis id="xKpis"> <span class="vs-chip">... <strong>..</strong></span> </pems-list-kpis>

        <pems-list-chips reset-href="@Url.Action("Index", new { search })">
            @if (...) { <pems-list-chip label="..." value="..." remove-href="@Url.Action("Index", new { /* the others */ })" /> }
        </pems-list-chips>

        <pems-list-grid>
            <pems-list-table grid-key="X" empty="@(!Model.Items.Any())" empty-text="@Localizer["..."]"
                             filtered="@(activeCount > 0 || !string.IsNullOrEmpty(search))" clear-href="@Url.Action("Index")">
                <thead>...</thead>
                <tbody> @foreach (...) { <tr class="lp-row">...</tr> } </tbody>
            </pems-list-table>
            <pems-list-footer> @await Html.PartialAsync("_Pagination", ...) </pems-list-footer>
        </pems-list-grid>
    </pems-list-panel>
</pems-list-page>

@section Scripts { <script src="~/_content/PEMS.UI/pems-list.js" asp-append-version="true"></script> }
```

Optional tools added in wave 1:

```cshtml
@* a tool with its own dropdown (quick filters, saved views, ...): li items only *@
<pems-list-menu id="xViewsBtn" icon="bi-bookmark" label="@Localizer["..."]">
    <li><a class="dropdown-item" href="...">...</a></li>
</pems-list-menu>

@* bulk bar, between the chips and the grid: shown while rows are ticked; every form inside gets the ticked ids *@
<pems-list-bulk count-text="@Localizer["Lp_SelectedCount"]" name="ids">
    <form asp-action="ExportSelected" method="post"><button type="submit" class="btn btn-sm btn-outline-success">...</button></form>
</pems-list-bulk>
@* header cell: <input type="checkbox" class="form-check-input" data-lp-select-all aria-label="@Localizer["Lp_SelectAll"]" />
   row cell:    <input type="checkbox" class="form-check-input" value="@id" data-lp-select aria-label="..." />  (th/td class lp-select) *@
```

A page with a second list (for example a history under a queue) puts a second `<pems-list-panel>` in the same
`<pems-list-page>`: the panels share the height and each table scrolls inside its own panel.

Row and cell classes are shared too:
- `lp-row`;
- `lp-no` and `lp-subject` for the record number link;
- `lp-actions`, `lp-act` and `lp-act--always` for the row actions;
- the `vs-pill` / `vs-chip` badges.

Column resize needs `data-col-key` / `data-resizable` on the `th` elements; the script starts it for every
`table.lp-table[data-grid-key]`. No page script is needed for search, autocomplete, KPIs or column widths.

## Waves

| Wave | Pages | State |
|---|---|---|
| W0 | component; Engineering Documents and VDR moved onto it | done |
| W1 | document control: DocumentRegister, DocumentRevisions, Transmittal, TechnicalQuery, MDB, MOC, DocumentWorkflow (DCC is a desk, not a list) | done |
| W2 | CMMS: WorkOrder, WorkRequest, Equipment, FunctionalLocation, Instrument, Tag, MaintenancePlan, MaintenanceStrategy, JobPlan, SparePart, Permit, Inspection, Finding, Asset, ... | next |
| W3 | warehouse and procurement: Inventory, MaterialItem, MaterialRequisition, StoreRequisition, PurchaseOrder, ProcurementPackage, Tender, Contract, Vendor, IrnReceipt, ... | |
| W4 | admin and base data: Users, Roles, Department, Discipline, Employee, AuditLog, SecurityEvents, ... | |

Not converted, because they are not record lists: dashboards, import wizards, AI tools and reports. The Letter list
keeps its own richer frame (folders, preview, bulk actions) but uses the same stylesheet.

## Tests

- `ListPageComponentTests` checks each tag on its own: texts, optional tools, badge, chips, empty state and accessibility.
- The page tests (`EngineeringDocumentsListLookTests`, `VdrIndexLayoutTests`, `DocumentControlListPagesTests`) render the real views.
