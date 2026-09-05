
**Project:** PEMS  
**Module:** CMMS — Computerized Maintenance Management System  
**Baseline:** CMMS Production Baseline v1.0  
**Status:** Architecture Baseline / Development Gate  
**Decision:** No uncontrolled feature development until this baseline is accepted  
**Target:** Petrochemical / Industrial Maintenance Environment


---


## 1. Purpose


This document defines the authoritative baseline for completing the PEMS CMMS module.


It converts the current architecture-gate findings into:


- Domain boundaries
- Master-data requirements
- Entity and relationship requirements
- Maintenance workflows
- Business rules
- Authorization requirements
- Reliability requirements
- Inventory integration
- Inspection and condition-monitoring requirements
- KPI requirements
- Migration requirements
- Testing and acceptance criteria


This baseline is the reference contract for implementation.


Claude Code or any other implementation agent MUST NOT introduce new CMMS entities, workflows, status transitions, numbering schemes, or business rules that conflict with this document.


---


## 2. Baseline Principles


### 2.1 No Rewrite


The existing PEMS architecture is retained.


The target is controlled hardening and completion, not a CMMS rewrite.


### 2.2 Domain First


No UI-first implementation.


The required order is:


1. Domain model
2. Business rules
3. Application services
4. Persistence
5. Authorization
6. API/controller
7. UI
8. Reporting/KPI
9. Automated tests


### 2.3 Service-Level Enforcement



The exact states may be refined during implementation, but unauthorized issuance is prohibited.

P0-03 Password Enforcement

MustChangePassword MUST be enforced on every authenticated request path where required, not only during one login path.

Required behavior:

Authenticated
    ↓
MustChangePassword?
    ├── Yes → Password Change Only
    └── No  → Normal Application
P0-04 Central Numbering

Never use:

Count() + 1

for business identifiers.

Implement a centralized, concurrency-safe Numbering/Sequence service.

Candidate document types:

WR — Work Request
WO — Work Order
MP — Maintenance Plan
SR — Store Requisition
MR — Material Request
RCA — Root Cause Analysis
INSP — Inspection
FAIL — Failure Event

The final numbering pattern is configuration, but allocation MUST be atomic.

P0-05 Work Request

Work Request MUST exist as a first-class domain concept.

The intended maintenance flow is:

Request
  ↓
Triage
  ↓
Technical Review
  ↓
Priority / Criticality Assessment
  ↓
Approval
  ↓
Planning
  ↓
Work Order

Direct creation of executable maintenance work without the required request/planning path MUST be explicitly controlled.

P0-06 Reliability Data Foundation

The data model MUST support reliable calculation of:

MTBF
MTTR
Availability
Downtime
Failure frequency
PM compliance
Schedule compliance
Planned vs unplanned maintenance

This requires trustworthy event timestamps and failure/maintenance relationships.

4. CMMS Domain Architecture

The target domain is:

Enterprise
  ↓
Plant
  ↓
Area
  ↓
Unit
  ↓
System
  ↓
Functional Location
  ↓
Asset
  ↓
Equipment

Maintenance operations then connect:

Equipment / Asset
       ↓
Work Request
       ↓
Work Order
       ↓
Maintenance Execution
       ↓
Failure / History / Cost

Planning connects:

Maintenance Strategy
       ↓
Maintenance Plan
       ↓
Job Plan
       ↓
Maintenance Tasks
       ↓
Scheduled Work
       ↓
Work Order
5. Master Data Baseline

The following master data domains MUST be explicitly governed.

5.1 Organizational / Location
Plant
Area
Unit
System
Functional Location
Maintenance Plant
Work Center
5.2 Asset
Asset
Equipment
Equipment Type
Equipment Class
Equipment Criticality
Manufacturer
Model
Serial Number
Installation Date
Commissioning Date
Status
Parent Equipment
Functional Location
5.3 Maintenance Organization
Employee
Team
Crew
Work Center
Skill
Qualification
Certification
Contractor

