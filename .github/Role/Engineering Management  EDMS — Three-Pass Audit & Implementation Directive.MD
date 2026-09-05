

# PEMS ENGINEERING MODULE

## Engineering Management / EDMS — Three-Pass Audit & Implementation Directive

**Repository:** `saberahmadi48-collab/PEMS-cmms-edit-empoleeys`

**Objective:** Bring the Engineering module to a production-grade Petrochemical / EPC Engineering Document Management standard.

---

# 1. CURRENT REPOSITORY BASELINE

Before making any changes, inspect the current repository and preserve the existing architecture.

The current repository already contains:

* `PEMS.Domain`
* `PEMS.Application`
* `PEMS.Infrastructure`
* `PEMS.Web`
* `PEMS.AI`
* `.claude`
* centralized `Document`
* specialized `EngineeringDocument`
* Engineering document workflow
* revisions
* attachments
* comments
* discipline permissions
* document numbering
* engineering copilot
* semantic search / knowledge-base integration

The current `Document` entity already acts as an Enterprise Document Core and contains:

* DocumentNo
* Title
* DocumentType
* Project
* Discipline
* Area
* Revision
* Status
* CommentStatus
* Freeze
* Planned/Actual dates
* Delay
* AI workflow stage
* comments
* tags

Do not replace this architecture.

`EngineeringDocument` currently adds:

* Equipment
* Contract
* Vendor
* Material Requisition
* Issue Purpose
* Prepared / Checked / Approved
* Client Approval
* Attachments
* Revisions
* Workflow

These are valuable foundations.

---

# 2. ABSOLUTE RULE

Do NOT treat this task as a CRUD improvement.

The Engineering module must become:

> **Engineering Management + EDMS + MDR + Review Workflow + Revision Control + Engineering Traceability + Engineering Intelligence**

The goal is not simply to make Engineering screens look better.

The goal is to make the Engineering module operationally correct.

---

# 3. THREE-PASS MANDATORY AUDIT

Perform three separate audits before implementation.

---

# PASS 1 — BUSINESS / PETROCHEMICAL ENGINEERING AUDIT

Review the module from the perspective of:

* Engineering Manager
* Discipline Lead
* Discipline Engineer
* Document Controller
* Project Manager
* Project Controls
* QA/QC
* Procurement
* Construction
* Commissioning
* Maintenance
* Operations
* Vendor
* Contractor

Determine whether the current Engineering module can answer:

### Portfolio

* How many engineering documents exist?
* How many are active?
* How many are overdue?
* Which disciplines are behind?
* Which projects have document backlog?
* Which vendors are late?
* Which documents require immediate action?

### Engineering Progress

* Planned documents
* Submitted documents
* Reviewed documents
* Approved documents
* Rejected documents
* Returned documents
* Revised documents
* Closed documents

### Discipline Performance

At minimum:

* Process
* Mechanical
* Piping
* Civil/Structural
* Electrical
* Instrumentation & Control
* Telecom
* HSE
* Process Safety
* HVAC
* Architecture
* Rotating Equipment
* Static Equipment
* Materials/Corrosion
* Other configured disciplines

Do NOT hard-code disciplines if PEMS already manages them as master data.

---

# PASS 2 — EDMS / MDR / EPC WORKFLOW AUDIT

Audit the actual document lifecycle.

The minimum lifecycle must be capable of representing:

```text
Draft
   ↓
Internal Check
   ↓
Internal Approval
   ↓
IDC / Interdisciplinary Review
   ↓
Document Controller Review
   ↓
Issued / Transmitted
   ↓
Client Review
   ↓
Client Comment
   ↓
Revise & Resubmit
   ↓
Approved
   ↓
Approved With Comments
   ↓
Issued for Construction / Use
   ↓
Superseded
   ↓
Archived
```

Do not blindly implement every state.

Inspect the existing workflow engine and determine which states already exist.

---

# 4. STATUS VS ACTION VS COMMENT STATUS

This distinction is mandatory.

Do not mix:

```text
Status
Action
Comment Status
Issue Purpose
```

For example:

### Status

```text
Draft
Under Review
Approved
Rejected
Superseded
Archived
```

### Action

