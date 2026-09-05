# PEMS CMMS Architecture Standard

**Document:** `PEMS_CMMS_ARCHITECTURE.md`  
**Purpose:** Canonical architecture and implementation rules for the PEMS CMMS module  
**Audience:** Claude, PEMS developers, architects, reviewers, and maintainers  
**Status:** Engineering Baseline

---

## 1. Purpose

PEMS CMMS is an integrated maintenance and reliability platform inside PEMS. It is not a generic CRUD module.

The CMMS architecture must support:

- Asset Management
- Equipment Management
- Functional Locations
- Maintenance Strategy
- Preventive Maintenance
- Work Requests
- Work Orders
- Labor Management
- Spare Parts
- Inventory
- Failure Management
- Root Cause Analysis
- Inspection
- Condition Monitoring
- Reliability Analytics
- Maintenance Cost
- Documents and Traceability
- Workflow
- AI-assisted maintenance intelligence

All implementation decisions must preserve the existing PEMS architecture and avoid unnecessary duplication.

---

## 2. Core Architecture

The conceptual backbone is:

```text
Project
   |
Equipment
   |
   +---- Functional Location
   |
   +---- Asset
            |
            +---- Maintenance
            +---- Inspection
            +---- Failure
            +---- Cost
```

Maintenance flow:

```text
Maintenance Strategy
        |
Maintenance Plan
        |
Maintenance Tasks
        |
Work Request
        |
Work Order
        |
+-------+--------+----------+-----------+
|       |        |          |           |
Labor  Parts   Failure   Inspection   Cost
```

Reliability flow:

```text
Failure Event
      |
Failure Mode
      |
Failure Cause
      |
Failure Effect
      |
Root Cause
      |
Corrective Action
      |
Work Order
      |
Reliability Metrics
```

---

## 3. Domain-First Rule

Never implement a CMMS feature starting from the UI.

Required sequence:

```text
Business Requirement
        |
Domain Model
        |
Relationships / Constraints
        |
Application Service / Use Case
        |
Persistence
        |
Controller / Endpoint
        |
ViewModel / DTO
        |
UI
        |
Validation / Authorization / Audit
        |
Testing
```

Before creating a new entity, service, controller, or table:

1. Search the entire repository.
2. Identify existing concepts with the same or similar meaning.
3. Determine whether the requested behavior can be implemented by extending an existing concept.
4. Avoid duplicate business concepts.
5. Document architectural justification when duplication is unavoidable.

---

## 4. Equipment, Asset, and Functional Location

These are different concepts and must not be treated as interchangeable.

### Equipment

Engineering identity of the physical or logical equipment.

Typical concerns:

- Tag
- Description
- Discipline
- Manufacturer
- Model
- Serial Number
- Project
- Engineering specifications

### Asset

CMMS-maintainable identity and lifecycle.

Typical concerns:

- Asset status
- Commissioning
- Maintenance
- Inspection
- Failure
- Cost
- Lifecycle

### Functional Location

Hierarchical location in the plant or facility.

Example:

```text
Plant
  -> Area
      -> Unit
          -> System
              -> Subsystem
                  -> Location
```

Where a real `FunctionalLocation` entity exists, do not represent its relationship using uncontrolled free text.

Prefer:

```text
FunctionalLocationId
ParentFunctionalLocationId
```

and explicit relational constraints.

---

## 5. Maintenance Strategy

Maintenance Strategy should support reliability-oriented decision making.

A strategy may define:

- Maintenance type
- Failure mode
- Consequence
- Justification
- Recommended trigger
- Reliability rationale

Do not reduce strategy to a single uncontrolled string.

Where reliability classification becomes important for reporting or analytics, use controlled master data rather than arbitrary strings.

---

## 6. Maintenance Plan

A Maintenance Plan is more than a frequency.

Conceptual structure:

```text
MaintenancePlan
    |
    +---- MaintenanceTask
```

A plan may use:

- Calendar trigger
- Meter trigger
- Calendar + Meter
- Condition trigger
- Event trigger

A `MaintenanceTask` may define:

- Sequence
- Description
- Frequency
- Duration
- Skill
- Required tools
- Safety requirements
- Checklist
- Procedure
- Estimated labor
- Required spare parts
- Supporting documents

Do not create one oversized MaintenancePlan entity containing all task-level information.

---

## 7. Work Request

Work Request represents a maintenance need before execution.

Conceptual flow:

```text
Request
   |
Review / Triage
   |
Approve / Reject
   |
Work Order
```

Work Request and Work Order must not be treated as the same transaction.

