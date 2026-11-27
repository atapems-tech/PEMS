# Known gaps of the conversions (input of the batch 10 audit, SCRUM-836)

Started 2026-11-27 (SCRUM-835). Owner decisions: a conversion changes no behaviour; engine and components are extended first; the audit runs in batch 10 against a running old application (demo, staging or a snapshot of the commit before batch 1). Append every gap here when a slice ships; never leave a gap only in a chat or a PR text.

Nothing of the slices below was built or tested (owner rule: no build until told). Every definition and every engine edit is unverified until the first full build, test run and browser pass.

## How a gap is closed

1. Open the old and the new screen side by side. 2. Decide: engine or component fix (never a screen exception), definition fix, or an approved change (owner). 3. Fix, add the test, tick the line.

## Engine and component edits made blind (verify first)

- Form field `searchSource` (select searched on the server), `default`, `step`; the FormHost applies `default` after link values and loads (SCRUM-835).
- Grid column `href` (a value shown as a link; `{value}`, `{id}`) in pems-grid.js.
- Page binding `steps` for the stepper and the `rejected` step state (PageRenderer, PemsStepper, CSS).
- Kanban (data-view): `groupsUrl`, `allowedField`, `moveFields`, `invalidMove` text; the move asks for JSON (also the calendar move).
- Operation-driven list filters from the address (optional string arguments with `param:`).

## Per screen

### MaintenanceStrategy (SCRUM-829, PRs 550, 552)
- The list had filters from the address (equipmentId, classId, status, type, q); the page has column filters only. Now possible with the address-filter pattern: pass them as in the work order list.
- The details page lost the one-time error list (TempData `RcmErrors`); the bulk assign skip report is the first 20 tags in the result message.
- Create, Edit, BulkCreate are still code (SCRUM-826, 834).
- Coverage and tag pages: the criticality badges and the localized criticality labels are plain keys; the equipment identity block (class, area/unit, badge) is a record view.

### Letters (SCRUM-827, 828; PR 548)
- The cartable and letter reads are `/x` operations; the list, details, compose and report screens are still code.

### Notification (SCRUM-831; PR 553)
- Dropdown (server-rendered fragment of the shell) and Open (redirect) stay in code; owner decision pending.
- The bell now refreshes its count after an action instead of using the answer of the action.

### Team (SCRUM-831; PR 554)
- Only the chart shell stays in code; the chart's details link template was already stale (no TeamDetails action). The three authorization tests of the removed actions were deleted (the right is the operation attribute).

### Task lists (SCRUM-831; PR 555)
- Up to 500 rows, no paging (the old page paged 50).
- The status step offers all four statuses (the service still refuses a wrong transition); the old form offered only the valid next ones.
- After an action the user lands on the to-do list (the follow-up tab was kept by `tab={tab}`).
- Task Details is still code.

### Work orders (SCRUM-831; PRs 558, 561, 560, 562, 563)
- List: the equipment of a prefilled create form shows the id until the user searches.
- Details: the sections the section matrix hides are empty tables (the old view showed a short read-only note per section); the old inline modals (completion data, review request, review response) are form pages; the legacy stage diagram lost the role label under the current stage; the totals row is three record fields.
- The respond-review form keeps the old fixed lists of failure modes and causes.

### Executive dashboard, coming soon, help (SCRUM-832; PRs 556, 557)
- Dashboard data: the `ResponseCache(NoStore)` attribute test was dropped; check the headers of `/x/executive-dashboard-data`.
- Coming soon: the title and section come from the address into a definition page.
- Help: Article and AskAi stay in code (AskAi answers a text; an action cannot yet answer JSON with a text).

### Task manager (SCRUM-831; PRs 564, 565)
- Start page: a table instead of the space cards; the "no employee" notice is a grid cell.
- Board: "create another" of the old create modal is missing; the red overdue style of a card is words in the subtitle; the per-card "Move to" select that worked without script is missing; the sprint banner is record fields.
- Backlog, AllWork, Update, Admin pages are still code.

## Tests removed or changed without a replacement (write the operation-level tests in batch 10)

