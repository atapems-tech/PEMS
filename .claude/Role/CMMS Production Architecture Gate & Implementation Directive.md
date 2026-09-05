
# PEMS — CMMS Production Architecture Gate & Implementation Directive

**Repository:** `saberahmadi48-collab/PEMS-cmms-edit-empoleeys`

**Role:** Senior Software Architect + Enterprise CMMS Architect + Petrochemical Maintenance Systems Architect + IT Architecture Reviewer

---

# 1. PRIMARY OBJECTIVE

Audit the current PEMS repository and evaluate its CMMS architecture against the requirements of an enterprise-grade petrochemical maintenance management system.

The audit must be performed from six perspectives:

1. Senior Software Architecture
2. Petrochemical Executive Management
3. IT Management
4. Maintenance & Reliability Management
5. Warehouse / Materials Management
6. Instrumentation, Control & Automation Management

The objective is **not** to rewrite the CMMS.

The objective is to:

- Preserve good existing architecture
- Identify architectural weaknesses
- Identify missing capabilities
- Identify duplicate domains
- Identify data integrity risks
- Identify performance risks
- Identify production blockers
- Define a controlled implementation roadmap

---

# 2. CRITICAL RULE — AUDIT FIRST

## During Phase 0–43:

**DO NOT MODIFY THE REPOSITORY.**

Do not:

- Modify code
- Modify entities
- Modify controllers
- Modify services
- Modify database schema
- Create migrations
- Apply migrations
- Delete data
- Rename tables
- Rename columns
- Create new domain entities
- Refactor existing code

The first stage is **READ-ONLY ARCHITECTURE AUDIT**.

After the audit report is completed:

> STOP and wait for explicit approval before implementing changes.

---

# 3. REPOSITORY AUDIT

Audit the complete repository.

Inspect at minimum:

```text
PEMS.Domain
PEMS.Application
PEMS.Persistence
PEMS.Web
Controllers
Services
Interfaces
Entities
Entity Configurations
DbContext
Migrations
Seed Data
Views
JavaScript
CSS
Authorization
Permissions
AI Services
Engineering Modules
CMMS Modules
Inventory Modules
Document Modules
````

Also inspect:

```text
README
Architecture Documents
Master Plans
Claude Instructions
Existing Audits
Migration History
EF Core Model Snapshot
```

Do not rely only on documentation.

The source code and database model are the primary source of truth.

---

# 4. DATABASE AUDIT

Extract and analyze:

* All entities
* All DbSets
* All primary keys
* All foreign keys
* All unique constraints
* All indexes
* All delete behaviors
* All nullable/required fields
* All decimal precision definitions
* All date/time fields
* All status fields
* All type fields
* All string-based enumerations
* All migrations
* All seed data
* All navigation properties

Produce:

| Entity | Table | PK | FK | Unique | Indexes | Risk | Recommendation |
| ------ | ----- | -- | -- | ------ | ------- | ---- | -------------- |

---

# 5. DUPLICATE DOMAIN AUDIT

Before proposing any new entity, search the entire repository for semantic equivalents.

Examples:

```text
Employee
Staff
Personnel
Worker
Technician
Labor
```

```text
Team
Crew
WorkGroup
MaintenanceTeam
```

```text
Skill
Competency
Capability
```

```text
Qualification
Certification
Competency
```

```text
Equipment
Asset
Machine
```

```text
Material
MaterialItem
SparePart
Part
```

If an equivalent concept already exists, do not create another entity automatically.

Every gap must be classified as:

```text
KEEP
EXTEND
REFACTOR
NEW
```

---

# 6. TARGET CMMS ARCHITECTURE

The target CMMS architecture should contain the following domains:

```text
CMMS
│
├── Asset Management
├── Functional Location
├── Work Request
├── Maintenance Strategy
├── Maintenance Plan
├── Maintenance Task
├── Job Plan
├── Planning
├── Scheduling
├── Work Order
├── Labor
├── Team
├── Crew
├── Skills
├── Qualifications
├── Certifications
├── Materials
├── Inventory
├── Failure Management
├── Reliability
├── Root Cause Analysis
├── Inspection
├── Condition Monitoring
├── Shutdown / Turnaround
├── Cost
└── KPI / Management Intelligence
```

This is a target architecture, not an instruction to create every entity immediately.

First determine which capabilities already exist.

---

# 7. ASSET MANAGEMENT

The target asset hierarchy is:

```text
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
```

Audit the existing relationship between:

```text
Asset
FunctionalLocation
Equipment
```

If `Asset.FunctionalLocation` is stored as free text while a canonical `FunctionalLocation` entity already exists, identify this as an architectural issue.

Target concept:

```text
Asset.FunctionalLocationId
        ↓