A Work Request may contain:

- Requester
- Asset / Equipment
- Functional Location
- Description
- Priority
- Requested date
- Safety indication
- Attachments
- Initial classification

---

## 8. Work Order

Work Order is the primary maintenance execution transaction.

Preferred lifecycle:

```text
Draft
  |
Released
  |
Assigned
  |
InProgress
  |
Completed
  |
Reviewed
  |
Closed
```

Status transitions must be controlled.

Important transitions should capture:

- Who
- When
- Previous status
- New status
- Reason
- Comment

Do not implement an independent workflow engine inside CMMS when the existing PEMS Workflow Platform can provide the required behavior.

Work Order should connect to:

- Asset / Equipment
- Functional Location
- Maintenance Plan / Task
- Work Request
- Labor
- Spare Parts
- Failure
- Inspection
- Documents
- Cost
- Workflow
- Audit

---

## 9. Employee, Team, Crew, and Labor

Do not create a second employee master inside CMMS.

The existing PEMS Employee model remains the authoritative employee identity.

CMMS may contain:

```text
Team
Crew
TeamMember
WorkOrderLabor
```

Preferred structure:

```text
Employee
   ^
TeamMember
   ^
Team
```

and:

```text
WorkOrder
   |
WorkOrderLabor
   +---- Employee
   +---- Team
   +---- RegularHours
   +---- OvertimeHours
   +---- Rate
   +---- OvertimeRate
   +---- LaborCost
```

If legacy fields such as free-text `AssignedTo` exist, do not immediately delete them. First determine data usage and backward compatibility. They must not remain the authoritative assignment mechanism if relational assignment already exists.

---

## 10. Failure Management

Failure and maintenance execution are different concepts.

A failure is an event.

A Work Order is an action / transaction.

Preferred model:

```text
Failure Event
   |
   +---- Failure Mode
   +---- Failure Cause
   +---- Failure Effect
   +---- Root Cause
   +---- Corrective Action
             |
             v
         Work Order
```

Do not duplicate the same failure semantics independently in WorkOrder and FailureRecord without an explicit reason.

---

## 11. Failure Master Data

Reliability analytics require controlled classification.

Where appropriate, maintain master data for:

- Failure Mode
- Failure Cause
- Failure Effect
- Failure Mechanism
- Root Cause Category
- Consequence Category

Controlled classification enables:

- MTBF
- MTTR
- Failure Pareto
- Reliability analysis
- RCM
- RCA
- AI recommendations

Do not introduce master data merely for abstraction. Introduce it when the business requires consistency, reporting, reuse, or governance.

---

## 12. Maintenance History

Preferred principle:

```text
WorkOrder = authoritative maintenance transaction
```

Maintenance history should normally be derived from completed maintenance transactions.

If a separate `MaintenanceHistory` entity exists, its purpose must be explicit.

It must not become a second contradictory source of truth for:

- execution date
- technician
- duration
- action
- cost
- findings

If historical denormalization is required for reporting, document the derivation and synchronization rules.

---

## 13. Spare Parts and Inventory

Do not treat:

```text
SparePart == Inventory
```

A Spare Part is a master item.

Inventory represents stock and transactions.

Conceptual model:

```text
SparePart
    |
Inventory
    |
+---+---+---+---+
|   |   |   |   |
Receipt
Issue
Return
Adjustment
Reservation
Transfer
```

Potential inventory concepts:

- Warehouse
- Bin
- Stock Balance
- Stock Transaction
- Receipt
- Issue
- Return
- Adjustment
- Reservation
- Transfer

Do not put the complete inventory transaction model into the SparePart master.

---

## 14. Inspection and Condition Monitoring

Keep the following concepts distinct:

```text
Inspection
Condition Monitoring
Measurement
Reading
Alarm
Limit
```

Inspection may reference:

- Equipment
- Asset
- Functional Location
- ITP
- Checklist
- Inspection plan

Condition Monitoring may capture:

- Parameter
- Value
- Unit
- Timestamp
- Normal range
- Warning limit
- Alarm limit
- Measurement source

Do not force continuous condition data into a generic inspection record when a measurement model is required.

---

## 15. Reliability Analytics

The CMMS domain must support reliable calculation of:

- MTBF
- MTTR
- Availability
- Failure frequency
- Downtime
- Maintenance cost
- Preventive vs corrective ratio
- Planned vs unplanned maintenance
- Repeat failures
- Top failure modes
- Top failure causes
- Asset criticality exposure

Metrics must be derived from authoritative transactions and events.

