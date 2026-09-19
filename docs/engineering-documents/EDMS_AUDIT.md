# Engineering Documents (EDMS) — enterprise capability audit

Scope: `EngineeringDocuments`, `DocumentRegister`, `DocumentRevisions`, `DocumentWorkflow`, `Transmittal`, `EngineeringDashboard`, `ReportController.Engineering`, and the entities/services behind them.
Method: full code read of the module (evidence with file:line in §B1–B12) + public documentation of mature EDMS products (§B2). No PEMS workflow was redesigned; only category **B** items were implemented.

## B2. Industry benchmark (capabilities only — no vendor ranking, no UI copying)

Public sources reviewed: SAP DMS overviews (LeverX, Surety Systems, SAPinsider), AVEVA Advanced Document Management / Assai, Hexagon HxGN SDx2 help & brochure, Assai product pages.
Statements below are limited to what those sources say; "?" = not stated in the material I could reach.

| Capability | PEMS today | SAP DMS | AVEVA / Assai | HxGN SDx | Gap | Priority | Action |
|---|---|---|---|---|---|---|---|
| Register with rich filtering | 5 filters (search, discipline, status, area, project*/min-rev* via URL only); SQL; fixed sort | search/text retrieval | Master Document Register, metadata-driven | ? | Sort + column choice missing | C | defer |
| **Export of the filtered list** | **was missing** (only Register had one) | ? | reports exportable to Excel | ? | **Filtered-list export** | **B** | **implemented** |
| Revision control | numeric "00,01…"; superseded chain; unique (doc,rev); history tab | versions, history, revert | strong revision control, compare previous revisions | automated revision mgmt, check-in/out | no compare, no letter scheme, revision fields (reviewer/approver/effective) never populated | C/E | defer |
| Status lifecycle | free-string statuses, mapped from workflow stages | in process / released / obsolete | predefined, configurable status + revision control | lifecycle state drives workflow | no enum; badge/code mismatch (A–D vs Approved/Rejected) | C | defer |
| Workflow | generic engine, sequential + parallel IDC, approve/reject/return, history | configurable approval/review | configurable | parallel + sequential steps, notifications | no due dates/SLA, reassign, delegate, notify (schema fields unused) | E | architectural decision |
| Personal task inbox | none for documents (org-wide pending list only) | ? | personal inbox of outstanding items | workflow assignments | "my tasks / overdue / returned" | C/E | defer |
| Transmittals | Draft/Sent/Ack/Cancelled, recipients, revision+file+hash snapshot, ack, response due, reissue | distribution via workflow | transmittals + distribution matrix, cover sheets, response sheets | incoming / outgoing / internal transmittal object | no direction, no package/ZIP, no list filters, no register export | C | defer |
| Distribution visibility | transmittal side complete; document page shows number/rev/date/status only | ? | inbox + matrix | transmittal workflow | recipients & ack not shown on document; Draft/Cancelled mixed in | C | defer |
| Overdue tracking / alerts | plan-vs-actual delay only; no workflow due dates | ? | overdue e-mails, dashboards | ? | workflow-based overdue | E | architectural decision |
| Comments / response | comment register with pins, response, action status | ? | redlining, comment extraction, response sheets | redlining | comments not tied to the revision raised on; no user id | C | defer |
| Reporting | in-memory engineering report + dashboard, no export | ? | standard reports, export to Excel | ? | report scoping/perf/export | C | defer |
| Access control | authenticated + discipline scope; **many actions have no server-side permission check** | role-based | role-based | document access controls | permission gaps | E | separate security task |

## A–E classification of findings

**A – already correct**: SQL-side filtering + paging; append-only WorkflowHistory; revision uniqueness + supersede chain; file SHA-256 per revision file; transmittal snapshot (revision/file/hash) and acknowledgement model; holds (append-only) blocking approve/issue/transmit; IDC parallel review with referral matrix; Register export (filters, scope, 20 000-row cap).
**B – missing & clearly required → implemented now**
1. Export of the **current filtered Engineering Documents list** (same filter object, same discipline scope, no paging, capped).
2. Register export column **Issue Purpose was always blank** (projection did not carry it) — fixed.
3. Index status filter omitted the real status **"Revise & Resubmit"** — added.
**C – useful, deferred**: list sorting; column show/hide/saved views on Index; Transmittal list filters + register export; recipients/ack on the document page (and hide Draft/Cancelled); revision comparison; comment↔revision binding; report export/scoping; my-tasks/overdue views (can reuse Status filter + a counter).
**D – not appropriate**: copying vendor UIs, redlining engine, distribution-matrix engine, large BI dashboard (PEMS must stay operational and minimal).
**E – needs a separate architectural decision**: workflow SLA/due dates/delegation/notifications (schema has `TimeoutHours`, `IsEscalatable`, `IsDelegatable` unused); linking workflow instances to a revision; revision status/reviewer/effective-date population; **server-side permission checks** on Index/Details/Delete/CreateRevision/Submit/Check/Reject/Issue/ReviseAndResubmit/RecordClientResponse/comment actions/DeleteAttachment and `DocumentRevisions`/`DocumentWorkflow` (currently only authentication + discipline scope; not changed here because tightening would change who can act in live projects); free-string status vocabulary; prior IDC referrals deleted on resubmission.

