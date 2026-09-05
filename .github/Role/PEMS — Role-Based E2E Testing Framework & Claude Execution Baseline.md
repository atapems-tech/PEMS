# PEMS — Role-Based E2E Testing Framework
## Role-Based End-to-End Testing & Security Validation

**Status:** APPROVED TESTING BASELINE  
**Applies To:** PEMS Web Application  
**Primary Use:** CMMS-10a and all subsequent PEMS phases  
**Execution Agent:** Claude  
**Testing Approach:** Browser-based E2E + API/Service authorization validation  
**Objective:** Validate PEMS as real users with real roles, permissions, workflows, and security boundaries.

---

# 1. Objective

Claude must test PEMS using **real test accounts mapped to real PEMS roles**.

Testing must not be limited to:

- Compilation
- Unit tests
- Controller tests
- Database tests
- Admin-user testing
- UI button visibility

The system must be validated from the perspective of actual operational users.

The core principle is:

```text
Role
  ↓
Authentication
  ↓
Authorization
  ↓
UI
  ↓
Application/API
  ↓
Database
  ↓
Workflow
  ↓
Audit Trail
```

A test is considered successful only when the complete chain behaves correctly.

---

# 2. Testing Philosophy

PEMS must be tested using two complementary approaches.

## Positive Authorization Test

Verify that a user **CAN perform** actions assigned to their role.

Example:

```text
Permit Issuer
    ↓
Issue Permit
    ↓
Expected: SUCCESS
```

## Negative Authorization Test

Verify that a user **CANNOT perform** actions outside their role.

Example:

```text
Permit Receiver
    ↓
Issue Permit
    ↓
Expected: ACCESS DENIED
```

Both tests are mandatory.

---

# 3. Test Accounts

Create or identify dedicated test users.

Do NOT use production users for automated testing.

Initial test-role matrix:

| Test User | Role | Primary Purpose |
|---|---|---|
| TEST-ADMIN | System Administrator | Full-system validation |
| TEST-MAINT-MGR | Maintenance Manager | Maintenance authorization |
| TEST-MAINT-ENG | Maintenance Engineer | Engineering/maintenance operations |
| TEST-PLANNER | Maintenance Planner | Planning/work management |
| TEST-PROD-ENG | Production Engineer | Production interaction |
| TEST-PERMIT-ISSUER | Permit Issuer | Permit issuance |
| TEST-PERMIT-RECEIVER | Permit Receiver | Permit request/receipt |
| TEST-HSE | HSE Officer | HSE review/control |
| TEST-ISOLATION | Isolation Authority | Isolation verification |
| TEST-WAREHOUSE | Warehouse Officer | Materials/inventory |
| TEST-ENGINEERING | Engineering User | Engineering functions |
| TEST-READONLY | Read Only | Restricted access validation |

The exact role names must follow the existing PEMS role definitions.

Do not create duplicate roles if equivalent roles already exist.

---

# 4. Test Environment

Testing must be performed against a dedicated non-production environment.

Required:

```text
PEMS Application
PEMS Database
Authentication
Authorization
Seeded Test Data
Browser
Playwright
Test Users
```

Recommended:

```text
Development / QA Environment
```

Never run destructive automated tests against production.

---

# 5. Authentication Strategy

Each test role must have an independent authenticated session.

Preferred structure:

```text
playwright/
└── .auth/
    ├── admin.json
    ├── maintenance-manager.json
    ├── maintenance-engineer.json
    ├── planner.json
    ├── production-engineer.json
    ├── permit-issuer.json
    ├── permit-receiver.json
    ├── hse.json
    ├── isolation-authority.json
    ├── warehouse.json
    ├── engineering.json
    └── readonly.json
```

Authentication state files must:

- Never contain production credentials
- Never be committed to Git
- Be excluded through `.gitignore`
- Be generated specifically for the test environment

If PEMS uses a different authentication mechanism, Claude must adapt to the existing architecture rather than replacing it.

---

# 6. Browser Automation

Use Playwright or the existing PEMS-compatible browser automation framework.

Claude must interact with PEMS through the actual user interface where practical.

Tests should simulate:

```text
Login
Navigate
Search
Create
Edit
Submit
Approve
Reject
Issue
Suspend
Resume
Close
Cancel
Revoke
View
Filter
```

Do not replace E2E tests with direct database manipulation.

Database manipulation may be used only for:

- Test-data setup
- Controlled cleanup
- Verification
- Fixture preparation

---

# 7. Role Isolation

Every test must explicitly identify the active role.

Example:

```text
TEST-PERMIT-ISSUER
Role = Permit Issuer
```

The test report must never simply say:

```text
Permit Issue Test = PASS
```

It must state:

```text
Role: Permit Issuer
Action: Issue Permit
Expected: Allowed
Actual: Allowed
Result: PASS
```

For negative tests:

```text
Role: Permit Receiver
Action: Issue Permit
Expected: Denied
Actual: Denied
Result: PASS
```

---

# 8. Authorization Test Matrix

Claude must build and maintain a Role × Module × Action matrix.

Initial structure:

| Module | Action | Admin | Maint. Mgr | Planner | Permit Issuer | Permit Receiver | HSE | Isolation | Read Only |
|---|---|---|---|---|---|---|---|---|---|
| Permit | View | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Permit | Create | ✓ | ✓ | ✓ | ✓ | ✓ | policy | policy | ✕ |
| Permit | Edit | ✓ | policy | policy | ✓ | own | policy | policy | ✕ |
| Permit | Submit | ✓ | ✓ | ✓ | ✓ | ✓ | policy | policy | ✕ |
| Permit | Approve | ✓ | ✓ | policy | policy | ✕ | ✓ | policy | ✕ |
| Permit | Issue | ✓ | policy | ✕ | ✓ | ✕ | policy | policy | ✕ |
| Permit | Suspend | ✓ | policy | ✕ | ✓ | ✕ | ✓ | policy | ✕ |
| Permit | Resume | ✓ | policy | ✕ | ✓ | ✕ | policy | policy | ✕ |
| Permit | Revoke | ✓ | policy | ✕ | ✓ | ✕ | ✓ | policy | ✕ |
| Permit | Close | ✓ | ✓ | policy | ✓ | policy | policy | policy | ✕ |

The table above is an initial testing template.

Claude MUST NOT assume that these permissions are the final business rules.

The existing PEMS authorization model and approved architecture must be inspected first.

Where policy is unknown, mark:

```text
UNDEFINED — REQUIRES BUSINESS DECISION
```

Do not silently grant access.

---

# 9. UI Authorization Validation

For each action, test:

### 1. Navigation

Can the user reach the relevant module?

### 2. Visibility

Is the action visible when authorized?

### 3. UI Enforcement

Is the action unavailable when unauthorized?

### 4. Server Enforcement

Can the user bypass the UI and invoke the operation directly?

### 5. Audit

Is the action recorded correctly?

A hidden button alone is NOT considered authorization.

---

# 10. Server/API Authorization

For every security-sensitive action, Claude must attempt to validate server-side enforcement.

Example:

```text
Permit Receiver
     ↓
Direct API request
     ↓
POST /api/permits/{id}/issue
```

Expected:

```text
403 Forbidden
```

or the equivalent PEMS authorization response.

If the UI hides the button but the API accepts the request:

```text
RESULT = FAIL
SECURITY DEFECT
```

This rule is mandatory.

---

# 11. CMMS-10a E2E Scenario

The first complete cross-role scenario must be:

```text
1. Permit Receiver logs in
2. Creates Permit
3. Completes required information
4. Submits Permit
5. Permit Issuer logs in
6. Reviews Permit
7. Approves Permit where authorized
8. Issues Permit
9. Permit becomes Active
10. Permit Issuer suspends Permit
11. Permit Issuer resumes Permit
12. Permit is closed
13. Audit history is inspected
```

At every step verify:

```text
Correct Role
Correct Action
Correct State
Correct UI
Correct Server Authorization
Correct Audit Entry
```

---

# 12. Contractor Personnel Scenario

CMMS-10a must explicitly test contractor personnel.

Scenario:

```text
1. Admin creates Vendor
2. Admin creates Employee
3. Employee.PersonnelType = Contractor
4. Employee.VendorId = Vendor.Id
5. Assign Contractor Employee as Permit Receiver
6. Create Permit
7. Select Contractor Receiver
8. Submit Permit
9. Verify Permit correctly displays:
   - Employee
   - Personnel Type
   - Vendor
10. Execute permitted lifecycle
11. Verify audit trail
```

Expected:

```text
Contractor personnel behave as valid PEMS personnel.
```

No parallel ContractorEmployee entity may be required.

---

# 13. Negative Security Tests

Claude must deliberately attempt unauthorized operations.

Examples:

```text
Permit Receiver → Issue Permit
Permit Receiver → Revoke Permit
Permit Receiver → Verify Isolation
Read Only → Edit Permit
Read Only → Create Permit
Warehouse Officer → Approve Permit
Maintenance Engineer → Emergency Override
Unauthorized User → Direct API Issue
Unauthorized User → Direct API Close
```

Expected result:

```text
Denied
```

The exact response code/message must follow PEMS conventions.

---

# 14. State-Machine Security Tests

Authorization and state validation must be tested together.

Examples:

```text
Closed → Issue
Closed → Resume
Cancelled → Activate
Revoked → Resume
Draft → Close
Submitted → Resume
```

Expected:

```text
Operation rejected
State unchanged
Audit does not falsely record successful transition
```

---

# 15. Cross-Role Workflow Test

The most important test category is cross-role execution.

Example:

```text
Permit Receiver
       ↓
Create
       ↓
Submit
       ↓
Permit Issuer
       ↓
Review
       ↓
Approve
       ↓
Issue
       ↓
Operations / Maintenance
       ↓
Execute
       ↓
Permit Issuer
       ↓
Close
```

Claude must validate that one user's permissions do not accidentally allow another user to perform another user's responsibilities.

---

# 16. Session Isolation

Verify that changing users does not leak authorization state.

Test:

```text
Login as Admin
Logout

Login as Permit Receiver
```

Verify that the Permit Receiver does not retain:

```text
Admin menus
Admin actions
Admin API permissions
Admin cached data
```

Also test browser refresh and new-tab behavior.

---

# 17. Multi-User Concurrency

Where practical, test simultaneous users.

Example:

```text
Permit Issuer A
       +
Permit Issuer B
       ↓
Same Permit
```

Verify:

- State consistency
- No duplicate transitions
- No unauthorized overwrite
- Correct optimistic concurrency behavior if implemented
- Correct audit history

---

# 18. Audit Validation

Every security-sensitive operation must produce a verifiable audit event.

Minimum information:

```text
User
Role
Action
Module
Entity
EntityId
Timestamp
Result
Previous State
New State
Reason where applicable
```

Example:

```text
User: TEST-PERMIT-ISSUER
Role: Permit Issuer
Module: Permit
Action: Issue
PermitId: PTW-000123
PreviousState: Approved
NewState: Issued
Result: Success
Timestamp: ...
```

For denied actions, the security/audit strategy must follow the existing PEMS design.

---

# 19. Test Data Strategy

Use deterministic test data.

Examples:

```text
TEST-VENDOR-001
TEST-EMP-001
TEST-EMP-CONTRACTOR-001
TEST-WO-001
TEST-PERMIT-001
```

Test data must be:

- Clearly identifiable
- Repeatable
- Isolated
- Safe to delete/reset
- Not confused with production data

---

# 20. Test Automation Structure

Recommended structure:

```text
tests/
└── e2e/
    ├── auth/
    ├── roles/
    ├── permits/
    │   ├── create.spec
    │   ├── lifecycle.spec
    │   ├── authorization.spec
    │   ├── contractor.spec
    │   └── audit.spec
    ├── cmms/
    └── security/
```

The actual structure must follow the existing PEMS test architecture if one already exists.

Do not create a competing test framework without justification.

---

# 21. Test Naming

Tests must clearly identify:

```text
ROLE
MODULE
ACTION
EXPECTED RESULT
```

Example:

```text
[Permit Issuer] can issue an approved permit

[Permit Receiver] cannot issue a permit

[Read Only] cannot edit a permit

[HSE] can review permit

[Isolation Authority] can verify isolation
```

---

# 22. Test Reports

