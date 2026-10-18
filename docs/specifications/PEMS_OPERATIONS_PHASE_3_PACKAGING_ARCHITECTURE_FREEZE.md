# PEMS Operations Phase 3 — Packaging Architecture Freeze Package

## STATUS: PREPARED — **NOT ACTIVE**

A freeze may only be declared when D1, D2, D3, D4 and D6 are resolved by their named owners and D5
is either resolved or formally excluded from scope (`PEMS_OPERATIONS_PHASE_2F_FINAL_BUSINESS_DECISION_BASELINE.md`
§1). **None of them is resolved.** This document therefore does **not** freeze anything. It is the
implementation-ready technical package: every item states the value under the technical
recommendation, the decision it depends on, and what changes if the business answers differently.
Declaring the freeze is a single act — see §12 (Activation).

No code, entity, migration, controller, view, permission or test is created by this document.

---

## 1. Verified Ground Truth (frozen regardless of Packaging decisions)

These are existing PEMS facts, verified against the repository this phase. Packaging must preserve
them unchanged.

```
MaterialItem = authoritative material/product master
ProductionBatch = authoritative production batch (QualityStatus: Produced→AwaitingQC→Released|Blocked|Rejected; Blocked→AwaitingQC)
InventoryTransaction = append-only ledger (nullable ProductionBatchId; ReferenceType/ReferenceId; Quantity decimal(18,3))
MaterialStock = projection, grain (MaterialItemId, WarehouseId, StorageLocationId), unchanged
BarcodeIdentity = identity layer; SupportedTargetTypes is a registration set (Equipment, MaterialItem, Warehouse, StorageLocation, ProductionBatch)
Phase 2C gate = ProductionRunService.PostOutputToInventoryAsync refuses posting unless batch QualityStatus == "Released"
Existing fact-table pattern = plain classes (no BaseEntity) ProductionInput/Output/Loss/Event, append-only, int PK, decimal(18,3)
State machines = explicit Dictionary<string,string[]> in services, not IWorkflowRuntime
Numbering = INumberingService defaults dictionary (e.g. "ProductionRun" → prefix PRN)
Audit = automatic AuditLog via PEMSDbContext.SaveChangesAsync for BaseEntity-derived entities
```

## 2. Decision-Dependency Map

| Frozen item | Depends on | Value under technical recommendation | If the answer differs |
|---|---|---|---|
| Package definition | D1 | Filled-container record: one `MaterialItemId`, quantity, UOM, source batch link(s), identity | Core shape of `Package` changes (no quantity / no container identity / hierarchy) |
| Material identity | D2, D6 | One `MaterialItem` per package; `PackagingOutput.MaterialItemId` independent of input | Mixed: `MaterialStock` grain and per-material mass balance break — redesign |
| Batch genealogy | D3 | Contribution-capable (one input row per source batch) | Single-batch: add a validation rule only; multi-batch approved: add per-batch contribution linkage |
| Quantity model | D4, D12 | Gross + Tare stored, Net derived, `decimal(18,3)`, corrections append-only | Model B adds a frozen Net column + consistency rule |
| Quality model | D1, D7 | Package has its own `QualityStatus` initialised from the source batch at creation, then independent (Phase 2D Model D) | Different model changes whether Package has a status at all |
| Inventory boundary | D1, D6, E-1 | Package output posts via existing `IMaterialStockService` only after `Released`; source bulk deducted if it was posted | Distinct packaged SKU: posting uses that `MaterialItem`; same SKU: state/metadata only |
| Barcode | D1 | Register `Package` in `SupportedTargetTypes`; new identity per package; reprint/replace reuse existing methods | No container identity ⇒ no Package barcode |
| Permissions / SoD | D7, E-4, E-5 | Reuse `"Production"` module and existing `Create↔Approve` pairs; no new SystemRoles | New module ⇒ new SoDPolicy rows; new roles ⇒ organizational approval |
| Workflow | none | No `IWorkflowRuntime`; explicit transition dictionaries | Only if a multi-role approval chain is confirmed |
| Split/merge | D5 | **Excluded from Phase 3** | If required: `PackageGenealogy` join table + "Superseded" state (additive later) |
| Handling unit/pallet, repacking | D9, D10 | Excluded | Additive later |