```text
Submit
Check
Approve
Reject
Return
Revise
Issue
Freeze
Unfreeze
```

### Comment Status

```text
No Comment
Comment Open
Comment Responded
Comment Closed
```

### Issue Purpose

```text
IFI
IFA
IFC
As-Built
Tender
Construction
Information
```

These must remain conceptually separate.

---

# 5. DOCUMENT REVISION CONTROL

This is a critical area.

Audit:

* Revision numbering
* Revision history
* Active revision
* Superseded revision
* Revision reason
* Change description
* Previous revision
* New revision
* Prepared By
* Checked By
* Approved By
* Issue Date
* Effective Date
* Vendor comment
* Reviewer
* AI revision summary

The existing repository already has Revision entities and revision mapping.

Do not create a second revision system.

Strengthen the existing one.

---

# 6. REVISION RULE

The UI must make it impossible to confuse:

```text
Current Revision
```

with:

```text
Historical Revision
```

Example:

```text
DOCUMENT: PID-1234

CURRENT
Revision: 03
Status: Approved
Purpose: IFC

HISTORY
Rev 02 — Superseded
Rev 01 — Superseded
Rev 00 — Superseded
```

The current revision must always be visually dominant.

---

# 7. MDR — MASTER DOCUMENT REGISTER

Engineering requires a true MDR view.

The MDR must support:

* Document No.
* Title
* Document Type
* Discipline
* Area
* Project
* Contractor/Vendor
* Revision
* Status
* Comment Status
* Planned Date
* Actual Date
* Forecast Date where applicable
* Delay
* Issue Purpose
* Responsible Person
* Review Status
* Client Status
* Last Action
* Next Action
* Next Action Due Date

The MDR must support:

* search
* filters
* sorting
* saved filters
* export
* column configuration
* pagination
* bulk operations where authorized
* drill-down

---

# 8. MDR HEALTH

Create calculated MDR health.

Possible states:

```text
Healthy
Watch
At Risk
Critical
```

Health should consider:

* overdue
* review duration
* client response delay
* missing revision
* missing attachment
* open comments
* pending approval
* discipline assignment
* workflow blockage

Never create an arbitrary score with no explanation.

If:

```text
AT RISK
```

the user must be able to inspect WHY.

---

# 9. ENGINEERING DASHBOARD

Build a real Engineering Management Dashboard.

The dashboard must answer:

> What is happening in Engineering right now?

First viewport:

### KPI Row

* Total Documents
* Active Documents
* Pending Review
* Pending Approval
* Overdue
* Critical
* Open Comments
* Client Pending

---

# 10. SECOND DASHBOARD AREA

Show:

### Document Status Distribution

### Discipline Performance

### Project Performance

### Vendor Performance

### Overdue Documents

### Critical Actions

### Recent Workflow Activity

Do not create decorative charts.

Every chart must answer a management question.

---

# 11. DISCIPLINE DASHBOARD

Every discipline should have its own operational view.

For example:

```text
Instrumentation & Control
```

must show:

* total documents
* pending
* overdue
* approved
* rejected
* open comments
* client pending
* average review time
* overdue percentage
* latest activity

The same framework should work for all disciplines.

---

# 12. ENGINEERING DOCUMENT 360°

Create a complete document workspace.

Recommended structure:

```text
Document Header
│
├── Overview
├── Current Revision
├── Workflow
├── IDC
├── Comments
├── Revision History
├── Attachments
├── Transmittals
├── Client Response
├── Related Documents
├── Related Equipment
├── Related Contract
├── Related Vendor
├── Related MR
├── Related Letters
├── Related Project
├── AI Intelligence
└── Audit Trail
```

Do not duplicate information.

Use existing PEMS entities and services.

---

# 13. DOCUMENT RELATIONSHIPS

Engineering documents must support traceability.

At minimum:

```text
Document
 ↕
Equipment
 ↕
Project
 ↕
Contract
 ↕
Vendor
 ↕
MR
 ↕
VDR
 ↕
Letters
 ↕
Other Engineering Documents
```

The repository already has several of these relationships.

Audit them.

Fix missing navigation and missing reverse relationships.

---

# 14. IDC — INTERDISCIPLINARY CHECK

IDC must be a first-class engineering workflow.

