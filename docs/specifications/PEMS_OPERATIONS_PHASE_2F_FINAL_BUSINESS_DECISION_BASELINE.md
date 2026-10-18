# PEMS Operations Phase 2F — Final Business Decision Baseline

**Status: DOCUMENTATION ONLY. No business approval is recorded in this document because none exists.**
Every "recommendation" below is labelled `TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL`.
Where a business owner has not decided, the entry says `BUSINESS APPROVAL REQUIRED`. The word
"selected" is never used to mean "approved".

**Evidence that no approval exists (checked this phase, not assumed):**
- The sign-off table in `PEMS_OPERATIONS_PHASE_2E_PACKAGING_EXECUTIVE_DECISION_PACK.md` (D1–D6 and
  "Final approval to proceed to Phase 3") is **blank** — no name, no date.
- A repository search of `docs/` for any recorded approval of D1–D6 returned nothing.
- `git log` contains no commit recording a Packaging business decision.
- No Packaging class exists in `PEMS.Domain`/`PEMS.Application`/`PEMS.Persistence`/`PEMS.Web`.

## 1. Phase 2F Gate Result

| Gate item | Status |
|---|---|
| D1 Package definition | **BLOCKED — BUSINESS APPROVAL REQUIRED** |
| D2 Mixed-product package | **BLOCKED — BUSINESS APPROVAL REQUIRED** |
| D3 Multi-batch package | **BLOCKED — BUSINESS APPROVAL REQUIRED** |
| D4 Gross / Tare / Net | **PHASE 3 QUANTITY MODEL BLOCKER** |
| D5 Split / Merge | **DEFERRED — OUTSIDE PHASE 3** (technical scope exclusion; business acknowledgement still required — see §4) |
| D6 Bulk vs packaged identity | **BLOCKED — BUSINESS APPROVAL REQUIRED** |

**Result: Phase 2F = BLOCKED for Phase 3 implementation.** Per the master program's own rule
(§15/§43-C), unresolved D1–D6 mean the architecture cannot be safely frozen and implementation
would require guessing. All non-blocked preparation is complete (this document, the Phase 3
Architecture Freeze package, the master audit report).

## 2. Decision Vocabulary

`DECIDED` (approved by an owner — **none exist**) · `TECHNICAL DEFAULT` (safe engineering default
that follows an existing PEMS convention; not a business choice) · `BUSINESS APPROVAL REQUIRED` ·
`DEFERRED` · `PHASE 3 OUT OF SCOPE` · `NON-BLOCKING` · `BLOCKING`.

## 3. Decision Matrix — D1 to D12