## 3. Candidate Entity Set (justified, not "all of them")

Each entity is included only if it has a verified reason and an existing pattern to mirror.

| Entity | Mirrors | Include in first release? | Justification / dependency |
|---|---|---|---|
| `PackagingRun` | `ProductionRun` | Yes — after D1/D4/D6 | Process record; parent optional order omitted (D8) |
| `PackagingInput` | `ProductionInput` | Yes | `SourceProductionBatchId` **required** (packaging always consumes a known batch) — Phase 2D §13 |
| `Package` | new physical identity | Yes | Depends on D1, D2, D4 |
| `PackagingOutput` | `ProductionOutput` | Yes | Links run to `Package`; optional `InventoryTransactionId` |
| `PackagingLoss` | `ProductionLoss` | Yes | Loss-category vocabulary is **not approved** (Phase 2D proposed `DamagedPackage/LabelDefect/UnderfillOverfill`) — vocabulary needs Packaging/Process owner approval before seeding |
| `PackagingEvent` | `ProductionEvent` | Yes | Separate table (Phase 2D §16) to avoid altering a live table |
| `PackagingOrder` | `ProductionOrder` | **No** (D8 default) | Additive later |
| `HandlingUnit`, `PackageGenealogy` | — | **No** (D9, D5) | Deferred |
| `QualityTestResult` | — | **No** | Laboratory test capture is a separate, unapproved capability; blocks OOS/retest metrics (see §10) |

Package attribute set (only fields the approved spec will need; no speculative fields): package
number (numbering service), `MaterialItemId`, quantity/UOM per D4, source-batch linkage per D3,
`QualityStatus` (+ reason, changed date, changed-by, mirroring `ProductionBatch`), lifecycle status,
warehouse/location only if inventory posting occurs, `IsActive`, `BaseEntity` audit columns.

## 4. Services (candidate)

`IPackagingRunService` (create, start, record input/output/loss, complete, post-to-inventory with its
own Released gate), `IPackageService` (quality transitions Released/Blocked/Rejected/Unblock with
transition dictionary, lookup). No new inventory service, no new ledger. The Released check is
**duplicated deliberately** at the package posting call site (Phase 2D §28), and must live in the
service, not only the controller (see baseline §8.1).

## 5. Quality Frozen Rules (independent of D-answers)

- Transitions: `Created→PendingQC→{Released|Blocked|Rejected}`, `Blocked→PendingQC`.
- Only `Released` packages may be posted. Blocked/Rejected/PendingQC never post.
- Release/Reject require the existing Approve-class authority; Block/Unblock require Hold/ReleaseHold-class authority; creator/editor cannot release (existing SoD pattern).
- Phase 2C batch gate untouched; package gate must not weaken it.

## 6. Inventory Frozen Rules

- No second ledger; `InventoryTransaction` is the only movement record (`ReferenceType="PackagingOutput"`).
- Idempotency: posting is refused when `InventoryTransactionId` is already set (existing pattern).
- Double-count invariant: if the source batch was already posted, the packaging input must post a matching deduction; if it was never posted, no deduction is needed.
- A failed posting must mutate nothing.

## 7. Mass Balance

`Input = Output(Net) + Loss + ApprovedVariance`. No tolerance is coded (D11). The raw difference is
always displayed, never forced to zero. Whether an imbalance blocks completion or only reports is a
**business rule not yet approved** — default technical behaviour: report, do not block.

## 8. Barcode

Additive registration only: add `"Package"` to `SupportedTargetTypes`, the three label/target switch
methods and `BarcodeController`'s routing switch. Existing five target types must keep passing their
current tests.

## 9. Permissions and SoD

Reuse `"Production"` (`Create`, `Edit`, `Delete`, `Approve`, `Hold`, `ReleaseHold`). No new role. Any
SoD addition is a new `SoDPolicy` **row**, not a new mechanism. The Phase 2E RACI role names do not
exist as PEMS roles (baseline §6 / E-5) — mapping is an organizational decision.

## 10. KPI Readiness (Phase 2E catalog — 56 metrics, corrected count)

