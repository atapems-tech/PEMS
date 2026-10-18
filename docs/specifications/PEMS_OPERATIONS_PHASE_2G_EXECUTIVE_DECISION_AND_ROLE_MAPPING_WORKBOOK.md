# PEMS Operations Phase 2G — Executive Decision and Role Mapping Workbook

**Purpose:** put every open Packaging decision in front of its owner in a form that can be answered
in one sitting, and map the governance roles to the roles PEMS actually has.
**Nature:** documentation. Nothing here is built, seeded, or approved.
**Every decision in this workbook is `OPEN`.** No approval exists in the repository (evidence in
§1). Technical recommendations are shown separately and are never approvals.

States used: `OPEN` · `OWNER REVIEW` · `APPROVED` · `REJECTED` · `DEFERRED` · `BLOCKED`.

---

## 1. Evidence Baseline (verified this phase)

| Check | Result |
|---|---|
| Sign-off table in the Phase 2E Executive Decision Pack | Blank — no name, no date |
| Any `APPROVED` Packaging decision in `docs/` | None (search matched only unrelated EDMS/workflow documents and a sentence in the 2F baseline saying "none exist") |
| Any Packaging class in `PEMS.Domain/Application/Persistence/Web` | None |
| Source files changed since the last full test run | None |
| Migration head | `20260923141435_AddContractApprovalRulePriority` — no Packaging migration |
| Existing quality gate (Phase 2C) | Present: `ProductionRunService.PostOutputToInventoryAsync` refuses a non-`Released` batch |

## 2. Numbering Change Control (read this first)

The master prompt lists the decisions with different numbering than Phase 2D/2E/2F used. Nothing is
overwritten; this table is the crosswalk. **IDs in this workbook follow the master prompt.**

| 2G ID | Topic | Earlier ID(s) | Note |
|---|---|---|---|
| D1 | Package definition | 2D-D1 | same |
| D2 | Mixed-product package | 2D-D2 | same |
| D3 | Multi-batch package | 2D-D3 | same |
| D4 | Gross / Tare / Net | 2D-D4 (+ rounding 2D-D12) | rounding is now part of the quantity model |
| D5 | Split / Merge / Repack | 2D-D5 + 2D-D10 | repacking merged in |
| D6 | Bulk vs packaged identity | 2D-D6 | same |
| D7 | Quality release authority | 2D-D7 | same |
| D8 | Packaging order / planning tier | 2D-D8 | same |
| D9 | Rework | 2D §32 (deferred, unnumbered) | newly numbered |
| D10 | Offline / mobile | 2D §34 (deferred, unnumbered) | newly numbered |
| D11 | Loss / variance tolerance | 2D-D11 | same |
| D12 | Financial / accounting treatment | 2E E-1 (overlaps) | E-1 is kept as an alias of D12 |
| — | Handling unit / pallet | 2D-D9 | **not in the master prompt's list**; carried as `2D-D9`, out of scope |
| E-1…E-5 | as before | 2E/2F | E-1 ≡ D12 |

## 3. Decision Owners

Owners and approvers below are the organizational roles from Phase 2E. **They are not PEMS
roles** (see §12) — a person has to be named by the business.

---

## 4. D1 — Package Definition

