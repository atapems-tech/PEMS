# PEMS — CMMS-10a Execution Baseline
## Core Permit Lifecycle

**Status:** APPROVED FOR IMPLEMENTATION  
**Phase:** CMMS-10a  
**Project:** PEMS — Plant Engineering & Maintenance System  
**Implementation Agent:** Claude  
**Authority:** PEMS Architecture / Engineering Review  
**Rule:** No implementation beyond CMMS-10a is authorized by this document.

---

# 1. Objective

Implement **CMMS-10a — Core Permit Lifecycle** as the first controlled sub-phase of the Permit-to-Work system.

CMMS-10 is intentionally divided into seven independently controlled phases:

```text
CMMS-10a  Core Permit Lifecycle
CMMS-10b  Approval & Authorization
CMMS-10c  LOTO / Isolation
CMMS-10d  Gas Testing
CMMS-10e  SIMOPS & Shift Handover
CMMS-10f  Execution Gate & Emergency
CMMS-10g  KPI / Dashboard / Audit
```

Only **CMMS-10a** is authorized for implementation at this stage.

Do not implement 10b–10g unless explicitly authorized.

---

# 2. Mandatory Architecture Decisions

The following decisions are FINAL for CMMS-10a.

## ADR-01 — Personnel Model

Contractor personnel must be represented through the existing `Employee` model.

Do **not** create a parallel `ContractorEmployee`, `PermitPerson`, or equivalent personnel entity.

Extend `Employee` with:

```text
PersonnelType
VendorId (nullable FK)
```

Recommended personnel types:

```text
Internal
Contractor
External
Temporary
```

The implementation may use an Enum or controlled Lookup consistent with the existing PEMS architecture.

`VendorId` is nullable because internal personnel do not belong to a Vendor.

Target model:

```text
Employee
 ├── EmployeeId
 ├── EmployeeNo
 ├── Name
 ├── PersonnelType
 ├── VendorId?
 ├── Active
 └── existing Employee properties

Vendor
 └── VendorId
```

Relationship:

```text
Employee.VendorId → Vendor.VendorId
```

This model must support future Permit roles such as:

```text
Permit Issuer
Permit Receiver
Gas Tester
Isolation Authority
Authorized Person
Workflow Actor
```

Do not duplicate personnel information inside Permit entities.

---

# 3. ADR-02 — Authorization Model

The existing permission model is insufficient for Permit-to-Work because Permit operations are not limited to:

```text
View
Create
Edit
Delete
Approve
```

Permit requires operational actions such as:

```text
Issue
Suspend
Resume
Revoke
VerifyIsolation
Approve
Reject
Close
EmergencyOverride
```

Therefore, do NOT retrofit the existing CRUD-only permission model.

Implement a generic Role/Module/Action authorization foundation.

Logical model:

```text
Role
  │
  └── RoleModuleAction
        ├── RoleId
        ├── Module
        ├── Action
        └── Scope
```

The authorization model must be generic enough for future modules.

Initial modules may include:

```text
Permit
LOTO
WorkOrder
Inspection
Document
```

Initial Permit actions must be designed so they can support:

```text
View
Create
Edit
Issue
Suspend
Resume
Revoke
VerifyIsolation
Approve
Reject
Close
EmergencyOverride
```

The implementation must not hardcode these actions into individual controllers or UI components.

Authorization must eventually be enforceable at the application/service layer.

---

# 4. Scope of CMMS-10a

CMMS-10a is ONLY the **Core Permit Lifecycle**.

The objective is to establish a clean Permit domain and its basic state machine.

The minimum lifecycle must support:

```text
Draft
Submitted
UnderReview
Approved
Issued
Active
Suspended
Closed
Cancelled
Revoked
```

The exact state names may follow existing PEMS conventions if equivalent states already exist.

Do not introduce duplicate state concepts.

---

# 5. Permit Domain

Before creating entities, inspect the existing repository thoroughly.

Required inspection:

```text
Domain Entities
DbContext
Configurations
Enums
Repositories
Services
Controllers
ViewModels / DTOs
Authorization
Workflow
Audit
User / Employee / Vendor
Existing CMMS entities
Existing migrations
Existing database schema
```