FunctionalLocation
```

Do not create or execute a migration until existing data has been analyzed.

Report:

* Existing row count
* Null references
* Invalid references
* Duplicates
* Migration risk
* Recommended migration strategy

---

# 8. WORK REQUEST

Determine whether a proper `WorkRequest` domain exists.

If not, classify it as a **P0 gap**.

Target conceptual model:

```text
WorkRequest
│
├── RequestNumber
├── RequestedBy
├── RequestedDate
├── Source
├── Description
├── Priority
├── FunctionalLocation
├── Asset
├── Equipment
├── FailureSymptom
├── Status
├── Planner
├── ScreeningResult
└── WorkOrderId
```

Target lifecycle:

```text
New
 ↓
Screening
 ↓
Approved
 ↓
Planned
 ↓
ConvertedToWorkOrder
```

Alternative states:

```text
Rejected
Cancelled
Duplicate
```

The architecture should normally prevent operators from directly creating Work Orders unless explicitly authorized by business rules.

---

# 9. MAINTENANCE STRATEGY

Target relationship:

```text
Asset
 ↓
MaintenanceStrategy
 ↓
MaintenancePlan
 ↓
MaintenanceTask
 ↓
JobPlan
```

Audit the current implementation.

Determine whether Maintenance Plans are capable of defining real maintenance work rather than only scheduling intervals.

---

# 10. MAINTENANCE TASK

Determine whether a real `MaintenanceTask` concept exists.

Target model:

```text
MaintenanceTask
│
├── TaskCode
├── Name
├── Description
├── Sequence
├── EstimatedLaborHours
├── RequiredSkill
├── RequiredMaterial
├── SafetyRequirement
├── MeasurementRequirement
└── Instructions
```

Do not create it automatically.

First search for equivalent existing concepts.

---

# 11. JOB PLAN

A Job Plan should conceptually support:

```text
JobPlan
 ↓
Tasks
 ↓
Labor
 ↓
Materials
 ↓
Tools
 ↓
Safety Requirements
 ↓
Inspection Requirements
 ↓
Measurements
```

Determine how much of this capability currently exists.

---

# 12. WORK ORDER STATE MACHINE

The Work Order lifecycle must be enforced at Application/Domain level.

Target lifecycle:

```text
Draft
 ↓
Open
 ↓
Assigned
 ↓
Planned
 ↓
Scheduled
 ↓
InProgress
 ↓
Completed
 ↓
Closed
```

Alternative states:

```text
OnHold
Cancelled
Rejected
```

Invalid transitions must be prevented.

Examples:

```text
Open → Closed
Open → Completed
Closed → InProgress
Cancelled → InProgress
```

must not be possible unless an explicit business rule allows them.

## Critical architectural rule

Controllers must not own the Work Order state machine.

Controllers should be thin:

```text
Controller
    ↓
Application Command / Service
    ↓
Domain/Application Rules
    ↓
Persistence
```

---

# 13. WORK ORDER EXECUTION

Audit whether Work Orders can correctly associate:

```text
Asset
FunctionalLocation
Equipment
MaintenancePlan
MaintenanceTask
JobPlan
Team
Crew
ResponsibleEmployee
Labor
RequiredSkill
Materials
Tools
Failure
Measurements
Inspection
Downtime
Cost
Documents
```

Report missing or incorrectly modeled relationships.

---

# 14. LABOR MANAGEMENT

The existing PEMS Employee Master must remain the authoritative employee source.

Do NOT create a second CMMS employee system.

Target:

```text
Employee
 ├── TeamMember
 ├── CrewMember
 ├── EmployeeSkill
 ├── EmployeeQualification
 ├── EmployeeCertification
 └── WorkOrderLabor