- MaintenanceStrategy controller authorization (Tag, Coverage, ExportUncovered, FailureModes, Index, Details), Rcm view render tests (index, details, tag/coverage), CmmsListPages (strategies, work orders), InfoGrid and StatGrid text tests of the removed views.
- WorkOrderGridPaginationTests (whole file), WorkOrderDetailsRenderTests (whole file), the review-request guard test entry.
- Task controller authorization (Index tests), TaskManager render test (index and board parts), Team authorization (members, tree), Notification controller tests (cross user, anonymous), ExecutiveDashboard no-cache test.
- Write for each: the operation answers the same rows and refuses the same users (permission attribute), the page definitions ship and validate, the old addresses redirect.

### Task manager backlog (SCRUM-831; this slice)
- The sprint panels (one section per sprint with its own table) are a sprints table plus one task table with a sprint column and filter; sprint commands are row actions that open forms (start, complete with the move-to list, edit, new sprint).
- The bulk bar (status, assign, sprint, archive) is the grid bulk action that opens a form with the selected ids (engine: `formUrl`); "select all matching" does not apply to these.
- Drag and drop re-ordering of tasks inside a sprint or the backlog (`data-tm-reorder-url`) is not available; the up and down row actions and the move-to-sprint form remain. Needs a grid row-reorder option in the engine.
- The old create modal opened from the backlog; the create button opens the new task form page.
- The Backlog and space header views, and the backlog part of the member render test (replaced by operation assertions), are removed.

### Task manager AllWork (SCRUM-831)
- The page filters are the select and text filters of the page-filters component; the assignee list is the broad list (not only the people of the chosen space); the type and sprint filters of the old page are column filters of the grid; "include archived" is a select.
- Up to 500 tasks, no paging (the old page paged 50); the sort links are the grid column sorts.
- The bulk bar showed status, assign and sprint only when one space was filtered; the four bulk actions are always offered and open forms whose choice lists need the space key of the filter (empty without it).
- "My tasks" and the old address `TaskManager/AllWork` with `WorkSpaceId`, `Assignee`, `StatusCategory`: the page takes `spaceKey`, `assignee`, `statusCategory`; `WorkSpaceId` in an old link is not understood.

### Task manager administration (SCRUM-831)
- The inline row forms (member role and active, type toggle, status edit, every field editable in the row) are row actions that open forms; the transition matrix is the generic matrix with a select cell (allowed / not allowed) instead of a toggle icon per cell.
- A non-administrator gets the refusal of the operation (the old pages answered Forbid).
- The status edit form is prefilled from the address (values with special characters in a name may be cut); the generic fix is a form `load` by row id.
- Engine: the matrix binding source takes the page filters (`?f.id=`).

### Task details (SCRUM-831)
- The inline forms of the old page (edit with dirty guard and Ctrl+Enter, assign select, status select with note, comment box, link form, subtask quick add, watch and archive buttons) are row actions that open form pages; each form comes back to the details page.
- The edit form is prefilled from the address (a title or description with special characters or longer than the address limit may be cut); the generic fix is a form `load` by id.
- The breadcrumb, the restricted-item lock icon, the "not eligible" assignee hint text, the sprint select of the create modal and the people-diagnostic help text of the assignee list are not shown.
- Quick add of a subtask no longer chains ("create another"); the old address `Task/Details/{id}` is a route.
- TaskManager `Create` and `Update` controller actions and the item row and table partials are removed (their users are gone).

### Maintenance calendar (SCRUM-831)
- The week view (rows by team, one column a day, conflict marks, drag between days) and the day timeline are the generic month calendar and the day-by-day timeline of the data view; team rows and the employee-conflict marks are not shown (the conflict flag is in the row but has no style yet).
- Moving a card to another day keeps its time and length (operation MoveDay, guarded as the schedule action); the day-by-day view moves with its start and end through the existing schedule action.
- Items shown: planned from 90 days back to a year ahead (the old page asked for the visible range); the filters are text selects of the page filters.
- The script `pems-maintenance-calendar.js`, its templates and the controller are unused now; delete them with the audit.

