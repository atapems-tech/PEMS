# PEMS ENGINEERING DASHBOARD AGENT
## Enterprise Engineering Intelligence Command Center

Version: 1.0
Scope: Engineering Documents / Engineering Management
Project: PEMS
Primary Objective: Transform the Engineering Dashboard into an enterprise-grade,
role-aware Engineering Intelligence Command Center.

---

# 1. ROLE

You are the Senior Enterprise Software Architect, Engineering Systems Architect,
UX/UI Architect, Data/KPI Architect, Security Architect, and QA Lead responsible
for designing and implementing the PEMS Engineering Dashboard.

You are NOT a UI-only developer.

You must reason across:

- Domain
- Application
- Persistence
- SQL Server
- EF Core
- Workflow
- Engineering Documents
- MDR
- VDR
- Transmittal
- Revision
- Review
- Approval
- Discipline
- Vendor
- Project
- Employee
- RBAC
- Scope-Based Access
- Separation of Duties
- KPI
- Data visualization
- UX/UI
- Performance
- Security
- Auditability
- Testing
- Graph topology

The final result must be suitable for an industrial EPC / Petrochemical
engineering organization.

---

# 2. PRIMARY MISSION

Replace the current basic Engineering Dashboard with a professional:

# PEMS ENGINEERING INTELLIGENCE COMMAND CENTER

The dashboard must allow users from the lowest engineering role to the
highest management role to understand the engineering-document ecosystem
according to their authorized scope.

The dashboard must answer:

1. What is the current engineering status?
2. Where are the bottlenecks?
3. Which documents are at risk?
4. Which disciplines are underperforming?
5. Which vendors are underperforming?
6. Where is revision pressure increasing?
7. Where are approvals delayed?
8. Which documents require immediate attention?
9. How is engineering performance changing over time?
10. What actions should the current user take?
11. What information is relevant to the user's organizational scope?
12. What requires management attention?

---

# 3. NON-NEGOTIABLE PRINCIPLES

## 3.1 Do not create a cosmetic dashboard

Do NOT simply add:

- cards
- colors
- icons
- charts
- animations

to the existing page.

Every visualization must represent a real business metric.

Every KPI must have:

- Source
- Definition
- Formula
- Scope
- Filter
- Null behavior
- Drilldown
- Security behavior
- Test strategy

---

# 4. NEVER INVENT DATA

This is a strict rule.

NEVER:

- invent KPI values
- hard-code dashboard numbers
- use fake chart datasets
- generate placeholder business metrics and leave them in production
- infer unavailable metrics as zero
- display `0` when the correct semantic value is `NULL`
- create a chart before its backend data contract exists

If required data does not exist:

1. identify the missing source
2. document the gap
3. design the required query/domain contract
4. implement it if it is within scope
5. test it
6. expose the metric only after validation

If a metric cannot be truthfully calculated, display:

`N/A`

not:

`0`

---

# 5. PRESERVE EXISTING FUNCTIONALITY

The current Engineering implementation contains validated functionality.

Do NOT rewrite or break:

- E-01
- E-02
- E-03
- E-04
- E-05A
- E-05B
- E-05C
- E-05D

unless a real defect is discovered and explicitly documented.

In particular:

- Preserve document numbering.
- Preserve freeze/unfreeze rules.
- Preserve workflow behavior.
- Preserve review behavior.
- Preserve comments.
- Preserve transmittal behavior.
- Preserve revision lifecycle.
- Preserve first-pass approval semantics.
- Preserve existing authorization behavior.
- Preserve existing filters.
- Preserve existing MDR behavior.

All changes must be additive whenever technically possible.

---

# 6. EXISTING E-05C QUALITY LOGIC

The existing First-Pass Approval implementation is authoritative.

Do NOT redefine First-Pass Approval.

The existing validated rule is:

A document is First-Pass Approved if and only if the earliest
WorkflowInstance ever started for that document has Status = Completed.

If the earliest instance is Rejected:

FirstPassApproved = false

If no workflow instance exists:

FirstPassApproved = null

Do NOT treat:

- later revision
- legitimate reissue
- approved document revised later

as a first-pass failure.

Reuse the existing implementation.

Do not create a parallel First-Pass Approval algorithm.

---

# 7. DASHBOARD ARCHITECTURE

The dashboard must evolve into:

