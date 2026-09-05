# E-05B — Engineering Management Dashboard: Architecture & Query Design

**Status:** Architecture/design only. No UI, controllers, DTOs, queries, indexes, migrations,
workflow, or permission changes were made in this phase.

**Re-verification:** `git status` confirms the repository is unchanged since E-05A (the only
diffs present are the E-01–E-04 implementation work and the E-05A report itself — nothing drifted
between phases). `graphify update .` was re-run against the live tree and reported **"No
code-graph topology changes detected; outputs left untouched"** — 6842 nodes · 13583 edges · 586
communities, identical to the count recorded at the end of E-05A. E-05A's findings are therefore
verified current, not stale, and this document builds directly on them rather than re-deriving
them from scratch.

---

## 1. Architecture Overview

The dashboard is a **read-only aggregation surface** over data the Engineering module already
owns — it introduces no new write paths, no new business rules, and no new entities. Its job is
to answer six management questions (what/where/why/which discipline/which record/what action) by
composing several independently-queryable, independently-failable sections, each backed by one
SQL-first aggregate query, all sharing one security boundary and one filter contract.

Three architectural commitments carry through every section below:

1. **Hybrid query architecture** (§2) — several focused query services, each one materialized
   query, not one giant composite query and not a pile of individual `CountAsync` round trips.
2. **Security is a query parameter, not a UI concern** (§3) — every query service takes
   `(isRestricted: bool, disciplineId: int?)`, the exact two-signal shape proven in E-03, and
   applies it before aggregation, in SQL.
3. **No fabricated metrics** (§20) — every KPI in this document traces to a concrete field
   verified live in E-05A. Anything that doesn't is explicitly deferred, not approximated.

---

## 2. Query Architecture

**Hybrid, per E-05A §17's recommendation — rejected alternatives documented as ADR-1 (§21).**

Nine query responsibilities, matching the directive's naming with light adjustment to match this
codebase's existing `I<Entity>Service` convention (see `IEquipmentService.GetDashboardAsync`,
`ILetterService.GetDashboardAsync`):