| Field | Content |
|---|---|
| **Decision** | What exactly is a "Package" in PEMS? |
| **Current PEMS behavior** | No Package concept exists. Production ends at `ProductionBatch`/`ProductionOutput` (a quantity of one material from one production event). Nothing tracks a physical container. |
| **Current data model** | `ProductionBatch` (quantity + `QualityStatus`), `ProductionOutput`, `InventoryTransaction` (nullable `ProductionBatchId`), `MaterialStock` (per material/warehouse/location), `BarcodeIdentity` (targets: Equipment, MaterialItem, Warehouse, StorageLocation, ProductionBatch). |
| **Options** | **A. Filled container** — one record = a physical unit holding a quantity of one material from identified batch(es). *For:* mirrors how a batch is both "quantity" and "event"; traceable; barcode-able. *Against:* couples container and content. **B. Quantity only** — "package" is a labelled quantity, no container identity. *For:* simplest. *Against:* no per-unit identity, no per-unit quality/barcode. **C. Container only** — content tracked elsewhere. *For:* container lifecycle. *Against:* two records to answer "what is in unit 4471". **D. Logistics/handling unit** — a package is a pallet/carton grouping. *For:* shipping. *Against:* changes the meaning of every packaging metric. |
| **Downstream impact** | Defines the central entity's shape. Every other decision assumes an answer. |
| **Migration impact** | New tables only under all options; shape of the central table differs. Under D a hierarchy is needed. |
| **Reporting impact** | A/C/D: countable units. B: quantities only, no "package count". |
| **Quality impact** | A/C/D: status per unit. B: status stays on the batch (no independent package quality). |
| **Inventory impact** | A/B: stock unit = material quantity. D: stock unit may be the handling unit. |
| **Barcode impact** | A/C/D: `TargetType="Package"` registration. B: none. |
| **Workflow impact** | None differing; lifecycle states differ. |
| **Audit impact** | Automatic (`BaseEntity`) under all options. |
| **KPI impact** | 13 Packaging-dependent metrics and 8 partly-dependent metrics assume A. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A. |
| **Business owner** | Packaging Manager |
| **Required approver** | Complex Manager, Laboratory Manager |
| **Approval status** | **OPEN** |

## 5. D2 — Mixed-Product Package

| Field | Content |
|---|---|
| **Decision** | May one package contain more than one product? |
| **Current PEMS behavior** | `MaterialStock` grain is one material per row; `ProductionBatch` is one material. No multi-material container exists. |
| **Current data model** | `ProductionBatch.MaterialItemId` single; `InventoryTransaction.MaterialItemId` single. |
| **Options** | **A. One package = one `MaterialItem`.** *For:* keeps per-material mass balance, quality and stock well-defined. *Against:* an assortment box must be modelled as its own item ("kit"). **B. Mixed allowed.** *For:* matches physical assortments. *Against:* per-material balance, quality and stock become undefined for the unit; `MaterialStock` grain is violated. |
| **Downstream impact** | B forces a package-content table and per-line quality/stock rules. |
| **Migration impact** | A: none extra. B: extra content table. |
| **Reporting impact** | B: package totals mix units. |
| **Quality impact** | B: which material's spec governs release? |
| **Inventory impact** | B: one package posts several ledger rows. |
| **Barcode impact** | Unchanged. |
| **Workflow impact** | Unchanged. |
| **Audit impact** | Unchanged. |
| **KPI impact** | Yield/loss/mass-balance are per-material under A; ill-defined under B. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A. |
| **Business owner** | Packaging Manager |
| **Required approver** | Engineering Manager, Complex Manager |
| **Approval status** | **OPEN** |

## 6. D3 — Multi-Batch Package

| Field | Content |
|---|---|
| **Decision** | May one package contain material from more than one production batch? |
| **Current PEMS behavior** | `ProductionInput` has an optional `SourceProductionBatchId` (one row per source). Batches are never merged. |
| **Current data model** | Existing pattern: one input row per source batch. |
| **Options** | **A. Single batch only** — enforced at execution. *For:* simplest genealogy and recall. *Against:* top-up from a second batch is impossible. **B. Multiple batches** — one contribution row per source batch. *For:* real blending supported; recall finds every package. *Against:* release rule becomes "all contributing batches Released" (a stricter rule that itself needs approval). |
| **Downstream impact** | Determines whether package→batch is a single FK or a contribution set. |
| **Migration impact** | B: contribution linkage table. A: one FK. |
| **Reporting impact** | B: contribution-level traceability both ways. |
| **Quality impact** | B: package quality vs several batch statuses. |
| **Inventory impact** | B: deduct each contributing batch. |
| **Barcode impact** | None. |
| **Workflow impact** | None. |
| **Audit impact** | Contribution rows are auditable. |
| **KPI impact** | `KRI-INV-02` (traceability completeness) needs an unambiguous chain. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** design for B (one input row per source batch, the existing pattern); enforce A as a rule if the business says blending never happens. |
| **Business owner** | Process Engineering Lead |
| **Required approver** | Packaging Manager, Laboratory Manager |
| **Approval status** | **OPEN** |