Employees remain in the existing Employees domain.

Do not duplicate employees inside CMMS.

5.4 Maintenance Classification
Maintenance Type
Maintenance Strategy
Priority
Criticality
Failure Mode
Failure Cause
Failure Effect
Activity Type
Work Type

Priority and Criticality MUST remain separate concepts.

Priority

How urgently work should be executed.

Criticality

How important the affected asset/equipment is to safety, production, environment, quality, or business continuity.

6. Functional Location Baseline

Functional Location MUST be a real relationship, not a free-text field.

Target:

FunctionalLocation
    1 ─── N
      Asset

Asset/Equipment MUST reference the Functional Location through a controlled relationship.

Avoid:

string FunctionalLocation

as the primary relationship.

Existing data MUST be audited before migration.

7. Asset and Equipment Baseline

Asset and Equipment responsibilities MUST be clear.

Recommended conceptual model:

Functional Location
       ↓
Asset
       ↓
Equipment

Equipment MUST support:

Identification
Classification
Criticality
Location
Parent/child hierarchy
Manufacturer
Model
Serial number
Installation/commissioning
Lifecycle status
Maintenance history
Failure history
Work history
Cost history
Documents

Criticality changes MUST be audited.

8. Work Request Baseline
8.1 Purpose

Work Request represents a maintenance need before it becomes executable work.

8.2 Minimum Data
Request Number
Title
Description
Requester
Requested Date
Asset
Equipment
Functional Location
Reported Failure/Condition
Priority
Suggested Discipline
Status
Reviewer
Review Date
Rejection Reason
Approval Information
Linked Work Order
8.3 Lifecycle
Draft
  ↓
Submitted
  ↓
UnderReview
  ↓
Approved
  ├── Rejected
  └── Planned
          ↓
      WorkOrder

Exact status names may be normalized, but illegal transitions MUST be rejected.

9. Work Order Baseline

Work Order is the executable maintenance transaction.

9.1 Minimum Responsibilities

A Work Order MUST support:

Source Work Request
Asset
Equipment
Functional Location
Work Type
Priority
Maintenance Type
Maintenance Plan/Task where applicable
Assigned Team/Crew
Assigned Employee
Planned labor
Actual labor
Planned material
Actual material
Tools
Safety requirements
Permit references where applicable
Instructions
Execution notes
Failure information
Completion information
Closure information
Cost
9.2 Lifecycle

Baseline lifecycle:

Open
  ↓
Planned
  ↓
Assigned
  ↓
Scheduled
  ↓
InProgress
  ↓
Completed
  ↓
Closed

Optional controlled states:

OnHold
Cancelled
Rejected

Every transition MUST be validated by the application/domain layer.

9.3 State Machine Rule

A transition is valid only when:

CurrentState + RequestedState + UserPermission + BusinessConditions

are all valid.

Do not rely only on Controller logic.

10. Maintenance Planning Baseline

The current MaintenancePlan concept is not sufficient as the final PM model.

Target:

Maintenance Strategy
        ↓
Maintenance Plan
        ↓
Job Plan
        ↓
Maintenance Task
        ↓
Schedule
        ↓
Work Order
10.1 Maintenance Strategy

Defines the maintenance approach.

Examples:

Corrective
Preventive
Predictive
Condition Based
Inspection Based
Run-to-Failure
10.2 Maintenance Plan

Defines recurrence and trigger.

Minimum concepts:

Strategy
Asset/Equipment scope
Frequency
Calendar
Trigger
Start Date
Next Due Date
Active/Inactive
Job Plan
10.3 Job Plan

Defines reusable execution instructions.

Minimum concepts:

Job Plan Number
Title
Scope
Estimated Duration
Required Skills
Required Labor
Required Materials
Required Tools
Safety Requirements
Procedure/Documents
10.4 Maintenance Task

Each task should represent an executable maintenance step.

Example:

Task 10 — Isolate equipment
Task 20 — Remove coupling guard
Task 30 — Inspect bearing
Task 40 — Replace bearing if required
Task 50 — Alignment
Task 60 — Test run

Do not flatten task structure into a single Description field.

11. Labor Baseline

Employee remains the authoritative personnel entity.

CMMS MUST reference:

Employee
Team
Crew
Skill
Qualification
Work Center

Do not create a second Employee table inside Maintenance.

Work Order labor MUST support:

Employee
Team/Crew
Skill
Planned Hours
Actual Hours
Start
End
Overtime where applicable
Labor Cost
12. Inventory Integration Baseline

Inventory and CMMS remain separate bounded domains but integrate transactionally.

Target:

MaterialItem
    ↓
Warehouse
    ↓
StorageLocation
    ↓
MaterialStock
    ↓
InventoryTransaction

CMMS consumes materials through controlled transactions.

12.1 Required Operations
Reserve
Release Reservation
Issue
Return
Adjust
Transfer
12.2 Quantity Rule

Never confuse:

OnHand
Reserved
Available
Issued
Returned

At minimum:

Available = OnHand - Reserved

subject to the final inventory transaction model.

12.3 Concurrency

Inventory operations MUST be concurrency-safe.

User-facing conflict handling MUST be explicit.

Example:

Requested: 100
Available after another transaction: 75
Result: Reservation rejected
Reason: Insufficient available stock
13. Failure Management Baseline

Failure data must be compatible with reliability analysis.

ISO 14224:2016 is the relevant petrochemical reliability-data reference for equipment, failure, and maintenance data. It defines categories including equipment data, failure data, and maintenance data and emphasizes standardized reliability/maintenance data collection. citeturn0search0

13.1 Failure Event

Minimum concepts:

Failure Number
Equipment
Functional Location
Failure Date/Time
Detection Date/Time
Maintenance Start
Restoration Date/Time
Failure Mode
Failure Cause
Failure Effect
Consequence
Downtime
Production Impact
Safety/Environmental Impact
Corrective Action
Linked Work Order
13.2 Failure Mode

Failure Mode must be structured/classified.

Do not rely only on free text.

13.3 Failure Cause

Failure Cause must be separated from Failure Mode.

Example:

Failure Mode:
Bearing seizure


Cause:
Lubrication failure
14. RCA Baseline

Root Cause Analysis MUST become a structured domain capability.

Minimum:

RCA
 ├── Problem Statement
 ├── Failure Event
 ├── Immediate Cause
 ├── Physical Cause
 ├── Human/Organizational Cause
 ├── Root Cause
 ├── Analysis Method
 ├── Corrective Action
 ├── Preventive Action
 ├── Responsible Person
 ├── Due Date
 ├── Verification
 └── Effectiveness

Supported methods may include:

5 Why
Fishbone
Fault Tree
Other approved methods

RCA MUST be linked to the originating failure and relevant Work Order.

15. Inspection Baseline

Inspection must support structured measurements.

Minimum:

Inspection
Inspection Type
Asset/Equipment
Inspector
Date/Time
Parameter
Actual Value
Unit
Normal Range
Warning Limit
Critical Limit
Result
Observation
Recommendation

Example:

Bearing Temperature
Actual = 87 °C
Warning = 80 °C
Critical = 90 °C
Result = Warning

Inspection findings should be able to create or escalate a Work Request.

16. Condition Monitoring Baseline

Condition Monitoring is a future-ready extension of Inspection.

Target:

Measurement
    ↓
Threshold Evaluation
    ↓
Alert
    ↓
Work Request
    ↓
Work Order

The first implementation may remain rule-based.

Predictive AI is NOT a prerequisite for CMMS production readiness.

17. I&C / Instrumentation Integration

Existing instrumentation/control concepts must not be duplicated unnecessarily.

Integration target:

Instrument
    ↓
Control Loop
    ↓
