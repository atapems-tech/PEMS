# PEMS Portal UX — Azure-Inspired Audit

*Audit-first, per the "PEMS Engineering Documents — Azure Portal Inspired UX Audit & Implementation" directive. No code changed while producing this document. Findings below are traced to exact files/lines throughout — this is not a re-derivation of the Enterprise UI/UX Design Baseline doc (`.github/Role/PEMS — Enterprise UIUX Design Baseline.md`), which is treated as an existing, pre-approved design authority and cross-referenced where relevant.*

**Bottom line up front (see the closing section for the full answer):** PEMS does **not** need an architectural rewrite to move toward this model. Most of the "Azure-like" building blocks either already exist (permissions, notifications-with-SignalR, a search-provider framework, a widget-based dashboard layout service, a command-bar+tabs pattern already live on the Engineering Document Details page) or can be added as small, additive, read-only aggregations over existing data. The genuine gaps (Help, generic Favorites, Recent Items, a `UserPreference` entity) are real but each is small and isolated — none require touching EDMS workflow, authorization, or document/revision integrity.

---

## A. Existing Shell

- `PEMS.Web/Views/Shared/_Layout.cshtml` (258 lines) is the **only** layout in the real app — Admin area reuses it verbatim (`Areas/Admin/Views/_ViewStart.cshtml` sets `Layout = "_Layout"`). **EXISTING — REUSE.**
- Persistent header (`.pems-topbar`) present on every page: sidebar toggle, brand, global search box, theme dropdown, language dropdown, notification bell + unread badge, user avatar menu (My Profile / Manage Passkeys / Logout). **EXISTING — REUSE.**
- **RISK / EXISTING — REFACTOR:** theme switching is implemented **twice**, inconsistently. `ThemeController.SetTheme` writes a server cookie but only ever normalizes to `light`/`dark`; `wwwroot/js/admin.js` independently reads/writes `localStorage['pems-theme']` for all 5 themes (light/dark/ocean/warm/midnight) and **never calls** `ThemeController`. The dropdown links are plain `data-theme` anchors intercepted only by the JS. Net effect: the FOUC-safe server-rendered `data-theme` attribute is unconditionally overwritten by client JS on every load, and the two mechanisms disagree about how many themes exist. No `prefers-color-scheme`/"System" option exists anywhere.
- **RISK / DUPLICATE:** the sidebar-toggle button (`#sidebarToggleBtn`) has **three** independent click handlers (inline script in `_Layout.cshtml`, and two near-duplicate blocks in `admin.js`), toggling three different, non-overlapping class names (`pems-collapsed`/`pems-opened`, `show`, `collapsed`/`expanded`). None persist the collapsed state.
- **MISSING:** no Help/documentation entry point in the header, despite the Enterprise UI/UX Baseline explicitly mandating one (§7 of that doc, "TopBar must provide ... Help"). No "Quick Create" entry point either (same section).

## B. Existing Navigation

- `PEMS.Web/Views/Shared/_SidebarMenu.cshtml` (553 lines) — a single global, **grouped accordion** sidebar (not flat): Home, Quick Setup, Engineering Management, AI & Knowledge, Correspondence & Doc Control, Quality Control, Contract Management, Project Controls, CMMS Integration, Inventory & Materials, Administration. **EXISTING — REUSE** as the structural base; it already groups by module the way the Azure-inspired directive's own example structure asks for, using PEMS's real module names rather than an invented taxonomy.
- Section expand/collapse is **computed per-request from the active route**, not stored as a user choice (`SectionClass`/`ContainsActive` helpers, lines 29/48) — feels persistent but isn't a real preference. **EXISTING — EXTEND** if "remember which sections I keep open" becomes a real requirement (needs a preference store — see §D/§O).
- **RISK (likely copy-paste bug):** the "Inventory & Materials" section (line 397) is gated by the exact same `MenuAccess.CmmsIntegration` role array as the "CMMS Integration" section — worth a one-line fix, unrelated to this UX task's core scope but cheap to flag.
- **MISSING:** no reusable "service-level" contextual sub-navigation component. The closest existing pattern is ad hoc **in-page Bootstrap tabs** on ~10 different detail pages (Engineering Documents, Equipment, VDR, Reliability, Criticality Assessment, RCM/FMEA, Material Item, etc.) — each view builds its own tab markup independently; there is no shared partial. A "Engineering Documents service menu" (§5 of the directive) would be a genuinely new, reusable secondary-nav pattern, but it should be built as one shared component usable by every module's detail pages, not an EDMS-only one-off.
- `PEMS.Web/Views/Shared/_Breadcrumb.cshtml` (26 lines) exists and is rendered unconditionally from the layout. **EXISTING — REUSE**, but it's thin: always "Dashboard" + optional `ViewData["Section"]`/`["Title"]` — not a real multi-level, clickable trail. **DUPLICATE:** at least 8 other views (`MaintenanceStrategy/BulkCreate`, `FunctionalLocation/Index`, `Asset/Index`, `Asset/Details`, `WorkRequest/Create`, `WorkRequest/Details`, `Permit/Create`, `Permit/Details`) contain hand-rolled breadcrumb-style markup instead of using the shared partial.

