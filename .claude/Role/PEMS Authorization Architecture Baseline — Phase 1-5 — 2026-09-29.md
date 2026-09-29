# PEMS Authorization Architecture Baseline

## Phase 1–5 Completion Baseline

### Baseline Date: 2026-09-29

---

## 1. Baseline Status

**STATUS: PRODUCTION-READY WITH EXPLICIT DEFERRED HARDENING ITEMS**

The PEMS RBAC/ABAC authorization architecture has completed the approved Phase 1–5 implementation path.

The architecture has been verified through:

* source-level audit and implementation review
* focused unit/controller tests
* full regression testing
* live browser validation with Playwright
* English and Persian/RTL localization validation
* runtime grant/revoke verification
* authorization parity testing
* Work Order governance regression testing
* database/schema impact verification

### Final Evidence

| Evidence                     |                                 Result |
| ---------------------------- | -------------------------------------: |
| Focused authorization tests  |                       **36/36 passed** |
| Final full regression        |                   **2020/2021 passed** |
| Known regression failure     | **1 pre-existing environmental flake** |
| Playwright validation        |                       **15/15 passed** |
| Schema changes               |                                  **0** |
| Authorization entities added |                                  **0** |
| Second authorization engine  |                                 **No** |
| Second workflow engine       |                                 **No** |
| Second Role system           |                                 **No** |
| Privilege expansion          |                    **None identified** |
| EN/FA localization           |                           **Verified** |
| RTL browser validation       |                             **Passed** |

The remaining full-regression failure is the previously identified:

`MrsImportBatchCapacityTests`

with an environmental/timing characteristic:

> "Commit took 597508ms — unexpectedly slow even for InMemory"

It is not attributable to the Authorization Architecture work.

---

# 2. Purpose of This Baseline

This document is the authoritative implementation baseline for the PEMS authorization architecture as of:

**2026-09-29**

Future development must treat this architecture as an existing system.

Future work MUST:

1. reuse the existing authorization architecture;
2. preserve its security invariants;
3. avoid introducing duplicate authorization infrastructure;
4. avoid introducing a second Role model;
5. avoid introducing a second workflow engine;
6. preserve `AccessControlService` as the authorization decision engine;
7. preserve `RolePermissionGrant` as the runtime permission-grant source;
8. preserve `WorkflowPlatform` as the workflow authority;
9. preserve existing Work Order authorization rules;
10. remain additive unless an existing defect is demonstrated.

This baseline supersedes assumptions that Phase 3 or Phase 4 are still pending.

---

# 3. Authoritative Architecture

The current authorization architecture is:

```text
User
  │
  ├── RoleAssignment
  │      ├── Role
  │      ├── ScopeType / ScopeValue
  │      ├── AssignmentType
  │      └── Effective Dates
  │
  └── ApplicationRole
         │
         └── RolePermissionGrant
                  │
                  └── Permission
                       ├── ResourceName
                       ├── ActionName
                       ├── IsDestructive
                       └── IsActive

              ↓

       AccessControlService

              ↓

       AccessDecision
       ├── Allowed
       ├── Reason
       ├── Detail
       ├── MatchedScopeType
       └── MatchedScopeValue

              ↓

       AuthorizationAuditLog
```

Workflow authorization integrates with the same authorization architecture:

```text
Workflow Transition
       │
       └── RequiredPermission
                │
                ↓
      WorkflowAuthorizationHook
                │
                ↓
      AccessControlService
                │
                ├── Permission
                ├── Scope
                ├── Organization
                ├── Discipline
                └── SoD
```

There is no parallel authorization engine.

---

# 4. Core Sources of Authority

## 4.1 Runtime Authorization

The runtime authorization decision is owned by:

`AccessControlService`

All new authorization features must reuse this service.

Do not create:

* another authorization service;
* another permission evaluator;
* another policy engine;
* controller-local permission logic;
* duplicated scope evaluation;
* duplicated SoD evaluation.

---

## 4.2 Runtime Permission Grants

