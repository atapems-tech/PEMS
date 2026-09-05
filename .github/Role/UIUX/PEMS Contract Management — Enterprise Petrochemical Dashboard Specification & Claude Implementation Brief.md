# PEMS — CONTRACT MANAGEMENT
## Enterprise Petrochemical Contract Management Dashboard
### Architecture, UX, KPI, Workflow & Implementation Specification

**Document Type:** Implementation Specification  
**System:** PEMS  
**Module:** Contract Management  
**Target Environment:** Petrochemical / Oil & Gas / EPC / Industrial Enterprise  
**Priority:** HIGH  
**Implementation Mode:** Inspect → Audit → Design → Implement → Validate

---

# 1. MISSION

Build a professional-grade **Contract Management Dashboard** for PEMS.

This must NOT be a generic CRUD dashboard.

It must function as an **Enterprise Contract Control Center** for a petrochemical organization.

The dashboard must allow management and contract professionals to understand, from one screen:

- What contracts exist?
- Which contracts are active?
- Which contracts are at risk?
- Which contracts are approaching critical dates?
- How much has been committed?
- How much has been paid?
- How much remains?
- How much has changed through Variations?
- What Claims are open?
- What Claims are financially exposed?
- Which contractors are underperforming?
- Which guarantees are expiring?
- Which insurances are expiring?
- Which contracts are delayed?
- Which obligations are overdue?
- Which disputes are active?
- Which contracts are approaching completion?
- Which contracts require management intervention?

The dashboard must support both:

**Executive decision-making**

and

**Contract administration / commercial operations.**

---

# 2. IMPORTANT ARCHITECTURAL PRINCIPLE

Contract Management must be treated as a **cross-functional control layer**, not an isolated module.

The Contract module should be connected conceptually and technically where applicable to:

```text
Contract Management
        │
        ├── Projects
        ├── Procurement
        ├── Engineering
        ├── VDR / Documents
        ├── MDR
        ├── Finance / Payments
        ├── Warehouse / Materials
        ├── CMMS
        ├── Maintenance
        ├── Planning
        ├── HSE
        ├── Quality
        ├── Permit
        ├── Risk
        └── Correspondence / Notifications
```

Do NOT duplicate data that already belongs to another domain.

Use references and relationships.

---

# 3. USERS AND MANAGEMENT VIEWPOINTS

The UI must support different perspectives.

## 3.1 CEO / Executive Management

Needs:

- Contract portfolio value
- Active contracts
- At-risk contracts
- Delayed contracts
- Major Claims
- Major Variations
- Financial exposure
- Expiring guarantees
- Expiring insurance
- Contractor performance
- Critical contractual obligations
- Major disputes
- Forecast final cost
- Contract completion forecast

The CEO should not need to understand technical contract administration to use the dashboard.

---

# 3.2 IT Manager

Needs:

- Module health
- permissions
- audit trail
- integration status
- document traceability
- workflow status
- data completeness
- notification reliability
- security events
- API/integration status

---

# 3.3 Maintenance Manager

Needs:

- Maintenance contracts
- Service contracts
- contractor performance
- SLA compliance
- work completion
- overdue contractual obligations
- spare/material responsibilities
- warranty
- defects liability
- contractor response performance
- maintenance cost exposure

---

# 3.4 Warehouse Manager

Needs:

- Contractual material obligations
- Free-issued materials
- Contractor-supplied materials
- delivery commitments
- material shortages affecting contract progress
- warehouse dependency
- material-related claims
- inventory responsibility

---

# 3.5 Control Systems / Instrumentation Management

Needs visibility into contracts involving:

- DCS
- PLC
- SIS
- ESD
- SCADA
- Instrumentation
- Calibration
- Control systems projects
- Vendor support
- Warranty
- Technical deliverables
- FAT/SAT
- commissioning obligations

---

# 3.6 Senior Maintenance Engineer

Needs:

- technical scope
- contractor obligations
- milestones
- technical deliverables
- variation requests
- claims
- defects
- warranty
- completion certificates
- outstanding obligations

---

# 3.7 Planner / Project Controls

Needs:

- baseline dates
- current forecast
- actual dates
- milestones
- delay
- critical path impact
- EOT
- progress
- cost
- variation impact
- claims impact

---

# 3.8 Production / Operations Management

Needs:

- contracts affecting production
- shutdown contracts
- operational risk
- contractor readiness
- critical equipment/service contracts
- delayed works
- outage exposure
- operational dependency

---

# 4. EXECUTIVE DASHBOARD — FIRST SCREEN

The first screen must be an executive control center.

Do NOT fill it with dozens of cards.

The first viewport must contain:

### A. Contract Portfolio Summary

KPIs:

- Total Contracts
- Active Contracts
- Expiring Soon
- At Risk
- Delayed
- Suspended
- Completed
- Terminated

---

### B. Financial Control

Show:

- Original Contract Value
- Approved Variations
- Current Contract Value
- Paid Amount
- Committed Amount
- Remaining Value
- Forecast Final Cost
- Financial Exposure

Core relationship:

```text
Current Contract Value
=
Original Contract Value
+
Approved Variations
+
Approved Adjustments
```

Never calculate financial KPIs from UI-only values.

Use authoritative backend data.

---

# 5. CONTRACT HEALTH SCORE

Every active contract should have a calculated health state.

Example:

```text
HEALTHY
WATCH
AT RISK
CRITICAL
```

Health must NOT be based on a single arbitrary score.

Consider:

- schedule
- cost
- claims
- variations
- overdue obligations
- guarantees
- insurance
- contractor performance
- quality
- HSE
- document compliance
- payment issues
- disputes

The user must be able to inspect WHY a contract received its health status.

Example:

```text
AT RISK

Reasons:
• 42 days schedule variance
• 2 overdue contractual obligations
• Performance bond expires in 18 days
• Open claim exposure: $1.8M
```

---

# 6. CONTRACT PORTFOLIO VISUALIZATION

Provide a portfolio view.

Useful dimensions:

- contract value
- health
- contractor
- project
- discipline
- contract type
- lifecycle stage
- expiration
- risk

The visualization must support drill-down.

---

# 7. CONTRACT LIFECYCLE

The system must represent the full lifecycle.

```text
Draft
   ↓
Tender / Procurement
   ↓
Award
   ↓
Contract Signed
   ↓
Notice to Proceed
   ↓
Active
   ↓
Execution
   ↓
Completion
   ↓
Defects Liability / Warranty
   ↓
Final Acceptance
   ↓
Closeout
   ↓
Archived
```

Additional states may include:

- Suspended
- Terminated
- Disputed

Do not hard-code workflow assumptions without inspecting the existing domain model.

---

# 8. CONTRACT REGISTER

Provide a high-quality enterprise table.

Minimum fields:

- Contract No.
- Contract Title
- Project
- Contractor
- Contract Type
- Contract Manager
- Original Value
- Current Value
- Paid
- Progress
- Start Date
- Original Completion Date
- Forecast Completion Date
- Days Remaining
- Delay
- Health
- Claims
- Variations
- Guarantees
- Insurance
- Status

Table capabilities:

- search
- advanced filtering
- sorting
- column selection
- saved views
- export
- bulk actions where appropriate
- drill-down
- status filtering
- contractor filtering
- project filtering
- date filtering
- financial filtering

---

# 9. CONTRACT DETAIL PAGE

Clicking a contract must open a complete contract workspace.

Recommended structure:

```text
Contract Header
│
├── Overview
├── Commercial
├── Scope
├── Schedule
├── Payments
├── Variations
├── Claims
├── Obligations
├── Milestones
├── Guarantees
├── Insurance
├── Risks
├── Contractor Performance
├── Documents
├── Correspondence
├── Meetings
├── Notices
├── Quality
├── HSE
├── Technical Deliverables
├── Defects / Warranty
├── Disputes
├── Audit Trail
└── Closeout
```

---

# 10. CONTRACT HEADER

Always show:

- Contract Number
- Title
- Contractor
- Employer / Owner
- Project
- Contract Type
- Contract Manager
- Status
- Health
- Original Value
- Current Value
- Start Date
- Completion Date
- Forecast Completion
- Remaining Days

Use a sticky or persistent header where appropriate.

---

# 11. COMMERCIAL CONTROL

The commercial section must show:

### Original Contract Value

### Approved Variations

### Pending Variations

### Claims Submitted

### Claims Approved

### Claims Rejected

### Current Contract Value

### Paid

### Outstanding

### Retention

### Advance Payment

### Advance Recovery

### Forecast Final Cost

### Financial Exposure

Include a visual bridge:

```text
Original Value
      +
Approved Variations
      +
Approved Claims
      -
Deductions
      =
Current Contract Value
```

---

# 12. PAYMENT MANAGEMENT

Provide:

- Payment Applications
- Payment Certificates
- Paid
- Pending Approval
- Pending Payment
- Retention
- Advance Payment
- Advance Recovery
- Late Payments
- Final Payment

Show aging.

Example:

```text
0–30 days
31–60 days
61–90 days
90+ days
```

Late payment must generate actionable alerts.

FIDIC contract administration explicitly covers interim payment applications, certificates, payment timing, delayed payment and retention.

---

# 13. VARIATION MANAGEMENT

This is a CORE module.

Track:

- Variation ID
- Contract
- Description
- Origin
- Cause
- Type
- Submitted Value
- Approved Value
- Time Impact
- Cost Impact
- Status
- Approval
- Related Documents
- Related Claim
- Related Change Request

Lifecycle:

```text
Identified
→ Submitted
→ Technical Review
→ Commercial Review
→ Management Approval
→ Approved / Rejected
→ Implemented
→ Closed
```

Variation must support:

- scope impact
- cost impact
- schedule impact
- risk impact

AACE explicitly treats contract change management as a formal process for identifying, assessing, reviewing, approving and incorporating scope/baseline changes.

---

# 14. CLAIMS MANAGEMENT

Claims must have their own dashboard.

Track:

- Claim ID
- Contract
- Claimant
- Claim Type
- Cause
- Contract Clause
- Notice Date
- Submission Date
- Entitlement
- Claimed Amount
- Assessed Amount
- Approved Amount
- Time Impact
- Status
- Probability
- Exposure
- Supporting Evidence
- Response Due Date

Claim lifecycle:

```text
Event
→ Notice
→ Submission
→ Entitlement Review
→ Quantum Review
→ Time Review
→ Negotiation
→ Determination
→ Approved / Rejected
→ Dispute
→ Closed
```

Claims must distinguish:

```text
Entitlement
+
Causation
+
Time Impact
+
Quantum
+
Evidence
```

FIDIC claim guidance explicitly emphasizes cause, effect, entitlement and quantum as core elements of a claim.

---

# 15. CLAIM EXPOSURE

Executive dashboard must show:

```text
Open Claim Exposure
+
Potential Exposure
+
Probable Exposure
+
Approved Claim Value
```

Do NOT treat:

```text
Claimed Amount = Financial Liability
```

The system must distinguish:

- Claimed
- Assessed
- Probable
- Approved
- Paid

---

# 16. DELAY & EOT MANAGEMENT

Dedicated section.

Track:

- Baseline completion
- Current forecast
- Actual completion
- Delay days
- Critical path impact
- Delay cause
- Responsible party
- EOT request
- EOT approved
- EOT pending
- LD exposure

EOT lifecycle:

```text
Delay Event
→ Notice
→ Analysis
→ EOT Submission
→ Review
→ Decision
→ Approved / Rejected
```

Include:

### Delay Heatmap

By:

- contract
- contractor
- project
- cause

---

# 17. LIQUIDATED DAMAGES

Where contract terms support it, track:

- LD trigger
- contractual completion date
- approved EOT
- revised completion date
- actual completion
- LD rate
- accrued LD
- maximum LD
- status

Never automatically calculate legal liability without contract-specific rules.

The UI should expose the contractual basis.

---

# 18. CONTRACTUAL OBLIGATIONS

This is one of the most important missing areas in typical contract dashboards.

Create an obligation register.

Fields:

- Obligation ID
- Contract
- Clause
- Description
- Responsible Party
- Due Date
- Status
- Completion Date
- Evidence
- Escalation Level

Views:

- Due Today
- Due This Week
- Overdue
- Upcoming
- Completed

Example:

```text
OVERDUE

Clause: 8.3
Obligation: Updated Schedule
Due: 2026-08-15
Responsible: Contractor
Status: Overdue
```

---

# 19. CONTRACTUAL NOTICE MANAGEMENT

Track:

- Notice ID
- Notice Type
- Sender
- Recipient
- Contract
- Clause
- Date
- Response Deadline
- Status
- Linked Claim
- Linked Variation
- Document

Critical notices must trigger alerts.

---

# 20. GUARANTEES / SECURITIES

Dedicated dashboard.

Track:

- Performance Bond
- Advance Payment Guarantee
- Retention Guarantee
- Warranty Guarantee
- Other Securities

