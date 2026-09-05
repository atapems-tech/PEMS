
````markdown
# PEMS — Enterprise UI/UX Design Baseline
## Design Specification & Audit Baseline

**Project:** PEMS  
**Full Name:** Petrochemical Engineering & Maintenance Management System  
**Document Type:** Enterprise UI/UX Design Baseline  
**Version:** UIUX-BASELINE-1.0  
**Status:** Design Authority Baseline  
**Target Platform:** Web + Tablet + Mobile  
**Languages:** Persian (RTL) + English (LTR)  
**Primary Environment:** Petrochemical / Industrial Enterprise

---

# 1. Purpose

This document defines the approved UI/UX architecture and design requirements for PEMS.

The purpose is to provide a single design baseline for:

- Google Stitch
- UI/UX designers
- Claude Code
- Frontend engineers
- Product owners
- QA engineers
- Technical reviewers

The design must represent PEMS as an integrated industrial enterprise platform.

PEMS must NOT be designed as:

- A generic SaaS dashboard
- A simple CMMS
- A document management system
- A collection of disconnected modules

PEMS is an integrated ecosystem connecting:

Engineering → Documents → Vendors → Contracts → MR/MRQ → Equipment → Maintenance → Work Orders → Reliability → Knowledge → AI

---

# 2. Design Authority

The design hierarchy is:

1. PEMS Business Architecture
2. Existing PEMS Domain Model
3. Existing Backend Workflows
4. Security / Authorization
5. Data Integrity
6. Approved PEMS Design System
7. Google Stitch Visual Design
8. Frontend Implementation

Visual design must NEVER override:

- Business rules
- Workflow rules
- Authorization
- Data integrity
- Audit requirements
- Backend validation

---

# 3. Product Vision

PEMS provides one enterprise workspace for:

- Engineering
- Document Control
- Contracts
- Vendors
- Procurement
- Maintenance
- Operations
- Reliability
- Management
- AI-assisted engineering intelligence

The user should be able to move naturally between related entities.

Example:

```text
Equipment
   ↓
Engineering Document
   ↓
Vendor
   ↓
Contract
   ↓
MR / MRQ
   ↓
Revision
   ↓
Work Order
   ↓
Failure
   ↓
Maintenance History
   ↓
Reliability
````

This relationship must be visible throughout the UX.

---

# 4. UX Principles

## 4.1 Enterprise First

PEMS must prioritize:

* Information density
* Traceability
* Fast navigation
* Clear status
* Workflow visibility
* Auditability
* Decision support

## 4.2 Action Oriented

Every important screen must clearly answer:

1. What is this?
2. What is its current status?
3. Who owns it?
4. What is the priority?
5. What is the deadline?
6. Is it overdue?
7. What is the next action?

## 4.3 Progressive Disclosure

Do not overwhelm users with all information at once.

Show:

Summary → Context → Details → History → Relationships → Audit

## 4.4 Consistency

The same interaction pattern must be used across modules.

---

# 5. Primary User Roles

PEMS must support:

* Executive
* Engineering Manager
* Discipline Manager
* Senior Engineer
* Engineer
* Document Controller
* Contract Manager
* Procurement Manager
* Vendor Coordinator
* Vendor User
* Maintenance Manager
* Maintenance Engineer
* Maintenance Planner
* Maintenance Supervisor
* Maintenance Technician
* Reliability Engineer
* Warehouse / Spare Parts User
* Operations User
* HSE User
* Administrator

Each role requires:

* Relevant dashboard
* Relevant navigation
* Relevant actions
* My Work
* Notifications
* Approvals
* Permission-aware UI

---

# 6. Global Application Shell

## 6.1 Sidebar

### Home

### Engineering

* Dashboard
* Engineering Documents
* MDR
* VDR
* Disciplines
* Review Queue
* Transmittals
* Correspondence

### Contracts

* Dashboard
* Contracts
* Vendors
* Deliverables
* MR
* MRQ

### Maintenance

* Dashboard
* Assets
* Functional Locations
* Equipment
* Maintenance Strategy
* Maintenance Plans
* Work Requests
* Work Orders
* Planning
* Preventive Maintenance
* Maintenance History
* Failure Records
* Spare Parts
* Reliability

### Intelligence

* AI Assistant
* Semantic Search
* Document Intelligence
* Knowledge Base
* Traceability

### Reports

### Administration

Sidebar requirements:

* Expanded mode
* Collapsed mode
* Active state
* Nested navigation
* Favorites
* Recent pages
* Permission-aware navigation

---

# 7. Top Navigation

The global TopBar must provide:

* Global Search
* AI Search
* My Work
* Tasks
* Notifications
* Quick Create
* Language
* Help
* User Profile

---

# 8. Global Search

Search must operate across:

* Documents
* MDR
* VDR
* Contracts
* Vendors
* MR
* MRQ
* Equipment
* Assets
* Work Orders
* Maintenance History
* Correspondence

Search result must identify:

* Entity Type
* Number / Tag
* Title
* Status
* Revision
* Owner
* Date
* Relevance

---

# 9. Design System

## 9.1 Visual Direction

PEMS visual language:

* Industrial
* Technical
* Professional
* Premium
* Reliable
* Enterprise
* Modern

Avoid:

* Consumer-app styling
* Excessive gradients
* Excessive rounded cards
* Excessive animations
* Marketing-style dashboards
* Cartoon illustrations

---

# 10. Color System

Define semantic colors for:

* Primary
* Secondary
* Success
* Warning
* Danger
* Critical
* Info
* Neutral
* Disabled

Status must NEVER rely on color alone.

Every status requires:

* Label
* Visual indicator
* Icon where appropriate

---

# 11. Typography

Typography must support:

* Persian
* English
* Technical identifiers
* Equipment tags
* Document numbers
* Contract numbers

Technical identifiers should remain visually readable in RTL interfaces.

---

# 12. Core Components

Create reusable components for:

* Button
* Icon Button
* Input
* Select
* Search
* Date Picker
* Filter
* Data Table
* KPI Card
* Status Badge
* Priority Badge
* Tabs
* Modal
* Drawer
* Confirmation Dialog
* Timeline
* Workflow Stepper
* Activity Feed
* Audit Trail
* Comments
* Attachments
* File Upload
* Document Preview
* Entity Card
* Relationship Card
* Alert
* Notification
* Tooltip
* Pagination
* Empty State
* Loading State
* Error State

---

# 13. Enterprise Table Standard

All major registers must support, where backend capabilities exist:

* Search
* Server-side pagination
* Sorting
* Advanced filtering
* Column visibility
* Column ordering
* Saved views
* Bulk selection
* Bulk actions
* Export
* Row actions

Avoid loading large datasets into the browser unnecessarily.

---

# 14. Dashboard Standard

Every major module should provide:

* KPI section
* Trend section
* Work queue
* Critical alerts
* Overdue items
* Recent activity
* Drill-down navigation

KPI cards must be actionable.

---

# 15. Engineering Module

Required screens:

* Engineering Dashboard
* Document Register
* Document Search
* Document Create
* Document Edit
* Document Detail
* Document Preview
* Document Review
* Document Approval
* Document Rejection
* Revision
* Revision Comparison
* Comments
* Comment Resolution
* Transmittals
* Related Documents
* Related Equipment
* Related Contracts
* Audit Trail
* Workflow History

---

# 16. Engineering Document Detail

Required header information:

* Document Number
* Title
* Discipline
* Revision
* Status
* Project
* Vendor
* Owner
* Due Date

Required actions:

* Open
* Download
* Review
* Comment
* Submit
* Revise
* Approve
* Reject
* Freeze
* Unfreeze
* Transmit

Tabs:

* Overview
* Document
* Revision History
* Workflow
* Comments
* Transmittals
* Related Entities
* AI Intelligence
* Audit Trail

---

# 17. Document Lifecycle

UX must support:

```text
Uploaded
    ↓
AI Processing
    ↓
Classified
    ↓
Discipline Assigned
    ↓
Under Review
    ↓
Comments Open
    ↓
Revision Required
    ↓
Revise & Resubmit
    ↓
Approved
    ↓