| Query Service Method | Responsibility | Query Shape |
|---|---|---|
| `GetSummaryAsync` | Layer 1 Executive KPI row | One query, `GroupBy`-free conditional-`Count` aggregation (see §7) |
| `GetDisciplinePerformanceAsync` | Layer 2 discipline matrix | One query, `GroupBy DisciplineId` |
| `GetWorkflowMetricsAsync` | Layer 3 workflow bottleneck | One query per stage over `WorkflowInstanceStage`, `GroupBy WorkflowStageId` |
| `GetRevisionQualityAsync` | Layer 4 document quality | One query, `GroupBy DocumentId` over `DocumentRevisions` |
| `GetVendorPerformanceAsync` | Layer 5 vendor performance | One query, `GroupBy VendorId`, joins `WorkflowHistory` only for the timing columns |
| `GetExceptionsAsync` | Layer 6 management attention | One query per exception type (§16), unioned in the service layer, not SQL `UNION` (different source tables per type) |
| `GetAgingAsync` | aging buckets (supports Layer 1/6) | One query, `CASE WHEN` bucketing (§14) |
| `GetTrendAsync` | Layer... trend chart | One query, `GroupBy` on a truncated date column, grain-parameterized (§15) |
| `GetDrilldownFilterAsync` | not a query — a **pure mapping function** (§10) | No DB access; maps a dashboard selection to an `EngineeringDocumentListFilter` (E-03's existing filter DTO) |

Each method is independently callable and independently failable — a slow vendor-performance join
must not block the Executive KPI row from rendering (directive §37's per-widget error isolation
requires this at the *query* layer, not just the UI layer, since a shared composite query would
make partial failure impossible).

---

## 3. Security Architecture

Every query method in §2 (except `GetDrilldownFilterAsync`, which touches no data) takes the same
two parameters `EngineeringDocumentRepository.GetPagedAsync`/`GetSummaryCountsAsync` already do:

```
isRestricted: bool     // false for SystemAdmin/EngineeringManager/EngineeringSupervisor/DocumentController,
                        // or when the user/employee record can't be resolved (fails open — existing behavior,
                        // not something this phase changes)
disciplineId: int?     // the restricted user's own discipline; MAY be null even when isRestricted is true
                        // (an employee with no discipline assigned sees only null-discipline documents,
                        // not everything — this exact nuance has a regression test from E-03)
```

Applied identically in every query's `WHERE`/filter step, **before** any `GroupBy`/aggregation:

```
if (isRestricted) query = query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);
```

This is not a per-section decision — it is mandatory on **every** aggregate, including ones that
don't obviously look like they leak discipline data. A global "Total Vendor Submissions" number,
for instance, must still be discipline-scoped for a restricted user, or it becomes a side channel
for inferring another discipline's volume. **Controller/API layer**: resolves `(isRestricted,
disciplineId)` once per request exactly as `EngineeringDocumentsController.Index` does today, and
passes it to whichever query methods the requested dashboard section needs — the controller does
not itself decide what to hide, it supplies the restriction and the query enforces it.
**Forbidden**: loading unrestricted data and hiding cards/rows in JavaScript. No dashboard
endpoint may return data the caller isn't authorized to see, full stop — the client never receives
it in the first place.

---

## 4. KPI Contract

Directly inherited from E-05A §3/§4, organized by dashboard layer. **GREEN** = direct SQL
aggregation, no caveats. **YELLOW** = SQL aggregation with joins/subqueries, still reliable.
**Excluded** = deferred per §20, not built.

| Layer | KPI | Status |
|---|---|---|
| 1 Executive | Active, Pending Workflow, Pending IDC, Pending Approval, Issued, Rejected, Revise & Resubmit, Overdue, Frozen | GREEN (Pending IDC is YELLOW — requires the `WorkflowInstanceStage` join) |
| 2 Discipline | Total/Pending/Overdue/Rejected/Revise&Resubmit by discipline | GREEN |
| 2 Discipline | Average Review Time by discipline | YELLOW |
| 3 Workflow | Current stage counts, stage aging, oldest waiting item | GREEN/YELLOW (aging needs a date delta, still direct SQL) |
| 4 Quality | Revision distribution, rejection rate, high-revision documents, repeat rejection | GREEN |
| 4 Quality | First-pass approval rate | YELLOW — cross-`WorkflowInstance` windowed query (E-05A §7), the single most complex query in this contract |
| 5 Vendor | Volume, approved, rejected, revision rate, overdue | GREEN |
| 5 Vendor | Average processing time | YELLOW |
| 6 Exceptions | Overdue, PendingApproval, PendingIDC, StuckWorkflow, HighRevision, RepeatedRejection | GREEN/YELLOW per type (§16) |
| 6 Exceptions | ClientResponseIssue | **Excluded** — see §20 |

---

## 5. Filter Contract

`EngineeringDashboardFilterDto` — deliberately a **superset-compatible sibling** of E-03's
existing `EngineeringDocumentListFilter`, not a competing design, so that drill-down (§10) is a
direct field-for-field mapping rather than a translation layer:

| Field | Type | Notes |
|---|---|---|
| ProjectId | int? | matches `EngineeringDocumentListFilter.ProjectId` |
| DisciplineId | int? | **user-chosen** filter, layered on top of (never replacing) the mandatory security restriction in §3 |
| DocumentType | string? | not in E-03's filter today — new, but same shape (`Contains`/exact match on `Document.DocumentType`) |
| VendorId | int? | new — `Document.VendorId` exists and is indexed-adjacent via the FK, direct filter |
| Status | string? | matches `EngineeringDocumentListFilter.Status` |
| WorkflowStage | string? | maps to `WorkflowInstance.CurrentStageId`'s stage name — requires a join, unlike the other filters |
| Revision | string? | direct `Document.RevisionNo` match — narrow use case (e.g. "show me all Rev 00 documents"), include for completeness only |
| DateFrom / DateTo | DateTime? | bounds `PlannedDate` for aging/trend queries; bounds `ActionDate` for trend queries — **the exact field it bounds depends on the query**, documented per query in §14/§15, not a single universal date column |
| IssuePurpose | string? | `Document.IssuePurpose` (E-04) — free text, `Contains` match |
| ActiveOrArchived | string? (`"Active"`/`"Archived"`/`"All"`) | `Document.IsActive` — dashboard defaults to Active only |

**All filters are server-side, applied in the same `IQueryable` chain as the security restriction,
before aggregation** — no filter in this contract is ever applied only in JavaScript.

---

## 6. Drilldown Contract

Every dashboard number resolves to a call into the **existing** MDR list
(`EngineeringDocumentsController.Index`, E-03) using its **existing**
`EngineeringDocumentListFilter` — no second filtering mechanism is introduced. This is possible
because §5 deliberately kept the dashboard filter contract field-compatible.

| Metric | Drilldown maps to `EngineeringDocumentListFilter` |
|---|---|
| Pending Approval (Executive/Discipline) | `Status = "Checked"` (+ current discipline/project context) |
| Discipline Overdue | `DisciplineId = <selected>` + an overdue flag the list view doesn't currently expose as a filter — **new, minimal addition needed**: E-03's filter/list would need an `OverdueOnly: bool?` field added in E-05C to complete this specific drilldown; documented as a required E-05C addition, not invented here |
| Pending IDC | `Status = "IDC Review"` |
| Vendor Revision Rate | `VendorId = <selected>` + a revision-count-threshold filter — same caveat as above, `MinRevisionCount: int?` would need adding to the list filter in E-05C |
| High Revision Documents | same `MinRevisionCount` addition |
| Repeated Rejection | `Status = "Rejected"` (approximation — true "repeated" requires the revision-count join, same underlying data as Quality model) |

**Security**: drilldown never re-derives permissions — it hands the *same* `(isRestricted,
disciplineId)` the dashboard request already resolved into the list controller's existing,
already-secure query path (E-03). No new authorization logic is introduced by drilldown.

---

## 7. Discipline Model

`EngineeringDisciplinePerformanceDto` — fields exactly as scoped in E-05A §15, SQL strategy added:

| Field | Type | SQL Strategy |
|---|---|---|
| DisciplineId, DisciplineName | int, string | `GroupBy DisciplineId`, joined to `Disciplines` for the name (small reference table, cheap) |
| TotalDocuments | int | `Count()` per group |
| PendingDocuments | int | conditional count, `Status IN ('Submitted','Checked')` |
| PendingIDC | int | requires the `WorkflowInstanceStage` join filtered to the IDC stage name, grouped by the document's discipline — **not** a simple `Document`-only group, since the pending-IDC signal lives on the workflow side |
| PendingApproval | int | conditional count, `Status = "Checked"` |
| IssuedDocuments, RejectedDocuments, ReviseAndResubmit | int | conditional counts on `Status` |
| OverdueDocuments | int | E-05A §5's predicate, grouped by discipline |
| RevisionCount | int | separate `GroupBy DocumentId` on `DocumentRevisions` joined back to discipline via `Document.DisciplineId` |
| AverageReviewTime | double? (nullable) | **only if reliable** — computed from `WorkflowHistory.ActionDate` deltas (E-05A §6); returns `null`, not `0`, when no completed instances exist for that discipline yet — the directive's own "missing means missing, zero means zero" rule applies here directly |

One query for TotalDocuments through OverdueDocuments (single `GroupBy` on `Document`), one
separate query for RevisionCount (different source table), one separate query for
PendingIDC/AverageReviewTime (different source table, `WorkflowInstanceStage`/`WorkflowHistory`) —
joined together in the service layer by `DisciplineId`, not one mega-join in SQL. This keeps each
query simple and independently indexable (§19).

---

## 8. Workflow Model

`EngineeringWorkflowMetricDto` — answers "where is Engineering currently stuck":

| Field | Type | Source |
|---|---|---|
| StageName | string | `WorkflowStage.Name` (Check/IDC/Approval/Issue) |
| PendingCount | int | `Count(*)` where `WorkflowInstanceStage.Status = "Pending"` grouped by stage |
| AverageAgeDays | double | `AVG(DATEDIFF(day, AssignedDate, GETDATE()))` over pending stages only |
| OldestDocumentNo | string | the pending stage row with the oldest `AssignedDate`, joined to its `Document` |
| CompletedCount | int | `Count(*)` where `Status IN ('Approve','Reject','Comment')` grouped by stage, for the trend window |
| AverageStageDuration | double? | **only where reliable** — `AVG(ActionDate - AssignedDate)` for completed stage rows; IDC's is reliable (has both dates), but note IDC still has **no due date**, so this is elapsed time only, never compared against a target (E-05A §6/§11) |

Discipline restriction applies via a join from `WorkflowInstance.BusinessObjectId` → `Document.DisciplineId`
before grouping — the workflow tables themselves carry no discipline column directly.

---

## 9. Quality Model

`EngineeringRevisionQualityDto`:

| Field | Type | Source | Notes |
|---|---|---|---|
| DocumentId, DocumentNo | int, string | Document | identity |
| RevisionCount | int | `COUNT(*) - 1` per `DocumentId` on `DocumentRevisions` (E-05A §3's off-by-one adjustment for the always-created Rev 00), filtered `IsActive = true` | |
| DisciplineName, VendorName | string | joins | context for the "High Revision Risk" list |
| LastStatus | string | `Document.Status` | |
| RejectionCount | int | `COUNT(*)` where `WorkflowHistory.Decision = 'Reject'` for that document's instances | "Repeated Rejection" exception type reuses this |
| FirstPassApproved | bool? | E-05A §7's cross-`WorkflowInstance` windowed query | **nullable** — null when the document hasn't completed a full cycle yet, never coerced to `false` |

**First-pass approval is explicitly NOT derived from `Status` history or revision count alone** —
per E-05A §7 and the directive's own §13 instruction, it requires the windowed
`WorkflowInstance` grouping (earliest `StartDate` per `(Module, BusinessObjectType,
BusinessObjectId)` that reached `Status = "Completed"`). This is the one query in this entire
contract flagged as highest implementation risk (§19 of E-05A) — recommend prototyping it early
in E-05C rather than discovering correctness/performance issues late.

---

## 10. Vendor Model

`EngineeringVendorPerformanceDto` — **Vendor only, no Contractor dimension** (ADR-3, §21):

| Field | Type | SQL Strategy |
|---|---|---|
| VendorId, VendorName | int, string | `GroupBy VendorId` joined to `Vendors` |
| Submitted, Approved, Rejected | int | conditional counts on `Document.Status`, `Document.VendorId IS NOT NULL` |
| OverdueCount | int | same predicate as §7, grouped by vendor |
| RevisionRate | double | `DocumentRevisions` count joined via `Document.VendorId` — GREEN |
| FirstPassRate | double? | same windowed query as §9, grouped by vendor — YELLOW, nullable |
| AverageReviewDays | double? | `WorkflowHistory.ActionDate` deltas joined via `Document.VendorId` — YELLOW |

Documents with `VendorId = null` (internal engineering work) are **excluded from this model
entirely**, not folded into a fake "no vendor" row unless the UI (E-05C) explicitly wants one —
that's a display decision, not a query-architecture one, and this document defers it.

---

## 11. Exception Model

`EngineeringDashboardExceptionDto`:

| Field | Type | Source |
|---|---|---|
| DocumentId, DocumentNo, Title | int, string, string | Document |
| DisciplineName | string | join |
| VendorName | string? | join, nullable |
| CurrentStatus | string | `Document.Status` |
| CurrentStage | string? | current `WorkflowInstance.CurrentStageId` → stage name, null if no active instance |
| Severity | string (`Low`/`Medium`/`High`/`Critical`) | computed per exception type — thresholds are an E-05C implementation decision, not invented here |
| AgeDays | int | computed from the relevant date per type (see below) |
| Reason | string | fixed text per exception type, e.g. `"Overdue by {N} days"` |
| RecommendedAction | string | fixed text per exception type — **never freeform/generated**, must be traceable to the type |
| DrilldownFilter | `EngineeringDocumentListFilter` | via §6's mapping |

**Exception types enabled, per E-05A's authoritative-data rule:**

| Type | Enabled? | Source |
|---|---|---|
| Overdue | Yes | §7's predicate |
| PendingApproval | Yes | `Status = "Checked"` + age |
| PendingIDC | Yes | `WorkflowInstanceStage` join |
| StuckWorkflow | Yes | pending stage age beyond an operator-visible threshold (no invented SLA — this is a **visualization threshold**, distinct from an SLA breach; see §14's Age-vs-SLA distinction) |
| HighRevision | Yes | §9's `RevisionCount` |
| RepeatedRejection | Yes | §9's `RejectionCount` |
| **ClientResponseIssue** | **No — excluded** | E-05A §8: no field distinguishes "issued to a client" from "issued internally," so this exception type cannot be raised reliably without producing false positives on internally-routed documents |

Each type is its own query (different source table/predicate), unioned in the service layer after
each has independently applied the §3 security restriction.

---

## 12. Trend Model

`EngineeringTrendDto`:

| Field | Type | Source |
|---|---|---|
| PeriodStart | DateTime | truncated `WorkflowHistory.ActionDate` (for Submitted/Approved/Rejected/Issued — these are workflow *actions*) or `DocumentRevision.CreatedDate` (for Revisions) |
| Submitted, Approved, Rejected, Issued | int | `COUNT(*)` grouped by truncated date and `ActionType`/`Decision` |
| RevisionsCreated | int | separate query, `DocumentRevisions` grouped by truncated `CreatedDate` |
| WorkflowCompleted | int | `WorkflowInstance` grouped by truncated `CompletionDate` where `Status = "Completed"` |

**Grain**: parameterized (`Day`/`Week`/`Month`), default **Month** for the executive view per the
directive's own default — SQL Server `DATETRUNC`/`DATEADD(month, DATEDIFF(month, 0, ActionDate), 0)`-
style truncation, computed in SQL, not pulled into memory first. Two separate queries (workflow
actions vs. revisions) because they're different source tables with different truncation columns —
combining them into one query would force an unnecessary join between unrelated grains.

---

## 13. Performance Strategy

Per §7 of the directive and §17 of E-05A: **every query in §2 is `IQueryable` →
filter/`Where` → `GroupBy`/conditional-aggregate → `ToListAsync()` at the end, never
`ToListAsync()` first.** This is the same discipline E-03 established for the MDR list
(`AsNoTracking()`, filter-then-page, never load-then-filter) — the dashboard extends it to
aggregation instead of pagination.

**Explicitly flagged self-critique**: E-03's own `EngineeringDocumentRepository.
GetSummaryCountsAsync` issues **six separate `CountAsync()` round trips** against the same
filtered `IQueryable`. That was acceptable for one small KPI strip; repeating that pattern across
nine dashboard sections would mean dozens of round trips per page load. **E-05C should
consolidate each section into one materialized `GroupBy` + conditional-sum query** (mirroring
`EquipmentService.GetDashboardAsync`'s proven one-round-trip pattern: one `ToListAsync()`, then
compute every number for that section from the same in-memory result), not extend the
six-`CountAsync` pattern to new sections.

`AsNoTracking()` on every query (read-only, no entity tracking needed). No `.Include()` beyond
what a given section's *displayed* fields actually need (E-03's own lesson: `GetPagedAsync`
dropped `Attachments`/`Equipment`/`Contract` includes the list view never rendered — the same
discipline applies here per-section).

---

## 14. Aging Model

Buckets (visualization only): `0–2`, `3–7`, `8–14`, `15–30`, `30+` days.

**Explicit architectural distinction, per the directive's own instruction**:

- **AGE** = `DATEDIFF(day, PlannedDate, GETDATE())` (or `CreatedDate` where `PlannedDate` is null —
  documented as a fallback, surfaced in the UI as "no planned date," never silently substituted) —
  a neutral, descriptive number.
- **SLA BREACH** = age compared against a *business-defined target duration*. **No such target
  exists anywhere in the current data model or configuration** (E-05A §11/§13). The aging model
  produces buckets; it does **not** produce a breach flag. Any red/critical coloring of an aging
  bucket in the future UI (E-05C) must be framed as "this is old" (age), never as "this violates
  an SLA" (breach) — those are different claims and only the first is currently true.

---

## 15. Index Strategy

| Query | Predicate | Grouping | Existing Index | Recommendation |
|---|---|---|---|---|
| `GetSummaryAsync` | `IsActive`, `Status`, discipline restriction | none (conditional counts) | `IX_Documents_IsActive_Status` | Sufficient |
| `GetDisciplinePerformanceAsync` | `IsActive`, discipline restriction | `DisciplineId` | `IX_Documents_IsActive_Status` covers the filter; **no index on `DisciplineId` alone** | Evaluate `IX_Documents_DisciplineId_Status` in E-05C once this query's actual plan is measured — not created here |
| `GetVendorPerformanceAsync` | `IsActive`, `VendorId IS NOT NULL`, discipline restriction | `VendorId` | **No index on `VendorId`** | Evaluate `IX_Documents_VendorId` in E-05C |
| `GetWorkflowMetricsAsync` | `WorkflowInstanceStage.Status = "Pending"` | `WorkflowStageId` | `IX_WorkflowInstanceStages (AssignedToEmployeeId, Status)` — **does not cover this query** since `AssignedToEmployeeId` is dead (E-05A §10) and the query doesn't filter on it | Evaluate an index on `WorkflowInstanceStage (WorkflowStageId, Status)` in E-05C |
| `GetTrendAsync` | `ActionDate` range | truncated `ActionDate` | `IX_WorkflowHistories (WorkflowInstanceId)` — **no index on `ActionDate`** | Evaluate `IX_WorkflowHistories_ActionDate` in E-05C — this is the query most likely to need it, since trend queries scan a date range across all instances rather than one instance at a time |
| `GetRevisionQualityAsync` | `IsActive` | `DocumentId` | No dedicated index on `DocumentRevisions.DocumentId` confirmed in E-05A — recommend checking during E-05C implementation | Evaluate |
| First-pass approval (§9) | `Module, BusinessObjectType, BusinessObjectId` + `StartDate` order | window partition | `IX_WorkflowInstances (Module, BusinessObjectType, BusinessObjectId, Status)` — covers the partition columns but not the `StartDate` ordering | Evaluate including `StartDate` in this index in E-05C, after measuring actual query cost |

**No index is created in this phase.** Every recommendation above is deferred to E-05C, to be
confirmed against measured query plans rather than speculative — consistent with the directive's
own "do not create indexes blindly" instruction (E-05A §14 already made this same call).

---

## 16. API Contract

Minimum surface, consolidating per the directive's §20 suggestion where the underlying queries are
cheap and share the same base filter, keeping heavier/independently-failable sections separate:

```
GET /EngineeringDashboard/Summary?<EngineeringDashboardFilterDto>
    → { Summary, DisciplinePerformance, WorkflowMetrics, RevisionQuality }
    (four GREEN/cheap sections, same base predicate, combined to cut round trips on first paint)

