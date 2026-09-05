# PEMS CMMS Maintenance Organization Blueprint

**File:** `PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md`  
**System:** PEMS  
**Domain:** CMMS / Maintenance Organization / Labor / Skills / Work Management  
**Status:** Architecture Baseline — Audit Before Implementation

## 1. Purpose

Define the target organizational and workforce architecture for PEMS CMMS.

The following concepts are related but must remain distinct:

- Organization
- Department
- Maintenance Function
- Maintenance Group
- Team
- Crew
- Employee
- Skill
- Qualification
- Certification
- WorkOrderLabor
- Discipline

The objective is to avoid a flat maintenance-personnel model and to support realistic petrochemical maintenance execution.

## 2. Non-Negotiable Rules

### Employee Master

The existing PEMS Employee/Employees model remains the authoritative employee master.

Do not create a second CMMS employee master.

CMMS may reference employees through:

- TeamMember
- CrewMember
- WorkOrderLabor
- Supervisor
- Planner
- Technician
- Reviewer

### Discipline vs Maintenance Group

These are different concepts.

```text
Discipline:
Mechanical
Electrical
Instrumentation
Civil
Piping
Process

Maintenance Group:
Rotating Equipment
Static Equipment
Electrical Maintenance
Instrumentation & Control
Piping Maintenance
```

Engineering may use Discipline; maintenance execution uses Maintenance Group.

### Department vs Discipline

A Department represents organizational ownership.

A Discipline represents technical classification.

Never assume:

```text
Department == Discipline
```

### Maintenance Group vs Team

A Maintenance Group represents a functional capability.

A Team is an operational unit within that capability.

Example:

```text
Mechanical Maintenance
  -> Rotating Equipment
      -> Team A
      -> Team B
```

### Team vs Crew

A Team may be a persistent organizational unit.

A Crew may be a working combination of personnel for a specific activity, work order, or work package.

Do not collapse them without repository and business validation.

### Skill vs Qualification vs Certification

These are separate concepts.

```text
Skill:
Pump Alignment

Qualification:
Qualified Pump Maintenance Technician

Certification:
Certificate No. XXX
Issuer: ...
Valid From: ...
Valid To: ...
```

### AI

AI may recommend classifications, skills, assignments, or required competencies.

AI must not silently make authoritative workforce or authorization records.

## 3. Target Organizational Model

```text
Organization
  |
  +-- Department
        |
        +-- Maintenance Function
              |
              +-- Maintenance Group
                    |
                    +-- Team
                          |
                          +-- TeamMember
                                |
                                +-- Employee
```

Employee capabilities are separate:

```text
Employee
  |
  +-- EmployeeSkill
  +-- EmployeeQualification
  +-- EmployeeCertification
```

Work execution is separate:

```text
WorkOrder
  |
  +-- Team
  +-- Crew
  +-- WorkOrderLabor
  +-- RequiredSkill
```

## 4. Maintenance Functions

Initial candidate functions:

- Maintenance Planning
- Mechanical Maintenance
- Electrical Maintenance
- Instrumentation & Control
- Piping Maintenance
- Civil Maintenance
- HVAC / Utility Maintenance
- Telecommunication Maintenance
- Inspection / Integrity
- Reliability Engineering
- Shutdown / Turnaround
- Workshop / Fabrication

These are architecture candidates, not permission to blindly seed all values.

Claude must inspect the repository and real existing master data first.

## 5. Mechanical Maintenance

Candidate capabilities:

```text
Mechanical Maintenance
  +-- Rotating Equipment
  +-- Static Equipment
  +-- Valve / Mechanical Valve
  +-- Workshop / Machining
  +-- Fabrication
```

Typical rotating equipment:

- Pump
- Compressor
- Turbine
- Fan
- Blower
- Gearbox
- Coupling
- Bearing
- Mechanical Seal

Typical static equipment:

- Vessel
- Column
- Tank
- Heat Exchanger
- Drum
- Reactor
- Filter

