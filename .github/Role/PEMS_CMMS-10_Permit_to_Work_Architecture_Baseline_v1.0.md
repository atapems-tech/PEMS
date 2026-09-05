# PEMS CMMS-10
## Permit to Work, Isolation & Operational Control
### Architecture & Functional Baseline v1.0

**Project:** PEMS – Petrochemical Engineering & Maintenance System  
**Module:** CMMS  
**Phase:** CMMS-10  
**Status:** Architecture / Implementation Baseline  
**Priority:** Critical  
**Target Environment:** Petrochemical / Process Plant

---

## 1. Phase Objective

CMMS-10 establishes the controlled execution layer for maintenance and operational work that requires formal authorization, safety controls, isolation, and coordination with Operations and Production.

This phase is **not** a simple Permit form.

The system shall determine:

- What work is being performed
- Where it is being performed
- On which Asset / Functional Location
- By whom
- Under which operating conditions
- With which hazards
- Which permits are required
- Which isolations are required
- Which safety controls are mandatory
- Who must approve the work
- When the work is authorized
- When the permit must be suspended, resumed, extended, or closed

Target lifecycle:

```text
Work Identification
        ↓
Work Planning
        ↓
Risk Assessment
        ↓
Permit Assessment
        ↓
Isolation / LOTO
        ↓
Permit Approval
        ↓
Permit Issuance
        ↓
Work Execution
        ↓
Monitoring / Shift Handover
        ↓
Permit Suspension / Extension
        ↓
Work Completion
        ↓
De-isolation
        ↓
Permit Closure
        ↓
Audit / History / KPI
```

---

## 2. Architectural Principle

Permit Management shall be a dedicated domain/subsystem.

A Permit must **not** be implemented as a few fields inside `WorkOrder`.

Recommended relationship:

```text
WorkRequest
    ↓
WorkOrder
    ↓
RiskAssessment
    ↓
PermitAssessment
    ↓
Permit(s)
    ↓
Isolation / LOTO
    ↓
Execution
```

A WorkOrder may have:

- No Permit
- One Permit
- Multiple Permits
- Suspended Permit
- Extended Permit
- Cancelled Permit
- Re-issued Permit

Therefore:

```text
WorkOrder 1 ───── N Permit
```

---

## 3. Operational Roles

### 3.1 Work Supervisor

Responsibilities:

- Request work execution
- Identify execution team
- Confirm team readiness
- Verify worksite conditions
- Confirm Permit controls
- Conduct Toolbox Talk
- Stop work when unsafe
- Report work completion

The system shall allow the Work Supervisor to report actual execution conditions.

---

### 3.2 Senior Shift / Shift Supervisor

The Senior Shift role is responsible for **Operational Authorization**.

Responsibilities:

- Review current unit status
- Review operating restrictions
- Confirm whether work can safely proceed
- Review SIMOPS
- Confirm operating conditions
- Control shift handover
- Suspend permits during abnormal/emergency conditions
- Authorize restart after suspension

Critical principle:

> Permit Approval is not the same as Operational Approval.

A Permit may be technically and HSE compliant while current operating conditions still prohibit execution.

---

### 3.3 Senior Maintenance Engineer

Responsibilities:

- Review technical scope
- Review Method Statement
- Review isolation requirements
- Verify Permit Type
- Verify required technical competencies
- Verify tools and equipment
- Define technical hold points
- Confirm technical readiness

---

### 3.4 Planner

The Planner shall not only schedule the WorkOrder.

The Planner shall be able to:

- See Permit Requirements
- Include Permit preparation time
- Include Isolation windows
- Identify Permit dependencies
- Detect Permit conflicts
- Plan SIMOPS
- Consider Shutdown / Turnaround requirements
- Include operational windows in the schedule

Planning model:

```text
Maintenance Schedule
        +
Permit Requirement
        +
Isolation Window
        +
Operational Window
        =
Executable Schedule
```

---

### 3.5 Senior Production Engineer

Production shall be an active approval/coordination role.

Responsibilities:

- Define operating conditions
- Identify Unit availability
- Define production restrictions
- Assess Production Impact
- Identify Operational Windows
- Review SIMOPS
- Approve or reject from Production perspective

---

## 4. Permit Assessment Engine

The system shall include a configurable **Permit Assessment Engine**.

Inputs may include:

```text
Work Type
Asset
Functional Location
Area
Process Unit
Energy Source
Hazard
Task Type
Work Method
Contractor
Risk Level
Operating Condition
SIMOPS
```

Expected output:

```text
Required Permit Types
Required Isolation
Required Gas Test
Required Safety Controls
Required Approvals
Required Competencies
Required Documents
```

Example:

```text
Pump Maintenance
+
Hydrocarbon Service
+
Line Opening
+
Electrical Isolation
+
Potential Hot Work
```

Possible result:

```text
Cold Work
Line Breaking
Electrical Isolation
Gas Test
LOTO
JSA
```

Permit determination must be configurable and auditable.

---

## 5. Permit Types

The initial Permit Type Master Data shall support at least:

1. Hot Work Permit
2. Cold Work Permit
3. Confined Space Entry Permit
4. Work at Height Permit
5. Excavation Permit
6. Electrical Work Permit
7. Line Breaking / Breaking Containment Permit
8. Lifting / Crane Permit
9. Radiography Permit
10. Vehicle / Mobile Equipment Permit
11. Gas Test / Atmospheric Test
12. Isolation / LOTO Certificate

The architecture must allow additional Permit Types without code redesign.

---

## 6. Permit Lifecycle

Permit shall use an explicit state machine.

Recommended states:

```text
Draft
 ↓
Requested
 ↓
UnderAssessment
 ↓
RiskReviewed
 ↓
IsolationRequired
 ↓
IsolationInProgress
 ↓
IsolationVerified
 ↓
PendingApproval
 ↓
Approved
 ↓
Issued
 ↓
Active
 ↓
Suspended
 ↓
Resumed
 ↓
Extended
 ↓
WorkCompleted
 ↓
PendingClosure
 ↓
Deisolated
 ↓
Closed
```

Exceptional states:

```text
Rejected
Cancelled
Expired
Revoked
EmergencySuspended
```

All state transitions must be controlled, authorized, and auditable.

---

## 7. Approval Matrix

Approval shall be Role-Based and Configurable.

Example:

```text
Work Supervisor
      ↓
Maintenance Engineer
      ↓
HSE / Permit Authority
      ↓
Operations / Senior Shift
      ↓
Production
      ↓
Permit Issuer
      ↓
Permit Receiver
```

Not every Permit Type requires every role.

The Approval Matrix shall be configurable based on:

- Permit Type
- Risk Level
- Area
- Unit
- Work Type
- Isolation Requirements
- Operating Conditions

Approvals must not be hard-coded.

---

## 8. Permit Issuer and Permit Receiver

These roles must remain distinct where Separation of Duties is required.

### Permit Issuer

Authorized representative of Operations / Permit Authority who issues the Permit.

### Permit Receiver

Authorized representative of the execution team who receives the Permit and accepts its conditions.

The system should prevent incompatible role combinations when Separation of Duties rules require separation.

---

## 9. Risk Assessment

Permit Management shall not replace Risk Assessment.

Required relationship:

```text
Hazard Identification
        ↓
Risk Assessment
        ↓
Control Measures
        ↓
Residual Risk
        ↓
Permit Decision
```

The system shall support linking:

- JSA
- TRA
- Risk Assessment
- Safety Controls

to the Permit.

---

## 10. Isolation / LOTO

Isolation / LOTO shall be an independent domain capability.

Lifecycle:

```text
Isolation Request
       ↓
Energy Identification
       ↓
Isolation Point Identification
       ↓
Isolation
       ↓
Lock / Tag
       ↓
Verification
       ↓
Permit Authorization
       ↓
Work
       ↓
De-isolation
       ↓
Restoration
```

Energy Types shall support at least:

- Electrical
- Mechanical
- Hydraulic
- Pneumatic
- Pressure
- Steam
- Thermal
- Chemical
- Gravity
- Stored Energy
- Process Energy

Permit and LOTO must be linked but must not be the same entity.

---

## 11. Isolation Point

An Isolation Point should support:

```text
IsolationPoint
├── Asset
├── FunctionalLocation
├── EnergyType
├── Location
├── NormalState
├── RequiredIsolationState
├── IsolationMethod
├── LockNumber
├── TagNumber
├── IsVerified
├── VerifiedBy
└── VerificationTime
```

Isolation verification shall be mandatory where required by the Permit / Isolation Matrix.

---

## 12. Gas Testing

Gas Testing shall be an independent entity/lifecycle.

Minimum information:

```text
GasTest
├── Permit
├── Location
├── DateTime
├── Tester
├── O2
├── LEL
├── H2S
├── CO
├── OtherParameters
├── Instrument
├── CalibrationStatus
├── Result
└── ValidUntil
```

Gas Test validity shall be time-bound.

A previous Gas Test shall not automatically remain valid for the entire Permit lifecycle.

---

## 13. SIMOPS Control