The user must be able to see:

```text
IDC
-----------------------
Discipline        Status
Process           Complete
Mechanical        Pending
Piping            Complete
Electrical        Pending
Instrumentation   Complete
Civil             N/A
```

Track:

* assigned discipline
* reviewer
* assigned date
* due date
* response date
* status
* comments
* decision

Possible states:

```text
Pending
In Review
Commented
Accepted
Rejected
No Comment
Overdue
```

---

# 15. IDC RULE

A document must not be considered fully reviewed merely because one reviewer approved it if required IDC disciplines are still pending.

The workflow engine must enforce this where the business rule requires it.

Do not implement this only in frontend JavaScript.

Server-side validation is mandatory.

---

# 16. COMMENTS / REVIEW COMMENTS

Engineering comments must be treated as controlled records.

Each comment should support:

* comment ID
* document
* revision
* reviewer
* discipline
* category
* severity
* comment
* response
* responsible person
* due date
* status
* closure evidence

Statuses:

```text
Open
Responded
Accepted
Rejected
Closed
```

Do not permanently overwrite the original reviewer comment.

---

# 17. COMMENT RESOLUTION

A closed comment must have traceability.

Example:

```text
Comment #125

Reviewer:
Process

Original Comment:
Update operating pressure.

Response:
Updated in Rev 03.

Evidence:
Page 14

Status:
Closed
```

The system must preserve the full history.

---

# 18. TRANSMITTAL

Engineering requires formal document transmission.

Audit whether PEMS has a proper Transmittal model.

If it does:

* integrate it.

If it does not:

* design a proper domain capability before implementation.

A Transmittal should support:

* Transmittal No.
* Date
* Sender
* Recipient
* Documents
* Revision
* Purpose
* Response required
* Due date
* Status
* Related contract/project
* correspondence

Do not simply use a generic Letter as a replacement for Transmittal if the domain requires distinct semantics.

---

# 19. LETTER / CORRESPONDENCE INTEGRATION

The repository already connects Engineering Documents with Letters.

Keep this integration.

The user should be able to trace:

```text
Document
→ Incoming Letter
→ Comment
→ Response
→ Revised Document
→ Transmittal
```

Do not create a second correspondence system.

---

# 20. CLIENT REVIEW

Client review must be distinct from internal engineering approval.

The current model already contains:

* ClientApprovalStatus
* ClientComment
* ClientResponseDate

Keep this capability.

Strengthen it into a proper workflow.

At minimum:

```text
Pending
Submitted
Under Client Review
Approved
Approved With Comments
Rejected
Revise & Resubmit
```

---

# 21. PREPARED / CHECKED / APPROVED

The current EngineeringDocument already has:

* PreparedBy
* CheckedBy
* ApprovedBy
* Employee IDs
* dates

Keep this.

The system must use actual Employee identities.

Do not rely on free-text names for authoritative workflow actions.

The displayed name may be derived from Employee.

---

# 22. DOCUMENT NUMBERING

The current controller already supports:

* automatic numbering
* permission-controlled manual numbering

Keep this architecture.

Audit:

* uniqueness
* concurrency
* project sequence
* discipline sequence
* document type
* manual override permission
* audit trail

Two users creating documents simultaneously must never receive the same number.

---

# 23. DOCUMENT FREEZE

The existing model has:

```text
IsFrozen
FrozenDate
```

Audit the behavior.

Freeze must be server-enforced.

A frozen document must not be silently editable.

Any unfreeze action must:

* require permission
* create audit entry
* capture user
* capture reason
* capture timestamp

---

# 24. DOCUMENT SECURITY

Review permissions by:

* role
* project
* discipline
* workflow stage
* action

Important:

Visibility ≠ Approval Authority.

A user may be allowed to SEE a document without being allowed to:

* approve
* reject
* issue
* freeze
* unfreeze

The current controller already distinguishes discipline visibility and approval authority.

Preserve and strengthen this behavior.

---

# 25. DISCIPLINE SCOPING

The existing implementation allows restricted users to see documents in their own discipline while supervisory/management/document-control roles retain broader visibility.

Audit this carefully.

Required behavior:

### Discipline Engineer

Own discipline.

### Discipline Supervisor