## 7. D4 — Gross / Tare / Net

| Field | Content |
|---|---|
| **Decision** | Which weights are stored, which derived, and how is the printed net weight protected? |
| **Current PEMS behavior** | Quantities are single values, `decimal(18,3)`. No Gross/Tare/Net anywhere. `MaterialUOM.ConversionFactor` exists but is unused. |
| **Current data model** | `ProductionOutput.Quantity`, `InventoryTransaction.Quantity`, all `decimal(18,3)` (verified in `ProductionConfigurations.cs`, `InventoryTransactionConfiguration.cs`). |
| **Options** | **A. Store Gross + Tare, derive Net.** *For:* Net can never disagree with its sources; standard weighbridge practice. *Against:* a corrected Tare changes Net everywhere, including past labels. **B. Store Gross + Tare + frozen Net.** *For:* a printed/certified Net never silently changes. *Against:* a consistency rule must be enforced. **C. Net only.** *For:* simplest. *Against:* cannot be audited or reconciled; disqualified for mass balance. |
| **Also to decide** | Tare source (container default / measured each time / default with override); device and operator capture; UOM conversion; rounding rule. |
| **Already fixed by PEMS convention (technical, not business)** | Precision `decimal(18,3)`; corrections are new append-only rows, never overwrites. |
| **Downstream impact** | Column set of `Package` and `PackagingOutput`; the mass-balance formula. |
| **Migration impact** | B adds a column; A does not. |
| **Reporting impact** | Cost per kg, average package weight, yield. |
| **Quality impact** | Certificates of analysis may quote Net. |
| **Inventory impact** | The quantity posted to the ledger. |
| **Barcode impact** | Label content. |
| **Workflow impact** | None. |
| **Audit impact** | Re-weigh must be a new fact. |
| **KPI impact** | `PI-PKG-02`, `KPI-FIN-01/02`, `RI-PKG-02`. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A, unless a certified frozen Net is required (then B). |
| **Business owner** | Finance Manager |
| **Required approver** | Laboratory Manager, Complex Manager |
| **Approval status** | **OPEN** — `PHASE 3 QUANTITY MODEL BLOCKER` |

## 8. D5 — Split / Merge / Repack

| Field | Content |
|---|---|
| **Decision** | Are splitting, merging, and repacking of existing packages permitted, and what are the quality and inventory consequences? |
| **Current PEMS behavior** | None. Existing barcode `ReplaceAsync` means "same thing, new label". |
| **Current data model** | `BarcodeIdentity` supersession chain only. |
| **Options** | **A. Not supported in the first release.** *For:* no undefined semantics. *Against:* operations must work around it. **B. Supported with re-confirmation of quality after every split/merge.** *For:* handling is a contamination/damage risk point. *Against:* slower. **C. Supported with inherited release.** *For:* fast. *Against:* unsafe if handling can introduce damage. |
| **Downstream impact** | B/C need a genealogy join table, a "superseded" state, and a new ledger transaction vocabulary (the existing types Receipt/Issue/Return/Transfer/Adjustment do not fit a same-material transformation). |
| **Migration impact** | A: none. B/C: additive later. |
| **Reporting / Quality / Inventory / Barcode / Workflow / Audit / KPI** | All grow under B/C; unchanged under A. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A for the first release; if ever built, B. |
| **Implementation scope (technical decision, not a business decision)** | Excluded from implementation until decided. |
| **Business owner** | Laboratory Manager |
| **Required approver** | Packaging Manager, Finance Manager |
| **Approval status** | **OPEN** (the business question is unanswered even though implementation is excluded) |

## 9. D6 — Bulk Product vs Packaged Product Identity

