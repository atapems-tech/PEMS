# PEMS Enterprise Organization, Role & Permission Management
## Real Organizational Hierarchy + RBAC + Scope-Based Authorization

### Objective

PEMS must implement a **real, configurable enterprise authorization architecture**.

The current authorization model must NOT be treated as a simple list of roles such as:

```text
Admin
User
Manager
Engineer
```

PEMS is an industrial/engineering platform and must support controlled authorization across:

- Engineering
- Document Control
- VDR
- Contracts
- Procurement
- Inventory & Materials
- CMMS
- Maintenance
- Operations
- Permit to Work
- HSE
- Reliability
- Planning
- IT
- Administration
- Management

The authorization architecture must enforce the principle:

> **A user receives only the permissions required by their organizational responsibility and functional scope.**

A user being a manager does NOT automatically grant access to every module.

A user being an IT administrator does NOT automatically grant access to engineering documents.

A senior shift role does NOT automatically grant destructive permissions.

---

# 1. Required Authorization Architecture

Implement a real enterprise authorization model based on:

```text
Organization
    ↓
Organizational Unit / Department
    ↓
Position
    ↓
User
    ↓
Role Assignment
    ↓
Permission
    ↓
Resource
    ↓
Action
    ↓
Scope
    ↓
Policy / Constraint
```

The system must support both:

### RBAC

Role-Based Access Control

and:

### Scope/Attribute-Based Authorization

The authorization decision must be capable of considering:

- User
- Role
- Department
- Organizational Unit
- Discipline
- Project
- Plant/Area
- Resource ownership
- Document classification
- Workflow status
- Record state
- Approval responsibility
- Shift responsibility
- Functional responsibility

Do NOT implement a fake RBAC system where every permission is simply a boolean on the User table.

---

# 2. Organizational Structure

PEMS must support a configurable organization hierarchy.

Example:

```text
Company
│
├── Executive Management
│
├── Engineering
│   ├── Process
│   ├── Mechanical
│   ├── Electrical
│   ├── Instrumentation & Control
│   ├── Civil
│   ├── Piping
│   ├── Telecom
│   └── Engineering Document Control
│
├── Maintenance
│   ├── Mechanical Maintenance
│   ├── Electrical Maintenance
│   ├── Instrumentation Maintenance
│   ├── Maintenance Planning
│   └── Reliability
│
├── Operations
│   ├── Production
│   ├── Shift Operations
│   └── Shift Supervision
│
├── HSE
│   ├── HSE
│   ├── Permit to Work
│   └── Process Safety
│
├── Procurement
│
├── Contracts
│
├── Warehouse / Materials
│
├── Project Controls
│
├── IT
│
└── Administration
```

This is an example baseline, NOT a hard-coded organizational structure.

The Organization Administrator must be able to create, rename, deactivate, reorder and restructure organizational units.

---

# 3. Real Positions

Support real organizational positions separately from application roles.

Examples:

```text
Managing Director
Engineering Manager
Engineering Discipline Manager
Senior Engineering Engineer
Engineering Engineer
Engineering Document Controller

Maintenance Manager
Maintenance Superintendent
Senior Maintenance Engineer
Maintenance Engineer
Maintenance Planner
Reliability Engineer
Maintenance Supervisor
Senior Shift Supervisor
Shift Supervisor
Shift Engineer
Maintenance Technician

Production Manager
Production Superintendent
Production Engineer
Operations Supervisor
Senior Shift Operator
Operator

HSE Manager
HSE Engineer
Permit Coordinator
Permit Issuer
Permit Receiver

Procurement Manager
Buyer
Procurement Engineer

Contracts Manager
Contract Engineer
Contract Administrator

Warehouse Manager
Warehouse Supervisor
Warehouse Clerk
Material Controller

Project Controls Manager
Planning Engineer
Cost Engineer

IT Manager
IT Administrator
Application Administrator
Helpdesk Administrator

System Administrator
Security Administrator
Audit Administrator
```