Fields:

- Instrument No.
- Issuer
- Amount
- Currency
- Issue Date
- Expiry Date
- Beneficiary
- Status

Alerts:

```text
Expired
Critical < 7 days
Warning < 30 days
Upcoming < 60 days
```

Never allow an expired critical security to remain visually invisible.

FIDIC contract administration explicitly includes performance security and advance-payment security.

---

# 21. INSURANCE MANAGEMENT

Track:

- Insurance Type
- Policy Number
- Insurer
- Coverage
- Amount
- Start Date
- Expiry Date
- Status
- Required by Contract
- Document

Insurance expiry must appear in the executive risk area.

---

# 22. CONTRACTOR PERFORMANCE

Create Contractor Performance Dashboard.

Dimensions:

### Schedule

- planned progress
- actual progress
- variance

### Cost

- budget
- actual
- forecast

### Quality

- NCR
- defects
- acceptance

### HSE

- incidents
- observations
- compliance

### Contract Administration

- overdue notices
- overdue obligations
- claims
- variations
- document compliance

### Responsiveness

- average response time
- overdue responses

Create Contractor Scorecard.

---

# 23. MILESTONES

Track contractual milestones.

Fields:

- Milestone
- Baseline Date
- Current Forecast
- Actual Date
- Variance
- Payment Link
- LD Link
- Status

Provide:

### Upcoming Milestones

and

### Missed Milestones

---

# 24. RISK MANAGEMENT

Contract risks must be visible.

Track:

- Risk ID
- Description
- Category
- Probability
- Impact
- Exposure
- Owner
- Mitigation
- Due Date
- Status

Categories:

- Commercial
- Schedule
- Technical
- Procurement
- Legal
- Financial
- Contractor
- HSE
- Quality
- Operational
- Supply Chain

---

# 25. DISPUTES

Dedicated dispute section.

Track:

- Dispute ID
- Contract
- Issue
- Value
- Parties
- Stage
- Counsel
- Submission
- Response
- DAB / Adjudication
- Arbitration
- Settlement
- Status

Lifecycle:

```text
Issue
→ Notice
→ Negotiation
→ Determination
→ Adjudication / DAB
→ Arbitration
→ Settlement / Final Decision
→ Closed
```

FIDIC contract administration includes dispute adjudication, arbitration and dispute-resolution mechanisms.

---

# 26. DOCUMENT CONTROL

Contract documents must be integrated with PEMS document architecture.

Do NOT create a second disconnected document-management system.

Link:

- Contract
- Contract clauses
- Variations
- Claims
- Notices
- Payments
- Guarantees
- Insurance
- Correspondence
- Technical documents
- VDR
- MDR

Every commercial decision should be traceable to supporting evidence.

---

# 27. CONTRACT CLAUSE INTELLIGENCE

If PEMS AI capabilities support it, provide:

### Contract Clause Intelligence

User can search:

```text
"What is the contractor's notification period for delay?"
```

System should return:

- clause
- source document
- revision
- extracted requirement
- relevant deadline
- confidence
- source link

AI must never silently become the legal authority.

Always show the original source.

---

# 28. CONTRACT CALENDAR

Create a contractual calendar containing:

- milestones
- notices
- payment deadlines
- guarantee expiry
- insurance expiry
- claim deadlines
- EOT deadlines
- meetings
- obligations
- completion
- warranty expiry

Provide:

- Month
- Week
- List
- Timeline

views.

---

# 29. ALERT CENTER

Priority levels:

### CRITICAL

- expired guarantee
- expired insurance
- major contractual deadline missed
- critical delay
- major claim exposure
- termination risk

### HIGH

- upcoming guarantee expiry
- overdue obligation
- delayed payment
- overdue contractor response
- major variation awaiting decision

### MEDIUM

- approaching milestone
- upcoming insurance expiry
- pending document

Do NOT generate notification spam.

---

# 30. CONTRACT MANAGEMENT KPI PANEL

Minimum KPIs:

### Portfolio

- Total Contracts
- Active Contracts
- At Risk
- Critical
- Expiring

### Financial

- Original Value
- Current Value
- Approved Variations
- Pending Variations
- Paid
- Outstanding
- Retention
- Forecast Final Cost
- Financial Exposure

### Schedule

- On Schedule
- Delayed
- Average Delay
- EOT Pending
- EOT Approved
- LD Exposure

