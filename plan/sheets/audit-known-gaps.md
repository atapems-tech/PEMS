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