IO Point
    ↓
Alarm / Trip
    ↓
Condition / Failure
    ↓
Work Request
    ↓
Work Order

The CMMS should reference the authoritative I&C entities.

Do not create duplicate Instrument entities inside CMMS.

18. Maintenance History

Maintenance History should be derived from controlled transactions rather than manually entered duplicate records.

Primary source:

Work Order
   ↓
Completion
   ↓
Maintenance History

History should retain:

Equipment
Functional Location
Work Type
Maintenance Type
Failure
Labor
Material
Downtime
Cost
Completion
Closure
User
Dates
19. Cost Baseline

CMMS cost must be traceable.

Cost categories:

Labor Cost
Material Cost
External/Contractor Cost
Service Cost
Other Maintenance Cost

Aggregate:

Work Order Cost
    ↓
Equipment Cost
    ↓
Functional Location Cost
    ↓
Unit Cost
    ↓
Plant Cost

Do not hard-code financial assumptions inside CMMS.

20. Audit Baseline

Two audit layers are recommended.

Technical Audit

Tracks:

Field
Old Value
New Value
User
Timestamp
Entity
Entity ID
Business Audit

Tracks events such as:

Work Request Approved
Work Order Released
Work Order Completed
Work Order Closed
Reservation Created
Material Issued
Material Returned
RCA Approved
Maintenance Plan Activated
Criticality Changed

Technical and Business audit must remain distinguishable.

21. Security Baseline

All sensitive operations require explicit authorization.

Minimum roles/capabilities should be configurable for:

Requester
Maintenance Technician
Planner
Supervisor
Maintenance Manager
Reliability Engineer
Storekeeper
Warehouse Supervisor
Inspector
Instrumentation/Control Engineer
Administrator
Auditor

Role names are configuration.

Permissions are the enforcement mechanism.

22. Numbering Baseline

Create one centralized numbering service.

Example conceptual API:

GetNextNumber(DocumentType, Context)

The service must guarantee:

Atomicity
Uniqueness
Concurrency safety
Auditability
Configurable prefix
Configurable sequence
Optional year/plant segmentation

No Controller may implement independent sequence generation.

23. KPI Baseline
Maintenance KPIs

Mandatory target set:

PM Compliance
PM Overdue
Schedule Compliance
Maintenance Backlog
Backlog Hours
Planned Maintenance %
Unplanned Maintenance %
Emergency Work %
Work Order Aging
Work Order Completion Rate
Repeat Failure Rate
Reliability KPIs
MTBF
MTTR
Availability
Failure Frequency
Downtime
Production Loss where data exists
Cost KPIs
Maintenance Cost
Labor Cost
Material Cost
Contractor Cost
Cost per Asset
Cost per Equipment
Cost by Functional Location
Cost by Maintenance Type
Inventory/Maintenance KPIs
Stockout affecting maintenance
Reservation Fulfillment
Material Consumption per Work Order
Critical Spare Availability
24. Dashboard Baseline

Dashboard architecture should separate:

Operational Dashboard
Planning Dashboard
Maintenance Management Dashboard
Reliability Dashboard
Inventory/Maintenance Dashboard
Executive Dashboard

Do not overload a single dashboard with all KPIs.

25. API / Application Service Rules

Every major domain operation must have an Application Service or equivalent application boundary.

Examples:

WorkRequestService
WorkOrderService
MaintenancePlanService
MaintenanceScheduleService
FailureService
RCAService
InspectionService
InventoryReservationService
NumberingService
MaintenanceKpiService

Controllers must remain thin.

Business rules belong below the Controller.

26. EF Core / Database Rules

Required:

Foreign keys for real relationships
Unique indexes for business identifiers
Appropriate indexes for operational queries
Concurrency handling where needed
Decimal precision explicitly defined
UTC strategy or explicitly documented timezone strategy
Soft delete only where justified
Transaction boundaries for multi-step operations
Migration review before deployment