### VDR and MDR import (SCRUM-830)
- The three-step banner (upload, review, commit) is gone; the include toggle of a row is two row actions (include, exclude) that reload the grid; commit and discard are confirmation form pages; the MDR tools "fix discipline matching" and "fix status values" are links to forms with an optional project.
- The batch figures are record fields; the colored validation badges and the red or yellow row tint are plain text columns; the MDR header warnings are one text field.
- Commit goes to the document register and discard back to the import page through the existing action redirects (old addresses are routes).

### Document pages (SCRUM-830, bunch 2)
- Engine: form fields `hidden` (posted, not shown: an id or part the form is opened for), `multiple` and `accept` on a file field (several files, the file dialog filter).
- Document register: the list is read whole (up to 20000 rows) by the grid instead of SQL paging; the discipline filter is one select (the address `disciplines=a,b` with several still works); the saved filters are the generic saved views of the grid; the quick filter "pending review" and the colored status pills and the frozen snowflake are plain columns; the "Open" redirect action stays a controller (SCRUM-837).
- Document register Excel export of the selected rows is a link to the file source with the ids; no row-count message page: a too large filter answers the error text of the file source.
- VDR list: the KPI chips and the vendor delay risk fold are one counts row; the top delayed vendors list is not shown; the search box has no autocomplete; paging is the grid (all rows up to 20000); the Create button is the form `vdr-create`.
- MDB list and details: part tables are three related tables; "Mark hard copy shipped" is a form page (volumes), the other marks are row actions; the DVD volume and not-applicable checkbox are form fields; the overdue badge is a column.
- Transmittal list and details: recipients on the create form are one text area, one recipient a line (name | type | organization | yes) instead of repeating rows; the document picker is a multi-select of all active documents not on hold (no project filter, no search box, no on-hold rows shown disabled, no counter); the details page actions are forms (send, cancel, record response, acknowledge) and the re-issue is a row action.
- DCC dashboard: three tables and two counts; the KPI tile icons and the "relationship explorer" card are links; RegisterIncoming (the two-panel script page) stays a controller (SCRUM-828 family).
- Document upload: the page is a form with a multi-file field; the explanation text is the help text; the old address is a route.
- Document intelligence: the queue page is two tables and four counts; the Review page stays a controller (next bunch).
- Tests removed with the screens: the list layout tests of the register, MDB, VDR list (the service-level VDR tests stay), the DCC and Transmittal view checks, the EDI index forbid test, the VDR index viewer test, the transmittal create script and controller checks.
- The scripts `transmittal-create.js` is deleted; the old `Transmittal/Create`, `MDB/*`, `DCC`, `DocumentRegister`, `VDR`, `VDR/Export`, `DocumentUpload` and `EngineeringDocumentIntelligence` addresses are routes.

### Table extraction (SCRUM-830)
- The attachment list is one grid of up to 2000 files without paging; the hint box of each row is a filter field of the result page (file id, mode `ai` or `nai`, hint), so the AI run starts at once without a hint and is run again with Apply; the result is kept ten minutes per user, file, mode and hint so its three reads extract once.
- The result page shows the counts, document info and warnings as one record, a warnings table and the rows table; the stage button is a row action of a one-row grid; nothing is saved by the page.
- The review page: the filter buttons and the tag search are the grid's own filters; every row can be selected (the service skips rows that cannot be confirmed); confirm, reject and link are small form pages with hidden fields; the bulk confirm asks on a form page instead of the browser confirm.
- The scripts `tableextraction-index.js` and `tableextraction-review.js` are deleted; the old addresses `TableExtraction`, `TableExtraction/Index` and `TableExtraction/Review` are routes.
- Tests removed with the screens: the Table extraction list paging tests and the index forbid test.

### Engineering documents list (SCRUM-830)
- The list, its counters (total, approved and rejected are links), search, discipline, status and area filters, saved views and the Excel export are the definition page `eng-docs`; paging is the grid (rows up to 20000); the project and the minimum revision count filters work from the address only; the delete is a confirmation form page (`eng-doc-delete`).
- Create, Edit, Details, UploadAttachment and the file download stay in the controller until the document viewer with comment pins, the revision modals and the attachment workflow have components (SCRUM-837 decision).
- Tests removed with the screen: the engineering documents list layout tests.