| Field | Content |
|---|---|
| **Decision** | Is the packaged product the same `MaterialItem` as the bulk, or a different one? |
| **Current PEMS behavior** | `MaterialItem` is the only product master. No transformation concept exists. |
| **Current data model** | `MaterialItem`; `MaterialStock` keyed by material/warehouse/location. |
| **Options** | **A. Same item** — packaging is state/metadata. *For:* one master, simple. *Against:* cannot stock or price "25 kg bag" separately. **B. Distinct packaged item** — output creates stock under its own code. *For:* matches an ERP/price-list SKU. *Against:* requires the business to create those items and a bulk→packaged relation for reporting. **C. Both, per product line.** *For:* flexible. *Against:* two behaviours to test. |
| **Key verified point** | Table shape need not depend on the answer if the package output stores its own `MaterialItemId`; the answer changes posting semantics, master-data setup, costing and reports. **No second Product Master is created under any option** — new packaged SKUs live in the existing `MaterialItem` master, created by the business. |
| **Downstream impact** | Inventory posting target and stock reports. |
| **Migration impact** | None if output item is stored independently. |
| **Reporting / Quality / Inventory / Barcode** | Inventory and reporting differ most. |
| **Workflow / Audit** | Unchanged. |
| **KPI impact** | Cost per unit/kg, inventory value. |
| **Technical recommendation** | **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** store output `MaterialItemId` independently so either answer works; the choice itself is the business's. |
| **Business owner** | Finance Manager |
| **Required approver** | Packaging Manager, Complex Manager |
| **Approval status** | **OPEN** |

## 10. D7–D12 (shorter form)

| ID | Decision | Current PEMS behavior | Options | Technical recommendation (NOT BUSINESS APPROVAL) | Owner / Approver | Status |
|---|---|---|---|---|---|---|
| D7 | Who releases a package? | Batch release = `Production.Approve` (held by `QAManager`, `EngineeringManager`) + creator/editor SoD; block = `Hold`; unblock = `ReleaseHold`; posting gate in service | (a) reuse `Production.Approve`; (b) separate packaging QC role | (a) — no new permission | Laboratory Manager / Engineering Manager | **OPEN** |
| D8 | Formal Packaging Order tier? | Production has Order + Run; numbering defaults exist for both | (a) none; (b) Order + Run | (a); a nullable parent can be added later | Operations Manager / Packaging Manager, Complex Manager | **OPEN** |
| D9 | Rework | Production loss category `Rework` exists, no re-entry mechanism | (a) not supported; (b) re-entry linked to the loss | (a) | Packaging Manager / Laboratory Manager, Finance Manager | **OPEN** |
| D10 | Offline / mobile capture | PWA confirmed network-only | (a) online only; (b) sync-queue | (a) | Operations Manager / Complex Manager | **OPEN** |
| D11 | Loss / variance tolerance | None; mass-balance reports show raw difference | (a) no tolerance; (b) defined per product | (a) until defined; never invent a number | Process Engineering Lead / Finance Manager, Operations Manager | **OPEN** |
| D12 (≡ E-1) | Financial / accounting treatment of packaging events | No packaging cost rule exists | Finance defines per event | none — not a technical question | Finance Manager / Complex Manager | **OPEN** |

## 11. E-2 to E-5

| ID | Decision | Status |
|---|---|---|
| E-2 | Commissioning vs production boundary marker on packaging records | **OPEN** — Commissioning Manager / Packaging Manager |
| E-3 | HSE stop-work authority and legal/site requirements | **OPEN** — `SITE / LEGAL CONFIRMATION REQUIRED` — HSE Manager |
| E-4 | Distinct `"Packaging"` permission module vs reuse of `"Production"` | **OPEN** — Complex Manager / Laboratory Manager / Engineering Manager. Technical note: a new module means a registry entry, a role seed block and new `SoDPolicy` rows (the mechanism is generic); reuse means zero new surface but shares grants with Production |
| E-5 | Role mapping | **OPEN** — analysis complete in §12; approval to create/change roles required |

---

## 12. E-5 — Role Mapping (from the real RBAC implementation)

### 12.1 What PEMS actually has (verified in code)

