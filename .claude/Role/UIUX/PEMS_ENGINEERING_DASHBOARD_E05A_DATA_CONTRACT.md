# E-05A — Engineering Management Dashboard: Data Contract & KPI Feasibility Audit

**Status:** Read-only. No code, schema, query, index, or permission changes were made in this
phase. Every finding below is verified against the live repository as of the end of E-04
(document numbering/freeze, test foundation, MDR pagination, reverse traceability + lifecycle
states), not against pre-E-04 audit assumptions.

---

## 1. Executive Summary

The Engineering module has a genuinely solid foundation for a dashboard — a real workflow engine
with reliable action timestamps (`WorkflowHistory.ActionDate` is non-null on every row of every
kind), a working IDC gate, and (after E-03) a proven pattern for discipline-scoped server-side
aggregation. But three structural gaps will shape what the dashboard can honestly show on day
one:

1. **IDC has no due date and no per-referral status at all.** `DocumentIdcReferral` is a
   5-column table (id, document, discipline, created-by, created-date) — completion is only
   inferable indirectly through the generic workflow engine's stage records, and there is no
   date anywhere to compute "IDC overdue."
2. **First-pass approval requires reasoning across multiple `WorkflowInstance` rows, not one.**
   Every resubmission starts a brand-new workflow instance with no link back to the one it
   replaced — genuinely calculable, but only via a windowed/grouped query, not a simple field
   read.
3. **"Contractor" does not exist as a concept distinct from `Vendor`.** Anywhere the directive
   says "Contractor," the codebase has only `Vendor`. Don't build a Vendor/Contractor split the
   data can't support.

Nothing here blocks E-05B. It changes what gets built: fewer invented metrics, more explicit
"not currently reliable" labels, and dashboard queries designed around the real two-signal
discipline-restriction pattern already proven in E-03 (`isRestricted: bool, disciplineId: int?`).

---

## 2. Current Architecture

- **Domain**: `Document` (base, TPT) → `EngineeringDocument`. Workflow-adjacent: `DocumentRevision`,
  `DocumentIdcReferral`, `DocumentComment`, `Transmittal`/`TransmittalItem`. Traceability FKs:
  `EquipmentId`, `ContractId`, `VendorId`, `RelatedMRId` — now bidirectional as of E-04
  (`Equipment.EngineeringDocuments`, `Contract.EngineeringDocuments`, `Vendor.EngineeringDocuments`,
  `MaterialRequisition.EngineeringDocuments`).
- **Workflow**: `PEMS.WorkflowPlatform` (generic engine, shared with Permit-to-Work) —
  `WorkflowDefinition`/`WorkflowVersion`/`WorkflowStage`/`WorkflowTransition`/`WorkflowInstance`/
  `WorkflowInstanceStage`/`WorkflowHistory`. Engineering's definition (`EngineeringDocumentWorkflowSeeder`):
  Check → IDC (parallel, dynamic participants) → Approval → Issue, plus the "Reject" transition
  reused for both `RejectAsync` and E-04's `ReviseAndResubmitAsync`.
- **Application/Persistence**: `EngineeringDocumentService`/`EngineeringDocumentRepository` now
  expose `GetPagedAsync`/`GetSummaryCountsAsync` (E-03) with a proven server-side discipline-
  restriction pattern. `DocumentWorkflowService` is the sole write path for all lifecycle
  transitions (Submit/Check/Approve/Reject/ReviseAndResubmit/Issue/IdcRespond/RecordClientResponse).
- **Web**: `EngineeringDocumentsController` (MDR list + Details + all workflow actions),
  `DocumentRegisterController` (a second, still-unpaged MDR view — flagged in E-03 as an open
  item, unrelated to this audit), `HomeController` (global dashboard — see §16 for why its
  pattern should **not** be copied).

---

## 3. Data Availability Matrix