The runtime permission grant source is:

`RolePermissionGrant`

The administration UI may interact with legacy permission storage where required for compatibility, but runtime authorization must ultimately reflect the authoritative grant state.

The Phase 4 startup-drift correction established immediate synchronization:

```text
RolePermission
      ↓
PermissionService.SetPermissionsAsync()
      ↓
SyncRoleFromRolePermissionsAsync()
      ↓
RolePermissionGrant
```

Grant:

```text
checked
→ active RolePermissionGrant
```

Revoke:

```text
unchecked
→ RolePermissionGrant deactivated
```

Revoke must NOT depend on application restart.

---

# 5. Role Architecture

PEMS uses:

`ApplicationRole`

as the application Role entity.

There is no second Role system.

Custom roles are first-class.

The authorization engine must not assume that all roles are members of a fixed predefined list.

The previous administrative limitation:

```text
SystemRoles.All
```

must NOT be treated as the complete universe of roles.

The Permission Matrix now operates against live roles.

---

# 6. Role Lifecycle

The approved lifecycle is:

```text
Create Role
     ↓
Empty Grant Set
     ↓
Default Deny
     ↓
Permission Configuration
     ↓
Explicit Administrative Grant
     ↓
Runtime Authorization
```

Creating a role must NOT:

* copy SystemAdmin permissions;
* inherit permissions from the role name;
* automatically grant permissions;
* infer permissions from Position;
* infer permissions from Department;
* infer permissions from title.

A newly created role with no grants must remain denied by default.

---

# 7. Role Administration

The Roles administration surface now provides:

* live role listing;
* authorization configuration status;
* grant count;
* "Not configured" indication;
* direct navigation to the Permission Matrix;
* SystemAdmin unrestricted indication.

The exact role from the original audit:

`maintenance planning`

was preserved and not modified during remediation.

It remains visible in the live role list and is correctly identified as unconfigured when it has no effective grants.

This is intentional evidence that the original defect was fixed without mutating existing data.

---

# 8. Permission Matrix

The Permission Matrix now uses live application roles.

It MUST NOT use:

```text
SystemRoles.All
```

as the complete role universe.

The Matrix supports:

* system roles;
* seeded governance roles;
* custom application roles;
* maintenance roles;
* operations authority roles;
* future roles.

Permission changes must reach runtime authorization without application restart.

Unchecking a permission must have a real runtime effect.

---

# 9. Permission Model

Permissions use the existing:

```text
Resource.Action
```

model.

Examples include:

```text
WorkOrder.Issue
WorkOrder.Start
EngineeringDocuments.Approve
EngineeringDocuments.ApproveAllDisciplines
```

No policy DSL or expression language has been introduced.

No speculative authorization grammar is permitted.

---

# 10. Phase 3 — Hard-Coded Role Migration

Phase 3 is COMPLETE.

The last significant hard-coded role authorization in:

`EngineeringDocumentsController.CanApproveDocumentDisciplineAsync`

was migrated to capability-based authorization.

The implementation uses:

```text
EngineeringDocuments.Approve
EngineeringDocuments.ApproveAllDisciplines
```

The additional permission:

`EngineeringDocuments.ApproveAllDisciplines`

was introduced because reusing:

`EngineeringDocuments.ViewAllDisciplines`

would have broadened Supervisor approval authority.

This preserves the previous business behavior without privilege expansion.

### Remaining ManagerRole Exception

`WorkOrderWorkflow.ManagerRole`

remains as a documented single-use business exception.

This is deliberate.

Do not introduce a policy engine solely to eliminate one role-name constant.

Generalize this only if a second genuine consumer demonstrates the need.

---

# 11. Phase 4 — Administrative Authorization Surface

Phase 4 is COMPLETE.

Implemented:

### Role provisioning

```text
Create Role
→ Permission Matrix
→ Explicit Configuration
```

### Permission Matrix

Live-role administration.

### Effective Permissions

Available for:

* Role
* User

The User view also exposes relevant RoleAssignment information including:

* Scope
* Assignment Type
* Effective dates

### Permission Simulator

Uses the real:

`IAccessControlService`

authorization engine.

It must never become a second decision engine.

The Simulator displays decision information including:

* Allow/Deny
* Reason
* Matched Role
* Matched Scope
* Detail

### SoD Policy Administration

Supports:

* policy creation;
* duplicate detection;
* reverse-order duplicate detection;
* self-conflict rejection;
* enable/disable.

Uses the existing `SoDPolicy` model.

No new SoD infrastructure was introduced.

### Authorization Audit Viewer

Dedicated viewer for:

`AuthorizationAuditLog`

The authorization audit viewer remains distinct from the generic entity-change AuditLog.

---

# 12. Position Architecture

The architecture review required verification of Position administration.

Verification established that Position already has an administrative surface through:

`TeamController`

including existing operations such as:

* Create
* Update
* Move
* Deactivate
* AssignEmployee

Therefore no duplicate Position administration was created.

Important:

Position is currently not itself a direct authorization decision input.

This baseline does not assume Position-based authorization without demonstrated business requirements.

---

# 13. RoleAssignment

The existing:

`RoleAssignment`

model remains the scope/delegation/temporal layer.

It supports concepts including:

* Role
* ScopeType
* ScopeValue
* AssignmentType
* Effective dates
* Context

A second Role Assignment administration system was intentionally NOT created.

Existing Users → Edit role assignment remains the current operational path.

Future scope/delegation administration must extend the existing model rather than create another assignment mechanism.

---

# 14. SoD Architecture

PEMS maintains two complementary SoD mechanisms.

## Generic authorization SoD

Uses:

`SoDPolicy`

against:

`AuthorizationAuditLog`

## Work Order-specific SoD

Uses:

`WorkflowHistory`

inside:

`WorkOrderAuthorizationHook`

These must NOT be merged merely for architectural symmetry.

The Work Order mechanism is instance-specific and depends on workflow history.

The generic mechanism is authorization-policy-oriented.

Both remain valid.

---

# 15. Workflow Architecture

`WorkflowPlatform` remains authoritative for workflow execution.

Workflow authorization uses:

```text
Transition.RequiredPermission
```

as the authorization contract.

`WorkflowStage.ResponsibleRole` is not itself the runtime authorization gate.

Workflow authorization remains independent of role-name coupling.

No second workflow engine was introduced.

---

# 16. Work Order Security Invariants

The following invariants are part of this baseline and MUST NOT be weakened:

### Operations Issue / Release

Operations is the release authority.

Issue/Release:

* authorizes work execution;
* does NOT set actual StartDate;
* does NOT mean Maintenance has started work.

### Start Work

Maintenance execution begins only through the Start Work operation.

Actual `StartDate` belongs to execution, not Operations release.

### Actual Labor

Actual labor entry is restricted to the execution-eligible state.

`WorkOrder.Edit` is not Actual Labor Authority.

### Separation of Duties

The following separation remains enforced:

```text
Planner       != Executor
Issuer        != Executor
Executor      != Verifier
Creator       != FinalApprover
Verifier      != FinalApprover
```

### Organization

Organizations remain separate:

```text
Engineering
Maintenance
Operations
Warehouse
```

Operations remains the issue/release authority.

Warehouse does not gain workflow authority merely because it exists in the organizational model.

Engineering retains originating-discipline verification authority where applicable.

Maintenance Planning remains part of the lifecycle.

---

# 17. Localization Baseline

The authorization administration surface is bilingual.

Supported:

* English
* Persian
* RTL Persian

All new user-facing strings introduced by Phase 4 are localized through the existing:

`IStringLocalizer<SharedResource>`

system.

Do not add hard-coded user-facing UI strings.

The LanguagePurityTests regression baseline remains enforced.

The Phase 4 implementation caused a genuine localization regression during development.

It was fixed by:

1. localizing the new UI strings;
2. adding English/Persian resource entries;
3. removing duplicate resource keys;
4. correcting view patterns that triggered false positives in the existing ratchet test.