## C. Existing Home/Dashboard

- `HomeController.Index()` mixes three genuinely different personalization layers that don't share one model:
  1. Global, non-personalized KPI cards + a doughnut chart (Projects/Documents/PendingApproval/Rejected/OpenRevisions/OpenWorkOrders/AIAlerts/Equipment/OpenMR/Transmittals).
  2. `IUserDashboardService`/`UserDashboardWidget` (`PEMS.Domain/Entities/UserDashboardWidget.cs`) — **stores only layout** (which of 8 widget keys are visible, order, size) per user; the actual data-fetching for each widget lives in a `switch` inside `HomeController` itself, and 2 of the 8 catalog keys (`MyDisciplineTasks`, `KnowledgeBaseShortcut`) have no case and silently render `0`. **EXISTING — EXTEND**: this is exactly the right shape for "Favorites of dashboard widgets" but is incomplete and has the fetch logic in the wrong layer (controller, not service).
  3. A hand-built "My Work" section (Permit approvals pending, WorkOrders assigned to me, open Letter referrals) — genuinely per-user and actionable, but **CMMS + Correspondence only**.
- **MISSING:** Engineering Documents, VDR, TechnicalQuery, and MOC have **zero** "things awaiting my action" queries anywhere in the codebase today (confirmed by direct search for per-user/per-discipline filtering patterns in each controller). This is the single largest concrete gap for a "My Tasks" feature and the most valuable one to close, since EDMS is this task's named focus.
- Per-module "my pending work" that DOES exist and can be extended as the pattern to copy: `LetterService.GetDashboardAsync` (`MyOpenReferrals`), `PermitController.Index(pendingMyApproval:true)`, `WorkOrderController.Index(assignedToMe:true)`.

## D. Existing User Preferences

- `ApplicationUser` (`PEMS.Persistence/Identity/ApplicationUser.cs`) carries only identity/org fields (`FullName`, `EmployeeId`, `DisciplineId`, `IsActive`, `CreatedDate`, `LastLoginDate`, `MustChangePassword`) — **no preference fields of any kind.**
- **MISSING:** no `UserPreference`/`UserSetting`/`UserProfile` entity exists anywhere in `PEMS.Domain/Entities`.
- `ISavedFilterService`/`SavedFilter` (`PEMS.Domain/Entities/SavedFilter.cs`) is a genuinely **generic-by-design** mechanism (arbitrary `PageKey` + raw `QueryString` + `Name`/`IsDefault`) — but it is wired into exactly **one** controller (`DocumentRegisterController`) despite being architecturally reusable everywhere. **EXISTING — EXTEND.**
- Theme (see §A) and sidebar-collapsed state (see §B) are the two clearest "should be a real per-user preference but currently isn't" cases.
- `DataGridPageSize` (`PEMS.Application/Common/DataGridPageSize.cs`) is a **stateless** normalization helper (clamps to 20/50/100, defaults to 50) — not a preference; resets every navigation with no `pageSize` param.

## E. Existing Tasks

- No unified "My Tasks" abstraction exists. Three independent, non-shared implementations exist today (Letters, Permit, WorkOrder — see §C), each hand-built inside its own controller/service.
- **MISSING** entirely for Engineering Documents, VDR, TechnicalQuery, MOC.
- Critically: none of the three existing implementations are backed by a persisted "Task" row — they are all **live queries against existing workflow/status fields** (e.g., `WorkOrder.AssignedEmployeeId`, `LetterReferral` still-open rows, `IWorkflowRuntime.GetPendingTasksAsync`). This is the right pattern to keep — a "My Tasks" feature should be built the same way (live aggregation across existing tables), **not** a new persisted task-tracking table, per the directive's own "do not create a duplicate workflow engine" instruction.

## F. Existing Notifications

