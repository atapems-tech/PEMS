# PEMS — ENGINEERING DASHBOARD
# E-05 — PETROCHEMICAL / EPC ENGINEERING MANAGEMENT COCKPIT
# IMPLEMENTATION DIRECTIVE

Status: IMPLEMENTATION
Phase: E-05
Module: Engineering Management / EDMS
Audience: Engineering Manager, Engineering Lead, Project Manager, Document Control Manager, Discipline Leads, Project Controls

---

# 1. MISSION

Build a production-grade Engineering Management Dashboard for PEMS.

This is NOT a generic dashboard.

This is NOT a collection of KPI cards.

This is an Engineering Management Cockpit designed for:

- Petrochemical Projects
- EPC / EPCM environments
- Engineering contractors
- Owner engineering organizations
- Multi-discipline engineering teams
- Large Master Document Registers
- IDC workflows
- Internal Engineering Review
- Client Review
- Document Revision Control
- Engineering deliverable monitoring

The dashboard must allow an Engineering Manager to answer, within seconds:

1. How healthy is Engineering?
2. Which disciplines are behind?
3. Which documents are overdue?
4. Where is the approval bottleneck?
5. Which vendors/contractors are underperforming?
6. How large is the IDC backlog?
7. How many documents are waiting for approval?
8. How many documents are in Revise & Resubmit?
9. How is Client Review performing?
10. What requires management intervention today?
11. Which documents are approaching SLA breach?
12. What changed compared with the previous period?

Every important dashboard metric MUST be actionable.

A KPI that cannot drill down to the underlying records is considered incomplete.

---

# 2. NON-NEGOTIABLE ARCHITECTURAL RULE

DO NOT create a new dashboard data architecture if existing Engineering services can provide the required information.

First inspect:

- EngineeringDocument
- Document
- DocumentRevision
- DocumentWorkflow
- DocumentComment
- DocumentIdcReferral
- Transmittal
- EngineeringDocumentService
- DocumentWorkflowService
- WorkflowPlatform
- NumberingService
- KnowledgeBase
- existing Dashboard/Home services
- existing PagedResult infrastructure

Reuse existing business logic.

Do NOT duplicate workflow rules inside the dashboard.

The Dashboard is a READ/ANALYTICS surface.

Business rules remain in Application/Domain services.

---

# 3. PRECONDITION

Before implementation, verify E-01 through E-04 status.

The following must already be corrected or protected:

- Document numbering
- Document freeze enforcement
- Test foundation
- MDR pagination/query architecture
- Traceability foundations
- Lifecycle model

If any prerequisite is incomplete, do NOT silently redesign it inside E-05.

Report the dependency.

---

# 4. DASHBOARD PRINCIPLE

The Dashboard must have four information layers:

LEVEL 1 — Executive Health
LEVEL 2 — Engineering Performance
LEVEL 3 — Bottlenecks / Exceptions
LEVEL 4 — Drill-down / Action

The user should be able to move:

Portfolio
→ Discipline
→ Workflow
→ Document
→ Action

without losing context.

---

# 5. PAGE STRUCTURE

Create a dedicated Engineering Dashboard route.

Preferred conceptual route:

/Engineering/Dashboard

or equivalent route consistent with the existing application architecture.

Do NOT replace the global corporate dashboard.

Engineering Dashboard must be its own first-class module.

---

# 6. GLOBAL FILTER BAR

At the top of the dashboard provide a persistent filter bar.

Required filters:

- Project
- Area
- Discipline
- Document Type
- Contractor
- Vendor
- Document Status
- Issue Purpose
- Revision
- Client Review Status
- IDC Status
- Date Range
- Health
- Responsible Person

Optional:

- Engineering Organization
- Package
- Work Area
- Document Category

Filters must be server-backed.

Do NOT load the entire MDR into JavaScript just to filter dashboard data.

---

# 7. FILTER BEHAVIOR

Filters must affect ALL dashboard widgets consistently.

Example:

Project = Project A
Discipline = Mechanical

Then:

- KPI cards
- discipline charts
- overdue list
- IDC backlog
- approval backlog
- revision metrics
- vendor metrics
- trend charts

