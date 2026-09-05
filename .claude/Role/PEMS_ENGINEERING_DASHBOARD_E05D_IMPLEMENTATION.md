# E-05D — Engineering Dashboard UI/UX & Operational Drilldown Implementation

## 1. Executive Summary

E-05D built and **live-verified in a real browser session against the live dev database** a
production Engineering Dashboard at `/EngineeringDashboard`, consuming exactly the two real query
methods E-05C validated (`GetSummaryCountsAsync`, `GetRevisionQualityAsync`) with zero changes to
the query layer, zero new business logic, and zero changes to the Engineering Document lifecycle.

Per the directive's own KPI Contract Rule (§6: "if a requested visual cannot be supported by an
existing validated query, DO NOT fabricate the value... stop that specific KPI implementation,
continue with the rest if independent"), three sections were built with real data (Executive
Summary, Quality & Review, Revision & Resubmission Pressure); Discipline Performance, Workflow
Bottleneck, and Vendor Performance were **not** built, because their backing queries
(`GetDisciplinePerformanceAsync`/`GetWorkflowMetricsAsync`/`GetVendorPerformanceAsync`) were
designed in E-05B but never implemented — this is documented as `Required` follow-up work, not
silently skipped.

**A significant, unplanned finding emerged from live verification, not from code review**: the
SQL-Server-backed tests built across E-01–E-05C (`FirstPassApprovalPrototypeTests`,
`EngineeringRevisionQualityTests`, `DocumentNumberUniquenessTests`, `NumberingServiceTests`) had
been leaving unclean data in the shared dev database — 71 test documents, 40 test projects, 32
test disciplines, 21 numbering-sequence rows — and this data had become visible in the real
dashboard's own filter dropdown and rejection/revision tables. This was found, scoped precisely by
distinctive test-only naming patterns, and cleaned up in a transaction (with the transaction
mechanism itself catching and safely rolling back a mid-script error on the first attempt) — full
detail in §14.

**Verdict: PASS.**

---

## 2. Pre-Implementation Audit

- **Existing dashboard code**: none. `find . -iname "*EngineeringDashboard*"` returned nothing
  before this phase — confirmed the entire controller/view/viewmodel layer was built from scratch.
- **E-05A/B/C reports**: re-confirmed current (git status showed the repository unchanged since
  E-05C's commit, aside from external `.claude/Role/` documentation reorganization unrelated to
  code — see §17).