Knowledge Base
```

The UI must never bypass server-side workflow rules.

---

# 18. MDR

Required screens:

* MDR Dashboard
* MDR Register
* MDR Detail
* MDR Import
* MDR Validation
* MDR Mapping
* MDR Exceptions
* MDR Progress
* MDR History
* MDR Reports

---

# 19. VDR

Required screens:

* VDR Dashboard
* VDR Register
* Submission Detail
* Create Submission
* Review Workspace
* Review Decision
* Comments
* Revise & Resubmit
* Approval
* Rejection
* SLA Dashboard
* Overdue Queue
* Vendor Submission History

Required fields:

* Vendor
* Document
* Revision
* Submission Date
* Review Due Date
* Working Days Remaining
* SLA Status
* Reviewer
* Workflow Status

SLA statuses:

* On Time
* Due Soon
* Overdue
* Critical

The frontend displays server-calculated SLA values.

---

# 20. Transmittals

Required screens:

* Transmittal Register
* Create Transmittal
* Transmittal Detail
* Issue
* Receive
* History
* Related Documents

---

# 21. Correspondence

Required screens:

* Inbox
* Outbox
* Correspondence Register
* Create Correspondence
* Correspondence Detail
* Attachments
* Responses
* Follow-up
* Overdue Correspondence
* History

---

# 22. Contract Management

Required screens:

* Contract Dashboard
* Contract Register
* Contract Create
* Contract Edit
* Contract Detail
* Contract Approval
* Contract Lifecycle
* Milestones
* Deliverables
* Documents
* Correspondence
* Risks
* Change Orders
* Variations
* Commercial Tracking
* Payment Milestones
* Expiry Monitoring
* Audit Trail

Contract health:

* Healthy
* Watch
* At Risk
* Critical

---

# 23. Vendor Management

Required screens:

* Vendor Dashboard
* Vendor Register
* Vendor Detail
* Vendor Profile
* Vendor Documents
* Vendor Deliverables
* Vendor Performance
* Vendor Evaluation
* Vendor Risk
* Vendor Communication
* Submission History
* Vendor KPIs

---

# 24. MR / MRQ

## MR

* Dashboard
* Register
* Create
* Detail
* Items
* Technical Requirements
* Approval
* Status
* Documents
* History

## MRQ

* Dashboard
* Register
* Create
* Detail
* Technical Evaluation
* Commercial Evaluation
* Vendor Response
* Revision
* Approval
* History

---

# 25. CMMS

CMMS must have a dedicated industrial UX.

Required domains:

* Assets
* Functional Locations
* Equipment
* Maintenance Strategy
* Maintenance Plans
* Work Requests
* Work Orders
* Planning
* Preventive Maintenance
* Maintenance History
* Failure Records
* Spare Parts
* Reliability

---

# 26. Asset Management

Required screens:

* Asset Dashboard
* Asset Register
* Asset Detail
* Asset Create
* Asset Edit
* Asset Hierarchy
* Asset History

Asset detail:

* Asset Tag
* Name
* Type
* Location
* Parent
* Criticality
* Status
* Owner
* Technical Data
* Engineering Documents
* Equipment
* Maintenance
* Work Orders
* Failures
* Costs
* Related Entities

---

# 27. Equipment Digital Passport

This is a flagship PEMS screen.

Display:

* Equipment Tag
* Equipment Name
* Functional Location
* Criticality
* Status
* Manufacturer
* Model
* Serial Number
* Health Score

Tabs:

* Overview
* Technical Data
* Engineering Documents
* Maintenance Strategy
* Maintenance Plans
* Work Orders
* Maintenance History
* Failures
* Spare Parts
* Safety
* AI Insights
* Related Entities
* Audit Trail

Key indicators:

* Health Score
* MTBF
* MTTR
* Open Work Orders
* Upcoming PM
* Failure Trend
* Critical Spare Parts

---

# 28. Maintenance Strategy

Required screens:

* Strategy Register
* Strategy Detail
* Create Strategy
* Edit Strategy
* Assignment
* Maintenance Policy
* Frequency
* Tasks
* Criticality
* Risk

---

# 29. Maintenance Plans

Required screens:

* Plan Dashboard
* Plan Register
* Plan Detail
* Create Plan
* Edit Plan
* Schedule
* Tasks
* Equipment Assignment
* Calendar
* History
* Compliance

---

# 30. Work Requests

Required screens:

* Work Request Dashboard
* Register
* Create
* Detail
* Approval
* Rejection
* Priority
* Convert to Work Order
* History

---

# 31. Work Orders

Required screens:

* Work Order Dashboard
* Register
* Create
* Edit
* Detail
* Planning
* Scheduling
* Assignment
* Execution
* Completion
* Supervisor Review
* Closure
* History
* Audit

Workflow:

```text
Requested
    ↓
Approved
    ↓
Planned
    ↓
Scheduled
    ↓