Again:

**Position ≠ Role ≠ Permission.**

A Position may have one or more Roles.

A Role may be assigned to multiple Positions.

---

# 4. Application Roles

Create configurable application roles.

Recommended baseline roles:

## Executive / Management

```text
ExecutiveViewer
DepartmentManager
FunctionalManager
```

## Engineering

```text
EngineeringManager
EngineeringDisciplineManager
EngineeringLead
EngineeringEngineer
EngineeringReviewer
EngineeringApprover
EngineeringDocumentController
```

## Document Control

```text
DocumentController
DocumentReviewer
DocumentApprover
TransmittalController
VDRController
```

## Maintenance / CMMS

```text
MaintenanceManager
MaintenancePlanner
MaintenanceEngineer
ReliabilityEngineer
MaintenanceSupervisor
SeniorShiftSupervisor
WorkOrderDispatcher
WorkOrderExecutor
MaintenanceViewer
```

## Operations

```text
ProductionManager
OperationsSupervisor
ShiftSupervisor
ShiftEngineer
Operator
```

## Permit to Work

```text
PermitManager
PermitCoordinator
PermitIssuer
PermitReceiver
PermitApprover
HSEReviewer
```

## Materials / Inventory

```text
MaterialsManager
WarehouseSupervisor
MaterialController
WarehouseClerk
InventoryViewer
```

## Procurement

```text
ProcurementManager
Buyer
ProcurementEngineer
ProcurementViewer
```

## Contracts

```text
ContractsManager
ContractEngineer
ContractAdministrator
ContractsViewer
```

## HSE

```text
HSEManager
HSEEngineer
HSEReviewer
```

## IT

```text
ITManager
ITAdministrator
ApplicationAdministrator
HelpdeskAdministrator
```

## Platform Security

```text
SecurityAdministrator
AuditAdministrator
OrganizationAdministrator
RoleAdministrator
```

---

# 5. Critical Rule — IT Must Not Equal Business Access

Implement explicit separation between:

```text
Technical Administration
```

and:

```text
Business Authorization
```

For example:

```text
ITAdministrator
```

may be able to:

- manage technical configuration
- manage integrations
- inspect system health
- manage technical settings
- manage application infrastructure

but MUST NOT automatically be able to:

- approve engineering documents
- revise engineering documents
- approve VDR documents
- issue Work Orders
- approve Work Orders
- delete CMMS records
- approve permits
- approve procurement
- approve contracts

The system must enforce this server-side.

---

# 6. Permission Model

Permissions must be granular.

Recommended structure:

```text
<Resource>.<Action>
```

Examples:

```text
EngineeringDocument.View
EngineeringDocument.Create
EngineeringDocument.Edit
EngineeringDocument.Revise
EngineeringDocument.Submit
EngineeringDocument.Review
EngineeringDocument.Approve
EngineeringDocument.Reject
EngineeringDocument.Issue
EngineeringDocument.Freeze
EngineeringDocument.Unfreeze
EngineeringDocument.Delete

WorkOrder.View
WorkOrder.Create
WorkOrder.Edit
WorkOrder.Plan
WorkOrder.Assign
WorkOrder.Dispatch
WorkOrder.Issue
WorkOrder.Start
WorkOrder.Suspend
WorkOrder.Resume
WorkOrder.Complete
WorkOrder.Cancel
WorkOrder.Close
WorkOrder.Delete

Material.View
Material.Create
Material.Edit
Material.Import
Material.Adjust
Material.Delete

Permit.View
Permit.Create
Permit.Edit
Permit.Issue
Permit.Approve
Permit.Suspend
Permit.Resume
Permit.Close
Permit.Cancel
Permit.Delete
```

Do NOT use a generic:

```text
CanEdit
CanDelete
CanApprove
```

model across the entire system.

Permissions must be resource-specific.

---

# 7. Destructive Actions Must Be Explicit

The following must always be explicit permissions:

```text
Delete
HardDelete
Cancel
Reject
Approve
Issue
Unissue
Close
Reopen
Freeze
Unfreeze
Override
EmergencyOverride
```

Having:

```text
WorkOrder.Edit
```

must NOT imply:

```text
WorkOrder.Delete
```

Having:

```text
EngineeringDocument.View
```

must NOT imply:

```text
EngineeringDocument.Edit
```

Having:

```text
EngineeringDocument.Edit
```

must NOT imply:

```text
EngineeringDocument.Approve
```

---

# 8. Example — Senior Shift Supervisor

A Senior Shift Supervisor may have:

```text
WorkOrder.View
WorkOrder.Create
WorkOrder.Edit
WorkOrder.Assign
WorkOrder.Dispatch
WorkOrder.Issue
WorkOrder.Start
WorkOrder.Suspend
WorkOrder.Resume
WorkOrder.Complete
```

But MUST NOT have:

```text
WorkOrder.Delete
WorkOrder.HardDelete
WorkOrder.Approve
MaintenanceStrategy.Delete
MaintenancePlan.Delete
Asset.Delete
```

unless a separate organizational policy explicitly grants such permissions.

The fact that the user is a senior operational role must never automatically grant destructive CMMS permissions.

---

# 9. Engineering Document Example

An Engineering Engineer may have:

```text
EngineeringDocument.View
EngineeringDocument.Create
EngineeringDocument.Edit
EngineeringDocument.Revise
EngineeringDocument.Submit
```

An Engineering Reviewer may have:

```text
EngineeringDocument.View
EngineeringDocument.Review
EngineeringDocument.Comment
EngineeringDocument.Reject
```

An Engineering Approver may have:

```text
EngineeringDocument.View
EngineeringDocument.Approve
EngineeringDocument.Reject
EngineeringDocument.Issue
```

A Document Controller may have:

```text
EngineeringDocument.View
EngineeringDocument.Register
EngineeringDocument.Revise
EngineeringDocument.Transmit
EngineeringDocument.Freeze
EngineeringDocument.Unfreeze
```

but should NOT automatically have engineering technical approval authority.

---

# 10. Separation of Duties

The system must support Separation of Duties.

Examples:

A user who creates an Engineering Document should not automatically be able to approve the same document.

A user who creates a Purchase Request should not automatically approve the same request.

A user who creates a Work Order should not automatically close it if organizational policy prohibits this.

A Permit Receiver must not automatically become Permit Issuer.

A Vendor/Contract record creator must not automatically become the final approver.

Implement configurable SoD policies.

Example:

```text
EngineeringDocument.Create
    CONFLICTS WITH
EngineeringDocument.Approve
```

```text
PurchaseRequest.Create
    CONFLICTS WITH
PurchaseRequest.Approve
```

```text
PermitReceiver.Accept
    CONFLICTS WITH
PermitIssuer.Issue
```

Do not hard-code every possible conflict. Build a configurable policy model.

---

# 11. Scope-Based Access

Permissions must support scope.

Example:

An Engineering Engineer may have:

```text
EngineeringDocument.Edit
```

but only for:

```text
Discipline = Mechanical
Project = Project-A
```

Another engineer:

```text
Discipline = Electrical
```

should not automatically edit Mechanical engineering documents.

Similarly:

A Maintenance Engineer may have access only to:

```text
Plant = Area-A
```

A Shift Supervisor may have access to:

```text
Shift = Current Shift
Area = Assigned Area
```

A Warehouse Controller may have access only to:

```text
Warehouse = WH-01
```

The authorization engine must therefore support:

```text
Global
Department
Discipline
Project
Plant
Area
Warehouse
Shift
Document
RecordOwner
```

scopes.

---

# 12. Role Assignment

Users must support multiple roles.

Example:

```text
User: John Smith

Position:
Senior Maintenance Engineer

Department:
Maintenance

Roles:
MaintenanceEngineer
WorkOrderPlanner
ReliabilityViewer

Scope:
Plant-A
Mechanical Area
```

