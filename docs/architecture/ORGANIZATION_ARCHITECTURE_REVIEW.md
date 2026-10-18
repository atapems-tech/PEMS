# Organization Architecture Review (pre-import)

Status: **review only - DATABASE ACTION: NOT AUTHORIZED.** Companion workbook: `docs/proposals/PEMS_Organization_Structure_Proposal.xlsx` (8 sheets). Nothing here has been imported or created.

## 1. Architecture (separation of concepts)

Enterprise -> **Organizational Units/Departments** (may contain Positions) - **Position hierarchy** (Executive -> Deputy/Director -> Department head -> Operational) - **EmployeeAssignment -> Employee** - **Operational structures** (Discipline, MaintenanceGroup, Team, Crew: already exist, must not be duplicated as Departments) - **Authorization** (Role, Permission).
Reporting lines exist only in `Position.ParentPositionId`; an employee's manager is always derived (Employee -> active primary assignment -> Position -> parent -> holder).

## 2. Department

* Real Departments (14, all flat): Construction, Document Control, Engineering (domain Engineering), Finance, HSE, Human Resources, IT, Maintenance (domain Maintenance), Operations (domain Operations), Planning & Control, Procurement, Project Management, QA/QC, WAREHOUSE (**domain NULL**).
* Test Departments: **59 at generation time** (54 earlier in the same session; each full regression run adds more because tests write into the shared dev DB). Also 60 test employees, 10 test positions, ~143 test disciplines.
* Recommended retained: all 14 real ones (Construction is outside both the tree and the target list: owner to confirm).
* Proposed new organizational unit: **Supply Chain** (parent of Procurement and WAREHOUSE). Nothing else.
* The 10 sub-units -> **position hierarchy, not Departments** (General Engineering, Mechanical/Electrical/Instrumentation Maintenance, Maintenance Planning, Reliability/CM, Material/Part Catalogue, Logistics; Project Engineering = REVIEW). Reason: Discipline/MaintenanceGroup/Team already model the operational split and none shows independent workflow/authorization/budget evidence. Their employees keep `Employee.DepartmentId` = the parent Department.
* `Position.DepartmentId` is nullable: a Position may legally have no Department. The import's mandatory Department is only the import's own rule.

## 3. Positions (60 proposed, all vacant)