```

Audit whether the current implementation follows this principle.

---

# 15. SKILLS, QUALIFICATIONS AND CERTIFICATIONS

These concepts must remain distinct.

```text
Skill
=
Capability
```

```text
Qualification
=
Competency / Eligibility
```

```text
Certification
=
Formal Certification
```

Example:

```text
Skill:
Pump Alignment

Qualification:
Pump Maintenance Technician

Certification:
Certificate #12345
```

Expired certifications must not satisfy requirements where certification is mandatory.

---

# 16. WORKFORCE ASSIGNMENT

Assignment should not be based only on Team membership.

The target decision chain is:

```text
WorkOrder
 ↓
RequiredSkill
 ↓
CandidateEmployee
 ↓
Skill Validation
 ↓
Qualification Validation
 ↓
Certification Validation
 ↓
Availability
 ↓
Current Workload
 ↓
Assignment
```

AI may recommend candidates.

AI must not bypass:

* Authorization
* Qualification
* Certification
* Safety requirements

---

# 17. INVENTORY ARCHITECTURE

Target model:

```text
MaterialItem
 ↓
Warehouse
 ↓
StorageLocation
 ↓
MaterialStock
```

and:

```text
InventoryTransaction
MaterialReservation
StoreRequisition
WorkOrderMaterial
```

Maintain the conceptual distinction:

```text
Reserved
≠
Issued
```

Reservation:

```text
ReservedQuantity ↑
OnHandQuantity unchanged
```

Issue:

```text
ReservedQuantity ↓
OnHandQuantity ↓
```

---

# 18. INVENTORY CONCURRENCY — P0

Audit inventory operations for race conditions.

Example:

```text
Available = 100

User A → Reserve 80
User B → Reserve 70
```

The system must never allow:

```text
Reserved = 150
```

when only 100 units are available.

The final architecture must use appropriate:

* Database transactions
* Concurrency control
* Atomic updates
* Database constraints
* Transaction boundaries

Determine the correct implementation for the existing EF Core architecture.

---

# 19. INVENTORY LEDGER

Inventory transactions should be treated as an immutable ledger.

Allowed:

```text
INSERT
```

Not allowed:

```text
UPDATE
DELETE
```

Corrections must be represented by new correction transactions.

---

# 20. SERIALIZED AND BATCH MATERIAL

Audit support for:

```text
IsSerialized
IsBatchControlled
```

If these flags exist, determine whether the operational model actually supports:

```text
MaterialSerial
MaterialBatch
```

Do not automatically create new entities.

First determine whether equivalent functionality already exists.

---

# 21. FAILURE MANAGEMENT

Failure management must distinguish:

```text
FailureOccurred
FailureDetected
MaintenanceStarted
FailureRestored
```

These timestamps must not be collapsed into a single completion timestamp.

Target conceptual chain:

```text
Failure
 ↓
FailureMode
 ↓
FailureMechanism
 ↓
FailureCause
 ↓
FailureEffect
 ↓
RootCause
 ↓
CorrectiveAction
```

Audit the existing model against this structure.

---

# 22. RELIABILITY ENGINEERING

The CMMS should eventually support:

```text
MTBF
MTTR
Availability
Failure Rate
Repeat Failure Rate
Downtime
Maintenance Cost
```

Target:

```text
Equipment
 ↓
Failure History
 ↓
Reliability Analysis
```

Identify all missing capabilities.

---

# 23. BAD ACTOR ANALYSIS

The system should eventually identify bad actors using:

```text
Failure Frequency
Downtime
Maintenance Cost
Repeat Failure
Criticality
```

Conceptual model:

```text
Equipment
 ↓
Failure Frequency
 ↓
Downtime
 ↓
Maintenance Cost
 ↓
Repeat Failure
 ↓
Bad Actor Score
```

---

# 24. ROOT CAUSE ANALYSIS

Critical failures should support:

```text
Failure
 ↓
RCA
 ↓
Root Cause
 ↓
Corrective Action
 ↓
Preventive Action
 ↓
Verification
```

Audit current RCA capability.

---

# 25. INSPECTION

Inspection must be capable of connecting to:

```text
Asset
Equipment
FunctionalLocation
WorkOrder
Failure
Measurement
Document
```

Target:

```text
InspectionPlan
 ↓
InspectionTask
 ↓
Measurement
 ↓
Reading
 ↓
Limit
 ↓
Condition
 ↓
Alert
 ↓
