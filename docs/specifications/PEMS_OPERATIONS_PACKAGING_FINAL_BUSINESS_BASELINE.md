# PEMS Operations — Final Packaging Business Baseline

**Document ID:** PEMS-OPS-PKG-FINAL-BUSINESS-BASELINE  
**Version:** 1.0  
**Date:** 2026-09-25  
**Status:** BUSINESS SIGN-OFF REQUIRED  
**Implementation Status:** BLOCKED UNTIL MANDATORY DECISIONS ARE APPROVED

> **Packaging business decisions are configurable policies, not permanent company-specific source-code rules.**
>
> Every approved decision D1–D7, E-4, E-5 and D12 in this document is the value of a versioned,
> effective-dated, audited **Packaging Policy** for the approving organization and scope. It is not a
> rule of the product. Another organization, another site, or a later effective date may approve a
> different policy without a source-code change. Domain safety invariants (inventory ledger
> integrity, idempotent posting, the Released-only posting gate, audit integrity, SoD) are not
> configurable. Normative rules, the policy model, and the repository architecture findings:
> `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md`. Architecture audit:
> `docs/audits/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_AUDIT.md`.
>
> **Architecture decisions M-1…M-5** (separate deployment per company; reusable policy infrastructure with
> domain-owned schema; typed values with stable codes; capability-readiness scope; governed approval through
> `IWorkflowRuntime`) are recorded in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`. They are *architecture* decisions:
> they choose how a business decision is represented, **never which option is chosen**, and they approve
> nothing in this document. Each sign-off here is the value of the initial policy (v1) for the named
> deployment and scope. A business approval of an option that is not yet an Implemented capability
> is recorded, but the resulting policy stays BLOCKED until the capability is Implemented, Tested,
> Authorized and Production-Ready.

---

# 1. Purpose

This document is the authoritative business baseline for the PEMS Packaging
implementation program.

It establishes the business decisions that must govern:

- Packaging domain architecture;
- Package genealogy;
- Product identity;
- Quality;
- Inventory;
- Barcode;
- Workflow;
- Security;
- Role mapping;
- KPI / PI / RI / KRI;
- Reporting;
- Financial treatment.

This document MUST be approved before dependent implementation begins.

---

# 2. Critical Governance Rule

A technical recommendation is NOT a business decision.

The following states are valid:

- OPEN
- OWNER REVIEW
- APPROVED
- REJECTED
- DEFERRED
- BLOCKED

Only an explicitly recorded business approval may change a decision to
APPROVED.

No developer, architect, AI assistant, or technical recommendation may
substitute for business approval.

---

# 3. Current PEMS Baseline

The current repository and live database establish:

- 20 live PEMS roles.
- 24 role assignments.
- 4 roles currently hold Production grants.
- QAManager currently has Production View + Approve.
- QAManager currently does NOT have Hold / ReleaseHold.
- EngineeringManager currently has the broad Production authority.
- MaintenanceEngineer currently has Production View/Create/Edit/Hold/ReleaseHold.
- Viewer currently has Production View.
- Packaging-specific roles do not currently exist.
- Warehouse-specific Packaging authority does not currently exist.
- Laboratory-specific role does not currently exist; QAManager is the current
  candidate mapping.
- Production quality gating from Phase 2C remains active.
- InventoryTransaction is the inventory ledger.
- MaterialStock is the inventory projection.
- IMaterialStockService is the existing stock mutation service.
- BarcodeIdentity is the existing barcode identity architecture.
- WorkflowPlatform exists, but Production currently uses explicit lifecycle
  transitions rather than the central workflow engine.
- The Phase 2E KPI catalog contains 56 metrics:
  - 15 KPI
  - 16 PI
  - 17 RI
  - 8 KRI

These facts are technical baselines, not business approvals.

---

# 4. Decision Register

## D1 — Package Definition

### Business Question

What is a Package in PEMS?

### Candidate Options

#### D1-A — Filled Product Unit

A Package is a physically filled and uniquely identifiable product unit
containing a defined quantity of one approved product.

#### D1-B — Generic Packaging Unit

A Package is any physical container/unit used to hold product, independent
of whether it is a saleable product unit.

#### D1-C — Logistics / Handling Unit

A Package represents a logistics or handling entity such as pallet,
container, drum group, or similar physical handling unit.

#### D1-D — Layered Model

Separate:

- Product Package
- Handling Unit
- Logistics Unit

This is architecturally richer but increases implementation complexity.

### Technical Recommendation

D1-A is the preferred initial implementation model because it provides a
clear product genealogy and avoids prematurely conflating Package with
Pallet / HandlingUnit.

This is ONLY a technical recommendation.

### BUSINESS DECISION

Selected Option:

`____________________________`

Business Owner:

`____________________________`

Approver:

`____________________________`

Decision Date:

`____________________________`

Evidence:

`____________________________`

Conditions:

`____________________________`

Status:

`OPEN`

---

# 5. D2 — Mixed Product Package

## Business Question

May a Package contain multiple MaterialItems/products?

### Options

#### D2-A — Single Product

One Package contains exactly one product / MaterialItem.

#### D2-B — Mixed Product

One Package may contain multiple products / MaterialItems.

#### D2-C — Primary Product + Controlled Components

One Package has one primary product while packaging materials/components
are separately represented.

### Technical Recommendation

D2-A for the product payload, combined with explicit packaging-material
consumption where bags, drums, labels or other packaging materials must be
tracked.

This avoids an unnecessarily complex mixed-product Package model.

### Packaging Material Decision

Determine whether:

- bags;
- drums;
- labels;
- liners;
- pallets;
- other packaging components

are inventory-controlled and consumed.

This must be explicitly defined.

### BUSINESS DECISION

Selected Product Model:

`____________________________`

Packaging Materials Tracked:

`YES / NO / PARTIAL`

Business Owner:

`____________________________`

Approver:

`____________________________`

Decision Date:

`____________________________`

Status:

`OPEN`

---

# 6. D3 — Multi-Batch Package

## Business Question

Can one Package contain material originating from multiple ProductionBatches?

### Options

#### D3-A — Single Batch

Each Package belongs to exactly one ProductionBatch.

#### D3-B — Multiple Batches

A Package may contain material from multiple ProductionBatches.

#### D3-C — Primary Batch + Secondary Genealogy

One primary batch is assigned while secondary batch contributions are
explicitly recorded.

### Technical Recommendation

D3-A for the initial implementation.

This provides the cleanest:

- genealogy;
- recall;
- quality;
- inventory;
- reporting;
- barcode traceability.

Multi-batch blending can be introduced later if formally required.

### BUSINESS DECISION

Selected Option:

`____________________________`

Business Owner:

`____________________________`

Approver:

`____________________________`

Decision Date:

`____________________________`

Status:

`OPEN`

---

# 7. D4 — Gross / Tare / Net

## Business Question

How shall Package quantity and weight be represented?

### Options

#### D4-A

Gross and tare are measured/stored; net is derived.

`NET = GROSS - TARE`

#### D4-B

Gross and net are measured; tare is derived.

#### D4-C

Gross, tare and net are independently stored with validation.

#### D4-D

Weight measurements are event-based and the current authoritative
measurement is selected according to the weighing process.

### Required Business Decisions

The business must also define:

- authoritative measurement source;
- UOM;
- precision;
- rounding;
- re-weigh;
- correction;
- tolerance;
- manual entry authority;
- laboratory verification;
- inventory quantity basis.

### Technical Recommendation

D4-A is preferred where the physical process provides reliable gross and
tare measurements.

Do not implement a numeric precision or tolerance until business-approved.

### BUSINESS DECISION

Selected Option:

`____________________________`

Authoritative Measurement:

`____________________________`

UOM:

`____________________________`

Precision:

`____________________________`

Rounding:

`____________________________`

Tolerance:

`____________________________`

Re-weigh Rule:

`____________________________`

Correction Authority:

`____________________________`

Approver:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 8. D5 — Split / Merge / Repack

## Business Decision

Split, merge and repack are:

`OUT OF INITIAL PHASE 3 SCOPE`

unless explicitly approved otherwise.

### Initial Implementation

No:

- Package Split;
- Package Merge;
- Package Repack;

will be implemented in Phase 3 unless separately approved.

### Future Requirements

Any future implementation must define:

- genealogy;
- quality inheritance;
- inventory reversal;
- barcode lifecycle;
- mass balance;
- audit;
- authorization;
- rework semantics.

### Acknowledgement

Packaging Owner:

`____________________________`

Laboratory / Quality Owner:

`____________________________`

Operations Owner:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 9. D6 — Bulk vs Packaged Product Identity

## Business Question

How does packaged product relate to the current MaterialItem model?

### Options

#### D6-A — Same MaterialItem

Bulk and packaged product use the same MaterialItem identity.

Package is a physical/operational representation.

#### D6-B — Separate MaterialItems

Bulk product and packaged product are distinct MaterialItems.

#### D6-C — Parent / Child Product Model

A packaged product references a parent bulk product through an explicit
product relationship.

#### D6-D — Transformation / State Model

Packaging is treated as a product transformation/state transition.

### Current Technical Evidence

The current PEMS implementation does not provide:

- packaged-vs-bulk dimension in stock;
- MaterialItem parent relationship;
- existing BOM model sufficient for this purpose.

### Technical Recommendation

Do NOT implement D6 by assumption.

The business must explicitly determine whether packaged product is:

- the same inventory identity;
- a separate inventory identity;
- a transformed product identity.

### BUSINESS DECISION

Selected Option:

`____________________________`

Bulk MaterialItem:

`____________________________`

Packaged MaterialItem:

`____________________________`

Inventory Relationship:

`____________________________`

Approver:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 10. D7 — Quality Release Authority

## Business Question

Who has authority to:

- Release;
- Reject;
- Hold;
- Release Hold;

Packaging output?

### Candidate Models

#### D7-A — Laboratory / QA Authority

Laboratory/Quality independently controls release.

#### D7-B — Existing QAManager Represents Laboratory Authority

Reuse QAManager as the application representation of Laboratory authority.

#### D7-C — Dedicated Packaging Quality Role

Create a dedicated role.

#### D7-D — Hybrid

Existing QAManager remains authority with dedicated Packaging permissions
where necessary.

### Current Finding

QAManager currently:

- can View;
- can Approve;
- can Release/Reject according to existing Production behavior;
- does NOT currently have Hold / ReleaseHold.

No permission shall be changed until D7 is approved.

### Technical Recommendation

Use an independent Quality authority and preserve separation between:

Production / Packaging execution

and

Quality release.

The exact PEMS role must be approved through E-5.

### BUSINESS DECISION

Quality Authority:

`____________________________`

Hold Authority:

`____________________________`

Release-Hold Authority:

`____________________________`

Approver:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 11. E-4 — Packaging Permission Model

## Options

### E4-A — Reuse Production Permissions

Packaging operates under Production permissions.

### E4-B — Dedicated Packaging Permission Module

Packaging receives its own permission namespace.

### E4-C — Hybrid

Shared permissions where responsibilities overlap, dedicated Packaging
permissions where least privilege requires separation.

### Technical Recommendation

E4-C is preferred if Packaging has materially different authority from
Production.

Do not create permissions until the business role model is approved.

### BUSINESS DECISION

Selected Option:

`____________________________`

Approver:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 12. E-5 — Organizational Role Mapping

The following organizational responsibilities must be mapped to the actual
20 PEMS roles.

| Business Role | Proposed PEMS Role | New Role? | Permission Gap | SoD Gap | Status |
|---|---|---:|---|---|---|
| Complex Manager | | | | | OPEN |
| Maintenance Manager | MNT.Manager | NO | | | OPEN |
| Finance Manager | | | | | OPEN |
| Operations Manager | | | | | OPEN |
| Commissioning Manager | | | | | OPEN |
| Operations Shift Supervisor | | | | | OPEN |
| Operator | | | | | OPEN |
| Engineering Manager | EngineeringManager | NO | | | OPEN |
| Process Engineering Lead | | | | | OPEN |
| Packaging Manager | | | | | OPEN |
| Laboratory Manager | QAManager candidate | TBD | | | OPEN |
| HSE Manager | | | | | OPEN |
| Security Manager | | | | | OPEN |
| Warehouse Manager | | | | | OPEN |

### Mandatory Rule

No role may be created or permission changed until E-5 is approved.

### BUSINESS DECISION

Role Mapping Approved By:

`____________________________`

Approval Date:

`____________________________`

Evidence:

`____________________________`

Status:

`OPEN`

---

# 13. D12 — Financial Treatment

D12 is a Phase 3C gate where financial treatment affects:

- packaging material consumption;
- stock posting;
- product transformation;
- cost;
- variance;
- accounting treatment.

This must not be invented by engineering.

### Business Decision

Financial treatment:

`____________________________`

Packaging Material Consumption:

`____________________________`

Product Transformation:

`____________________________`

Cost Treatment:

`____________________________`

Variance Treatment:

`____________________________`

Approver:

`____________________________`

Date:

`____________________________`

Status:

`OPEN`

---

# 14. Mandatory Phase 3 Gates

Phase 3A may start only when:

- D1 = APPROVED
- D2 = APPROVED
- D3 = APPROVED
- D4 = APPROVED
- D5 = ACKNOWLEDGED
- D6 = APPROVED
- D7 = APPROVED
- E-4 = APPROVED
- E-5 = APPROVED

and:

- Architecture Freeze = ACTIVE
- Migration Strategy = VALIDATED
- Inventory Boundary = VALIDATED
- Barcode Strategy = VALIDATED
- Security / SoD = VALIDATED

---

# 15. Phase 3C Additional Gate

Before inventory posting is implemented:

- D12 must be APPROVED;
- Quality release authority must be approved;
- Inventory boundary must be validated;
- mass-balance behavior must be defined;
- packaging material treatment must be defined.

---

# 16. Quality Governance Principle

Unless explicitly changed by an approved business decision:

Packaging execution does NOT equal Quality Release authority.

The quality lifecycle must preserve the established safety principle:

Produced
→ AwaitingQC
→ Released
OR
→ Blocked
OR
→ Rejected

Inventory posting must not bypass the authoritative Released state.

---

# 17. Inventory Governance Principle

Inventory source of truth:

InventoryTransaction

Inventory projection:

MaterialStock

Inventory mutation:

IMaterialStockService

No parallel Packaging inventory engine may be created.

---

# 18. Barcode Governance Principle

Barcode identity:

BarcodeIdentity

No parallel Packaging barcode identity architecture may be created.

---

# 19. Workflow Governance Principle

Use existing PEMS workflow architecture where applicable.

Do not create a parallel Packaging workflow engine.

Where explicit domain lifecycle states are necessary, they must remain
consistent with the existing PEMS workflow/authorization architecture.

---

# 20. Audit Governance

Every significant Packaging mutation must be traceable through the existing
audit architecture.

At minimum:

- actor;
- timestamp;
- entity;
- entity ID;
- previous state;
- new state;
- action;
- reason where required;
- correlation/reference.

---

# 21. KPI / PI / RI / KRI Baseline

The approved metric catalog remains:

15 KPI
16 PI
17 RI
8 KRI

Total:

56 metrics.

No target may be invented.

Where a target is not approved:

TARGET = NOT DEFINED

---

# 22. Business Approval Record

The following statement is mandatory:

> I confirm that the decisions recorded in this document represent the
> approved business requirements for the PEMS Packaging implementation.

Business Owner:

`____________________________`

Executive Approver:

`____________________________`

Date:

`____________________________`

Approval Reference:

`____________________________`

Signature / Electronic Approval:

`____________________________`

---

# 23. Change Control

Any change to D1-D7, E-4, E-5 or D12 after implementation begins requires:

1. Change request.
2. Impact assessment.
3. Business approval.
4. Technical impact analysis.
5. Migration analysis where required.
6. Re-validation of affected tests.
7. Update of this baseline.

No silent business-rule changes are permitted.

---

# END OF FINAL BUSINESS BASELINE