Do not automatically create a database group for every category. Determine the required granularity from the existing system and operating model.

## 6. Electrical Maintenance

Candidate capabilities:

```text
Electrical Maintenance
  +-- Motors
  +-- MCC
  +-- Switchgear
  +-- Transformers
  +-- Power Distribution
  +-- Cables
  +-- Protection
  +-- UPS
  +-- Battery
  +-- Emergency Power
```

## 7. Instrumentation & Control

Candidate capabilities:

```text
Instrumentation & Control
  +-- Field Instruments
  +-- Control Valves
  +-- PLC
  +-- DCS
  +-- SIS
  +-- Analyzer
  +-- Calibration
  +-- Control Systems
  +-- Fire & Gas
```

Engineering tags, loops, equipment, and documents must not be duplicated inside CMMS.

## 8. Piping Maintenance

Candidate capabilities:

```text
Piping Maintenance
  +-- Pipeline
  +-- Flange
  +-- Expansion Joint
  +-- Supports
  +-- Valves
  +-- Piping Repair
  +-- Hot Work
```

Piping may organizationally belong under Mechanical in the real plant. Do not hard-code the hierarchy without verifying the business structure.

## 9. Inspection / Integrity

Inspection is related to maintenance but is not automatically an ordinary maintenance execution group.

Candidate capabilities:

- NDT
- Thickness Measurement
- RBI
- Pressure Test
- Vessel Inspection
- Piping Inspection
- PSV Inspection
- Corrosion
- Integrity Assessment

It must integrate with:

```text
Asset
Equipment
FunctionalLocation
Inspection
Measurement
Failure
WorkOrder
Document
```

## 10. Reliability Engineering

Reliability is primarily an engineering and analysis function.

Candidate capabilities:

- RCA
- RCM
- FMEA
- Failure Analysis
- MTBF
- MTTR
- Availability
- Bad Actor Analysis
- Reliability Improvement

Do not force Reliability into a technician/team model merely because it participates in maintenance.

## 11. Maintenance Planning

Planning is a planning and control function.

Candidate responsibilities:

- Backlog
- Scheduling
- Resource Planning
- Maintenance Plans
- Job Plans
- Shutdown Planning
- Work Packages

Important distinction:

```text
Planner != Technician
```

Both may be Employees but have different roles, permissions, skills, and responsibilities.

## 12. Shutdown / Turnaround

Shutdown should not automatically be a permanent maintenance group.

Prefer an operational context/campaign where required:

```text
Shutdown / Turnaround Campaign
  +-- Work Packages
  +-- Planning
  +-- Contractors
  +-- Mechanical
  +-- Electrical
  +-- Instrumentation
  +-- Piping
  +-- Civil
  +-- Inspection
  +-- QA/QC
```

The same employee or team may participate in normal maintenance and a shutdown campaign.

## 13. Team

A Maintenance Group may contain multiple Teams.

Example:

```text
Maintenance Group:
Rotating Equipment

Teams:
  Team A
  Team B
  Team C
```

Teams may support:

- Identity
- Code
- Name
- Active status
- Leader/Supervisor where required
- Members
- Group association
- Capability association

Never create a second employee identity inside Team.

## 14. Crew

A Crew is an execution-oriented combination of personnel.

Example:

```text
Pump Maintenance Crew
  +-- Mechanical Technician
  +-- Electrician
  +-- Instrument Technician
  +-- Rigger
  +-- Welder
  +-- Supervisor
```

A Crew may be persistent, temporary, work-order-specific, or work-package-specific.

Verify the existing PEMS implementation before creating a new Crew entity.

## 15. Employee

Use the enterprise Employee master.

An employee may participate through:

```text
Employee
  +-- Department
  +-- Team Membership
  +-- Crew Membership
  +-- Skills
  +-- Qualifications
  +-- Certifications
  +-- WorkOrderLabor
  +-- Approval Authority
  +-- Planning Role
  +-- Inspection Role
```