- `Notification` entity, `INotificationService`/`NotificationService`, `NotificationHub` (SignalR) + `wwwroot/js/notifications-hub.js` — this is a **solid, genuinely real-time, end-to-end working mechanism**, generically designed (arbitrary `RelatedType`/`RelatedId`, doc comment explicitly states reusability intent). **EXISTING — REUSE**, no reason to build a second notification pipeline.
- **Gap:** in practice, only the Letters/Referral flow ever calls `CreateAsync` — EDMS, VDR, CMMS, TQ, MOC never create a notification today, despite the infrastructure supporting it trivially.
- **Data-model gap relevant to §18 of the directive:** the `Notification` entity has no field distinguishing "informational" from "actionable." Per the directive's explicit instruction to keep Notifications and Tasks as **separate concepts** (not one unread counter), the right fix is **not** to add an `IsActionable` flag to `Notification` — it is to keep "My Tasks" as its own read model (§E) entirely separate from the Notification feed, exactly as the directive requires. No schema change needed here at all.

## G. Existing Search

Two **parallel, non-integrated** implementations exist — a genuine duplicate worth resolving, not two complementary systems:

1. **`GlobalSearchController` + `Views/GlobalSearch/Index.cshtml`** — the one wired to the topbar search box. Directly queries `PEMSDbContext` for 8 hardcoded entity types (Documents, VDR, Equipment, Vendors, Letters, Projects, Permits, WorkOrders), `Take(15)` each, no pagination, no permission scoping. Its own code comment references a prior audit finding that this box "is decorative and does nothing" — it's since been made functional but never reconciled with (2).
2. **`SearchApiController` + `GlobalSearchService` + 19 `ISearchableEntityProvider` implementations** (`PEMS.Persistence/Servises/SearchProviders/`) + `pems-global-search.js` (`data-pems-search` typeahead component, used as a picker in 22+ forms). This is **not** a fan-out aggregator — `GlobalSearchService.SearchAsync` requires an exact `EntityType` and delegates to exactly one provider, throwing if none matches. It's a single-entity-type lookup framework, well-suited to form pickers, not to a unified "search everything" experience.

**RISK — security-relevant, not merely cosmetic:** of the 19 providers, only `NotificationSearchProvider` filters by the current user at all. The other 18 (Equipment, EngineeringDocument, Employee, Vendor, MaterialRequisition, WorkOrder, Contract, SparePart, etc.) filter only by `IsActive` and an *optional*, inconsistently-applied `projectId` — any authenticated user can search and see labels for records system-wide regardless of project/discipline/role scope. This should be fixed **before** any of these providers are reused as the backbone of a new unified global search, or the new feature would surface the same gap more prominently.

No caching anywhere in the search path; several providers additionally run a second, unconditional up-to-500-row "fallback pool" query when the first pass returns fewer than 5 matches — worth keeping an eye on if search volume grows, not urgent today.

## H. Existing Help

**Entirely absent.** No `HelpController`, no `HelpTopic`/`HelpArticle` entity, no `Views/Help/`, no FAQ, no "Learn more" links anywhere in the app. `title="..."` attributes exist widely but are exclusively icon-button accessibility labels ("Edit", "Delete", "SHA-256: ...") or truncated-value tooltips — never explanatory guidance. `SharedResource.resx`/`.fa.resx` (94 entries each, full EN/FA parity) is a UI-label translation file; only 4 entries are longer than a phrase, and those 4 are dashboard-metric captions, not general help content. **This is genuinely greenfield** — there is nothing to extend, only a clean, minimal architecture to design (see §P).

## I. Existing Engineering Documents UI

This module is **considerably more mature than "a collection of independently designed MVC pages"** — several Azure-inspired patterns are already live here specifically:

- **Details.cshtml (1347 lines)** already has: a breadcrumb, a genuine **command-bar pattern** (state-dependent primary actions rendered via an `if/else if` chain keyed on `Model.Status`, with low-frequency actions — Edit, Create Revision, Freeze, Hold, AI analysis — grouped into a "More ▼" dropdown), **tabs** (Overview / Attachments & AI / IDC Review / Revisions / Workflow History / Correspondence) explicitly redesigned away from one long scrolling page (per its own code comment), status badges with text+color via a shared `DocumentStatusBadge` helper, a dedicated Revision History tab, a dedicated Workflow History tab rendered as a visual timeline with the raw audit table preserved underneath, a Correspondence tab linking related Letters, and tooltips on icon/action buttons.
- **Index.cshtml (254 lines)** has a cross-module quick-nav row (Documents/Register/MDR Import/VDR/Transmittals), 6 KPI cards, search+filter toolbar with an offcanvas advanced-filter panel, and a resizable-column grid.
- **Gaps relative to the target model:** no Transmittal cross-link anywhere on the Details page (only Letters/Correspondence and an Equipment P&ID reverse-lookup); the workflow-stage diagram partial (`_WorkflowStagesDiagram`) is **static/hardcoded**, not driven by real `WorkflowStage` data (explicitly noted as deferred in its own code comment); almost none of Details.cshtml's rich pieces (timeline, PDF-pin commenting, the dozen modals) are extracted into reusable partials — only the KPI card, pagination, and (static) stage-diagram are shared components today, so a portal-wide command-bar/timeline component would need to be extracted from this page's inline markup, not invented from nothing.
- **Authorization gap already known and disclosed** (not newly discovered — see `docs/audits/PEMS_EDMS_PHASE4_FINAL_REPORT.md`): `Submit`, `Check`, `Reject`, `Issue`, `IdcRespond`, `ReviseAndResubmit`, `Delete`, and all attachment actions have **no** explicit permission check today (only workflow-state guards); only `Create`, `Edit`, `Freeze`, `Unfreeze`, `Hold`, `ReleaseHold`, and `Approve` do. This is out of scope for a UX task to fix silently, but the audit record should note it since a "permission-aware command bar" (§20 of the directive) would otherwise imply a false sense of enforcement — the view should never claim tighter gating than the controller actually has.
- SoD (self-approval) protection **is** already wired for `Create`→`Approve`/`Edit`→`Approve` via `IAccessControlService.AuthorizeAsync`, in both `EngineeringDocumentsController.Approve` and the duplicate entry point in `DocumentWorkflowController.Approve`.

## J. Existing Design System

- 9 CSS files under `wwwroot/css/`: `site.css` (core tokens + shell + 5-theme engine), `pems-data-grid.css` (opt-in grid standard), `pems-global-search.css`, `admin-enterprise.css`, `admin.css`, `admin.rtl.css`, `administration-design-system.css`, `cmms-rtl.css`, `auth.css`.
- **RISK — fragmentation:** three independent `:root` token definitions exist (`site.css`, `administration-design-system.css` with a different blue accent color, `auth.css` with a different navy hex) using overlapping names but different values, plus two different Admin-scoping conventions (`.admin-area` vs `.admin-page`/`.admin-page-shell`). Building new Azure-inspired components (Favorites panel, Help panel, My Tasks widget) on top of this without first picking one source of truth would add a fourth inconsistent surface.
- **DEAD CODE:** `cmms-rtl.css` is never `<link>`-referenced anywhere in the app — confirmed via exhaustive grep. Either wire it in or remove it; shipping unused CSS is low-risk but should be a conscious decision, not left as-is by accident.
- The Enterprise UI/UX Design Baseline (`.github/Role/PEMS — Enterprise UIUX Design Baseline.md`, 1664 lines) already mandates almost everything this directive asks for — sidebar favorites/recent, topbar Help/Quick Create/My Work/Tasks, progressive disclosure, status-never-color-alone, an Enterprise Table Standard, and a Core Components catalog (Button/Table/KPI Card/Status Badge/Tabs/Modal/Drawer/Timeline/Workflow Stepper/Entity Card/Empty-Loading-Error states). **This document is the design authority already in force** — this audit's recommendations (§P) are calibrated to implement pieces of it that remain unbuilt, not to compete with it.

## K. Existing Localization

- Cookie-based (`CookieRequestCultureProvider`), `en`/`fa` only, with a deliberate, correct decision to force the Gregorian calendar even under the `fa` culture (protects audit-log timestamps and export filenames from silently corrupting into Shamsi dates). **EXISTING — REUSE**, this is sound and shouldn't be touched.
- RTL applied via the `dir` HTML attribute (auto-flips flexbox layout) plus targeted RTL-only stylesheets for genuinely-directional exceptions (`bootstrap.rtl.min.css`, `admin.rtl.css`) and some (inconsistent) logical-CSS-property usage elsewhere. The orphaned `cmms-rtl.css` (see §J) is the one loose end here.

## L. Existing Authorization Integration