Ready
    ↓
Executing
    ↓
Completed
    ↓
Closed
```

Work Order detail:

* Equipment
* Functional Location
* Priority
* Work Type
* Planner
* Technician
* Planned Date
* Due Date
* Scope
* Safety
* Permit Prerequisites
* Isolation
* Tools
* Spare Parts
* Labor
* Attachments
* Findings
* Failure Code
* Cause
* Remedy
* Completion Notes

---

# 32. Maintenance Planning

Required screens:

* Planning Dashboard
* Planning Board
* Weekly Planning
* Monthly Planning
* Calendar
* Timeline
* Technician Workload
* Equipment Availability
* Backlog
* Priority Management

---

# 33. Preventive Maintenance

Required screens:

* PM Dashboard
* PM Calendar
* PM Plans
* Due PM
* Overdue PM
* PM Compliance
* PM History
* PM Schedule

---

# 34. Maintenance History

Required screens:

* History Dashboard
* Equipment History
* Work Order History
* Failure History
* Cost History
* Maintenance Timeline

---

# 35. Failure Management

Required screens:

* Failure Dashboard
* Failure Register
* Failure Detail
* Failure Classification
* Failure Cause
* Failure Mode
* Failure Effect
* Corrective Action
* Failure History
* Failure Trends

---

# 36. Spare Parts

Required screens:

* Spare Parts Dashboard
* Part Register
* Part Detail
* Inventory
* Stock
* Reservations
* Usage
* Low Stock
* Critical Spare Parts
* Equipment Compatibility

---

# 37. Reliability

Required screens:

* Reliability Dashboard
* MTBF
* MTTR
* Failure Frequency
* Failure Trends
* Bad Actors
* Equipment Criticality
* Reliability Ranking
* Reliability Analysis

---

# 38. AI Assistant

AI is a first-class PEMS capability.

It must NOT look like a generic chatbot.

Required:

* Chat
* Conversation History
* Context
* Search
* Source References
* Entity References
* AI Summary
* AI Insights
* AI Recommendations
* AI Explanation
* Feedback

Example:

> Which pumps have repeated failures?

The answer should expose traceable sources.

---

# 39. Semantic Search

Required:

* Search Home
* Natural Language Search
* Results
* Filters
* Entity Search
* Document Search
* Equipment Search
* Contract Search
* Work Order Search
* Relationship Search
* Search History

Filters:

* Entity
* Discipline
* Vendor
* Project
* Status
* Revision
* Date

---

# 40. Document Intelligence

Required:

* Processing Queue
* Processing Detail
* Classification
* Metadata Extraction
* AI Results
* AI Validation
* Human Review
* Knowledge Base Indexing
* Processing History

---

# 41. Knowledge Base

Required:

* Knowledge Dashboard
* Documents
* Entities
* Topics
* Search
* Relationships
* Knowledge Graph
* AI Insights

---

# 42. Traceability

Design an interactive graph connecting:

```text
Equipment
    ↓
Engineering Document
    ↓
Vendor
    ↓
Contract
    ↓
MR / MRQ
    ↓
Transmittal
    ↓
Revision
    ↓
Work Order
    ↓