### Claims

- Open Claims
- Claimed Value
- Assessed Value
- Approved Value
- Potential Exposure

### Compliance

- Overdue Obligations
- Expiring Guarantees
- Expiring Insurance
- Overdue Notices

### Contractor

- Top At-Risk Contractors
- Contractor Performance Score

---

# 31. MANAGEMENT DRILL-DOWN

Every KPI must be clickable.

Example:

```text
At Risk Contracts: 7
        ↓
7 Contracts
        ↓
Contract Detail
        ↓
Reason:
• Schedule delay
• Claim exposure
• Expiring guarantee
```

Never create a KPI that leads nowhere.

---

# 32. EXECUTIVE RISK MATRIX

Show contracts on:

```text
Financial Exposure
        ↑
        │
Critical│
        │
At Risk │
        │
Watch   │
        │
Healthy └────────────────────→ Schedule Risk
```

Allow filtering by:

- project
- contractor
- contract type
- manager
- value

---

# 33. CONTRACTOR RANKING

Provide:

```text
Contractor
Contracts
Value
Schedule Performance
Quality
HSE
Claims
Variations
Compliance
Overall Health
```

Do not create a simplistic ranking based only on financial value.

---

# 34. CONTRACT TYPES

System should support configurable types such as:

- EPC
- EPCM
- Construction
- Procurement
- Engineering
- Maintenance
- Service
- Consultancy
- Supply
- Framework
- Turnaround / Shutdown
- IT / Digital
- Inspection
- Calibration
- Technical Support

Do not hard-code this list if the domain already provides configurable master data.

---

# 35. PETROCHEMICAL-SPECIFIC CONTRACT VIEWS

Provide specialized filters/views for:

### EPC Contracts

Focus on:

- Engineering
- Procurement
- Construction
- Schedule
- Cost
- Claims
- Variations
- Milestones

### Maintenance Contracts

Focus on:

- SLA
- work orders
- response time
- availability
- contractor manpower
- materials
- equipment
- performance

### Shutdown Contracts

Focus on:

- readiness
- manpower
- materials
- permits
- milestones
- critical path
- daily progress
- HSE
- completion

### Technical Vendor Contracts

Focus on:

- technical deliverables
- FAT
- SAT
- commissioning
- warranty
- spare parts
- documentation

---

# 36. RELATIONSHIP WITH CMMS

Maintenance-related contracts must link to:

```text
Contract
   ↓
Contractor
   ↓
Work Orders
   ↓
Maintenance Plan
   ↓
Asset
   ↓
Maintenance Cost
```

The Contract dashboard should be able to answer:

> How is this contract affecting maintenance execution?

---

# 37. RELATIONSHIP WITH PROCUREMENT

Contract must link to:

- procurement package
- purchase orders
- vendor
- material
- delivery
- payment
- claims

The system should identify procurement-related contract risks.

---

# 38. RELATIONSHIP WITH ENGINEERING

Contract must link to:

- Engineering Documents
- MDR
- VDR
- revisions
- technical submittals
- approvals
- comments

Engineering delays that affect contractual milestones should be traceable.

---

# 39. RELATIONSHIP WITH WAREHOUSE

Where applicable:

```text
Contract
→ Material Obligation
→ Purchase / Supply
→ Delivery
→ Warehouse
→ Issue
→ Site
```

Material delays should be distinguishable from contractor delays.

---

# 40. RELATIONSHIP WITH HSE

Where applicable, show:

- HSE compliance
- incidents
- contractor safety performance
- permit dependency
- HSE contractual obligations

HSE data should not be duplicated.

Reference authoritative HSE records.

---

# 41. ROLE-BASED DASHBOARD

Do NOT show exactly the same dashboard to everyone.

Provide configurable views:

```text
Executive View
Contract Manager View
Commercial View
Project Controls View
Maintenance View
Procurement View
Engineering View
Warehouse View
HSE View
Legal / Claims View
IT / Audit View
```

Use RBAC.

Users must only see information they are authorized to access.

---

# 42. SEARCH

Global Contract Search must support:

- Contract Number
- Contractor
- Project
- Clause
- Claim
- Variation
- Notice
- Document
- Guarantee
- Insurance
- Milestone

Search should support semantic AI search only where the underlying PEMS AI architecture supports it.

---

# 43. AUDIT TRAIL