Do not assume one employee can belong to only one team unless the business requirement explicitly requires it.

## 16. Skill

Skills represent capabilities.

Examples:

- Pump Alignment
- Vibration Analysis
- Mechanical Seal Replacement
- Welding
- NDT
- Pressure Calibration
- Temperature Calibration
- DCS
- PLC
- SIS
- Electrical Protection
- Motor Testing

Skills may be associated with:

- Employee
- Team
- Maintenance Task
- Job Plan
- Work Order requirement

## 17. Required Skill

A Maintenance Task may require skills.

Example:

```text
Maintenance Task:
Pump Alignment

Required Skills:
  Pump Alignment
  Vibration Analysis
```

This enables skill-based workforce assignment rather than relying only on MaintenanceGroupId.

## 18. Qualification

Qualifications represent eligibility or demonstrated competency.

Possible fields:

- Qualification Type
- Issuer
- Issue Date
- Expiry Date
- Status
- Evidence Document
- Employee

## 19. Certification

Certifications must support traceability.

Possible fields:

- Certificate Number
- Certificate Type
- Issuer
- Issue Date
- Expiry Date
- Status
- Evidence Document
- Employee

Expired certifications must be detectable.

Where safety or legal rules require it, unauthorized personnel must not be assignable to restricted work.

## 20. WorkOrderLabor

Do not depend on a single employee field for work execution.

A Work Order may involve multiple people.

Conceptual structure:

```text
WorkOrder
  |
  +-- WorkOrderLabor
        +-- Employee
        +-- Role
        +-- Skill
        +-- Hours
        +-- Regular/Overtime
        +-- Cost
        +-- Start/End
```

Use existing PEMS role/master-data architecture where appropriate.

## 21. Work Order Assignment

A Work Order may reference:

- Primary Team
- Crew
- Responsible Employee
- WorkOrderLabor
- Required Skills

These are different concepts.

Example:

```text
WO-2026-00125

Primary Team:
Rotating Equipment Team A

Crew:
Pump Maintenance Crew

Responsible:
Supervisor A

Labor:
Technician 1
Technician 2
Rigger 1

Required Skills:
Pump Alignment
Mechanical Seal
```

## 22. Discipline Integration

Do not merge Discipline and Maintenance Group.

Example:

```text
Engineering Discipline:
Mechanical

Maintenance Group:
Rotating Equipment
```

Another:

```text
Engineering Discipline:
Instrumentation

Maintenance Group:
Instrumentation & Control
```

Where justified, the relationship may be many-to-many.

## 23. Asset Integration

Maintenance organization must integrate with the Asset hierarchy:

```text
Asset
  |
  +-- FunctionalLocation
  +-- Equipment
  +-- MaintenanceStrategy
  +-- MaintenancePlan
  +-- MaintenanceTask
  +-- WorkOrder
  +-- WorkOrderLabor
```

Do not duplicate equipment identity inside maintenance groups.

## 24. Skill-Based Assignment

Long-term target:

```text
WorkOrder
  |
RequiredSkill
  |
CandidateEmployees
  |
Qualification / Certification Check
  |
Availability
  |
Team / Crew
  |
Assignment
```

AI may assist with recommendations but must not bypass authorization, qualification, certification, or safety rules.

## 25. Security

Permissions must be independent from organizational classification.

Examples:

- Maintenance Planner
- Maintenance Supervisor
- Maintenance Technician
- Reliability Engineer
- Inspection Engineer
- CMMS Administrator

A user belonging to Mechanical does not automatically have permission to approve all Mechanical Work Orders.

## 26. UI/UX

CMMS administration should provide a clear hierarchy:

```text
Maintenance
  +-- Functions
  +-- Groups
  +-- Teams
  +-- Crews
  +-- Employees
  +-- Skills
  +-- Qualifications
  +-- Certifications
```