Own discipline approval authority.

### Engineering Manager

Cross-discipline visibility/approval according to authorization.

### Document Controller

Cross-discipline document-control visibility.

### System Admin

Administrative access.

Do not accidentally expose documents by hiding only the list while allowing direct URL access.

The current controller already contains direct-access protection. Preserve it.

---

# 26. ATTACHMENT MANAGEMENT

Engineering attachments must support:

* file metadata
* version association
* document association
* upload date
* uploader
* MIME type
* size
* checksum where appropriate
* AI processing state
* preview where supported

Never allow a new upload to silently replace an authoritative historical file.

---

# 27. ENGINEERING AI

The repository already contains AI capabilities.

Do not create another AI architecture.

Integrate the existing:

* AI extraction
* semantic search
* knowledge base
* Engineering Copilot
* relationship analysis

The current EngineeringDocumentService already indexes document metadata/content into the existing knowledge base.

Extend this carefully.

---

# 28. AI DOCUMENT INTELLIGENCE

AI should support:

* document classification
* discipline suggestion
* document type suggestion
* metadata extraction
* tag extraction
* equipment/tag detection
* contract detection
* MR detection
* revision detection
* key specification extraction
* relationship discovery
* duplicate detection
* revision comparison
* summary

AI output must be:

```text
Suggested
```

not silently authoritative.

---

# 29. AI CONFIDENCE

AI results should show:

* confidence
* source
* extraction timestamp
* model
* human verification state

Example:

```text
AI Suggested Discipline:
Instrumentation

Confidence:
94%

Source:
Document Page 1

Human Verified:
No
```

---

# 30. REVISION INTELLIGENCE

Add revision comparison.

User should be able to compare:

```text
Rev 02 vs Rev 03
```

and see:

* changed metadata
* changed comments
* changed extracted values
* changed equipment references
* changed specifications
* changed text where technically feasible
* changed pages where PDF comparison is available

AI may summarize changes, but the original documents remain authoritative.

---

# 31. DUPLICATE DOCUMENT DETECTION

Engineering needs duplicate detection.

Detect possible duplicates using:

* DocumentNo
* title
* project
* discipline
* file hash
* semantic similarity

Do not automatically delete.

Flag for review.

---

# 32. DOCUMENT QUALITY GATE

Before a document enters a controlled workflow, validate:

* document number
* title
* project
* discipline
* document type
* revision
* issue purpose
* attachment
* required metadata
* responsible discipline
* required reviewers

Show:

```text
READY FOR SUBMISSION
```

or

```text
BLOCKED

Missing:
• Discipline
• Attachment
• Issue Purpose
```

Validation must be server-side.

---

# 33. ENGINEERING WORKFLOW GATE

Every transition must validate its prerequisites.

Example:

```text
Submit
```

requires valid mandatory metadata.

```text
Approve
```

requires all required checks.

```text
Issue
```

requires required approval.

```text
IFC
```

requires required workflow completion.

Do not rely on UI button hiding.

---

# 34. SLA / REVIEW PERFORMANCE

Add configurable engineering review metrics:

* average review time
* average approval time
* average client response time
* average comment closure time
* overdue percentage
* first-pass approval rate
* revision rate

Do not hard-code SLA values unless the business configuration provides them.

---

# 35. ENGINEERING KPIs

Minimum KPI framework:

### Documents

* Total
* Active
* Submitted
* Under Review
* Approved
* Rejected
* Superseded

### Schedule

* On Time
* Overdue
* Average Delay
* Critical Delay

### Review

* Pending Review
* Pending Approval
* Client Pending
* IDC Pending

### Comments

* Open
* Overdue
* Closed
* Average Closure Time

### Revisions

* Revision Count
* Average Revision Count
* Rework Rate

### Discipline

* Performance by Discipline

### Vendor

* Vendor Submission Performance

---

# 36. ENGINEERING HEATMAP

Provide:

```text
Discipline × Status
```

Example:

```text
                 Pending  Review  Approved  Overdue
Process             8       3       41        2
Mechanical           5       7       32        4
Piping               9       6       38        5
Instrumentation      4       3       29        1
Electrical            7       4       35        3
```

All values must be real backend aggregates.

---