Every significant action must be auditable.

Track:

- user
- action
- timestamp
- old value
- new value
- reason
- related entity

Important actions:

- approval
- rejection
- variation
- claim decision
- payment certification
- status change
- contract amendment
- guarantee update
- insurance update
- closeout

---

# 44. UX DESIGN RULES

The dashboard must follow the existing PEMS Petrochemical UI/UX Skill.

Mandatory:

- enterprise UX
- RTL
- bilingual
- responsive
- accessibility
- consistent design tokens
- no decorative dashboards
- no arbitrary colors
- no excessive animations
- no unnecessary modals
- no color-only status
- no fake metrics

---

# 45. VISUAL HIERARCHY

The visual priority must be:

```text
1. Critical Risk
2. Required Action
3. Financial Exposure
4. Schedule Exposure
5. Contract Health
6. Operational Status
7. Analytical Detail
8. Historical Information
```

---

# 46. FIRST SCREEN PROPOSED LAYOUT

Recommended layout:

```text
┌─────────────────────────────────────────────────────────────┐
│ CONTRACT MANAGEMENT                    Search   Filters     │
├─────────────────────────────────────────────────────────────┤
│ Contracts │ Active │ At Risk │ Delayed │ Expiring │ Claims │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ CONTRACT PORTFOLIO                    FINANCIAL CONTROL     │
│ Health / Risk Distribution            Original Value       │
│                                      Current Value         │
│                                      Paid / Outstanding   │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ CRITICAL ACTIONS                     CONTRACT PERFORMANCE  │
│ • Expiring Guarantees               Planned vs Actual     │
│ • Overdue Obligations               Delay                 │
│ • Major Claims                      Progress              │
│ • Pending Variations                                      │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ CLAIMS / VARIATIONS                  CONTRACT CALENDAR     │
│ Exposure / Status                    Milestones / Deadlines│
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ AT-RISK CONTRACTS                                           │
│ Contract | Contractor | Value | Delay | Claims | Health    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

This is a conceptual architecture, not permission to copy the ASCII layout literally.

Use the existing PEMS design system and responsive architecture.

---

# 47. SECOND SCREEN — CONTRACT WORKSPACE

Recommended:

```text
Contract Header
        ↓
Health + Financial + Schedule
        ↓
Tabs
        ↓
Overview
Commercial
Schedule
Payments
Variations
Claims
Obligations
Guarantees
Insurance
Risks
Performance
Documents
Correspondence
Technical
Quality
HSE
Disputes
Closeout
Audit
```

---

# 48. THIRD SCREEN — CONTRACT 360°

Create a Contract 360° view.

The user must see:

```text
CONTRACT
   │
   ├── Project
   ├── Contractor
   ├── People
   ├── Financials
   ├── Schedule
   ├── Variations
   ├── Claims
   ├── Obligations
   ├── Documents
   ├── Engineering
   ├── Procurement
   ├── Materials
   ├── Maintenance
   ├── HSE
   ├── Quality
   ├── Risks
   └── Disputes
```

This should become the central operational view for a contract.

---

# 49. THREE-LAYER INFORMATION ARCHITECTURE

The module must have three layers.

## Layer 1 — EXECUTIVE

"What requires management attention?"

## Layer 2 — CONTROL

"Why is this contract in this condition?"

## Layer 3 — TRANSACTION

"What exactly happened?"

Example:

```text
CEO:
Contract At Risk

      ↓

Contract Manager:
42-day delay + $1.8M claim

      ↓

Detailed:
Delay Event
→ Notice
→ EOT
→ Evidence
→ Correspondence
→ Clause
→ Decision
```

---

# 50. NO-DEAD-END UX RULE

Every important item must have a path to evidence.

Examples:

```text
KPI
 ↓
Contract
 ↓
Issue
 ↓
Source Record
 ↓