If PEMS already has Department, Employee, Role, Permission, or Organization screens, integrate with them instead of duplicating administration.

## 27. Database Safety

Before any schema change:

1. Inspect entities.
2. Inspect DbContext.
3. Inspect entity configurations.
4. Inspect foreign keys.
5. Inspect indexes.
6. Inspect unique constraints.
7. Inspect migrations.
8. Inspect model snapshot.
9. Inspect seed data.
10. Inspect current data.
11. Inspect all consumers.

Then decide:

```text
Reuse
Extend
Refactor
New Entity
```

No destructive migration without a data-preservation plan.

## 28. Data Preservation

Before modifying Employee, Department, Team, or maintenance tables:

- Count existing rows.
- Identify foreign keys.
- Identify orphaned records.
- Identify duplicate business keys.
- Identify nullability constraints.
- Identify seed dependencies.
- Identify migration dependencies.

Do not casually rename or delete existing fields.

## 29. Naming

Preferred terminology:

```text
Employee
Team
TeamMember
Crew
CrewMember
MaintenanceFunction
MaintenanceGroup
Skill
EmployeeSkill
Qualification
EmployeeQualification
Certification
EmployeeCertification
WorkOrderLabor
RequiredSkill
```

These names are proposals only. If equivalent concepts already exist, reuse or extend them.

## 30. Duplicate Prevention

Search for semantic equivalents, not only exact names.

Potential overlaps include:

```text
Employee / Staff / Personnel / Worker / Labor / Technician
Team / Crew / WorkGroup / MaintenanceTeam
MaintenanceGroup / MaintenanceFunction
Skill / Competency / Capability
Qualification / Certification
```

Do not create a new model until existing equivalents have been checked.

## 31. Migration Strategy

Use:

```text
Existing Model
  |
Compatibility
  |
New Relationships
  |
Backfill
  |
Validation
  |
Adoption
  |
Deprecation
```

Never use a destructive rewrite as the default strategy.

## 32. Required Repository Audit

Before implementation Claude must report:

1. Existing Employee model
2. Existing Department model
3. Existing Organization model
4. Existing MaintenanceGroup equivalent
5. Existing Team/Crew equivalents
6. Existing Skill equivalents
7. Existing Qualification/Certification equivalents
8. Existing WorkOrderLabor model
9. Existing Role/Permission model
10. Existing CMMS entities
11. Existing Engineering Discipline model
12. Existing Asset hierarchy
13. Existing migrations
14. Existing seed data
15. Duplicate concepts
16. Missing concepts
17. Recommended reuse/extension/new entities
18. Migration risks
19. Data preservation plan

## 33. Implementation Order

### Phase 0 — Repository Audit

No schema changes.

### Phase 1 — Organization/Labor Foundation

After audit:

- Maintenance Function
- Maintenance Group
- Team
- TeamMember
- Crew if required

### Phase 2 — Skills

- Skill
- EmployeeSkill
- RequiredSkill

### Phase 3 — Qualification

- Qualification
- EmployeeQualification
- Certification where required

### Phase 4 — Work Execution

- WorkOrderLabor
- Team assignment
- Crew assignment
- Skill validation

### Phase 5 — Asset Integration

Connect to:

- Asset
- Functional Location
- Equipment
- Maintenance Plan
- Maintenance Task

### Phase 6 — Reliability / Inspection / Shutdown

Integrate:

- Reliability
- Inspection
- Integrity
- Shutdown
- Work Packages

### Phase 7 — Intelligence

Add:

- Skill-based assignment recommendation
- Workforce optimization
- Maintenance workload analysis
- AI recommendations

## 34. Testing

Every structural change must verify:

### Build

```text
dotnet build
```

### Database

- Migration generation
- Migration application
- Existing data validation

### Application

- Login
- Authorization
- Administration
- CMMS navigation
- Work Order creation
- Work Order assignment
- Employee selection
- Team selection
- Skill selection

### Regression

Verify existing Engineering, VDR, MDR, Administration, Asset Management, and CMMS behavior.