Claude must produce a machine-readable and human-readable test report.

Minimum summary:

```text
Total Tests
Passed
Failed
Blocked
Skipped
Security Failures
Workflow Failures
UI Failures
API Authorization Failures
```

Example:

```text
PEMS E2E ROLE TEST REPORT

Total: 142
Passed: 136
Failed: 4
Blocked: 2

Security Failures: 1
Workflow Failures: 2
UI Failures: 1
```

A test suite is NOT considered successful merely because most tests passed.

Critical authorization failures must block phase completion.

---

# 23. Failure Classification

Every failure must be classified:

```text
AUTHENTICATION
AUTHORIZATION
UI
API
DOMAIN
DATABASE
WORKFLOW
AUDIT
DATA
ENVIRONMENT
TEST
```

Example:

```text
FAIL-001
Category: AUTHORIZATION
Role: Permit Receiver
Action: Issue Permit
Expected: 403
Actual: 200
Severity: CRITICAL
```

---

# 24. Severity

Use:

```text
CRITICAL
HIGH
MEDIUM
LOW
```

### CRITICAL

Examples:

- Unauthorized user can issue Permit
- Unauthorized API operation succeeds
- Role isolation failure
- Privilege escalation
- Production data affected

### HIGH

Examples:

- Valid role cannot perform mandatory action
- Workflow state corruption
- Audit trail missing for critical action

### MEDIUM

Examples:

- UI authorization display mismatch
- Incorrect role-specific navigation
- Non-critical audit metadata missing

### LOW

Examples:

- Cosmetic test issue
- Non-critical message mismatch

---

# 25. Definition of Done

Role-Based E2E Testing for a phase is complete only when:

- [ ] Test users exist
- [ ] Roles are mapped
- [ ] Authentication works independently per role
- [ ] Positive authorization tests pass
- [ ] Negative authorization tests pass
- [ ] UI authorization tests pass
- [ ] Server/API authorization tests pass
- [ ] State-machine tests pass
- [ ] Cross-role workflow passes
- [ ] Contractor personnel scenario passes
- [ ] Audit trail is verified
- [ ] Session isolation passes
- [ ] No critical security defect remains
- [ ] Test report is generated
- [ ] Failed tests are classified
- [ ] Evidence is retained
- [ ] Existing regression tests pass

---

# 26. Mandatory Security Gate

The following automatically blocks phase completion:

```text
Unauthorized user can perform privileged action
```

Examples:

```text
ReadOnly → Edit
PermitReceiver → Issue
Unauthorized → Approve
Unauthorized → Revoke
Unauthorized → VerifyIsolation
Unauthorized → EmergencyOverride
```

If any of these succeed:

```text
PHASE STATUS = FAILED
```

regardless of the number of other passing tests.

---

# 27. Evidence Requirements

For important E2E tests, retain evidence where practical:

```text
Screenshot
Video
Trace
Console Log
Network Result
API Response
Database Verification
Audit Record
```

For security failures, retain enough evidence to reproduce the defect.

Do not store credentials in evidence.

---

# 28. Regression Requirement

Every new PEMS phase must run:

```text
New Phase Tests
+
Role-Based Authorization Tests
+
Relevant Existing Regression Tests
```

A new feature must not silently break an existing role.

Example:

After CMMS-10a:

```text
Permit tests
+
CMMS existing tests
+
Authentication tests
+
Authorization regression
```

---

# 29. Claude Execution Protocol

Claude must follow:

```text
STEP 1
Inspect existing PEMS authentication.

STEP 2
Inspect existing PEMS roles.

STEP 3
Inspect existing PEMS authorization.

STEP 4
Inspect existing test framework.

STEP 5
Inspect Playwright/browser automation availability.

STEP 6
Inspect test-user creation mechanism.

STEP 7
Prepare Role × Module × Action matrix.

STEP 8
Prepare E2E test plan.

STEP 9
Request/confirm test credentials through secure environment configuration.

STEP 10
Run authentication tests.

STEP 11
Run positive authorization tests.

STEP 12
Run negative authorization tests.

STEP 13
Run workflow tests.

STEP 14
Run API/server authorization tests.

STEP 15
Run audit verification.

STEP 16
Generate report.

STEP 17
Classify failures.

STEP 18
Fix only authorized defects.

STEP 19
Re-run failed tests.

STEP 20
Run regression.

STEP 21
Produce final evidence package.
```