- `RequirePermissionAttribute` (controller-level filter) + `IPermissionService.HasPermissionAsync(roles, module, action)` (fine-grained module+action matrix) + `MenuAccess` (coarse, static role-list-per-module class used for sidebar gating) + `ICurrentUserService` + `SystemRoles` (9 confirmed roles: SystemAdmin, EngineeringManager, EngineeringSupervisor, DisciplineEngineer, DocumentController, MaintenanceEngineer, ContractManager, QAManager, Viewer). **EXISTING — REUSE**, this is the authoritative system and nothing here should be replaced.
- **MISSING:** no reusable Razor TagHelper/ViewComponent exists for permission-gated UI — every view does it manually with `@if (MenuAccess.CanSee(...))` or a direct `HasPermissionAsync` call. Building several new portal-style components (Favorites star, Quick Create menu, task widgets) without first adding one small, reusable permission-check helper would multiply this manual-`@if` pattern further.
- **Design-vs-implementation gap worth flagging, not fixing here:** the Enterprise UI/UX Baseline's persona model (~19 personas: Executive, Discipline Manager, Procurement Manager, Vendor Coordinator, Maintenance Planner, etc.) is materially more granular than the 9 actual `SystemRoles`. Any new role-aware workspace UI (§13 of the directive) must be built against the **real 9 roles**, not the baseline doc's aspirational persona list, to avoid inventing UI states the backend can't actually authorize differently.

## M. Reusable Components (confirmed, ready to reuse as-is)

`_KpiCard.cshtml`, `_Pagination.cshtml`, `_Breadcrumb.cshtml`, `_WorkflowStagesDiagram.cshtml` (static today), `_RichTextEditorScripts.cshtml` (Quill-based), `DocumentStatusBadge`/`LetterStatusBadge` (`PEMS.Web/Helpers/`), `pems-data-grid.css` (opt-in grid standard), `MenuAccess` (role-gate helper), `PemsTooltips.init()`/`PemsUI.init()` (`site.js`, generic Bootstrap tooltip wiring already used across EDMS views).

## N. Duplicate/Conflicting Implementations (consolidated list)

1. Theme: server cookie (`ThemeController`, 2 options) vs. client `localStorage` (`admin.js`, 5 options) — never synchronized.
2. Sidebar toggle: 3 separate JS handlers, 3 different class-name conventions, none persisted.
3. CSS design tokens: 3 independent `:root` definitions with overlapping names, different values.
4. Admin CSS scoping: `.admin-area` vs `.admin-page`/`.admin-page-shell`.
5. `cmms-rtl.css`: shipped but never loaded (dead code).
6. Global search: `GlobalSearchController` (topbar page) vs. `SearchApiController`+providers (form pickers) — not integrated.
7. Breadcrumbs: shared `_Breadcrumb.cshtml` bypassed by hand-rolled markup in 8 views.
8. "Inventory & Materials" sidebar section reuses the "CMMS Integration" role gate verbatim (likely unintentional).

## O. Gaps (things that are simply missing, not duplicated or conflicting)