## 35. UI/UX Validation

Verify:

- RTL
- LTR
- Persian localization
- English localization
- Responsive behavior
- Loading states
- Empty states
- Validation states
- Error states
- Search
- Filtering
- Pagination
- Dialog behavior
- Navigation
- Area routing

Do not hard-code Area-sensitive URLs where MVC route generation is appropriate.

## 36. Claude Safety Protocol

Claude MUST NOT:

- Create duplicate Employee entities.
- Create duplicate Department entities.
- Create duplicate Discipline entities.
- Create duplicate Team/Crew entities without audit.
- Create a second authorization system.
- Create a second workflow engine.
- Create a second document master.
- Delete existing data.
- Drop columns/tables without a migration plan.
- Change existing seed IDs casually.
- Change primary keys casually.
- Rewrite working modules unnecessarily.
- Make AI output authoritative without review.
- Assume organizational hierarchy without evidence.
- Treat Discipline as Maintenance Group.
- Treat Team as Crew.
- Treat Skill as Qualification.
- Treat Qualification as Certification.
- Use UI hiding as authorization.
- Claim completion without build and validation.

## 37. Required Claude Output Before Coding

Claude must first return:

```text
CMMS MAINTENANCE ORGANIZATION AUDIT

A. Existing Architecture
B. Existing Entities
C. Existing Relationships
D. Existing Administration
E. Existing Employee Model
F. Existing Maintenance Model
G. Existing Skill/Labor Model
H. Existing WorkOrderLabor Model
I. Existing Engineering Discipline Model
J. Duplicate Concepts
K. Missing Concepts
L. Recommended Reuse
M. Recommended Extensions
N. Recommended New Entities
O. Migration Risk
P. Data Preservation Plan
Q. Implementation Sequence
```

Only after this audit is accepted should structural implementation begin.

## 38. Required Final Change Report

After implementation:

```text
IMPLEMENTED
- ...

CHANGED
- ...

REUSED
- ...

NEW ENTITIES
- ...

NOT CHANGED
- ...

DATABASE
- Migration Required: YES/NO
- Migration Applied: YES/NO

DATA
- Existing Records Preserved: YES/NO

WORKFLOW
- ...

SECURITY
- ...

UI/UX
- ...

VALIDATION
- Build:
- Tests:
- Runtime:

RISKS
- ...

FOLLOW-UP
- ...
```

## 39. Final Target Architecture

```text
PEMS
 |
Organization
 |
Department
 |
Maintenance Function
 |
Maintenance Group
 |
Team
 |
TeamMember
 |
Employee
 |
+-- Skill
+-- Qualification
+-- Certification
 |
RequiredSkill
 |
MaintenanceTask
 |
WorkOrder
 |
+-- Team
+-- Crew
+-- WorkOrderLabor
 |
Asset
 |
+-- FunctionalLocation
+-- Equipment
 |
+-- MaintenanceStrategy
+-- MaintenancePlan
+-- Failure
+-- Inspection
```

## 40. Final Principle

The objective is not to create more tables.

The objective is to create a correct maintenance operating model in which PEMS can answer:

```text
Who can perform this work?
Which team owns it?
Which crew is available?
What skills are required?
Who is qualified?
Which certifications are valid?
Which asset is affected?
Which maintenance strategy applies?
What work has been performed?
How much labor was consumed?
What failed?
Why did it fail?
What should happen next?
```

The architecture must remain:

```text
Employee-centered
Skill-aware
Asset-aware
Work-order-centric
Auditable
Traceable
Permission-controlled
Migration-safe
AI-assisted
Human-governed
```

This document is an architecture directive, not permission to immediately create every proposed entity.

The repository is the source of truth for what already exists.

The first implementation step is always:

```text
AUDIT -> MAP -> DECIDE -> IMPLEMENT -> MIGRATE SAFELY -> BUILD -> TEST -> VERIFY
```