WorkRequest
```

---

# 26. CONDITION MONITORING

The architecture should support future condition monitoring for:

```text
Vibration
Temperature
Pressure
Flow
Current
Lubrication
```

and instrumentation/control systems:

```text
Calibration
Signal Quality
Drift
Alarm
Loop Performance
```

Target flow:

```text
Condition Monitoring
 ↓
Anomaly
 ↓
WorkRequest
```

Do not implement predictive maintenance prematurely.

First ensure the architecture can support it.

---

# 27. ENGINEERING / I&C INTEGRATION

PEMS already contains Engineering-related concepts.

Audit all existing:

```text
Engineering Equipment
Instrument
ControlLoop
IOPoint
DCS
SIS
Alarm
CauseEffect
```

Avoid duplicate master data.

Target conceptual relationship:

```text
Engineering Equipment
        ↓
Instrument
        ↓
ControlLoop
        ↓
IOPoint
        ↓
DCS / SIS
        ↓
Alarm
        ↓
Failure / Anomaly
        ↓
WorkRequest
        ↓
WorkOrder
```

The CMMS must consume authoritative Engineering Master Data rather than recreate it.

---

# 28. MAINTENANCE COST

The target cost model should support:

```text
WorkOrder Cost
=
Labor
+
Material
+
Contractor
+
External Service
+
Other Cost
```

and reporting by:

```text
Asset
Equipment
Maintenance Group
Plant
```

Audit whether current cost data is sufficient.

---

# 29. KPI MODEL

The CMMS should eventually provide:

```text
PM Compliance
PM Overdue
Schedule Compliance
Backlog
Backlog Hours
MTBF
MTTR
Availability
Downtime
Maintenance Cost
Material Consumption
Emergency Work Orders
Repeat Failures
Critical Asset Failures
```

Audit existing KPI implementation.

---

# 30. EXECUTIVE MANAGEMENT

The Executive layer should eventually answer:

```text
Plant Health
Maintenance Cost
Critical Failures
Downtime
Backlog
PM Compliance
Top Bad Actors
Critical Equipment
Material Risk
```

Evaluate the current system from the perspective of a Petrochemical CEO.

---

# 31. PERFORMANCE AUDIT

Search the entire codebase for inefficient query patterns.

Pay particular attention to:

```text
ToListAsync()
```

followed by:

```text
Where()
Select()
Count()
Sum()
```

performed in memory.

Bad:

```text
Load entire table
 ↓
