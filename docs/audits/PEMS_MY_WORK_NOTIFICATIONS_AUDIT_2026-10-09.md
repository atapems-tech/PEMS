# PEMS — My Work & Notification architecture audit (2026-10-09)

Scope: existing My Work workspace, notification system, workflow task assignment and approval inbox. Audit first; only verified, architecture-fitting improvements were implemented. Nothing was committed or pushed. No migration was added.

## 1. Existing architecture and data flow (evidence from code + the Graphify graph)

| Concern | Source of truth | Path |
|---|---|---|
| **Actionable approvals** (anything waiting for a person) | `WorkflowInstanceStages` (Status = Pending) of **Running** `WorkflowInstances` | `WorkflowRuntimeService.GetPendingTasksAsync(employeeId, roles)` — two sources that mirror the rule enforced at action time (`ExecuteActionAsync`): (a) **role queue** (`AssignedToRole`, exact match in `RoleMatches`); (b) **responsibility stages**: only the *selected candidate snapshot* (`WorkflowInstanceStageCandidates.IsSelected`), initiator excluded (SoD) |
| Who is eligible (responsibility stages) | `EnterpriseActorResolver` → snapshot at stage entry | position/role/scope/permission/SoD filters; fail-closed |
| Modules that start workflow-runtime instances (**11**) | `IWorkflowRuntime.StartInstanceAsync` callers | PartCatalogue, Permit, MOC, TechnicalQuery, VendorDocument(VDR), EngineeringDocument, Contract, NCR(QC), IRN, WorkOrder (lifecycle + multi-discipline review), WorkRequest. **Store Requisition (MRS) and procurement approvals are NOT runtime-based** (the `StoreRequisition` module constant is only used for permissions; no code starts an instance for it), so they are absent from the unified inbox (see G6). An earlier draft of this report listed 13 including StoreRequisition - that was wrong. |
| Ad-hoc action items | `TaskItem` / `TaskService` (`/Task`) | separate, independent of workflow; has Due/Priority |
| Module lists | per-module "awaiting me" views (Permit `pendingMyApproval`, WorkOrder `assignedToMe`, Part Catalogue `AwaitingMe`, Letters referrals) | unchanged |
| **Notifications** | `Notification` (UserId, IsRead, `DismissedDate`, RelatedType/Id, LinkUrl) via `NotificationService` | server-side per-user; `NotificationController` (antiforgery POSTs, local-URL-only redirect, 404 for other users' ids); `NotificationHub` (`[Authorize]`, group from the authenticated principal, no client-supplied id); `SignalRNotificationBroadcaster`; `notifications-hub.js` (counter re-synced from the server after reconnect) |
| Unread counter | `GetUnreadCountAsync` (active + unread) | authoritative on the server; SignalR only pushes it |
| Completed work | `ResolveRelatedAsync` dismisses related notifications when the task is done; reading/dismissing never completes anything | verified by existing tests |
| Dashboard "My Work" | `HomeController.Index` → `MyWorkSummary` | Permit approvals (from the runtime inbox), my Work Orders, referrals, engineering documents (status heuristic) |

Background jobs: **none** exist (`BackgroundService`/`IHostedService` absent). SLA is **display-only** (`WorkflowReadModelService`: due = AssignedDate + `TimeoutHours`; Escalation is not executed). No stage in the **clone** database has a timeout configured (`TimeoutHours` is NULL on all stages; not checked on the shared dev database).

## 2. Candidate repository comparison (GitHub API, checked 2026-10-09)

| Repo | License | Activity | What it is | Relevant to PEMS? | Decision |
|---|---|---|---|---|---|
| Taskist/taskist | MIT | pushed 2026-10-02, 6★ | ASP.NET Core MVC (.NET 10) task/bug tracker | its `NotificationService` is UI flash messages (`NotifyData{Type,Message}`), not a persisted inbox; dashboard widgets are counters | nothing to reuse; PEMS already has a stronger persisted inbox |
| volkanvanlioglu/TaskManagementApp | MIT | pushed 2026-05-25, 1★ | Blazor sample | `NotificationItem` = {UserId(string), Title, Message, IsRead} — a strict subset of PEMS `Notification` (no link, related entity, dismiss); `DashboardUpcomingDueDto`(title, due, assignees, `CanOpenTask`) is a useful *shape* | pattern only (due/overdue list with "can open"); no code copied |
| SteffenBlake/TaskBud | MIT | last push 2021-06-01 (stale) | small MVC task organizer + SignalR `TaskHub` | hub only holds event-name constants; no per-user groups/authorisation | rejected (stale, weaker than PEMS hub) |
| danielgerlag/workflow-core | MIT | pushed 2026-09-24, 5.9k★ | general workflow **engine** | PEMS already has `PEMS.WorkflowPlatform` (instances, stages, candidates, SLA, SoD). Adopting it would be a **second workflow engine** — explicitly forbidden | rejected as a dependency; no code copied |

All four are MIT, so licence would not block reuse, but **no code was copied or added as a dependency** — none provides a capability PEMS lacks. (Repository code was inspected through the GitHub API only; no clone/build was performed.)

## 3. Confirmed defects / gaps (root causes)

| # | Finding | Evidence | Root cause | Status |
|---|---|---|---|---|
| D1 | **My Work ignored all workflow modules except Permit.** A Part Catalogue approver (employee 70, request #18 pending) saw nothing about it on the dashboard | clone DB: pending `PartCatalogue/DirectManager` for candidate 70; dashboard card list had no such category; `HomeController` counted only `BusinessObjectType == "Permit"` from the runtime inbox | the generic inbox existed but was consumed for one module | **fixed** |
| D2 | No unified list of "approvals awaiting me" (module, stage, age, due/overdue, link to record) | only per-module screens | missing view over the existing inbox | **fixed** (`/MyWork`) |
| D3 | `GetPendingTasksAsync` loaded **every pending stage in the system** into memory then filtered roles in C# | clone: 741 pending rows; dev DB: 740 (almost all engineering "Issue/Check" role-queue stages) on each dashboard load for permit approvers | role CSV matched only in memory | **improved** (SQL pre-filter on the role CSV; exact in-memory match retained) — gain is limited when the user's role genuinely matches almost all rows (SystemAdmin: 739/741) |
| D4 | Pending tasks carried no SLA information | `PendingTaskDto` had no due date although `WorkflowStage.TimeoutHours` exists | DTO gap | **fixed** (additive `TimeoutHours/DueDate/IsOverdue`). `IsOverdue` is a *calculated display value*, true only for a stage that has a configured `TimeoutHours`; **no SLA, reminder or escalation exists today** (no stage in the clone has a timeout) - the page hides the Overdue chip and says so when no stage has an SLA |
| D5 | A user's *personal* task would sort behind hundreds of role-queue tasks (seen in the browser: request #18 was last of 740) | UI check | no personal/queue distinction | **fixed** (`IsPersonal`, sorted first, filter chip, badge) |
| G1 | No reminders / escalation for due or overdue work (no background job) | no hosted services; SLA display-only | not built | **not implemented** — needs an owner decision (see §6) |
| G2 | No retention policy for notifications | dismissed rows are kept forever by design ("never deleted, audit") | policy not defined | **not implemented** — decision needed |
| G3 | `Notification` creation is not idempotent (duplicate events can create duplicates) | `CreateAsync` has no de-duplication key | by design simplicity | **not implemented** — a change in semantics (legitimately repeated notices for the same record) needs a business rule |
| G4 | Engineering-document "awaiting review" tile is a *status* count (not the user's actual assignment) | `HomeController` counts documents by status/discipline | legacy heuristic | **left unchanged** (changing a visible KPI needs owner agreement); the new page shows the real role-queue/personal tasks |
| G5 | Stage names in the list were shown as stored (English) in Persian UI | `PendingTaskDto` had no `NameFa`; stored `NameFa` is NULL for most stages | DTO gap | **fixed** (data `NameFa` first, then 40 `MyWork_Stage_*` resource keys; guard test enumerates every known stage name) |
| G6 | **MRS / Store Requisition and procurement approvals are not in the unified inbox** | no `StartInstanceAsync` caller for them; their approval is a permission-gated status transition (`IStoreRequisitionLifecycleService`), not a runtime stage | these modules predate/bypass the workflow platform | **not implemented** - showing them in My Work needs either migrating them to the runtime or a per-module query; owner decision |

Notification subsystem itself: **no defect found.** Per-user ownership, antiforgery, open-redirect protection, hub identity from the principal, unread-counter resync, paging and the dismiss/resolve lifecycle are already implemented and covered by `NotificationLifecycleTests` (23 tests, incl. cross-user and hub identity cases).

## 4. What was implemented (minimal, no migration)

* `PEMS.WorkflowPlatform`: `PendingTaskDto` + `TimeoutHours`, `DueDate`, `IsOverdue`, `IsPersonal`; `GetPendingTasksAsync` role branch pre-filtered in SQL (`AssignedToRole` CSV `Contains`), due date computed with the same rule as `WorkflowReadModelService`.
* `PEMS.Application`: `IMyWorkService`, `MyWorkDtos` (summary + paged approvals, filters: module / overdue / assigned-to-me).
* `PEMS.Persistence`: `MyWorkService` (reuses `IWorkflowRuntime` — **no new engine, table or task store**); DI registration.
* `PEMS.Web`: `MyWorkController` (read-only; identity from `ICurrentUserContext`, no client-supplied user/employee id, no POST actions), `Views/MyWork/Index.cshtml` (responsive, RTL), `WorkflowTaskLinks` (verified routes only; unknown types → no link), dashboard card "Other approvals" (Permit and engineering documents keep their own tiles), 28 EN/FA resource keys.
* Notifications stay separate: the page shows the unread-notification and open-action-item counters as links only; reading/dismissing a notification never touches a workflow stage.

## 5. Verification

| Check | Result |
|---|---|
| New tests `MyWorkInboxTests` (31 theory/fact cases: exact-role vs substring, stage/instance filtering, due/overdue, ordering, paging, filters, personal vs queue, cross-user, link map & unsafe input, controller identity & read-only) | **31 passed, 0 failed** |
| Prefilter parity on **real SQL Server** (clone DB, read-only, SaveChanges suppressed): old unfiltered logic vs new, 8 role sets incl. `Engineering`, `Manager` (substring traps) | **identical in all 8** (e.g. SystemAdmin 739=739, DocumentController 706=706, substring roles 0=0); generated SQL contains the LIKE pre-filter |
| Real browser (Chromium, clone DB via a separate instance pinned with `--ConnectionStrings:...`; session check showed it connected only to `PEMSDB_PcatClone`) as the clone-only test account `test-pcat-dm` | dashboard shows "1 Other approvals"; `/MyWork` lists request #18 first (*Assigned to me*, link `/Warehouse/PartCatalogueRequest/Details/18`) before 739 role-queue document tasks; no horizontal scroll at 375 px; Persian page `dir=rtl lang=fa`; unknown module filter → empty state; hostile paging parameters → normal page |
| Focused regression (workflow / notifications / part catalogue / permit / authorization / my-work), run before the Phase-4 changes | **952 passed, 0 failed, 0 skipped** (28 m 15 s) |
| **Full suite** (`PEMS.Tests`, 2,462 tests, built from the tree before the later UI-only edits; those edits touched `pems-ui.js`, `_Layout.cshtml`, CSS and views, not compiled code) | **2,461 passed, 1 failed, 0 skipped** (1 h 7 m). The one failure is the known baseline timing test `MrsImportBatchCapacityTests.Five_thousand_row_file_stages_and_commits_as_one_operation_within_a_practical_time` (9 m 49 s; the machine was also running browser crawls) - unrelated to this change |
| Final tree: Web/UI-guard + My Work + Part Catalogue + Engineering-documents UI + Notifications groups (after all UI edits) | **243 passed, 0 failed**; full solution Debug build **0 errors** |
| **Database isolation** | the suite defaults to the disposable `PEMSDB_RegressionTest` and refuses `PEMSDB` unless `PEMS_TEST_ALLOW_SHARED_DB=1` (not set); tests that boot the real app override the connection to the regression DB; live check: the test host held sessions only to `PEMS_CONC_*`, `PEMS_TEST_VDRVENDOR_*`, `PEMSDB_MrsCommitConcurrencyTest`, `PEMSDB_RegressionTest`, never `PEMSDB`; the only `PEMSDB` sessions were the user's dev server |
| **Clone seeder investigation** | the temporary clone instances produced exactly six audited changes (`AuditLogs`): `RolePermissionGrant 782` (StoreRequisition.Create) toggled False->True at each app start (net zero, repeats at every earlier start since 2026-10-08 = routine startup behaviour) and two `LastLoginDate` updates for the clone-only account `test-pcat-dm`. Nothing was reset or deleted. Noted as a pre-existing quirk: that grant is briefly inactive on every app start |
| Phase 4 (browser, clone DB, EN + FA) | sidebar entry directly under Dashboard (ungated like Dashboard; active state verified); Persian stage names; no-SLA note and hidden Overdue chip; unrelated user sees an empty state; anonymous `/MyWork` redirects to Login |

## 6. Unresolved / not done (needs a decision or separate work)

1. **Reminders & escalation** (G1): requires a hosted service and an owner-approved SLA/escalation policy; in the clone **no stage has `TimeoutHours`**, so no overdue state can occur there until SLAs are configured.
2. **Notification retention** (G2) and **de-duplication** (G3): need business rules.
3. **Real-time refresh of the `/MyWork` page** was *not* added: the existing SignalR hub only carries notification counters; the page is server-rendered and refreshed on load. Pushing task changes would need a new hub message and was judged not justified without a requirement.
4. Engineering-document KPI semantics (G4).
5. ~~Sidebar entry~~ done (see verification).
6. Not tested: other browsers, real concurrent double-action on the same stage (the engine's existing optimistic checks apply; no new write path was added), a SQL-Server run of the full suite.