Do not store calculated KPIs as manually editable master data.

---

## 16. Criticality

Do not confuse:

```text
Asset / Equipment Criticality
```

with:

```text
Work Order Priority
```

Criticality describes the consequence/importance of an asset.

Priority describes how urgently a specific maintenance transaction should be handled.

They influence each other but are not interchangeable.

---

## 17. Cost

Maintenance cost should be traceable to its source.

Potential cost components:

```text
Labor
Spare Parts
External Service
Equipment / Rental
Other Direct Cost
```

Cost should be traceable to:

```text
Work Order
Asset
Equipment
Project / Cost Center
```

Do not allow unexplained manual totals to become the only source of cost truth.

---

## 18. Documents

CMMS records may reference:

- Equipment documents
- Maintenance procedures
- Inspection documents
- Datasheets
- Manuals
- Drawings
- Work instructions
- Safety documents
- VDR documents
- Engineering Documents

Do not duplicate the engineering document repository inside CMMS.

Use references to the authoritative document system.

---

## 19. Workflow

CMMS workflow must integrate with the existing PEMS Workflow Platform.

Do not create:

- a second approval engine
- a second task engine
- a second notification engine
- a second state machine framework

unless the existing platform cannot satisfy the business requirement and the architectural decision is explicitly documented.

---

## 20. Search

CMMS must be compatible with the PEMS-wide search architecture.

Where applicable, support:

```text
Exact Search
Partial Search
Prefix Search
Fuzzy Search
Code Search
Tag Search
Number Search
Semantic Search
```

Search should work across:

- Equipment
- Asset
- Functional Location
- Work Request
- Work Order
- Spare Part
- Failure
- Documents
- Employees
- Teams

Do not create a CMMS-only search architecture when a shared PEMS search service already exists.

---

## 21. AI in CMMS

AI is an assistant, not the authoritative source of engineering truth.

Preferred architecture:

```text
Deterministic Data
      |
Validation
      |
AI when ambiguity / analysis exists
      |
Confidence
      |
Human Review where required
      |
Authoritative Record
```

Potential AI capabilities:

- Work Order Assistant
- Failure classification
- Failure pattern analysis
- Maintenance recommendation
- Similar failure search
- Spare part recommendation
- Procedure recommendation
- Reliability analysis
- Natural language search

AI output must not silently become authoritative engineering data.

Where appropriate record:

- Source
- Confidence
- Model
- Extraction / inference method
- Timestamp
- Reviewer
- Review status

---

## 22. Database and EF Core Rules

Before changing the Domain model:

1. Inspect `DbContext`.
2. Inspect Entity configurations.
3. Inspect relationships.
4. Inspect indexes.
5. Inspect constraints.
6. Inspect delete behavior.
7. Inspect existing migrations.
8. Inspect model snapshot.
9. Check existing data compatibility.

### Migration Required

Migration is normally required when changing:

- Entity structure
- Column
- Data type
- Relationship
- Foreign key
- Index
- Unique constraint
- Database constraint
- Table structure

### Migration Not Required

Normally no migration is required for changes limited to:

- Razor Views
- CSS
- JavaScript
- Controller logic
- Application services
- DTOs
- ViewModels
- Authorization logic

Never create a cosmetic or empty migration.

---

## 23. EF Core Relationship Rules

Review:

- Required / optional relationships
- Foreign keys
- Delete behavior
- Cascade rules
- Indexes
- Unique constraints
- Decimal precision
- Maximum lengths
- Concurrency
- Nullability

Avoid dangerous cascade deletion in critical maintenance history, audit, failure, cost, and transactional relationships.

---

## 24. UI/UX Rules

CMMS UI must be:

- Enterprise-grade
- RTL-first
- Responsive
- Accessible
- Consistent with PEMS Design System
- Persian / English compatible

A feature is not complete because its Entity and Controller exist.

The UI must include appropriate:

- Page header
- Breadcrumb/context
- Search
- Filters
- Actions
- Data grid
- Pagination
- Empty state
- Loading state
- Error state
- Validation
- Confirmation
- Authorization-aware actions

Do not hardcode direction or language in a way that breaks switching between Persian and English.

---

## 25. Routing Rules

Do not hardcode Administration or Area URLs when ASP.NET routing can generate them.

Prefer:

```text
asp-area
asp-controller
asp-action
```

or:

```text
Url.Action()
```

Verify navigation between:

```text
Admin
CMMS
VDR
Engineering Documents
```

Areas must not create URL collisions or context-dependent navigation failures.

---

## 26. Security

