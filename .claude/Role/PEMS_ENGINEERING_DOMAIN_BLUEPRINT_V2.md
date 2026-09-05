# PEMS Engineering Domain Blueprint V2

**Document:** `PEMS_ENGINEERING_DOMAIN_BLUEPRINT_V2.md`  
**Purpose:** Engineering domain architecture, audit baseline, and implementation directive for PEMS  
**Audience:** Claude, PEMS developers, architects, reviewers, and maintainers  
**Status:** Engineering Baseline

---

## 1. Executive Decision

PEMS Engineering has a strong **Engineering Document Management foundation**, but it must evolve into an **Engineering Intelligence Platform**.

Target:

```text
Document
  -> Page / Sheet
  -> Engineering Entity
  -> Attribute
  -> Relationship
  -> Provenance
  -> Cross-Document Reconciliation
  -> Conflict / Missing / Orphan Detection
  -> Revision Intelligence
  -> Engineer Review
  -> Golden Engineering Record
  -> Knowledge Graph
  -> Engineering Intelligence
  -> MDR / VDR / CMMS / AI
```

Do not begin by adding random entities or performing a large migration. Establish the domain boundaries first.

---

## 2. Existing Architecture to Preserve

Preserve unless an explicit architectural decision proves otherwise:

- `Document` shared document core
- `EngineeringDocument`
- `VDRItem`
- `DocumentRevision`
- `DocumentWorkflow`
- `DocumentAttachment`
- Employee integration
- Discipline scoping
- Automatic numbering
- Permission-controlled manual numbering
- AI extraction staging
- Human review
- Existing semantic-search infrastructure
- Existing PEMS Workflow Platform
- Existing MDR/VDR integration

Preferred structure:

```text
Document
  +-- EngineeringDocument
  +-- VDRItem
```

Do not create separate document identities for Engineering, VDR, MDR, or CMMS.

---

## 3. Engineering Domain Boundary

Engineering consists of two cooperating layers.

### Document Management

Responsible for:

- Document identity
- Number
- Title
- Type
- Discipline
- Project
- Revision
- Status
- Workflow
- Transmittal
- Attachments
- Comments
- Approvals
- MDR/VDR relationships

### Engineering Intelligence

Responsible for:

- Engineering entities
- Attributes
- Relationships
- Provenance
- Extraction
- Reconciliation
- Conflict detection
- Missing data
- Orphan detection
- Revision intelligence
- Knowledge graph
- Semantic search
- Engineering analytics
- AI assistance

These layers integrate but must not be conflated.

---

## 4. Separate Business Lifecycle from AI Lifecycle

Do not use one state machine for both.

### Document Lifecycle

```text
Draft
Submitted
UnderReview
Approved
Rejected
Superseded
Archived
```

### AI Processing Lifecycle

```text
Pending
Processing
Completed
Failed
RequiresReview
```

Therefore:

```text
DocumentStatus != AIProcessingStatus
```

`Approved` is a business state; `Processing` is an AI state.

---

## 5. Engineering Document Core

`EngineeringDocument` remains the primary engineering document record.

It may reference:

- Equipment
- Contract
- Vendor
- MR
- Issue Purpose
- Prepared By
- Checked By
- Approved By
- Client Approval
- Attachments
- Revisions
- Workflows

Where Employee foreign keys exist, they are authoritative.

If legacy string names remain, they are display/snapshot values only.

```text
EmployeeId = authoritative identity
String Name = snapshot/display only
```

---

## 6. Controlled Vocabulary

Review and progressively control:

- DocumentType
- DocumentStatus
- ReviewStatus
- CommentStatus
- AIProcessingStatus
- IssuePurpose
- Discipline
- SubmissionType
- ApprovalStatus

Do not introduce master data merely for abstraction. Use controlled values where consistency, governance, reporting, reuse, or authorization requires them.

---

## 7. Revision Management

Revision remains a first-class concept.

A revision should support:

- Revision Number
- Status
- Reason
- Change Description
- Reviewer
- Issued Date
- Effective Date
- Superseded Revision
- AI Revision Summary

Revision history alone is insufficient; the target is **Revision Intelligence**.

---

## 8. Revision Intelligence

Compare revisions at entity and attribute level.

Example:

```text
Revision 02 -> Revision 03

Added:
PT-105

Removed:
TT-201

Modified:
PT-101.Range

Relationship Changed:
PT-101 -> V-101
```

Classify:

- Added entity
- Removed entity
- Modified entity
- Added attribute
- Removed attribute
- Changed attribute
- Added relationship
- Removed relationship
- Changed relationship

Every result must retain source revision provenance.

---

## 9. Engineering Entity Layer

Engineering entities must become first-class concepts.

Initial target catalog:

```text
Drawing
Sheet
Tag
Equipment
Instrument
Valve
Line
Loop
Cable
JunctionBox
IOPoint
Motor
Pump
Vessel
Tank
HeatExchanger
Requirement
Specification
```

Do not implement every entity blindly. Prioritize according to real PEMS document types and project needs.

---

## 10. Engineering Tag

`EngineeringTag` should be a first-class entity.

Example:

```text
PT-101
```

must not exist as unrelated strings across:

- P&ID
- Instrument Index
- Datasheet
- Loop Diagram
- I/O List
- Cable Schedule

Preferred structure:

```text
EngineeringTag
  +-- DocumentOccurrence
  +-- Equipment
  +-- Instrument
  +-- Loop
  +-- Line
  +-- Cable
  +-- IOPoint
```

Separate tag identity from its occurrence in a document.

---

## 11. Engineering Entity Identity

Each engineering entity should have a stable internal identity.

Example:

```text
EngineeringEntityId
TagNo
EntityType
ProjectId
DisciplineId
CurrentStatus
```

A tag number is not necessarily the database identity.

This allows the same engineering entity to survive:

```text
Revision 01
Revision 02
Revision 03
```

while its attributes and document occurrences evolve.

---

## 12. Engineering Attributes

Important attributes must be structured enough to support:

- Search
- Validation
- Comparison
- Reconciliation
- Reporting
- AI reasoning

Example:

```text
PT-101
  Range = 0..100 bar
  Accuracy = 0.5%
  Signal = 4-20mA
  Service = Reactor Feed
```

Flexible JSON may be used for genuinely variable attributes, but core engineering attributes should not be hidden in one uncontrolled blob.

---

## 13. Document Occurrence

Separate entity identity from evidence of its appearance in a document.

Recommended concept:

```text
EngineeringEntityOccurrence
```

Possible fields:

- EngineeringEntityId
- DocumentId
- RevisionId
- PageId
- SheetId
- Region
- BoundingBox
- ExtractedText
- ExtractionMethod
- Confidence
- ReviewStatus

---

## 14. Provenance

Every AI-extracted or AI-inferred engineering fact must be traceable.

Minimum:

```text
Document
Revision
Page
Sheet
Region / BoundingBox
ExtractionMethod
Model
ModelVersion
Confidence
CreatedAt
Reviewer
ReviewStatus
```

Example:

```text
PT-101
Range = 0-100 bar

Source:
INS-DS-001 Rev.03
Page 7
Region X/Y/W/H
Confidence 0.94
Reviewer <employee>
ReviewStatus Approved
```

No AI-generated engineering fact should silently become authoritative.

---

## 15. AI Extraction

Use staged processing:

```text
Source Document
      |
Extraction
      |
AI Staging
      |
Validation
      |
Human Review
      |
Approved Engineering Entity / Attribute
      |
Authoritative Record
```

Do not use:

```text
PDF -> AI -> Production DB
```

without appropriate validation and review.

---

## 16. AI Confidence

Retain:

- Confidence
- Extraction method
- Model
- Model version
- Timestamp
- Reviewer
- Review status

Suggested policy:

```text
High confidence   -> automatic candidate
Medium confidence -> human review
Low confidence    -> mandatory human review
```

Thresholds must be configurable and validated against project data.

---

## 17. Relationship Model

Engineering relationships must be first-class.

Example:

```text
PT-101
  +-- Loop-101
  +-- JB-12
  +-- Cable-C102
  +-- V-101
  +-- P&ID-001
```

A relationship should capture:

- Source Entity
- Target Entity
- Relationship Type
- Source Document
- Source Revision
- Confidence
- Detection Method
- Review Status
- Created By / AI
- Created At

AI-suggested relationships are not authoritative until reviewed where required.

---

## 18. Knowledge Graph

The long-term target is an Engineering Knowledge Graph.

```text
Engineering Entity
      |
Relationship
      |
Engineering Entity
```

Example:

```text
Pump P-101
  +-- driven-by --> Motor M-101
  +-- monitored-by --> PT-101
  +-- located-at --> Unit-100
  +-- documented-in --> PID-001
```

Support:

- Dependency analysis
- Impact analysis
- Similar equipment
- Related documents
- Cross-document navigation
- AI grounding
- Revision impact
- CMMS handover

Do not introduce a graph database simply for technology reasons. Establish the logical graph model first.

---

## 19. Cross-Document Reconciliation

This is a P0 capability.

Example:

```text
PT-101

Datasheet Rev.03:
Range = 0-100 bar

Instrument Index Rev.02:
Range = 0-150 bar
```

Output:

```text
CONFLICT
```

with:

- Entity
- Attribute
- Source A
- Source B
- Revision A
- Revision B
- Values
- Severity
- Confidence
- Review Status
- Resolution
- Reviewer

---

## 20. Conflict Detection

Support:

```text
Attribute Conflict
Relationship Conflict
Missing Entity
Duplicate Entity
Revision Conflict
Document Status Conflict
Reference Conflict
Unit Conflict
Range Conflict
Classification Conflict
```

Severity:

```text
Info
Low
Medium
High
Critical
```

Severity must be rule-driven and configurable.

---

## 21. Orphan Detection

Detect records with no expected counterpart.

Examples:

```text
Tag in P&ID but no Datasheet
Tag in Datasheet but not in P&ID
Cable references PT-101 but no corresponding Loop
Equipment exists but no authoritative document
```

Orphan findings must be reviewable and actionable.

---

## 22. Golden Engineering Record

The target is a reconciled engineering record.

Example:

```text
PT-101

Golden Value:
Range = 0-100 bar

Sources:
Datasheet Rev.03
Instrument Index Rev.03
Loop Diagram Rev.02

Conflicting Source:
Instrument Index Rev.02
Range = 0-150 bar

Resolution:
Engineer Review #123
```

The Golden Record must preserve:

- Source evidence
- Revision
- Resolution
- Reviewer
- Audit trail

It is not simply the latest database value.

---

## 23. MDR

MDR should be a register/projection over the Document Core.

```text
MDR
  +-- Document
  +-- Revision
  +-- Planned Date
  +-- Actual Date
  +-- Status
  +-- Comment Status
  +-- Workflow
  +-- Transmittal
  +-- Discipline
  +-- Responsible Party
```

MDR must not become a second independent document master.

---

## 24. VDR Integration

Preferred:

```text
Document Core
  +-- EngineeringDocument
  +-- VDRItem
```

Core document identity remains authoritative.

VDR-specific business data remains in VDR.

Avoid duplicate authoritative fields across Core and VDR. If projections are required, define synchronization rules.

---

## 25. Engineering Search

Support:

```text
Exact
Partial
Prefix
Fuzzy
Code
Tag
Number
Semantic
```

Search domain:

```text
Documents
Tags
Equipment
Instruments
Loops
Lines
Cables
Specifications
Requirements
Comments
Revisions
Conflicts
```

Natural-language engineering queries should retrieve semantically relevant records, not only title matches.

---

## 26. Engineering Copilot

Preferred:

```text
User Question
  |
Intent Detection
  |
Semantic Retrieval
  |
Engineering Entity Retrieval
  |
Source Document Retrieval
  |
Provenance
  |
Answer
```

Answers should provide evidence.

If conflicting sources exist, the Copilot must expose the conflict rather than hide it.

---

## 27. Controller Boundary

Keep controllers thin:

```text
Controller
  |
Application Use Case
  |
Domain/Application Service
  |
Repository
```

Avoid controllers accumulating:

- DbContext operations
- Workflow logic
- Permission logic
- Comment logic
- Letter logic
- AI logic
- Business rules

Refactor incrementally without breaking working behavior.

---

## 28. Authorization

Enforce server-side:

- Authentication
- Roles
- Permissions
- Discipline scope
- Resource scope
- Approval authority
- AI review authority
- Revision control
- Manual numbering
- Delete permissions

Hiding a UI action is not authorization.

---

## 29. Routing

Do not hardcode Area-sensitive URLs.

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
Engineering
Admin
VDR
CMMS
MDR
```

---

## 30. Engineering Workspace UI/UX

Move from isolated CRUD screens toward a unified Engineering Workspace.

Target:

```text
+---------------------------------------------------+
| Document Header                                   |
| Number | Title | Discipline | Revision | Status |
+---------------------------------------------------+
| Workflow | Approval | Review | Actions           |
+---------------------------------------------------+
| Document Viewer / Page Viewer                    |
+---------------------------------------------------+
| Entities | Relationships | Conflicts | Revisions |
+---------------------------------------------------+
| AI Findings | Provenance | Comments              |
+---------------------------------------------------+
```

The user should be able to understand one engineering document without navigating through unrelated pages.

---

## 31. UI States

Every major Engineering page must support:

- Loading
- Empty
- Error
- Unauthorized
- No results
- Validation error
- Processing
- AI review required
- Conflict detected
- Approved
- Superseded

Do not implement only the happy path.

---

## 32. RTL / Localization

Engineering UI must support Persian and English without breaking:

- Layout
- Routing
- Tables
- Icons
- Numeric fields
- Search
- Document numbers
- Technical identifiers
- Charts
- AI output

Language and direction are separate concerns.

Do not rely on browser Back navigation to repair RTL/LTR state.

---

## 33. Database and EF Core Rules

Before Domain changes:

1. Inspect the Entity.
2. Inspect `DbContext`.
3. Inspect Entity configuration.
4. Inspect relationships.
5. Inspect indexes.
6. Inspect unique constraints.
7. Inspect migrations.
8. Inspect model snapshot.
9. Check existing data.
10. Check all consumers.

Do not create migrations for changes that do not alter schema.

Never perform destructive schema changes without a data-preservation plan.

---

## 34. Data Integrity

Use:

- Foreign keys
- Unique constraints
- Validation
- Concurrency control
- Transaction boundaries
- Audit records

Protect approved engineering history from uncontrolled editing.

---

## 35. Performance

Engineering list/search pages must use:

- Server-side pagination
- DTO/ViewModel projection
- Indexed filters
- Async database access
- Controlled eager loading
- Search-specific indexing

Do not load entire registers, revisions, attachments, or engineering entities into memory.

---

## 36. Implementation Priority

### P0 — Architecture Foundation

1. Engineering Entity model
2. Entity Identity
3. Document Occurrence
4. Provenance
5. Separate Document and AI states
6. Relationship model
7. Reconciliation model
8. Conflict model

### P1 — Intelligence

9. Revision Intelligence
10. Cross-Document Reconciliation
11. Orphan Detection
12. Golden Engineering Record
13. Knowledge Graph logical model
14. Engineering Semantic Search

### P2 — Experience

15. Engineering Workspace UI
16. Provenance Viewer
17. Conflict Dashboard
18. Revision Comparison UI
19. Engineering Copilot improvements
20. Engineering Analytics

---

## 37. Migration Strategy

Do not perform a destructive rewrite.

Use:

```text
Existing Engineering
  |