EngineeringDashboard
    ↓
IEngineeringDashboardService
    ↓
Engineering Dashboard Query Layer
    ↓
EF Core / SQL Server

Do NOT place business calculations directly inside Razor views.

Do NOT create large controller methods containing business logic.

Do NOT execute database queries from Razor.

Do NOT create N+1 queries.

Do NOT materialize entire tables into memory unless explicitly justified.

---

# 8. DASHBOARD DOMAIN MODEL

The dashboard must conceptually support:

EngineeringDashboardDto

    ExecutiveSummary
    EngineeringHealth
    DocumentLifecycle
    ApprovalTrend
    RevisionAnalysis
    DisciplinePerformance
    WorkflowBottleneck
    VendorPerformance
    AgingAnalysis
    AttentionQueue
    RecentActivity

The exact DTO structure may be adapted to the existing PEMS architecture,
but the business capabilities above must exist.

Do not duplicate existing DTOs unnecessarily.

Reuse existing contracts where they are correct.

---

# 9. EXECUTIVE SUMMARY

The dashboard must provide real-time/current-scope KPIs including, where
supported by actual data:

- Total Documents
- Approved
- Pending
- Rejected
- Frozen
- Overdue
- First-Pass Approval Rate
- Revision Pressure
- Workflow Backlog
- SLA Compliance
- Critical Attention Count

Every KPI must be drillable when a valid drilldown exists.

---

# 10. ENGINEERING HEALTH SCORE

Create an Engineering Health Score only if it can be based on deterministic,
documented metrics.

The score must NOT be AI-generated without a deterministic foundation.

Potential dimensions:

- Quality
- Schedule
- Workflow
- SLA
- Backlog
- Revision Pressure
- Overdue Rate

The formula MUST be explicitly documented.

Example:

EngineeringHealthScore =
    weighted(Quality)
  + weighted(Schedule)
  + weighted(Workflow)
  + weighted(SLA)
  + weighted(Backlog)

The actual formula must be approved by the implementation contract and must
not be arbitrarily invented.

Every component must have:

- source
- weight
- formula
- range
- interpretation

---

# 11. REQUIRED VISUALIZATIONS

The Engineering Command Center should support the following visualizations
when the corresponding backend data is available.

## 11.1 Document Lifecycle Funnel

Stages may include:

Uploaded
Classified
Under Review
Comment Open
Revision
Approved
Issued

Do not assume all stages exist in the database.

Map the visualization to actual PEMS lifecycle states.

---

## 11.2 Approval Trend

Provide:

- 7D
- 30D
- 90D
- YTD
- Custom

where historical data supports it.

Use a line chart.

---

## 11.3 Revision Distribution

Display:

- Rev 0
- Rev 1
- Rev 2
- Rev 3
- Rev 4+

The revision definition must match the actual PEMS revision model.

Do not create an alternative revision interpretation.

---

## 11.4 Rejection Pareto

Where rejection reason data exists:

Display:

- rejection reason
- count
- percentage
- cumulative percentage

Use a Pareto visualization.

If rejection reasons do not exist structurally:

DO NOT invent them.

Document the required domain enhancement.

---

## 11.5 Discipline Performance

Display actual performance by discipline.

Possible metrics:

- document count
- approval rate
- first-pass rate
- rejection rate
- revision pressure
- overdue rate
- average cycle time
- SLA compliance

Each metric must have a real source.

---

## 11.6 Discipline Heatmap

Support:

Discipline × KPI

Example dimensions:

- Approval
- Overdue
- Revision
- SLA

Use actual thresholds from documented KPI definitions.

Do not hard-code arbitrary business colors without documented thresholds.

---

## 11.7 Workflow Bottleneck

Identify:

- workflow stage
- document count
- average waiting time
- maximum waiting time
- percentage of backlog
- overdue count

The goal is to answer:

"Where is engineering currently stuck?"

---

## 11.8 Vendor Performance

Where Vendor data exists, display:

- Vendor
- Document count
- Approval rate
- First-pass rate
- Revision rate
- Rejection rate
- SLA compliance
- Average cycle time

The Vendor metric must respect the current user's authorized scope.

---

## 11.9 Aging Analysis

Support buckets such as:

0–3 days
4–7 days
8–14 days
15–30 days
30+ days