Process in application memory
```

Preferred:

```text
SQL filtering
SQL aggregation
SQL projection
```

Audit especially:

```text
WorkOrders
InventoryTransactions
MaintenanceHistory
FailureRecords
MaterialStocks
Assets
Employees
```

---

# 32. PAGINATION

Large datasets must support:

```text
Pagination
Filtering
Sorting
Search
Projection
```

At minimum audit:

```text
WorkOrders
InventoryTransactions
MaintenanceHistory
FailureRecords
MaterialStocks
Employees
Assets
```

---

# 33. DOCUMENT NUMBERING

Do not use:

```text
Count() + 1
```

for business document numbering.

Use a concurrency-safe approach such as:

```text
Database Sequence
```

or:

```text
Concurrency-safe Numbering Service
```

Audit:

```text
WO numbers
WR numbers
MR numbers
SR numbers
Inventory transaction numbers
```

---

# 34. DATE AND TIME

Audit all use of:

```text
DateTime.Now
DateTime.UtcNow
```

Evaluate whether the system should use:

```text
DateTimeOffset
```

and/or:

```text
TimeProvider / IClock
```

for testability and consistent audit behavior.

---

# 35. STRING-BASED STATUS AND TYPE VALUES

Audit string values such as:

```text
Open
Closed
Completed
InProgress
Preventive
Corrective
Emergency
```

Determine whether they should be:

```text
Strongly Typed Enums
```

or:

```text
Master Data
```

Do not change them automatically.

Provide an architectural recommendation.

---

# 36. SECURITY AND AUTHORIZATION

Audit:

```text
Authentication
Authorization
Roles
Permissions
Approvals
Planner
Supervisor
Technician
Storekeeper
Reliability Engineer
Inspector
Administrator
```

Organizational membership must not automatically equal authorization.

For example:

```text
Mechanical
Electrical
Instrumentation
```

must not automatically grant administrative permissions.

---

# 37. APPROVAL GOVERNANCE

Audit authorization for:

```text
Work Request Approval
Work Order Approval
Material Request Approval
Critical Work Approval
Work Order Closure
Inventory Adjustment
Inventory Issue
```

Sensitive operations must have explicit authorization.

---

# 38. AUDIT TRAIL

Critical changes should record:

```text
Who
What
When
From
To
Reason
```

At minimum:

```text
Status
Priority
Criticality
Approval
Quantity
Reservation
Issue
Cost
Assignment
```

---

# 39. MIGRATION SAFETY

Before any future schema change, the implementation must verify:

```text
Existing Row Count
FK Integrity
Orphan Records
Duplicates
Null Values
Unique Constraints
Seed Dependencies
Existing Migrations
```

No destructive migration is allowed without a data preservation plan.

Never:

```text
DROP TABLE
DROP COLUMN
DELETE DATA
```

without explicit approval and a validated migration strategy.

---

# 40. TESTING REQUIREMENTS

After implementation, validate:

```text
dotnet build
```

Then validate:

```text
Migration Generation
Migration Application
Database Schema
Foreign Keys
Indexes
Constraints
```

Functional regression must cover:

```text
Login
Authorization
CMMS Navigation
Asset
Functional Location
Work Request
Work Order
Assignment
Labor
Materials
Reservation
Issue
Failure
History
Inventory
```

---

# 41. REGRESSION SCOPE

CMMS changes must not break:

```text
Engineering
MDR
VDR
Documents
AI
Letters
Administration
Employees
Assets
Inventory
```

---

# 42. UI / UX AUDIT

Audit CMMS UI for:

```text
RTL
LTR
Persian
English
Responsive Design
Search
Filtering
Pagination
Loading States
Empty States
Error States
Validation
Modal Behavior
Navigation
Authorization
```

The UI must reflect actual domain state.

Do not create UI actions for unauthorized state transitions.

---

# 43. CONTROLLER ARCHITECTURE

Controllers should remain thin.

Avoid:

```text
Controller
 ├── Business Rules
 ├── Database Queries
 ├── Workflow
 ├── Calculations
 └── Persistence
```

Target:

```text
Controller
 ↓
Application Command / Service
 ↓
Domain / Application Rules
 ↓
Persistence
```

Identify violations.

Do not refactor them during the audit phase.

---

# 44. AI GOVERNANCE

AI may:

```text
Recommend
Classify
Predict
Summarize
Detect Anomalies
Suggest Skills
Suggest Failure Modes
Suggest RCA
Suggest Work Orders
```

AI must not independently perform authoritative operations such as:

```text
Approve
Close
Issue Material
Change Criticality
Override Certification
Authorize Personnel
```

without explicit human authorization.

Target:

```text
AI
 ↓
Recommendation
 ↓
Human Review
 ↓
Authorization
 ↓
Authoritative Record
```

---

# 45. ARCHITECTURAL DECISION FRAMEWORK

Every identified gap must be classified as exactly one of:

## KEEP

Existing implementation is architecturally sound.

## EXTEND

Existing model is correct but incomplete.

## REFACTOR

Existing concept is correct but implementation is architecturally weak.

## NEW

A genuinely new domain capability is required.

Never use `NEW` simply because the current implementation is inconvenient.

---

# 46. REQUIRED AUDIT REPORT

Do not modify the repository during the audit.

Produce a complete report with exactly this structure:

```text
1. Executive Summary

2. Current System Architecture

3. Current CMMS Architecture

4. Database Audit

5. Entity Audit

6. Duplicate Domain Audit

7. Asset Management Audit

8. Functional Location Audit

9. Work Request Audit

10. Maintenance Strategy Audit

11. Maintenance Plan Audit

12. Maintenance Task Audit

13. Job Plan Audit

14. Planning & Scheduling Audit

15. Work Order Audit

16. Labor / Workforce Audit

17. Skills / Qualifications / Certifications Audit

18. Materials & Inventory Audit

19. Failure Management Audit

20. Reliability Engineering Audit

21. RCA Audit

22. Inspection Audit

23. Condition Monitoring Audit

24. Engineering / I&C Integration Audit

25. Cost Audit

26. KPI Audit

27. Security / Authorization Audit

28. Audit Trail Audit

29. Performance Audit