Every CMMS feature must be checked for:

- Authentication
- Authorization
- Roles
- Permissions
- Policies
- Resource-level access
- Auditability

Hiding a UI action is not authorization.

Backend authorization is mandatory.

Sensitive operations should be auditable.

---

## 27. Audit and Traceability

Important CMMS actions must support traceability:

```text
Who
What
When
Before
After
Reason
Source
```

Especially:

- Work Order status changes
- Assignment changes
- Failure classification
- Maintenance plan changes
- Criticality changes
- Cost changes
- Inventory transactions
- Approval decisions
- Permission changes

---

## 28. Data Integrity

Never silently overwrite engineering or maintenance history.

Use appropriate:

- Foreign keys
- Unique constraints
- Validation
- Concurrency control
- Transaction boundaries
- Audit records

Historical transactions should remain immutable where business rules require it.

Corrections should use controlled correction/reversal mechanisms rather than destructive editing.

---

## 29. Performance

CMMS list pages must use:

- Server-side pagination
- Appropriate filtering
- Indexed search fields
- Projection to DTO/ViewModel
- No unnecessary eager loading
- Async database access

Avoid loading entire equipment, work order, labor, or history tables into memory.

---

## 30. Change Control

Before implementing any change, Claude must determine:

```text
What exists?
What is missing?
What is duplicated?
What depends on it?
What will change?
Why?
Does the Domain change?
Does the database change?
Does Migration change?
Does existing data need protection?
Does UI change?
Does Workflow change?
Does Authorization change?
```

Never rewrite working code merely to make it look different.

Prefer minimal, targeted, backward-compatible changes.

---

## 31. Validation Before Completion

Before declaring a CMMS change complete:

```text
[ ] Existing functionality preserved
[ ] Domain reviewed
[ ] Relationships reviewed
[ ] Persistence reviewed
[ ] Migration decision made
[ ] No unnecessary migration
[ ] Authorization reviewed
[ ] Routing reviewed
[ ] Workflow reviewed
[ ] Audit reviewed
[ ] Search reviewed
[ ] RTL/LTR reviewed
[ ] Localization reviewed
[ ] UI states reviewed
[ ] Build passes
[ ] Relevant tests pass
[ ] Runtime behavior verified
[ ] No duplicate Entity introduced
[ ] No duplicate business logic introduced
```

---

## 32. Implementation Output

For every significant CMMS implementation, provide:

```text
IMPLEMENTED
- ...

CHANGED
- ...

NOT CHANGED
- ...

DOMAIN
- ...

DATABASE
- Migration Required: YES / NO
- Reason: ...

WORKFLOW
- ...

SECURITY
- ...

UI/UX
- ...

VALIDATION
- Build:
- Tests:
- Runtime:

RISKS
- Low / Medium / High / Critical
```

Never claim a feature is complete unless the actual codebase supports the claim.

---

## 33. Current CMMS Audit Baseline

The current PEMS CMMS audit identified the following areas requiring controlled follow-up.

### High Priority

- Clarify Asset / Equipment / Functional Location boundaries.
- Replace textual Functional Location relationships with relational references where appropriate.
- Introduce a proper Maintenance Task concept if the current model does not provide task-level execution.
- Formalize Work Order state transitions.
- Separate Failure Event from maintenance execution.
- Remove duplicated failure semantics across entities.
- Establish controlled Failure Master Data where required.
- Prevent Maintenance History from becoming a second source of truth.

### Medium Priority

- Separate Spare Part master from Inventory transactions.
- Review legacy free-text Work Order assignment fields.
- Separate Inspection from Condition Monitoring.
- Strengthen reliability analytics foundations.
- Review string-based reliability classifications.

### Existing Strengths To Preserve

- Equipment backbone
- Functional Location hierarchy
- Reliability-oriented Maintenance Strategy
- Calendar and meter-based maintenance planning
- Employee / Team separation
- WorkOrderLabor concept
- Inspection / ITP integration
- Basic Spare Part stock controls
- Deterministic document/table extraction architecture
- Existing PEMS Workflow Platform
- Existing PEMS-wide search architecture

These strengths must not be removed or duplicated during future implementation.

---

## 34. Final Principle

The CMMS must remain:

```text
Simple where possible
Explicit where necessary
Traceable by design
Relational where relationships matter
Workflow-aware
AI-assisted but not AI-dependent
Engineering-safe
Backward-compatible
Auditable
```

The goal is not to create the largest CMMS.

The goal is to create a reliable CMMS that integrates correctly with the existing PEMS Engineering Intelligence platform.