# 37. VENDOR PERFORMANCE

For vendor-submitted documents:

Track:

* submissions
* on-time rate
* average review cycle
* rejection rate
* revision rate
* open comments
* overdue responses

Link to existing Vendor data.

Do not create duplicate vendor entities.

---

# 38. PROJECT ENGINEERING DASHBOARD

For every project:

Show:

* engineering document count
* completion
* overdue
* critical
* discipline performance
* vendor performance
* client review
* open comments
* revision trend
* engineering bottlenecks

Project Engineering Dashboard must use the same underlying data model.

---

# 39. ENGINEERING → PROCUREMENT TRACEABILITY

Where a document is related to MR:

```text
Engineering Document
→ MR
→ Procurement
→ Vendor
→ Material
```

The user should be able to navigate this relationship.

---

# 40. ENGINEERING → CONTRACT TRACEABILITY

For contract-linked engineering documents:

```text
Engineering Document
→ Contract
→ Contractor
→ Deliverable
→ Variation / Claim where applicable
```

Do not duplicate Contract data.

---

# 41. ENGINEERING → EQUIPMENT TRACEABILITY

For equipment-linked documents:

```text
Equipment
→ Engineering Documents
→ Drawings
→ Datasheets
→ Manuals
→ Inspection
→ Maintenance
→ Work Orders
```

This is critical for future CMMS integration.

---

# 42. ENGINEERING → CMMS

Engineering documents should become a knowledge source for maintenance.

Examples:

```text
P&ID
Datasheet
OEM Manual
Equipment Drawing
Cause & Effect
Logic Diagram
```

should be discoverable from the corresponding Asset/Equipment context.

Do not copy documents into CMMS.

Use shared document references.

---

# 43. SEARCH

Engineering search must support:

### Exact Search

* document number
* equipment tag
* contract
* MR
* revision

### Structured Search

* project
* discipline
* status
* issue purpose
* vendor
* date
* area

### Semantic Search

Use existing PEMS AI infrastructure.

Search result must show:

* document
* revision
* status
* source
* relevance
* project
* discipline

---

# 44. EMPTY / LOADING / ERROR STATES

Every Engineering screen must have proper:

* loading state
* empty state
* error state
* unauthorized state
* blocked workflow state

Do not leave blank screens.

---

# 45. PERFORMANCE

The current service retrieves full document collections.

Audit whether this is appropriate at scale.

The Engineering module must support large projects.

Avoid loading thousands of documents into memory.

Prefer:

* server-side filtering
* pagination
* projection
* aggregation
* indexed search
* lazy loading
* asynchronous queries

Do not introduce frontend-only filtering for large MDR datasets.

---

# 46. DATABASE / DOMAIN RULE

Do NOT create duplicate entities.

Before adding anything:

1. Search existing domain entities.
2. Search interfaces.
3. Search repositories.
4. Search services.
5. Search controllers.
6. Search migrations.
7. Reuse existing capability if present.

Only create a new entity when the business concept genuinely does not exist.

---

# 47. ARCHITECTURE RULE

Preserve:

```text
PEMS.Domain
      ↓
PEMS.Application
      ↓
PEMS.Infrastructure
      ↓
PEMS.Web
```

AI remains integrated through the existing architecture.

Do not introduce:

* unnecessary CQRS
* unnecessary event sourcing
* multi-tenancy
* duplicate document stores
* duplicate workflow engines

---

# 48. UI / UX

Apply the existing PEMS Petrochemical UI/UX Skill.

Engineering UI must be:

* enterprise-grade
* information-dense
* readable
* RTL
* bilingual
* accessible
* responsive
* role-aware
* workflow-aware

Avoid consumer-dashboard aesthetics.

---

# 49. ENGINEERING NAVIGATION

Recommended structure:

```text
Engineering
│
├── Engineering Dashboard
├── MDR
├── My Discipline
├── Documents
├── Pending Review
├── Pending Approval
├── IDC
├── Comments
├── Transmittals
├── Revisions
├── Client Review
├── Vendors
├── Engineering Analytics
├── AI Intelligence
└── Engineering Settings
```

Only expose items supported by actual implementation and authorization.

---

# 50. ENGINEERING DASHBOARD — PROPOSED FIRST VIEW