- **20 roles in the live database** (`AspNetRoles`, queried read-only this phase). Twelve are the
  code constants in `PEMS.Persistence/Identity/SystemRoles.cs`: SystemAdmin, EngineeringManager,
  EngineeringSupervisor, DisciplineEngineer, DocumentController, MaintenanceEngineer,
  ContractManager, QAManager, Viewer, Legal, Finance, HSE. Eight more are CMMS work-order governance
  roles seeded by `WorkOrderGovernanceSeeder`: `MNT.Manager`, `MNT.PlanningHead`, `MNT.PlanningExpert`,
  `MNT.DisciplineHead`, `MNT.SeniorExpert`, `MNT.Expert`, `maintenance planning`, and
  **`OPS.IssueAuthority`**. Their only grants are on the `WorkOrder` module (e.g. `MNT.Manager` =
  View + Approve, `OPS.IssueAuthority` = View). None holds any `Production` grant.
  > **CORRECTION.** Phase 2F and the first version of this workbook said PEMS "defines only 12
  > roles". That came from reading the code constants only. The live database has 20. The Packaging
  > conclusions below were re-derived with the 20.
- **Permission actions:** View, Create, Edit, Delete, Approve, ManualNumber, Freeze, Unfreeze, Hold,
  ReleaseHold (per role × module).
- **Production module grants:**

| Role | View | Create | Edit | Delete | Approve | Hold | ReleaseHold |
|---|---|---|---|---|---|---|---|
| MaintenanceEngineer | ✔ | ✔ | ✔ | | | ✔ | ✔ |
| QAManager | ✔ | | | | ✔ | | |
| EngineeringManager | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Viewer | ✔ | | | | | | |
| Finance, HSE, Legal, EngineeringSupervisor, DisciplineEngineer, DocumentController, ContractManager | — no Production row — | | | | | | |

- **Other relevant modules:** `Barcode` (MaintenanceEngineer, EngineeringManager: Create/Delete/Approve),
  `InventoryMasterData` and `MaterialItem` (MaintenanceEngineer, EngineeringSupervisor,
  EngineeringManager: Create/Edit). Finance and HSE hold rows only on `Contracts`/`Tender`.
- **SoD policies (10 pairs):** EngineeringDocuments Create/Edit↔Approve; WorkOrder Create/Edit↔Close;
  Permit Create↔Issue; **Production Create/Edit↔Approve**; Contracts Create/Edit↔Approve;
  ProcurementPackage Create↔Approve.
- **Workflow:** Production uses explicit transition dictionaries in its services, **not**
  `IWorkflowRuntime` (verified: no Production service references it).

### 12.2 Mapping matrix

"New role required" is a **technical assessment of least-privilege fit, not an approval**; creating a
role is an organizational decision (E-5).