Another user may have:

```text
Position:
Senior Shift Supervisor

Roles:
SeniorShiftSupervisor
WorkOrderDispatcher

Scope:
Plant-A
Unit-300
Shift-B
```

Do not force one user = one role.

---

# 13. Temporary / Delegated Roles

Support temporary delegation.

Example:

```text
Engineering Manager
        ↓
Delegates approval authority
        ↓
Engineering Discipline Manager
        ↓
Valid:
2026-08-20 08:00
to
2026-08-25 18:00
```

Delegated permissions must:

- have start/end dates
- identify delegator
- identify delegate
- identify granted permissions
- identify scope
- be auditable
- automatically expire

---

# 14. Emergency Authorization

Emergency permissions must be separate from normal permissions.

Example:

```text
EmergencyOverride
```

must require:

- explicit permission
- reason
- timestamp
- user
- affected record
- authorization basis
- audit log

Emergency authority must NOT silently grant permanent permissions.

---

# 15. Backend Enforcement

This is mandatory.

Hiding a button is NOT authorization.

For every sensitive operation:

```text
UI
 ↓
API
 ↓
Authorization Policy
 ↓
Permission
 ↓
Scope
 ↓
Business Rule
 ↓
Database Operation
```

must be enforced.

A user must receive:

```text
403 Forbidden
```

when attempting an unauthorized action through:

- API
- direct HTTP request
- browser developer tools
- CLI
- integration
- background request

Do not rely on frontend visibility.

---

# 16. Authorization Decision

Implement a central authorization service/policy layer.

Conceptually:

```text
Authorize(
    User,
    Resource,
    Action,
    ResourceId,
    Context
)
```

The result should distinguish:

```text
Allowed
Denied
DeniedByRole
DeniedByScope
DeniedBySoD
DeniedByWorkflowState
DeniedByOwnership
DeniedByPolicy
```

This is important for audit and troubleshooting.

---

# 17. Workflow-State Authorization

Authorization must also consider workflow state.

Example:

A Work Order in:

```text
Draft
```

may be editable.

A Work Order in:

```text
Issued
```

may no longer be freely edited.

A Work Order in:

```text
Completed
```

must not be deleted.

Similarly:

```text
EngineeringDocument = Approved
```

must not be editable through the normal Edit permission.

The authorization layer must evaluate:

```text
Permission + Current State + Requested Transition
```

---

# 18. Role Management UI

Create a real administration interface:

```text
Administration
    ├── Organization
    ├── Departments
    ├── Positions
    ├── Users
    ├── Roles
    ├── Permissions
    ├── Role Assignments
    ├── Delegations
    ├── Separation of Duties
    ├── Authorization Policies
    └── Authorization Audit
```

### Role editor

An administrator must be able to:

- create role
- rename role
- activate/deactivate role
- assign permissions
- define scope
- assign role to positions
- view users assigned to role
- view effective permissions
- clone role
- compare roles

---

# 19. Effective Permissions

Every user must have an:

```text
Effective Permissions
```

view.

Example:

```text
User:
Senior Shift Supervisor

Department:
Operations

Roles:
SeniorShiftSupervisor
WorkOrderDispatcher

Effective permissions:

WorkOrder.View              ALLOW
WorkOrder.Create            ALLOW
WorkOrder.Assign            ALLOW
WorkOrder.Dispatch          ALLOW
WorkOrder.Issue             ALLOW
WorkOrder.Delete            DENY
WorkOrder.HardDelete        DENY
EngineeringDocument.Edit    DENY
EngineeringDocument.Approve DENY
CMMS.Asset.Delete           DENY
```

The UI should clearly explain **why** a permission is allowed or denied.

---

# 20. Permission Simulator

Implement an Authorization Simulator for administrators.

Example:

```text
User:
Senior Shift Supervisor

Resource:
WorkOrder #WO-2026-00125

Action:
Delete
```