- In-app contextual Help/Documentation system (entirely).
- A generic Favorites mechanism (`LetterStar` is the only instance, hard-FK'd to `Letter`, not reusable for other entities without a schema change).
- Recent Items (absent everywhere — zero matches for any "recently viewed" concept).
- A `UserPreference` entity (theme/density/page-size/sidebar-state/default-filters as one coherent per-user record).
- A unified "My Tasks" read model spanning Engineering Documents, VDR, TechnicalQuery, MOC (Letters/Permit/WorkOrder already have the pattern; it's just not generalized or extended to the other four modules).
- Permission/project scoping on 18 of 19 global-search providers.
- A reusable permission-gating Razor construct (TagHelper/ViewComponent).
- A true unified, cross-entity global search aggregator with per-result type badges (Azure Portal-style "DOC-INS-00123 · Engineering Document · Rev 04").
- A "Quick Create" topbar entry point (mandated by the existing baseline doc, never built).

## P. Recommended Architecture

The guiding principle throughout: **extend, aggregate, and consolidate — do not parallel-build.** Concretely:

- **Shell consistency (no schema change):** pick the server-cookie theme mechanism as the single source of truth (or the client one — either is fine, but not both), delete the redundant sidebar-toggle handlers down to one, consolidate the 3 CSS token sets into `site.css`'s existing tokens (having `administration-design-system.css`/`auth.css` reference the same custom properties instead of redefining them), and either wire in or delete `cmms-rtl.css`. Pure refactor, zero business-logic risk.
- **EDMS contextual navigation (no schema change):** the Engineering Documents Details page already has the command-bar/tabs pattern the directive wants generalized — the actual work here is (a) adding a Transmittal cross-link tab/section next to the existing Correspondence tab, using the already-existing `TransmittalController`/`Transmittal` entity, and (b) extracting the page's command-bar/tab-strip markup into a reusable partial so other modules (VDR, WorkOrder, etc.) can adopt the same pattern later without copy-pasting 1300 lines.
- **My Tasks (no schema change):** add the same "live query against existing status/discipline fields" pattern already used for Letters/Permit/WorkOrder to Engineering Documents (documents in `Submitted`/`Checked`/`IDC Review` scoped to the user's discipline and role), and equivalently for VDR/TQ/MOC if time permits, surfaced through `HomeController`'s existing "My Work" section — extending, not replacing, what's there.
- **Global search (no schema change, but a real fix, not cosmetic):** add permission/project scoping to the 18 unscoped `ISearchableEntityProvider` implementations first (this is a security fix independent of any UX work and should probably not wait for the "search improvements" phase), then build a thin aggregator on top of the *existing* provider framework that fans out, merges, and tags each result with its entity type — retiring the separate, cruder `GlobalSearchController` page rather than maintaining two systems.
- **Permission-gated UI helper (no schema change):** one small `PermissionTagHelper` or `ViewComponent` wrapping the existing `IPermissionService`/`MenuAccess`, used by every new portal component from here on so the manual-`@if` pattern doesn't keep multiplying.
- **Contextual Help — requires a new entity and controller.** Minimal shape per the directive's own §8 spec: `HelpTopic(TopicId, Module, RouteKey, TitleEn, TitleFa, SummaryEn, SummaryFa, ContentEn, ContentFa, RelatedTopicIds, Keywords)`. This is genuinely greenfield (§H) — **flagged as a STOP-and-confirm item, not built in this pass**, per the directive's own instruction to stop before an unclear-impact schema change, even though this one's impact is actually well-understood and low-risk (a small, standalone, purely-additive table with no foreign keys into any workflow-critical entity).
- **User Preferences — requires a new entity.** Minimal shape: `UserPreference(ApplicationUserId PK/FK, Theme, SidebarCollapsed, TableDensity, DefaultPageSize, ...)`. **Flagged as a STOP-and-confirm item.**
- **Favorites + Recent Items — requires new/generalized entities.** Recommend generalizing `LetterStar` into a polymorphic `UserFavorite(ApplicationUserId, EntityType, EntityId, CreatedDate)` (migrating its handful of existing rows) rather than leaving Letters with its own favorite system while every other module gets a second, parallel one — and a capped `RecentItem(ApplicationUserId, EntityType, EntityId, ViewedDate)` table with retention trimming. **Flagged as a STOP-and-confirm item**, both because it touches an existing table (`LetterStar`) and because "how many items to retain, and does viewing something you can no longer access get scrubbed retroactively" (§26 of the directive) are product decisions, not something to guess.

## Q. Implementation Priority

| Phase (per directive §29) | Schema change? | Risk | Recommendation this pass |
|---|---|---|---|
| 1. Unified shell consistency | No | Low | **Implement now** (see below) |
| 2. Engineering Documents contextual navigation | No | Low-Medium | **Implement now**, scoped to the Transmittal cross-link + extracting the existing command-bar/tabs into a reusable partial |
| 3. Contextual Help + Learn More | **Yes** (new entity) | Low blast-radius, but a genuine new system | **STOP — present spec, wait for confirmation** |
| 4. Home / My Workspace / My Tasks | No | Low-Medium | **Implement now**, EDMS "pending my action" query at minimum |
| 5. User preferences | **Yes** (new entity) | Low blast-radius | **STOP — present spec, wait for confirmation** |
| 6. Favorites + Recent | **Yes** (new/generalized entities, touches `LetterStar`) | Medium (data migration) | **STOP — present spec, wait for confirmation** |
| 7. Global search improvements | No (but fixes a security gap) | The security fix itself is urgent; the aggregator UX is not | **Implement the permission-scoping fix now**; defer the unified-aggregator UI |
| 8. Cross-module consistency | No | Ongoing | Naturally falls out of Phases 1-2 done well |

---

*This document is followed by `docs/design/PEMS_ENGINEERING_DOCUMENTS_PORTAL_UX_SPEC.md` (the required Engineering Documents-specific UX specification) and, after that, an implementation pass covering Phases 1, 2, 4 (partial), and the 7 security fix — the schema-touching phases (3, 5, 6) are documented but not built pending confirmation, per the directive's own stop conditions.*