| Code | Title (EN, proposed) | Parent | Classification | Status | Note |
|---|---|---|---|---|---|
| MD | Managing Director | - | Position | PROPOSED | Valid root position (no parent). |
| ENG-DEP | Deputy Head - Engineering | MD | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| ENG-GEN-MGR | Manager - General Engineering | ENG-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| ENG-MECH-HEAD | Head of Mechanical Engineering | ENG-GEN-MGR | Position | PROPOSED |  |
| ENG-MECH-ENG | Mechanical Engineer | ENG-MECH-HEAD | Position | PROPOSED |  |
| ENG-MECH-SPEC | Mechanical Specialist | ENG-MECH-HEAD | Position | PROPOSED |  |
| ENG-ELEC-HEAD | Head of Electrical Engineering | ENG-GEN-MGR | Position | PROPOSED |  |
| ENG-ELEC-ENG | Electrical Engineer | ENG-ELEC-HEAD | Position | PROPOSED |  |
| ENG-ELEC-SPEC | Electrical Specialist | ENG-ELEC-HEAD | Position | PROPOSED |  |
| ENG-INST-HEAD | Head of Instrumentation Engineering | ENG-GEN-MGR | Position | PROPOSED |  |
| ENG-INST-ENG | Instrumentation Engineer | ENG-INST-HEAD | Position | PROPOSED |  |
| ENG-INST-SPEC | Instrumentation Specialist | ENG-INST-HEAD | Position | PROPOSED |  |
| ENG-PROC-HEAD | Head of Process Engineering | ENG-GEN-MGR | Position | PROPOSED |  |
| ENG-PROC-ENG | Process Engineer | ENG-PROC-HEAD | Position | PROPOSED |  |
| ENG-PRJ-MGR | Manager - Project Engineering | ENG-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| ENG-PRJ-CTRL | Project Control | ENG-PRJ-MGR | Review Required | REVIEW_REQUIRED | Unit label or position? Treated as a position - REVIEW. // ANALYSIS: Recommend FUNCTIONAL_AREA (project controls) staffe |
| ENG-PRJ-ENG | Project Engineering (position) | ENG-PRJ-MGR | Review Required | REVIEW_REQUIRED | Unit label or position? (same name as the parent unit) - REVIEW. // ANALYSIS: Recommend POSITION (Project Engineer / Pro |
| MNT-DEP | Deputy Head - Maintenance | MD | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-MECH-MGR | Manager - Mechanical Maintenance | MNT-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-MECH-SHIFT-A | Shift Supervisor A - Mechanical Maintenance | MNT-MECH-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-MECH-SHIFT-B | Shift Supervisor B - Mechanical Maintenance | MNT-MECH-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-MECH-SHIFT-C | Shift Supervisor C - Mechanical Maintenance | MNT-MECH-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-MECH-TECH | Mechanical Technician | MNT-MECH-SHIFT-A | Position | PROPOSED | Listed only under Shift A in the tree. Do the same roles report to Shift B/C? REVIEW. |
| MNT-MECH-SKILLED | Skilled Worker | MNT-MECH-SHIFT-A | Position | PROPOSED | Listed only under Shift A in the tree. REVIEW. |
| MNT-ELEC-MGR | Manager - Electrical Maintenance | MNT-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-ELEC-SHIFT-A | Shift Supervisor A - Electrical Maintenance | MNT-ELEC-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-ELEC-SHIFT-B | Shift Supervisor B - Electrical Maintenance | MNT-ELEC-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-ELEC-SHIFT-C | Shift Supervisor C - Electrical Maintenance | MNT-ELEC-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-INST-MGR | Manager - Instrumentation Maintenance | MNT-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-INST-SHIFT-A | Shift Supervisor A - Instrumentation Maintenance | MNT-INST-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-INST-SHIFT-B | Shift Supervisor B - Instrumentation Maintenance | MNT-INST-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-INST-SHIFT-C | Shift Supervisor C - Instrumentation Maintenance | MNT-INST-MGR | Position | PROPOSED | Same title appears under 3 maintenance branches: distinct positions (different parent). |
| MNT-PLAN-MGR | Manager - Maintenance Planning & Control | MNT-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-PLAN-PM | PM Planner | MNT-PLAN-MGR | Position | PROPOSED |  |
| MNT-PLAN-CM | CM Planner | MNT-PLAN-MGR | Position | PROPOSED |  |
| MNT-PLAN-SCHED | Scheduling Specialist | MNT-PLAN-MGR | Position | PROPOSED |  |
| MNT-REL-MGR | Manager - Reliability / Condition Monitoring | MNT-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| MNT-REL-ENG | Reliability Engineer | MNT-REL-MGR | Position | PROPOSED |  |
| MNT-REL-CBM | CBM Engineer | MNT-REL-MGR | Position | PROPOSED |  |
| MNT-REL-ANALYST | Analyst | MNT-REL-MGR | Position | PROPOSED |  |
| OPS-DEP | Deputy Head - Operations | MD | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| OPS-PROD-MGR | Production Management | OPS-DEP | Review Required | REVIEW_REQUIRED | Leaf node: unit or the Production Manager position? Kept (not skipped); REVIEW. // ANALYSIS: Recommend POSITION (Product |
| OPS-SHIFT-A | Operations Shift Supervisor A | OPS-DEP | Position | PROPOSED | Sibling of Production Management in the tree. Should shifts report to the Production Manager instead? REVIEW. |
| OPS-SHIFT-B | Operations Shift Supervisor B | OPS-DEP | Position | PROPOSED | Sibling of Production Management in the tree. Should shifts report to the Production Manager instead? REVIEW. |
| OPS-SHIFT-C | Operations Shift Supervisor C | OPS-DEP | Position | PROPOSED | Sibling of Production Management in the tree. Should shifts report to the Production Manager instead? REVIEW. |
| SCM-DEP | Deputy Head - Supply Chain | MD | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| SCM-PUR-MGR | Manager - Purchasing | SCM-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| SCM-PUR-BUYER | Purchasing Specialist | SCM-PUR-MGR | Position | PROPOSED |  |
| SCM-PUR-CONTRACT | Contract Specialist | SCM-PUR-MGR | Position | PROPOSED |  |
| SCM-WH-MGR | Manager - Warehouse | SCM-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| SCM-WH-SUP | Warehouse Supervisor | SCM-WH-MGR | Position | PROPOSED |  |
| SCM-WH-SPEC | Warehouse Specialist | SCM-WH-MGR | Position | PROPOSED | Sibling of Warehouse Supervisor in the tree (both under Warehouse). Should specialists report to the supervisor? REVIEW. |
| SCM-LOG-MGR | Logistics Management | SCM-DEP | Review Required | REVIEW_REQUIRED | Leaf node: unit or Logistics Manager position? Kept; REVIEW. // ANALYSIS: Recommend POSITION (Logistics Manager) represe |
| SCM-MAT-MGR | Manager - Material / Part Catalogue | SCM-DEP | Position | IMPLIED | Unit-head position IMPLIED by the unit node; the tree does not name it. Confirm title/existence. |
| SCM-MAT-PCS | Part Catalogue Specialist | SCM-MAT-MGR | Position | PROPOSED |  |
| SCM-MAT-MDM | Material Master Data (specialist) | SCM-MAT-MGR | Review Required | REVIEW_REQUIRED | Unit label or position title? REVIEW. // ANALYSIS: Recommend POSITION (Material Master Data Specialist) inside the Mater |
| HSE-MGR | HSE Management | MD | Review Required | REVIEW_REQUIRED | Leaf node directly under MD: unit or the manager position? Kept; REVIEW. // ANALYSIS: Recommend POSITION (head of the ex |
| FIN-MGR | Finance Management | MD | Review Required | REVIEW_REQUIRED | Leaf node directly under MD: unit or the manager position? Kept; REVIEW. // ANALYSIS: Recommend POSITION (head of the ex |
| HR-MGR | Human Resources Management | MD | Review Required | REVIEW_REQUIRED | Leaf node directly under MD: unit or the manager position? Kept; REVIEW. // ANALYSIS: Recommend POSITION (head of the ex |
| IT-MGR | IT Management | MD | Review Required | REVIEW_REQUIRED | Leaf node directly under MD: unit or the manager position? Kept; REVIEW. // ANALYSIS: Recommend POSITION (head of the ex |