| Business role | PEMS role (nearest) | Required permissions (Packaging, if built on `"Production"`) | Existing | Missing | SoD requirement | Gap type | New role required? | Evidence | Approval |
|---|---|---|---|---|---|---|---|---|---|
| Complex Manager | Viewer | View across modules | View | none (approval authority is organizational, not a system permission) | n/a | None | **NO** | `Viewer` view rows | OPEN |
| Maintenance Manager | **MNT.Manager** (live DB; WorkOrder View+Approve) and MaintenanceEngineer | Work request/order flow; Production Hold/ReleaseHold for equipment holds | WorkOrder approve (`MNT.Manager`); Production C/E/Hold/ReleaseHold (`MaintenanceEngineer`) | none for the packaging maintenance flow (uses WorkOrder) | keep WorkOrder Create↔Close | None | **NO** | `AspNetRoles`, `RolePermissions` | OPEN |
| Finance Manager | Finance (1 user) | View Production/inventory reports; cost approval later | Contracts/Tender edit only | **No Production or inventory View** | Finance must not hold Packaging Create/Approve | Permission gap | **NO** (grant View to existing role) | Finance rows | OPEN |
| Operations Manager | none (`OPS.IssueAuthority` exists but is a WorkOrder View-only issue role, not an operations line-management role) | Production Create/Edit/Hold; order-level release | held only by MaintenanceEngineer/EngineeringManager | No operations line-management role; EngineeringManager over-grants Delete/engineering rights | Create↔Approve | Role gap | **YES** (technical assessment) | live `RolePermissions` | OPEN |
| Commissioning Manager | none | depends on E-2 | none | undefined | undefined | Undetermined | **NOT DETERMINED** (depends on E-2) | — | OPEN |
| Operations Shift Supervisor | none | Create/Edit/Hold on runs | MaintenanceEngineer has them but is a maintenance role | dedicated shift role | Create↔Approve | Role gap | **YES** | grants above | OPEN |
| Operator | none | record input/output/loss only (Create); no Approve/Hold/Delete | none suitable — MaintenanceEngineer would also grant Barcode approve and InventoryMasterData edit | least-privilege operator role | Create↔Approve; no inventory adjustment | Role gap | **YES** | grants above | OPEN |
| Engineering Manager | EngineeringManager | technical approval, deviations | full Production incl. Create **and** Approve | none | SoD pair blocks same-record create+approve | None | **NO** | grants above | OPEN |
| Process Engineering Lead | EngineeringSupervisor (approx.) | View/Edit for investigation | no Production row | Production View/Edit | none | Permission gap | **NO** (grant to existing role) | role table | OPEN |
| Packaging Manager | none | packaging plan/execution ownership | none | no such role | Create↔Approve; must not hold release | Role gap | **YES** | absent from `SystemRoles.All` | OPEN |
| Laboratory Manager | QAManager (1 user in live DB) | Approve (release/reject); **Block requires Hold** | View + Approve | **Hold / ReleaseHold** (cannot Block/Unblock today) | naturally separated from Create (no Create) | Permission gap | **NO** (nearest role fits; grants need review) | `ProductionRunController.BlockBatch` requires `Hold`; QAManager lacks it | OPEN |
| HSE Manager | HSE | View; stop-work mechanism | no Production row | View; stop-work has no permission concept | n/a | Permission + mechanism gap | **NO** for View; stop-work OPEN (E-3) | HSE rows | OPEN |
| Security Manager | none | audit view; investigation | not verified (audit-log viewing rights not mapped) | undefined | must not hold Approve | Undetermined | **NOT DETERMINED** | — | OPEN |
| Warehouse Manager | none | receive/store/move stock; **not** quality release | InventoryMasterData C/E exist on engineering roles | no warehouse role; stock posting from production is gated by `Production.Create` | must not hold Production Approve | Role gap | **YES** | grants above | OPEN |

**Live grants match the seeded defaults:** the live `RolePermissions` rows for `Production` are exactly the four seeded roles (EngineeringManager, MaintenanceEngineer, QAManager, Viewer), so the matrix above reflects live state, not only code defaults. Live packaging tables: none.

**Tally (technical assessment):** new role likely required — 5 (Operations Manager, Shift
Supervisor, Operator, Packaging Manager, Warehouse Manager); not required — 7; not determined — 2.

### 12.3 Findings from the RBAC audit (documented, not fixed — outside Packaging scope)

| # | Finding | Severity | Note |
|---|---|---|---|
| R-1 | `QAManager` holds `Production.Approve` only. `BlockBatch` requires `Hold` and `UnblockBatch` requires `ReleaseHold`, which QAManager lacks. The role that releases/rejects cannot block. | MEDIUM | Existing behavior; changing grants is an E-5/D7 decision, not a Packaging change |
| R-2 | `Finance` and `HSE` have no `Production` row. | LOW | Governance roles cannot see Production data as configured |
| R-3 | `EngineeringManager` holds Create + Approve + Delete on Production. | INFORMATIONAL | SoD pair prevents same-record create+release; delete right is broad |
| R-4 | `PostOutputToInventory` is controller-gated by `Production.Create`; the quality gate is in the service. | INFORMATIONAL | Safe by the service gate; a package equivalent must keep the gate in the service |
| R-5 | Production lifecycle uses explicit dictionaries, not `IWorkflowRuntime`. | INFORMATIONAL | Precedent for Packaging; see the 2H activation record |
| R-6 | Only **one** live user holds `QAManager` (the sole release authority for production batches). | LOW | Operational continuity, not a code defect |
| R-7 | The live role list differs from the code constants (8 extra CMMS governance roles). | INFORMATIONAL | Documentation only; the earlier "12 roles" statement is corrected |

## 13. What each owner must do

Answer, in `PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md`: pick an option, add name and
date, set `Status: APPROVED`. That file is the only place the program reads approvals from.
