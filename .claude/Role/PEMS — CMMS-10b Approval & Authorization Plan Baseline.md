# PEMS — CMMS-10b Approval & Authorization

**Phase:** CMMS-10b  
**Status:** PLAN MODE ONLY  
**Predecessor:** CMMS-10a — Core Permit Lifecycle  
**Objective:** Real multi-role Permit approval and authorization using the existing PEMS workflow runtime.

---

## 1. Mission

Extend the completed CMMS-10a Permit lifecycle with a **real, configurable, multi-role approval workflow**.

The target is:

```text
Permit
  ↓
Submit
  ↓
Workflow Runtime
  ↓
Approval Chain
  ↓
Role / Action Authorization
  ↓
Approve / Reject / Return
  ↓
Next Approval Step
  ↓
Final Approval
  ↓
Permit Issue eligibility
```

Do not replace the existing `IWorkflowRuntime`.

Do not create a second workflow engine.

---

# 2. HARD SCOPE

CMMS-10b covers only:

- Approval workflow definition
- Approval-chain execution
- Role-based approval
- Sequential approval
- Parallel approval where already supported by the runtime
- Approve
- Reject
- Return / Rework where supported
- Approval history
- Authorization enforcement
- Approval UI
- E2E multi-role testing

Explicitly OUT OF SCOPE:

```text
LOTO
Gas Testing
SIMOPS
Shift Handover
Emergency Override
Execution Gate
Advanced KPI/Dashboard
```

Those belong to CMMS-10c through 10g.

---

# 3. MANDATORY FIRST STEP — AUDIT ONLY

Before changing code, inspect the repository.

Do NOT implement anything yet.

Audit:

```text
IWorkflowRuntime
Workflow definitions
Workflow instances
Workflow steps
Workflow actors
Approval entities
Approval history
Role
RolePermission
RoleModuleAction
User → Role
Permit
PermitStatusHistory
Existing approval UI
Existing workflow UI
Existing authorization middleware/policies
Existing tests
```

Also search for all usages of:

```text
IWorkflowRuntime
WorkflowRuntime
WorkflowDefinition
WorkflowInstance
Approval
Approver
RolePermission
RoleModuleAction
```

Do not assume names or structure.

---

# 4. ARCHITECTURAL RULE

The existing workflow engine is the system of record for workflow execution.

Do NOT create:

```text
PermitApprovalEngine
PermitWorkflowEngine
PermitApprovalService
```

if their purpose duplicates `IWorkflowRuntime`.

A Permit-specific application service may orchestrate Permit operations, but workflow execution must remain centralized.

Target:

```text
Permit Service
     ↓
IWorkflowRuntime
     ↓
Workflow Definition
     ↓
Workflow Instance
     ↓
Approval Step
     ↓
Actor Resolution
     ↓
Role / Action Authorization
```

---

# 5. AUTHORIZATION MODEL

CMMS-10a established:

```text
RoleModuleAction
```

CMMS-10b must determine how it integrates with workflow approval.

Do NOT delete or silently modify the existing:

```text
RolePermission
```

system.

First determine whether:

```text
RolePermission
```

is legacy/general application authorization while:

```text
RoleModuleAction
```

is operational/action authorization.

Document the final relationship.

---

# 6. REQUIRED APPROVAL CONCEPT

The workflow must be capable of expressing:

```text
Step
 ├── Required Role
 ├── Required Action
 ├── Sequence
 ├── Status
 ├── Approver
 ├── Decision
 ├── Decision Time
 └── Comment
```

Example:

```text
Permit Submitted
      ↓
HSE Review
      ↓
Maintenance Approval
      ↓
Operations Approval
      ↓
Permit Issuer
      ↓
Issue Eligible
```

The exact chain must come from existing PEMS workflow capabilities and business rules.

Do not hardcode this example as the universal Permit workflow.

---

# 7. ACTOR RESOLUTION

The audit must determine how the runtime resolves an approver.

Supported concepts may include:

```text
Specific User
Role
Department
Discipline
Position
Team
Dynamic Actor
```

Do not introduce new actor concepts unless the existing runtime cannot satisfy the requirement.

For Contractor personnel, ensure the existing:

```text
Employee.PersonnelType
Employee.VendorId
```

model remains compatible.

Do not rework the Employee model during 10b unless a blocking architectural defect is discovered.

---

# 8. APPROVAL DECISIONS

Evaluate existing runtime support for:

```text
Approve
Reject
Return
Request Changes
Delegate
Escalate
```

CMMS-10b minimum:

```text
Approve
Reject
Return / Rework
```

If Delegate/Escalate are not supported and are not required for the current approved scope, document them as future capability.

Do not implement speculative features.

---

# 9. STATE INTEGRATION

Permit state transitions remain controlled by the CMMS-10a state machine.

Workflow approval must NOT directly manipulate:

```text
Permit.Status
```

through arbitrary assignment.

Instead:

```text
Workflow Decision
      ↓
Authorized Permit Transition
      ↓
Permit State Machine
      ↓
Audit
```

Example:

```text
Final Approval
      ↓
Permit becomes Approved
```

only through the existing validated Permit transition mechanism.

---

# 10. ISSUE GATE

CMMS-10b must establish the rule:

```text
Permit cannot be Issued
until all mandatory approval requirements are satisfied.
```

The exact enforcement location must be determined during audit.

Preferred:

```text
Permit Service
    ↓
Approval Completion Check
    ↓
State Transition
```

Do not rely solely on UI button visibility.

---

# 11. REJECTION / RETURN

Define the difference between:

```text
Reject
Return / Rework
```

If the existing workflow runtime already distinguishes them, reuse that behavior.

If not, document the recommended semantic distinction before implementation.

Minimum expectation:

```text
Reject
→ approval process terminates or enters rejected state

Return / Rework
→ Permit returns to an editable/reviewable state
```

Do not invent semantics without confirming the existing domain model.

---

# 12. AUDIT

Every approval decision must be traceable.

At minimum:

```text
Permit
Workflow Instance
Workflow Step
Actor
Role
Action
Decision
Timestamp
Comment
Previous State
New State
```

Reuse existing workflow/audit infrastructure where possible.

Do not create competing approval-history systems.

---

# 13. UI REQUIREMENTS

The Permit UI must clearly show:

```text
Current Permit Status
Approval Status
Current Approval Step
Completed Steps
Pending Steps
Approver Role
Decision
Decision Date
Comments
Available Action
```

Example:

```text
Approval Progress

✓ HSE Review
✓ Maintenance Approval
● Operations Approval
○ Permit Issuer
```

The user must understand:

- What has happened
- What is pending
- Who is responsible
- What action is available
- Why an action is unavailable

---

# 14. ROLE-BASED UI

For each approval action:

```text
Authorized Role
    → action visible + executable

Unauthorized Role
    → action unavailable
```

But:

**UI visibility is not authorization.**

The server/runtime must enforce the same rule.

---

# 15. SECURITY TESTS

Mandatory negative tests:

```text
Unauthorized user → Approve
Unauthorized user → Reject
Unauthorized user → Return
Wrong role → Approve
Wrong role → Issue
Approver outside assigned chain → Approve
User attempts to skip approval step
User attempts to approve twice
User attempts to approve after workflow completion
User attempts to approve rejected Permit
```

All unauthorized operations must be rejected server-side.

---

# 16. MULTI-ROLE E2E TEST

At least one complete real workflow must use different users.

Example:

```text
User A
Role: Permit Receiver
        ↓
Submit Permit

User B
Role: HSE
        ↓
Approve

User C
Role: Maintenance Manager
        ↓
Approve

User D
Role: Operations / Permit Issuer
        ↓
Final Approval / Issue Eligibility
```

Do not perform every step using Admin.

---

# 17. APPROVAL MATRIX TEST

Create a test matrix:

| Step | Required Role | Correct User | Wrong User | Result |
|---|---|---|---|---|
| HSE Review | HSE | Allowed | Denied | PASS |
| Maintenance | Maintenance Manager | Allowed | Denied | PASS |
| Operations | Operations | Allowed | Denied | PASS |
| Final Approval | Configured Role | Allowed | Denied | PASS |

Actual roles must follow the PEMS business configuration discovered during audit.