30. Migration Risk Audit

31. UI / UX Audit

32. AI Governance Audit

33. Production Readiness Assessment

34. P0 Issues

35. P1 Issues

36. P2 Issues

37. KEEP / EXTEND / REFACTOR / NEW Matrix

38. Target Architecture

39. Migration Strategy

40. Implementation Roadmap

41. Test Strategy

42. Final Recommendation
```

---

# 47. GAP MATRIX

For every identified issue, produce:

| ID | Domain | Current State | Problem | Severity | Decision | Proposed Solution | Migration Risk | Test |
| -- | ------ | ------------- | ------- | -------- | -------- | ----------------- | -------------- | ---- |

Severity must be one of:

```text
P0 = Production Blocker
P1 = Major Architectural / Functional Gap
P2 = Important Improvement
P3 = Enhancement
```

---

# 48. PRODUCTION READINESS SCORE

Provide scores from 0–10 for:

```text
Architecture
Database
Asset Management
Work Management
Maintenance Planning
Work Order Execution
Labor Management
Inventory
Reliability
Inspection
I&C Integration
Security
Performance
Auditability
UI/UX
AI Governance
Testing
Production Readiness
```

Provide a final overall score.

---

# 49. IMPLEMENTATION ROADMAP

After completing the audit, propose the implementation order:

```text
CMMS-01
Asset / Functional Location

↓

CMMS-02
Work Request

↓

CMMS-03
Maintenance Strategy / Plan / Task / Job Plan

↓

CMMS-04
Planning / Scheduling

↓

CMMS-05
Work Order State Machine / Execution

↓

CMMS-06
Labor / Skills / Qualifications

↓

CMMS-07
Materials / Inventory Integration

↓

CMMS-08
Failure / Reliability / RCA

↓

CMMS-09
Inspection / Condition Monitoring

↓

CMMS-10
Cost / KPI / Management Intelligence

↓

CMMS-11
Engineering / I&C Integration

↓

CMMS-12
AI / Engineering Intelligence
```

This is a roadmap, not permission to implement everything immediately.

---

# 50. NON-NEGOTIABLE ARCHITECTURAL RULES

The following rules are mandatory:

1. Do not rewrite the entire CMMS.
2. Do not create a duplicate Employee system.
3. Do not create duplicate Equipment Master Data.
4. Do not create duplicate Material Master Data.
5. Do not create duplicate Discipline Master Data.
6. Do not bypass existing PEMS Master Data.
7. Do not perform destructive migrations.
8. Do not put business rules in Controllers.
9. Do not allow AI to perform authoritative actions without authorization.
10. Do not use `Count() + 1` for business document numbering.
11. Do not load large tables into memory for KPI calculations.
12. Do not allow concurrent inventory operations to corrupt stock.
13. Do not treat Reserved as Issued.
14. Do not treat Failure Completion Date as Failure Occurrence Date.
15. Do not store canonical Functional Location relationships as free text when a proper entity exists.
16. Do not create a new entity before searching for semantic equivalents.
17. Do not modify the database during Phase 0.
18. Every schema change requires migration and data-preservation analysis.
19. Every change requires regression testing.
20. Production readiness must be explicitly assessed.
21. Do not implement features merely to increase entity count.
22. Prefer domain integrity over UI convenience.
23. Prefer database integrity over application-only validation.
24. Prefer transactional operations for inventory and financial data.
25. Preserve existing working functionality unless there is a documented architectural reason to change it.

---

# 51. FINAL EXECUTION COMMAND

## Execute ONLY the audit phase now.

Perform:

```text
Full Repository Audit
+
Database Audit
+
Entity Audit
+
Migration Audit
+
Duplicate Domain Audit
+
CMMS Gap Analysis
+
Performance Audit
+
Security Audit
+
Production Readiness Audit
```

Then produce:

```text
PEMS CMMS PRODUCTION ARCHITECTURE GATE REPORT
```

using the required structure above.

## IMPORTANT

At this stage:

```text
NO CODE CHANGES
NO DATABASE CHANGES
NO MIGRATIONS
NO ENTITY CREATION
NO DELETIONS
NO RENAMES
NO REFACTORING
```

After the report is complete:

> **STOP. Do not implement anything. Wait for explicit approval of the architecture and implementation roadmap.**

```
```