Compatibility Layer
  |
New Entity Model
  |
Backfill / Reconciliation
  |
Validation
  |
Gradual Adoption
  |
Legacy Retirement
```

Before deleting or renaming an existing field:

- Search all usages
- Determine data volume
- Determine dependencies
- Create migration/backfill plan
- Validate existing records
- Only then deprecate

---

## 38. Required Audit Checklist

```text
[ ] Existing implementation inspected
[ ] Existing entity reused where possible
[ ] No duplicate business concept introduced
[ ] Domain boundary verified
[ ] Relationships verified
[ ] Source of truth defined
[ ] Provenance defined
[ ] Authorization verified
[ ] Routing verified
[ ] Workflow verified
[ ] Audit verified
[ ] Search verified
[ ] RTL/LTR verified
[ ] Localization verified
[ ] UI states verified
[ ] Existing data preserved
[ ] Migration decision documented
[ ] Build passes
[ ] Tests pass
[ ] Runtime verified
[ ] AI output is traceable
[ ] Conflicts are not hidden
```

---

## 39. Required Claude Implementation Protocol

Before modifying PEMS Engineering, Claude must:

1. Read this document.
2. Inspect the existing repository.
3. Identify existing implementations.
4. Build an impact map.
5. Identify duplicates.
6. Identify source-of-truth fields.
7. Identify required schema changes.
8. Identify migration risk.
9. Implement the smallest safe change.
10. Build and test.
11. Report exactly what changed.

Claude must not:

- Rewrite working modules without justification.
- Create duplicate entities.
- Create duplicate workflow engines.
- Create duplicate document repositories.
- Create duplicate employee masters.
- Create uncontrolled engineering strings when a controlled model exists.
- Let AI silently overwrite authoritative engineering data.
- Hide conflicts.
- Remove legacy data without a migration plan.
- Claim completion without build/runtime verification.

---

## 40. Required Implementation Report

Every significant Engineering change must end with:

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

AI
- ...

UI/UX
- ...

VALIDATION
- Build:
- Tests:
- Runtime:

RISKS
- Low / Medium / High / Critical

FOLLOW-UP
- ...
```

---

## 41. Final Architecture

```text
                         PEMS ENGINEERING
                                |
              +-----------------+-----------------+
              |                                   |
       DOCUMENT MANAGEMENT              ENGINEERING INTELLIGENCE
              |                                   |
      +-------+-------+                    +------+------+
      |       |       |                    |             |
 Engineering VDR     MDR              Entities    Relationships
      |                                   |             |
 Revision Workflow                  Attributes       Graph
      |                                   |
 Transmittal                        Provenance
      |                                   |
      +-------------------+---------------+
                          |
                  Reconciliation
                          |
             +------------+------------+
             |            |            |
          Conflict     Missing       Orphan
             |
       Engineer Review
             |
      Golden Engineering Record
             |
       Knowledge Graph
             |
      Engineering Intelligence
             |
     +-------+--------+---------+
     |                |         |
    VDR              CMMS      AI
```

---

## 42. Final Principle

PEMS Engineering must not become a collection of document CRUD screens.

It must become an engineering knowledge and intelligence layer connecting:

```text
Documents
+
Revisions
+
Engineering Entities
+
Attributes
+
Relationships
+
Evidence
+
Workflow
+
Human Review
+
AI
+
MDR
+
VDR
+
CMMS
```

The engineering system must remain:

```text
Traceable
Auditable
Relational
Revision-aware
Conflict-aware
AI-assisted
Human-governed
Backward-compatible
Engineering-safe
```

The objective is not to build the largest engineering system.

The objective is to build a system in which an engineer can trust:

```text
What is this?
Where did it come from?
Which revision is authoritative?
What changed?
What conflicts?
What is missing?
What is affected?
Who reviewed it?
What should happen next?
```

That is the target definition of PEMS Engineering Intelligence.