must all reflect the same filter context.

Display active filters clearly.

Provide:

Clear Filters

Do not require page reload where the current application architecture allows efficient asynchronous refresh.

---

# 8. EXECUTIVE HEALTH HEADER

Create a high-level Engineering Health section.

Primary KPIs:

1. Total Active Documents
2. Due Documents
3. Overdue Documents
4. Pending IDC
5. Pending Engineering Review
6. Pending Approval
7. Client Review
8. Revise & Resubmit
9. Rejected
10. Issued

Secondary indicators:

- First Pass Approval Rate
- Overdue Rate
- Average Review Time
- Average Approval Time
- Average Client Response Time

Each KPI MUST provide:

- current value
- comparison with previous period where meaningful
- trend indicator
- drill-down action

Example:

Pending Approval
42
↑ 12%

Click:
→ MDR filtered to Pending Approval

---

# 9. ENGINEERING HEALTH SCORE

Create an overall Engineering Health indicator.

Do NOT make this a random visual score.

The score must be based on documented measurable indicators.

Candidate inputs:

- overdue percentage
- pending IDC percentage
- pending approval percentage
- SLA breach rate
- revision rate
- first-pass approval rate
- client response aging

The exact formula MUST be documented in code/configuration.

Do not hardcode unexplained magic numbers inside Razor/JavaScript.

If a composite score is not sufficiently reliable from current data, use a Health Classification instead of inventing a misleading numeric score.

Preferred classifications:

HEALTHY
WATCH
AT RISK
CRITICAL

---

# 10. DISCIPLINE PERFORMANCE MATRIX

This is a CORE dashboard component.

Create a discipline performance table.

Example:

| Discipline | Total | Pending | Overdue | IDC | Approval | Revise | Rejected | Health |
|------------|------:|--------:|--------:|----:|---------:|-------:|---------:|--------|
| Process | | | | | | | | |
| Mechanical | | | | | | | | |
| Piping | | | | | | | | |
| Electrical | | | | | | | | |
| Instrumentation | | | | | | | | |
| Civil | | | | | | | | |
| Structural | | | | | | | | |

Required metrics:

- Total
- Pending
- Overdue
- Pending IDC
- Pending Approval
- Revise & Resubmit
- Rejected
- Client Review
- Average Review Time
- First Pass Approval Rate
- Health

Each row must be clickable.

Clicking Mechanical:

→ Dashboard context becomes Mechanical

OR

→ MDR filtered to Mechanical

Prefer contextual drill-down rather than opening an unrelated page.

---

# 11. DISCIPLINE × STATUS HEATMAP

Create a visual heatmap:

Rows:
Disciplines

Columns:

Draft
IDC
Review
Approval
Client
Revise
Rejected
Issued
Overdue

Cell values:

Document count.

The heatmap must allow quick detection of bottlenecks.

Do not use color as the only information.

Display the numeric count inside each meaningful cell.

Clicking a cell:

→ filtered MDR

Example:

Mechanical × Pending Approval = 17

Click:

→ MDR:
Discipline = Mechanical
Status = Pending Approval

---

# 12. OVERDUE CONTROL CENTER

Create a dedicated "Engineering Exceptions" section.

Categories:

- Overdue Documents
- SLA Breach
- IDC Overdue
- Approval Overdue
- Client Response Overdue
- Comments Overdue
- Revision Overdue

Show:

- document number
- title
- discipline
- responsible person
- contractor/vendor
- current status
- due date
- aging
- next action
- severity

Sort by:

Criticality / aging / due date.

This is an ACTION LIST, not a chart.

---

# 13. TOP MANAGEMENT EXCEPTIONS

Create a "Management Attention Required" panel.

Show the most important exceptions.

Examples:

CRITICAL:
Document overdue > 30 days

CRITICAL:
Client response overdue

HIGH:
IDC pending beyond SLA

HIGH:
Repeated revision

MEDIUM:
Approval approaching SLA

Each item must have:

- reason
- owner
- age
- recommended next action
- direct navigation

---

# 14. DOCUMENT AGING

Create an aging analysis.

Buckets:

0–7 days
8–14 days
15–30 days
31–60 days
61–90 days
90+ days