### Document intelligence review (SCRUM-830)
- The review page is a record, a stage table (set the stage from a form page), the classification, tag and comment tables with their commands as small form pages; the approve form has one checkbox per field to apply, prefilled from the suggestion; the status badges and the inline response box are plain columns and form pages; the controller is deleted and the old address `EngineeringDocumentIntelligence/Review/{id}` is a route.

### Engineering document create, edit and upload (SCRUM-830)
- Create, Edit and UploadAttachment are the forms `eng-doc-create`, `eng-doc-edit` (loaded by the operation `EngDocForm.One`) and `eng-doc-upload-attachment` over `IEngineeringDocumentFormOperations`, which carries the old logic (numbering, first revision, workflow row, access-control record, duplicate-number and frozen messages, hashed storage, first official file, background processing).
- The status select and the disabled revision box of the create form are gone (the status was always Draft, the revision 00); the manual-number box is always shown and ignored without the right (as before it only worked with the right); related equipment is the server-searched select; the validation summary is the form's own errors.
- Edit changes only the fields of its form (the old action copied the whole posted entity); the access-control record carries no client address when it cannot be read.
- The old addresses `EngineeringDocuments/Create`, `Edit/{id}` and `UploadAttachment/{id}` are routes. The controller keeps Details and the redirect of the download (SCRUM-837 decision on the viewer).

### Letter template preview (SCRUM-830, 832)
- The preview is a record page over `LetterTemplatePage.Preview` (sample values put in the tokens, values encoded); the engine got the record-view field type `html` for the template body (markup the operation already sanitized or encoded). The controller and the view are deleted; the old address `LetterTemplate/Preview/{id}` is a route. The test of the controller attribute and the view render are replaced by catalog and operation checks.
- The Admin area redirect `Admin/Employee` is a route to the employees page (the role gate of the redirect is the page permission now).

### VDR item page (SCRUM-830, 838)
- The six tabs are sections of one page: the stage stepper, the record, one row of actions (start review, approve, comment, reject, final decision, return to vendor, edit, hold, release, deactivate, add comment, upload file; each shown with the old flags), the discipline review table, submissions (the two sheet uploads are row actions), files, the document viewer, comments, workflow history and related letters. There is no default tab by status and no file preview chosen from the files table (the viewer shows the latest main file; a row action opens any previewable file).
- Every modal is a form page (hold, release, deactivate, edit details, add comment, reply, comment status, sheets, file upload, start review with the disciplines as a multi-select instead of the collapsed picker with a live summary); the start form has no "why disabled" hint and no summary card; the add-comment form no longer picks up the pin of a click in the viewer.
- A click in the viewer (the existing file-preview annotations, SCRUM-775) puts a pin through the action `vdr-comment-pin`, which makes a comment of the submission whose main file is shown with the note as its text (no category or severity; use Add comment for those); the old pin colour and the comment-number tooltip are the generic pin.
- The stage diagram role labels are part of the step label; the status badges are plain text. Tests removed with the screen: the VDR details layout tests, the VDR controller authorization tests (the operation rights are checked by the catalog tests).
- The document register Open redirect now builds `/{type}/Details/{id}` (old addresses are routes), also fixing the types whose controllers were converted earlier.

### Engineering document page (SCRUM-838)
- The tabs are sections of one page: stepper, record (identity, engineering, frozen and next-step notes, client response, related equipment and contract, the equipment this is the P&ID of as text), one row of actions (submit, check, approve, reject, revise, my IDC review three ways, issue, client response, new revision, edit, analyze, freeze, unfreeze, hold, release, upload, add comment), files, IDC, revisions, workflow history, hold history, transmittals, correspondence, viewer and comments; every modal is a form page (the submit form lists disciplines as a multi-select, the issue form shows the department or the external receiver by the delivery unit).
- There is no tab counter, no IDC progress line, no signature images, no stage-diagram icons, no overview summary cards, no hold history fold, no confirmation of unfreeze and release beyond the form page, no marking of superseded file rows, no file preview with a chosen file; the P&ID equipment are plain text, not links.
- The viewer shows the latest PDF; a click puts a pin through `eng-doc-comment-pin`, a comment with the note as its text (use Add comment for category, severity and the revision mark).
- The old address `EngineeringDocuments/Details/{id}` and `DownloadAttachment/{id}` are routes; the controller, its view and scripts are deleted, the redirect after every command is the page.
- Tests removed with the page: the engineering document details layout tests and the view-text checks of the removed views.