## B3–B6 List, search, export, revisions (evidence)
* Index: `EngineeringDocumentsController.Index` binds `EngineeringDocumentListFilter` (Search, ProjectId, DisciplineId, Status, Area, MinRevisionCount, Page, PageSize) → `EngineeringDocumentRepository.ApplyFilters` (SQL). Search = LIKE on number/title. Sort fixed `CreatedDate desc`. No column show/hide/reorder/saved view (only resize/reset). Register has saved filters.
* Export before: only `DocumentRegisterController.ExportExcel/ExportSelected`. After: `EngineeringDocumentsController.Export` (GET, same filter) → `IEngineeringDocumentService.GetAllMatchingAsync` → `EngineeringDocumentRepository.GetAllMatchingAsync` (reuses `ApplyFilters`) → `EngineeringDocumentListExport.Build` (uses shared `ExcelExportHelper`). Mapping shared with Index via one `ToListDto`; scope shared via `ResolveListScopeAsync`. Cap 20 000 rows → friendly redirect with message. Columns = list columns (there is no column-selection UI, so "selected columns" = the visible list columns). Export has no permission stricter than Index (it can never return rows the user could not list).
* Revisions: numeric only; `Document.RevisionNo` cached; previous revision `Status="Superseded"`, `SupersededRevisionId`; `IssuePurpose`, `PreparedBy/CheckedBy/ApprovedBy/Reviewer/EffectiveDate` on revision never populated; no comparison; attachments bound to revision with `IsOfficial` + SHA-256.

## B7–B12 Status, workflow, review, transmittal, distribution, reporting (evidence)
Status set: Draft, Submitted, IDC Review, Checked, Approved, Issued, Rejected, Revise & Resubmit, Client Approved, Archived (revision: Superseded). Transitions and authority: workflow runtime roles per stage + controller checks for Approve (discipline + separation of duties), Freeze/Hold permissions; history in `WorkflowHistory` (actor, role, decision, comment, time, IP). Details page shows stage diagram, IDC matrix and history; **not** current assignee, due date, next action. Operational lists: only `DocumentWorkflow/Index` (org-wide Submitted/Checked, excludes IDC). Transmittals: see table; document page "Related Transmittals" lacks recipients/ack. Reports: `ReportController.Engineering` (in-memory, no scoping/export), `EngineeringDashboardController` (scoped, no export).

## B14 Implemented improvements
Filtered-list Excel export (Index toolbar, one button); Issue Purpose fix in Register export; status option; tests `EngineeringDocumentListExportTests` (filters honoured & paging ignored, discipline scope parity with the list, cap behaviour, Issue Purpose projection, workbook content); live check: export row count = list total for the unfiltered list (324 = 324) on the dev database.

## B15 Deferred (see C and E above)
Sorting, column management/saved views, Transmittal list filters/register export, recipients+ack on document page, revision compare, comment↔revision, report export, my-tasks/overdue, workflow SLA/notifications, permission hardening (highest-priority separate security task).

### Sources
- https://leverx.com/solutions/sap-dms · https://www.suretysystems.com/insights/a-guide-to-sap-dms-streamline-document-control-and-analysis/ · https://sapinsider.org/topic/sap-data-and-data-management/sap-dms/
- https://www.aveva.com/en/products/advanced-document-management-for-asset-heavy-industries/ · https://www.aveva.com/en/perspectives/blog/aveva-s-digital-twin-now-with-assai-document-management/
- https://docs.hexagonppm.com/r/en-US/HxGN-SDx-Help/1130263 · https://aliresources.hexagon.com/smart-digital-reality/transforming-document-management-and-document-control
- https://assai-software.com/engineering-document-management-with-assai-software-leading-edm-solution/ · https://assai-software.com/maximizing-efficiency-the-role-of-dms-in-review-cycles/