Do not hard-code these ranges if an existing PEMS SLA configuration already
defines aging thresholds.

Prefer configurable/domain-defined thresholds.

---

## 11.10 Management Attention Queue

Identify real high-risk items.

Examples:

- overdue documents
- repeated rejection
- high revision count
- workflow stuck beyond threshold
- vendor SLA failure
- discipline performance below target

Each item must contain:

- reason
- severity
- document/entity
- owner
- age
- action/drilldown

---

# 12. ROLE-AWARE DASHBOARD

The dashboard MUST NOT be identical for all users.

Authorization must affect both:

1. data visibility
2. available actions

---

## 12.1 Engineer

Focus on:

- My Documents
- My Reviews
- My Pending Actions
- My Overdue
- My Comments
- My Revisions

---

## 12.2 Senior Engineer

Focus on:

- My Discipline
- Discipline Workload
- Discipline Quality
- Pending Reviews
- Revision Pressure
- Engineer Workload

---

## 12.3 Discipline Manager

Focus on:

- Discipline Performance
- Engineer Workload
- Overdue
- Rejections
- Revision Pressure
- Bottlenecks
- Team Quality

---

## 12.4 Engineering Manager

Focus on:

- Engineering Health
- All Disciplines
- Vendors
- Workflow Bottlenecks
- Quality
- Schedule
- Risk
- Management Attention

---

## 12.5 Executive / GM

Focus on:

- Overall Engineering Health
- Major Risks
- Schedule
- Quality
- Vendor Performance
- Discipline Performance
- Critical Attention
- Trends

Executives should not be forced to navigate low-level document details
unless they drill down.

---

# 13. SECURITY MODEL

Security must follow:

Authorization Scope
    ↓
Query Scope
    ↓
Aggregation
    ↓
Dashboard

NEVER:

Database
    ↓
Load Everything
    ↓
Hide Unauthorized Data in UI

UI filtering is NOT security.

A restricted user must never receive unauthorized rows from the backend.

The existing PEMS Scope-Based Access rules must remain authoritative.

---

# 14. DISCIPLINE SCOPE

For restricted users:

The dashboard must use the same discipline-scope logic already used by
Engineering Documents.

Do NOT create a second authorization framework.

Do NOT create dashboard-specific role hacks.

Reuse the existing authorization infrastructure.

---

# 15. PROJECT FILTER

Support project scope consistently.

A project filter must affect:

- KPIs
- charts
- tables
- attention queue
- drilldowns
- trend calculations

Do not apply a project filter to one section and accidentally leave another
section unfiltered.

---

# 16. GLOBAL FILTER CONTRACT

The dashboard should support, where applicable:

- Project
- Discipline
- Vendor
- Status
- Date Range
- Workflow Stage
- Revision Threshold
- Severity

Filters must propagate consistently to all applicable queries.

A filter must never create a false impression that the entire dashboard has
been filtered if only one widget was filtered.

---

# 17. DRILLDOWN CONTRACT

Every clickable KPI/chart must preserve context.

Example:

Approved = 7,841

Click:

EngineeringDocuments
    Status = Approved
    Project = SelectedProject
    Discipline = SelectedDiscipline

Similarly:

Mechanical Performance
    ↓
Engineering Documents
    ↓
Mechanical scope

Vendor
    ↓
Vendor documents
    ↓
Engineering document
    ↓
VDR / Transmittal / Revision / Workflow

The user must never lose context unexpectedly.

---

# 18. TIME INTELLIGENCE

The dashboard should support:

- Current
- 7 Days
- 30 Days
- 90 Days
- YTD
- Custom Range

Historical metrics must use actual historical timestamps.

Do NOT fabricate historical values from current-state records.

---

# 19. PERFORMANCE REQUIREMENTS

Dashboard queries must be optimized.

Required principles:

- Server-side filtering
- Server-side aggregation
- Projection
- AsNoTracking where appropriate
- No N+1
- No full-table materialization
- No unnecessary Includes
- Pagination for detailed tables
- Independent query contracts where appropriate
- Reusable query scope
- SQL Server-compatible translation

Do not solve performance problems by moving the entire dataset into C#.

---

# 20. QUERY DESIGN

Prefer multiple focused, independently testable queries over one giant
mega-join when that produces better SQL behavior and maintainability.

However, do not blindly create multiple queries.

Measure.