### DCC register incoming letter (SCRUM-838)
- The two-panel script page is the form `dcc-register-incoming` over `DccIncoming.Register`: the sender is a select of the first 200 parties or a new name and type (no search-as-you-type, no party thread preview, no number chip, no live duplicate check); dates are ISO date fields (no Persian date helper); the owner is a server-searched select and the copies a multi-select; the files are one multi-file field (no drop zone, no per-file role); the links are a JSON text; the "reply to" letter is its id (no pick from the party thread); the duplicate warning is the form error with a confirmation checkbox to register anyway; the success banner is the redirect to the letter. The controller, view and script are deleted and the address `DCC/RegisterIncoming` is a route.
- Tests removed with the screen: the DCC register incoming controller tests (the lookups of `DccOperations` and the service rules are tested elsewhere); the view tests that used the DCC controller as a host use `TestHostController`.

### Correspondence list, dashboard and my referrals (SCRUM-828)
- The mail-style workspace of the list (folder sidebar, personal tag tree, thread view, saved quick views, bulk toolbar, keyboard shortcuts, snippet preview, unread bold rows, the density and page-size settings) is the generic list: folder counters (links), a filter bar (search, type, status, priority, project, discipline, number, subject, sender, receiver, dates), the grid's own saved views, columns and export, an Excel and a CSV export link and links to the folders; address filters (`folder`, `tagId`, `unread`, `starred`, `hasAttachments`, `overdueReferral`, `confidentiality` and the others) still work; the star is a row action; the bulk archive and the bulk submit are not on the grid (the bulk action is still `letter-bulk`).
- The dashboard is a counts row and four tables (status, type, aging, needs attention); the reminder run button is not on it (action `letter-send-reminders` stays). My referrals is a grid.
- Rows are read in one go (up to 2000) instead of paging; tests removed with the screens: the list view render tests, the workspace index tests, the cartable index view-bag test and the dashboard read-only test; tests that render the removed Letter views through helpers (dashboard, my referrals, index) fail until they are deleted in the audit.

### Cartable report (SCRUM-828)
- The report is a filter bar (dates), a counts row, three generic charts (response SLA donut, received by month, received by discipline) and a subordinates table; the SLA gauge, the coloured tiles and the nested indentation (dashes) are not carried; the labels of the SLA slices are English text. The controller and view are deleted and `LetterCartable/Report` is a route. Tests removed: the report page render test and the chart tag-count check of the view; the localizer key test no longer reads the deleted views.

### Part catalogue request form (SCRUM-826)
- The edit page is the form `pc-request-edit` (loaded by `PcForm.One`, saved by `PcForm.Save` over the same service, actor, rules and messages; the row version is a hidden field; "save and submit" is a checkbox). The side commands are form pages (generate number, load an existing part, add equipment, remove equipment line, upload and remove attachment) linked from the request page; notes use the existing `pc-request-add-note`. The controller keeps only the attachment download.
- Not carried (they need engine kinds: repeating rows, dependent selects, lookup autofill, a result panel): the attribute table is a text area (one `name = value` a line, first line the free format text); commodity groups 1 and 2 list all groups, not the ones of the chosen section and group; the technical class, manufacturer and supplier are plain selects of up to 500 names (no search-as-you-type); the technical class resolution from the free format text, the derived critical and capital flags, the duplicate check panel and the manufacturer name check do not run on the form; the section-dependent and type-dependent hints, the required stars by request type and the read-only old manufacturer text are not shown; the equipment and attachment lists are on the request page, not on the form.
- Tests: none referenced the removed view.
### Letter page (SCRUM-828)
- The page is a record (with the sanitized body as an html field), one row of actions with the old flags (submit, approve, return, reject, issue, close, send, archive, reply and refer through the composer, sign, response requirement, refer, add recipient, tag, upload, PDF, history), and tables for recipients, referrals (complete is a form), attachments, approval steps (reassign is a form for an administrator), reviews, tags and the history. The single-click commands now read the row id (the actions `letter-approve`, `issue`, `close`, `send`, `archive`, `submit-review`).
- Not carried: the conversation panel with the full text of earlier messages and the thread, the embed mode of the cartable pane, the history role filter and order switch, the in-page editing of a returned letter body (use the composer), mark-as-read buttons of recipients and referrals, the confirmation prompts of the single-click commands, the tag colours.
- Tests removed with the page: the cartable details history tests, the referral conversation and button tests, the stat-grid text check of the details view.