Document / Clause / Transaction
```

No dashboard metric should exist without traceability.

---

# 51. DATA INTEGRITY

Do NOT invent metrics.

Do NOT calculate authoritative financial values in frontend JavaScript if backend/domain services are responsible.

Use domain services for:

- financial calculations
- claim exposure
- contract value
- variation totals
- payment totals
- schedule status
- health calculation

Frontend is responsible for presentation.

---

# 52. PERFORMANCE

The dashboard must remain usable with:

- hundreds of contracts
- thousands of claims
- thousands of variations
- large document relationships
- large audit trails

Use:

- server-side filtering
- pagination
- aggregation
- lazy loading
- efficient queries
- indexed search
- caching where justified

Do not load the entire Contract database into the browser.

---

# 53. MOBILE

Mobile should prioritize:

1. Critical alerts
2. Contract health
3. Obligations
4. Approvals
5. Claims
6. Variations
7. Milestones
8. Documents

Do not reproduce the entire desktop dashboard on mobile.

---

# 54. RESPONSIVE

Required:

- Desktop
- Laptop
- Tablet
- Mobile

Desktop is the primary Contract Management workstation.

Mobile is an operational/approval companion.

---

# 55. ACCESSIBILITY

Target WCAG 2.2 AA principles.

Status must never depend exclusively on color.

Keyboard navigation must work.

Tables must remain accessible.

Dialogs must be accessible.

Charts must have meaningful alternatives.

---

# 56. AUDIT — PASS/FAIL CRITERIA

Before declaring the module complete, Claude must verify:

## Executive

- [ ] Portfolio visible
- [ ] Financial exposure visible
- [ ] Schedule exposure visible
- [ ] Claims visible
- [ ] Variations visible
- [ ] At-risk contracts visible
- [ ] Critical deadlines visible

## Commercial

- [ ] Contract value
- [ ] Variations
- [ ] Claims
- [ ] Payments
- [ ] Retention
- [ ] Advance payment
- [ ] Forecast final cost

## Schedule

- [ ] Milestones
- [ ] Delay
- [ ] EOT
- [ ] LD

## Compliance

- [ ] Obligations
- [ ] Notices
- [ ] Guarantees
- [ ] Insurance

## Contractor

- [ ] Performance
- [ ] Quality
- [ ] HSE
- [ ] Schedule
- [ ] Claims

## Legal / Disputes

- [ ] Claims
- [ ] Disputes
- [ ] Evidence
- [ ] Clause references

## Integration

- [ ] Engineering
- [ ] VDR
- [ ] MDR
- [ ] Procurement
- [ ] Warehouse
- [ ] CMMS
- [ ] Maintenance
- [ ] HSE
- [ ] Documents

## UX

- [ ] RTL
- [ ] Bilingual
- [ ] Responsive
- [ ] Accessible
- [ ] Design system compliant
- [ ] No arbitrary colors
- [ ] No dead-end KPIs
- [ ] No fake data

---

# 57. THREE-PASS MANDATORY AUDIT

Before implementation:

### PASS 1 — BUSINESS AUDIT

Review from the perspective of:

- CEO
- Contract Manager
- Project Controls
- Maintenance
- Procurement
- Engineering
- Warehouse
- Operations
- HSE

Identify missing business capabilities.

---

### PASS 2 — CONTRACT / COMMERCIAL AUDIT

Review:

- Contract lifecycle
- Scope
- Obligations
- Notices
- Variations
- Claims
- Payments
- Securities
- Insurance
- Schedule
- EOT
- LD
- Risk
- Disputes
- Closeout

Identify missing contract-management capabilities.

---

### PASS 3 — SOFTWARE / UX AUDIT

Review:

- Architecture
- Domain boundaries
- Data integrity
- API usage
- Query performance
- RBAC
- Auditability
- RTL
- Accessibility
- Responsive behavior
- Design system
- Mobile UX
- Error handling
- Empty states
- Loading states
- Failure states

Do not declare completion until all three audits pass.

---

# 58. IMPLEMENTATION RULE

Before writing code:

1. Inspect the current PEMS Contract Management implementation.
2. Inspect Domain entities.
3. Inspect Application services.
4. Inspect repositories.
5. Inspect controllers/API endpoints.
6. Inspect existing views/components.
7. Inspect database relationships.
8. Inspect existing design system.
9. Identify what already exists.
10. Reuse existing architecture.

Do NOT rebuild existing functionality unnecessarily.

Do NOT create duplicate entities.

Do NOT create duplicate document systems.

Do NOT introduce CQRS or unnecessary architectural complexity.

Do NOT introduce multi-tenancy.

Do NOT modify unrelated modules.

---

# 59. DATABASE RULE

Do not modify the database schema simply to satisfy dashboard visuals.

If missing data is discovered:

1. identify the missing domain capability
2. determine whether it already exists elsewhere
3. reuse existing entity
4. only propose schema changes when genuinely required

Never create UI-only fake fields for missing business data.

---

# 60. DESIGN QUALITY BAR

The final result must look and behave like an enterprise system suitable for a major petrochemical company.

Target characteristics:

- calm
- precise
- dense but readable
- professional
- operational
- trustworthy
- traceable
- consistent
- responsive
- bilingual
- audit-ready

Avoid:

- marketing-dashboard aesthetics
- excessive gradients
- giant colorful cards
- unnecessary animations
- excessive whitespace
- consumer-app patterns
- meaningless charts

---

# 61. FINAL DELIVERABLE

Claude must implement the Contract Management Dashboard in the existing PEMS architecture.

Do NOT only provide a design description.

Deliver:

1. Dashboard
2. Contract Register
3. Contract 360°
4. Contract Detail
5. Commercial Control
6. Payment Control
7. Variation Management
8. Claims Management
9. Delay / EOT
10. Obligations
11. Guarantees
12. Insurance
13. Contractor Performance
14. Risks
15. Disputes
16. Contract Calendar
17. Alerts
18. Document integration
19. Audit trail
20. Role-aware views

---

# 62. FINAL ACCEPTANCE TEST

The module is NOT complete merely because the page renders.

Claude must demonstrate:

### Scenario 1

CEO opens Contract Management.

Can they identify the highest-risk contracts within 30 seconds?

### Scenario 2

Contract Manager opens a contract.

Can they understand:

- value
- schedule
- claims
- variations
- obligations
- guarantees
- insurance
- risks?

### Scenario 3

A variation is pending.

Can the user understand:

- why
- value
- time impact
- current approval stage
- responsible person
- evidence?

### Scenario 4

A claim is open.

Can the user distinguish:

- claimed
- assessed
- probable
- approved
- paid?

### Scenario 5

A performance guarantee expires in 18 days.

Does the system clearly surface the issue?

### Scenario 6

A contractor is 42 days late.

Can the user trace:

```text
Delay
→ Cause
→ Notice
→ EOT
→ Contract Clause
→ Claim
→ Financial Impact
→ Decision
```

### Scenario 7

A maintenance contract is underperforming.

Can the Maintenance Manager trace:

```text
Contract
→ Contractor
→ SLA
→ Work Orders
→ Asset
→ Maintenance Performance
→ Cost
```

---

# 63. FINAL REPORT FORMAT

After implementation report exactly:

```text
PEMS CONTRACT MANAGEMENT DASHBOARD
==================================