Allow segmentation by:

- discipline
- status
- vendor
- contractor

Provide both:

chart
and
exact counts.

---

# 15. WORKFLOW BOTTLENECK ANALYSIS

Show where documents are accumulating.

Stages:

Draft
Submitted
IDC
Engineering Review
Approval
Client Review
Issue

For each stage show:

- count
- average age
- oldest document
- SLA breach count

The objective is to identify:

"Where is Engineering getting stuck?"

---

# 16. IDC PERFORMANCE

Create a dedicated IDC panel.

Metrics:

- Pending IDC
- Completed IDC
- Overdue IDC
- Average IDC completion time
- Outstanding disciplines
- Documents blocked by IDC
- IDC rejection/comment rate

Show:

Document
Discipline
Reviewer
Status
Due Date
Age

Click:

→ IDC filtered workspace

---

# 17. APPROVAL PIPELINE

Show:

Pending Engineering Approval
Approved
Approved With Comments
Rejected
Revise & Resubmit

Metrics:

- approval backlog
- average approval time
- rejection rate
- first-pass approval rate
- revision rate

The dashboard must clearly distinguish:

Approval workload
from
Document quality.

---

# 18. CLIENT REVIEW

Create a separate Client Review section.

Metrics:

- Submitted to Client
- Under Client Review
- Client Approved
- Client Approved With Comments
- Client Rejected
- Client Response Overdue

Show:

- average client response time
- client backlog
- overdue client responses
- client rejection rate

Do not mix Client Review with internal Engineering approval metrics.

---

# 19. REVISION / QUALITY ANALYSIS

Create a revision quality panel.

Metrics:

- First Pass Approval %
- Revision Rate
- Average Revision Count
- Documents with 2+ revisions
- Documents with 3+ revisions
- Documents with 4+ revisions
- Rejected documents
- Revise & Resubmit documents

Create an optional "High Revision Risk" list.

Example:

Document
Revision Count
Discipline
Vendor
Last Status
Owner

This should help Engineering Management identify recurring quality problems.

---

# 20. VENDOR / CONTRACTOR PERFORMANCE

Create a performance panel.

Metrics per Vendor/Contractor:

- Documents Submitted
- On-Time %
- Overdue %
- First Pass Approval %
- Revision Rate
- Rejection Rate
- Average Review Time
- Average Response Time
- Open Comments

Do NOT label a vendor "poor" based on an arbitrary threshold.

Use explicit metrics.

If a health classification is used, document the calculation.

Allow sorting.

Allow drill-down.

---

# 21. ENGINEERING TREND ANALYSIS

Provide time-series trends.

Required:

Documents Submitted
Documents Approved
Documents Rejected
Documents Issued
Documents Revised
Overdue Documents

Time granularity:

Daily
Weekly
Monthly

Default should be appropriate for project scale.

The user must be able to change the date range.

---

# 22. PLAN vs ACTUAL ENGINEERING DELIVERY

Where planned/forecast/actual document dates exist, provide:

Planned
Forecast
Actual

analysis.

Show:

- planned deliverables
- forecast delay
- actual completed
- overdue
- upcoming

Do not invent dates when data is missing.

Explicitly distinguish:

Missing data
from
Zero.

---

# 23. UPCOMING DEADLINES

Create a forward-looking section.

Show documents due:

Today
Next 3 Days
Next 7 Days
Next 14 Days
Next 30 Days

Include:

- document
- discipline
- owner
- vendor
- due date
- current status

This section is intended for proactive management.

---

# 24. MY DISCIPLINE MODE

When a user belongs to a restricted discipline:

The dashboard MUST respect the same server-side discipline scoping already used by Engineering.

The user must not receive:

- other discipline counts
- other discipline document details
- hidden data through API responses
- aggregate data that leaks restricted information

Do NOT rely on frontend filtering.

Authorization must occur server-side.

For Engineering Managers with broader permissions:

show cross-discipline information.

---

# 25. ROLE-BASED DASHBOARD

The dashboard should adapt to authorization context.

## Engineering Manager

Portfolio view.

## Discipline Lead

Own discipline view.