* 14 IMPLIED unit-head positions (`PROPOSED - IMPLIED BY ORGANIZATIONAL STRUCTURE`, not confirmed master data): ENG-DEP, ENG-GEN-MGR, ENG-PRJ-MGR, MNT-DEP, MNT-MECH-MGR, MNT-ELEC-MGR, MNT-INST-MGR, MNT-PLAN-MGR, MNT-REL-MGR, OPS-DEP, SCM-DEP, SCM-PUR-MGR, SCM-WH-MGR, SCM-MAT-MGR.
* 9 ambiguous nodes (still REVIEW_REQUIRED; recommendations in the workbook): Production Management -> POSITION; Logistics Management -> POSITION of a FUNCTIONAL_AREA; HSE/Finance/HR/IT Management -> HEAD POSITIONS of existing Departments; Project Control -> FUNCTIONAL_AREA; Project Engineering -> POSITION (Engineering vs Project Management = REVIEW); Material Master Data -> POSITION (specialist) inside the Part Catalogue functional area.
* Codes are **proposed** (no existing convention; `PositionCode` is new). They are the stable import key: freeze before import.

## 4. Employee model (verified)

* `Employee.DepartmentId` **required**; `Employee.DisciplineId` required; `Employee.Position` free text (display only); `Employee.PositionId` legacy FK: **no application code reads or writes it** (EF configuration only), so it cannot currently contradict assignments, but nothing prevents it either.
* Authoritative source: **active primary `EmployeeAssignment`** (unique index per employee; resolver and chart use only this).
* Vacant positions fully supported; Direct Manager resolves from the hierarchy (no `ManagerId`).
* Required diagnostics (specified, **not yet implemented**): per employee - Employee.Department, primary Position, Position.Department, status in {CONSISTENT, EMPLOYEE_DEPARTMENT_ONLY, POSITION_DEPARTMENT_ONLY, CONFLICT, NO_DEPARTMENT}; plus `LEGACY_POSITION_CONFLICT` when `Employee.PositionId` differs from the primary assignment's position. Read-only, no auto-repair.