### Form option sources (found 2026-11-27, SCRUM-830)
- A form field's `optionsFrom` must name a data source of kind `provider` with the provider `operation-options` (or entity-options); a `grid` source gives grid rows and the form host refuses it with a logged problem, so the select stays empty. 59 grid sources were used that way by the forms of the task, work order, document, import, DCC, part catalogue and help conversions; each got an options sibling `<key>-opts` (operation-options over the same operation, value and label) and the 56 forms point at it. The grid sources stay for the grids and the page filters. A definition check that rejects `optionsFrom` to a grid source is part of the audit (SCRUM-836).

### Maintenance strategy forms and bulk assignment (SCRUM-829, 826)
- Engine: the dependent select (`dependsOn`, `dependsSource`, `dependsArg`, script `pems-form-depends.js`): the failure mode follows the equipment class and the failure cause follows the mode on the strategy form, the commodity groups follow the section on the part catalogue form; `optionsFrom` gives the full list the server validates against.
- Create and edit are the forms `strategy-create` and `strategy-edit` over `StrategyForm` operations (row version hidden; Create or Edit right checked in the operation; the services' rules and messages). Not carried: the start-from-tag banner (the tag only chose the class), the failure mode search box with the five-row list, the live warning of the run-to-failure rule, the show and hide of the condition-monitoring block by strategy type, the new-version notice; the code lists show the codes (no translated labels).
- Bulk assignment is a filter page with a selectable candidate grid (max 200 rows, nothing without a narrowing filter, the conflict of each tag shown when a strategy is chosen) and the confirmation form `strategy-assign`; the multi-selects of disciplines, types and functional locations are single selects, the functional-location picker and its exact or descendants mode are not carried; the skip report is the message.
- The controller, both views and the edit script are deleted; the old addresses are routes. Tests removed: the strategy controller authorization tests, the bulk define controller tests, the strategy view render tests, and the test factory helper that built the controller.

### Hosts by proposed decision (SCRUM-837, SCRUM-833)
- `classification.json` now marks as `host` (code by a proposed decision the owner confirms, or asks for a generic component) the controllers whose remaining actions are shell fragments, script applications, redirects or file answers: Home (privacy, status and error pages), Help (article, AI answer), EquipmentImport (PDF tag scan), Notification (header dropdown and open), Team (chart shell), TaskManager (space redirect), LetterCompose (the script composer and its JSON API), Letter (reply and create redirects, editor image upload, PDF export), DocumentRegister (open redirect by document type), PartCatalogueRequest (attachment download), and the generic host ActionHost. Account, AccountRecovery, RecoveryCodes and BreakGlass were already `host`. TaskController and WorkOrderController have no screen action left and are `converted`.

### PDF engine and the letter PDF (SCRUM-839)
- The letter PDF is the layout `letter-pdf` over the record operation `LetterPdf.Record`; the page, the letterhead, the lines, the subject, the plain body, the signature and the footer follow the old drawing; the labels are in both languages (the old PDF was English only), the direction follows the language, the date and time of printing are in the footer text with the page number below it. `LetterController.ExportPdf` is deleted and `Letter/ExportPdf/{id}` is a route. PDFs still to convert to layouts: the part catalogue request print (its Excel template stays), the barcode labels, the PDF format of the grid export (`file.export`); each needs its record or rows as read operations.

## SCRUM-839 part catalogue request PDF (layout definition)
- `pc-request-pdf` is now a `pdf` layout; `PrintRecord` supplies the values (N/A fallbacks, ADD/CHANGE boxes, 5 signature columns). Renderer gained generic block `border`/`background`/`padding`/`minHeight`, `italic`, keyValue `border`/`labelBackground`. Not verified: pixel-level match with the hand-written QuestPDF page (border widths, column proportions, page fit on one A4). `BuildPdf` removed; the Excel template export is unchanged.

## SCRUM-839 barcode labels (layout definition)
- `barcode-label-pdf` and `barcode-label-sheet` are `pdf` layouts (10x6 cm page for every label via `pagePerRecord`, SVG symbol block); `RenderLabelSheetPdf` and its two tests are removed. Not verified: pixel match with the old label (spacing, QR/linear heights, Consolas font fallback on the server).
- The actions `barcode-print` and `barcode-bulk-generate` now record/generate and redirect to `/x/barcode-label-pdf?id={result}` / `/x/barcode-label-sheet?...&ids={result}` instead of downloading the file inside the action; check in the browser that the redirect shows or downloads the PDF the same way. A refused print (unknown barcode) now shows the refusal message and no file.

## SCRUM-839 grid export PDF (one engine)
- `file.export` format `pdf` now builds a layout (heading, one table, page number) and draws it with the PDF engine; the hand-written QuestPDF table is removed. Not verified: same look as before (cell padding, header shading, page number placement, long tables over pages).

## SCRUM-840 letter compose, history and compliance (form definition)
- `LetterComposeController` is deleted. `letter-compose` is a form definition (richText body, select/multiSelect fields from `operation-options` sources, `visible` rules for outgoing/response/approval) with two submit buttons (engine: `FormDefinition.SubmitActions`, `__submit`): Save the draft (redirects back to `/f/letter-compose/{id}`) and Submit (redirects to the letter). Reply and referral open `/f/letter-compose?replyTo=` / `?referTo=` (the load op `LetterCompose.Record` applies the same view check, a letter the user may not see is a 403). History is the page `letter-history`, the compliance CSV is `/x/letter-compliance-csv`; old addresses are routes.
- Not carried (to rebuild as components or accept in the audit): autosave and the saved-state indicator; recipient search as you type, the CSV/text bulk import of recipients (`ResolveRecipients`), the group menu, the directory JSON endpoint; recipients are checkbox groups (`multiSelect`) of the active employees (first 3000); attachments in the composer (drag and drop upload on a draft; now attach on the letter page with the existing `letter-upload-attachment` form after the first save); the approval chain is a set of checked approvers (order = list order, no drag order, no manager grouping); template chooser inside the form (a template is opened with `?templateId=`); the live preview modal (open the PDF `/x/letter-pdf?id=` of the saved draft); the sender block card, confidentiality minimum of a parent letter (rule is still checked on save by the service), subject counter, the warnings list of the submit check (only errors are shown), the discard button; the version bodies of the history (only subject, reason, who and when).
- Fixed on the way: `letter-add-recipient`, `letter-refer` and `letter-reassign-approver` named grid data sources as option sources (the form host refuses those); they now use `letter-departments-opts` / `letter-approvers-opts`.
- Verify in the browser: `multiSelect` with ~hundreds of employees, `__submit` button posting the other action, richText with the image upload (still `/Letter/UploadEditorImage` until that is converted), the `visible` expressions, `FormHost` 403 for a refused load.

## SCRUM-840 register incoming, reply and editor image (definitions)
- `LetterController` has no action left (Reply GET/POST, Create GET/POST, UploadEditorImage removed with `Views/Letter/Create.cshtml`, `Reply.cshtml`, `_RichTextEditorScripts.cshtml`); the empty class stays until the audit because tests still build it. Reply was already a redirect to the composer: `Letter/Reply/{id}` is a route to `/f/letter-compose?replyTo={id}`; the old reply POST (subject, body, receiver) is gone with its view.
- `letter-register-incoming` is a form (load op `IncomingRecord` for `?templateId=`, action `letter-register-incoming`, op `RegisterIncoming`: number generated unless the user has ManualNumber and ticks the switch, response rule checked before a number is taken). Not carried: the template picker as a GET form above the form (use `?templateId=`), the collapsible "More details" section, the "disabled number box until the manual switch" behaviour, related document and MR are the latest 200 (a plain select, no search), the error under the response date (it is a form error now), the register/send/save button label by type.
- Editor image: richText fields have `uploadUrl` (`/a/letter-editor-image`, action with upload profile `image`, op `LetterCompose.EditorImage`); the action host answers JSON with `result` (the address) and `pems-rte.js` sends `Accept: application/json` and reads `res.url || res.result`. Verify in the browser: the image button appears on the compose and register forms, the picture is stored under letter-images and shown, a refused file shows the toast.
- Tests removed with their views: Create_* (LetterPhase2ViewTests), Reply* (LetterPhase3ViewTests), LetterCreate_EveryFieldHasAnAssociatedLabel (SiteWideUiSystemTests). Other tests still render the deleted Letter views (Index, Details, Dashboard, MyReferrals) and `new LetterController(...)`; they fail until the audit deletes them.

## SCRUM-841 redirects (file host)
- `NotificationController.Open`, `DocumentRegisterController.Open` (controller deleted) and `TaskManagerController.Space` are gone. The file host `/x/{key}` now follows an operation answer that carries `RedirectUrl` (a path of this site; a refusal is 404): data sources `notification-open` and `document-open` over `Shell.OpenNotification` / `Shell.OpenDocument`. `TaskManager/Space` is a route to `/p/task-backlog` (the query `key` is kept; the old `view=board` is lost on the old address, the pages link to `/p/task-board` and `/p/task-backlog` directly). Tests that called the controllers now call `ShellOperations`; `RelatedDocumentLinks_GoThroughOpen` still reads a deleted Letter view (stale, audit).

## SCRUM-841 tag scan, help article, privacy, part catalogue download (definitions)
- Equipment PDF tag scan: `EquipmentImportController` deleted. `equipment-scan-pdf` (project + PDF, upload profile `pdf`) runs `EquipmentTagScan.Scan` which keeps the found tags in the cache for 30 minutes under a token and redirects to `equipment-scan-select` (discipline + a `multiSelect` of the tags that do not exist yet, `optionsArgs` token/projectId) which runs `EquipmentTagScan.Create` (Approve) and redirects to `/p/equipments`. Not carried: the scan result card with the file name and the explanation text, "Select all / Deselect all" buttons, existing tags shown as "(exists)" (they are simply not offered), the count of created tags in the message (static text). A refused or expired token shows no tags. Needs a browser check of `optionsArgs` with `token` (a query value, not a field) and of the cache key per user.
- Help article: `HelpController` deleted. `/p/help-article-view?code=` (record-view with the html content, related table); old addresses `Help/Article` and `Help/Article/{code}` are routes. Not carried: the breadcrumb, the visuals (`_HelpVisual` charts) and the standard-reference badges of the old view, the inline Ask-AI box (now a form `help-ask`; the answer is shown once on the secret answer page with a link back). `help-article.js` is removed.
- Privacy: page `privacy` and the route `Home/Privacy`. Part catalogue request attachment download: the controller (one action) is deleted; `/x/pc-request-attachment` already existed (checked per file by the service); the old address `Warehouse/PartCatalogueRequest/Download` is a route.
- Still code, with the reason: `HomeController.StatusPage` and `Error` (a status page that must open for anonymous users and for a crashing request, outside the page engine until an anonymous page option and an exception-aware record exist), `TeamController.OrganizationChart` (a `org-chart` component for the 130-line shell and its scripts is not built yet), `NotificationController.Dropdown` (the dropdown fragment of the layout). Tests removed: PublicHelpController_RequiresAuthenticatedUser_ButNoSpecificRole, PartCatalogueControllers_NeverAcceptApproverManagerOrPositionIdsFromTheBrowser, Equipment_CreateFromTags_* (replaced by a permission attribute test).