---

# 30. Mandatory Restrictions

Claude MUST NOT:

- Use production accounts
- Store plaintext production passwords
- Commit authentication state
- Bypass authorization to make tests pass
- Modify permissions merely to make tests green
- Disable security middleware
- Disable CSRF/security controls
- Modify production data
- Mark failed tests as passed
- Skip negative authorization tests
- Assume UI hiding equals authorization
- Introduce a second authentication framework
- Introduce a second authorization framework without approval

---

# 31. First Task — AUDIT ONLY

Before implementing or changing the testing framework, Claude must inspect the repository.

The first output must contain:

```text
PEMS ROLE-BASED E2E TEST AUDIT
```

including:

1. Authentication architecture
2. Existing roles
3. Existing permissions
4. Existing authorization middleware/policies
5. Existing test infrastructure
6. Existing Playwright/Selenium/browser tooling
7. Existing test accounts
8. Existing seed data
9. Existing audit system
10. Existing API authorization
11. Existing CI test execution
12. Recommended test architecture
13. Required files
14. Required dependencies
15. Risks
16. Implementation sequence

No code changes are allowed during this first audit.

---

# 32. CMMS-10a Integration

For CMMS-10a, the following tests are mandatory before phase closure:

```text
Role Authentication
        ↓
Permit Create
        ↓
Permit Submit
        ↓
Permit Review
        ↓
Permit Approve
        ↓
Permit Issue
        ↓
Permit Activate
        ↓
Permit Suspend
        ↓
Permit Resume
        ↓
Permit Close
        ↓
Audit Verification
```

Run the workflow across appropriate users rather than using Admin for every step.

---

# 33. Final Acceptance Rule

PEMS must be considered operationally tested only when:

```text
The correct user
can perform
the correct action
on the correct entity
in the correct state
through the correct interface
and the server enforces the same rule
with a correct audit trail.
```

This is the acceptance standard for CMMS-10a and should become the standard testing model for subsequent PEMS phases.

---

# FINAL DIRECTIVE TO CLAUDE

**Start with AUDIT ONLY.**

**Do not modify the repository.**

**Do not create test accounts automatically unless explicitly authorized by the existing environment/security process.**

**Do not change application authorization to make tests pass.**

First inspect the current PEMS authentication, authorization, roles, test infrastructure, and browser automation capabilities.

Then return:

```text
PEMS ROLE-BASED E2E TEST AUDIT
```

followed by the proposed implementation plan.

**WAIT FOR IMPLEMENTATION AUTHORIZATION AFTER THE AUDIT.**

# PEMS — UI/UX Quality & Acceptance Rule

**Status:** MANDATORY  
**Applies To:** All PEMS Modules and Phases  
**Primary Target:** Web UI / Responsive UI / Role-Based UI  
**Enforcement Agent:** Claude  
**Acceptance Level:** Production Grade

---

# 1. Purpose

The PEMS user interface is not considered complete merely because:

- the page loads,
- the API works,
- the CRUD operation works,
- the database is correct,
- or the automated test passes.

Every implemented feature must provide a **production-grade operational user experience**.

The UI must be:

```text
Functional
+
Consistent
+
Responsive
+
Accessible
+
Role-Aware
+
Operationally Clear
+
Error-Tolerant
+
Persian/RTL Compatible
+
Visually Consistent
```

---

# 2. Core UI Principle

Claude must implement the UI from the perspective of the actual PEMS user.

The question is not:

> "Does the developer's screen work?"

The question is:

> "Can an actual plant user understand what is happening and complete the task safely, quickly, and without ambiguity?"

---

# 3. UI Must Follow Existing PEMS Design System

Before creating or modifying any UI, Claude MUST inspect the existing PEMS:

```text
Layouts
Navigation
Components
CSS
Colors
Typography
Buttons
Forms
Tables
Cards
Dialogs
Notifications
Icons
Spacing
Responsive behavior
RTL implementation
```