| KPI / Data | Available? | Source Entity | Source Field(s) | Calc Required? | Timestamp? | Reliable? | Missing Dependency |
|---|---|---|---|---|---|---|---|
| Total Active Documents | Yes | Document | `IsActive` | No | — | Yes | — |
| Overdue Documents | Yes | Document | `PlannedDate`, `ActualDate` | Yes (see §5) | via PlannedDate | Yes | — |
| Due Documents (upcoming) | Yes | Document | `PlannedDate` | Yes (date-range) | Yes | Yes | — |
| Pending IDC | Yes (count only) | WorkflowInstanceStage | `Status="Pending"` for IDC stage | Yes (join) | No (no due date) | Partially | No due date, no per-referral status |
| Pending Engineering Review | Yes | Document | `Status="Submitted"` | No | — | Yes | — |
| Pending Approval | Yes | Document | `Status="Checked"` | No | — | Yes | — |
| Client Review (submitted-to-client count) | Partial | Document | `Status="Issued"` | Yes | No | **Not reliable** | Issue doesn't distinguish client vs internal delivery on the Document itself (§8) |
| Revise & Resubmit | Yes | Document | `Status="Revise & Resubmit"` (E-04) | No | — | Yes | — |
| Rejected | Yes | Document | `Status="Rejected"` | No | — | Yes | — |
| Issued | Yes | Document | `Status="Issued"` | No | — | Yes | — |
| Frozen Documents | Yes | Document | `IsFrozen` (E-01) | No | `FrozenDate` | Yes | — |
| First Pass Approval Rate | Yes, nontrivial | WorkflowInstance | `(Module,BusinessObjectType,BusinessObjectId)` grouped by `StartDate` | Yes (windowed query) | `StartDate`/`CompletionDate` | Yes, but cross-instance | — |
| Revision Rate / Avg Revision Count | Yes | DocumentRevision | `COUNT(*) - 1` per DocumentId | Yes | `CreatedDate` | Yes | Must filter `IsActive`, subtract 1 for the always-created Rev 00 |
| Docs with 2+/3+/4+ revisions | Yes | DocumentRevision | `COUNT(*) GROUP BY DocumentId HAVING >= N+1` | Yes | — | Yes | Same off-by-one adjustment |
| Rejection Rate | Yes | WorkflowHistory | `Decision="Reject"` count / total decisions | Yes | `ActionDate` | Yes | — |
| Avg Review Time (Submit→Check) | Yes | WorkflowHistory | `ActionDate` of Start row → Check's Approve/Reject row | Yes (join within instance) | Yes | Yes | — |
| Avg Approval Time (Check→Approval decision) | Yes | WorkflowHistory | consecutive `ActionDate`s within one instance | Yes | Yes | Yes | — |
| Avg IDC Completion Time | Partial | WorkflowInstanceStage (IDC) | `AssignedDate`→`ActionDate` | Yes | Yes | Yes, but no per-discipline due date to compare against | No SLA/due-date target exists to compute *breach*, only elapsed time |
| Avg Client Response Time | Yes, once "issued to client" is defined (§8) | Document | `ModifiedDate` at Issue → `ClientResponseDate` | Yes | Yes | Partially | Depends on resolving §8's gap first |
| Overdue Rate | Yes | Document | Same predicate as Overdue Documents | Yes | Yes | Yes | — |
| SLA Breach Rate | **No** | — | — | — | — | **Not available** | No SLA target duration is configured anywhere in the codebase for any stage |
| Total/Pending/Overdue/... by Discipline | Yes | Document | `DisciplineId` + same predicates as above | Yes (GroupBy) | — | Yes | Must apply discipline-restriction filter identically to detail queries (§9) |
| Vendor: Submitted/Approved/Rejected/Revision Rate/First-Pass/Overdue/Avg Review/Avg Response | Yes | Document + WorkflowHistory | `VendorId` join | Yes (joins) | Yes | Yes | "Contractor" breakdown is not separately available — see §11 |
| Planned/Forecast/Actual | Partial | Document | `PlannedDate`, `ActualDate` | — | — | Partial | **No `ForecastDate` field exists anywhere on `Document`/`EngineeringDocument`** — only Planned and Actual |
| Delay Days | Yes | Document | `DelayDays` (computed `[NotMapped]` property, E-03 already reimplemented the logic in SQL for `GetSummaryCountsAsync`) | Yes | Yes | Yes | — |

---

## 4. KPI Feasibility Matrix