---

# 18. CONCURRENCY

If the workflow runtime supports parallel approval, test:

```text
Approver A
+
Approver B
```

on the same workflow instance.

Verify:

- Duplicate decisions are prevented
- Completion criteria are correct
- Race conditions do not corrupt Permit state
- Audit history remains correct

If parallel approval is not supported by the existing runtime, document the gap rather than creating an independent mechanism.

---

# 19. REGRESSION

CMMS-10b must not break CMMS-10a.

Re-run:

```text
Permit Create
Permit Submit
Permit State Machine
Permit Audit
Contractor Employee
Employee → Vendor
WorkOrder → Permit
Authorization
```

The complete CMMS-10a E2E scenario must remain green.

---

# 20. KNOWN CARRY-FORWARD GAP

Do not silently modify the previously accepted:

```text
Employee.DepartmentId
Employee.DisciplineId
```

contractor-personnel behavior.

It remains a documented architectural gap unless CMMS-10b proves it is a blocking requirement.

If it becomes blocking:

```text
STOP
Document impact
Provide options
Request architectural approval
```

---

# 21. PLAN DELIVERABLE

The first Claude response must contain:

```text
CMMS-10b ARCHITECTURE & IMPLEMENTATION PLAN
```

with:

1. Existing workflow architecture
2. `IWorkflowRuntime` capabilities
3. Existing workflow entities
4. Existing approval entities
5. Existing actor-resolution mechanism
6. Relationship between `RolePermission` and `RoleModuleAction`
7. Permit integration point
8. Required changes
9. New entities, if genuinely required
10. UI changes
11. Authorization changes
12. Migration impact
13. Test strategy
14. E2E multi-role scenario
15. Security risks
16. Open decisions
17. Exact implementation sequence

---

# 22. STOP CONDITIONS

Claude MUST STOP before implementation if:

```text
IWorkflowRuntime cannot support required approval behavior
Existing authorization models conflict
Actor resolution is ambiguous
Approval state conflicts with Permit state machine
Migration requires destructive changes
Existing workflow data could be corrupted
A second workflow engine appears necessary
```

Report:

```text
Finding
Impact
Recommended Solution
Alternatives
Decision Required
```

Then wait.

---

# 23. IMPLEMENTATION GATE

Only after the audit and plan are reviewed and explicitly approved may Claude proceed:

```text
PLAN
 ↓
APPROVAL
 ↓
IMPLEMENT
 ↓
MIGRATE
 ↓
TEST
 ↓
E2E
 ↓
SECURITY TEST
 ↓
REGRESSION
 ↓
LIVE VERIFICATION
```

---

# 24. DEFINITION OF DONE

CMMS-10b is complete only when:

- [ ] Existing `IWorkflowRuntime` fully audited
- [ ] No duplicate workflow engine introduced
- [ ] Approval workflow integrated with Permit
- [ ] Multi-role approval works
- [ ] Approval authorization is server-enforced
- [ ] Approver actor resolution is deterministic
- [ ] Approve works
- [ ] Reject works
- [ ] Return/Rework works where supported
- [ ] Issue is blocked until required approvals complete
- [ ] Approval audit is complete
- [ ] Approval UI is operationally clear
- [ ] Positive role tests pass
- [ ] Negative authorization tests pass
- [ ] Multi-user E2E passes
- [ ] CMMS-10a regression passes
- [ ] No LOTO/Gas/SIMOPS/10c–10g scope leaked into this phase
- [ ] No unresolved CRITICAL security defect remains

---

# FINAL DIRECTIVE TO CLAUDE

**CMMS-10a is CLOSED and ACCEPTED.**

**Start CMMS-10b in PLAN MODE ONLY.**

Do not modify application code.

Do not create migrations.

Do not change database schema.

Do not alter the existing workflow engine.

First audit `IWorkflowRuntime`, workflow definitions, approval infrastructure, actor resolution, and the relationship between `RolePermission` and `RoleModuleAction`.

Then return:

```text
CMMS-10b ARCHITECTURE & IMPLEMENTATION PLAN
```

**WAIT FOR EXPLICIT IMPLEMENTATION AUTHORIZATION.**