Do not introduce a second visual language.

If an existing reusable component exists:

```text
REUSE
```

Do not create another visually similar component.

---

# 4. No One-Off UI

Avoid isolated implementations such as:

```text
Custom button for one page
Custom table for one module
Custom modal for one feature
Custom form layout for one entity
Custom notification style
```

Prefer reusable components.

The UI architecture should move toward:

```text
PEMS Design System
        ↓
Reusable Components
        ↓
Module Screens
        ↓
Feature Screens
```

---

# 5. Role-Aware UI

The UI must reflect the user's actual authorization.

For every action:

```text
Authorized
    → Visible + Enabled

Unauthorized
    → Not Available
```

However:

**UI visibility is NOT the security boundary.**

Server-side authorization remains mandatory.

The UI must never expose actions that the current role cannot legitimately perform unless there is a clear UX reason and the action is visibly disabled with an explanation.

---

# 6. Action Hierarchy

Primary actions must be visually obvious.

Example:

```text
Primary
Approve
Issue
Submit
Save

Secondary
Edit
Review
Print

Destructive
Delete
Cancel
Revoke
Reject
```

Destructive actions must never appear visually equivalent to normal actions.

---

# 7. Workflow Visibility

For workflow-driven modules such as Permit, Work Order, Approval, Inspection, and Documents, the current state must always be clear.

Example:

```text
DRAFT
  ↓
SUBMITTED
  ↓
UNDER REVIEW
  ↓
APPROVED
  ↓
ISSUED
  ↓
ACTIVE
  ↓
CLOSED
```

The UI should communicate:

```text
Current State
Available Actions
Next Expected Step
Responsible Role
Validity / Deadline
```

A user must not have to infer the state from unrelated fields.

---

# 8. Status Design

Status must not depend exclusively on color.

Do not use:

```text
Red = Rejected
Green = Approved
```

without also displaying textual status.

Use:

```text
[APPROVED]
[UNDER REVIEW]
[SUSPENDED]
[REVOKED]
```

Color may reinforce meaning but must not be the only indicator.

---

# 9. Forms

Forms must be designed for operational use.

Every important form must clearly communicate:

```text
Field Label
Required / Optional
Expected Format
Validation
Units
Allowed Values
Error
Help / Explanation where necessary
```

Do not rely on placeholder text as the field label.

---

# 10. Validation

Validation must occur:

```text
Client/UI
+
Server
```

The UI must show validation errors close to the relevant field.

Bad:

```text
Error occurred.
```

Good:

```text
Valid To must be later than Valid From.
```

Validation messages must be understandable to the operational user.

---

# 11. Error Handling

Never expose:

```text
Stack Trace
Exception Details
SQL Error
Internal Class Name
Database Connection String
Developer Debug Information
```

to normal users.

Instead display:

```text
What happened
What the user can do
Whether the operation was completed
```

Example:

```text
Permit could not be issued.

The Permit is not in an issuable state.
Current state: Under Review.
```

---

# 12. Loading States

Every asynchronous operation must provide clear feedback.

Examples:

```text
Loading...
Saving...
Submitting...
Processing...
Generating...
```

Do not allow the user to wonder whether the system is frozen.

Prevent accidental double submission where appropriate.

---

# 13. Empty States

Every data-driven screen must have a meaningful empty state.

Bad:

```text
No data.
```

Better:

```text
No open Permits found.

Try changing the filters or create a new Permit.
```

---

# 14. Tables

Operational tables must support where appropriate:

```text
Search
Filter
Sort
Pagination
Column clarity
Status
Action visibility
Responsive behavior
```

Do not overload tables with excessive columns.

Important operational information must appear before secondary metadata.

---

# 15. Dashboard Rules

Dashboards must answer operational questions.

Do not create dashboards merely because charts look attractive.

Every KPI should have:

```text
Metric Name
Value
Unit
Time Period
Context
Trend where useful
```

Example:

```text
Open Permits
17
Current Shift
↑ 3 vs Previous Shift
```

---

# 16. Responsive Design

PEMS must remain usable on:

```text
Desktop
Laptop
Tablet
Mobile
```

At minimum test common viewport classes.

The UI must not produce:

```text
Horizontal overflow
Overlapping controls
Hidden buttons
Unreadable tables
Broken modals
Clipped text
```

---

# 17. RTL / Persian

Persian UI must be genuinely RTL.

Do not merely apply:

```css
direction: rtl;
```

and consider the task complete.

Verify:

```text
Layout
Navigation
Tables
Forms
Dialogs
Icons
Alignment
Numbers
Dates
Breadcrumbs
Pagination
Dropdowns
```

Mixed Persian/English technical data must remain readable.

Technical identifiers such as:

```text
Permit No.
Equipment Tag
Work Order
Document Number
```

must not become visually corrupted by RTL rendering.

---

# 18. Bilingual Support

Where PEMS bilingual support exists, UI components must support:

```text
Persian
English
```

without requiring separate duplicated screens.

Do not hardcode visible text inside controllers or business logic.

Use the existing localization architecture.

---

# 19. Date / Time / Number Formatting

Dates and times must follow PEMS localization rules.

The UI must clearly distinguish:

```text
Date
Time
Duration
Validity
Expiry
```

Do not create inconsistent date formats across modules.

---

# 20. Confirmation for Critical Actions

Critical operational actions require confirmation where appropriate.

Examples:

```text
Issue Permit
Suspend Permit
Revoke Permit
Reject Permit
Cancel Permit
Delete
Emergency Override
```

Confirmation should communicate consequences.

Example:

```text
Suspend Permit?

This will prevent execution under this Permit
until it is resumed.

[Cancel] [Suspend Permit]
```

Do not use confirmation dialogs for trivial actions.

---

# 21. Unsaved Changes

Where forms are complex or operationally important, warn the user before losing unsaved data.

Example:

```text
You have unsaved changes.

Leave this page?
```

---

# 22. Accessibility

UI must target practical accessibility standards.

At minimum verify:

```text
Keyboard navigation
Visible focus
Readable text
Sufficient contrast
Meaningful labels
Form accessibility
Dialog accessibility
```

Do not rely exclusively on icons.

Important actions should have text or accessible labels.

---

# 23. Icon Rules

Icons must support meaning, not replace it unnecessarily.

Avoid ambiguous icons for critical actions.

For example:

```text
Revoke
Delete
Suspend
Close
```

must not rely on obscure icons alone.

Tooltips should be provided where an icon-only control is justified.

---

# 24. Notifications

Notifications must clearly distinguish:

```text
Success
Information
Warning
Error
```

Example:

```text
✓ Permit submitted successfully.

⚠ Permit cannot be issued while it is Under Review.

✕ Unable to save Permit.
```

Do not use generic:

```text
Operation successful.
```

when the user needs more context.

---

# 25. Performance

UI must not make unnecessary requests.

Claude must inspect for:

```text
Duplicate API calls
N+1 requests
Repeated database loads
Large unpaginated datasets
Unnecessary full-page reloads
Blocking UI operations
```

Performance problems discovered during UI testing must be reported.

---

# 26. Navigation

Users must always understand:

```text
Where am I?
How did I get here?
What can I do?
Where can I go next?
```

Use consistent:

```text
Sidebar
Breadcrumb
Page Title
Tabs
Back navigation
```

Do not create isolated navigation patterns for individual modules.

---

# 27. Detail Pages

Important operational entities should provide a clear information hierarchy.

Recommended:

```text
Header
 ├── Entity Number
 ├── Title
 ├── Status
 └── Primary Actions

Summary
 ├── Critical information
 └── Operational metadata

Tabs / Sections
 ├── Details
 ├── Related Work
 ├── Documents
 ├── Workflow
 ├── Audit
 └── History
```

Use only sections relevant to the entity.

---

# 28. Mobile / Small Screen Behavior

On small screens:

```text
Primary actions remain accessible
Critical status remains visible
Forms remain usable
Tables adapt appropriately
```

Do not simply shrink desktop UI.

Where necessary:

```text
Table → Card/List
Multi-column form → Single column
Sidebar → Drawer
```

---

# 29. UI Regression

Every new phase must test existing UI.

At minimum:

```text
Login
Navigation
Dashboard
Relevant existing module
New module
Role-specific UI
Responsive behavior
RTL
Error handling
```

