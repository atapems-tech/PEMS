# PEMS — UI-01 Focused Enterprise UX Audit

**Document ID:** PEMS-UI-01  
**Status:** MANDATORY AUDIT GATE  
**Scope:** UI/UX ONLY  
**Implementation:** NOT YET AUTHORIZED  
**Priority:** Controlled / Evidence-Based  
**Version:** 1.0

---

# 1. Objective

Perform a focused audit of the existing PEMS UI/UX.

The objective is NOT to redesign the entire application.

The objective is to identify only the UI/UX problems that materially affect:

- usability
- operational efficiency
- safety-critical workflows
- mobile operation
- role-based work
- Permit approval
- collaboration
- consistency
- accessibility

The audit must be evidence-based and repository-driven.

---

# 2. Expert Perspective

Evaluate the UI from these operational and technical perspectives:

1. Senior Software Engineer
2. Senior System Architect
3. Senior Maintenance Engineer
4. Maintenance Manager
5. Production Manager
6. Operations Manager
7. IT Manager
8. Electrical Manager
9. Instrumentation Manager
10. Technical Services Manager
11. Inspection Manager
12. HSE Manager
13. Warehouse Manager
14. Warehouse Specialist

Every finding must consider the actual operational value of the proposed change.

Do not recommend UI changes merely because another design looks more modern.

---

# 3. Scope

Audit ONLY these eight areas:

## 3.1 Global Layout & Navigation

Review:

- Main layout
- Sidebar
- Header
- Breadcrumbs
- Navigation hierarchy
- Mobile navigation
- Global actions
- User context
- Notification access
- Search access
- Messaging access

Determine whether users can reach their most important work efficiently.

---

# 4. Role-Based Workspace / My Work

Evaluate whether PEMS provides a useful role-oriented workspace.

The target concept is:

```text
MY WORK
├── My Tasks
├── My Approvals
├── My Permits
├── My Work Orders
├── My Notifications
├── My Alerts
└── My Messages


The system should not force users to browse multiple modules merely to discover work requiring their attention.

Evaluate the UX for:

Maintenance
Production
Operations
HSE
Electrical
Instrumentation
Technical Services
Inspection
Warehouse
Engineering
Management

Do NOT implement role dashboards during this audit.

Only identify gaps.

5. Permit & Approval UX

Permit is a safety-critical workflow.

Audit:

Permit list
Permit details
Permit status
Execution Gate presentation
Safety controls
Approval Chain
Approver visibility
Authority visibility
Competency visibility
Certification visibility
Approval actions
Reject actions
Emergency Override presentation
Approval history
Audit visibility
Mobile approval UX

The UI must clearly distinguish:

Authentication
      ≠
Authorization
      ≠
Competency
      ≠
Permit Eligibility

A successful login must not visually imply that the user is authorized to approve a Permit.

The UI must be consistent with:

PEMS-SEC-PERMIT-AUTH-001

Do NOT modify that security architecture.

6. Mobile / PWA UX

PEMS currently supports Mobile Web / PWA.

Audit the mobile experience for operational workflows.

Focus on:

Permit review
Permit approval
Step-up authentication
Work Orders
Assets
Notifications
Messaging
Search
Critical alerts

Mobile must be treated as an operational interface.

Do NOT simply evaluate whether the desktop UI technically shrinks to mobile size.

Do NOT start native Android/iOS implementation.

Native mobile is a future roadmap item.

7. Messaging / Chat Readiness

PEMS should evolve toward:

PEMS Operational Collaboration

This is NOT intended to be a generic social network.

Audit the current UI and architecture readiness for:

Direct Messaging
User ↔ User
Group Messaging

Examples:

Maintenance
Operations
HSE
Unit 300
Shift Team
Project Team
Contextual Messaging

A conversation may be associated with:

Permit
Work Order
Asset
Engineering Document
Material Request
Inspection
Incident

Example:

PTW-2026-001245
HOT WORK
Unit 300
P-301


[Discussion]

The user should eventually be able to open the referenced Permit directly from the conversation.

8. Messaging UX Requirements

Audit readiness for:

Direct messages
Group conversations
Unread count
Read status
Typing indicator
Message search
Conversation search
Attachments
Document references
PEMS entity references
Important messages
Mute
Notifications
Message timestamps

Also evaluate the ability to share PEMS objects inside conversations.

Example:

[PTW-2026-001245]
HOT WORK
Unit 300
Status: BLOCKED


[Open Permit]

Other examples:

[WO-8821]
Pump P-301
Priority: Critical


[Open Work Order]
[MR-1021]
Material Request
Cable 36 Core
Qty: 15m


[Open Request]

Do NOT copy LinkedIn visually.

Use mature enterprise messaging patterns only as conceptual inspiration.

9. Design System & CSS Duplication

Audit the existing UI foundation.

Identify:

Duplicate CSS systems
Conflicting CSS
Duplicate components
Duplicate JavaScript
Repeated styles
Inconsistent buttons
Inconsistent forms
Inconsistent cards
Inconsistent badges
Inconsistent tables
Inconsistent modal/dialog behavior
Inconsistent spacing
Inconsistent typography
Inconsistent status presentation

Do not rewrite CSS during this phase.

Only identify material duplication or inconsistency.

10. RTL / LTR & Accessibility

Audit:

RTL/LTR
Persian layout
English layout
Sidebar behavior
Icons
Tables
Forms
Modals
Direction-sensitive spacing
Navigation
Mobile layout
Accessibility

Check for:

Keyboard navigation
Focus states
ARIA labels
Screen reader semantics
Contrast
Touch target size
Modal focus management
Error association
Status indication without relying only on color
Reduced motion considerations

Only report meaningful problems.

11. Dashboard & Data Tables

Audit the current Dashboard and major data tables.

Focus on:

Information hierarchy
Decision usefulness
Filtering
Search
Sorting
Pagination
Responsive behavior
Status visibility
Critical information
Operational relevance

Do not recommend complex dashboards unless there is a clear operational reason.

12. Important Scope Control

The following are explicitly OUT OF SCOPE for implementation during UI-01:

Backend redesign
Database redesign
Permit workflow redesign
Execution Gate redesign
LOTO redesign
Gas Test redesign
SIMOPS redesign
Authorization engine redesign
Competency engine redesign
WebAuthn redesign
Security architecture redesign
Native Android implementation
Native iOS implementation
New frontend framework
Full UI rewrite

Existing security and Permit architecture must remain unchanged.

13. Critical Rule
DO NOT OVER-ENGINEER

The goal is not to maximize the number of findings.

The goal is to identify the smallest set of UI/UX changes that produces the largest operational improvement.

If something works well:

KEEP IT.

If a problem is cosmetic only:

Prefer P2 or do not report it.

If a proposed redesign does not materially improve:

usability
safety
efficiency
clarity
mobile operation
role effectiveness
collaboration

then do not recommend it.

14. Audit Method

Before making findings:

Inspect repository UI structure.
Inspect existing Views.
Inspect existing Layout.
Inspect navigation.
Inspect CSS.
Inspect JavaScript.
Inspect responsive rules.
Inspect Permit UI.
Inspect Dashboard UI.
Inspect current Notification/SignalR UI readiness.
Inspect current messaging-related components.
Identify reusable components.

Do not modify code.

15. Finding Classification

Every finding MUST have one severity:

P0 — Critical

Examples:

Safety-critical UX problem
Security-related UI misrepresentation
User can easily make a dangerous operational mistake
Critical mobile workflow unusable
Major authorization misunderstanding
P1 — High

Examples:

Significant operational inefficiency
Major navigation problem
Important role-based workflow missing
Major mobile usability issue
Major Permit approval UX issue
Significant collaboration limitation
P2 — Improvement

Examples:

Visual consistency
Minor usability improvements
Non-critical accessibility improvements
Cosmetic inconsistencies
Future optimization
16. Required Finding Format

For EVERY finding use:

Finding ID:
Severity:
Area:


Current Evidence:
<What exists today>


Problem:
<What is wrong>


Operational Impact:
<Why it matters>


Recommended Fix:
<Minimal practical solution>


Affected Files/Components:
<Specific repository references>


Dependencies:
<If any>


Regression Risk:
<Low / Medium / High>

Do not create vague findings such as:

"UI should be more modern."

Every finding must identify a concrete problem.

17. Required Audit Output

Return exactly these sections:

A. Executive Summary

Maximum 15 key findings.

B. Global Layout & Navigation
C. Role-Based Workspace / My Work
D. Permit & Approval UX
E. Mobile / PWA
F. Messaging / Chat Readiness
G. Design System / CSS
H. RTL/LTR & Accessibility
I. Dashboard / Data Tables
J. P0 Findings
K. P1 Findings
L. P2 Findings
M. Recommended Target UX

Keep this concise.

N. Implementation Sequence

Recommend a controlled implementation order.

18. Implementation Sequence Recommendation

The audit should consider this sequence:

UI-01
Focused Enterprise UX Audit
        ↓
Approval Gate
        ↓
UI-02
Design System Consolidation
        ↓
UI-03
Global Shell + Navigation
        ↓
UI-04
My Work / Role Workspace
        ↓
UI-05
Permit & Approval UX
        ↓
UI-06
Messaging + Contextual Collaboration
        ↓
UI-07
Mobile / PWA Optimization
        ↓
UI-08
Accessibility + RTL/LTR QA
        ↓
Final UI Gate

Do not assume every phase is required.

The audit must determine which phases are actually justified.

19. Current Security Baseline

The following architecture is COMPLETE and MUST NOT be modified as part of UI-01:

PEMS-SEC-PERMIT-AUTH-001

Completed capabilities include:

WebAuthn / Passkey
Fingerprint / Face ID / PIN delegation
No raw biometric storage
Step-up authentication
Authorization & Competency Engine
Role validation
Competency validation
Certification validation
Expiry validation
Area scope validation
Approval Chain enforcement
Permit revision binding
Tamper-evident audit
Emergency Override authorization
Mobile Web / PWA foundation

Important limitation:

Actual browser-based WebAuthn ceremony with a real platform authenticator has not yet been E2E tested in the development environment.

Do not change the server-side implementation because of this limitation.

20. Final Rule

PEMS is not a collection of CRUD pages.

PEMS is an operational platform.

Every important screen should answer:

WHO is using it?


WHAT do they need to know?


WHAT action do they need to perform?


WHAT authority do they have?


WHAT operational context does the action belong to?


WHAT happens after the action?


CAN the action be audited?

However:

Do not redesign everything merely to satisfy this principle.

Make only changes supported by repository evidence and operational need.

21. STOP CONDITION
DO NOT CODE.
DO NOT MODIFY FILES.
DO NOT CREATE MIGRATIONS.
DO NOT CHANGE BACKEND.
DO NOT CHANGE SECURITY.
DO NOT CHANGE DATABASE.
DO NOT IMPLEMENT CHAT YET.

First complete the audit.

After delivering the audit, STOP and wait for explicit approval.

END OF UI-01