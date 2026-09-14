# PEMS HELP GOVERNANCE BASELINE

## Mandatory Help Synchronization Standard

**Baseline ID:** PEMS-HLP-BASELINE-001  
**Status:** MANDATORY  
**Scope:** Entire PEMS Repository  
**Applies To:** All future development, modification, refactoring and feature implementation  
**Audience:** Claude, Developers, Architects, Reviewers, QA

---

# 1. CORE RULE

## NO FUNCTIONAL CHANGE WITHOUT CORRESPONDING HELP

From this baseline onward:

> **Every functional change made to PEMS MUST result in the creation, update, or explicit verification of the corresponding Help content.**

This rule is mandatory.

A development task is **NOT considered complete** if:

- a new module was implemented but Help was not created
- a new page was added but contextual Help was not added
- an existing workflow was changed but its Help was not updated
- a business rule was changed but its explanation was not updated
- a permission/role behavior changed but Role Help was not reviewed
- a field was added/removed/renamed but Field Help was not reviewed
- an action/button changed behavior but Action Help was not reviewed
- a validation rule changed but Troubleshooting/Business Rule Help was not reviewed
- an existing feature was significantly redesigned but Help still describes the old behavior

---

# 2. MANDATORY CHANGE → HELP MATRIX

Every implementation must evaluate the following matrix.

| Development Change | Required Help Action |
|---|---|
| New Module | Create Module Help |
| New Feature | Create Feature/Task Help |
| New Page | Create Page Help |
| New Form | Create Form/Task Help |
| New Field | Create or verify Field Help |
| New Button/Action | Create or verify Action Help |
| New Workflow | Create Workflow Help |
| Workflow Change | Update Workflow Help |
| New Business Rule | Create Business Rule Help |
| Business Rule Change | Update Business Rule Help |
| New Permission | Update Role/Permission Help |
| RBAC Change | Review Role Help |
| SoD Change | Review Security/Role Help |
| New Status | Update Status/Workflow Help |
| Status Change | Update Workflow Help |
| Validation Change | Update Business Rule/Troubleshooting Help |
| New Error | Add Troubleshooting Help when appropriate |
| Search Behavior Change | Update Search Help |
| Report Change | Update Report Help |
| Dashboard Change | Update Dashboard Help |
| AI Behavior Change | Update AI Help |
| Configuration Change | Update Administration Help |
| Localization Change | Verify both language versions |
| UI/UX Change | Review contextual Help placement/content |
| API/Integration Change affecting users | Update relevant user-facing Help |
| Data Import/Export Change | Update Import/Export Help |
| CMMS Process Change | Update relevant CMMS workflow Help |
| Engineering Process Change | Update relevant Engineering Help |

---

# 3. NEW MODULE REQUIREMENT

When Claude creates a new module, it MUST create Help content for that module.

At minimum:

```text
Module Overview
What is this module?
What problem does it solve?
Who uses it?
Main capabilities
Main workflow
Important business rules
Permissions / roles
Common tasks
Common problems
Related modules
```

Example:

```text
CMMS
```

must not be implemented without Help explaining the CMMS module.

---

# 4. NEW PAGE REQUIREMENT

Every significant new page MUST have contextual Help.

Example:

```text
CMMS.WorkOrders.Index
```

must have a corresponding Help Context.

Minimum content:

```text
What is this page?
What can the user do here?
Main actions
Important filters
Important fields
Workflow
Business rules
Permissions
Related pages
```

---

# 5. WORKFLOW CHANGE REQUIREMENT

Workflow changes require special attention.

If a workflow changes from:

```text
Request
↓
Review
↓
Approval
↓
Issue
```

to:

```text
Request
↓
Technical Review
↓
Approval
↓
Permit
↓
Issue
```

then the Help describing that workflow MUST be updated in the same development task.

Do NOT leave outdated workflow documentation.

---

# 6. BUSINESS RULE REQUIREMENT

Whenever a business rule is introduced or changed, Help MUST explain the user-facing consequence.

Example:

Implementation:

```text
Work Order cannot be issued unless PermitStatus = Approved
```

Required Help:

```text
Why can't I issue this Work Order?

A Work Order requires an approved Permit before it can be issued.
```

The Help must explain the rule in operational language.

Do not expose unnecessary implementation details to ordinary users.

---

# 7. ROLE AND PERMISSION REQUIREMENT

If implementation changes:

- Role
- Permission
- Claim
- Policy
- RBAC
- SoD
- approval authority
- workflow authority

then Help MUST be reviewed.

Example:

If:

```text
Senior Shift Supervisor
```

gains permission to Issue Work Orders, relevant Role Help must be updated.

If that permission is removed, the Help must no longer state that the role can issue Work Orders.

---

# 8. FIELD CHANGE REQUIREMENT

When adding a meaningful field, evaluate:

```text
Field Help
Tooltip
Business Rule
Workflow Help
Search Help
Report Help
```

Do not automatically create long documentation for trivial fields.

Use judgment.

The objective is useful contextual guidance, not documentation noise.

---

# 9. ACTION / BUTTON REQUIREMENT

For significant actions such as:

```text
Create
Submit
Approve
Reject
Issue
Revise
Close
Cancel
Delete
Import
Export
Baseline
Release
```

Help must explain the action when its behavior is not self-evident.

For complex actions include:

```text
What does it do?
Who can use it?
What happens after clicking it?
What prerequisites exist?
Can it be reversed?
```

---

# 10. ERROR AND VALIDATION REQUIREMENT

If a new validation rule or user-facing error is introduced, evaluate whether Help should explain it.

Preferred pattern:

```text
Error:
Cannot issue Work Order.

Reason:
Required Permit has not been approved.

Help:
Why is a Permit required?
```

Help should reduce repetitive support questions.

---

# 11. UI REDESIGN REQUIREMENT

A UI redesign does not automatically require rewriting all Help.

However, Claude MUST review existing Help whenever:

- navigation changes
- terminology changes
- buttons move or change meaning
- workflow presentation changes
- fields are renamed
- user interaction changes
- an existing task is performed differently

If the existing Help becomes misleading, it MUST be updated.

---

# 12. TERMINOLOGY REQUIREMENT

When a business term changes:

```text
Old:
Work Request Group

New:
Maintenance Request Group
```

Claude MUST search the Help knowledge base for the old terminology and update relevant content.

Do not leave inconsistent terminology.

---

# 13. BILINGUAL REQUIREMENT

PEMS Help is bilingual.

At minimum:

```text
English
Persian
```

Whenever Help content is created or materially changed:

1. Update English
2. Update Persian
3. Verify semantic equivalence
4. Verify RTL presentation

Do not leave a new feature documented in only one language unless explicitly approved.

---

# 14. HELP COMPLETENESS CHECK

Before declaring a task complete, Claude MUST perform this checklist:

```text
[ ] Did I add a new module?
[ ] Did I add a new page?
[ ] Did I add a new workflow?
[ ] Did I modify a workflow?
[ ] Did I add/change a business rule?
[ ] Did I add/change a permission?
[ ] Did I add/change a role?
[ ] Did I add/change a field?
[ ] Did I add/change an important action?
[ ] Did I add/change validation?
[ ] Did I add/change a user-facing error?
[ ] Did I change terminology?
[ ] Did I change navigation?
[ ] Did I change user interaction?
[ ] Did I change AI behavior?
[ ] Did I change import/export behavior?
[ ] Did I change reporting/dashboard behavior?
```

For every `YES`, Claude MUST identify the corresponding Help action.

---

# 15. HELP CHANGE TYPES

Every development task must classify its Help impact as one of:

### CREATE

New Help content is required.

### UPDATE

Existing Help must be modified.

### REVIEWED — NO CHANGE REQUIRED

Existing Help was reviewed and remains accurate.

### NOT APPLICABLE

The change has no meaningful user-facing Help impact.

Claude MUST NOT use `NOT APPLICABLE` casually.

It must be justified when the change could reasonably affect user understanding.