The system shall support **Simultaneous Operations (SIMOPS)** assessment.

Example:

```text
Job A
+
Job B
+
Job C
```

The system shall detect potential conflicts based on:

- Area
- Unit
- Hazard
- Permit Type
- Energy Source
- Operating Condition
- Work Method
- Time Window

Example conflict:

```text
Hot Work
+
Hydrocarbon Transfer
+
Line Breaking
```

The system shall create a Conflict, Warning, or mandatory approval depending on configured rules.

---

## 14. Area and Unit Control

Permit shall integrate with the plant hierarchy:

```text
Site
 ↓
Plant
 ↓
Area
 ↓
Process Unit
 ↓
Functional Location
 ↓
Asset
 ↓
WorkOrder
 ↓
Permit
```

This hierarchy is required for operational authorization, SIMOPS, and reporting.

---

## 15. Shift Handover

Active Permits shall participate in Shift Handover.

Lifecycle:

```text
Active Permits
        ↓
Shift Handover
        ↓
Condition Review
        ↓
Incoming Shift Acceptance
        ↓
Continue / Suspend / Close
```

The incoming Senior Shift must be able to review all active Permits and their conditions.

Handover must be auditable.

---

## 16. Permit Suspension

Permit may be suspended due to:

- Emergency
- Unit Upset
- Gas Detection
- Loss of Isolation
- Change in Operating Condition
- SIMOPS Conflict
- Unsafe Condition
- Shift Change
- Alarm / Trip
- Weather or Environmental Conditions

Suspension must capture:

```text
Who
Why
When
Operating Condition
Action Taken
Resume Criteria
```

---

## 17. Emergency Control

Emergency workflow:

```text
Emergency
    ↓
Suspend Relevant Active Permits
    ↓
Notify Responsible Roles
    ↓
Secure Worksite
    ↓
Reassess
    ↓
Resume / Cancel
```

Emergency actions must be permission-controlled and fully auditable.

Emergency workflow must not become a mechanism for bypassing mandatory safety controls.

---

## 18. Permit Extension

Permit extension shall not be implemented as simply changing `ValidTo`.

Every extension must record:

- Reason
- Previous Validity
- New Validity
- Current Conditions
- Reconfirmation
- Required Approvals
- Approver
- Timestamp

---

## 19. Permit Closure

Permit closure shall require controlled checks.

Recommended sequence:

```text
Work Completed
        ↓
Tools Removed
        ↓
Personnel Accounted For
        ↓
Housekeeping Completed
        ↓
Temporary Connections Removed
        ↓
Guards / Covers Restored
        ↓
Isolation Review
        ↓
Operational Acceptance
        ↓
De-isolation
        ↓
Permit Closed
```

Permit closure must not be a simple `Close` button.

---

## 20. WorkOrder Integration

Primary relationship:

```text
WorkOrder
    └── PermitAssessment
            └── Permit(s)
```

Permit must have a clearly defined work scope.

A Permit must not be reused for unrelated work.

For normal workflows:

> Permit shall not exist without a valid Work Context.

Emergency workflows may use a controlled exception path, followed by post-work reconciliation.

---

## 21. Emergency Permit

Emergency workflow:

```text
Emergency Request
       ↓
Emergency Risk Review
       ↓
Emergency Authorization
       ↓
Emergency Permit
       ↓
Execution
       ↓
Post-Work Review
```

Emergency authorization must preserve mandatory controls wherever technically possible.

---

## 22. Contractor Integration

Permit must support Contractor personnel.

Recommended prerequisite chain:

```text
Contractor
 ↓
Personnel
 ↓
Competency
 ↓
Training
 ↓
Authorization
 ↓
Permit
```

The system shall validate required competencies before Permit issuance.

---

## 23. Competency Management

Certain Permit Types require specific competencies.

Model:

```text
Permit Type
      ↓
Required Competency
      ↓
Employee / Contractor Personnel
      ↓
Validity
```

A Permit shall not be issued to an unauthorized person where competency is mandatory.

---

## 24. Document Integration

Permit shall integrate with existing PEMS Document / Engineering capabilities.

Supported references may include:

- Method Statement
- JSA / TRA
- Engineering Drawing
- P&ID
- SLD
- Datasheet
- Vendor Document
- Work Instruction
- SOP
- Emergency Procedure
- Relevant Engineering Documents

Do not create a second independent Document Management System.

---

## 25. Authorization and Security

Minimum permissions:

```text
Create Permit
Request Permit
Assess Permit
Approve Permit
Issue Permit
Receive Permit
Suspend Permit
Resume Permit
Extend Permit
Revoke Permit
Close Permit
Verify Isolation
De-isolate
Emergency Override
```