The final localization tests pass.

---

# 18. Testing Baseline

## Focused Tests

Final focused authorization-related validation:

**36/36 passed**

Covered areas include:

* Authorization Architecture completion
* SoD Policy
* Language Purity
* Access Control parity
* Access Control scope/SoD
* Engineering Document approval
* Work Order governance/workflow structure

---

## Full Regression

Final run:

```text
Total:   2021
Passed:  2020
Failed:  1
```

The single failure is the known pre-existing environmental/timing issue:

`MrsImportBatchCapacityTests`

This failure existed before the authorization implementation and remained unrelated to the authorization changes.

An intermediate `IrnNumberingRealSqlTests` failure was reproduced in isolation and passed on rerun, confirming it as an environmental SQL setup/connectivity issue rather than an implementation defect.

---

# 19. Playwright Baseline

Live browser validation:

**15/15 passed**

Validated flow includes:

```text
Login
→ Forced password change
→ Roles Index
→ Live role visibility
→ Create Role
→ Permission Matrix redirect
→ New role visibility
→ Not Configured state
→ Effective Permissions / Role
→ Effective Permissions / User
→ Permission Simulator
→ SoD Policy
→ SoD Policy Create
→ Authorization Audit
→ Persian/RTL
→ Mobile 375px
```

The Playwright validation also verified the exact original audit role:

`maintenance planning`

without modifying its underlying data.

---

# 20. Playwright Test Infrastructure Finding

During browser validation, a selector defect was found in the test script.

The original selector matched a hidden logout-form button instead of the intended visible submit button.

This was identified through the development server request log.

After scoping the selector to:

```text
.content-card form button[type="submit"]
```

the submissions correctly reached the server.

This was a Playwright test-script defect, not a product defect.

---

# 21. Database Baseline

No schema changes were made.

No migration was created.

No migration was applied.

No authorization table was added.

No existing authorization table was altered.

One permission catalog row is introduced through the existing seeding mechanism:

```text
EngineeringDocuments.ApproveAllDisciplines
```

This is data, not schema.

It follows the existing idempotent permission seeding mechanism.

---

# 22. Architecture Constraints

Future development MUST NOT introduce any of the following without a new architecture decision:

* second authorization engine;
* second Role system;
* second workflow engine;
* policy DSL;
* speculative expression engine;
* external IAM dependency;
* microservice authorization layer;
* duplicate scope evaluator;
* duplicate SoD engine;
* automatic privilege inheritance from role names;
* automatic permission inheritance from titles;
* speculative baseline-role explosion;
* unnecessary schema redesign.

---

# 23. Security Principles

The following principles are mandatory:

### Default Deny

No grant means no permission.

### Explicit Grant

Permissions must be explicitly granted.

### Server-Side Enforcement

UI visibility is never treated as authorization.

### Least Privilege

Migration must preserve or reduce privilege, never silently expand it.

### Scope Awareness

Scope must remain part of authorization where the existing module requires it.

### SoD

SoD must remain fail-closed.

### Auditability

Authorization decisions must remain explainable and auditable according to the existing architecture.

### Workflow Independence

Workflow authorization must remain capability-based rather than role-name-based.

---

# 24. Deferred Hardening Items

These are explicitly deferred and are NOT Phase 3/4 completion blockers.

## 24.1 Codebase-Wide Direct HTTP Attack Suite

Dedicated direct HTTP authorization regression tests were not added for the three new Admin controllers.

Reason:

The controllers use the same established:

```text
[Authorize(Roles = SystemRoles.SystemAdmin)]
```

mechanism used by the existing Admin surface.

A future security-hardening phase may create a consistent codebase-wide HTTP authorization suite covering Admin endpoints rather than testing only the newly introduced controllers.

This item must remain explicitly tracked.

---

## 24.2 RoleModuleAction Administration

`RoleModuleAction` remains a separate legacy/Permit-family authorization surface used by modules including:

* Permit
* LOTO
* GasTest
* Simops
* ShiftHandover
* ExecutionGate

It currently has no administrative UI.

No UI was added because:

* current Phase 4 scope did not require it;
* no existing UI writes to it;
* no demonstrated runtime drift exists from the new authorization UI.

A future decision may determine whether and how this legacy surface should be administered.

Do not create a speculative mega-permission table merely to eliminate this difference.

---

# 25. Known Test Baseline Exception

The following test remains known and pre-existing:

`MrsImportBatchCapacityTests`

Failure characteristic:

```text
Commit took 597508ms — unexpectedly slow even for InMemory
```

This must NOT be silently reclassified as an Authorization Architecture failure.

If this test later becomes consistently reproducible, investigate it independently.

---

# 26. Required Future Change Protocol

Before changing authorization code, future agents MUST:

1. Read this baseline.
2. Inspect the current implementation.
3. Verify whether the baseline is still current.
4. Identify the existing authorization component responsible for the requested behavior.
5. Reuse the existing service/entity/workflow infrastructure.
6. Prove why an additive change is necessary.
7. Avoid creating duplicate authorization concepts.
8. Run focused authorization tests.
9. Run relevant regression tests.
10. Update this baseline only when the architecture or verified security contract actually changes.

---

# 27. Mandatory Audit Questions for Future Work

Any future authorization change must answer:

### Architecture

* Which existing authorization component owns this behavior?
* Why is an existing component insufficient?
* Does this introduce a second source of truth?

### Security

* Does this change privilege?
* Does it affect default deny?
* Does it affect scope?
* Does it affect SoD?
* Does it affect workflow authority?

### Runtime

* Does the change take effect immediately?
* Does revoke take effect immediately?
* Is application restart required?
* Is there any cache or synchronization path involved?

### UI

* Is the UI merely exposing existing authorization capability?
* Is server-side enforcement present?
* Are all strings localized?
* Does EN/FA remain correct?

### Testing

* Is there a focused test?
* Is there an authorization parity test?
* Is there an API/direct-request test where appropriate?
* Does existing workflow governance remain green?

---

# 28. Baseline Acceptance Statement

As of **2026-09-29**, the PEMS Authorization Architecture has completed the approved Phase 1–5 implementation path.

The system now provides:

* centralized authorization decisions;
* role-based permission grants;
* scope-aware authorization;
* SoD enforcement;
* workflow-integrated authorization;
* live custom-role administration;
* permission provisioning;
* immediate grant/revoke synchronization;
* effective-permission inspection;
* permission simulation;
* SoD policy administration;
* authorization audit visibility;
* bilingual EN/FA administrative UI;
* runtime and browser verification.

The original Role Matrix defect has been closed at three levels:

```text
Source
  ↓
Tests
  ↓
Live Browser
```

The authorization architecture remains based on the previously approved design.

No parallel authorization architecture was introduced.

No schema redesign was required.

No privilege expansion was identified.

---

# 29. Baseline Signature

```text
PEMS Authorization Architecture
Baseline: Phase 1–5
Date: 2026-09-29

Focused Tests:        36/36 PASS
Full Regression:      2020/2021 PASS
Known Failure:        1 pre-existing environmental flake
Playwright:           15/15 PASS
Schema Changes:       0
New Auth Entities:    0
Second Auth Engine:   NO
Second Role System:   NO
Second Workflow:      NO
Privilege Expansion:  NONE IDENTIFIED

Status:
PRODUCTION-READY
WITH EXPLICIT DEFERRED HARDENING ITEMS
```

---

# 30. Baseline Rule

**Future Claude Code sessions must treat this document as an architectural constraint, not merely historical documentation.**

Do not redesign the authorization architecture because an individual feature is inconvenient to implement.

First locate the existing authorization path.

Extend it only when the existing architecture demonstrably cannot satisfy the requirement.

Any proposal that introduces a new authorization engine, Role system, permission source of truth, workflow engine, policy language, or unnecessary schema must stop and produce an architecture-impact report before implementation.