- **Existing Engineering Document list/detail pages**: `EngineeringDocumentsController.Index`
  (E-03's paginated, filtered list) and `Details` (pre-existing) — both reused directly as
  drilldown targets, no duplicate detail page created.
- **Navigation/layout**: `_SidebarMenu.cshtml`'s `engineeringControllers` array and
  `MenuAccess.CanSee(User, MenuAccess.EngineeringManagement)` gate — reused, one new nav entry
  added, no new permission created.
- **Authorization**: confirmed `EngineeringDocumentsController` has no `[Authorize]` attribute and
  no per-action permission check on its read-only `Index` — relies entirely on the global
  `RequireAuthenticatedUser()` fallback policy (`Program.cs:75`). The new
  `EngineeringDashboardController` mirrors this exactly, plus reuses the same two-signal
  discipline-restriction resolution E-03 established (verified authorization behavior in §9).
- **Localization/RTL infrastructure**: `_Layout.cshtml` sets `dir="rtl"/"ltr"` from
  `CultureInfo.CurrentUICulture` globally — no page-specific work required or done.
- **Responsive/mobile**: no dashboard-specific breakpoints exist anywhere in the app; the
  `.content-card`/Bootstrap grid (`.row.g-3`, `.col-6.col-md-2`) pattern already used by
  `Home/Index.cshtml` and `EngineeringDocuments/Index.cshtml` was reused as-is.
- **Design system/components**: `_KpiCard.cshtml` shared partial (existing, `KpiCardViewModel`)
  reused directly for all six Executive Summary cards — not reinvented. `table-enterprise`,
  `content-card`, `bg-icon-*` (8 defined variants) — all existing classes, no new CSS written.
- **API/query endpoints exposed by E-05C**: **none** — E-05C delivered repository/service methods
  only, no HTTP surface. This phase is the first to expose them via a controller.

---

## 3. Existing Dashboard Assessment

`HomeController`/`Home/Index.cshtml` (the global dashboard) uses an inconsistent pattern —
inline, per-KPI `SafeCountAsync` calls directly in the controller against `PEMSDbContext`, no
dedicated service. This was **not** the pattern followed here (E-05B's own
recommendation, reaffirmed in E-05C's performance findings: prefer one materialized query per
section over many individual counts). `ReportController` was the closer, better structural
precedent — multiple section-specific services, a shared filter-loading helper, `ViewData["Title"]
/["Section"]` — and was the template actually followed for `EngineeringDashboardController`.

---

## 4. UI/UX Architecture

```
Engineering Domain (unchanged)
  → Validated Query Layer (E-03 GetSummaryCountsAsync, E-05C GetRevisionQualityAsync — unchanged)
  → EngineeringDashboardController.Index (new, thin — resolves auth/discipline-restriction,
    calls the two service methods, computes FirstPassApprovalRate as a pure in-memory aggregation
    of already-fetched data, no new query)
  → EngineeringDashboardViewModel (new, composition model — carries the existing DTOs directly)
  → Views/EngineeringDashboard/Index.cshtml (new — 3 sections + filter form + deferred-sections note)
  → KPI Drilldown (Url values on KpiCardViewModel / table row links — plain <a href>, no JS)
  → EngineeringDocumentsController.Index (existing, E-03) / .Details (existing) — reused unchanged
```

---

## 5. KPI Mapping / Traceability Table

Per §28's requirement — only mappings actually verified in the repository, none hypothetical:

| KPI | Backend Query | Filter | Drilldown | Status |
|---|---|---|---|---|
| Total Documents | `GetSummaryCountsAsync` (E-03) | discipline-restriction only | `EngineeringDocuments/Index` (unfiltered) | **PASS** — verified live, rendered `9,190` post-cleanup |
| Approved | `GetSummaryCountsAsync` | discipline-restriction only | `EngineeringDocuments/Index?Status=Approved` | **PASS** — verified live, link present in rendered HTML |
| Pending Review | `GetSummaryCountsAsync` | discipline-restriction only | **none** — `Status IN ('Submitted','Checked')` cannot be expressed as the single-value `EngineeringDocumentListFilter.Status`; a misleading partial drilldown was deliberately not built | **PASS (non-drillable by design)** |
| Rejected | `GetSummaryCountsAsync` | discipline-restriction only | `EngineeringDocuments/Index?Status=Rejected` | **PASS** — verified live |
| Frozen | `GetSummaryCountsAsync` | discipline-restriction only | **none** — no `IsFrozen` field on `EngineeringDocumentListFilter` | **PASS (non-drillable by design)** |
| Overdue | `GetSummaryCountsAsync` | discipline-restriction only | **none** — no overdue field on the filter | **PASS (non-drillable by design)** |
| First-Pass Approval Rate | `GetRevisionQualityAsync` (E-05C), aggregated in-memory in the controller | discipline-restriction, ProjectId, DisciplineId | none (aggregate stat, not a list) | **PASS** — verified live, rendered `0%` post-cleanup (previously `1%` with polluted data, `N/A` was the documented fallback for the zero-denominator case, exercised and confirmed correct during development before real data made it moot) |
| Most Rejected Documents (table) | `GetRevisionQualityAsync`, sorted by `RejectionCount desc`, top 10 | same + `RejectionCount > 0` | each row → `EngineeringDocuments/Details/{id}` | **PASS** — verified live, real document rows rendered with working Details links |
| Revision & Resubmission Pressure (table) | `GetRevisionQualityAsync`, sorted by `RevisionCount desc`, top 10 | same + `RevisionCount > 0`, `MinRevisionCount` | each row → Details; "View all" → `EngineeringDocuments/Index?MinRevisionCount=N` | **PASS** — verified live, real rows + working "View all in MDR" link (E-05C's proven drilldown wiring) |

---

## 6. Dashboard Information Architecture

Three built sections (Executive Summary, Quality & Review, Revision & Resubmission Pressure) plus
one explicit, visible "deferred sections" notice (`.alert.alert-secondary`) naming what's missing
and why — chosen deliberately over silently omitting those sections, so a user or reviewer
doesn't mistake "not built" for "checked and found zero."

---

## 7. Drilldown Architecture / Matrix

Per §29's requirement:

| Dashboard Section | KPI | Applied Filter | Destination | Verified |
|---|---|---|---|---|
| Executive Summary | Total | (none) | `EngineeringDocuments/Index` | **YES** |
| Executive Summary | Approved | `Status=Approved` | `EngineeringDocuments/Index?Status=Approved` | **YES** |
| Executive Summary | Rejected | `Status=Rejected` | `EngineeringDocuments/Index?Status=Rejected` | **YES** |
| Quality & Review | Most Rejected Documents (row) | `id` | `EngineeringDocuments/Details/{id}` | **YES** |
| Revision Pressure | Revision table (row) | `id` | `EngineeringDocuments/Details/{id}` | **YES** |
| Revision Pressure | "View all in MDR" | `MinRevisionCount` | `EngineeringDocuments/Index?MinRevisionCount=N` | **YES** |

All drilldown targets use the **existing** `EngineeringDocumentListFilter` (E-03) — no parallel
filter mechanism was created, per ADR-5.

---

## 8. Filter Integration

Dashboard's own filter form: `projectId`, `disciplineId`, `minRevisionCount` — exactly the three
parameters the two real backend queries actually accept today. No filter dimension was added that
the backend can't honor (e.g. no Vendor/DocumentType/WorkflowStage/DateFrom-To filter UI, since
none of the two real queries support them — adding the UI without backend support would violate
the KPI Contract Rule). `OverdueOnly` was **not** added — re-confirmed in this phase (as E-05C
already determined) that nothing in this phase's real, backend-supported scope needs it.

---

## 9. Authorization Review

`EngineeringDashboardController.Index` replicates `EngineeringDocumentsController.Index`'s exact
discipline-restriction resolution (`isRestricted: bool`, `myDisciplineId: int?`, including the
"employee with no discipline stays restricted to null-discipline documents, not everything" nuance
E-03 specifically regression-tested). Both `GetSummaryCountsAsync` and `GetRevisionQualityAsync`
calls pass this through — **confirmed by code inspection**, not just assumed: no dashboard number
or table row can include another discipline's data for a restricted user, since the restriction is
applied inside the same repository queries E-03/E-05C already proved apply it correctly (their
existing discipline-restriction-parity tests cover this exact logic; this phase adds no new
authorization code, only reuses it). No parallel authorization framework was created.

---

## 10. Responsive / RTL Review

No dashboard-specific CSS or breakpoints were written — the page uses only pre-existing,
already-responsive classes (`.content-card`, `.row.g-3`, `.col-6.col-md-2` for the KPI grid,
`.table-scroll-area` for horizontally-scrollable tables on narrow screens). RTL requires zero
page-specific code, confirmed by inspecting `_Layout.cshtml` (culture-driven `dir` attribute,
already applies to every page including this new one). **Not independently re-verified with a
real Persian-culture browser session in this phase** — the live verification in §14 was done in
the default (English) culture; flagged as a residual manual-QA item in §18, not claimed as fully
verified.

---

## 11. Performance Results

Baseline from E-05C (query layer, unchanged): Quality query ~375-491ms warm on ~9,226 documents;
paginated list drilldown ~95ms.

**This phase's dashboard page load**: two sequential calls
(`GetSummaryCountsAsync` + `GetRevisionQualityAsync`) plus two small reference-data queries
(`Projects`, `Disciplines` dropdowns) — 4 total DB round trips for the whole page, no N+1 (the
`Quality` list is fetched once and both the "Most Rejected" and "Revision Pressure" tables are
derived from the same in-memory list via LINQ `OrderBy`/`Where`/`Take`, not two separate queries).
No client-side polling, no auto-refresh, no repeated identical requests — confirmed by inspecting
the controller (single `Index` action, no AJAX endpoints added). Full authenticated page
request-to-response observed at **200 OK** with no server-side errors logged, during live
verification (exact client-side render timing not separately profiled — not required by this
phase's regression baseline).

---

## 12. Accessibility Review

Icon-only interactive elements (Reset filter button) carry both `title=` (matching the confirmed
local convention in `EngineeringDocuments`/`Permit` views — `aria-label` is not the established
pattern there) and `aria-label` (an additive improvement, not a convention break — HTML permits
both). Status is never communicated by color alone: every badge/icon pairs with visible text
(`badge-status-@q.Status` shows the literal status word; first-pass check/x icons carry `title`/
`aria-label` text). Tables use semantic `<th>` headers. **Not independently tested with a screen
reader or keyboard-only navigation walkthrough** in this phase — flagged in §18 as a residual
manual-QA item, consistent with this being the first UI phase in the E-05 series and no existing
accessibility test tooling being present in the project to automate this check.

---

## 13. Test Results

`dotnet test` — **58/58 passing**, unchanged from E-05C's count (this phase added no new test
files; §16 explains why controller-level tests were not added, and what coverage substitutes for
them). Re-ran the full suite twice in this phase: once before live verification, once after the
database cleanup in §14 — both green, confirming the cleanup didn't disturb anything the tests
depend on (each test creates its own fresh data per run, none depend on database state persisting
between runs).

---

## 14. Regression Results — and the Test-Data-Pollution Finding

- E-01 (numbering/freeze), E-02 (workflow/IDC/client-review/comments), E-03 (pagination), E-04
  (traceability/lifecycle), E-05C (Quality query) — all covered by the same 58 tests, all green,
  both before and after this phase's changes and the cleanup.
- `Document Revision`, `Revise & Resubmit`, `IssuePurpose`, reverse traceability, workflow
  lifecycle, Engineering Document filters — all exercised by name-matched tests in the existing
  suite (`EngineeringWorkflowTests`, `ReverseTraceabilityTests`, `EngineeringDocumentPagedListTests`)
  — no changes made to any file those tests cover.

**Unplanned finding, corrected during this phase**: live browser verification (§2's audit didn't
catch this — it only surfaced once real HTTP requests hit the real database) revealed that
SQL-Server-backed tests since E-01 had accumulated **71 test `EngineeringDocument` rows, 40 test
`Project` rows, 32 test `Discipline` rows, and 21 test `NumberingSequence` rows** in the shared
dev database — visible in the live dashboard's own Project filter dropdown and rejection/revision
tables. Root cause: those test files (`FirstPassApprovalPrototypeTests`,
`EngineeringRevisionQualityTests`, `DocumentNumberUniquenessTests`, `NumberingServiceTests`) use
`PemsDbContextFactory.CreateSqlServerOrThrow()` against the real dev database (required, since the
atomic numbering and first-pass-approval queries use T-SQL that InMemory/SQLite can't execute —
documented since E-01) but never clean up what they create.

**Remediation performed**: identified all polluted rows by exact, distinctive naming patterns
unique to this session's test code (`DocumentNo LIKE 'FPA-%'/'RQ-%'/'TEST-DUP-%'`, `ProjectCode
LIKE 'FPA%'/'RQ%'`, `Discipline.Code LIKE 'RQD%'/'OTH%'` — deliberately matched on GUID-suffixed
codes, not human-readable names, to eliminate any risk of matching real data), deleted in correct
FK-dependency order inside a single transaction
(`WorkflowHistories → WorkflowInstanceStages → WorkflowInstances → DocumentRevisions →
DocumentIdcReferrals/DocumentWorkflows → EngineeringDocuments → Documents → NumberingSequences →
Projects → Disciplines`). First attempt failed cleanly on a `QUOTED_IDENTIFIER` setting error
mid-script — **the wrapping transaction rolled back automatically, verified by re-querying row
counts before retrying** (nothing was lost). Second attempt, with `SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;` added, committed successfully. Post-cleanup verification: 0 remaining
test-pattern rows, real document count restored to exactly **11,945** (the same total-`Documents`
figure recorded all the way back at the start of E-01, before any test ever ran — confirming no
real data was lost, only what this session's tests had added was removed), 1 real project remains.

**This is flagged as a real gap in this session's own test-hygiene discipline, not swept under the
rug** — see §18 for the concrete recommendation.

---

## 15. Graphify Baseline

Captured before implementation began (per §3 of the directive):

- Nodes: **6930**
- Edges: **13784**
- Communities: **601**
- Repository clean at baseline (confirmed via `git log` showing the user's own squashed commits
  through E-05C, `git status` clean before this phase's edits began).
- Dashboard-related dependency paths at baseline: **none** — zero nodes matched "EngineeringDashboard"
  anywhere in the graph, confirming the feature genuinely did not exist before this phase.

---

## 16. Graphify Final State

`graphify update .` was run again after all code changes (per §25's requirement).

- **Topology changed**: yes (18/18 uncached files extracted — the 3 new files
  (`EngineeringDashboardController.cs`, `EngineeringDashboardViewModel.cs`, `Index.cshtml`) plus
  the resx/sidebar edits).
- **Nodes**: 6946 (baseline 6930 → **+16**).
- **Edges**: 13815 (baseline 13784 → **+31**).
- **Communities**: 602 (baseline 601 → +1; graphify logged "renamed 87 community(ies) by their
  hub" — re-clustering, not loss).
- **New dashboard dependency paths**: `EngineeringDashboardController` → `IEngineeringDocumentService`
  (existing interface, already depended on by `EngineeringDocumentsController` — no new
  cross-module coupling), `EngineeringDashboardController` → `PEMSDbContext` (same dependency
  every other controller in this area already has, for the Projects/Disciplines dropdown queries),
  `Views/EngineeringDashboard/Index.cshtml` → `Views/Shared/_KpiCard.cshtml` (reuse of an existing
  shared partial, not a new one).
- **Unexpected coupling**: none found — the new controller depends only on interfaces/types
  already depended on elsewhere in `PEMS.Web`; no new dependency on `PEMS.Domain`/`PEMS.Persistence`
  beyond what `EngineeringDocumentsController` already has.
- No manual edits were made to any graphify output file.

---

## 17. Git Diff Summary

**Intended E-05D changes** (confirmed via `git status`/`git diff --stat`):
- `PEMS.Web/Controllers/EngineeringDashboardController.cs` (new)
- `PEMS.Web/Models/EngineeringDashboardViewModel.cs` (new)
- `PEMS.Web/Views/EngineeringDashboard/Index.cshtml` (new)
- `PEMS.Web/Views/Shared/_SidebarMenu.cshtml` (+6 lines: one nav entry + array membership)
- `PEMS.Web/Resources/SharedResource.resx` / `.fa.resx` (+2 lines net — one large appended
  `<data>` block each, English + Persian)

**Zero changes** to `PEMS.Application`/`PEMS.Persistence`/`PEMS.Domain` — confirmed, matches the
directive's "do not redesign the query architecture" instruction exactly.

**Pre-existing/unrelated changes observed** (not part of this phase, not reverted or touched):
`PEMS_PROJECT_INTELLIGENCE_REPORT.md` (an auto-regenerated report file, changed outside this
session's edits — same external-housekeeping pattern already noted in the E-05C report for the
`.claude/Role/` folder reorganization). `graphify-out/*` changes are this phase's own baseline/
final graphify runs, expected and intended.

**Database changes** (not a migration — a data cleanup, §14): 71+40+32+21 = 164 test-generated
rows removed from the shared dev database across 10 tables, executed in one transaction, verified
before and after. No schema change. No real/production-representative data was touched — verified
by exact row-count reconciliation against the earliest recorded baseline (11,945 total `Documents`,
matching the count from the start of E-01).

---

## 18. Known Limitations

- **Discipline Performance, Workflow Bottleneck, and Vendor Performance sections are not
  implemented** — classified `Required` (not `Recommended`/`Deferred`) for whichever phase
  continues the E-05 series, since they were explicitly designed in E-05B and are the natural next
  layer of this dashboard; blocked purely on the missing query methods, not on anything UI-related.
- **RTL layout was not independently re-verified with a live Persian-culture browser session** in
  this phase (relies on the pre-existing, already-proven `_Layout.cshtml` mechanism, but this
  specific new page wasn't screenshotted in `fa` culture).
- **Accessibility was not tested with a screen reader or keyboard-only walkthrough** — the markup
  follows established local conventions (`title`/`aria-label`, semantic `<th>`, no color-only
  status) but wasn't independently audited with assistive-technology tooling, which doesn't exist
  in this project's toolchain today.
- **Controller-level automated tests were not added** — `PEMS.Tests` has zero `PEMS.Web`
  reference and zero controller tests exist anywhere in this codebase (not an Engineering-specific
  gap). Coverage instead comes from the already-passing E-03/E-05C repository-level tests for the
  exact two queries this dashboard calls, plus this phase's live browser verification.
- **Test-data hygiene gap in the SQL-Server-backed test suite** (§14) — the four test files that
  target the real dev database create persistent rows with no teardown. This session's occurrence
  was cleaned up, but the root cause remains: any future run of those tests will recreate the same
  problem. **Concrete recommendation for a future pass**: wrap each SQL-Server-backed test's body
  in a `DbTransaction` that is never committed (rolled back in `Dispose`), or add an explicit
  cleanup step keyed to the same distinctive naming patterns used in this phase's cleanup script —
  not fixed in this phase, since it touches E-01–E-05C test files under an E-05D directive that
  explicitly forbids unrelated changes (§30).

---

## 19. Deferred Items

Classified per the directive's own `Required`/`Recommended`/`Deferred` taxonomy (§30):

- **Required**: `GetDisciplinePerformanceAsync`, `GetWorkflowMetricsAsync`,
  `GetVendorPerformanceAsync` implementations (blocks the three deferred dashboard sections).
- **Required**: fixing the SQL-Server test suite's data-hygiene gap (§18) before it recurs.
- **Recommended**: a live Persian-culture (`fa`) browser screenshot pass and a screen-reader
  walkthrough, once the dashboard has more sections worth reviewing together.
- **Deferred**: Vendor/DocumentType/WorkflowStage/DateFrom-To filter dimensions on the dashboard —
  no backend support exists yet; adding the UI first would violate the KPI Contract Rule.
- **Deferred**: `OverdueOnly` filter field — re-confirmed not needed by anything built in this
  phase.

---

## 20. E-05E Readiness Assessment

**What E-05D completed**: a real, browser-verified Engineering Dashboard consuming the validated
E-05C query contract with zero query-layer changes, full drilldown wiring into the existing MDR
list and document detail pages, and a live-verification pass that caught and fixed a genuine
data-hygiene defect in the test suite that pure code review would not have surfaced.

**What remains**: the three `Required` backend query methods (§19) are the direct blocker for the
next layer of dashboard sections (Discipline/Workflow/Vendor). Until those exist, expanding the
dashboard further would mean either fabricating data (forbidden) or building UI for sections that
stay permanently empty (equally unhelpful).

**Recommendation for E-05E**: implement `GetDisciplinePerformanceAsync` first among the three
(highest management value per the original remediation directive's own priority framing — "which
discipline is behind" was one of the original audit's headline unanswerable questions), following
the same validated pattern established across E-05C/E-05D: prototype the query, verify its
generated SQL and edge-case correctness against real data *before* wiring it into any UI, exactly
as the First-Pass Approval gate modeled. `GetWorkflowMetricsAsync` and `GetVendorPerformanceAsync`
can follow in either order once the discipline layer's pattern is proven.

**Is the Engineering Dashboard ready for production hardening?** Not yet, and not because of what
was built this phase — because it currently covers only 3 of the 6 information layers the original
remediation directive scoped, and shipping it as "the Engineering Dashboard" without the
Discipline/Workflow/Vendor layers would undersell what management actually needs to answer "where
is Engineering behind." What **was** built this phase is solid and ready as-is: real data, real
drilldowns, real authorization, zero fabricated metrics.

## Verdict: **PASS**

Three dashboard sections, all backed by validated queries, all drilldowns verified working in a
real authenticated browser session against the live database — not just compiled. Zero fabricated
KPIs (confirmed against the KPI Contract Rule for every number on the page). Zero lifecycle,
schema, or query-architecture changes. Full regression suite green, twice. One real defect
(test-data pollution) was found through live verification — the exact scenario this phase's
"verify in a browser, not just compile" requirement exists to catch — and was fixed transparently,
not hidden.