| KPI | Feasibility | Notes |
|---|---|---|
| Total/Approved/Pending/Rejected/Frozen/Overdue (Executive KPI row) | **GREEN** | Same predicates E-03's `GetSummaryCountsAsync` already uses — just needs Revise&Resubmit/Issued/Pending IDC/Client Review added |
| Pending IDC count | **YELLOW** | Requires a join to `WorkflowInstanceStage` for the IDC stage, not a `Document` field alone |
| IDC Overdue | **RED** | No due date exists anywhere in the IDC data model |
| First Pass Approval Rate | **YELLOW** | Correct but requires a windowed/grouped query across `WorkflowInstance`, not a single-table read |
| Revision Rate / 2+/3+/4+ revisions | **GREEN** | Direct `GROUP BY` on `DocumentRevisions`, minus the off-by-one adjustment |
| Average Review/Approval Time | **YELLOW** | Requires self-joining `WorkflowHistory` rows within the same instance to compute deltas between consecutive `ActionDate`s |
| SLA Breach Rate | **RED** | No SLA target exists to breach against — do not invent one |
| Client Review metrics | **RED→YELLOW** | Blocked until "was this issued to a client" is resolved (§8); once resolved, becomes YELLOW (join through `Letter`) |
| Discipline performance matrix | **GREEN** | Direct `GROUP BY DisciplineId`, same predicates as Executive KPIs |
| Discipline × Status heatmap | **GREEN** | Direct `GROUP BY DisciplineId, Status` |
| Vendor performance | **YELLOW** | Needs joins to `WorkflowHistory` for review-time/response-time; submission/rejection counts alone are GREEN |
| Aging buckets | **GREEN** | Simple `CASE WHEN` on `(Now - PlannedDate)` / `(Now - CreatedDate)`, same shape as E-03's overdue predicate |
| Upcoming deadlines | **GREEN** | Direct date-range filter on `PlannedDate` |
| Plan vs Forecast vs Actual | **RED for Forecast** | No `ForecastDate` field exists — Plan vs Actual alone is GREEN |
| Health classification | **YELLOW** | Feasible as a *classification* (not a numeric score) from the GREEN/YELLOW signals only — see §12 |

---

## 5. Metric Definition Audit — "Overdue"

Only one authoritative date pair exists on `Document`: `PlannedDate` and `ActualDate` — no
`DueDate`/`ClientDueDate`/`ForecastDate` field exists anywhere on `Document`/`EngineeringDocument`.
This is the **same** logic E-03's `GetSummaryCountsAsync` already implemented in SQL (reproducing
the `[NotMapped] DelayDays` computed property, since EF can't translate it):

```
Overdue := PlannedDate IS NOT NULL AND (
    (ActualDate IS NOT NULL AND ActualDate > PlannedDate)   -- delivered late
    OR (ActualDate IS NULL AND PlannedDate < GETDATE())      -- not delivered, past due
)
```

**IDC due date, Client due date**: neither exists. Any dashboard "IDC Overdue" or "Client Response
Overdue" tile must either be omitted or explicitly labeled as unavailable — not silently computed
from a substitute field.

---

## 6. Workflow Timestamp Audit