| ID | Topic | Owner | Approver | Status | Blocking Phase 3? | Technical recommendation (NOT BUSINESS APPROVAL) |
|---|---|---|---|---|---|---|
| D1 | What a Package is | Packaging Manager | Complex Manager, Laboratory Manager | BUSINESS APPROVAL REQUIRED | **BLOCKING** | One record = a physical container instance holding one quantity of one `MaterialItem` produced from identified batch(es) |
| D2 | Mixed-product package | Packaging Manager | Engineering Manager, Complex Manager | BUSINESS APPROVAL REQUIRED | **BLOCKING** | One Package = one `MaterialItem` |
| D3 | Multi-batch package | Process Engineering Lead | Packaging Manager, Laboratory Manager | BUSINESS APPROVAL REQUIRED | **BLOCKING** | Support multiple source-batch contributions per package (schema-neutral: many input rows) |
| D4 | Gross/Tare/Net | Finance Manager | Laboratory Manager, Complex Manager | PHASE 3 QUANTITY MODEL BLOCKER | **BLOCKING** | Store Gross+Tare, derive Net (Model A); Model B only if a certified frozen Net is required |
| D5 | Split/Merge | Laboratory Manager | Packaging Manager, Finance Manager | DEFERRED — OUTSIDE PHASE 3 | Resolvable by scope exclusion | Do not implement split/merge in Phase 3 |
| D6 | Packaged product identity | Finance Manager | Packaging Manager, Complex Manager | BUSINESS APPROVAL REQUIRED | **BLOCKING** | Keep `PackagingOutput.MaterialItemId` independent of input so either answer needs no schema change |
| D7 | Package release authority | Laboratory Manager | Engineering Manager | TECHNICAL DEFAULT | Non-blocking | Reuse existing `Production.Approve` (held today by `QAManager`, `EngineeringManager`) |
| D8 | Packaging Order tier | Operations Manager | Packaging Manager, Complex Manager | TECHNICAL DEFAULT | Non-blocking | No Order tier in Phase 3; a nullable parent can be added additively later |
| D9 | Handling unit / pallet | Warehouse Manager | Operations Manager | PHASE 3 OUT OF SCOPE | Non-blocking | Not built |
| D10 | Repacking | Packaging Manager | Laboratory Manager, Security Manager | PHASE 3 OUT OF SCOPE | Non-blocking | Not built |
| D11 | Loss/variance tolerance | Process Engineering Lead | Finance Manager, Operations Manager | BUSINESS APPROVAL REQUIRED | Non-blocking for build; blocks reliable variance *reporting* | Show raw difference; never invent a tolerance |
| D12 | Rounding | Finance Manager | Packaging Manager | DEFERRED | Non-blocking | Existing scale is `decimal(18,3)`; no rounding rule invented |

## 4. The Six Gate Decisions in Detail

### D1 — Package definition
- **Options analysed:** (a) container only; (b) product quantity only; (c) filled container = container + quantity + identity; (d) logistics/handling unit.
- **Technical recommendation:** (c). It mirrors how `ProductionBatch` is both "a quantity" and "a production event".
- **Business decision:** NONE RECORDED — `BUSINESS APPROVAL REQUIRED`.
- **Consequences if (c):** Technical — one `Package` row per physical unit with a required `MaterialItemId`, quantity, UOM and source linkage. Inventory — the stock unit is the package's `MaterialItem` quantity, posted through the existing ledger. Quality — quality status lives on the package. Barcode — `TargetType="Package"` identifies one physical unit. Reporting — package counts are meaningful. Phase 3 — Package is the central entity.
- **If the business answers (a)/(b)/(d):** the `Package` entity's core shape changes (no quantity, or no container identity, or a hierarchy). This is why D1 is BLOCKING.

### D2 — Mixed-product package
- **Recommendation:** `TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL`: one Package contains one `MaterialItem`. A real assortment/kit should be its own `MaterialItem`, not a multi-material package.
- **Impact if mixed is required:** per-material mass balance, per-material quality, and per-material stock become undefined for the package; `MaterialStock` grain would be violated. BLOCKING until answered.

### D3 — Multi-batch package
- **Recommendation:** model source batches as one input row per source batch (this is the existing `ProductionInput` pattern). Whether a single package may draw on more than one batch is then a **validation rule**, not a schema difference — but the package→batch link (single FK vs contribution rows) differs, so the answer is still needed before the entity is frozen.
- **If approved:** preserve per-batch contribution quantity, quality rule (a package is releasable only if every contributing batch is Released — a stricter rule that requires business confirmation), and two-way recall traceability.
- **If not approved:** enforce single-batch at execution time.

### D4 — Gross / Tare / Net
- **Measured vs derived:** Gross and Tare are measured facts; Net is derived (`Net = Gross − Tare`) under Model A. Under Model B Net is additionally frozen.
- **Decided by existing PEMS convention (TECHNICAL DEFAULT, not business choice):** precision `decimal(18,3)` (verified in `ProductionConfigurations.cs` and `InventoryTransactionConfiguration.cs`); corrections are new append-only rows, never overwrites (verified pattern across `ProductionOutput`, `InventoryTransaction`, `WorkflowHistory`).
- **Genuinely unresolved (BUSINESS APPROVAL REQUIRED):** Model A vs B; tare source (container default, measured each time, or default-with-override); weighing device/operator capture; UOM conversion (the existing `MaterialUOM.ConversionFactor` is unused — this gap is inherited, not created); rounding (D12).
- **Status:** `PHASE 3 QUANTITY MODEL BLOCKER` — Package/Output quantity columns cannot be frozen.