## Document Controller

MDR / workflow / transmittal focus.

## Project Manager

Schedule / overdue / delivery focus.

## Engineering Reviewer

My review workload.

## Executive

High-level health and exceptions.

Do not duplicate dashboards unnecessarily.

Prefer one dashboard with role-aware visibility.

---

# 26. DRILL-DOWN CONTRACT

Every major number must have a drill-down definition.

Examples:

Pending IDC
→ MDR / IDC filtered

Overdue
→ MDR / overdue filtered

Mechanical
→ Mechanical filtered

Revise & Resubmit
→ status filtered

Vendor overdue
→ vendor + overdue filtered

Client Review
→ client-review filtered

Drill-down must preserve:

Project
Discipline
Date range
Vendor
Status

context.

---

# 27. DATA CONTRACT

Do NOT expose entire EngineeringDocument entities to the dashboard.

Create purpose-built read DTOs.

Examples:

EngineeringDashboardSummaryDto

EngineeringDisciplinePerformanceDto

EngineeringHealthDto

EngineeringExceptionDto

EngineeringAgingDto

EngineeringWorkflowMetricDto

EngineeringVendorPerformanceDto

EngineeringTrendDto

Use immutable/read-only DTO patterns where appropriate.

---

# 28. QUERY ARCHITECTURE

Dashboard queries MUST be optimized.

Do NOT:

- call GetAllAsync()
- load the full MDR
- use JavaScript to calculate KPIs
- calculate thousands of records in memory
- perform N+1 queries
- duplicate business rules in controllers

Prefer:

- SQL aggregation
- grouped queries
- projections
- AsNoTracking()
- server-side filtering
- indexed columns
- bounded date ranges

Dashboard endpoints should return only required aggregates.

---

# 29. PERFORMANCE TARGET

Target:

Initial dashboard response:
≤ 2 seconds under normal project load.

Filtered dashboard:
≤ 2 seconds target.

Heavy analytics:
≤ 5 seconds acceptable if clearly indicated.

Do not sacrifice correctness to meet an artificial performance target.

If query performance is poor:

- inspect SQL
- inspect execution plan where available
- add appropriate indexes
- reduce joins
- aggregate server-side
- cache only where correctness permits

Do NOT introduce caching blindly.

---

# 30. DATABASE INDEX REVIEW

Before declaring E-05 complete, inspect indexes for dashboard filtering/aggregation.

Likely candidates include combinations involving:

- ProjectId
- DisciplineId
- Status
- IsActive
- PlannedDate
- ForecastDate
- ActualDate
- DocumentType
- VendorId
- Contractor
- ClientReviewStatus

Do not create indexes blindly.

Verify existing indexes first.

Add only justified indexes.

---

# 31. UI DESIGN LANGUAGE

The dashboard must visually communicate:

CONTROL
RISK
WORKLOAD
TREND
ACTION

Use a professional industrial/EPC visual language.

Avoid:

- consumer dashboard aesthetics
- excessive rounded cards
- excessive gradients
- decorative illustrations
- unnecessary animation
- huge typography
- excessive whitespace
- meaningless charts

Prefer:

- compact information density
- clear hierarchy
- strong alignment
- restrained visual treatment
- consistent status badges
- readable tables
- clear drill-down affordances

---

# 32. COLOR SEMANTICS

Status colors must be consistent throughout PEMS.

Recommended semantic meanings:

Healthy
Normal

Watch
Attention

At Risk
Action Required

Critical
Immediate Attention

Never communicate status through color alone.

Every status must include:

- text
- icon where appropriate
- accessible contrast

Do not introduce arbitrary colors unrelated to the existing PEMS design system.

---

# 33. RESPONSIVE DESIGN

Desktop:
Full management cockpit.

Tablet:
Reduced multi-column layout.

Mobile:
Prioritize:

- Health
- Critical exceptions
- KPIs
- overdue
- pending approval
- pending IDC

Large heatmaps may become horizontally scrollable.

Do not simply shrink the desktop dashboard.

---

# 34. RTL / BILINGUAL

Support:

Persian RTL
English LTR

All visible labels must use localization resources.

No hardcoded English labels in Razor.