Conceptually:

```text
┌─────────────────────────────────────────────────────────────┐
│ ENGINEERING MANAGEMENT                    Search / Filters  │
├─────────────────────────────────────────────────────────────┤
│ Total │ Review │ Approval │ Overdue │ Comments │ Client    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ DOCUMENT HEALTH                  DISCIPLINE PERFORMANCE     │
│ Healthy / Watch / Risk          Process                    │
│                                  Mechanical                 │
│                                  Piping                     │
│                                  Instrumentation             │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ CRITICAL ACTIONS                   REVIEW PIPELINE          │
│ Overdue Documents                 Pending IDC              │
│ Client Pending                    Pending Approval          │
│ Open Comments                     Client Review             │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ MDR OVERDUE                        RECENT ACTIVITY          │
│ Document | Rev | Discipline       Submit / Review / Issue │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ ENGINEERING BOTTLENECKS                                    │
│ Discipline | Vendor | Project | Delay | Comments           │
└─────────────────────────────────────────────────────────────┘
```

This is a conceptual information architecture.

Use the actual PEMS design system.

---

# 51. DOCUMENT 360° — REQUIRED

The document detail page must answer:

> What is this document, where is it in its lifecycle, who is responsible, what changed, what comments exist, and what must happen next?

Header:

```text
Document No
Title
Revision
Status
Issue Purpose
Discipline
Project
Health
Next Action
```

Then:

```text
Overview
Workflow
Comments
Revision History
IDC
Attachments
Transmittals
Client Review
Relationships
AI
Audit
```

---

# 52. NEXT ACTION ENGINE

Every active document should expose:

```text
Current State
Next Valid Action
Responsible Role
Due Date
Blocking Reason
```

Example:

```text
Status:
Pending Client Review

Next Action:
Await Client Response

Due:
2026-08-21

Owner:
Client

Blocking:
Client review
```

This is more valuable than another status badge.

---

# 53. AUDIT TRAIL

Every controlled Engineering action must be traceable.

Track:

* user
* employee
* timestamp
* IP
* browser/user agent where existing audit architecture supports it
* action
* previous state
* new state
* comment/reason
* document
* revision

The current controller already collects client IP and user agent for workflow auditing.

Preserve this architecture.

---

# 54. SECURITY AUDIT

Test:

* direct URL access
* unauthorized approval
* unauthorized edit
* unauthorized revision
* unauthorized freeze/unfreeze
* unauthorized comment closure
* unauthorized client response
* unauthorized manual numbering

Security must be enforced server-side.

---

# 55. TEST MATRIX

Claude must create tests for at least:

### Document Creation

* automatic number
* authorized manual number
* unauthorized manual number
* duplicate number

### Revision

* new revision
* superseding
* historical revision
* invalid revision transition

### Workflow

* valid transition
* invalid transition
* missing prerequisite
* unauthorized transition

### Discipline

* restricted user
* supervisor
* manager
* document controller
* admin

### IDC

* pending reviewer
* completed reviewer
* overdue reviewer
* missing required reviewer

### Comments

* open
* response
* closure
* unauthorized closure

### Freeze

* frozen edit rejection
* authorized unfreeze
* audit trail

### Client Review

* pending
* approval
* approval with comments
* rejection
* resubmission

---

# 56. THREE-PASS FINAL QA

Before declaring completion:

## PASS 1 — BUSINESS

Can Engineering Management operate the module?

## PASS 2 — EDMS / WORKFLOW

Can Document Control and Engineering Control the complete document lifecycle?

## PASS 3 — SOFTWARE

Does the implementation satisfy:

* architecture
* security
* performance
* database integrity
* UX
* RTL
* accessibility
* testing
* auditability?

All three must PASS.

---

# 57. DEFINITION OF DONE

Engineering is NOT complete if only:

* CRUD works
* dashboard renders
* forms submit
* pages look attractive

Engineering is complete only when:

* MDR works
* lifecycle works
* revision control works
* IDC works
* comments work
* client review works
* transmittal is handled correctly
* document traceability works
* role/security rules work
* numbering works
* freeze works
* audit trail works
* AI integration is controlled
* dashboards are actionable
* performance is acceptable
* tests pass