All critical actions require authorization and audit logging.

---

## 26. Audit Trail

For every Permit, record:

```text
Created By
Requested By
Assessed By
Approved By
Issued By
Received By
Suspended By
Resumed By
Extended By
Closed By
```

Every important transition should capture:

```text
Timestamp
Reason
Old Value
New Value
Comment
Actor
```

Audit history must be immutable from normal user workflows.

---

## 27. Execution Readiness Gate

For Permit-required WorkOrders, the system shall provide a single execution readiness status.

Example:

```text
Work Order              ✓
Required Permit         ✓
Risk Assessment         ✓
JSA                     ✓
Isolation               ✓
Isolation Verification ✓
Gas Test                ✓
Competency              ✓
Tools                   ✓
Material                ✓
Operations Approval     ✓
Production Approval     ✓
Permit Issued           ✓

EXECUTION STATUS: READY
```

If any Mandatory Control is incomplete:

```text
EXECUTION STATUS: NOT READY
```

This Gate must prevent unauthorized execution.

---

## 28. Golden Rule

A Permit alone shall not authorize work execution.

Execution authorization is the result of:

```text
Permit
+
Risk Assessment
+
Isolation
+
Operational Authorization
+
Competency
+
Mandatory Safety Controls
=
Execution Authorization
```

Where applicable, Production approval, HSE approval, Gas Testing, JSA, and other controls must also be satisfied.

---

## 29. KPI and Reporting

Minimum KPI set:

- Permit Count
- Active Permits
- Expiring Permits
- Suspended Permits
- Permit Lead Time
- Permit Approval Time
- Permit Preparation Time
- Permit Violations
- Permit Rejections
- Permit Extensions
- Emergency Permits
- Isolation Failures
- Gas Test Failures
- SIMOPS Conflicts
- Permit-related Delays
- WorkOrders Delayed by Permit
- Permit Compliance Rate

---

## 30. Critical Alerts

System alerts shall cover:

- Permit Near Expiry
- Permit Expired
- Gas Test Expired
- Isolation Not Verified
- Missing Approval
- Missing Competency
- SIMOPS Conflict
- Permit / WorkOrder Scope Mismatch
- Active Permit Without Active Work
- Work Completed but Permit Open
- Permit Open During Shift Handover
- De-isolation Pending
- Emergency Suspension

---

## 31. Dashboards

### Operations Dashboard

```text
Active Permits
Expiring Soon
Suspended Permits
Emergency Permits
SIMOPS Conflicts
Pending Operations Approvals
```

### Maintenance Dashboard

```text
WorkOrders Waiting for Permit
Permit Delays
Isolation Status
Execution Readiness
```

### Planner Dashboard

```text
Upcoming Permit Requirements
Permit Lead Time
Schedule Conflicts
Isolation Windows
```

### Production Dashboard

```text
Unit Impact
Operational Windows
SIMOPS
Production Restrictions
Pending Approvals
```

### HSE Dashboard

```text
Permit Compliance
Violations
Gas Tests
High Risk Work
Suspensions
Emergency Permits
```

---

## 32. AI Integration

AI may assist with:

- Permit Type Recommendation
- Hazard Identification Assistance
- Missing Control Detection
- SIMOPS Detection
- Document Review
- Similar Job Retrieval
- Historical Permit Analysis
- Permit Delay Analysis
- Risk Pattern Detection

AI must **not** independently:

- Issue a Permit
- Approve a Permit
- Authorize Work
- Override Isolation
- Bypass mandatory controls
- Perform final safety authorization

Final authorization remains Human-Controlled.

---

## 33. Proposed Domain Objects

Minimum domain objects:

```text
Permit
PermitType
PermitAssessment
PermitRequirement
PermitApproval
PermitCondition
PermitControl
PermitExtension
PermitSuspension
PermitClosure

RiskAssessment
RiskControl

IsolationRequest
IsolationPoint
IsolationDevice
Lock
Tag
IsolationVerification
DeIsolation

GasTest
GasTestReading

SIMOPSConflict
OperationalApproval
ShiftHandover

CompetencyRequirement
PermitCompetency

PermitDocument
PermitNotification
```

Before creating any new Entity, Claude must inspect existing PEMS entities and reuse existing domain objects where appropriate.

---

## 34. Integration Architecture