A new feature must not visually or functionally break an existing module.

---

# 30. UI Test Matrix

Claude must maintain:

| Test | Desktop | Tablet | Mobile | RTL | English | Role |
|---|---|---|---|---|---|---|
| Login | ✓ | ✓ | ✓ | ✓ | ✓ | All |
| Dashboard | ✓ | ✓ | ✓ | ✓ | ✓ | Role |
| Permit List | ✓ | ✓ | ✓ | ✓ | ✓ | Relevant Roles |
| Permit Create | ✓ | ✓ | ✓ | ✓ | ✓ | Authorized |
| Permit Detail | ✓ | ✓ | ✓ | ✓ | ✓ | Relevant Roles |
| Permit Workflow | ✓ | ✓ | ✓ | ✓ | ✓ | Relevant Roles |
| Error Handling | ✓ | ✓ | ✓ | ✓ | ✓ | Relevant Roles |

The exact matrix may be expanded according to module complexity.

---

# 31. Visual QA

After implementation, Claude must perform a visual inspection.

Check for:

```text
Alignment
Spacing
Typography
Consistency
Overflow
Broken components
Incorrect icons
Status clarity
Button hierarchy
Form usability
Responsive layout
RTL correctness
```

Automated tests alone are insufficient.

Where browser tooling permits, capture screenshots at important checkpoints.

---

# 32. UI Acceptance Criteria

A screen is NOT accepted if:

```text
Functionality works
BUT
UI is confusing
```

or:

```text
Desktop works
BUT
Mobile is broken
```

or:

```text
English works
BUT
Persian RTL is broken
```

or:

```text
Button is hidden
BUT
API allows unauthorized operation
```

or:

```text
Form saves
BUT
validation is unclear
```

---

# 33. Mandatory UI Definition of Done

Every implemented UI feature must satisfy:

- [ ] Uses existing PEMS design system
- [ ] No unnecessary duplicate components
- [ ] Role-aware UI
- [ ] Server-side authorization remains enforced
- [ ] Clear workflow/state visibility
- [ ] Clear validation
- [ ] Clear error handling
- [ ] Loading states implemented
- [ ] Empty states implemented
- [ ] Responsive behavior verified
- [ ] RTL verified
- [ ] English verified where applicable
- [ ] Accessibility basics verified
- [ ] Critical actions confirmed where appropriate
- [ ] No sensitive technical errors exposed
- [ ] No unnecessary API calls identified
- [ ] Existing UI regression tested
- [ ] Visual QA completed
- [ ] Screenshots/evidence captured where appropriate

---

# 34. Claude's Required UI Audit Before Coding

Before modifying any screen, Claude must inspect:

```text
Current Screen
Current Layout
Current Components
Current CSS
Current Design Tokens
Current Navigation
Current Localization
Current Responsive Behavior
Existing Similar Screens
```

Then report:

```text
UI AUDIT

Existing Components:
Reusable Components:
Required Changes:
New Components:
Visual Risks:
Responsive Risks:
RTL Risks:
Accessibility Risks:
```

Only then should implementation begin.

---

# 35. UI Quality Gate

The final phase status must include:

```text
FUNCTIONAL TEST       PASS / FAIL
AUTHORIZATION TEST    PASS / FAIL
E2E TEST              PASS / FAIL
VISUAL QA             PASS / FAIL
RESPONSIVE QA         PASS / FAIL
RTL QA                PASS / FAIL
ACCESSIBILITY QA      PASS / FAIL
REGRESSION QA         PASS / FAIL
```

If any **Critical** UI/security defect exists:

```text
PHASE STATUS = FAILED
```

---

# FINAL DIRECTIVE

PEMS UI must be treated as an **operational system interface**, not merely a presentation layer.

Claude must therefore validate:

```text
User
 ↓
Role
 ↓
Permission
 ↓
UI
 ↓
Workflow
 ↓
API
 ↓
Database
 ↓
Audit
```

A feature is complete only when the entire chain is correct.

**Do not declare a phase complete based solely on compilation, API success, or functional tests.**

**UI/UX Quality is part of the Definition of Done for every PEMS phase.**