---

# 58. IMPORTANT — DO NOT BREAK EXISTING PEMS

Before changing code:

* inspect current implementation
* inspect existing migrations
* inspect existing services
* inspect existing workflow
* inspect existing document core
* inspect existing VDR
* inspect existing letters
* inspect existing AI
* inspect existing permissions

Do not rewrite working components merely for stylistic reasons.

---

# 59. DATABASE MIGRATION RULE

Do NOT automatically create or apply migrations during this task.

First determine whether domain changes are actually necessary.

If schema changes are required:

1. document them
2. implement them
3. generate migration
4. do not apply destructive changes automatically
5. report the migration explicitly

---

# 60. FINAL IMPLEMENTATION REPORT

After implementation report:

```text
PEMS ENGINEERING MODULE
=======================

PASS 1 — BUSINESS AUDIT: PASS / FAIL
PASS 2 — EDMS AUDIT: PASS / FAIL
PASS 3 — SOFTWARE AUDIT: PASS / FAIL

Engineering Dashboard: PASS / FAIL
MDR: PASS / FAIL
Document 360: PASS / FAIL
Revision Control: PASS / FAIL
IDC: PASS / FAIL
Comments: PASS / FAIL
Client Review: PASS / FAIL
Transmittal: PASS / FAIL
Workflow Gates: PASS / FAIL
Document Numbering: PASS / FAIL
Freeze Control: PASS / FAIL
Permissions: PASS / FAIL
Audit Trail: PASS / FAIL
AI Intelligence: PASS / FAIL
Semantic Search: PASS / FAIL
Equipment Traceability: PASS / FAIL
Contract Traceability: PASS / FAIL
MR Traceability: PASS / FAIL
VDR Integration: PASS / FAIL
Letter Integration: PASS / FAIL
CMMS Traceability: PASS / FAIL
RTL: PASS / FAIL
Responsive: PASS / FAIL
Accessibility: PASS / FAIL
Performance: PASS / FAIL
Tests: PASS / FAIL

Critical Findings:
[list]

Implemented Changes:
[list]

Remaining Gaps:
[list]

Database Changes:
YES / NO

Migration Created:
YES / NO

Unrelated Modules Modified:
YES / NO
```

# FINAL COMMAND

Do not merely explain the gaps.

Execute the work.

Start with repository inspection.

Then:

```text
INSPECT
→ PASS 1 BUSINESS AUDIT
→ PASS 2 EDMS/MDR AUDIT
→ PASS 3 SOFTWARE AUDIT
→ GAP MATRIX
→ IMPLEMENT
→ TEST
→ SECURITY AUDIT
→ PERFORMANCE AUDIT
→ FINAL QA
→ REPORT
```

Do not declare Engineering complete until all three audits pass.

### نتیجه ممیزی فعلی من

سه مورد را **خصوصاً به Claude تأکید کن**:

**1. Engineering را از نو نسازد.**
در Repository یک هسته خوب `Document → EngineeringDocument` وجود دارد و باید توسعه داده شود، نه اینکه دوباره EDMS جدید بسازد.

**2. بزرگ‌ترین تمرکز بعدی باید Workflow/MDR/IDC باشد.**
الان Controller قابلیت‌های زیادی دارد، اما باید مطمئن شویم این قابلیت‌ها واقعاً به یک lifecycle منسجم و server-side enforced تبدیل شده‌اند. Controller فعلی همین حالا Workflow، Permission، Comment، Letter، Numbering و Copilot را در خود دارد.

**3. AI را از نو نسازد.**
`EngineeringDocumentService` همین حالا Semantic Search/Knowledge Base را برای Engineering Document تغذیه می‌کند.  بنابراین Claude باید همین مسیر را تقویت کند، نه اینکه یک AI Document Intelligence موازی بسازد.

**حکم من:** Engineering فعلی را **قابل توسعه و دارای Foundation مناسب** می‌دانم، اما برای اینکه آن را Production-grade در سطح یک پتروشیمی/EPC بدانیم، هنوز باید روی **MDR + IDC + Transmittal + Comment Resolution + Revision Intelligence + Workflow Gates + Engineering Dashboard + SLA/Analytics + Traceability** کار شود.