Maintenance History
```

Capabilities:

* Explore
* Filter
* Expand
* Collapse
* Open Entity
* View Relationship
* View History
* Impact Analysis

If Graphify / Graphify-Out exists in the implementation, the design must accommodate the existing graph architecture.

---

# 43. My Work

Create a unified work center:

* Assigned to Me
* Awaiting My Review
* Approval Required
* Due Soon
* Overdue
* Mentioned
* System Alerts

---

# 44. Notifications

Required categories:

* Workflow
* Approval
* Overdue
* Critical
* System
* Mention
* Task

Support:

* Unread
* Read
* Notification Detail
* Bulk Mark as Read

---

# 45. Approval Center

Required:

* Approval Dashboard
* Pending Approvals
* Approved
* Rejected
* Delegated
* Approval Detail
* Approval History

---

# 46. Reporting

Required:

* Reports Dashboard
* Report Builder
* Saved Reports
* Engineering Reports
* MDR Reports
* VDR Reports
* Contract Reports
* Vendor Reports
* Maintenance Reports
* Work Order Reports
* PM Reports
* Reliability Reports
* Equipment Reports
* Executive Reports

Support:

* Export
* Print
* Filter
* Date Range
* Saved Views

---

# 47. Administration

Required:

* Administration Dashboard
* Users
* User Detail
* Create User
* Edit User
* Roles
* Role Detail
* Permission Matrix
* Organization
* Departments
* Disciplines
* Numbering Rules
* Workflow Configuration
* Status Configuration
* Notification Configuration
* System Settings
* Audit Log
* Integration Settings
* AI Configuration

---

# 48. Audit Trail

All major entities require an audit component.

Show:

* User
* Action
* Date/Time
* Previous Value
* New Value
* Revision
* Workflow Transition
* Approval
* Comment

Audit information must be easy to locate but not visually dominate the primary workflow.

---

# 49. Entity Relationship UX

Every major entity detail screen should expose related entities.

Examples:

## Equipment

Equipment
→ Documents
→ Vendors
→ Contracts
→ Work Orders
→ Failures
→ Spare Parts

## Document

Document
→ Equipment
→ Vendor
→ Contract
→ MR/MRQ
→ Transmittal
→ Revision

## Contract

Contract
→ Vendor
→ Deliverables
→ Documents
→ MR/MRQ
→ Correspondence

## Work Order

Work Order
→ Equipment
→ Functional Location
→ Maintenance Plan
→ Spare Parts
→ Failure
→ History

---

# 50. RTL / LTR

PEMS must fully support:

* Persian RTL
* English LTR

Do NOT simply apply RTL to the entire page.

Special handling is required for:

* Equipment Tags
* Document Numbers
* Contract Numbers
* MR Numbers
* MRQ Numbers
* Dates
* Numeric values
* Charts
* Tables
* Technical identifiers

---

# 51. Responsive Design

## Desktop

Primary enterprise experience.

## Tablet

Management and engineering use.

## Mobile

Field-oriented experience.

Mobile priority:

1. Work Orders
2. Equipment
3. Field Tasks
4. Approvals
5. Notifications
6. My Work
7. Search
8. AI Assistant
9. Maintenance Completion

Do not simply shrink desktop layouts.

---

# 52. Accessibility

Support:

* Keyboard navigation
* Visible focus
* Adequate contrast
* Readable typography
* Touch targets
* Screen-reader-friendly labels
* Status icons + labels
* Clear error messages

Never communicate critical state through color alone.

---

# 53. Global Page States

All important screens must define:

* Normal
* Loading
* Empty
* Error
* Unauthorized
* No Permission
* No Search Results
* Warning
* Overdue
* Critical
* Success

---

# 54. Security UX

UI must respect backend authorization.

Examples:

* Hide unauthorized actions
* Disable unavailable actions
* Explain permission restrictions
* Confirm destructive actions

However:

> Frontend permission handling is NOT a security boundary.

Backend remains authoritative.

---

# 55. Mobile Safety

For field maintenance:

* Critical actions require confirmation.
* Safety information must be highly visible.
* Status must be obvious.
* Offline behavior must be designed only if supported by backend architecture.
* Do not invent offline capabilities.

---

# 56. Design Review Gates

## Gate 1 — Information Architecture

Verify:

* Navigation
* Modules
* Entities
* User roles
* Relationships

## Gate 2 — Design System

Verify:

* Typography
* Colors
* Components
* Status system
* Tables
* Forms

## Gate 3 — Workflow UX

Verify:

* Engineering lifecycle
* VDR lifecycle
* Contract lifecycle
* Work Order lifecycle
* Approval flows

## Gate 4 — Responsive

Verify:

* Desktop
* Tablet
* Mobile

## Gate 5 — RTL/LTR

Verify:

* Persian
* English
* Mixed technical data

## Gate 6 — Enterprise UX

Verify:

* Search
* Filters
* Tables
* Audit
* Permissions
* Notifications
* My Work

## Gate 7 — Implementation Readiness

Verify that a developer can implement the design without making major UX decisions.

---

# 57. Design Coverage Matrix

Before final approval, produce:

| Module            | List | Detail | Create | Edit | Review | Approval | Workflow | History | Audit | Related | Mobile |
| ----------------- | ---- | ------ | ------ | ---- | ------ | -------- | -------- | ------- | ----- | ------- | ------ |
| Engineering       | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| Documents         | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| MDR               | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| VDR               | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| Contracts         | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| Vendors           | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| MR/MRQ            | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| Assets            | ✓    | ✓      | ✓      | ✓    | —      | —        | —        | ✓       | ✓     | ✓       | ✓      |
| Equipment         | ✓    | ✓      | ✓      | ✓    | —      | —        | ✓        | ✓       | ✓     | ✓       | ✓      |
| Maintenance Plans | ✓    | ✓      | ✓      | ✓    | —      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |
| Work Requests     | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | ✓      |
| Work Orders       | ✓    | ✓      | ✓      | ✓    | ✓      | ✓        | ✓        | ✓       | ✓     | ✓       | ✓      |
| PM                | ✓    | ✓      | ✓      | ✓    | —      | —        | ✓        | ✓       | ✓     | ✓       | ✓      |
| Failures          | ✓    | ✓      | ✓      | ✓    | —      | —        | —        | ✓       | ✓     | ✓       | —      |
| Spare Parts       | ✓    | ✓      | ✓      | ✓    | —      | —        | —        | ✓       | ✓     | ✓       | ✓      |
| Reliability       | ✓    | ✓      | —      | —    | —      | —        | —        | ✓       | ✓     | ✓       | —      |
| AI                | ✓    | ✓      | —      | —    | —      | —        | —        | ✓       | ✓     | ✓       | ✓      |
| Search            | ✓    | ✓      | —      | —    | —      | —        | —        | ✓       | —     | ✓       | ✓      |
| Knowledge Base    | ✓    | ✓      | ✓      | ✓    | —      | —        | —        | ✓       | ✓     | ✓       | —      |
| Traceability      | ✓    | ✓      | —      | —    | —      | —        | —        | ✓       | ✓     | ✓       | —      |
| Reports           | ✓    | ✓      | ✓      | ✓    | —      | —        | —        | ✓       | —     | ✓       | —      |
| Administration    | ✓    | ✓      | ✓      | ✓    | —      | ✓        | ✓        | ✓       | ✓     | ✓       | —      |

---

# 58. Stitch Deliverables

Google Stitch must produce:

1. Complete application shell
2. Complete navigation
3. Design System
4. Dashboard templates
5. List/Register templates
6. Detail templates
7. Form templates
8. Workflow templates
9. Review templates
10. Approval templates
11. Audit templates
12. Engineering screens
13. VDR/MDR screens
14. Contract/Vendor screens
15. MR/MRQ screens
16. CMMS screens
17. Equipment Digital Passport
18. Work Order workflows
19. Reliability screens
20. Spare Parts screens
21. AI Assistant
22. Semantic Search
23. Knowledge Base
24. Traceability
25. Reports
26. Administration
27. Responsive layouts
28. RTL/LTR layouts
29. Empty/loading/error states
30. Role-based UX

---

# 59. Implementation Constraint

The approved Stitch design will later be implemented into the existing PEMS repository.

The implementation team must:

* Reuse existing backend APIs
* Preserve domain logic
* Preserve workflows
* Preserve authorization
* Preserve database integrity
* Reuse existing business entities
* Avoid fake data
* Avoid duplicated business logic

The UI is NOT allowed to redefine business rules.

---

# 60. Final Acceptance Criteria

The design is accepted only when:

* All major modules are represented.
* Major entities have complete task coverage.
* Major workflows are visually defined.
* List/detail/create/edit patterns are consistent.
* Approval/review states are defined.
* Revision lifecycle is defined.
* SLA states are defined.
* Audit patterns are defined.
* Entity relationships are visible.
* Search is defined.
* Notifications are defined.
* My Work is defined.
* Responsive behavior is defined.
* RTL/LTR behavior is defined.
* Permission states are defined.
* Loading/empty/error states are defined.
* Design system is reusable.
* No major UX decision is left for the implementation team.

---

# 61. Final Product Principle

PEMS must feel like:

> One integrated petrochemical enterprise platform.

NOT:

> Engineering + CMMS + Contracts + AI as separate applications.

The user should be able to navigate naturally across:

Engineering → Documents → Vendors → Contracts → Equipment → Maintenance → Work Orders → Reliability → AI

without losing context.

---

# FINAL STATUS

This document is the PEMS UI/UX Design Baseline.

Google Stitch:

DESIGN

Claude Code:

IMPLEMENT

Backend:

REMAIN AUTHORITATIVE

Business Logic:

REMAIN AUTHORITATIVE

Security:

REMAIN SERVER-SIDE

UI:

MUST FOLLOW APPROVED DESIGN