## 5. Workflow / Department consumers (audited)

| Consumer | Depends on | Note |
|---|---|---|
| Work Order governance (`WorkOrderWorkflow.ResolveDepartmentDomainAsync`) | `Employee.DepartmentId` -> ancestors' `OrganizationDomain` | Only Maintenance/Operations domains drive rules. Sub-units as positions keep working because employees stay in the parent Department. Employee **Discipline** is also required by Verify/FinalApprove/Execute. |
| `EnterpriseActorResolver` scope `Department` | `Employee.DepartmentId` | Explicit scope filter; no active responsibility uses it. Dev leftover: `DirectManager` row has a Discipline scope (971644) and is inactive: reset to RequesterManager before go-live. |
| Direct/Superior Manager (Part Catalogue) | Position hierarchy only | Independent of Department. |
| Users admin, Employee list, Department page, Letters (recipient department) | `Employee.DepartmentId` / Department | CRUD and addressing, not approvals. |
| Store Requisition, IRN, MRS, Contract, MOC, Permit, NCR, Engineering Documents, TQ | Roles/Permissions (+ discipline checks) | Not Department-based. |
| Supply Chain approvers (Part Catalogue) | Pools, Position scope, commodity responsibles, hierarchy | Org-independent apart from the hierarchy. |

**Gap:** there is no single `IOrganizationResolver`. Equivalents are scattered: `OrganizationManagerChain` (manager), `WorkOrderWorkflow.ResolveDepartmentDomainAsync` (domain), `EnterpriseActorResolver` (actors), `OrganizationHierarchyService` (read model). Recommended: a thin `IOrganizationResolver` facade (employee -> primary position, holder, parent, direct manager, department (employee/position), domain) that **delegates** to these; then migrate `WorkOrderWorkflow` to it **without behavior change** (characterization tests first). Not implemented.

## 6. Data cleanup (proposal only - nothing deleted)

Workbook sheets 7-8. Dependencies: Departments <- 59 employees; test employees <- 59 EngineeringDocuments (ApprovedBy/CheckedBy), 10 assignments, 9 approvals + 7 Part Catalogue requests, 9 user accounts, 1 meeting attendee, 1 task; AuditLogs (~230k rows) reference ids without FK and must never be deleted. Safe sequence: backup -> export lists -> **deactivate** (reversible) -> re-point/archive the 59 documents -> only then consider physical delete in a generated FK order. Prevent recurrence: tests that write into PEMSDB should use isolated databases.

## 7. Import contract (change only after authorization)

Make Department **optional** (validated only when supplied); keep PositionCode as the key. Consumers of the required `Employee.DepartmentId` are unaffected (employees, not positions).

## 8. Recommendation

```
ORGANIZATION MODEL:  READY WITH DATA CLEANUP
DEPARTMENT MODEL:    READY WITH DATA CLEANUP (flat, test-polluted; add Supply Chain; set WAREHOUSE domain; sub-units stay positions)
POSITION MODEL:      READY (codes, 14 implied heads and 9 ambiguous nodes await human confirmation)
EMPLOYEE MODEL:      READY WITH DATA CLEANUP (add consistency + legacy-position diagnostics; personnel codes; test employees)
WORKFLOW RESOLUTION: READY, one structural improvement recommended (IOrganizationResolver facade, no behavior change)
DATABASE ACTION:     NOT AUTHORIZED
```