Result:

```text
DENIED

Reason:
Role SeniorShiftSupervisor does not contain WorkOrder.Delete.
```

Another example:

```text
User:
Mechanical Engineering Engineer

Resource:
EngineeringDocument #ED-00125

Action:
Edit
```

Result:

```text
ALLOWED

Role:
EngineeringEngineer

Scope:
Discipline = Mechanical

Document:
Discipline = Mechanical
```

This will be extremely valuable for diagnosing authorization issues.

---

# 21. Audit Trail

Every privileged authorization event must be auditable.

Record:

```text
User
Role
Permission
Action
Resource
ResourceId
Scope
Decision
Reason
Timestamp
IP
CorrelationId
```

Especially log:

```text
Delete
Approve
Reject
Issue
Unissue
Override
EmergencyOverride
RoleChange
PermissionChange
Delegation
OrganizationChange
```

Role/permission changes themselves must be audited.

---

# 22. Security Rules

Implement:

### Deny by default

If no explicit permission exists:

```text
DENY
```

### Least privilege

Do not grant broad permissions merely because a role sounds senior.

### No privilege inheritance from management title

`Manager` must NOT mean:

```text
FullAccess
```

### Technical administration ≠ business authority

IT administrators do not automatically receive engineering/CMMS/business permissions.

### UI security ≠ backend security

Every sensitive action must be server-side authorized.

---

# 23. Seed Baseline Organization

Create a controlled baseline seed suitable for development/testing.

Do NOT create a single:

```text
SuperAdmin
```

with unrestricted business permissions and use it as the normal application model.

If a bootstrap/system administrator is required, clearly separate:

```text
PlatformBootstrapAdministrator
```

from normal business roles.

---

# 24. Migration / Existing Users

Before modifying authorization:

1. Inspect current User model.
2. Inspect current Role model.
3. Inspect current claims/policies.
4. Inspect all `[Authorize]` attributes.
5. Inspect custom authorization handlers.
6. Inspect module-specific permission checks.
7. Identify duplicated authorization logic.
8. Create migration strategy.
9. Preserve existing users.
10. Preserve security-critical access.
11. Avoid accidental privilege escalation.

Do not blindly replace the current security system.

---

# 25. Required Permission Matrix

Create a machine-readable permission matrix.

At minimum cover:

```text
Engineering
VDR
Contracts
Procurement
Inventory & Materials
CMMS
Maintenance
Operations
Permit to Work
HSE
Administration
IT
```

For each:

```text
Role
Resource
View
Create
Edit
Review
Approve
Reject
Issue
Execute
Close
Delete
HardDelete
Override
```

The matrix must be persisted/documented and used as the basis for automated authorization tests.

---

# 26. Required Automated Security Tests

Create authorization tests for both positive and negative cases.

Examples:

### Engineering

```text
ITAdministrator → EngineeringDocument.Edit = DENY
ITAdministrator → EngineeringDocument.Approve = DENY

EngineeringEngineer → EngineeringDocument.Edit = ALLOW
EngineeringEngineer → EngineeringDocument.Approve = DENY

EngineeringApprover → EngineeringDocument.Approve = ALLOW
```

### CMMS

```text
SeniorShiftSupervisor → WorkOrder.Create = ALLOW
SeniorShiftSupervisor → WorkOrder.Issue = ALLOW
SeniorShiftSupervisor → WorkOrder.Delete = DENY

MaintenancePlanner → WorkOrder.Plan = ALLOW
MaintenancePlanner → WorkOrder.Delete = DENY
```

### Inventory

```text
WarehouseClerk → Material.View = ALLOW
WarehouseClerk → Material.Import = ALLOW
WarehouseClerk → Material.Delete = DENY
```

### Separation of Duties

```text
DocumentCreator → Document.Approve = DENY
```

where the SoD policy prohibits self-approval.

### Scope