Source of the classification: `PEMS_OPERATIONS_PHASE_2E_KPI_PI_RI_KRI_CATALOG.md` §13 (reproducible
from each metric's Data-source line). Note: this document's first draft used an unsupported
estimate (15/14/19 of 48); it was replaced after the catalog itself was recounted.

| Class | Count | Phase 3 meaning |
|---|---|---|
| E — source entities exist today | 19 | Not built by Phase 3; reporting on them is a separate, unscheduled task. Some still need a Finance cost rate, count capture or a rule definition |
| PB — batch-level today, package-level after Phase 3 | 8 | Package-level part becomes computable after 3A–3C |
| P — needs Packaging entities | 13 | Computable only after 3A–3C; two also need E-1 |
| G — no source entity exists, none planned | 16 | **Not computable — data source gap.** Requires HSE logging, laboratory result, deviation log, process-parameter capture, rework or shipment capabilities that are not in Phase 3 scope |

Phase 3E may therefore only deliver the 13 P-class metrics and the package-level part of the 8
PB-class metrics, and only after 3A–3C exist. No target is invented; all remain
`TARGET — BUSINESS APPROVAL REQUIRED`.

## 11. Exclusions (frozen when active)

Split/merge, repacking, handling unit/pallet, packaging order tier, rework re-entry, laboratory test
result entity, shipment/dispatch, offline/mobile capture, Package import/export (Import Center
baseline: no page-level import buttons; not needed until a business requirement exists), new
workflow engine, new RBAC, new SystemRoles.

## 12. Implementation-Ready Work Plan (executed only after activation)

| Phase | Content | Depends on |
|---|---|---|
| **3A-0 Policy foundation** (M-2, M-3, M-5; design: `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`) | Additive migration for `PolicyDefinition`, `PolicyVersion`, `PackagingPolicyValues` only; registry-backed value type with text codes; lifecycle service; resolver (Deployment > FunctionalLocation > ProductionWorkCenter, rules R1-R10); validator orchestration; `PackagingPolicy` permission module registered with **no role granted** (E-5 is an administrator's act); approval workflow **not seeded** (stages are administrator data using `Permission:` participants); tests incl. auto-skip and author != approver. No policy row seeded; no other domain migrated | activation gate (all 15, incl. the design record that settles TI-1…TI-4) |
| 3A Foundation | Entities `PackagingRun/Input/Output/Loss/Event`, `Package`; `PackagingPolicyVersionId` on policy-dependent records; EF configurations (`decimal(18,3)`, `Restrict` FKs); numbering default; one additive migration; entity/validation tests | 3A-0, D1, D2, D3, D4 |
| 3B Execution | `PackagingRunService` state machine (Planned→InProgress⇄Paused→Completed, OnHold, Cancelled), record input/output/loss, mass-balance calculation DTO, controller + views (existing `_PageHeader`, EN/FA resx, RTL), Help articles | 3A |
| 3C Integration | Package quality transitions + gate, opt-in inventory posting, barcode target registration | D6, D7, E-1 |
| 3D Control | Permissions (reuse), SoD rows if any, audit verification tests, reporting (operations, quality, inventory, genealogy) | E-4, E-5 |
| 3E KPI readiness | Data-source verification per metric; only metrics with a real source; no fabricated targets | 3A–3D |

Migration rule: inspect current model snapshot; additive only; generate and read the `Up()` method;
do not apply unless the repository workflow authorizes it.

**Test matrix (must exist before any phase exits):** valid/invalid package; quantity validation;
state transitions; Released posting succeeds; AwaitingQC/Blocked/Rejected posting fails; duplicate
posting prevented; failed posting mutates nothing; package→batch and batch→package traceability;
valid/invalid mass balance; authorized vs unauthorized action; SoD conflict; barcode uniqueness,
replacement, audit; audit rows for creation, quality decisions, posting; regression of the existing
Production quality gate and existing barcode target types.

## 13. Activation Procedure

The freeze becomes ACTIVE only when a document records, with name and date:
1. Owner decisions for D1, D2, D3, D4, D6 (option chosen).
2. D5 decided, or the exclusion acknowledged by the Laboratory and Packaging owners.
3. Formal acknowledgement of the Phase 2E readiness-gate items (quality authority, inventory
   boundary, RACI, SoD, mass balance, KPI ownership, loss/variance treatment, barcode policy).
4. An E-5 decision mapping stakeholder roles to real PEMS roles (or approval to add roles).

After activation, edit §2's "value" column to the chosen option, change the status line, and
begin Phase 3A. No conceptual requirement may be added afterwards without reopening this document.

## 14. Status Log

- 2026-09-25 (safety review): `docs/audits/PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md` found 7 HIGH design findings and defined required changes PR-1..PR-16 (workflow auto-approval guard, null-employee bypass, user-aware authorization, break-glass, grant-administration prerequisite, immutability guard, D12 separation, capability gate, history reference on `PackagingRun` and `Package`, and others). They must be accepted into the 3A design freeze. Status unchanged: PREPARED - NOT ACTIVE; 0 of 16 business approvals; checklist 0 of 15.
- 2026-09-25 (TI-1..TI-4): technical design written (`PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`): infrastructure shape, value representation, approval-role mapping, scope precedence. §15 item 8 and the 3A-0 row updated. Status unchanged: PREPARED - NOT ACTIVE; 0 of 16 business approvals; checklist 0 of 15 (gate 15: technical evidence complete, architecture-owner approval required).
- 2026-09-25 (M-decisions): architecture decisions M-1…M-5 recorded (ACCEPTED, not business approvals); work plan gains step 3A-0. Status unchanged: PREPARED — NOT ACTIVE; 0 of 16 business decisions approved; activation checklist 0 of 15.
- 2026-09-25 (configurability): the Configurability Guide was added as §15 of the freeze and as
  activation gate 15. Status unchanged: PREPARED — NOT ACTIVE. Still 0 of 16 decisions approved.
- 2026-09-25 (Phase 2H): gate re-evaluated against `PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md` — 0 of 16 decisions approved. Status unchanged: PREPARED — NOT ACTIVE. See `PEMS_OPERATIONS_PHASE_2H_ARCHITECTURE_ACTIVATION_RECORD.md`.
- Role facts in this document were re-checked against the live database (20 roles); the "no new SystemRoles" rule in §9 is unaffected.

## 15. Configurability (part of the freeze)

`PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md` is part of this Architecture Freeze
(Baseline → Configurability Guide → Architecture Freeze → Phase 3A–3I). When the freeze is
activated it is activated together with the guide; the following bind every Phase 3 sub-phase:

1. The "value" column of §2 is the **initial Packaging Policy (v1)** of the approving organization
   and scope, not a product constant. No code branches on company, site or packaging type.
2. Domain services consume a resolved, version-stamped policy; policy is never read ad hoc.
3. Packaging records that depend on policy carry the policy version they were created under.
4. Non-configurable invariants (guide A2), including the §5 quality rules and §6 inventory rules of
   this document, cannot be switched off by any policy value.
5. A policy naming a capability that is not Implemented + Tested is rejected at activation
   (`BLOCKED`), never silently accepted. A mode-support matrix (guide M-4) lists which modes are
   in the first release.
6. No configured policy means "Packaging unavailable — configuration required", never developer
   defaults.
7. Authorization uses `HasPermissionAsync(roles, module, action)`; no role-name literal is
   introduced. E-5 role mapping is data, not code.
8. Architecture decisions M-1…M-5 are **closed** in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` (2026-09-25): separate deployment
   per company (no tenant model); reusable policy infrastructure with domain-owned schema; typed values with
   stable text codes; capability-readiness scope (only Implemented+Tested+Authorized+Production-Ready may be
   Active); governed approval via `IWorkflowRuntime` (risk-based tiers; Tier 2 empty; `AuditLog` always).
   Technical items TI-1..TI-4 are **settled (design)** in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` (2026-09-25), the evidence for checklist gate 15 (*technical evidence complete - architecture owner approval required*); implementation still cannot start before all 15 gates.
9. **Safety review (2026-09-25):** the 3A-0 scope includes PR-1..PR-4, PR-7..PR-9, PR-11..PR-16; PR-5 (grant administration) is a prerequisite to executing E-5; PR-6 belongs to 3D. D12 may become a separate `PackagingFinancial` policy type (PR-10, decision pending). `PackagingPolicyVersionId` is carried by `PackagingRun` **and** `Package`.