```text
                    ┌──────────────────┐
                    │ Engineering / VDR│
                    └────────┬─────────┘
                             │
                             ↓
┌──────────────┐      ┌──────────────┐
│ Maintenance  │─────→│   WorkOrder  │
└──────────────┘      └──────┬───────┘
                             ↓
                      ┌──────────────┐
                      │ Risk / JSA   │
                      └──────┬───────┘
                             ↓
                      ┌──────────────┐
                      │Permit Assess.│
                      └──────┬───────┘
                             ↓
                      ┌──────────────┐
                      │    Permit    │
                      └──────┬───────┘
                         ┌───┴────┐
                         ↓        ↓
                    ┌────────┐ ┌─────────┐
                    │  LOTO  │ │ GasTest │
                    └────┬───┘ └────┬────┘
                         └────┬──────┘
                              ↓
                    ┌──────────────────┐
                    │ Operations /      │
                    │ Production        │
                    └────────┬─────────┘
                             ↓
                    ┌──────────────────┐
                    │    Execution     │
                    └──────────────────┘
```

---

## 35. Mandatory Pre-Implementation Audit

Before coding, Claude must inspect:

1. Current CMMS architecture
2. Existing WorkOrder lifecycle
3. Asset and FunctionalLocation entities
4. Employee and Contractor structure
5. Existing workflow engine
6. Existing authorization / RBAC
7. Existing Document Core / VDR
8. Existing Audit Trail
9. Existing notification infrastructure
10. Existing database and migrations
11. Existing HSE-related entities, if any
12. Existing Maintenance Master Data

Claude must produce an **Architecture Conflict Report** before implementation.

---

## 36. Implementation Restrictions

Claude shall NOT:

- Create duplicate Employee entities
- Create duplicate Asset entities
- Create duplicate FunctionalLocation entities
- Create a second Document Management System
- Create a second Workflow Engine
- Hard-code approval chains
- Store LOTO as unstructured JSON when relational/auditable data is required
- Reduce Permit to a few WorkOrder fields
- Allow AI to perform final authorization
- Modify previous CMMS phases without documented architectural justification
- Create database migrations before inspecting existing migrations
- Break existing PEMS domain relationships

---

## 37. Acceptance Test — End-to-End

CMMS-10 shall not be considered complete until the following scenario can be executed end-to-end:

```text
Work Request
→ Work Order
→ Permit Assessment
→ Risk Assessment
→ Required Permit Identification
→ JSA
→ Isolation Request
→ Isolation Execution
→ Isolation Verification
→ Gas Test
→ Approval
→ Permit Issue
→ Permit Receiver Acceptance
→ Work Execution
→ Shift Handover
→ Permit Suspension / Resume
→ Work Completion
→ De-isolation
→ Operational Acceptance
→ Permit Closure
→ Audit Trail
→ KPI
```

The scenario must be tested using real application workflows, not only unit-level code tests.

---

## 38. Definition of Done

CMMS-10 is considered complete only when:

- Domain model is approved
- Database model is consistent with PEMS architecture
- Permit lifecycle is implemented
- Permit Assessment is implemented
- Approval Matrix is configurable
- LOTO lifecycle is implemented
- Isolation Verification is implemented
- Gas Test lifecycle is implemented
- SIMOPS controls are implemented
- Shift Handover is implemented
- Emergency workflow is implemented
- Permit Suspension/Resume is implemented
- Permit Extension is implemented
- Permit Closure is controlled
- Competency validation is implemented
- Contractor integration is implemented
- RBAC is implemented
- Audit Trail is complete
- Execution Readiness Gate is implemented
- Dashboards are implemented
- Critical Alerts are implemented
- End-to-End Acceptance Scenario passes
- Existing CMMS functionality remains regression-safe

---

## 39. Final Architecture Decision

CMMS-10 shall be implemented as:

> **Permit to Work, Isolation & Operational Control**

This phase is an **Execution Governance Layer** for PEMS CMMS.

Final operating model:

```text
ASSET
  ↓
MAINTENANCE STRATEGY
  ↓
PLANNING
  ↓
WORK REQUEST
  ↓
WORK ORDER
  ↓
RISK / JSA
  ↓
PERMIT ASSESSMENT
  ↓
PERMIT + LOTO + GAS TEST
  ↓
OPERATIONS / PRODUCTION AUTHORIZATION
  ↓
EXECUTION
  ↓
COMPLETION
  ↓
DE-ISOLATION
  ↓
HISTORY / RELIABILITY / KPI
```

**CMMS-10 must be treated as a safety-critical operational control capability, not as a generic CRUD module.**

Before production deployment, it requires Architecture Review, Domain Review, Security Review, Workflow Review, Safety/HSE Review, and End-to-End Scenario Validation.
