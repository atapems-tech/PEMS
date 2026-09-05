# E-05C — Engineering Dashboard Quality Query Implementation Gate

## 1. Executive Summary

E-05C validated the highest-risk query identified across E-05A/E-05B (First-Pass Approval) against
a real SQL Server database — both its generated SQL and its behavior across the concrete lifecycle
scenarios that make it genuinely hard (rejection-then-resubmission, revision-after-successful-issue,
never-submitted documents) — then implemented the full Quality query contract on top of it:
`GetRevisionQualityAsync` (revision count, rejection count, first-pass approval) and the
`MinRevisionCount` drilldown wiring into the existing E-03 `EngineeringDocumentListFilter`.

All 6 ADRs from E-05B held. One real gap was found and fixed during implementation, not assumed
away: `GetRevisionQualityAsync` initially had no `ProjectId` filter, which E-05B's own filter
contract (§5) called for — discovered because this repository's dev database contains ~9,226 real,
pre-existing engineering documents, and the first test run against it returned thousands of
unrelated rows instead of the 1-2 rows each test actually seeded. Fixed by adding the parameter,
not by weakening the tests.

**Verdict: PASS.**

---

## 2. First-Pass Approval Prototype

### 2.1 What "First-Pass Approval" actually means (verified, not assumed)

Investigation of `WorkflowRuntimeService.ExecuteActionAsync` confirmed: **within a single
`WorkflowInstance`, a `Reject` transition is always terminal** — `instance.Status` becomes
`"Rejected"` and that instance never recovers or continues. There is no "reject then approve within
the same instance" path. This means "first-pass approval" is meaningless at the single-instance
level (an instance either completes cleanly or doesn't — there's no partial-credit state to
measure) and can only be meaningfully defined **per document, across however many
`WorkflowInstance` rows that document has accumulated**.

Further investigation of `SubmitAsync` and the `EngineeringDocumentsController.CreateRevision`
action confirmed the two mechanisms that can produce multiple instances for one document:

1. **Reject/Revise & Resubmit → manual resubmit**: each `SubmitAsync` call starts a brand-new
   `WorkflowInstance` unconditionally (verified: no "attempt number" or `PreviousInstanceId` link
   exists between instances).
2. **`CreateRevision` on an already-Issued document**: verified this action has **no status guard
   beyond `IsFrozen`** — it can be called on an `Issued` document, silently resetting
   `Document.Status` back to `"Draft"` without touching the workflow engine at all. A subsequent
   `Submit` then starts a second, independent, legitimate instance for a **new revision cycle**,
   not a quality failure.

**This second mechanism is why a naive "does this document have more than one instance" check
would be wrong** — it would incorrectly penalize documents that were revised for entirely
legitimate reasons (as-built updates, client-requested changes) after a clean first approval.

### 2.2 Validated definition

```
A document is First-Pass Approved if and only if:
  the EARLIEST WorkflowInstance ever started for it (by StartDate) has Status = 'Completed'.

If the earliest instance is 'Rejected', the document is NOT first-pass approved,
regardless of what happens in any later instance.

If a document has never had any WorkflowInstance, FirstPassApproved is NULL — not false.
```

This correctly handles all three cases: reject-then-resubmit-then-approve is `false` (earliest
instance rejected); approve-then-later-revise-and-reissue is `true` (earliest instance still
completed); never-submitted is `null` (no data to judge).

### 2.3 EF Core query and generated SQL — verified, not assumed