GET /EngineeringDashboard/VendorPerformance?<filter>
    → List<EngineeringVendorPerformanceDto>
    (separate: heavier join, vendor-specific)

GET /EngineeringDashboard/Exceptions?<filter>
    → List<EngineeringDashboardExceptionDto>
    (separate: multiple unioned sub-queries, independently failable)

GET /EngineeringDashboard/Trends?<filter>&grain=Month
    → List<EngineeringTrendDto>
    (separate: different aggregation grain, cache-friendliest section if caching is ever added)

GET /EngineeringDashboard/Aging?<filter>
    → List<EngineeringAgingBucketDto>
    (separate: distinct bucket shape)
```

Drilldown (§6) is **not a new endpoint** — it's a client-side navigation to the existing
`EngineeringDocumentsController.Index` route with query-string parameters derived from
`EngineeringDocumentListFilter`, per §6's mapping table.

A new `EngineeringDashboardController` (mirroring the existing `EngineeringDocumentsController`
pattern, not folded into `HomeController`) — the directive's own §5 requires the Engineering
Dashboard be its own first-class module, not a tab on the generic dashboard, and `HomeController`'s
existing inline-aggregation style (§13's self-critique) is explicitly not the pattern to extend.

---

## 17. DTO Contract

All DTOs below are **new**, proposed only, living in `PEMS.Application/DTOs/` per existing
convention. None expose `Document`/`EngineeringDocument`/`WorkflowInstance` entities directly.

| DTO | Key Properties | Nullability Notes | Security Scope |
|---|---|---|---|
| `EngineeringDashboardFilterDto` | see §5 | all optional | none — this is input, scoped by the controller before use |
| `EngineeringDashboardSummaryDto` | Active, PendingWorkflow, PendingIDC, PendingApproval, Issued, Rejected, ReviseAndResubmit, Overdue, Frozen (all `int`) | none nullable — all are `0` when genuinely zero (never used as a "no data" signal) | discipline-scoped |
| `EngineeringDisciplinePerformanceDto` | see §7 | `AverageReviewTime: double?` nullable | discipline-scoped (a restricted user should only ever receive their own row) |
| `EngineeringWorkflowMetricDto` | see §8 | `AverageStageDuration: double?` nullable | discipline-scoped |
| `EngineeringRevisionQualityDto` | see §9 | `FirstPassApproved: bool?` nullable | discipline-scoped |
| `EngineeringVendorPerformanceDto` | see §10 | `FirstPassRate`, `AverageReviewDays: double?` nullable | discipline-scoped |
| `EngineeringDashboardExceptionDto` | see §11 | none nullable except `VendorName`/`CurrentStage` | discipline-scoped |
| `EngineeringTrendDto` | see §12 | none nullable | discipline-scoped |
| `EngineeringAgingBucketDto` | `BucketLabel: string`, `Count: int` | none | discipline-scoped |
| `EngineeringDashboardDrilldownDto` | not a query result — a pure output of §6's mapping function: wraps an `EngineeringDocumentListFilter` plus a display label (e.g. `"Overdue documents in Mechanical"`) for the link text | n/a | inherits the caller's already-resolved scope, adds no new authorization |

---

## 18. UI Contract (design only — no implementation)

Layout, per the directive's §22, confirmed compatible with everything above:

```
HEADER — title, "as of <request time>" (see §19 freshness), active filter chips
ROW 1  — Executive KPI strip (from GetSummaryAsync)
ROW 2  — Discipline Performance | Workflow Bottleneck (side by side)
ROW 3  — Document Quality | Vendor Performance (side by side)
ROW 4  — Trend chart (with an accessible data-table alternative per accessibility req)
ROW 5  — Management Exceptions (action list, not a chart)
```

Every number in Rows 1–3 and every row in Row 5 is a link constructed via §6's drilldown mapping.
Row 4 (trend) has no drilldown in this contract — trend points aggregate across many documents and
don't map to a single filtered list meaningfully; this is a **known, accepted limitation**, not an
oversight. RTL/bilingual/responsive/accessibility requirements are carried forward unchanged from
the directive's §23/§33-35 — nothing about the query/DTO architecture above constrains or
conflicts with them; that work belongs entirely to E-05C/E-05G.

---

## 19. Data Freshness

No caching is introduced (§18 of the directive, confirmed — see below). Every dashboard request
executes the queries in §2 fresh against the live database at request time. **Source of truth**:
the same `PEMSDbContext`/SQL Server database the rest of the Engineering module reads and writes.
**"Last Updated"** in the UI (§18 layout) should show the timestamp of the request itself, not a
cached-data timestamp, since there is no cache — this is genuinely real-time, not "near-real-time
presented as real-time" (the directive's own §24 warning doesn't apply because no caching layer
exists to create the gap it's warning about).

**Caching** (§18 of the directive): **not evaluated as necessary in this phase.** None of the
query shapes in §2 are expensive enough on the data volumes seen in E-05A's exploration (~12,000
documents at time of audit) to justify the complexity of cache-key/TTL/invalidation design before
E-05C has even measured real query costs. If E-05C's implementation reveals a genuinely expensive
section (most likely candidate: the trend query or the first-pass-approval windowed query, per
§13's flagged risk), that specific section can revisit caching with real numbers to justify it —
not preemptively here.

---

## 20. Deferred Metrics (explicit, not fabricated)

Carried forward from E-05A §19/§20, restated as architecture deferrals rather than raw findings:

| Metric | Why deferred | What would unblock it |
|---|---|---|
| SLA Breach Rate | No SLA target exists anywhere in configuration | A business decision defining target durations per stage — out of scope for architecture |
| IDC Overdue | `DocumentIdcReferral` has no due-date field | A schema change adding a due date, plus a business rule for how it's set — not proposed here |
| Client Review Overdue | Same — no client due-date field | Same |
| Forecast Variance | No `ForecastDate` field exists on `Document` | A schema change — not proposed here |
| Contractor Performance | "Contractor" is not a modeled concept, only `Vendor` exists | A business/data decision on whether Contractor should even be introduced as distinct from Vendor — this document does not recommend it |
| Numeric Health Score | Directive's own rule: don't invent a misleading composite score | Use the categorical model in §17 of the directive's own document instead — see ADR-2 |
| Responsible Person (as a named individual) | No live "currently assigned" field carries an `EmployeeId` — `WorkflowInstanceStage.AssignedToEmployeeId` exists but is never populated; only `AssignedToRole` (a role string) is reliable | Populating `AssignedToEmployeeId` in `WorkflowRuntimeService` would unblock this — a workflow-engine change, explicitly out of scope for a dashboard phase |
| Client Review status/metrics generally | `IssueAsync`'s delivery-unit choice isn't persisted on `Document`, so "issued to client" can't be distinguished from "issued internally" after the fact | Persisting the delivery unit type on the document at Issue time — a small, real schema change, but a workflow-adjacent one, deliberately not bundled into a dashboard architecture phase |

---

## 21. ADR Decisions

**ADR-1: Hybrid query architecture (§2), not single-composite or per-widget-`CountAsync`.**
Rejected single composite query because it defeats per-section failure isolation (directive §37)
and becomes a maintenance liability whenever one section's definition changes. Rejected extending
E-03's six-`CountAsync`-per-summary pattern to the whole dashboard because it doesn't scale past
one small widget — documented as a self-critique in §13, not a criticism of E-03's original,
narrower scope.

**ADR-2: Health model is categorical, not numeric (per directive §17), and is explicitly NOT
implemented in E-05B.** E-05A already established the reliable signal set (overdue %, frozen
count, rejected/revise-resubmit %, IDC backlog size); this document does not add a health query to
§2's list because the directive's own instruction for this phase is "define the architecture and
required inputs only," and those inputs are already the ones feeding `GetSummaryAsync`/
`GetDisciplinePerformanceAsync` — no separate query is needed, only a classification function over
their outputs, to be implemented (with explicit, documented thresholds — not invented here) in
E-05C or later.

**ADR-3: No Contractor dimension, ever, unless a future business/data decision introduces one.**
`Vendor` is the only modeled organizational relationship on `EngineeringDocument`. Every "Vendor
Performance" DTO/query in this document is named and scoped accordingly.

**ADR-4: Client Review is excluded from the Exception model and the Vendor/Discipline models don't
attempt to backfill it.** The `ClientResponseIssue` exception type is explicitly disabled (§11)
rather than approximated from `Status = "Issued"`, per E-05A §8's finding that doing so would
conflate internally-routed and client-routed documents.

**ADR-5: Drilldown reuses `EngineeringDocumentListFilter` (E-03) rather than inventing a second
filter/query mechanism for the MDR list.** Two small additive fields (`OverdueOnly`,
`MinRevisionCount`) are identified as needed on that existing filter to complete every drilldown
target — flagged as required E-05C work, not designed here (that's implementation, not
architecture).

**ADR-6: No caching in this phase (§19).** Revisit only if E-05C's measured query costs justify
it.

---

## 22. E-05C Implementation Plan (preview only — not started)

In dependency order:

1. Create the 9 new DTOs (§17) in `PEMS.Application/DTOs/`.
2. Add `OverdueOnly: bool?` and `MinRevisionCount: int?` to `EngineeringDocumentListFilter`
   (ADR-5) — the only change to existing E-03 code this architecture requires.
3. Implement `IEngineeringDashboardService`/`EngineeringDashboardService` with the 8 real query
   methods from §2 (excluding `GetDrilldownFilterAsync`, which is a pure mapping function, not a
   service method needing DI/DB access) — each as ONE materialized `GroupBy` query per §13's
   consolidated pattern, not per-count round trips.
3a. **Prototype the first-pass-approval windowed query first** (§9/§21's flagged highest-risk
    query) before building the rest of the Quality model around it.
4. New `EngineeringDashboardController` with the 5 routes from §16.
5. Apply the §15 index recommendations only after measuring real query plans against realistic
   data volume — not before.
6. Write tests mirroring the existing `PEMS.Tests/Engineering/` convention: discipline-restriction
   parity tests (the E-03 pattern — restricted-with-null-discipline, restricted-with-discipline,
   unrestricted) for every new query method, plus the off-by-one revision-count case and the
   first-pass-approval cross-instance case explicitly.
7. **STOP before any UI** — E-05C per the directive's own phase boundary is query/service/
   controller only; Views/Razor/charts remain E-05C-later-subphase or E-05D+ per the original
   remediation directive's own phase split, to be confirmed with the user before starting.

**E-05B complete. Awaiting approval before E-05C.**