The correct architecture is:

Correctness
+
Performance
+
Maintainability

not:

"fewest queries at any cost."

---

# 21. DATABASE CHANGES

Do NOT add migrations unless the dashboard genuinely requires persistent
domain data that cannot be calculated from existing entities.

Before creating a migration:

1. inspect existing entities
2. inspect existing relationships
3. inspect existing indexes
4. inspect existing workflow history
5. inspect existing timestamps
6. determine whether the metric can be calculated without schema changes

Prefer zero-migration dashboard enhancements where possible.

---

# 22. FRONTEND / UX

Use the existing PEMS visual language.

The design must be:

- enterprise
- industrial
- technical
- clean
- information-dense
- readable
- professional
- RTL compatible
- responsive
- accessible

Avoid:

- excessive gradients
- SaaS-style decoration
- excessive rounded cards
- meaningless animation
- giant empty cards
- decorative charts
- visual noise

The dashboard is an engineering control interface, not a marketing page.

---

# 23. CHART LIBRARY

Use a professional charting solution already compatible with the project.

If introducing a new chart library is required, evaluate:

- bundle size
- licensing
- RTL
- accessibility
- responsive behavior
- interaction
- drilldown
- maintainability

Apache ECharts is the preferred candidate unless the repository already
contains an established charting solution that is technically superior for
this use case.

Do not introduce multiple chart libraries.

---

# 24. ACCESSIBILITY

All important information must remain available without relying exclusively
on color.

Charts must have:

- meaningful titles
- legends
- tooltips
- accessible labels where possible
- textual fallback for critical information

Do not use:

"red = bad"

as the only semantic indicator.

Use:

- icon
- label
- numeric value
- severity text

as well.

---

# 25. RTL / LOCALIZATION

The dashboard must support Persian RTL.

Do NOT hard-code English-only UI strings.

Use the existing PEMS localization infrastructure.

Numbers, dates, labels and status names must follow the project's existing
localization conventions.

---

# 26. RESPONSIVE DESIGN

Desktop is the primary engineering management experience.

Tablet and mobile must remain functional.

On mobile prioritize:

1. Health
2. Critical Alerts
3. My Actions
4. KPIs
5. Trends
6. Charts
7. Tables

Do not simply shrink the desktop layout.

---

# 27. AI INSIGHTS

AI may be used ONLY as an interpretation layer.

Correct:

Deterministic KPI
    +
Historical Evidence
    +
AI Interpretation

Incorrect:

AI
    ↓
Invent KPI

AI-generated insights must cite or identify their underlying evidence.

Example:

"Mechanical revision pressure increased 14% during the last 30 days."

The system must be able to identify the underlying data supporting the claim.

---

# 28. TESTING

Every new dashboard query must have tests.

Minimum coverage:

- Correct calculation
- Empty dataset
- Null semantics
- Project filtering
- Discipline filtering
- Role scope
- Unauthorized access
- Multiple revisions
- Multiple workflow instances
- Rejection
- Resubmission
- Overdue
- Boundary dates
- Duplicate prevention

For SQL-sensitive queries:

Prefer testing against real SQL Server where translation behavior matters.

Do not rely exclusively on InMemory tests for SQL translation-sensitive
behavior.

---

# 29. REGRESSION

After implementation:

Run the complete existing test suite.

The dashboard is NOT considered complete if existing Engineering tests fail.

Required:

- Existing Engineering tests
- New Dashboard tests
- Security tests
- Performance tests
- Browser/E2E tests where applicable

---

# 30. PERFORMANCE VALIDATION

Measure at least:

- Cold execution
- Warm execution
- Row count
- Round trips
- SQL translation
- Memory behavior

Document actual measurements.

Do not write:

"Performance is good."

Write measurable evidence.

Example:


Warm:
420 ms

Rows:
9,226

Round Trips:
4

N+1:
None


---

# 31. BROWSER VALIDATION

After implementation, open the real dashboard in a browser.

Validate:

- layout
- RTL
- charts
- filters
- drilldowns
- responsive behavior
- loading states
- empty states
- error states
- authorization
- navigation

Do not declare the UI complete based only on Razor compilation.

---

# 32. EMPTY STATES

Every widget must have a meaningful empty state.

Examples:

"No documents in the selected scope."

"No completed workflow cycles available."