### D5 — Split / Merge
- **Recommendation:** do not implement in Phase 3.
- **Change-control record (Phase 2D §41 called D5 a hard blocker):**
  - *Original decision:* D5 is a hard blocker because the genealogy shape depends on it.
  - *New interpretation:* if split/merge is **excluded from Phase 3 scope**, D5 does not affect any Phase 3 record shape — the genealogy join table and a "Superseded" state are purely additive later.
  - *Reason:* excluding the feature removes the dependency; it does not answer the underlying business question.
  - *Affected documents:* Phase 2D specification §22–23 and register D5; Phase 2E baseline §28/§29.
  - *Required approval:* the Laboratory Manager and Packaging Manager must acknowledge that split/merge will not exist in the first release. Until then D5 stays a gate item.

### D6 — Bulk vs packaged identity
- **Options:** same `MaterialItem` (packaging is metadata/state); distinct `MaterialItem` (packaged SKU); transformation-related items.
- **Observation (new, verified against Phase 2D §27):** the *schema* does not depend on D6 if the packaging output's `MaterialItemId` is stored independently of the input's. D6 changes **posting semantics, master-data setup, costing and reports**, not table shape. It therefore blocks the inventory-integration and reporting phases (3C/3E) more than the foundation entities.
- **Constraint preserved:** no second Product Master is created under either answer; a distinct packaged SKU would be created in the existing `MaterialItem` master by the business, not by Packaging code.
- **Business decision:** NONE RECORDED — `BUSINESS APPROVAL REQUIRED`.

## 5. Secondary Decisions and New Items

| ID | Topic | Status | Detail |
|---|---|---|---|
| D7 | Release authority | TECHNICAL DEFAULT | Existing model: `Production.Approve` releases/rejects a batch, `Production.Hold` blocks, `Production.ReleaseHold` unblocks; SoD pairs `Create↔Approve`, `Edit↔Approve` (verified in `AccessControlSeeder`, `ProductionRunController`). Reusing them for Package needs no new permission. |
| D8 | Order tier | TECHNICAL DEFAULT | Omit in Phase 3. |
| D11 | Tolerance | BUSINESS APPROVAL REQUIRED | No invented number. |
| E-1 | Financial treatment of packaging events | BUSINESS APPROVAL REQUIRED | Accounting rules are not invented. |
| E-2 | Commissioning vs production boundary | BUSINESS APPROVAL REQUIRED | No marker is added until decided. |
| E-3 | HSE stop-work authority / legal | `SITE / LEGAL CONFIRMATION REQUIRED` | Not invented. |
| E-4 | Distinct Packaging permission module | BUSINESS APPROVAL REQUIRED | Default if none: reuse `"Production"`. |
| **E-5 (new, this phase)** | **Mapping of the 14 stakeholder roles to real PEMS roles** | **BUSINESS APPROVAL REQUIRED** | See §6. |

## 6. Stakeholder Governance vs the Real RBAC Model (new finding)

> **CORRECTION (Phase 2G).** This section was written from `SystemRoles.cs` only and says PEMS has
> 12 roles. The live database has **20** (eight extra CMMS work-order governance roles seeded by
> `WorkOrderGovernanceSeeder`: `MNT.Manager`, `MNT.PlanningHead`, `MNT.PlanningExpert`,
> `MNT.DisciplineHead`, `MNT.SeniorExpert`, `MNT.Expert`, `maintenance planning`,
> `OPS.IssueAuthority`). "Maintenance Manager → none/approximate" should read `MNT.Manager`. None of
> the extra roles has a Production grant, so the Packaging conclusions stand. Authoritative mapping:
> `PEMS_OPERATIONS_PHASE_2G_EXECUTIVE_DECISION_AND_ROLE_MAPPING_WORKBOOK.md` §12.