Do not assume that an entity does not exist.

Search the repository first.

If an existing entity partially represents the required concept, evaluate whether it should be extended rather than duplicated.

---

# 6. Permit Core Entity

The Permit entity must represent the Permit-to-Work master record.

At minimum, evaluate support for:

```text
PermitId
PermitNumber
PermitType
Title
Description
Location / FunctionalLocation
WorkOrderId (nullable)
RequestedBy
PermitIssuer
PermitReceiver
Status
Priority
ValidFrom
ValidTo
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
ClosedAt
ClosedBy
CancellationReason
RevocationReason
```

Use existing PEMS entities wherever appropriate.

For example:

```text
FunctionalLocation
Equipment
WorkOrder
Employee
Vendor
User
```

must not be duplicated merely for Permit purposes.

---

# 7. Permit Types

The design must support controlled Permit Types.

Initial types should be capable of supporting:

```text
Cold Work
Hot Work
Confined Space
Electrical
Excavation
Working at Height
Radiography
Line Breaking
Other
```

Do not over-engineer specialized Permit entities during CMMS-10a.

Specialized requirements will be addressed in later phases.

---

# 8. Permit ↔ Work Order

Permit must be capable of linking to an existing Work Order.

Relationship:

```text
WorkOrder
    │
    └── Permit
```

The relationship should support:

- Permit associated with Work Order
- Permit independent of Work Order where operationally valid
- Multiple permits associated with one Work Order if the domain requires it

Do not redesign the complete WorkOrder subsystem during CMMS-10a.

If an architectural conflict is discovered, document it and stop before making unrelated changes.

---

# 9. Permit Personnel

Permit participants must reference `Employee`.

Examples:

```text
PermitIssuerEmployeeId
PermitReceiverEmployeeId
```

Do not store names as the source of truth.

The UI may display:

```text
Employee Name
Employee Number
Personnel Type
Vendor
Competency Status
```

but the database must reference the Employee entity.

Contractor personnel must work through:

```text
Employee.PersonnelType = Contractor
Employee.VendorId = Vendor.Id
```

---

# 10. Competency Foundation

CMMS-10a does NOT need the complete competency management system.

However, the Permit model must not prevent future competency validation.

Therefore:

```text
Permit
   ↓
Employee
   ↓
Future Competency / Qualification subsystem
```

Do not create fake competency records merely to satisfy the current phase.

If the existing PEMS competency model exists, integrate with it.

If it does not exist, document the integration point for a later phase.

---

# 11. State Machine

Permit lifecycle must be implemented as a controlled state machine.

Do NOT allow arbitrary status updates such as:

```csharp
permit.Status = "Issued";
```

from unrelated controllers.

Transitions must be explicit.

Example:

```text
Draft
  ↓ Submit
Submitted
  ↓ Review
UnderReview
  ↓ Approve
Approved
  ↓ Issue
Issued
  ↓ Activate
Active
  ↓ Suspend
Suspended
  ↓ Resume
Active
  ↓ Close
Closed
```

Exceptional transitions:

```text
Draft → Cancelled
Submitted → Cancelled
UnderReview → Rejected
Approved → Revoked
Issued → Revoked
Active → Revoked
```

Actual transitions must be validated against the approved domain rules.

---

# 12. Auditability

Every meaningful Permit state transition must be auditable.

At minimum capture:

```text
PermitId
FromStatus
ToStatus
Action
PerformedBy
PerformedAt
Reason
```

Do not rely only on:

```text
UpdatedAt
UpdatedBy
```

because they do not provide a state-transition history.

Reuse the existing PEMS audit infrastructure if available.

Do not create a second competing audit framework.

---

# 13. Numbering

Permit numbers must follow the existing PEMS numbering architecture.

Do not create an isolated numbering implementation if the project already has a generic numbering service.

The system must support:

```text
Automatic numbering
Manual override
```

subject to existing PEMS rules and authorization.

Numbers must be unique.

---

# 14. Authorization Boundary

CMMS-10a must establish the authorization integration point but must not implement the complete authorization program planned for CMMS-10b.