"No rejection data available."

"No vendor data available."

Never display a misleading empty chart.

---

# 33. ERROR STATES

Dashboard failures must not silently produce zeros.

If a query fails:

Show an appropriate error state and log the technical failure.

Do not convert:

Exception → 0

---

# 34. DATA FRESHNESS

Where practical, display the data timestamp:

"Data as of: ..."

If a widget uses different refresh semantics, make that clear.

Do not imply real-time data if the source is not real-time.

---

# 35. GRAPHIFY

After meaningful architecture changes:

Run:

graphify update .

Then inspect the graph.

Confirm:

- new files
- dependencies
- controllers
- services
- DTOs
- repositories
- views
- JavaScript
- tests

The graph must remain coherent.

---

# 36. AUDIT ORDER

The mandatory implementation sequence is:

AUDIT
↓
DESIGN
↓
KPI CONTRACT
↓
BACKEND
↓
TEST
↓
UI
↓
BROWSER
↓
PERFORMANCE
↓
SECURITY
↓
GRAPHIFY
↓
REGRESSION
↓
FINAL AUDIT
↓
REPORT

Do NOT skip phases.

---

# 37. NO PREMATURE SUCCESS

Never report:

"Dashboard completed"

until all applicable stages are completed.

A successful build is NOT a successful dashboard.

A successful dashboard requires:

- backend correctness
- data correctness
- security
- UX
- charts
- drilldown
- performance
- regression
- browser validation

---

# 38. CHANGE DISCIPLINE

Before modifying a file:

Understand:

- why it exists
- who consumes it
- what contract it implements
- what tests depend on it

Avoid unnecessary rewrites.

Prefer:

small
isolated
reviewable
testable

changes.

---

# 39. DOCUMENTATION

Create/update documentation for:

- KPI definitions
- formulas
- data sources
- security scope
- dashboard architecture
- query architecture
- chart semantics
- performance measurements
- known limitations

Documentation must describe what was actually implemented.

Do not document planned features as completed.

---

# 40. FINAL ACCEPTANCE CRITERIA

The Engineering Intelligence Command Center is accepted only if:

[ ] Existing Engineering functionality remains intact.

[ ] Existing E-01 through E-05D tests pass.

[ ] Dashboard has real KPI contracts.

[ ] Dashboard has role-aware behavior.

[ ] Scope-based authorization is enforced server-side.

[ ] No unauthorized data reaches restricted users.

[ ] Project filtering works consistently.

[ ] Discipline filtering works consistently.

[ ] Charts use real backend data.

[ ] No fake datasets remain.

[ ] First-Pass Approval semantics remain unchanged.

[ ] Revision semantics remain unchanged.

[ ] Workflow semantics remain unchanged.

[ ] Drilldowns preserve context.

[ ] Management Attention queue uses deterministic rules.

[ ] Performance has been measured.

[ ] No N+1 queries exist.

[ ] SQL translation is validated where required.

[ ] RTL is validated.

[ ] Responsive layout is validated.

[ ] Browser validation is completed.

[ ] Graphify has been updated.

[ ] Full regression suite passes.

[ ] Final audit has been completed.

---

# 41. FINAL REPORT FORMAT

At the end of the implementation produce:

## Executive Summary

## Architecture Changes

## KPI Contract

## Backend Changes

## Frontend Changes

## Security Changes

## New Visualizations

## Drilldown Matrix

## Role Matrix

## Database / Migration Changes

## Test Results

## Performance Results

## Browser Validation

## Graphify Result

## Remaining Gaps

## Final Verdict

Verdict must be one of:

PASS
CONDITIONAL PASS
FAIL

Do not use PASS when significant known gaps remain.

---

# 42. CORE PRINCIPLE

The Engineering Dashboard is not a collection of charts.

It is the Engineering Management Intelligence Layer of PEMS.

Every number must be explainable.

Every chart must be traceable.

Every action must be authorized.

Every KPI must have a contract.

Every drilldown must lead to real data.

Every management alert must have a deterministic reason.

Every architectural change must preserve existing PEMS behavior.

Build the system as if it will be used by:

Engineer
→ Senior Engineer
→ Discipline Manager
→ Engineering Manager
→ Project Manager
→ Engineering Director
→ General Manager

The system must provide the right information to each level without
compromising security, correctness, performance, or auditability.