```text
MechanicalEngineer
    → MechanicalDocument.Edit = ALLOW

MechanicalEngineer
    → ElectricalDocument.Edit = DENY
```

### Direct API attack

Attempt unauthorized actions directly against the API.

Expected:

```text
403 Forbidden
```

---

# 27. No Hard-Coded Business Roles

The system must not contain logic such as:

```csharp
if (user.Role == "SeniorShiftSupervisor")
```

throughout the application.

Business authorization must use centralized permissions/policies.

Bad:

```text
if role == "Admin"
```

Preferred:

```text
AuthorizationService.Authorize(
    user,
    Permission.WorkOrder.Delete,
    workOrder
)
```

---

# 28. Compatibility With Existing PEMS Modules

Inspect and integrate authorization into the existing:

- Engineering
- Engineering Documents
- VDR
- Transmittal
- Contracts
- Inventory & Materials
- CMMS
- Work Orders
- Permit to Work
- Maintenance
- Dashboard

Do not implement only the Administration UI while leaving existing controllers unprotected.

Every sensitive existing endpoint must be audited.

---

# 29. Graphify

Update Graphify after implementation.

The graph should represent:

```text
Organization
    ↓
Department
    ↓
Position
    ↓
User
    ↓
Role
    ↓
Permission
    ↓
Resource
    ↓
Scope
    ↓
Policy
```

and relevant relationships such as:

```text
Role → grants → Permission
User → assigned → Role
Role → scoped-to → OrganizationUnit
Permission → protects → Resource
Policy → restricts → Action
SoD → conflicts-with → Permission
```

---

# 30. Required Final Audit

Before declaring PASS:

1. Audit current authorization architecture.
2. Audit all existing roles.
3. Audit all existing permissions.
4. Audit all sensitive endpoints.
5. Design Organization/Position/Role/Permission model.
6. Implement database model.
7. Implement centralized authorization service.
8. Implement scopes.
9. Implement workflow-state checks.
10. Implement SoD.
11. Implement delegation.
12. Implement emergency authorization where applicable.
13. Implement Administration UI.
14. Implement Effective Permissions.
15. Implement Permission Simulator.
16. Implement audit trail.
17. Protect existing APIs.
18. Add positive authorization tests.
19. Add negative/security tests.
20. Run full regression.
21. Update Graphify.
22. Produce final authorization audit report.

---

# Definition of Done

This task is PASS only when:

- [ ] Organization hierarchy is configurable.
- [ ] Departments are configurable.
- [ ] Positions are separate from application Roles.
- [ ] Roles are configurable.
- [ ] Permissions are granular.
- [ ] Resource/action permissions exist.
- [ ] Deny-by-default is enforced.
- [ ] Permission scopes exist.
- [ ] Workflow state affects authorization.
- [ ] Separation of Duties exists.
- [ ] Temporary delegation exists.
- [ ] Emergency authorization is controlled and audited.
- [ ] IT administration is separated from business authorization.
- [ ] Destructive permissions are explicit.
- [ ] Backend authorization is enforced.
- [ ] Direct API unauthorized actions return 403.
- [ ] Effective Permissions can be inspected.
- [ ] Authorization Simulator exists.
- [ ] Role/permission changes are audited.
- [ ] Existing PEMS modules are protected.
- [ ] Automated positive/negative security tests exist.
- [ ] Full regression suite passes.
- [ ] Graphify is updated.
- [ ] No unrelated production behavior is changed.

## Final Principle

PEMS authorization must answer three questions for every action:

```text
WHO is acting?
WHAT are they trying to do?
WHERE / ON WHICH RESOURCE are they allowed to do it?
```

And the final authorization decision must be:

```text
ALLOW
or
DENY + explicit reason
```

The system must never assume:

```text
Manager = Full Access
IT = Full Access
Engineer = Edit Everything
Supervisor = Delete Everything
Admin = Business Authority
```

The target is a real **industrial enterprise authorization system**, not a cosmetic role dropdown.