Charts must correctly support RTL where applicable.

Numbers and document numbers must remain readable in both cultures.

Do not reverse document identifiers.

---

# 35. ACCESSIBILITY

Required:

- semantic headings
- keyboard navigation
- aria-label
- accessible chart alternatives
- visible focus
- sufficient contrast
- text-based status labels
- accessible filters
- accessible tables

Charts must have an accessible textual summary or equivalent data table.

---

# 36. EMPTY STATES

Each widget must handle:

No Data
No Matching Data
Loading
Error

Example:

"No overdue engineering documents for the selected filters."

NOT:

"No data."

Where appropriate provide:

Clear Filters

---

# 37. ERROR HANDLING

Dashboard failures must not break the whole page.

If one analytical widget fails:

- show widget-level error
- preserve other widgets
- log technical failure
- do not expose stack traces

Do not silently return fake zero values.

Zero means zero.

Missing means missing.

Error means error.

---

# 38. EXPORT

Where useful, provide export for management views.

Recommended:

- Engineering KPI summary
- Discipline performance
- Overdue register
- Vendor performance
- Engineering trends

Reuse existing export infrastructure where possible.

Do not build a separate export framework.

---

# 39. AUDITABILITY

Dashboard values must be reproducible.

Document metric definitions in code and/or technical documentation.

For each KPI define:

- name
- definition
- source
- filters
- calculation
- date semantics
- exclusions

Example:

OVERDUE DOCUMENTS

Definition:
Active engineering documents whose applicable due date is before the current evaluation date and whose status is not terminal.

Do not leave metric semantics ambiguous.

---

# 40. KPI DEFINITIONS

At minimum document:

### Total Active Documents
Count of active Engineering documents within filter context.

### Pending IDC
Documents with active IDC referrals not completed.

### Pending Approval
Documents currently awaiting engineering approval.

### Overdue
Documents whose applicable due date has passed and are not completed/issued/closed according to lifecycle rules.

### Revision Rate
Percentage of applicable documents requiring revision during selected period.

### First Pass Approval Rate
Percentage of submitted documents approved without requiring revision.

### Average Review Time
Average elapsed time between submission to review completion.

### Average Approval Time
Average elapsed time between approval entry and approval completion.

### Client Response Time
Elapsed time between client submission and client response.

All definitions must match actual workflow timestamps.

---

# 41. DATA QUALITY PROTECTION

Dashboard must not hide data-quality problems.

If required fields are missing:

show:

Data Quality Warning

Examples:

- Missing due date
- Missing discipline
- Missing responsible person
- Missing vendor
- Missing planned date

Do not silently classify missing values as:

0
Healthy
On Time

unless the business rule explicitly says so.

---

# 42. TESTING

Create meaningful tests.

Minimum:

### KPI tests
- total documents
- pending IDC
- overdue
- approval backlog
- client review
- revision

### Filter tests
- project
- discipline
- status
- vendor
- date range

### Authorization tests
- Engineering Manager
- Discipline Lead
- restricted discipline

### Drill-down tests
- KPI → MDR filter
- discipline → MDR
- vendor → MDR
- status → MDR

### Metric correctness
- overdue calculation
- SLA calculation
- first-pass approval
- revision rate

### Empty state
- no documents
- no overdue
- no IDC

### Error state
- repository failure
- invalid filter

---

# 43. NO FAKE DATA

Absolutely no:

- mock production KPI
- hardcoded counts
- placeholder charts presented as real
- random percentages
- invented SLA metrics

During development, mock data may only be used in isolated UI testing and MUST NOT ship as production behavior.

---

# 44. DO NOT BREAK EXISTING MODULES

Before modifying shared components inspect their consumers.

Potentially affected:

- Home Dashboard
- Engineering Documents
- Document Register
- Workflow
- IDC
- VDR
- CMMS
- Contracts
- Vendors
- Transmittals

Do not introduce regressions.

---

# 45. IMPLEMENTATION PHASES

## E-05A — Data Contract

Inspect existing domain/application data.

Define:

- dashboard DTOs
- metric definitions
- filter contract
- query contract

STOP and report.

---

## E-05B — Dashboard Query Layer