The code must be structured so that actions can later be protected by:

```text
Module = Permit
Action = <specific action>
```

Example:

```text
Permit.Create
Permit.Edit
Permit.Submit
Permit.Approve
Permit.Issue
Permit.Suspend
Permit.Resume
Permit.Close
Permit.Revoke
```

Do not scatter role checks throughout controllers.

Prefer centralized authorization through the application/service layer.

---

# 15. Database Rules

Before migration:

1. Inspect existing schema.
2. Inspect existing migrations.
3. Inspect FK conventions.
4. Inspect naming conventions.
5. Inspect indexes.
6. Inspect delete behaviors.
7. Inspect audit conventions.
8. Inspect seed-data conventions.

Follow the existing PEMS database architecture.

Do not introduce unnecessary architectural patterns.

Do not add:

```text
CQRS
MediatR
Microservices
Multi-tenancy
Event sourcing
```

unless already part of the existing architecture and required by the current implementation.

---

# 16. UI Requirements

CMMS-10a UI must provide the minimum operational lifecycle.

Required capabilities:

### Permit List

Display:

```text
Permit Number
Type
Title
Location
Work Order
Status
Issuer
Receiver
Valid From
Valid To
```

Support appropriate filtering.

### Permit Creation

Provide:

```text
Permit Type
Title
Description
Location
Equipment / Functional Location
Work Order
Requested By
Permit Receiver
Validity
Priority
```

### Permit Detail

Display:

```text
Core information
Personnel
Status
Validity
Linked Work Order
Audit / transition history
Available actions
```

### Lifecycle Actions

Actions must depend on current state and authorization.

Do not display actions that are invalid for the current state.

---

# 17. Validation

Minimum validation must include:

```text
Required Permit Type
Required Title
Required Location where applicable
Valid date range
Valid personnel references
Valid Work Order reference where supplied
No invalid state transition
No duplicate Permit Number
```

Do not allow:

```text
ValidTo < ValidFrom
Closed permit → Active
Cancelled permit → Issued
Revoked permit → Active
```

unless an explicitly defined business rule permits it.

---

# 18. Security

Never trust UI state to enforce authorization.

The following must be enforced server-side:

```text
Create
Edit
Submit
Approve
Issue
Suspend
Resume
Close
Cancel
Revoke
```

A hidden UI button is NOT an authorization mechanism.

---

# 19. Testing Requirements

CMMS-10a is not complete when the code compiles.

Required testing:

## Domain Tests

Test valid and invalid state transitions.

## Service Tests

Test:

```text
Create
Submit
Approve
Issue
Suspend
Resume
Close
Cancel
Revoke
```

## Authorization Tests

At minimum verify that unauthorized users cannot execute protected actions.

## Database Tests

Verify:

```text
FK integrity
Unique Permit Number
Nullable VendorId
Employee → Vendor relationship
Permit → Employee relationships
Permit → WorkOrder relationship
```

## UI Tests

Verify:

```text
Create Permit
View Permit
Filter Permit
Execute lifecycle actions
Display invalid actions correctly
Display audit history
```

---

# 20. Live-Test Gate

Before declaring CMMS-10a complete, perform an end-to-end scenario.

### Scenario

```text
1. Create Employee
2. Mark Employee as Contractor
3. Assign Vendor
4. Create Work Order
5. Create Permit
6. Assign Contractor as Permit Receiver
7. Submit Permit
8. Review Permit
9. Approve Permit
10. Issue Permit
11. Activate Permit
12. Suspend Permit
13. Resume Permit
14. Close Permit
15. Verify complete audit trail
```

The complete scenario must work against the real application/database environment.

---

# 21. Definition of Done

CMMS-10a is DONE only when all conditions below are satisfied:

- [ ] Existing repository architecture fully inspected
- [ ] Employee personnel model extended
- [ ] Contractor personnel supported
- [ ] Employee → Vendor relationship implemented
- [ ] Generic authorization integration point established
- [ ] Permit domain implemented
- [ ] Permit lifecycle implemented as a state machine
- [ ] Permit ↔ Employee relationships implemented
- [ ] Permit ↔ WorkOrder relationship implemented
- [ ] Permit numbering implemented
- [ ] Audit trail implemented/reused
- [ ] Server-side validation implemented
- [ ] Server-side authorization boundary established
- [ ] Permit UI implemented
- [ ] Database migration created
- [ ] Migration successfully applied
- [ ] Existing functionality regression-tested
- [ ] Unit/service tests pass
- [ ] End-to-end lifecycle test passes
- [ ] Contractor personnel scenario passes
- [ ] Audit trail verified
- [ ] No duplicate architecture introduced
- [ ] No CMMS-10b through CMMS-10g functionality implemented

---

# 22. Mandatory Stop Conditions

Claude MUST STOP and report before coding if any of the following is discovered:

```text
Existing Employee model conflicts with ADR-01
Existing Vendor model cannot support the required relationship
Existing authorization architecture conflicts with ADR-02
Existing Permit entity already exists with incompatible semantics
Existing workflow/state-machine infrastructure conflicts with this design
Existing audit framework conflicts with the required transition audit
Existing WorkOrder relationship creates a major domain contradiction
Database migration would require destructive changes
Existing production data would be endangered
```

Do not silently work around architectural conflicts.

Report:

```text
Finding
Impact
Options
Recommended Decision
```

and wait for approval.

---

# 23. Scope Protection

The following are explicitly OUT OF SCOPE for CMMS-10a:

```text
CMMS-10b Approval Engine completion
CMMS-10c LOTO
CMMS-10d Gas Testing
CMMS-10e SIMOPS
CMMS-10e Shift Handover
CMMS-10f Execution Gate
CMMS-10f Emergency Override implementation
CMMS-10g KPI dashboards
CMMS-10g advanced analytics
Full competency management
Full training management
Advanced isolation management
Predictive maintenance
```

Do not implement these capabilities under the name of "preparation" unless the required foundation is genuinely necessary for CMMS-10a.

---

# 24. Required Working Method

Execute CMMS-10a using the established PEMS phase discipline:

```text
PLAN
  ↓
SCOPE
  ↓
ARCHITECTURE CHECK
  ↓
IMPLEMENT
  ↓
MIGRATE
  ↓
TEST
  ↓
LIVE TEST
  ↓
AUDIT
  ↓
CLOSE
```

Do not jump directly into coding.

---

# 25. First Action — PLAN MODE ONLY

Your first response after receiving this document must NOT modify the repository.

Perform a repository audit and produce:

```text
CMMS-10a PLAN
```

including:

1. Existing relevant entities
2. Existing Employee/Vendor structure
3. Existing authorization structure
4. Existing workflow/state-machine infrastructure
5. Existing audit infrastructure
6. Existing WorkOrder relationships
7. Existing migration state
8. Files/modules requiring modification
9. New entities required
10. Existing entities requiring modification
11. Migration impact
12. Test plan
13. Risks
14. Exact implementation sequence

Then explicitly state:

```text
PLAN READY — AWAITING IMPLEMENTATION AUTHORIZATION
```

Do NOT start implementation until the plan has been produced and reviewed.

---

# 26. Architectural Principle

The objective is not merely to "add a Permit screen".

The objective is to establish a **production-grade Permit-to-Work domain foundation** that can safely evolve through:

```text
CMMS-10a
    ↓
CMMS-10b
    ↓
CMMS-10c
    ↓
CMMS-10d
    ↓
CMMS-10e
    ↓
CMMS-10f
    ↓
CMMS-10g
```

without requiring destructive redesign of:

```text
Employee
Vendor
Authorization
Workflow
Permit
WorkOrder
Audit
```

The implementation must therefore prioritize:

**Domain integrity → Security → State control → Auditability → Extensibility → UI**

over rapid feature accumulation.

---

# FINAL DIRECTIVE

**Start CMMS-10a PLAN MODE ONLY.**

**Do not modify code.**

**Do not create migrations.**

**Do not change the database.**

**Do not implement CMMS-10b through CMMS-10g.**

First inspect the repository and return the complete CMMS-10a implementation plan.

After plan review and explicit authorization, proceed to implementation.