Implementation: COMPLETE / PARTIAL

Executive Dashboard: PASS / FAIL
Contract Register: PASS / FAIL
Contract 360: PASS / FAIL
Commercial Control: PASS / FAIL
Payments: PASS / FAIL
Variations: PASS / FAIL
Claims: PASS / FAIL
Delay/EOT: PASS / FAIL
Obligations: PASS / FAIL
Guarantees: PASS / FAIL
Insurance: PASS / FAIL
Contractor Performance: PASS / FAIL
Risk: PASS / FAIL
Disputes: PASS / FAIL
Document Integration: PASS / FAIL
CMMS Integration: PASS / FAIL
Engineering Integration: PASS / FAIL
Procurement Integration: PASS / FAIL
Warehouse Integration: PASS / FAIL
RTL: PASS / FAIL
Responsive: PASS / FAIL
Accessibility: PASS / FAIL
Performance: PASS / FAIL
Auditability: PASS / FAIL

Business Audit: PASS / FAIL
Contract Audit: PASS / FAIL
Software/UX Audit: PASS / FAIL

Critical Findings:
[list]

Remaining Risks:
[list]

Files Modified:
[list]

Files Created:
[list]

Database Changes:
YES / NO

Unrelated Code Modified:
YES / NO
```

# FINAL INSTRUCTION TO CLAUDE

Do not treat this document as a suggestion.

Treat it as the **Contract Management UX/Architecture Acceptance Specification for PEMS**.

Inspect the existing implementation first.

Then perform the three mandatory audits.

Then implement the highest-quality solution that fits the existing PEMS architecture.

Do not bypass existing domain rules.

Do not invent business data.

Do not create duplicate systems.

Do not stop at visual implementation.

The final result must be a real **Petrochemical Contract Management Control Center**, not merely a dashboard UI.

**Inspect → Audit 1 → Audit 2 → Audit 3 → Design → Implement → Test → Validate → Report.**