Do not use migrations as a substitute for domain design.

27. Migration Rules

Before introducing structural changes:

Inspect current schema
Inspect current migrations
Inspect existing data
Identify orphan records
Identify duplicate business identifiers
Define source-to-target mapping
Define null/default behavior
Define rollback strategy
Generate migration
Test on a copy
Validate counts and relationships
Only then deploy

Never assume an empty table unless verified.

28. Testing Baseline

Every production-critical domain operation requires automated tests.

Unit Tests

At minimum:

Work Request transitions
Work Order transitions
Priority/Criticality rules
Reservation rules
Numbering
Failure calculations
KPI calculations
Authorization policies
Integration Tests

At minimum:

Database transactions
Inventory concurrency
Work Order + Inventory
Work Request → Work Order
Maintenance Plan → Work Order
Audit logging
Authorization
Migration compatibility
Security Tests

Verify unauthorized users cannot:

Approve
Issue
Close
Delete
Override numbering
Change criticality
Approve RCA
Bypass lifecycle rules
29. Acceptance Criteria

CMMS cannot be marked Production Ready until:

Architecture
 No uncontrolled rewrite
 Domain boundaries documented
 Controllers are thin
 Business rules are service/domain enforced
Security
 Permission coverage completed
 Password enforcement complete
 Sensitive operations authorization-tested
Data Integrity
 Central numbering implemented
 Unique indexes verified
 Concurrency handling verified
 Migration reviewed
CMMS Core
 Work Request implemented
 Work Order lifecycle enforced
 Functional Location relationship finalized
 Asset/Equipment relationships finalized
Maintenance Planning
 Maintenance Strategy implemented
 Maintenance Plan finalized
 Job Plan implemented
 Maintenance Tasks implemented
 PM generation verified
Reliability
 Failure event timestamps finalized
 Failure Mode/Cause structured
 RCA implemented
 MTBF verified
 MTTR verified
 Availability verified
Inspection
 Structured inspection measurements
 Threshold evaluation
 Work Request integration
Inventory
 Reservation lifecycle
 Issue/Return lifecycle
 Concurrency behavior
 CMMS material consumption traceability
Audit
 Technical audit
 Business audit
 Critical lifecycle events auditable
KPI
 Maintenance KPI dashboard
 Reliability KPI dashboard
 Cost KPI dashboard
 Inventory/Maintenance KPI dashboard
Testing
 Unit tests
 Integration tests
 Security tests
 Migration tests
 Regression suite
30. Implementation Sequence

The implementation MUST follow this sequence.

CMMS-00 — Production Hardening
Authorization coverage
Store Requisition authorization
Password enforcement
Central numbering
Audit hardening
CMMS-01 — Asset Foundation
Functional Location
Asset
Equipment
Criticality
Master Data relationships
CMMS-02 — Maintenance Planning
Maintenance Strategy
Maintenance Plan
Job Plan
Maintenance Task
Scheduling foundation
CMMS-03 — Work Management
Work Request
Work Order lifecycle
Assignment
Scheduling
Completion
Closure
CMMS-04 — Resources
Employee integration
Team
Crew
Skill
Qualification
Labor actual/planned
CMMS-05 — Materials
Reservation
Issue
Return
Material consumption
Cost integration
CMMS-06 — Reliability
Failure
Failure Mode
Failure Cause
Failure Effect
RCA
MTBF
MTTR
Availability
CMMS-07 — Inspection
Inspection
Measurements
Thresholds
Condition Monitoring
Alert → Work Request
CMMS-08 — Cost & KPI
Labor Cost
Material Cost
Contractor Cost
Equipment Cost
Maintenance KPIs
Reliability KPIs
Management Dashboards
CMMS-09 — I&C Integration
Instrument
Alarm
Trip
Cause & Effect
Failure/Work Request/Work Order integration
31. Explicit Non-Goals

The following are NOT required for the first production baseline unless separately approved:

Full predictive AI
Autonomous maintenance decisions
Complex CQRS
Event-sourcing rewrite
Multi-tenancy
Microservices decomposition
Duplicate master-data domains
AI-generated maintenance decisions without human approval

AI may assist, recommend, classify, search, summarize, and analyze.

AI must not silently bypass CMMS authorization or lifecycle controls.

32. AI Integration Baseline

Future AI capabilities:

AI Search
AI Work Order Assistant
AI Failure Classification
AI RCA Assistant
AI Maintenance Recommendation
AI Document Intelligence
AI Reliability Analysis

AI output must be distinguishable from authoritative transactional data.

AI recommendations require human validation where they affect:

Safety
Maintenance execution
Criticality
Work Order approval
Material issuance
RCA closure
33. Definition of Done

A CMMS feature is NOT Done merely because:

Entity exists
Migration succeeds
Controller exists
Razor page works

A feature is Done only when:

Domain
+ Business Rules
+ Authorization
+ Persistence
+ Audit
+ UI
+ Validation
+ Error Handling
+ Tests
+ Migration
+ KPI/Reporting Impact
+ Documentation

are addressed.

34. Final Production Gate

The final decision must be:

                    ┌─────────────────────┐
                    │ CMMS Production Gate│
                    └──────────┬──────────┘
                               ↓
                    P0 Security / Integrity
                               ↓
                         Asset Foundation
                               ↓
                      Work Request / WO
                               ↓
                     Maintenance Planning
                               ↓
                    Reliability / Failure
                               ↓
                         Inspection
                               ↓
                         Inventory
                               ↓
                       KPI / Cost
                               ↓
                         Integration
                               ↓
                      Automated Testing
                               ↓
                     Production Readiness

Final statuses:

NOT READY
READY FOR PILOT
READY FOR PRODUCTION

No production release is permitted while a P0 item remains open.

35. Governing Rule for Implementation Agents

The implementation agent MUST:

Read this baseline before changing CMMS code.
Inspect the existing repository before creating entities.
Reuse existing authoritative entities where appropriate.
Avoid duplicate master data.
Never silently change domain semantics.
Never create a migration merely to make compilation succeed.
Never bypass authorization.
Never implement business rules only in UI/controllers.
Never use Count()+1 for business numbering.
Never mark a feature complete without tests.
Report conflicts between repository and this baseline.
Stop and request architectural approval when a change affects the baseline domain model.
36. Baseline Authority

This document is the implementation baseline for CMMS v1.

If repository code conflicts with this baseline:

Existing Code ≠ Automatically Correct

The conflict must be identified, analyzed, and resolved deliberately.

If implementation requires changing:

Entity boundaries
Core relationships
Workflows
State machines
Numbering
Authorization model
Master-data ownership

the change requires explicit architecture review.

37. Source and Scope Note

This baseline is derived primarily from the supplied CMMS Production Architecture Gate Report and the repository findings summarized in that report. The report identifies the current CMMS as not production-ready, highlights P0 blockers including authorization coverage, Store Requisition authorization, password enforcement, numbering, Work Request absence, and reliability KPI/data gaps, and recommends hardening rather than a rewrite.

The reliability-data portion is additionally aligned conceptually with ISO 14224:2016, whose scope covers standardized equipment, failure, and maintenance reliability data for petroleum, natural gas, and petrochemical operations. ISO states that the current 2016 edition remains confirmed/current. citeturn0search0

38. Version Control
Field	Value
Document	PEMS CMMS Production Baseline
Version	1.0
Status	Architecture Baseline
Owner	PEMS Architecture
Scope	CMMS
Production Gate	Mandatory
Rewrite	Not Approved
Uncontrolled Feature Development	Not Approved
Next Gate	Architecture Review / Implementation Planning

"""

path = Path("/mnt/data/PEMS_CMMS_Production_Baseline_v1.0.md")
path.write_text(content, encoding="utf-8")
print(path)