| Transition | Start Event | End Event | Timestamp Source | User Source | Status/Action Source | Reliable? |
|---|---|---|---|---|---|---|
| Submission → Review (Check) | `WorkflowHistory` row `ActionType="Start"` | `WorkflowHistory` row `ActionType="Approve"/"Reject"` at Check stage | `ActionDate` on both rows | `UserId`/`Role` on the ending row only (Start rows have null UserId/Role) | `Decision` on the ending row | **Yes** |
| Review → Approval | Check-stage decision row | Approval-stage decision row | `ActionDate` on both | Yes on both | `Decision` on both | **Yes** |
| Submission → IDC completion | Check→IDC transition row | Last IDC-stage decision row (per `DetermineStageOutcome`'s all-referred-disciplines-responded logic) | `ActionDate` | Yes per referral response | `Decision` | **Yes**, but only elapsed time — no due date to compare against |
| Client Submission → Client Response | **Not currently reliable** | `Document.ClientResponseDate` | `ClientResponseDate` exists but "Client Submission" has no matching event field (§8) | — | — | **NOT CURRENTLY RELIABLE** |
| Approval → Issue | Approval decision row | Issue action row | `ActionDate` | Yes | `ActionType="Issue"` | **Yes** |

`ActionDate` is populated on **every** `WorkflowHistory` row regardless of type (confirmed against
`WorkflowRuntimeService.ExecuteActionAsync`/`StartInstanceAsync`/`TryAutoSkipEmptyStageAsync`) — it
is the one timestamp field the dashboard can always trust. `Decision`/`Role`/`UserId` are null on
system-generated `"Start"` and `"AutoSkip"` rows (expected — those aren't human decisions), so any
query computing "who approved" must filter `ActionType NOT IN ('Start','AutoSkip')`.

---

## 7. First-Pass Approval Audit

**PARTIALLY AVAILABLE — calculable, but not from a single instance's history.**

`SubmitAsync` calls `_runtime.StartInstanceAsync` unconditionally on every submit *and every
resubmit* (confirmed: the only guard is on the document's *starting* status — Draft/Rejected/Revise
& Resubmit — not on whether this is a first attempt). Each call creates a **brand-new**
`WorkflowInstance` row with a fresh `StartDate` and no `PreviousInstanceId`/attempt-number field
linking it to whatever instance it's replacing.

**What's missing**: a direct link between instances for the same document. **What's not missing**:
enough information to reconstruct it — every instance still carries `(Module, BusinessObjectType,
BusinessObjectId)`, so:

```sql
first_pass := an instance is "first pass" if it is the earliest (by StartDate) among all
              WorkflowInstance rows sharing the same (Module, BusinessObjectType, BusinessObjectId),
              AND it reached Status = 'Completed'.
```

This requires a windowed query (`ROW_NUMBER() OVER (PARTITION BY ... ORDER BY StartDate)` or
equivalent grouped subquery) — feasible in SQL, not a simple field read. **Do not attempt to
approximate this from `Document.Status` history or revision count** — those are influenced by
in-instance rejections too (an instance can bounce Check→IDC→Reject without ever creating a new
`DocumentRevision`), so revision count is not a valid proxy for "how many submission attempts."

---

## 8. Client Review Audit

**Internal Engineering Status and Client Review Status are NOT equivalent — confirmed, and the
gap is sharper than previously understood.**

Existing fields (`EngineeringDocument.ClientApprovalStatus`, `ClientComment`, `ClientResponseDate`)
are populated only by `RecordClientResponseAsync`, gated to `Document.Status == "Issued"`.

**The real gap**: `IssueAsync` accepts a `deliveryUnitType` (`InternalDCC` / `InternalDepartment` /
`External`) that determines whether a delivery letter is created — but **this value is never
persisted on the `Document` itself**. So after the fact, a query cannot reliably tell whether a
given `Status="Issued"` document was actually sent to a client (`External`) or just routed
internally (`InternalDCC`/`InternalDepartment`). The only indirect proxy is the `Letter` table
(`LetterType="Outgoing"` + `RelatedDocumentId`), which is a **join, not a field**, and conflates
"issued externally" with any other outgoing correspondence about that document.

| Field | Available | Source | Notes |
|---|---|---|---|
| Submitted-to-client date | **No** | — | No field distinguishes an External issue from Internal; must proxy via `Letter.LetterDate` where `LetterType="Outgoing"` and `RelatedDocumentId` matches — indirect, not exact |
| Response date | Yes | `ClientResponseDate` | Reliable |
| Client status | Yes | `ClientApprovalStatus` (A/B/C/D codes mapped to Approved/Rejected) | Reliable |
| Client comments | Yes | `ClientComment` | Reliable |
| Overdue condition | **No** | — | No client due-date field exists |
| Approval / Rejection / Approval-with-comments | Partial | `ClientApprovalStatus` codes A/B/C/D | Codes B/C/D all collapse to `Document.Status="Rejected"` today — "Approved With Comments" as a client-side distinction is not separately preserved on `Document.Status`, only in the raw `ClientApprovalStatus` code if the dashboard reads that directly instead of `Status` |

---

## 9. Discipline Security Model

Confirmed unchanged and must be replicated exactly for every new dashboard query — this is the
single most important cross-cutting constraint for E-05B.

`EngineeringDocumentsController` resolves two signals once per request:
- `isRestricted: bool` — false for `SystemAdmin`/`EngineeringManager`/`EngineeringSupervisor`/
  `DocumentController`, or if the user/employee record can't be resolved (fails open, matching
  pre-existing behavior — not something this audit is proposing to change).
- `myDisciplineId: int?` — the restricted user's own discipline, which may itself be `null`
  (an employee with no discipline assigned is restricted to *only* null-discipline documents, not
  "everything" — this exact nuance was the subject of a dedicated regression test in E-03).

**Every** new dashboard aggregate — not just document lists — must accept and apply
`(isRestricted, myDisciplineId)` server-side:

- Executive KPI counts: **must** be scoped (a restricted user must not see the global total).
- Discipline performance matrix: a restricted user should see only their own row (or the matrix
  should be omitted/collapsed for them entirely — a UI decision for E-05C, not this audit, but the
  *query* must not return other disciplines' numbers regardless of what the UI does with them).
- Vendor performance, aging, trends: same restriction applies — a restricted user must not be able
  to infer other-discipline volume through a vendor or trend breakdown that isn't itself
  discipline-filtered.
- **Do not solve this in the UI.** Every new service method needs the same two parameters
  `EngineeringDocumentRepository.GetPagedAsync`/`GetSummaryCountsAsync` already take.

---

## 10. Vendor / Contractor Data Audit

**"Contractor" is not a modeled concept — confirmed by repo-wide search.** Every occurrence of the
word "Contractor" in the codebase is either: a `WorkOrder.ContractorName`/`ContractorCost` pair
(free-text CMMS work-order fields, unrelated to EDMS), a label inside an inspection line item
comment, an `Employee.PersonnelType` classification note, or prose in the remediation directive
itself describing an *aspirational* traceability chain that doesn't exist in schema.

`EngineeringDocument` has exactly one organization-side FK relevant here: `VendorId` (plus
`ContractId`, which points to a `Contract` that itself has `VendorId`). **There is no
free-text or FK "Contractor"/"ResponsibleOrg" field on `Document`/`EngineeringDocument`.**

**Recommendation: build vendor performance metrics against `Vendor` only.** Do not build a
parallel "Contractor" dimension — it would either duplicate `Vendor` or require inventing a new
field the business hasn't asked for through any existing form/import path.

**Responsible Person**: also not a direct field. `PreparedById`/`CheckedById`/`ApprovedById` are
historical (set once an action happens, not a live assignment). The only "currently assigned"
signal is `WorkflowInstanceStage.AssignedToRole` (a **role string**, e.g. `"EngineeringSupervisor"`
or `"Discipline:5"`) — `AssignedToEmployeeId` on that same table is declared but **never actually
populated** by `WorkflowRuntimeService` (confirmed by exploration: only `AssignedToRole` is set at
every stage-creation call site). So a "Responsible Person" column can show a **role**, not a named
individual, without a new field — flag this explicitly rather than fabricating a name from
`PreparedById` as a stand-in for "who owns this now" (they're different questions).

---

## 11. Health Model Feasibility

### Candidate Signals (reliable today)
- Overdue % (§5's predicate)
- Frozen count
- Rejected % / Revise & Resubmit %
- IDC backlog **size** (pending count — not overdue, since no due date exists)
- Revision rate (2+/3+/4+ revision documents)

### Missing Signals
- SLA breach rate (no SLA target exists anywhere)
- IDC overdue (no IDC due date)
- Client response overdue (no client due date)
- First-pass approval rate *could* be included but requires the cross-instance query from §7 —
  usable, just costlier than the others

### Recommended Minimum Health Model
Per the remediation directive's own fallback rule ("if a composite score is not sufficiently
reliable, use a Health Classification instead of inventing a misleading numeric score") — **do
not build a numeric score.** Build a **classification** (Healthy / Watch / At Risk / Critical)
from only the reliable signals: overdue %, frozen count, rejected/revise-resubmit %, IDC backlog
size. Document the exact thresholds in code as named constants when E-05B implements it, not as
inline magic numbers — this audit intentionally does not propose the thresholds themselves, that's
implementation, not data-contract work.

---

## 12. Data Quality Risks

| Field | Classification | Notes |
|---|---|---|
| `Discipline` | GREEN | Required-ish in practice, FK exists, reference table small and stable |
| `Project` | GREEN | Required (non-nullable `ProjectId`) |
| `Vendor` | YELLOW | Nullable FK — many documents (internal engineering work) legitimately have no vendor; dashboard must treat "no vendor" as a real category, not an error |
| `Responsible Person` | RED | No direct field — see §10 |
| `Planned Date` | YELLOW | Nullable; delay/overdue/aging calculations silently exclude documents with no `PlannedDate` — must be surfaced as "no planned date" not folded into "on time" |
| `Forecast Date` | RED | Field doesn't exist |
| `Actual Date` | YELLOW | Nullable, populated only via MDR import or Issue-adjacent flows |
| `Revision` | GREEN | Reliable, append-only |
| `Workflow timestamps` | GREEN (for in-instance) / YELLOW (cross-instance) | See §6/§7 |
| `Client response timestamp` | YELLOW | Reliable once recorded, but "client submission" event itself is unreliable (§8) |
| `IDC due date` | RED | Doesn't exist |
| `SLA target` | RED | Doesn't exist anywhere in configuration |

---

## 13. Query Feasibility

| Dashboard Section | Feasibility | Notes |
|---|---|---|
| Executive KPI row | GREEN | Direct aggregation, same shape as E-03 |
| Discipline performance matrix | GREEN | `GROUP BY DisciplineId` |
| Discipline × Status heatmap | GREEN | `GROUP BY DisciplineId, Status` |
| IDC backlog (count, not overdue) | YELLOW | Join to `WorkflowInstanceStage` filtered to IDC stage name |
| Approval pipeline | GREEN | Status-based counts, same as Executive row |
| Aging buckets | GREEN | `CASE WHEN` over `PlannedDate`/`CreatedDate` deltas |
| Revision quality | GREEN | `GROUP BY DocumentId` on `DocumentRevisions` |
| First-pass approval rate | YELLOW | Windowed/grouped query across `WorkflowInstance` (§7) |
| Avg review/approval time | YELLOW | Self-join within `WorkflowHistory` per instance |
| Vendor performance | YELLOW | Join `Document.VendorId` → `WorkflowHistory` for timing metrics; counts alone are GREEN |
| Client review metrics | RED (blocked on §8) | Becomes YELLOW once "issued to client" is resolved |
| SLA breach | RED | No data-model change proposed here — needs a business decision on what the SLA targets even are, which is out of scope for a data-contract audit |
| Trends (time series) | GREEN | `GROUP BY` on a truncated date column of `WorkflowHistory.ActionDate` or `Document.CreatedDate` |

---

## 14. Index / Performance Assessment

| Existing Index | Table | Columns | Useful for Dashboard? |
|---|---|---|---|
| `IX_Documents_IsActive_Status` (composite, non-unique) | Documents | `IsActive, Status` | **Useful** — backs almost every Executive KPI predicate |
| `IX_Documents_DocumentNo` (unique, filtered) | Documents | `DocumentNo` | Not relevant to aggregation |
| `IX_DocumentIdcReferrals` (unique) | DocumentIdcReferrals | `DocumentId, DisciplineId` | Useful for the referral join, but the table carries no status/date to filter on beyond this |
| `IX_WorkflowInstances` (non-unique) | WorkflowInstances | `Module, BusinessObjectType, BusinessObjectId, Status` | **Partial** — good for per-document instance lookup, but §7's first-pass-approval query orders by `StartDate`, which isn't part of this index; a large-scale trend/first-pass query would benefit from `StartDate` being included |
| `IX_WorkflowInstanceStages` (non-unique) | WorkflowInstanceStages | `AssignedToEmployeeId, Status` | **Effectively dead** — `AssignedToEmployeeId` is never populated (§10), so this index doesn't serve any query pattern the dashboard would actually use; `AssignedToRole` (the field that *is* used) isn't indexed at all |
| `IX_WorkflowHistories` (non-unique) | WorkflowHistories | `WorkflowInstanceId` | **Partial** — fine for per-instance history reads, but §6/§7's time-based aggregation (avg review time, trends) filters/sorts by `ActionDate`, which has no index |

**Missing, not created in this phase (E-05A is read-only)**: no index exists on `Documents
(ProjectId)`, `Documents (DisciplineId)`, or `Documents (VendorId)` individually — E-03's
`GetPagedAsync`/`GetSummaryCountsAsync` filters on these columns today without dedicated index
support beyond the `(IsActive, Status)` composite. This was acceptable at E-03's scope (a single
paginated list); a dashboard issuing several discipline/vendor-grouped aggregate queries per page
load will exercise these predicates far more heavily. **Recommend evaluating** (not creating, per
E-05A's read-only constraint) an index on `Documents (ProjectId, DisciplineId, Status)` and
`WorkflowHistories (ActionDate)` as part of E-05B once the actual query shapes are finalized —
premature to add before then.

---

## 15. Proposed DTO Contract

All proposed, none created. Field-purpose tables only — no domain entities exposed directly,
matching the existing `EquipmentDashboardDto`/`LetterDashboardDto` convention (flat scalar counts
+ `List<(string Key, int Count)>`-style breakdowns), not `PagedResult<T>` (that's for lists, not
summaries).

**`EngineeringDashboardFilter`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| ProjectId | int? | user input | scope to one project |
| DisciplineId | int? | user input | scope to one discipline (in addition to, not instead of, the mandatory security restriction) |
| VendorId | int? | user input | scope to one vendor |
| DateFrom / DateTo | DateTime? | user input | bound trend/aging queries |

**`EngineeringDashboardSummary`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| Total, Approved, Pending, Rejected, Frozen, Overdue, Issued, ReviseAndResubmit | int | Document | Executive KPI row (extends E-03's `EngineeringDocumentSummaryCountsDto`) |
| PendingIdc | int | WorkflowInstanceStage join | Executive KPI row |
| FirstPassApprovalRate | double? | WorkflowInstance windowed query | nullable — null when no completed instances exist yet, not 0 |

**`EngineeringDisciplinePerformance`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| DisciplineId, DisciplineName | int, string | Discipline | row identity |
| Total, Pending, Overdue, PendingIdc, PendingApproval, Rejected, ReviseAndResubmit | int | Document/WorkflowInstanceStage | matrix row |

**`EngineeringWorkflowMetric`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| StageName | string | WorkflowStage | bottleneck row identity |
| PendingCount, AvgAgeDays, OldestDocumentNo | int/double/string | WorkflowInstanceStage + Document | "where is Engineering stuck" |

**`EngineeringExceptionItem`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| DocumentId, DocumentNo, Title | int/string | Document | identity |
| DisciplineName, Severity, AgeDays, NextAction | string/int/string | derived | action-list row (not a chart) |

**`EngineeringAgingBucket`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| BucketLabel | string | computed (`0-7`, `8-14`, ...) | chart/table axis |
| Count | int | Document | bucket size |

**`EngineeringVendorPerformance`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| VendorId, VendorName | int, string | Vendor | row identity |
| Submitted, Approved, Rejected, OverdueCount | int | Document | counts (GREEN) |
| RevisionRate, FirstPassRate, AvgReviewDays | double | DocumentRevision/WorkflowHistory | rates (YELLOW — needs join) |

**`EngineeringClientReviewMetric`** — explicitly marked partial per §8; only build once the
"issued to client" gap is resolved, or ship with a visible "data quality" caveat.

**`EngineeringRevisionMetric`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| DocumentId, DocumentNo | int/string | Document | identity |
| RevisionCount | int | `COUNT(*)-1` | quality flag input |
| VendorName, DisciplineName | string | joins | "High Revision Risk" list context |

**`EngineeringTrendPoint`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| PeriodStart | DateTime | truncated `ActionDate`/`CreatedDate` | X-axis |
| Submitted, Approved, Rejected, Issued | int | WorkflowHistory/Document | Y-series |

**`EngineeringHealth`**
| Field | Type | Source | Purpose |
|---|---|---|---|
| Classification | string (`Healthy`/`Watch`/`AtRisk`/`Critical`) | computed from §12's reliable signals only | headline indicator, not a numeric score |
| ContributingFactors | List\<string\> | computed | "why" — required by the directive's own drill-down/explainability rule |

---

## 16. Proposed Query/API Contract

Minimum read surface, one method per concern (mirrors the granularity the audit above actually
supports, rather than one giant call):

- `GetSummaryAsync(filter, isRestricted, disciplineId)` → `EngineeringDashboardSummary`
- `GetDisciplinePerformanceAsync(filter, isRestricted, disciplineId)` → `List<EngineeringDisciplinePerformance>`
- `GetWorkflowMetricsAsync(filter, isRestricted, disciplineId)` → `List<EngineeringWorkflowMetric>`
- `GetExceptionsAsync(filter, isRestricted, disciplineId)` → `List<EngineeringExceptionItem>`
- `GetAgingAsync(filter, isRestricted, disciplineId)` → `List<EngineeringAgingBucket>`
- `GetVendorPerformanceAsync(filter, isRestricted, disciplineId)` → `List<EngineeringVendorPerformance>`
- `GetRevisionQualityAsync(filter, isRestricted, disciplineId)` → `List<EngineeringRevisionMetric>`
- `GetTrendsAsync(filter, isRestricted, disciplineId, granularity)` → `List<EngineeringTrendPoint>`
- `GetHealthAsync(filter, isRestricted, disciplineId)` → `EngineeringHealth`

**Client Review metrics are deliberately excluded from this list** until §8's gap is either
accepted-with-caveat or resolved — proposing an endpoint for data that can't be trusted would
contradict the directive's own "no fake data" rule.

**Combining calls**: `GetSummaryAsync` + `GetDisciplinePerformanceAsync` could reasonably be
combined into one call for the initial page load (both are cheap `GROUP BY` queries touching the
same base predicate) to cut one round trip on first paint. The others (workflow metrics,
exceptions, aging, vendor, trends) involve materially different joins/costs and should stay
separate so a slow one (e.g. vendor performance's `WorkflowHistory` join) doesn't block the fast
ones — this also lets E-05's own performance target (§29 of the directive: "if one widget fails,
show a widget-level error, preserve the others") work naturally, since each is already an
independent call.

---

## 17. Recommended E-05B Architecture

**Hybrid (directive's Option C), specifically: several focused service methods (per §16), each
doing one materialized query with in-memory grouping — mirroring `EquipmentService.GetDashboardAsync`'s
proven pattern, not `HomeController`'s inline-`SafeCountAsync`-per-KPI pattern.**

Why not Option A (many tiny focused queries) as literally as E-03's own
`GetSummaryCountsAsync` did it: that method issues **six separate `CountAsync` round trips**
against the same filtered `IQueryable`. It works today because it's one small widget, but a full
dashboard repeating that pattern across nine sections would mean dozens of round trips per page
load. **Recommend consolidating each section's counts into one query with `GroupBy`/conditional
aggregation** (the `EquipmentDashboardDto` precedent: one `ToListAsync()`, then compute every
number for that section from the same in-memory materialization) rather than one `CountAsync` per
number.

Why not Option B (single composite query for the whole page): the sections have genuinely
different join costs and failure domains (§37 of the directive: a failing widget shouldn't break
the page) — one giant query would make partial degradation impossible and would be a maintenance
liability every time one section's definition changes.

**Do not introduce caching** — none of the discovered query shapes are expensive enough to justify
it yet, and per the directive's own §29, caching should only be added if profiling shows an actual
problem, not preemptively.

---

## 18. Risks

- **Client Review metrics are the highest risk of accidentally shipping misleading data** — the
  "issued to client" ambiguity (§8) is subtle enough that a future implementer could plausibly
  build it against `Status="Issued"` alone and produce numbers that quietly include internally-
  routed documents. Flag this prominently in E-05B's own plan.
- **SLA Breach Rate has no basis to exist at all** — any pressure to "just add a reasonable default
  SLA" during implementation should be resisted; that's a business decision, not a data-contract
  gap this audit can close.
- **`WorkflowInstanceStage.AssignedToEmployeeId` being dead code** is a trap for a future person who
  assumes it's populated (its own index exists, implying it should be) — worth a one-line note in
  E-05B's implementation, not just this audit.
- **First-pass approval's windowed query** is the most complex query in this whole contract;
  worth prototyping early in E-05B rather than discovering performance/correctness issues late.

---

## 19. Explicit "DO NOT BUILD" List

- No "Contractor" entity, field, or dashboard dimension separate from `Vendor`.
- No SLA Breach Rate metric (no target exists to breach).
- No IDC Overdue metric (no due date exists).
- No Client Review Overdue metric (no due date exists).
- No Forecast-based Plan/Forecast/Actual comparison (no Forecast field exists) — Plan vs Actual
  only.
- No numeric Health Score — classification only, per the directive's own fallback rule.
- No "Responsible Person" shown as a named individual sourced from a field that doesn't reliably
  carry one — show the assigned **role** (from `WorkflowInstanceStage.AssignedToRole`) or omit the
  column, don't fabricate a name from `PreparedById`.
- No first-pass-approval approximation from revision count or `Document.Status` history — only the
  cross-instance `WorkflowInstance` grouping in §7 is correct.

---

## 20. E-05A Acceptance Criteria — Status

- [x] Every KPI has a source (§3)
- [x] Every KPI has a feasibility status (§4, §13)
- [x] Every time-based KPI has verified timestamps (§6)
- [x] First-pass approval feasibility is verified (§7)
- [x] Revision calculation is verified (§3, §6 audit item 4 in exploration)
- [x] IDC metrics are verified (§3, §6)
- [x] Client Review metrics are verified (§8)
- [x] Discipline security is verified (§9)
- [x] Vendor/Contractor data is verified (§10)
- [x] Health model inputs are verified (§11)
- [x] Data-quality gaps are documented (§12)
- [x] Query feasibility is documented (§13)
- [x] Existing indexes are reviewed (§14)
- [x] DTO contract is proposed (§15)
- [x] Query/API contract is proposed (§16)
- [x] No implementation changes were made — confirmed: this session made zero edits to
      `PEMS.Domain`/`PEMS.Application`/`PEMS.Persistence`/`PEMS.Web`, no migrations, no queries, no
      indexes, no DTOs, no controllers, no views.

**E-05A complete. Awaiting approval before E-05B.**
