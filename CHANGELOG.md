# Changelog

All notable changes to PEMS. No system version is defined yet (plan row F_CI), so entries are grouped by date
and theme and were reconstructed from the git history (initial import 2026-09-05). Newest first.

## 2026-11-01 - InfoGrid wave I2: description lists of detail pages (SCRUM-337)

### Added
- `<pems-info-grid layout="rows">`: each label beside its value (side panels, long technical lists), label over value again at phone width. `value-class` keeps a page's own hook on a value.

### Changed
- Part catalogue request, pre-procurement, purchase order and task details take their description lists from the component (6 grids, 54 items). The page styles `pc-dl` and `tm-dl` are removed. An empty value now shows a dash.

## 2026-11-01 - InfoGrid wave I1: the label / value block of detail pages (SCRUM-337)

### Added
- `<pems-info-grid>` / `<pems-info-item label="...">` in PEMS.UI: the label / value block at the top of a detail page, written as a description list (screen readers read each label with its value) in the responsive grid the pages already used. Options: `wide`, `num` (numbers, codes and dates in a fixed-width left-to-right run), `mono`, `ltr` (phone numbers), `tone="danger"`; an empty value shows a dash. Styles in `pems-ui.css`, loaded by the layout.
- Adoption ratchet pattern `info-grid` (hand-written `vs-info__label` and `<dt>`), so the hand-written form cannot come back.

### Changed
- User, engineering document, equipment, VDR and vendor details take their label / value blocks from the component (8 grids, 49 items). The look stays the same.

## 2026-11-01 - RelatedList wave R3d: work order details (SCRUM-337)

### Changed
- Work order details: its five tables come from `<pems-related-table>`: workflow history, labour, spare parts used, tasks and required materials. The page keeps its tabs, its actions, its forms and the height-limited scroll areas around the labour and spare parts tables. This finishes the RelatedList waves for the heavy detail pages.

## 2026-11-01 - RelatedList wave R3c: VDR details (SCRUM-337)

### Changed
- VDR document details: its four tables come from `<pems-related-table>`: submission history, comment management, workflow history and related correspondence. The page keeps its tabs, its actions and its forms.

## 2026-11-01 - RelatedList wave R3b: permit details (SCRUM-337)

### Changed
- Permit details: its seven tables come from `<pems-related-table>`: execution gate checks, approval steps, LOTO points, isolations, SIMOPS conflicts and links, and status history. The page keeps its sections, its actions and its forms.

## 2026-11-01 - RelatedList wave R3a: part catalogue request and RCM / FMEA class (SCRUM-337)

### Changed
- Part catalogue request (edit): the equipment, attribute and note tables come from `<pems-related-table>`; removing an equipment line uses `<pems-related-remove>`. The page keeps its own sections, the sticky table headers and the attribute script (it finds the table by its `id`).
- RCM / FMEA class detail: the functional failure, FMEA item and RCM decision tables come from `<pems-related-table>`.

## 2026-11-01 - Sprint membership is set and verified when a batch starts (SCRUM-479)

### Changed
- Guideline chapter 0 and AGENTS.md: moving an issue to In Progress does not put it in the sprint; set the sprint field (`customfield_10020`) in the same step and verify with `sprint in openSprints()`. The session sweep also lists In Progress issues with no sprint. Found when eight issues of the component batch sat outside Sprint 1.

## 2026-11-01 - The rest of the buttons in the views (SCRUM-53, #31)

### Changed
- `<pems-button>` takes `tone="none"` (the page passes the class), `disabled="@(cond)"` and `as="label"`/`as="span"`; `ButtonOptions.Tone` is nullable. 91 more buttons in 49 views moved; the button ratchet is down to the 4 buttons built inside JavaScript strings in `_ContextualHelpDrawer`.

## 2026-11-01 - Card, progress, accordion, list-group and spinner components (SCRUM-53, #31)

### Added
- `PemsCard`, `PemsProgress`, `PemsAccordion`/`PemsAccordionItem`, `PemsListGroup`, `PemsSpinner` in `PEMS.UI` (options records, manifests, samples, tag helpers `pems-card`, `pems-progress`, `pems-accordion`, `pems-accordion-item`/`-header`, `pems-list-group`, `pems-spinner`) writing the Bootstrap markup the pages wrote by hand.

### Changed
- All 25 hand-written blocks in 14 views moved; ratchet baselines for card, progress, accordion, list-group and spinner are now empty (the accordion-item counter no longer matches `pems-accordion-item`). `Letter/Dashboard` `BarRow` became async because it contains a tag helper.

## 2026-11-01 - RelatedList wave R2: tender, procurement package and receipt details (SCRUM-337)

### Changed
- **Tender details:** invited vendors, invitations, bids and clarifications are related sections.
- **Procurement package details:** MRs, quotations, communications and approvals are related sections. Its English-only headings, empty texts and buttons now read in the language.
- **Incoming receipt (IRN) details:** lines, disposition records and status history are related sections; so are receiving from the PO and recording a disposition.
- **Inside the sections:** add forms use `<pems-related-add>` where they are simple and keep their layout otherwise. Every table comes from the component, and the buttons are `pems-button`. Confirmations read in the language.
- 16 texts in English and Persian. The header workflow buttons of the receipt are not changed (SCRUM-53 / SCRUM-483).

## 2026-11-01 - RelatedList: child collections of a detail page (SCRUM-337)

### Added
- **PEMS.UI RelatedList tag helpers:**
  - `<pems-related-list>`: a section with icon, title, count and empty text.
  - `<pems-related-table>`: the child table; it keeps the page's `id` and `data-*` hooks.
  - `<pems-related-add>`: the add form under the table, with the parent key and the antiforgery token.
  - `<pems-related-remove>`: the delete form of one row, with the row and parent keys and an optional confirmation.
  - `<pems-related-row-form>` + `<pems-related-save>`: a row edited in place; the inputs point at the hidden form with `form="..."`.
  - Styles `lp-rel-*` in `pems-list.css`.

### Changed
- JobPlan details: the task and material lists are related lists, with labelled add fields.
- Administration: Numbering, Routing rules and Contract approval authority use the list frame. Each row is edited in place through the component. 8 texts in English and Persian.

## 2026-11-01 - Contract creation SQL tests pass again (SCRUM-498)

### Fixed
- `ContractCreationAtomicitySqlTests` T12 and T13 failed with Forbid: the test user had no user name, so the tender creator separation (P0-10) could not verify the approver and failed closed. The test user now has its name; the product behaviour is unchanged.

## 2026-11-01 - List page component, wave 4b: Administration settings lists (SCRUM-337)

### Changed
- SoD policies, storage providers, storage routes, workflow definitions, help articles and the PO authority table are built from the list-page component; 32 texts in English and Persian (headings, notes, empty texts, provider health, confirmations, publish / unpublish).
- The storage provider count cards are the KPI row; the help filters are chips; the PO authority rule form opens from the toolbar (`pems-list-popover`). Row actions stay.
- Not converted: contract approval authority, numbering and routing rules edit their rows in place; they move with the RelatedList / DataTable row-form mode.

## 2026-10-31 - List page component, wave 4a: users, roles and base data (SCRUM-337)

### Changed
- Users, Roles, Departments, Disciplines, Employees, the Audit Log, Security Events and the Authorization Audit are built from the list-page component; 68 texts in English and Persian (headings, notes, columns, empty texts, statuses, confirmations, audit actions, personnel types).
- Filter rows moved into the filters dropdown, each active filter (also a user opened from another page) is a chip, and paging keeps the filters. The user confirmations read in the language. Row actions, the reset-administrator link and the create / edit modals stay.
- The Admin area loads the PEMS.UI tag helpers from its `_ViewImports`. The adoption baseline is lower for these views. The settings tables of Administration (approval authorities, numbering, SoD, storage, routing, workflow, help) follow in wave 4b.

## 2026-10-31 - List page component, wave 3: procurement and warehouse lists (SCRUM-337)

### Changed
- Purchase Orders, Tenders, Material Requisitions, Vendors, Contracts, Procurement Packages, Inventory Transactions, Store Requisitions, Incoming Receipts (IRN), Replenishment, the Material Master and Part Catalogue Requests are built from the list-page component; 145 texts in English and Persian (headings, columns, empty texts, notes, and the statuses and transaction types that were shown as stored values).
- Filter rows, status selects and the Part Catalogue side panel moved into the filters dropdown (`<pems-list-filter-select>`), each active filter is a chip, and paging keeps the filters (the Material Master pager lost "low stock" and "critical"). A tender list opened from a package shows the package as a chip and offers New for that package.
- The warehouse views load the PEMS.UI tag helpers from the area's `_ViewImports`. The create modals stay until SCRUM-482. The adoption baseline is lower for these views.

## 2026-10-31 - Badge completed in the views (SCRUM-53, #31)

### Changed
- `<pems-badge>` takes `tone="none"` (the page passes the classes), a runtime tone (`tone="@x"`) and the other attributes of the tag; 258 more badges in 106 views moved, the `text-bg-*` view helpers now return tone names, the badge ratchet is down to the 4 toast lines in `_Layout` (the counter no longer counts `bi-person-badge` icons).

## 2026-10-31 - `<pems-button>` takes `asp-*` routing (SCRUM-53, #31)

### Changed
- `<pems-button as="a" asp-controller asp-action asp-route-*>` builds its href through the built-in anchor helper (the tag helper derives from it); 291 more links in 147 views moved; the button ratchet fell again.

## 2026-10-31 - `<pems-button>` and the buttons in the views (SCRUM-53, #31)

### Added
- `<pems-button tone size outline css as>text</pems-button>`: writes a `button` (or an `a` with `as="a"`) with the classes from `ButtonState`, keeps the page's own attributes (type, id, href, data-*, onclick) and content. Tones neutral, primary, danger, success, warning, info, light, dark; sizes small, medium, large.

### Changed
- 741 static buttons and links in 181 views now use `<pems-button>` with the same classes; the button ratchet fell from 1126 to 385. Not moved: tags with `asp-*` attributes (the built-in anchor helper does not run on a renamed element), tags with Razor expressions in the start tag, buttons inside `<script>` strings, solid `btn-primary`, `btn-link`, and other special forms.

## 2026-10-31 - Badge and Button skin, `<pems-badge>` (SCRUM-53, #31)