Candidate query (a correlated `NOT EXISTS` subquery identifying "no instance for this document
started earlier"), tested via `IQueryable.ToQueryString()` against real SQL Server
(`PEMS.Tests/Engineering/FirstPassApprovalPrototypeTests.cs`):

```sql
SELECT [w].[BusinessObjectId]
FROM [WorkflowInstances] AS [w]
WHERE [w].[Module] = N'EngineeringDocument' AND [w].[BusinessObjectType] = N'Document' AND NOT EXISTS (
    SELECT 1
    FROM [WorkflowInstances] AS [w0]
    WHERE [w0].[Module] = N'EngineeringDocument' AND [w0].[BusinessObjectType] = N'Document'
      AND [w0].[BusinessObjectId] = [w].[BusinessObjectId] AND [w0].[StartDate] < [w].[StartDate])
  AND [w].[Status] = N'Completed'
```

**Fully server-side. Single round trip. No client evaluation.** This is the standard, reliable
"first row per group" SQL pattern (a correlated `NOT EXISTS`), chosen over the alternative
`GroupBy(...).Select(g => g.OrderBy(...).First())` shape specifically because that alternative has
a history of inconsistent EF Core translation across versions — the `NOT EXISTS` form was verified
working, not assumed to work.

### 2.4 Scenario validation (all against real SQL Server, real seeded workflow definition)

| Scenario | Expected | Result |
|---|---|---|
| Single instance, Submit→Check→Approve→Issue, no rejection | First-pass = true | **PASS** |
| Reject at Check, resubmit, then Submit→Check→Approve→Issue succeeds | First-pass = false (earliest instance was Rejected) | **PASS** |
| Submit→Check→Approve→Issue (clean), then `CreateRevision` (no workflow touch), then Submit→Check→Approve→Issue again | First-pass = true (earliest instance was still Completed) | **PASS** |
| Document created, never submitted (no `WorkflowInstance` at all) | Excluded from result set entirely (null, not false) | **PASS** |

All 5 gate tests (`FirstPassApprovalPrototypeTests`) pass. **First-Pass Approval is proven, not
assumed.**

---

## 3. Query Semantics

| Query | Source Entities | Grouping Key | Filters | Date/Window Semantics | Revision Semantics | Workflow Semantics | Duplicate-Row Risk |
|---|---|---|---|---|---|---|---|
| Document list (base) | `EngineeringDocument` | — | `IsActive`, discipline restriction, `ProjectId` | — | — | — | None — one row per document |
| Revision count | `DocumentRevision` | `DocumentId` | `IsActive` | — | `COUNT(*) - 1` (Rev 00 always exists, verified via `EngineeringDocumentsController.Create`/`CreateRevision`) | — | None — `GROUP BY` guarantees one row per document |
| Rejection count | `WorkflowHistory` ⋈ `WorkflowInstance` | `WorkflowInstance.BusinessObjectId` | `Decision = "Reject"`, `Module/BusinessObjectType` | — | — | Counts every Reject decision across every instance for that document | None — join is on `WorkflowInstanceId` (unique per history row), then grouped |
| First-pass approval | `WorkflowInstance` (self-correlated) | `BusinessObjectId` | `Module/BusinessObjectType`, earliest `StartDate` | `StartDate` used to identify "earliest," not a filter range | Deliberately independent of revision count (see §2.1) | Status of the earliest instance only | None — `NOT EXISTS` guarantees at most one row per document |

---

## 4. Query Architecture (ADR-1 compliance)

`GetRevisionQualityAsync` is implemented as **four small queries**, matching E-05B §9's own design
(explicitly *not* one mega-join):

1. Base document projection (`Select` directly to DTO shape, discipline + project scoped).
2. Revision counts, `GroupBy DocumentId`, scoped to the document IDs from (1).
3. Rejection counts, join + `GroupBy`, scoped to the same document IDs.
4. First-pass status, the validated `NOT EXISTS` query, scoped to the same document IDs.

Results (2)-(4) are combined into the (1) list **in the service/repository layer via
`Dictionary` lookups**, not a SQL-level join — this is deliberately the same pattern E-05B
specified for the Discipline model (§7): several cheap, independently-indexable queries, combined
in C#, rather than one query with three joins that would be harder to reason about and to tune.
**No `GetAllAsync()` and no full-table materialization** — every query here is filtered before
`ToListAsync()`.

---

## 5. Quality KPI Contract

| Dashboard KPI | Query Method | Entity/Relationship | Filter | Result | Drilldown |
|---|---|---|---|---|---|
| Revision Count (per document) | `GetRevisionQualityAsync` → query 2 | `DocumentRevision.DocumentId` | `IsActive`, off-by-one adjustment | `int`, 0 when only Rev 00 exists | `EngineeringDocumentListFilter.MinRevisionCount` |
| Rejection Count (per document) | `GetRevisionQualityAsync` → query 3 | `WorkflowHistory.Decision="Reject"` via `WorkflowInstance` | `Module/BusinessObjectType` | `int` | `EngineeringDocumentListFilter.Status = "Rejected"` (approximation, per E-05B §6 — true "repeated rejection" needs this same `RejectionCount` field, not yet a dedicated filter) |
| First Pass Approved (per document) | `GetRevisionQualityAsync` → query 4 | `WorkflowInstance` earliest-by-`StartDate` | `Module/BusinessObjectType` | `bool?` — **null is a real, distinct value** | not directly drillable (a boolean isn't a list filter); the aggregate **First Pass Approval Rate** (`COUNT(true) / COUNT(NOT NULL)`) is a Layer-4 KPI computed from this same field-level data, left to the caller (E-05D+) to aggregate — this query returns the row-level truth, not the rate |
| High Revision Documents | `GetRevisionQualityAsync` with `minRevisionCount` | same as Revision Count | `RevisionCount >= N` | filtered `List<EngineeringRevisionQualityDto>` | same list, already filtered — this **is** the drilldown target |

---

## 6. Drilldown Contract

**`OverdueOnly` was NOT added** — verified against the actual Quality model scope (E-05B §9/§6):
it belongs to the Discipline/Overdue drilldown from a different dashboard section, not Quality.
Adding it here would have been scope creep into a filter nothing in this phase's contract needs.

**`MinRevisionCount` was added** to `EngineeringDocumentListFilter` (E-03's existing filter, not a
new mechanism) and wired into `EngineeringDocumentRepository.ApplyFilters` as a correlated
subquery:

```sql
WHERE (SELECT COUNT(*) FROM DocumentRevisions r WHERE r.DocumentId = x.DocumentId AND r.IsActive) - 1 >= @threshold
```

Verified end-to-end: `GetPagedAsync` with `MinRevisionCount` set returns exactly the documents the
standalone `GetRevisionQualityAsync` query identifies as meeting the threshold
(`GetPagedAsync_MinRevisionCountFilter_DrilldownWiring_Works` test) — the drilldown target and the
Quality query agree by construction, not by coincidence, since both ultimately express the same
"COUNT − 1" rule.

All other filter dimensions checked and confirmed already functional from E-03: discipline
filtering, status filtering, project filtering. Vendor filtering exists on the entity
(`VendorId`) but is not yet exposed as an `EngineeringDocumentListFilter` field — not needed for
the Quality drilldown specifically (Quality's DTO carries `VendorName` for display, not as a
drilldown target in this phase), left for whichever phase implements the Vendor Performance
drilldown.

---

## 7. Performance Validation

Measured against the **live dev database** (not a synthetic dataset): 9,226 `EngineeringDocument`
rows, 20,223 `DocumentRevision` rows, 756 `WorkflowInstance` rows, 804 `WorkflowHistory` rows.

| Query | Cold (incl. connection open) | Warm | Round Trips | Notes |
|---|---|---|---|---|
| `GetRevisionQualityAsync` (unscoped, full dataset, 9,225 result rows) | 6.5–7.2s | **375–491ms** | 4 (base + 3 lookups) | No N+1 — each lookup query is scoped to the exact document ID set from the base query, not per-row |
| `GetPagedAsync` (page 1 of 25, filtered from 9,225) | 230ms | **95ms** | 2 (count + page) | Matches E-03's original design expectation |

**No N+1 behavior observed** in either query — confirmed by inspecting the query shapes
themselves (§3/§4), not just by timing. The cold-vs-warm gap (6-7s → 375-491ms) is standard
ADO.NET/EF connection-open and query-plan-cache overhead on the first call of a test run, not a
per-query cost — documented here so it isn't mistaken for a real performance problem later.

**GroupBy materialization**: all three grouped queries (`revisionCounts`, `rejectionCounts`,
`earliestInstanceStatus`) materialize via `ToDictionaryAsync`, keyed by document ID — O(1) lookup
when combining into the base document list, not O(n) scans.

**Index dependency**: per E-05B §15/§19, no new index was created (out of scope for an
implementation-gate phase per the directive's own instruction). The unscoped 375-491ms warm
timing is already acceptable for a management dashboard section without one. If a future phase
adds a discipline- or project-heavy filter to this specific query at larger data volume, revisit
`WorkflowInstances (Module, BusinessObjectType, BusinessObjectId, StartDate)` (the E-05B §15
recommendation, still not created) — not needed today based on measured evidence.

---

## 8. Lifecycle Compatibility

**Zero changes to `PEMS.Domain`, workflow transitions, workflow seeding, or any existing
Engineering Document controller action's behavior.** Every change in this phase is additive:

- New DTO (`EngineeringRevisionQualityDto`) — new type, nothing removed.
- New repository/service methods (`GetRevisionQualityAsync`) — new methods, existing methods
  (`GetPagedAsync`, `GetSummaryCountsAsync`, `GetAllAsync`, `GetByIdAsync`, `UpdateAsync`, etc.)
  untouched.
- One new optional field (`MinRevisionCount`) on `EngineeringDocumentListFilter` — optional,
  defaults to `null`, no behavior change for any existing caller that doesn't set it.
- One new optional parameter (`projectId = null`) added to `GetRevisionQualityAsync`'s own
  signature mid-phase (see §1) — this method was introduced in this same phase, so there is no
  prior caller to break.
- `ApplyFilters` changed from `private static` to `private` (instance method) to access `_context`
  for the new `MinRevisionCount` subquery — a visibility/binding change only, not a behavior
  change to any existing filter it already applied.

**No migration.** All 58 tests across the full suite (E-01 through E-05C) pass — the E-01
(numbering/freeze), E-02 (workflow/IDC/client-review/comments), E-03 (pagination), and E-04
(traceability/lifecycle) test suites were re-run in full alongside the new E-05C tests, not just
the new ones in isolation.

---

## 9. Test Results

```
Total: 58, Passed: 58, Failed: 0, Skipped: 0
```

New in this phase (16 tests):
- `FirstPassApprovalPrototypeTests` (5): the mandatory gate — SQL translation + 4 lifecycle
  scenarios (§2.4).
- `EngineeringRevisionQualityTests` (7): revision-count off-by-one, rejection-count across
  instances, first-pass-null-when-never-submitted, discipline-restriction parity (both signals,
  mirroring the E-03 pattern exactly), `MinRevisionCount` filtering on both the Quality query and
  the paginated list drilldown.
- `EngineeringRevisionQualityPerformanceTests` (2): the §7 measurements, non-flaky (30s/5s
  ceilings, not tight timing assertions) so they don't become a source of CI flakiness while still
  catching a genuine regression.

All SQL-Server-dependent (first-pass approval cannot be validated against InMemory/SQLite — same
constraint documented since E-01's `NumberingServiceTests`).

---

## 10. Graphify Result

`graphify update .` was run at the end of this phase against the live repository (including the
externally-reorganized `.claude/Role/` documentation tree, §11).

- **Topology changed**: yes (expected — 5 new test files, 1 new DTO, and new/relocated
  documentation were all extracted this run: "AST extraction: 25/25 uncached files").
- **Node count**: 6909 (up from 6866 at the end of E-05B — +43 nodes, consistent with the new code
  added: `EngineeringRevisionQualityDto`, `GetRevisionQualityAsync` on both interface/repository/
  service, `MinRevisionCount` field, and 16 new test methods across 3 new test files).
- **Edge count**: 13764 (up from 13606).
- **Community count**: 577 (down from 594 — communities were re-clustered, not lost; graphify
  logged "renamed 88 community(ies) by their hub").
- **No unexpected coupling**: the new code only touches the Engineering module's existing
  Application/Persistence layers (`IEngineeringDocumentRepository`, `EngineeringDocumentRepository`,
  `IEngineeringDocumentService`, `EngineeringDocumentService`) — no new cross-module dependency was
  introduced (confirmed by the diff in §11 touching only 6 pre-existing Engineering-scoped files
  plus new, self-contained files).
- No manual edits were made to any graphify output file.

---

## 11. Git Diff Summary

**Code changes (all additive, confirmed via `git status`/`git diff --stat`)**:
- `PEMS.Application/DTOs/EngineeringDocumentListFilter.cs` (+6 lines: `MinRevisionCount`)
- `PEMS.Application/DTOs/EngineeringRevisionQualityDto.cs` (new file)
- `PEMS.Application/Interfaces/IEngineeringDocumentRepository.cs` (+5: new method signature)
- `PEMS.Application/Interfaces/IEngineeringDocumentService.cs` (+3: new method signature)
- `PEMS.Application/Services/EngineeringDocumentService.cs` (+5: passthrough implementation)
- `PEMS.Persistence/Repositories/EngineeringDocumentRepository.cs` (+97/-1: `GetRevisionQualityAsync`, `MinRevisionCount` wiring, `ApplyFilters` visibility change)
- 5 new test files under `PEMS.Tests/Engineering/`

**Unrelated, externally-observed change**: the `.claude/Role/` documentation folder was
reorganized outside this session — my own E-05A/E-05B report files were relocated into a new
`.claude/Role/UIUX/` subfolder (alongside other pre-existing specification documents not
authored by this session), and a `VENDOR DOCUMENTATION MANAGEMENT PROCEDURE.md` file moved from
`.claude/` to `.claude/Role/`. **This is documentation housekeeping, not a code or schema change**
— confirmed no `.cs`, `.cshtml`, `.csproj`, or migration file was affected. Flagged here per the
directive's own "verify the repository has not changed unexpectedly" instruction, not treated as
a blocker: this phase's E-05A/E-05B reports are now at
`.claude/Role/UIUX/PEMS_ENGINEERING_DASHBOARD_E05A_DATA_CONTRACT.md` and
`.claude/Role/UIUX/PEMS_ENGINEERING_DASHBOARD_E05B_ARCHITECTURE.md` respectively — this report
continues to reference them by their original names since content is unchanged, only location.

**No migrations added or removed in this phase.**

---

## 12. Known Limitations

- **First Pass Approval *Rate*** (the aggregate percentage, not the per-document boolean) is not
  yet computed anywhere — this phase delivers the row-level `FirstPassApproved: bool?` field;
  aggregating it into a rate (`COUNT(true) / COUNT(true OR false)`, excluding nulls per the
  directive's "missing ≠ zero" rule) is straightforward but was left to whichever phase builds the
  actual `EngineeringDashboardSummaryDto`/Executive KPI row, since that's a different query
  responsibility (§2 of E-05B) than the Quality model this phase covers.
- **Repeated Rejection** as an Exception type (E-05B §11) still has no dedicated filter — it's
  approximated via `Status = "Rejected"` per E-05B's own note; the `RejectionCount` field this
  phase adds is the right data to eventually build a proper `RejectionCount >= N` filter, but that
  filter wasn't added to `EngineeringDocumentListFilter` in this phase since it wasn't required by
  anything in the Quality contract's own drilldown list.
- **Vendor filtering** is not yet exposed on `EngineeringDocumentListFilter`, despite
  `EngineeringRevisionQualityDto` carrying `VendorName` — deferred to the Vendor Performance
  phase's own drilldown design, not fabricated here.
- Index recommendations from E-05B §15 remain unimplemented, by design (measured performance
  didn't demonstrate a need yet — see §7).

---

## 13. Architecture Decisions Confirmed

| ADR | Status | Evidence |
|---|---|---|
| ADR-1 (Hybrid query architecture) | **Confirmed, not reopened** | §4 — 4 small queries, not 1 mega-join, not N+1 |
| ADR-2 (Health model categorical, no new query) | **Confirmed, not reopened** | No health query was added in this phase; nothing in this phase's KPI contract computes a numeric score |
| ADR-3 (No Contractor dimension) | **Confirmed, not reopened** | `EngineeringRevisionQualityDto` carries `VendorName` only |
| ADR-4 (No fake Client Review inference) | **Confirmed, not reopened** | Not touched by the Quality contract at all — no Client Review logic exists in this phase's code |
| ADR-5 (Reuse `EngineeringDocumentListFilter`, additive only) | **Confirmed, refined** | Only `MinRevisionCount` was added; `OverdueOnly` verified as genuinely not needed for this phase's scope (§6) rather than added speculatively |
| ADR-6 (No caching) | **Confirmed, not reopened** | Measured warm performance (375-491ms for a full-dataset query) does not justify it |

**No ADR was invalidated by implementation evidence.** The one design gap found (missing
`ProjectId` filter) was a completeness gap against E-05B's own filter contract (§5), not a
contradiction of any ADR.

---

## 14. E-05D Readiness Assessment

Ready to proceed, with the following carried forward as explicit inputs to whatever comes next
(not blockers):

- The `NOT EXISTS` first-pass-approval pattern (§2.3) is now a proven, reusable building block —
  any future query needing "earliest/latest row per group" (e.g. a similar pattern might be useful
  for Client Review's "first response" semantics, if that gap is ever closed) can reuse this exact
  shape with confidence.
- The 4-small-queries-combined-in-C# pattern (§4) is now demonstrated twice (Discipline model
  design in E-05B, Quality model implementation here) — treat it as the established convention for
  the remaining dashboard sections (Workflow, Vendor, Exceptions, Trends), not something to
  redesign per-section.
- First Pass Approval Rate (the aggregate) and Repeated Rejection filtering (§12) are the two
  smallest, most natural next additions if the next phase continues the Quality/Discipline
  direction rather than moving to a different layer (Workflow bottlenecks, Vendor performance,
  etc.) — flagged as options, not a decision made here.

## Verdict: **PASS**

First-Pass Approval was proven against real SQL Server, real generated SQL, and four concrete
lifecycle scenarios — not assumed from entity names. All 6 ADRs held under implementation
pressure. One real gap (missing `ProjectId` filter) was found via real-data testing and fixed
correctly, not glossed over. All 58 tests pass, including full regression across E-01–E-04. No
lifecycle, schema, or unrelated behavior was changed.