The Phase 2E RACI names 14 organizational roles. PEMS `SystemRoles` (verified in
`PEMS.Persistence/Identity/SystemRoles.cs`) defines only: `SystemAdmin`, `EngineeringManager`,
`EngineeringSupervisor`, `DisciplineEngineer`, `DocumentController`, `MaintenanceEngineer`,
`ContractManager`, `QAManager`, `Viewer`, `Legal`, `Finance`, `HSE`.

| Stakeholder role | Nearest existing PEMS role | Status |
|---|---|---|
| Complex Manager | none | No equivalent — `BUSINESS APPROVAL REQUIRED` |
| Maintenance Manager | `MaintenanceEngineer` | Approximate |
| Finance Manager | `Finance` | Exists |
| Operations Manager | none | No equivalent |
| Commissioning Manager | none | No equivalent |
| Operations Shift Supervisor | none | No equivalent |
| Operator | none | No equivalent |
| Engineering Manager | `EngineeringManager` | Exists |
| Process Engineering Lead | `EngineeringSupervisor` (approximate) | Approximate |
| Packaging Manager | none | No equivalent |
| Laboratory Manager | `QAManager` (holds `Production.Approve`) | Approximate — **business must confirm** |
| HSE Manager | `HSE` | Exists |
| Security Manager | none | No equivalent |
| Warehouse Manager | none | No equivalent |

**Documentation discrepancy (Phase 2E vs repository):** Phase 2E presents the Laboratory Manager as
the exclusive release authority; the repository's actual release authority is whoever holds
`Production.Approve` (today `QAManager` and `EngineeringManager`). Nothing is wrong in the code; the
governance documents simply assumed roles PEMS does not have. Creating new roles is an
organizational decision, so it is recorded as E-5 rather than done. Any Phase 3 permission design
must be expressed in existing roles unless E-5 is approved.

## 7. Quality Authority (preserved)

```
Production / Packaging → Product produced → Laboratory / Quality → Released | Blocked | Rejected
```
Verified in code: a batch cannot be posted to inventory unless `QualityStatus == "Released"`
(Phase 2C gate in `ProductionRunService.PostOutputToInventoryAsync`); release requires
`Production.Approve` with a creator/editor SoD check in `ProductionRunController.ReleaseBatch`.
Packaging personnel, Warehouse, Security and Operations have no release path today, and none may be
given one by Phase 3.

## 8. Observations on Existing Implementation (no defect claimed)

1. `PostOutputToInventory` in `ProductionRunController` is gated by `Production.Create`, while the
   quality gate is enforced inside the service. This is safe (a non-Released batch cannot post) but
   means the *poster* is a creator-level role. The Phase 3 package posting design should keep the
   same service-level gate and must not rely on the controller permission alone.
2. `ProductionDowntimeEvent`, `ProductionDowntimeService` and `ProductionTarget` **exist**. Phase 2D
   described downtime cause categories as "(if built)". This is a documentation discrepancy of
   wording only; the downtime cause whitelist (`Equipment, Process, Material, Utility, Planned,
   Other`) exists and packaging downtime metrics can reuse the concept.
3. `InspectionRecord.EquipmentId` is non-nullable — still not reusable for product/package quality.

## 9. Affected Documents

Phase 2D specification, Phase 2D register, Phase 2E governance baseline, Phase 2E KPI catalog,
Phase 2E executive pack — none is modified by this phase; contradictions are recorded here and in
the master audit report.

## 10. Conclusion

Phase 2F cannot close D1, D2, D3, D4 or D6 without inventing management approval. The prepared
implementation package is in `PEMS_OPERATIONS_PHASE_3_PACKAGING_ARCHITECTURE_FREEZE.md` (status:
PREPARED, NOT ACTIVE).