### Added
- `BadgeOptions` and `ButtonOptions` get `Skin` (default Bootstrap: today's `badge bg-*` / `btn btn-*` markup; Pems: the library classes) and `CssClass` (layout helpers such as `ms-1`, `w-100`). `ButtonOptions.Outline` writes `btn-outline-*`. `Tone` gains Info, Light and Dark (no Pems-skin colour yet).
- `<pems-badge tone="warning" css="ms-1">text</pems-badge>`: tones neutral, primary, danger, success, warning, info, light, dark.

### Changed
- 265 static `<span class="badge bg-*">` blocks in 82 views now use `<pems-badge>` with the same output; the ratchet baseline for badges fell from 689 to 405. Badges with computed classes (`@badgeClass`) and `text-bg-*` stay for the next batch.
- The button and badge golden manifests were re-recorded; the empty and error states keep the Pems skin for their button.

## 2026-10-31 - List page component, wave 2b: the other CMMS lists (SCRUM-337)

### Added
- `open="true"` on `<pems-list-kpis>` and `<pems-list-kpi-toggle>`: the KPI row shows first until the viewer hides it.
- `<pems-list-filter-select id name label value all-text items>`: one select in the filters dropdown, so a page no longer writes the select by hand (SCRUM-341 ratchet).
- `<pems-list-popover id icon label primary>`: a toolbar tool that opens a dropdown panel with the page's own short form (no modal).

### Changed
- Criticality Assessments, Equipment Criticality, Mechanical Equipment, Failure Mode Catalogue, Shift Handovers, RCM / FMEA coverage and the Equipment / Functional Location assignment batches are built from the list-page component; 88 texts in English and Persian. Paging on Criticality Assessments now keeps the filters. The assignment upload opens from the toolbar's Upload button. The adoption baseline is lower for these seven views (badges, buttons, tables, inline scripts).

## 2026-10-31 - Organization import saves again (SCRUM-497)

### Fixed
- **Administration > Organization > Excel import:** committing a valid file failed with "Nothing was saved: The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions". The save now runs inside the SQL Server retry strategy, like the other services. New `OrganizationImportSqlServerTests` (real SQL Server with `EnableRetryOnFailure`); guideline 01 section 3 states the rule.

## 2026-10-31 - Adoption ratchet (SCRUM-341)

### Added
- **PEMS.UI.Tests/AdoptionRatchetTests** and **tools/UiAudit/adoption-baseline.json**: per-view and per-script counts of 22 hand-written patterns (buttons 1148, text inputs 1379, badges 689, selects 716, tables 248 ...). The test fails when a count rises or a new file brings a pattern, and when a count falls without lowering the baseline. Pull request template and guideline 02 updated.

## 2026-10-31 - Alerts come from the component (SCRUM-481)

### Changed
- **PEMS.Web views:** 133 hand-written `alert alert-*` blocks in 67 views are now `<pems-alert tone=... css=...>` (new tag helper in PEMS.UI); the markup is the same, with a `role` added. `PemsAlert` takes page markup as content, extra classes (`CssClass`) and the tones primary and secondary.

## 2026-10-31 - Part Catalogue reports (SCRUM-478)

Owner request 2026-10-31: reports of the Part Catalogue for any period, with an Excel download. Everyone who may view the Part Catalogue sees them; the rows follow the request list's visibility rule.

### Added
- **Warehouse > Part Catalogue reports** (`PartCatalogueReport`). The page uses the list component and has six tabs. Each tab is one table and one Excel sheet:
  - **Summary:** KPIs and charts.
  - **By person:** who created, submitted, approved, returned or cancelled requests, and who registered IFS part numbers.
  - **Activity:** every action with its request.
  - **By equipment:** requests, registered part numbers, in progress and critical.
  - **Parts per equipment:** manufacturer, part numbers, supplier, class, unit, installed quantity, critical, specifications.
  - **Stages:** counts by status and by approval step, age of open requests, completed, average and median days to complete.
- **Period:** this month, last month, this quarter, this year, all time, or a custom range (both dates included). There are also filters for section, type, status, person and equipment.
- **Excel:** the Summary tab downloads the whole workbook; the other tabs download their own sheet. Headers follow the language, and the sheets are right to left in Persian.
- **Links:** both sidebars (Warehouse) and the report hub.
- **Accounts without an employee record** (for example a system admin) can open the reports. With Admin, Review or Approve they see every request; otherwise they see nothing.

### Changed
- The request list and the reports share one visibility rule (`PartCatalogueRequestScope`). The list behaves as before.

## 2026-10-31 - Agents run the issue-creation check themselves until SCRUM-475 (SCRUM-475)

### Changed
- Chapter 0 section 5d and AGENTS.md: until the nightly `tracker-check` runs, an agent reads each issue back after creating it and sweeps the open issues from SCRUM-400 on (parent epic, `[#N]` title, labels, `qa-pending`) at the start of a session and before any In Review, fixes metadata findings and reports the rest.

## 2026-10-31 - PEMS.UI components render today's look (SCRUM-480)

### Changed
- **PEMS.UI:** alert, modal, drawer, confirm-dialog, tabs and breadcrumb take `Skin`; the default `Bootstrap` renders the Bootstrap markup the pages use now, `Pems` keeps the library classes and native dialog. `pems-overlay.js` copies the row key of a Bootstrap modal opener.
- **Layouts** (`_Layout`, `_LayoutEmbed`, `_AuthLayout`): load `pems-ui.css`, `pems-overlay.js` and `pems-form.js` from PEMS.UI.

## 2026-10-31 - List page component, wave 2: CMMS (SCRUM-337)

### Added
- `<pems-list-new modal="...">` opens the page's own create modal (a button instead of a link); `<pems-list-export label="...">` names an export that is not the filtered list. A page's TempData message can sit inside the full-height list page.

### Changed
- Work Orders, Work Requests, Equipment, Functional Locations, Instruments, Letter Tags, Maintenance Plans, Maintenance Strategies, Job Plans, Spare Parts, Permits, Inspections, Findings and Assets are built from the list-page component. Each page keeps its filters, columns, modals, row actions and permissions. Count cards became KPI chips on demand; status button rows (Work Requests, Permits) became the status filter, and "pending my approval" a switch in it; page notes became the title tool tip; coverage, the failure-mode catalogue and bulk assignment moved into the strategies' "more" menu; status badges became tone pills.
- The English and Persian texts written in these pages (headings, columns, empty texts, notes, confirmations and work-order, work-request, permit, finding, instrument and asset statuses, priorities) are resource keys in both languages now (prefixes `Wo_`, `Wr_`, `Tg_`, `Ins_`, `Fn_`, `Mp_`, `Jp_`, `Pt_`, `Eq_`, `Fl_`, `In_`, `Ms_`, `Sp_`, `As_`, `Pri_`).

## 2026-10-31 - Login page in one language (SCRUM-477)

### Fixed
- **Login page:** the page was partly hard-coded. With the Persian language cookie, only the link "Use a recovery code" was Persian among English text, and the sign-in errors were always Persian. Every text of the login page now comes from SharedResource in both languages: the title, hints, Remember me, Sign In, the loading text, Forgot password, the validation messages and the sign-in errors.
- **Sign-in layout** (`_AuthLayout`): now takes `lang`, `dir` and the Bootstrap RTL stylesheet from the current language, so the Persian sign-in pages are right to left. The brand line and the footer are translated.

### Changed
- **Validation messages and `[Display]` names** of view models are now looked up in SharedResource (`AddDataAnnotationsLocalization` provider). A text without a key is shown as written, as before.

## 2026-10-30 - One guard for every upload; sandboxed preview (SCRUM-202)

### Added
- `IUploadGuard` (PEMS.Application/Uploads): every uploaded file is checked against the action's profile (Attachment, Image, Spreadsheet, Csv, Pdf, LicenceFile) - extension allowlist, declared content type, first bytes, size, then the malware-scan hook. Executables, scripts, HTML and SVG are never accepted.
- A global MVC filter runs the guard before model binding for all 34 upload actions (each carries `[UploadPolicy(...)]`); files sent to an action without a policy are refused. A refused page post returns to the page with the message; fetch/AJAX gets 400 JSON, `/api` a problem.
- Malware-scan hook `IMalwareScanner`, offline only: `None` (default) or `Command` (a scanner installed on the server, e.g. ClamAV `clamscan` or Defender `MpCmdRun`, run on a quarantined copy; a failed or timed-out scan refuses the file).
- Settings `Uploads__MaxFileSizeMb` (25), `Uploads__MaxRequestSizeMb` (100) and `Uploads__MalwareScan__*` in all six env files; Kestrel, IIS in-process and the form reader follow the request limit.
- `Files/Preview/{id}`: same access check as Download; PDF, raster images and text are shown inline, every other type is downloaded.
- Served files carry `X-Content-Type-Options: nosniff`, a `Content-Security-Policy` (sandbox; PDFs without `sandbox` because Chromium's viewer refuses it) and PEMS's own content type for the extension.

### Changed
- Profile photos, drawn signatures and letter editor images are stored in the file store (outside wwwroot) and served by `Files/Image`, which needs a login. Old `/uploads/profile-photos|signatures|letters/inline` URLs keep working through the same action; nothing under `/uploads` is served as a static file any more (old editor images, 404 since the containment work, show again).
- The correspondence signature check uses the shared allowlist table.


## 2026-10-30 - Receiving against PO lines and incoming supply (SCRUM-473)

Owner decisions 2026-10-30:
- **D6a:** quantity in QC counts as incoming until the IRN is closed.
- **D6b:** the buyer decides "replacement from vendor" (the default, the PO line reopens) or "close short" for a rejected quantity.
- **D7:** the Warehouse Manager resolves Hold from My Work.
- **Over-receipt:** allowed, with a warning and on the OS&D report.
- **D8:** imported POs without lines stay out of incoming supply.

### Added
- **Receiving against a PO:** an IRN raised against an issued PO receives its lines ("Receive from PO"). Each IRN line links to its PO line (`IrnReceiptLines.PurchaseOrderLineId`, migration `PoLineReceivingAndIncomingSupply`). These are warnings, not errors:
  - more than the open quantity;
  - another warehouse than the line's delivery warehouse;
  - inspection required and not released.

  The vendor must be the PO's vendor.
- **Delivery warehouse per PO line:** defaults from the pre-procurement demand. A stock line needs one to be submitted.
- **Inspection release before shipment per PO line:** optionally against the project's ITP. A user with IRN Approve can release.
- **OS&D report** per IRN, printable: over, short, rejected/damaged and Hold against the open quantity before the receipt.
- **Quantity buckets per PO line** (`PoLineSupply`), shown on the PO page: ordered, in QC, accepted, Hold, rejected (replaced / closed short), open, over-received.
- **Workflow `IRN / HoldResolution`** (17 module workflows): Hold left by an approved disposition is a task for `WH.Manager`. The accepted part is posted to stock; the rejected part follows the buyer's decision.
- **Incoming supply in net demand:**
  - open quantity of stock lines of issued POs at the delivery warehouse, plus quantity in QC at the receiving warehouse;
  - the result is now `CompleteNetDemand` (Projected = Usable + Incoming - Unreserved);
  - a line in another unit, or without a delivery warehouse, makes the result not definitive.

### Changed
- Pre-procurement planned supply no longer counts quantity already on an issued or closed PO line (it is incoming supply), so no unit is counted twice. The replenishment calculator version is now 2.
## 2026-10-30 - Command palette and permission-checked global search (SCRUM-64)

### Added
- Command palette: Ctrl+K / Cmd+K on every page, a click in the top-bar search box, or the search icon on a phone. It finds:
  - menus, read from the sidebar the server already filtered for the user;
  - actions (new letter, document, VDR item, work request, work order, equipment, MR, contract; My work, notifications, help, change password, theme and language), each shown only when the user passes the check of its page;
  - records of every module the user may open, five per type, with "Show all results".
- Keyboard and screen-reader support (dialog, combobox, listbox, `aria-activedescendant`), Light, Dark and Turquoise themes, RTL, phone layout. Component `pems-command-palette.js/.css` in PEMS.UI; no library, no external call.
- Security contract of search providers: every `ISearchableEntityProvider` declares `SearchAccessRule Access` (any user, module + action, or roles; lookup-only), equal to the page its results open. `GlobalSearchService` checks it before the provider runs; row scopes (letter confidentiality, own discipline, own notifications) stay in the providers.

### Changed
- `/GlobalSearch` (the top-bar search page) shows the same checked results as the palette. Before, it queried nine tables directly with no permission or row check, so work orders, confidential letters and documents of other disciplines could be found by any signed-in user.
- Vendor and contract results now need Vendors View / Contracts View, discipline results DisciplineWorkspace View (palette and search page). Form pickers (`/SearchApi?type=`) are unchanged.

## 2026-10-30 - Rich text as a standard field type, sanitized on the server (SCRUM-59)

### Added
- Field kind `RichText` (JSON `"kind":"richText"`) in `FieldDescriptor`: a form from the form builder can hold formatted text. `PemsField` renders it as a labelled `<textarea data-pems-rte>` (a plain textarea without JavaScript).
- `PEMS.UI/wwwroot/pems-rte.js` + `pems-rte.css`, loaded by the layout: the one rich-text editor of PEMS (vendored Quill 1.3.7, BSD-3-Clause, loaded only on pages with a rich-text field).
  - A Persian page starts right-to-left, an English page left-to-right; the direction button switches a paragraph.
  - Every toolbar control has a localised name (`Rte_*`); a required editor is checked on submit; the image button appears only when the field gives an upload URL.
- `FormValueSanitizer` (`PEMS.Web/Definitions`): the server step for posted form values. Rich-text values go through `IRichTextSanitizerService`, an empty editor becomes empty, names the form does not declare are dropped, other values stay as posted.

### Changed
- `_RichTextEditorScripts` (Letters) is now an adapter over `pems-rte.js`: no inline script, same behaviour (editor under its textarea, image upload, `rte-change`).
- `RichTextSanitizerService` is stricter:
  - only the editor's `ql-*` classes are kept (no page CSS classes in stored content);
  - links and images only use http, https, mailto or a relative URL;
  - a link with a `target` gets `rel="noopener noreferrer"`.
  It applies when content is saved; stored content is unchanged until it is saved again.

## 2026-10-30 - List page component, wave 1: document control (SCRUM-337)

### Added
- Two optional list-page tools in `PEMS.UI`: `<pems-list-menu>` (an icon tool with the page's own dropdown items) and `<pems-list-bulk>` (a bar shown while rows are ticked with `data-lp-select` / `data-lp-select-all`; its forms post the ticked ids). Three generic keys (`Lp_SelectedCount`, `Lp_SelectAll`, `Lp_SelectRow`).

### Changed
- Document Register, Document Revisions, Transmittal, TQ / TSR / RFI, MDB, MOC and Document Workflow are built from the list-page component: full-window table, one icon toolbar, filters in a dropdown with chips, counts as KPI chips on demand, the shared empty state. Each page keeps its filters, columns, actions and paging. The Document Register's saved and quick filters moved into one toolbar menu, and its row selection into the shared bulk bar (no page selection script). Document Workflow shows the queue and the recent history as two panels.
- The English texts written in these pages (headings, columns, empty texts, TQ and MOC statuses and risk levels) are resource keys in both languages now (prefixes `Dr_`, `Rv_`, `Tq_`, `Moc_`, `Mdb_`, `Dw_`).

## 2026-10-30 - Purchase-order lines and approval by the PO authority table (SCRUM-470)

### Added
- Purchase-order lines (table `PurchaseOrderLines`, migration `PurchaseOrderLinesAndAuthority`) from the pre-procurement path:
  - "Create PO" on an MR issued to procurement, or raised from the procurement cartable, adds one line per MR item not already ordered, keeping the registered part;
  - the tender and awarded bid are linked when the MR's package has one;
  - "Add lines from MR" works on a draft PO.
- Line rules on submit (SAP text item / IFS no-part line):
  - a line that goes into stock must be catalogued;
  - a free-text line must be direct use or service, with a commodity group and a reason;
  - setting `Procurement__CatalogueOnlyLines` (all six env files, default `false`) allows catalogued parts only.
- The PO authority table, separate from the contract rules (table `PurchaseOrderAuthorityRules`, admin page Administration > PO authority). Nothing is preset.
- Workflow `PurchaseOrderApproval` (16 module workflows): on submit the PO goes to the role of the lowest active level whose currency and amount range match, as a task in My Work. With no match the PO waits as "responsible not set" and "Route again" retries.
- The approver must hold the routed role, and the creator or submitter can never approve. A rejection returns the PO to draft with the reason.
- `PRC.Buyer` gets PurchaseOrder Create/Edit; `PRC.Manager` (Procurement Manager) gets PurchaseOrder Approve and issues approved POs (owner decision 2026-10-30). Both rows are also added at startup on databases where these roles already exist; a row an admin already set is kept.

### Changed
- `PurchaseOrderService.IssueAsync` issues only an **approved** PO (Draft → Submitted → Approved → Issued). The issue action keeps its guard (PurchaseOrder Approve permission, creator cannot issue, SystemAdmin rule). Imported POs still arrive as Issued.
- R6: a concurrent issue now shows a clear message instead of the error page.

## 2026-10-31 - Jira issue-creation guard (SCRUM-479)

### Added
- `plan_sync.py tracker-check` also fails on an issue (SCRUM-400 and later) without a parent epic, without a `[#N]` title equal to its `plan-N` label, without `area-`, `doer-`, `effort-` labels, or In Review without `qa-pending`; the REST client now reads `parent` and the issue type. Tests added.
- Creation checklist in `AGENTS.md` and chapter 00 section 5d; learnings-log entry; open points for the owner in chapter 06 (required Parent in Jira, `JIRA_*` secrets).
- Plan rows 0423 and 0478 for issues that had none.

## 2026-10-29 - Pre-procurement intake in the procurement cartable (SCRUM-471)

### Added
- Every purchase now enters the **procurement cartable** first (owner decision 2026-10-29): table `ProcurementDemands` (migration `PreProcurementDemands`) with one live item per source (filtered unique index `UX_ProcurementDemands_LiveSource`), and `MaterialRequisitionItems.ProcurementDemandId` as the two-way origin link.
- Sources:
  - "Send to procurement" on an open replenishment recommendation (the recommendation becomes `Converted`, history `SentToProcurement`);
  - direct-purchase lines of a released work order, raised with "Request from warehouse" (SCRUM-472);
  - an engineering MR when it reaches "Issued to Procurement".
- New workflow `PreProcurement`, one task per item in the procurement role queue of My Work (contract and binding; 15 module workflows now). New roles `PRC.Buyer` and `PRC.Manager` and permission module `PreProcurement`.
- Procurement cartable page (`/PreProcurement`, sidebar Purchasing). On each item the buyer can:
  - add a material demand to a new MR or an existing Draft MR, carrying the registered part;
  - accept an engineering MR, or return it to engineering (back to Approved);
  - dismiss an item with a reason.

### Changed
- Replenishment run: quantity already in procurement (open items, and converted items whose MR is not closed or cancelled) is subtracted before recommending (design §6.1 PlannedSupply), so a shortage is never recommended twice. A recommendation fully covered closes as `CoveredByProcurement`.

## 2026-10-31 - PEMS.UI: overlays, navigation, more field kinds and the form shell (SCRUM-336, SCRUM-338, SCRUM-352)

### Added
- Components in `PEMS.UI` (each with typed options, texts as inputs, a manifest snapshot, a sample, render tests in fa and en): `alert` (trace id + copy), `modal` (native dialog, one modal for many rows through the opener's row key), `drawer`, `confirm-dialog` (optional required reason, antiforgery token, posts the row key), `tabs`, `breadcrumb`, `stepper`, `menu`, `side-panel-nav`, `form-section`, `action-bar`.
- Field kinds: Password (value never rendered back), Money, Time, Radio, Switch, Tags, CheckboxMatrix (`Options` rows x `Columns`, posts `row|column` pairs); `<pems-field>` maps DataType Password, Currency, Time.
- `pems-overlay.js` (dialogs, tabs, menu, copy, dismiss) and `pems-form.js` (leave guard when `FormDefinition.WarnOnLeave`), both offline and idempotent; `ButtonOptions.Opens` / `RowKey` open a dialog for a row.

### Not built (follow-ups)
- No page uses the new components yet: adoption tasks per component are in the plan (see the adoption tasks of SCRUM-338). Lookup/TreeSelect fields stay with SCRUM-57 and the registry (#10).

## 2026-10-29 - Design token file moved into PEMS.UI (SCRUM-29, SCRUM-19)

### Changed
- The Light and Dark token blocks (`:root`, `[data-theme="dark"]`, `:root[data-theme="dark"]`) moved verbatim from `PEMS.Web/wwwroot/css/site.css` to `PEMS.UI/wwwroot/tokens.css`, served as `/_content/PEMS.UI/tokens.css` and linked right before `site.css` in `_Layout`, `_AuthLayout` and `_LayoutEmbed`. Same selectors and order, so no computed value changes. The theme and accessibility tests read the token file as well as `site.css`.

### Not built (follow-up SCRUM-476)
- Turquoise and the other theme blocks, hex colour and left/right replacement in the four shared CSS files, screenshot comparison.

## 2026-10-29 - Warehouse intake of work-order materials (SCRUM-472)

### Added
- Work-order materials have a supply code: **From stock** (default) or **Direct purchase** (SAP stock / non-stock component, IFS supply code). Migration `WarehouseIntakeSupplyCode`: `WorkOrderMaterials.SupplyCode` (default `Stock`) and `StoreRequisitionLines.WorkOrderMaterialId` (nullable link, no cascade).
- "Request from warehouse" on a released work order (Assigned, In Progress, On Hold) puts one task in the warehouse role queue of My Work (`WH.Manager`, `WH.Storekeeper`); new workflow `WarehouseIntake` with its module contract (14 module workflows now) and binding.
- Warehouse intake page (Warehouse area): the storekeeper picks the supplying warehouse (never pre-filled by inference) and the quantities; a Draft store requisition is raised for that warehouse with each line linked to its work-order line, and the task closes when every stock line is sourced. Submit, approve, reserve and issue stay in the existing store requisition flow.
- `IWarehouseIntakeService.GetDirectPurchaseDemandAsync`: the direct-purchase lines of released work orders, for the procurement intake (SCRUM-471).

### Changed
- Net demand: a work-order line supplied through linked requisition lines is counted on the requisition side (with its warehouse) and keeps only its unsourced quantity; only unlinked requisitions for the same work order and material are still an ambiguous overlap. Direct-purchase lines are excluded as `DirectPurchaseNotStockDemand` (informational, not blocking).
- A work-order material line that a live store requisition supplies can no longer be removed or have its supply code changed.

## 2026-10-29 - Demo pipeline: backup, smoke test, rollback, staging-first rule, runbook (SCRUM-313, SCRUM-314, SCRUM-317)

### Added
- `scripts/ci/smoke-test.sh`: anonymous post-deploy smoke test (health, database state, login form, stylesheet, protected page redirect, 404) run by `deploy-site.yml` after the health wait.
- `scripts/ci/backup-database.sh`: copy-only `BACKUP DATABASE` before a deploy when the optional secret `<P>_BACKUP_DIR` is set; a failing configured backup stops the deploy.
- `deploy-site.yml` inputs `ref` (rollback), `require_staged`, `allow_unstaged`, `backup`; `deploy-demo.yml` / `deploy-staging.yml` accept `rollback_ref` on a manual run.
- A staging run records the tag `staged/<tree hash>`; a demo run refuses a source tree without it (every demo deploy is preceded by a staging deploy of the same tree).
- `docs/deployment/RUNBOOK_DEPLOY_SITES.md`: deploy, roll back, reset and restore per site, access and owner table.

### Not built (follow-up task)
- Sample data pack load, demo licence install from a secret and automatic rollback after a failed smoke test: their dependencies (#140, #145, #352/#353, #301) do not exist yet. The landing site has no pipeline (#294).

## 2026-10-29 - plan-sync: tracker reconcile, pull, check and push (SCRUM-350)

### Added
- `tools/plan-sync/plan_sync.py`: `reconcile` (status differences, rows without issue, issues without row or Plan ID, duplicate titles), `pull` (tracker status into the plan rows), `tracker-check` (exit 1 on drift other than status) and `push` (summary, priority and plan-managed labels; dry run unless `--apply`). Issues come from the Jira REST API (standard library, `JIRA_*` environment variables, retry on 429/5xx) or offline from a Jira CSV/JSON export. `check` also fails on a duplicate task title.
- `.github/workflows/plan-sync.yml`: nightly and manual run (reconcile report, pull with a pull request, tracker-check); inert without the four `JIRA_*` repository secrets.
- 12 offline tests with a fake HTTP transport.

## 2026-10-29 - UI pattern audit: 30 heaviest pages reviewed (SCRUM-18)

### Added
- `docs/design/ui-pattern-review.csv`: hand review of the 30 heaviest pages (A convertible as is 13, B needs new components 16, C custom 1; 15 calls marked uncertain for the QA step).
- `docs/design/UI_PATTERN_AUDIT.md`: the heaviest-pages table lists 30 pages, the review table, the missing components (RelatedList, PdfViewer, TreeView, SidePanelNav, CheckboxMatrix, CommentsThread replies) and the convertible percentage per wave W1-W5 (reviewed pages measured, the rest estimated by a rule).

### Changed
- `tools/UiAudit/pattern-audit.py` reads the review file, counts table rows holding forms and PDF.js use, and writes the review and per-wave sections; static totals refreshed.
- Plan rows #322-#325 prompts list the components the audit found missing.

## 2026-10-29 - Jira key check on pull requests (SCRUM-349)

### Added
- `.github/workflows/commit-key-check.yml` (pull_request only, read-only, no secrets, no deploy) fails a PR when `SCRUM-<n>` is missing from the PR title, the branch name or any commit subject; merge commits, bot commits/PRs and `main`/`staging`/`demo` heads are exempt; batch keys missing from the title only warn. Logic and unit tests (standard library, offline) in `tools/commit-key-check`.

### Changed
- `docs/process/PLAN_TRACKER_WORKFLOW.md` section 3 now uses the `SCRUM-123` branch/commit/title convention of AGENTS.md (was `PEMS-123`); chapter 5, README and the `build.yml` comment no longer say that pull requests run nothing.

## 2026-10-29 - Docker image restore layer (SCRUM-445)

### Fixed
- `Dockerfile`: the restore layer now copies the project files of `PEMS.Licensing`, `PEMS.UI.Abstractions` and `PEMS.UI`, which `PEMS.Web` references; `dotnet restore` inside the image (also the Azure UAT build `az containerapp up --source .`) could not find them. `LinuxDeploymentArtifactsTests` is green again.

## 2026-10-29 - Registered part numbers in material requisition items (SCRUM-428)

### Added
- MR items link to the registered part their part number resolves to: a completed part catalogue request (`PartCatalogueRequestId`) or Material Master (`MaterialItemId`); migration `MaterialRequisitionItemRegisteredPart` (two nullable columns, indexes, restrict FKs). The service resolves on the server (exact, normalised); a registered part fills an empty description and unit; a typed number that is not registered stays free text, shown "Not catalogued".
- New MR form: the part number of each item is a searchable lookup over the registered parts (part number, manufacturer part number, description) that fills description and unit; a typed part can still be used; "Request a new part" opens the part catalogue. MR details show catalogued / not catalogued per item.
- `pems-select`: `data-pems-select-url` (page endpoint source) and `data-pems-select-create` (allow a typed value).

## 2026-10-29 - Tender invitations by the organisation's email (SCRUM-429)

### Added
- Email through the organisation's own SMTP server on MailKit 4.18.2 (MIT): `IEmailSender` with `SmtpEmailSender`; settings `Email__Enabled`, `Email__Host`, `Email__Port`, `Email__Security`, `Email__FromAddress`, `Email__FromName`, `Email__TimeoutSeconds` in every `env/*.env` (off everywhere), `Email__UserName` and `Email__Password` only in the secrets files.
- Tender details: "Send invitations" (one email per invited vendor from a fa/en template) and "Resend" for failed or skipped vendors, with the Tender Edit permission; sending runs in the background (Hangfire, 3 attempts); with email off the page says so.
- Table `TenderInvitationDispatches` (migration `TenderInvitationDispatches`): the send log on the tender (recipient, exact text, status Queued/Sent/Failed/Skipped, attempts, last error, who and when); an audit entry `SendInvitations`/`ResendInvitations` on the tender.

## 2026-10-28 - API quality gates (SCRUM-436)

### Added
- `tools/api-lint`: a Spectral ruleset of the REST standard (path form, no GET actions, 401/403 and problem+json on every operation, If-Match with 412/428 on writes, 201 on collection POST, camelCase names, declared security, summaries), `lint.sh` to lint the approved OpenAPI snapshot and `lint.sh selftest` to prove each rule fires on a deliberately bad document.
- `ApiConformanceTests`: every operation of the OpenAPI document driven through the real pipeline (401, 403, antiforgery, 404, 428/412, 422, 500, problem+json shape); `ApiPipelineBaselineTests`: in-process p95 guard.
- `tools/loadtest/pems-api-baseline.js` (k6, read-only API scenarios with per-endpoint budgets) and `docs/performance/API_PERFORMANCE_BASELINE.md` (budgets and the stored in-process baseline).
- The OpenAPI document now has operation ids, tags, summaries and descriptions, the declared cookie security scheme and no server list.
- The manual build workflow runs the API tests, the lint and its self-test as failing steps.

### Fixed
- Error responses of controller actions were sent as `application/json` instead of `application/problem+json` (the controller-level `[Produces]` overrode them); problems the framework builds itself (antiforgery 400, bare 404/405/415) now have the `urn:pems:problem:` type and an `errors` list.
- `PUT`/`POST` actions on `/api/v1/definitions` judged the body before looking the resource up; an unknown id is now 404 first.

## 2026-10-28 - Definition store REST API (SCRUM-434)

### Added
- Tables `Definitions`, `DefinitionVersions`, `DefinitionReferences`, `DefinitionStamps` (migration `DefinitionStore`) and the service behind them: draft, validate, publish (the old version is retired), rollback (an older version is published again as a new number), discard draft, export and import (hash-checked package), a runtime read side that serves the published version from a cache refreshed by a per-kind stamp.
- `/api/v1/definitions`: list, get, versions, draft (GET/PUT/DELETE with If-Match), actions `create-draft`, `validate`, `publish`, `rollback`, `export`, `import`; two concurrent edits give 412 for the second.
- Kinds `form` (the FormDefinition record) and `page` (regions of component instances, options checked against the component manifest); limits `Definitions__MaxPayloadBytes`, `Definitions__MaxInstancesPerPage`, `Definitions__MaxJsonDepth` in every `env/*.env`.
- Permission module `Definitions` in the permission matrix (grant it to a role to use the API; publish and rollback need Approve).

### Changed
- The generic audit trail no longer stores the old/new text of `PayloadJson` columns.

## 2026-10-28 - The plan is markdown files, not an Excel workbook (SCRUM-446)

### Changed
- `plan/rows/NNNN.md`: one file per backlog row (front matter, then Evidence, Prompt, Requirement, Report); `plan/sheets/*.md`: the other former sheets. A row edit now changes only that file's lines; two people editing different rows no longer conflict.
- `tools/plan-sync/plan_sync.py` reads the folder and gained `show`, `set`, `report`, `new` and `summary` (the old Summary sheet and Roadmap formulas, now over all rows); `export` no longer writes `plan/backlog.csv`. Standard library only (no openpyxl).
- AGENTS.md, guidelines 00, 01, 03-07 and README, the PR template, the workflow doc, ADR sources and the plan pages point at the folder; the manual build workflow runs the plan check.

### Removed
- `plan/backlog.csv`; `plan/PEMS_Backlog_Roadmap.xlsx` moved to `0 - trash/` (history only).

## 2026-10-28 - REST API walking skeleton and first resource (SCRUM-435)

### Added
- `/api/v1` (ADR-0009, guideline chapter 08): `PEMS.Web/Api` with RFC 9457 `application/problem+json` errors carrying an `errors` list, ETag / `If-Match` (412, 428), `Idempotency-Key` on creates, the list contract (`sort`, `filter[field]`, `q`, `fields`, offset paging capped at 10,000 rows), `allowedActions`, permission policies `[ApiPermission(module, action)]` over the permission matrix, 401/403 problems instead of login redirects for `/api`, the antiforgery header `X-CSRF-TOKEN` on every unsafe verb, and the OpenAPI 3.1 document at `/api/v1/openapi.json` (generated by Microsoft.AspNetCore.OpenApi, MIT, signed-in users only).
- Pilot resource `/api/v1/disciplines`: GET list, GET one, POST (201 + `Location`), PUT, PATCH (JSON Merge Patch) and DELETE (409 when other records use the discipline), on the same service as the Discipline master-data page.
- Migration `DisciplineRowVersion`: `Disciplines.RowVersion` (concurrency token).
- Tests: contract tests of the pilot resource, a gate that fails for an API action without a permission policy, and an approved OpenAPI snapshot.

### Changed
- `tools/rest-inventory` skips `PEMS.Web/Api` (the REST controllers are the target of the migration, not part of the map of what still has to move).

## 2026-10-28 - Searchable dropdowns on every form (SCRUM-57)

### Added
- `PEMS.UI` searchable select (`pems-select.js`/`.css`, on the vendored Tom Select), loaded once by the layout: every single `<select class="form-select">` with 8 or more options becomes searchable; `data-pems-select-source="<type>"` searches the permission-aware `/SearchApi` as you type for large lists; `data-pems-select` forces it, `data-pems-select="off"` opts out. Opens in place inside modals and dropdown panels, re-reads options a page replaces, keeps a "-- None --"/"All" choice only where the page has one, RTL and dark theme, fa/en texts from the layout (`Sel_*`).

### Changed
- The user Create/Edit pages no longer load Tom Select themselves (the layout does).

## 2026-10-28 - Automatic numbering of procurement documents (SCRUM-40)

### Added
- Admin > Numbering: the system administrator sets the numbering pattern of each procurement document (MR, procurement package, tender, bid, purchase order, contract): tokens `{YYYY}` `{YY}` (Jalali or Gregorian year), `{PROJECT}`, `{DISC}`, `{SEQ}`, counter digits, yearly or no reset, active, manual entry allowed. Live preview, validation, a version per change (and the generic audit log).
- New table `NumberingSchemes` (migration `NumberingSchemes`); a startup seeder adds an active Jalali yearly default for each type that has none (`MR-1405-0001`, `PP-…`, `TND-…`, `BID-…`, `PO-…`, `CT-…`).

### Changed
- Procurement package, MR, purchase order and contract numbers (also the contract from a winning bid) are assigned automatically on save; tender and bid numbers follow the pattern too. A typed number is kept only when the pattern allows manual entry; with the pattern switched off the form asks for the number as before. The atomic counter is separate per year/project/discipline as the pattern says, a number that already exists is skipped, existing numbers never change, imports keep their numbers.
- The create forms show the next number (or the pattern shape when it depends on the project) with the `DocumentNumberField` view component.

## 2026-10-27 - Procurement & supply chain section in the sidebar (SCRUM-423)

### Changed
- One sidebar section "Procurement & Supply Chain" (fa: تدارکات و زنجیره تأمین), like CMMS: Vendors, then the sub-headings Purchasing (Material Requisition, Procurement Package, Tender, Purchase Orders), Contracts (Contracts, Contract Milestones, Claims) and Warehouse (all former warehouse links). Engineering Management no longer lists the purchasing pages; the Contract Management and Warehouse sections are gone. Both sidebars changed (data-driven `NavigationMenuDefaults` and the classic `_SidebarMenu.cshtml` used in demo, UAT and production). Each link keeps its former gate plus its former section module, so no role sees more or less. Existing databases: the startup sync adds the new menu rows and deactivates the old system rows (not deleted); no migration.

## 2026-10-28 - ADR-0009, ADR-0010 and ADR-0011 accepted

### Changed
- The owner accepted the REST API standard, the metadata registry envelope and the UI definition model. One change: decimals in the API are plain JSON numbers (not strings); fields needing more than about 15 significant digits are declared as strings in OpenAPI.

## 2026-10-28 - Component manifests (SCRUM-433)

### Added
- Every PEMS.UI component now describes itself from its code: `[PemsComponent]` with category and icon, a generated manifest with the JSON Schema of its options, and `ComponentManifestCatalog` to validate stored options JSON (field error list) and convert it to parameters. This is the base for the page and form designers; no page changes. New dependency in PEMS.UI: LateApexEarlySpeed.Json.Schema 4.2.2 (BSD-3-Clause).

## 2026-10-28 - REST inventory of the controllers (SCRUM-432)

### Added
- `tools/rest-inventory/rest_inventory.py` (static scan, `--check` for staleness), `docs/audits/rest-inventory.csv` (every action: verb, kind, permission signal, target resource, wave) and `docs/audits/REST_INVENTORY_2026-10-28.md` (totals, findings, the 30 heaviest controllers classified by hand, target shape of every JSON endpoint). Result: 147 controllers, 1,062 actions, 170 JSON actions in 57 controllers, 269 actions with no permission signal beyond sign-in (a review list), 2 preference changes through GET.

## 2026-10-28 - Metadata registry and UI definition model designed (SCRUM-25, SCRUM-430)

### Added
- `docs/architecture/METADATA_REGISTRY_DESIGN.md`, ADR-0010 (one definition envelope with Draft/Validated/Published/Retired versions, rollback by republish, shipped vs customer definitions, cache stamp) and ADR-0011 (pages, layouts, component instances, data sources and actions as definitions), with a compile-checked C# sketch. Status: proposed, waiting for the owner. Design only; no code or migration.

## 2026-10-28 - REST API standard written (SCRUM-431)

### Added
- ADR-0009 and guideline chapter 08: resource-oriented JSON under `/api/v1`, verbs and status codes, ETag with If-Match, Idempotency-Key, RFC 9457 errors, one list query contract with offset paging for grids and cursor paging for exports and very large tables, OpenAPI 3.1 as the contract. Status: proposed, waiting for the owner to accept. No code yet (the walking skeleton is #435).

## 2026-10-28 - Title row on five pages that showed no heading (SCRUM-425)

### Changed
- Findings (list), Import Center, New Shift Handover, New Criticality Assessment and Verify Barcode now show the shared title row (page title, plus the module label when it differs) above their content; before, they set a title but showed no heading, so it appeared only in the browser tab. The layout still does not render the row; every other page is unchanged.

## 2026-10-28 - Persian digits in the pagination footer (SCRUM-426)

### Changed
- On Persian pages the pagination footer shows Persian digits in the "showing X to Y of Z" text, the page numbers and the rows-per-page buttons (new `IPemsLocale.Digits`, which swaps digits without reformatting). Links, the `page` and `pageSize` parameters and `aria-label` numbers stay Latin; English pages are unchanged byte for byte.

## 2026-10-28 - Page title row moved into PEMS.UI (SCRUM-425)

### Changed
- The page title row is now the `PEMS.UI` partial `_PemsPageHeader` (`PageHeaderModel`: Title, Section, texts as inputs); `_PageHeader` in `PEMS.Web` stays as an adapter that localizes the texts and honours `HidePageTitle`. Output is byte-identical (7 golden cases, en and fa). No page calls it yet: the layout does not render it by owner decision, so adding callers waits for the owner's choice of pages.

## 2026-10-27 - Unified loading, empty and error states (SCRUM-424)

### Added
- Component inputs are now named-property objects (`new ButtonOptions { Label = ... }`) instead of positional arguments, for buttons, badges, the new states and the field/column/view/form descriptors; one shared property-name table is in guideline chapter 02. Rendered output is unchanged (goldens identical).
- `PemsEmptyState`, `PemsErrorState` and `PemsLoadingState` in `PEMS.UI` with a `_PemsState` partial for existing pages: one look, one accessibility contract (status, alert, busy), texts as inputs, registry keys `empty-state`, `error-state`, `loading-state`. No existing page changes yet.

## 2026-10-27 - List page component, wave 0 (SCRUM-337)

### Added
- `PEMS.UI` list-page component: `<pems-list-page>`, `-panel`, `-toolbar`, `-search`, `-tools`, `-filters`, `-kpi-toggle`, `-export`, `-reset-columns`, `-new`, `-kpis`, `-chips`, `-chip`, `-grid`, `-table`, `-footer` tag helpers with the Engineering Documents look; texts are inputs (`PemsListTexts`, `ListPageTexts.From(Localizer)`). `docs/design/LIST_PAGE_COMPONENT.md` (usage, what stays with each page, waves).
- Five generic resource keys (`Lp_Kpis`, `Lp_ApplyFilters`, `Lp_Total`, `Lp_RemoveFilter`, `Lp_ResetAll`).

### Changed
- Engineering Documents and VDR lists are built from the component (same look and behaviour; about 80 lines less each, no page script).
- The list styles and script moved into the component library: `PEMS.Web/wwwroot/css/pems-letter-index.css` -> `PEMS.UI/wwwroot/pems-list.css` (the Letter pages only changed the link), `pems-list-page.js` -> `PEMS.UI/wwwroot/pems-list.js` (now also wires the search autocomplete, column widths and reset-columns for every list page).

## 2026-10-27 - Pagination footer moved into PEMS.UI (SCRUM-418)

### Changed
- The list pagination footer is now the component `_PemsPagination` in `PEMS.UI`; its texts are inputs (`PaginationTexts`). `_Pagination` in `PEMS.Web` stays as a thin adapter, so the 54 list pages are unchanged. The HTML output is byte-identical (8 golden cases, en and fa).

## 2026-10-27 - Persian pages accept decimals and Persian digits (SCRUM-421)

### Fixed
- A decimal such as 12.5 posted by a number input, and Persian or Arabic-Indic digits typed by the user, are now accepted when the page language is Persian; one model binder (`PEMS.UI.Binding`) is registered for numeric and date types. English binding and MVC's error messages are unchanged. Known-issue tests became regression tests; ADR-0008, guideline chapter 02 and manual check M-19 updated.

## 2026-10-27 - Bugs are normal tasks; gate works on Windows (SCRUM-422)

### Changed
- Guideline chapter 00 section 3a: a reported bug gets its own Jira issue and plan row first, a regression test, a PR and QA; chapter 07 and AGENTS.md point to it.

### Fixed
- `LiteralTextGateTests` no longer fails on Windows (CRLF) checkouts (fix merged in atapems-tech/PEMS#44).

## 2026-10-27 - Every module workflow editable in Workflow Management, guarded by module contracts (SCRUM-103)

### Added
- Module contracts on the workflow engine (`IWorkflowModuleContractRegistry`, `PemsWorkflowContracts`): for each of the 13 module workflows, the actions, stages, dynamic-participant hand-overs and BusinessStatus its code relies on. The validator refuses a version that breaks them (`CONTRACT_*` errors), so Validate/Publish cannot break a module.
- Startup seeder `WorkflowBindingBackfillSeeder`: an explicit binding, pinned to today's published version, for every module workflow that had none (same version the legacy path started, so behaviour is unchanged).
- `docs/workflow/WORKFLOW_PLATFORM_MODULE_CONTRACTS.md`.

### Changed
- Admin > Workflow Management: all 13 definitions are editable (was 3 by whitelist); the reason and the designer show which parts the module contract keeps.
- The legacy module action names moved from `Program.cs` to `PemsWorkflowContracts.CreateActionRegistry()` (one list for the app, seeders and tests).

## 2026-10-27 - Component texts are inputs (SCRUM-416)

### Added
- `TranslatableText` (JSON map of language slug to text, fallback chain, completeness check), `IPemsLocale` with the default `PemsLocale` (Jalali dates and Persian digits for Persian cultures, ISO dates otherwise, number formatting, options), `PemsJson`, and a culture parameter on the request-free renderer. ADR-0008.

### Changed
- `PEMS.UI` descriptors and components take `TranslatableText` for every user-visible text and hold none; table rows carry values of any type formatted by the locale. A test fails when a literal word appears in a component. `PEMS.Web` pages are unchanged.

## 2026-10-27 - PEMS.Web consumes PEMS.UI (SCRUM-29, step 1)

### Changed
- `PEMS.Web` references `PEMS.UI` (component registry and renderer registered, `<pems-field>` tag helper available, `_content/PEMS.UI/pems-ui.css` published but not linked, so no page changes). The shared partials `_KpiCard` and `_PemsLoadingOverlay` and `KpiCardViewModel` moved into `PEMS.UI`; their output is byte-identical (golden tests in `PEMS.Tests/Ui`). ADR-0007 accepted.

## 2026-10-27 - Database upgrade from every supported release, pre-update backup (SCRUM-280)

### Added
- `deploy/db/supported-releases.json`: the database states PEMS must upgrade from (2026-10-22 self-installing build, 2026-10-25 demo), each as its exact migration set; `deploy/db/released-migrations.sha256` fixes the shipped migrations.
- `DatabaseUpgradePathSqlTests` (category `DbUpgrade`): each supported release is upgraded with representative data through the real migrator and must equal a fresh install (schema) and keep its rows; also proves the pre-update backup and its restore. Runs in the Migration Chain Gate workflow after the chain script.
- `MigrationCompatibilityRulesTests`: released migrations unchanged, manifest valid, unique ids with Designer files.
- Automatic pre-update backup: `Database__BackupBeforeUpdate` (true in production/uat) and `Database__BackupDirectory` in every `env/*.env`; an update first writes a COPY_ONLY `<db>_preupdate_<utc>.bak` and is not started when that fails. The file is recorded in the run history (migration `DatabaseUpdateRunBackupFile`) and shown on Admin > Database Update.

### Changed
- Admin > Database Update: with the automatic backup on, the manual "backup taken" box is replaced by a notice; the run table shows the backup file.
- `CODE_FIRST_DATABASE.md`: supported releases, compatibility rules (immutable released migrations, expand/contract) and the rollback (restore) procedure; guideline chapter 01 section 3 updated.

## 2026-10-27 - Component library spike (SCRUM-23)

### Added
- `PEMS.UI.Abstractions` (descriptor contract), `PEMS.UI` (Razor class library: button, badge, skeleton, field, table, form, descriptor host, component registry, request-free renderer, `<pems-field>` Tag Helper facade, View Component for comparison) and `PEMS.UI.Tests` with an extension-pack fixture. Standalone: `PEMS.Web` does not reference them yet, so no page changes.
- ADR-0007 (proposed): static Razor components with a Tag Helper facade, with the measured comparison.

## 2026-10-27 - Password history (SCRUM-412)

### Added
- A new password must differ from the current one and from the last N (`Authentication__Password__HistoryCount`, default 5, 0 = off) on every path that sets a password: forced first change, voluntary change, administrator reset, second-administrator reset, recovery code and break-glass. Replaced hashes are kept in the new table `PasswordHistory` (migration `PasswordHistory`, hashes only). Message in fa and en.
- README manual check M-19; `PasswordHistoryTests`.

## 2026-10-27 - Offline PDF viewers and an external-URL check (SCRUM-20)

### Changed
- The PDF viewers of Engineering Documents and VDR load pdf.js 4.0.379 from `wwwroot/lib/pdfjs` through `wwwroot/js/pdfjs-loader.js` instead of cdnjs.cloudflare.com (the product is offline).

### Added
- `OfflineAssetsTests`: no view, component, script or stylesheet may reference an external host (XML namespaces and one placeholder text are allow-listed); `tools/UiAudit/pattern-audit.py` now exits with an error on a CDN reference.
- README manual check M-17.

## 2026-10-27 - Migration chain gate (SCRUM-413)

### Added
- Workflow `migration-chain.yml` and script `scripts/ci/test-migration-chain.sh`: the migration chain is applied to a real SQL Server (empty install, upgrade from `origin/main`, model agrees) and a failure names the migration.
- `AccountSafetyOfMigrationsAndPipelinesTests`: no migration, workflow or deploy script touches accounts or passwords.
- Guideline chapter 01 rules (a model change ships its migration; the gate before staging/demo), `CODE_FIRST_DATABASE.md` section, `DEPLOYMENT_README.md` note, manual check M-15.

## 2026-10-27 - Deploys and updates never reset the administrator password (SCRUM-410)

### Added
- `AdminPasswordSurvivesUpdateTests`: the start-up seeder changes nothing when users exist and the development reset needs Development plus its variable.
- Rule in guideline chapter 01 (security), `DEPLOYMENT_README.md` section 9, a note in `LINUX_VM_DEPLOYMENT.md` and manual check M-14.

## 2026-10-27 - VDR list with the Engineering Documents look (SCRUM-409)

### Added
- VDR list: quick search (document number, description, vendor) and an Excel export of the filtered list (`VDR/Export`, same columns as the table, capped at 20,000 rows).

### Changed
- VDR list: same frame as Engineering Documents - full-page table, one icon toolbar, project/vendor/status filters in a dropdown with a count badge and chips, KPI chips and the vendor delay/risk view hidden until the KPI button is pressed (remembered per browser).

## 2026-10-26 - Architecture decision log (SCRUM-14)

### Added
- `plan/adr`: ADR template, index and six accepted ADRs (hybrid storage, Razor + PEMS.UI library, code-first database, env/secrets files, licensing threat model and VM policy, demo/staging branch policy), linked from the docs that explain them.

## 2026-10-26 - Engineering Documents list with the correspondence look (SCRUM-408)

### Changed
- Engineering Documents list: same frame as the correspondence list - the table fills the window (sticky header, scrolls inside, pager at the bottom), one icon toolbar (title with count, search over the title, filters, KPIs, Excel, reset columns, New), filters in a dropdown with a count badge and chips instead of the side panel, KPI chips hidden by default and shown on demand (remembered per browser).
- `pems-letter-index.css` gained a generic full-page frame `.lp-page` (the cartable frame without the folder column) and `wwwroot/js/pems-list-page.js` (search open/close, KPI toggle) for list pages that follow this look.

## 2026-10-26 - Searchable multi-select for user roles (SCRUM-407)

### Added
- Tom Select 2.6.2 (Apache-2.0) vendored offline in `PEMS.Web/wwwroot/lib/tom-select` (complete build, Bootstrap 5 theme, source maps, licence).
- `wwwroot/js/pems-multiselect.js` + `wwwroot/css/pems-multiselect.css`: any `<select multiple data-pems-multiselect>` becomes a searchable multi-select with removable chips and option groups, in the PEMS themes and in RTL/LTR; the list opens on <body> so cards cannot clip it.

### Changed
- Admin > Users create/edit: the role checkboxes are one grouped multi-select (responsible, expert, custom roles) with search. The posted field (`SelectedRoles`) is unchanged and works without JavaScript. The old `_RoleCheckboxes` partial moved to `legacy-trash/views`.

## 2026-10-26 - Administrator recovery: one-time recovery codes and server break-glass (SCRUM-355, SCRUM-356)

### Added
- One-time recovery codes for administrators (migration `AdminRecoveryCodes`): 10 codes per set, 80 bits each, stored only as PBKDF2 hash + salt, append-only rows (use/revoke recorded once). User menu > Recovery codes shows how many are left and generates a new set (current password required; unused old codes are revoked); the codes are shown once (no-store) with copy, print and download.
- `/AccountRecovery` (link on the sign-in page): user name + recovery code + new password. The code is consumed once, the password is set, MFA is reset for re-enrolment, all sessions are revoked, the event is audited and all administrators are notified. Wrong codes count toward the Identity lockout; a locked account is refused even with a valid code.
- Rate limit for the sign-in-free recovery pages: 5 POSTs per client IP per 15 minutes (`Security:Recovery:RateLimit:*`), answer 429.
- Server break-glass (`/break-glass`): placing `pems-recovery.request` in `Security:BreakGlass:RequestFolder` opens the page for 15 minutes, reachable only from localhost or `Security:BreakGlass:ManagementNetworks` (every X-Forwarded-For/Forwarded hop must be allowed too); anything else is 404. One administrator is reset (temporary password shown once, forced change, MFA reset); the file is deleted after use or on expiry.
- Optional vendor-signed recovery token (ECDSA P-256, at most 24 hours, bound to a machine fingerprint, single use), off by default (`Security:BreakGlass:VendorRecovery:Enabled=false`).

### Changed
- The MFA reset and unlock step of the second-admin reset moved into a shared helper used by all three recovery paths.

### Notes
- The first-run wizard (SCRUM-188) does not exist yet; it will call the same recovery-code service. Until a licence engine exists, the vendor-token machine fingerprint is a setting.

## 2026-10-27 - Branch hygiene and local gates (SCRUM-394)

### Changed
- Guideline: weekly branch audit and rules for In Review/Done branches that are not on `main` (chapter 00 section 4a, chapter 05), no direct Done, restart cloud sessions from `main`, local gates and the verified cloud-session toolchain (SDK, `dotnet-ef --tool-path`, drift gate) in chapter 05; chapter 07 now expects cloud authors to run everything except the SQL Server tests.

## 2026-10-26 - QA check in the UI, merge before In Review (SCRUM-394)

### Changed
- Workflow (owner decision): the PR is merged into `main` right before the issue moves to In Review (label `qa-pending`); In Review means merged and waiting for the QA check; Done means the QA check in the UI passed.
- Entering or re-entering In Review requires a QA checklist comment (how to test by README references) and a Tests paragraph, re-checked and updated after every failed QA (chapter 07 section 1a).
- A failed QA check sends the issue back to To Do (label `qa-failed`, defects in a comment, assignee kept).
- Tests and review are one step for now: the local test run is line 1 of every QA checklist, an issue may move to In Review with tests listed as not run, Done needs the tests green and the QA check passed.
- Manual checks and local tests are documented in the existing READMEs (root `README.md` "Local tests" and "Manual checks in the UI", `env/README.md` "Verifying settings", `docs/deployment/DEPLOYMENT_README.md` section 8); QA checklists refer to them and stay on the Jira issue after Done.
- New guideline chapter `docs/guidelines/07-qa-check-in-the-ui.md` (who, where, "fully in the UI", checklist template, defects, tasks without a screen); chapters 00, 01, 06, README, `AGENTS.md`, `PLAN_TRACKER_WORKFLOW.md` and the PR template updated.

## 2026-10-26 - Configuration constants second pass (SCRUM-335)

### Changed
- New settings in every `env/*.env`: `Hosting__HstsMaxAgeDays` (30), `Authentication__Cookie__SlidingExpiration` (true), `Database__MigrationCommandTimeoutMinutes` (10); values equal the previous behaviour.
- Production password policy: minimum length 12 in `env/production-kangan.env` (other environments stay at 8); the first administrator password must meet it.
- `docs/design/CONFIGURATION_CONSTANTS.md` classifies the remaining literals (setting, policy constant, domain rule, tuning with trigger).

## 2026-10-26 - Secrets audit: no built-in credentials (SCRUM-17)

### Security
- Removed every built-in password found in the audit: users created from an employee now get a random one-time password (`PEMS.Shared/Security/TemporaryPasswordGenerator.cs`); no default administrator password in the guides; the load-test password comes from `LoadTest__Password` in `env/local.secrets.env` or `PEMS_LOADTEST_PASSWORD`; the example administrator password is a placeholder.
- `docker-compose.yml` publishes SQL Server, Ollama and the app on `127.0.0.1` only (a test keeps it so).
- Audit report `docs/audits/SECRETS_AUDIT_2026-10.md` (HEAD plus all 202 commits of all refs), rotation runbook `docs/deployment/CREDENTIAL_ROTATION_RUNBOOK.md` and the read-only `scripts/security/Report-DefaultCredentialExposure.sql`. Environments created before 2026-10-23 still need rotation (plan row #317); the old values remain in git history (held, #374).

### Changed
- Workflow: the SCRUM project has an On Hold status; the Jira report is kept current during the work (Instructions rule 27); existing tasks are studied before a new one is added (rule 28).

## 2026-10-25 - Administrator recovery: audit trail, last-admin guard, second-admin reset (SCRUM-357, SCRUM-354)

### Added
- `SecurityEvents` table (migration `SecurityEventsRecoveryAudit`): append-only record of every administrator recovery attempt (actor, target, method, IP, UTC time, result, fixed failure-reason code); only the SIEM marker `ForwardedToSiemAt` may change. Read-only list at Admin > Security Events (fa/en).
- `IRecoveryEventService`: one service for all recovery paths. A successful recovery revokes all sessions and remember-me cookies of the target (security stamp), writes the audit record, notifies every active SystemAdmin and logs under `PEMS.Security`; a failure only audits.
- Last-admin guard: the last active SystemAdmin cannot be deactivated or removed from the role (checked on the server in the user edit and activate/deactivate actions).
- Second-admin reset (Admin > Users, key button on other administrators): random temporary password shown once and valid for 24 hours, forced password change, lockout cleared, passkeys and authenticator key removed, `MfaEnrollmentRequired` set (migration `AdminRecoveryUserFlags`). Resetting your own account is refused. An expired temporary password is refused at sign-in with the generic message.

### Changed
- A password change of an administrator in the user edit form is audited (method `AdminConsole`) and its Identity result is no longer ignored.

### Notes
- `MfaEnrollmentRequired` is only a flag until MFA at sign-in exists (SCRUM-188 / SCRUM-206).
## 2026-10-25 - Staging environment live

### Added
- Staging deploys from the `staging` branch: run 40208748572 is green, the database installed (HTTP 200, up to date) and only the changed files (33.8 MB) were uploaded. Root causes found on the way: stale migration-chain test, FTP protocol default, SQL login with a wrong user name.

## 2026-10-25 - Faster deploys: no repeated tests, health check shows the real error

### Changed
- The deploy pipeline records `gate-pass/<source tree hash>` after build, drift gate and tests pass and skips them when the same tree is deployed again (for example demo after staging); NuGet packages are cached; `force_tests` runs the gate anyway.
- The post-deploy health check prints the readable error page and, after 20 consecutive 5xx answers, the newest `logs/stdout*` and `logs/pems-*.log` lines read over FTP, then fails.

## 2026-10-25 - EF migrations in their own assembly (SCRUM-371)

### Changed
- New project `PEMS.Migrations` holds the 119 migrations, the model snapshot and the design-time factory; `PEMS.Persistence.dll` shrinks from 29 MB to 6 MB and `PEMS.Migrations.dll` (23 MB) stays byte-identical when only data-layer code changes, so deploys no longer re-upload the migrations.
- `PEMSDbContext` defaults `MigrationsAssembly` to `PEMS.Migrations`; Web and Tests reference the project. Add a migration with `dotnet ef migrations add <Name> --project PEMS.Migrations --startup-project PEMS.Web`; the drift gate, `deploy.ps1`, the Dockerfile and the docs use the new project.
- Tests: the update-run test no longer assumes the last two migrations are the code-first ones; a new database-free test checks that a plain `UseSqlServer` finds the migrations.

## 2026-10-25 - Smaller deployment package

### Changed
- `Directory.Build.props`: the git commit hash is no longer written into assembly versions, so unchanged PEMS assemblies stay byte-identical between commits and the FTP difference upload skips them.
- Environment packages (`-p:PemsEnv=<name>`) drop debug symbols, satellite resources of languages other than en/fa and the x86 native libraries: 285 MB -> 245 MB. `wwwroot/uploads` is excluded from publishes.
- `wwwroot/uploads` (17 runtime files incl. signatures and documents) is no longer tracked: added to `.gitignore`, files stay on disk.
- FTP steps treat an empty `<P>_FTP_PROTOCOL` as `ftps` everywhere; the pipeline checks FTP login, folder and write access before the long build; the migration-chain test no longer requires `FullTextSearch` to be the last migration.

## 2026-10-25 - Pipelines only on demo and staging, staging environment, deployment README

### Changed
- No workflow runs on pushes or pull requests to `main` or working branches: `build.yml` is manual (`workflow_dispatch`).
- The demo pipeline is now a reusable workflow `deploy-site.yml` (target and secrets prefix as inputs); `deploy-demo.yml` (push to `demo`) and the new `deploy-staging.yml` (push to `staging`) call it.

### Added
- `env/staging.env` and `env/staging.secrets.env.example` (same keys as every environment; the environment-files test includes `staging`); secrets `STAGING_*`.
- `docs/deployment/DEPLOYMENT_README.md`: subdomains, databases, FTP, secrets, Actions, manual deploy and manual reset, rollback, new-environment checklist.
- Plan rows #351 (this work), #352 (versioned, hashed, importable data package in the admin UI) and #353 (reset on deploy with compatibility precheck); the two data features are not built yet.
- Branch `staging` (main + demo + dev); branch `dev` retired after merging into `main`.

## 2026-10-24 - Demo pipeline works end to end

### Fixed
- Model-drift gate failed in CI (design-time factory needs a connection string): dummy value for the model-only check.
- FTP upload failed with `550 ... being used by another process` (IIS file locks): `app_offline.htm` is uploaded first and removed after a successful upload.
- FTP `Timeout (control socket)`: 180 s timeout and one automatic retry.
- Health check could not see the app state behind a missing TLS certificate: probe uses `--insecure`; the certificate is reported separately.
- `demo` pushes that only change docs/plan no longer redeploy.

### Added
- Startup output of the demo app is logged to `logs/stdout*`; `docs/deployment/DEMO_DEPLOYMENT.md` records the runs, causes and fixes.
- Result: run 5 on `demo` is green and the runner's check reports `HTTP 200 UpToDate` (demo database installed). Open: TLS certificate (#340).

## 2026-10-24 - Tracker-free plan workbook, Sprint 1, clearer task text

### Changed
- The plan workbook no longer mentions or stores anything tracker-specific (the `Jira Key` column and all Jira wording are gone);
  tracker-specific export lives only in `tools/plan-sync`. `docs/process/PLAN_JIRA_WORKFLOW.md` is now `PLAN_TRACKER_WORKFLOW.md`.
- Backlog columns `Sprint` and `Owner`; new `Sprint 1` sheet (goal, dates, capacity, committed and stretch items).
- Sprint 1 tasks rewritten with an unambiguous *What to do* and *Done when*; new rows #338 (metadata registry design) and #339 (licensing technical design).
- Export: summaries `[#<Plan ID>] <task>`, structured descriptions (why, what to do, done when, dependencies with titles), Sprint and Assignee
  columns (assignees from the git-ignored `plan/members.json`), epics per milestone.

## 2026-10-23 - Component library plan, UI audit, plan/Jira workflow

### Added
- `tools/UiAudit/pattern-audit.py`: static audit of every view (pattern, target components, migration wave, hand-written markup counts);
  results in `docs/design/UI_PATTERN_AUDIT.md` and `docs/design/ui-pattern-audit.csv`.
- `docs/design/COMPONENT_LIBRARY_PLAN.md`: research, decisions, taxonomy, Definition of Done and delivery plan for the PEMS.UI library.
- `docs/process/PLAN_TRACKER_WORKFLOW.md` and `tools/plan-sync/plan_sync.py` (export to Jira CSV, `plan/backlog.csv` mirror, offline check);
  `.github/pull_request_template.md`.
- Plan workbook: backlog #319-#337 (component program, migration waves, Jira workflow), a `Jira Key` column, instructions 25-26.

## 2026-10-23 - Environment settings and secrets files

### Added
- `env/<name>.env` (all non-secret settings, committed) and `env/<name>.secrets.env` (secrets, git-ignored) for
  `local`, `docker`, `demo`, `uat` and `production-kangan`, plus `*.secrets.env.example` templates and `env/README.md`.
- `PEMS_ENV` selects the environment; precedence is env file < secrets file < real environment variables < command line.
  Loader: `PEMS.Shared.Configuration.EnvFiles` and `AddPemsEnvFiles` (fails fast on a missing file or required setting).
- Demo pipeline writes `env/demo.secrets.env` from GitHub secrets (`DEMO_SQL_CONNECTION_STRING`, `DEMO_ADMIN_PASSWORD`)
  and sets `PEMS_ENV=demo` in `web.config`; `dotnet publish -p:PemsEnv=<name>` ships the matching non-secret file.
- Tests: file format, precedence, identical key sets in every environment, no secrets in the committed files.
- Backlog #314: "What's new" panel on the login page (planned).

### Changed
- `appsettings.json` is empty; `appsettings.Development.json` and `appsettings.UAT.json` are removed (moved to env files).
- Hard-coded settings moved out of the code: log level/path/retention, password policy, auth cookie name/lifetime,
  security-stamp interval, WebAuthn domain/name/origins/drift, Ollama URL/models/timeout, default and supported
  cultures, Work Request segregation-of-duties flag, and the first administrator (user name, email, password).
- No first administrator is created unless `Seed:Admin:*` is configured (the built-in default account is gone).
- The design-time EF factory reads the env files and no longer has a hard-coded connection string.
- `docker-compose.yml` reads `env/docker.env` and `env/docker.secrets.env`; the default SQL Server password is gone.
- Linux release package (`deploy/Build-Package.ps1`) ships `env/*.env`; the VM must set `PEMS_ENV`.

## 2026-10-22 / 2026-10-23 - Fully code-first database

### Added
- `IDatabaseUpdateService`: tells a first run (empty database) from an update (pending migrations), reports an
  unmanaged schema (tables without migration history, never touched) and a database newer than the application,
  applies the install/update and records every run in the new `DatabaseUpdateRuns` table.
- Admin > System Monitoring > **Database Update** page (SystemAdmin, backup confirmation) and anonymous
  `GET /health/database` (200 only when every migration is applied).
- Migration `FullTextSearch` (full-text catalog and indexes, non-transactional, idempotent).
- Model-drift gate in `build.yml`; Persian and English UI strings for the new page.
- Plan workbook in the repository: `plan/PEMS_Backlog_Roadmap.xlsx`.

### Changed
- Startup now creates the database if missing and installs/updates it before the seeders run
  (`Database:CreateIfMissing`, `Database:MigrateOnStartup`; set `Database__MigrateOnStartup=false` to keep the
  reviewed-script flow).
- Demo pipeline (`deploy-demo.yml`): drift gate, runtime configuration, FTP upload, then waits for
  `/health/database` to report up to date.

### Removed
- Hand-run schema SQL moved to `legacy-trash/manual-sql/` (kept, not deleted): `InitialCreate.sql`, the full-text
  script, the procurement and TaskManager migration scripts.

## 2026-10-20 / 2026-10-21 - Correspondence, accessibility, themes, KPI
- Formal internal letter compose, approval chain and compliance; letter list filters, bulk actions, export,
  dashboard, response-due tracking, notifications, templates and reminders (schema migrations included).
- Accessibility: skip link, AA-safe status text colours, sidebar menu search, friendly status pages.
- Theme audit (Turquoise, Light, Dark) for contrast and cascade.
- Executive KPI dashboard, permission UI auto-save, Part Catalogue rules and settings, org-chart vacancy views.
- Fix: null-key crash for class-level RCM strategies in the criticality matrix.

## 2026-10-15 / 2026-10-19 - Navigation, task manager, procurement gates, merges
- Data-driven sidebar with per-item permissions, live badges and admin management (enabled in Development).
- Enterprise Task Manager on `TaskItem` (spaces, backlog, board, sprints) and a task IDOR fix.
- Procurement: read-view gates (P0-09) and segregation of duties (P0-10) per owner decisions D-11..D-13.
- Fix: user creation is atomic when role assignment fails; employee/discipline/role-management work merged;
  personal source merged into the company repository.

## 2026-10-08 / 2026-10-14 - Organization, procurement, CMMS audit fixes, deployment kit
- Enterprise organization hierarchy, actor resolution, org chart and test isolation.
- Employee account and authorization panel, role synchronization, discipline master-data UI and gating,
  employee imports rejected without a valid discipline.
- Procurement/inventory: Phase 1 hardening, Phase 2 net demand (P0-P5), part-catalogue duplicate detection,
  contract-creation hardening (P0.4/P0.5); CMMS audit fixes and Work Order section policy; RCM strategy work.
- My Work inbox and a UI repair program.
- Docker Compose deployment kit for the Linux test VM.
- Graphify AST cache no longer versioned.

## 2026-09-28 / 2026-10-07 - RBAC-ABAC, warehouse, maintenance planning
- Part Catalogue Request module, Warehouse area, Role Catalog, notification lifecycle.
- Navigation Visibility architecture with a hierarchical admin UI; RBAC-ABAC phases 3-5 and Work Order governance fixes.
- Work Order scheduling and Maintenance Calendar; dynamic maintenance organization, Work Order discipline scoping.
- Package Procurement contract finalization and IRN receiving.
- Fix: Admin-area ambient route values leaking into application links; sidebar hover-intent delay.

## 2026-09-19 / 2026-09-27 - Workflow platform, Help, UI redesign
- Central Workflow Platform: hardening (phase 1), Work Request migration (phase 2), Work Order lifecycle (phase 3B),
  Workflow Administration and Visual Designer (phase 4, closure audit accepted with documented limitations).
- Bilingual Help system with a knowledge system and standards references; Reliability Analytics audit.
- VDR/Vendor redesign, single Import Center, filtered Engineering Documents export; global compact page header and
  Digital Equipment Passport Work Request history.
- Fixes: MRS import 500-row ceiling and description truncation; VDR duplicate-vendor-key crash on commit.

## 2026-09-13 / 2026-09-18 - Operations, barcode, EDMS lifecycle, UAT readiness
- Operations and Production core with CMMS boundary correction.
- Enterprise Barcode/QR core with Warehouse/Inventory integration.
- Resource Header pilot, Equipment/Functional Location assignment, bulk strategy scope, history import.
- Controlled letter revision lifecycle and Gmail-inspired workspace.
- UAT hosting audit, hardening and an Azure Container Apps runbook.
- Portal shell consistency, search security, authorization wiring.

## 2026-09-08 / 2026-09-12 - CMMS intelligence, reliability, EDMS integrity, file storage
- Maintenance master data and standards taxonomy; Equipment Classification, ISO 14224 failure taxonomy and PM
  template intelligence; hierarchy-aware maintenance intelligence with human-reviewed classification.
- RCM/FMEA/FMECA decision intelligence, Data Quality coverage gate, Reliability Analytics, Failure Pareto, Bad Actors.
- CMMS execution lifecycle closure, operational readiness, engineering criticality assessment, operations dashboard,
  RAM/predictive intelligence, Functional Location master data import.
- EDMS: revision-file binding, revision-scoped client review, transmittal revision and acknowledgement integrity,
  self-approval segregation-of-duties enforcement.
- Enterprise File Storage Management; Quick Setup onboarding.
- RBAC hardening: Team/Crew/Discipline/Master Data authorization, navigation restructure, AI pipeline endpoints.

## 2026-09-05 / 2026-09-07 - Initial import and hardening
- Initial import of the PEMS engineering and CMMS platform.
- Phase 6B hardening waves: authorization remediation, Material Request state machine, WorkOrderTask ownership,
  database-level uniqueness, server-side pagination for the highest-volume grids, final repository audit.
- Engineering Documents: consolidated search and compact density.