---

# 16. MANDATORY IMPLEMENTATION REPORT

Every Claude implementation report MUST include:

## Help Synchronization

```text
Help Impact: CREATE / UPDATE / REVIEWED / N/A

Affected Help Contexts:
- ...
- ...

Help Changes:
- ...
- ...

Business Rules Updated:
- ...
- ...

Workflow Documentation Updated:
- ...
- ...

Role/Permission Documentation Updated:
- ...
- ...

Languages Verified:
- English
- Persian
```

Example:

```text
Help Impact: UPDATE

Affected Help Contexts:
- CMMS.WorkOrders.Create
- CMMS.WorkOrders.Details

Help Changes:
- Added Permit prerequisite explanation
- Updated Issue Work Order workflow
- Added troubleshooting guidance

Business Rules:
- Work Order requires approved Permit

Languages:
- English: Updated
- Persian: Updated
```

---

# 17. HELP MUST BE PART OF THE SAME CHANGESET

Help updates should normally be included in the same implementation task/change set as the feature.

Avoid:

```text
Feature implemented today
Help updated next month
```

Preferred:

```text
Feature
+
Business Rules
+
UI
+
Tests
+
Help
```

all evolve together.

---

# 18. DEFINITION OF DONE

A functional change is NOT DONE until:

```text
Implementation
      +
Tests
      +
Security Review
      +
Help Review
      +
Help Update if required
      =
DONE
```

The Help requirement is therefore part of the Definition of Done.

---

# 19. CLAUDE MANDATORY BEHAVIOR

Before implementing any task:

## Step 1

Understand the requested functional change.

## Step 2

Identify affected modules/pages/workflows/business rules.

## Step 3

Search existing Help content related to those areas.

## Step 4

Determine:

```text
CREATE
UPDATE
REVIEWED
N/A
```

## Step 5

Implement the functional change.

## Step 6

Implement/update Help in the same task.

## Step 7

Verify Help against the final implementation.

## Step 8

Verify English/Persian consistency.

## Step 9

Include Help Synchronization in the final report.

---

# 20. DO NOT DUPLICATE HELP

If a Help article already exists:

DO NOT create a second article explaining the same thing.

Instead:

- update it
- extend it
- link it
- reuse it

The Help system must remain a single source of truth.

---

# 21. SINGLE SOURCE OF TRUTH

Where possible:

```text
Business Rule
      ↓
Application Implementation
      ↓
Help Knowledge
      ↓
AI Explanation
```

The AI must not independently invent business rules.

The Help system should explain the rules implemented by PEMS.

---

# 22. HELP DRIFT PREVENTION

Help drift is considered a defect.

Example:

Application:

```text
Supervisor can Issue Work Order
```

Help:

```text
Only Maintenance Manager can Issue Work Order
```

This is an implementation defect and must be corrected.

The same applies to:

- permissions
- statuses
- workflow
- terminology
- prerequisites
- field meanings
- navigation
- business rules

---

# 23. FINAL BASELINE RULE

This is the most important rule of this document:

> **Every time PEMS functionality changes, Claude MUST determine the Help impact and synchronize the corresponding Help content before declaring the task complete.**

No module is considered fully implemented until its user-facing knowledge is also implemented.

---

# 24. SHORT RULE FOR CLAUDE

Use this rule in every task:

```text
CHANGE → IDENTIFY IMPACT → UPDATE HELP → VERIFY → REPORT
```

Never:

```text
CHANGE → CODE → DONE
```

The correct PEMS development lifecycle is:

```text
Requirement
    ↓
Architecture
    ↓
Implementation
    ↓
Business Rules
    ↓
UI
    ↓
Tests
    ↓
HELP SYNCHRONIZATION
    ↓
Validation
    ↓
Final Report
```

---

# BASELINE STATUS

**PEMS-HLP-BASELINE-001**

This baseline is mandatory for all future PEMS development work.

Any future Claude instruction, implementation prompt, feature request, refactoring task or module development must comply with this baseline unless a higher-priority project decision explicitly supersedes it.