Implement server-side aggregated queries.

No UI redesign yet.

Verify:

- SQL execution
- performance
- authorization
- metric correctness

STOP and report.

---

## E-05C — Executive Cockpit

Implement:

- global filters
- executive KPIs
- health indicator
- critical exceptions

STOP and report.

---

## E-05D — Discipline / Workflow Analytics

Implement:

- discipline performance
- heatmap
- workflow bottlenecks
- IDC
- approval pipeline
- aging

STOP and report.

---

## E-05E — Vendor / Client / Revision Analytics

Implement:

- vendor performance
- contractor performance
- client review
- revision quality
- trends
- upcoming deadlines

STOP and report.

---

## E-05F — Drill-down

Implement contextual navigation from every major metric.

STOP and report.

---

## E-05G — UX / RTL / Responsive / Accessibility

Complete:

- localization
- RTL
- responsive
- accessibility
- loading
- empty
- error states

STOP and report.

---

## E-05H — QA

Run:

- build
- unit tests
- integration tests
- authorization tests
- metric tests
- query performance checks
- regression checks

STOP and report.

---

# 46. DEFINITION OF DONE

E-05 is COMPLETE only when:

## Executive
- [ ] Engineering Health exists
- [ ] KPI layer exists
- [ ] comparison/trend exists where meaningful
- [ ] critical exceptions exist

## Discipline
- [ ] Discipline performance matrix
- [ ] Discipline × Status heatmap
- [ ] drill-down

## Workflow
- [ ] IDC backlog
- [ ] approval backlog
- [ ] workflow bottleneck
- [ ] aging

## Quality
- [ ] first-pass approval
- [ ] revision rate
- [ ] rejection rate

## Client
- [ ] client review metrics
- [ ] client overdue

## Vendor
- [ ] vendor performance
- [ ] contractor performance

## Schedule
- [ ] upcoming deadlines
- [ ] planned vs forecast vs actual where data permits

## Exceptions
- [ ] overdue control center
- [ ] management attention

## Technical
- [ ] server-side aggregation
- [ ] no full MDR loading
- [ ] authorization enforced
- [ ] indexed queries reviewed
- [ ] performance verified

## UX
- [ ] RTL
- [ ] bilingual
- [ ] responsive
- [ ] accessibility
- [ ] loading
- [ ] empty
- [ ] error

## Testing
- [ ] KPI tests
- [ ] filter tests
- [ ] permission tests
- [ ] drill-down tests
- [ ] metric correctness tests
- [ ] regression tests

---

# 47. FINAL ACCEPTANCE QUESTIONS

Before reporting E-05 as COMPLETE, Claude MUST answer:

1. Can an Engineering Manager identify the worst discipline in under 10 seconds?
2. Can the manager identify overdue documents in one click?
3. Can the manager identify the current IDC bottleneck?
4. Can the manager identify approval bottlenecks?
5. Can the manager identify poor vendor performance?
6. Can the manager identify client-review delays?
7. Can every major KPI drill down to actual documents?
8. Does the dashboard respect discipline-level authorization?
9. Are KPI values generated from server-side queries?
10. Can the dashboard operate without loading the entire MDR?
11. Are all metric definitions documented?
12. Are missing data and zero values distinguished?
13. Are dashboard numbers test-covered?
14. Does the dashboard work in Persian RTL?
15. Does it remain usable on tablet/mobile?
16. Does it preserve the existing PEMS architecture?

If any answer is NO:

E-05 MUST NOT be marked COMPLETE.

---

# FINAL COMMAND TO CLAUDE

Start E-05.

FIRST perform a read-only inspection of the existing Engineering data model, services, workflow timestamps, dashboard implementation, authorization model, MDR query layer, and UI design system.

Do NOT immediately write UI code.

Produce:

1. Current-state map
2. Data availability matrix
3. KPI feasibility matrix
4. Query/API design
5. Missing-data risks
6. Index/performance assessment
7. Implementation sequence

Then begin E-05A.

Do not implement future phases prematurely.

Do not modify unrelated modules.

Do not create duplicate architecture.

Do not use fake data.

Do not report completion without tests and evidence.