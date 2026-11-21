# PPC Stage 0 - Audit 01: Functional and Manufacturing Review

Status: Pass 1, draft for owner review. Date: 2026-11-13. Repository: PEMS (main branch tree, worktree copies excluded).

## 1. Scope and method

- Read the named entities and services: ProductionOrder, ProductionRun, ProductionBatch, ProductionInput/Output/Loss/Event, ProductionTarget, ProductionWorkCenter, ShiftDefinition, MaterialUOM, ProductionOrderService, ProductionRunService, ProductionTargetService, OperationsReportService, NetDemandCalculator, NetDemandService, MaterialStock.
- Searched PEMS.Domain, PEMS.Application, PEMS.Persistence and PEMS.Web for BOM, recipe, routing, MPS, RCCP, MRP, CRP, calendar, capacity, scheduling, lot/serial and genealogy terms. Migrations and worktree copies were excluded.
- Paths are relative to `PEMS\`. Line numbers are from the files as read.
- The owner brief contains no Persian terms, so none are used here. Owner term MPP has status PROPOSED BY OWNER; its definition is taken from the brief only.
- No standard or clause is cited.

## 2. Findings

| ID | Severity | Description | Evidence (path, class, method) | Affected scope | Required action | Owner decision |
|---|---|---|---|---|---|---|
| F1 | Critical | No BOM, recipe or routing. Consumed materials are typed at run time with no standard usage or yield. | Domain has no BOM/Recipe/Routing type (grep). `ProductionRunService.RecordInputAsync` (292-326) accepts any active material. | Process, discrete | Model product structure and routing | yes (OD-02, OD-03) |
| F2 | Critical | No MRP. Production demand cannot drive procurement. | `NetDemandCalculator.Calculate` (NetDemandCalculator.cs 20-159) nets work-order demand lines. `NetDemandService.cs` has no reference to Production* types (grep). | MRP | Define production demand source and netting | yes (OD-06) |
| F3 | Critical | No capacity model (RCCP, CRP). | `ProductionWorkCenter` (11-30) has no capacity field. `ProductionOrderService.CreateAsync` (244-285) and `ProductionRunService.CreateAsync` (179-208) check status and existence only. | RCCP, CRP | Define capacity per work center | yes (OD-05) |
| F4 | Critical | No MPS, MPP or APP layer. | `ProductionOrder` (16-59) is one order with PlannedQuantity and dates. No version, product family or time bucket. | APP, MPP, MPS | Define plan hierarchy | yes (OD-04) |
| F5 | Major | Plan and release are status changes only. | `PlanAsync` (287-288) and `ReleaseAsync` (290-303) check the transition table only, not material, capacity or dates. | MPS, release | Define release gate | yes (OD-06) |
| F6 | Major | No scheduling. | `ShiftDefinition` (12-27) is a clock range with no calendar. `ProductionOrder` dates (30-31) are checked only for finish >= start (`ProductionOrderService.cs` 257-258). | PS, APS | Define calendar and scheduling rule | yes (OD-07) |
| F7 | Major | Order completion has no quantity closure. | `ProductionOrderService.CompleteAsync` (361-379) checks unfinished runs only. `ProductionOrder` has no actual-quantity field. PlannedQuantity is never compared with output. | Actual vs plan | Add closure rule | yes (OD-10) |
| F8 | Major | Order hold and cancel do not reach runs. | `HoldAsync` (305-321) changes the order only. `RecordOutputAsync` (328-340) checks run status, not order status. `CancelAsync` (349-350) ignores OnHold and Planned runs. | Production order lifecycle | Propagate hold and cancel | yes (OD-10) |
| F9 | Major | Over-production and concurrent runs are allowed. | `RecordOutputAsync` (328-394) has no cap against PlannedQuantity. `CreateAsync` (179-208) allows several runs per order. | Actual vs plan | Define tolerance and run rule | yes (OD-10) |
| F10 | Major | Output can be added to a batch already decided by QC. | `RecordOutputAsync` (347-376) increases batch Quantity without checking QualityStatus. | Quality gate | Block or re-open QC on new output | yes (OD-08) |
| F11 | Major | Inputs are not gated by batch quality. | `RecordInputAsync` (305-309) checks batch existence only. `PostInputCoreAsync` (427-460) has no QC check. | Quality gate, genealogy | Gate inputs by QC status | yes (OD-08) |
| F12 | Major | No quarantine or recall after release. | `BatchQualityTransitions` (`ProductionRunService.cs` 32-37) has no edge out of Released. Posted output (`PostOutputCoreAsync` 467-511) cannot be reversed. | Quality, recall | Define recall path | yes (OD-08) |
| F13 | Major | Stock has no batch dimension. | `MaterialStock` (OnHandQuantity, ReservedQuantity, lines 23-25) is per material and warehouse. Batch identity appears only on InventoryTransaction via productionBatchId (`ProductionRunService` 449-453, 500-504). | Traceability | Decide batch stock model | yes (OD-09) |
| F14 | Major | Discrete traceability is absent. | `ProductionBatch.Quantity` (39) is a decimal lot. No serial or unit record. Genealogy is one level (`ProductionInput.SourceProductionBatchId`, 29-32). `ConsumedAsSourceIn` (60-63) is a hook only. | Discrete, traceability | Choose lot or serial model | yes (OD-09) |
| F15 | Major | Reconciliation sums across materials. | `BuildReconciliation` (`ProductionRunService.cs` 142-177) sums all inputs, outputs and losses without MaterialItemId. Comparable only when UoM is equal or null (156). No per-material balance. | Actual vs planned, mass balance | Per-material balance and tolerance | yes (OD-10) |
| F16 | Major | Operations variance mixes units and materials. | `OperationsReportService.GetSummaryAsync` (30-89) sums Quantity across UoM (50, 68, 76). Losses ignore material (56-57). `VarianceQuantity` (`OperationsFoundationDtos.cs` 81) = output minus target. | KPI, variance | Unit-safe variance | yes (OD-11) |
| F17 | Moderate | Unit conversion is not implemented. | `MaterialUOM.ConversionFactor` (20-21) is stored and documented as unused. Written only in `MaterialMasterConfigurations.cs` and `InventoryMasterDataController.cs`. | All quantities | Define conversion policy | yes (OD-11) |
| F18 | Moderate | Actuals carry no shift. | `ProductionOutput` has no shift field. `OperationsReportService` filters only work center and material (35-44). Targets carry ShiftDefinitionId (`ProductionTarget` 29). | Shift performance | Add shift attribution | yes (OD-05) |
| F19 | Moderate | Target is not checked against the order material. | `ProductionTargetService.CreateAsync` (57-118) checks order existence (69-73) but not that order material equals target material (96). | Target integrity | Add material check | no |
| F20 | Moderate | Dashboard shows a placeholder and mixed units. | `ProductionOrderService.GetDashboardAsync` line 240 sets `UnresolvedReconciliationIssueCount = 0`. Lines 177-179 sum output across materials and units. | Dashboard | Replace placeholder, unit-safe sums | no |
| F21 | Minor | Run resume ignores order status. | `ProductionRunService.ResumeAsync` (233-246) does not check `ProductionOrder.Status`. | Run lifecycle | Add guard | no |
| F22 | Observation | Existing controls are sound. | Transition tables (`ProductionOrderService` 19-27; `ProductionRunService` 21-27). Loss categories (30). Primary/ByProduct check (334-336). Mixed-UoM refusal (156-171). Post only for Released batch (491-495). | Baseline | Keep | no |
| F23 | Observation | Owner terms are not in the code. | Grep for MPP, APP, MPS, RCCP, CRP, PS, APS, master production found no code match. Docs mention planning only in `docs\specifications\PEMS_OPERATIONS_PHASE_2E_KPI_PI_RI_KRI_CATALOG.md` (88). | Scope | Confirm terms with owner | yes (OD-01) |

Totals: Critical 4, Major 12, Moderate 4, Minor 1, Observation 2 (23 findings).

## 3. Owner terms

| Term | Current PEMS support | Gap | Decision needed |
|---|---|---|---|
| APP | None. `ProductionTarget` (per date, material, work center, shift) is the nearest record. | No product family or aggregate horizon. | OD-04 |
| MPP | None. Status PROPOSED BY OWNER. | No approved, versioned plan. | OD-04 |
| MPS | `ProductionOrder` PlannedQuantity and dates (single order). | No time buckets, versions or netting. | OD-04 |
| RCCP | None. | No capacity per work center (F3). | OD-05 |
| MRP | `NetDemandCalculator`, procurement-side only. | No BOM explosion, no production demand (F1, F2). | OD-02, OD-06 |
| CRP | None. `ProductionTarget` is a target, not a load. | No load per period. | OD-05 |
| PS | `ShiftDefinition` clock range only. | No sequencing or machine allocation (F6). | OD-07 |
| APS | None. | Not started. | OD-07 |
| MRP II (closed loop) | Actuals from `ProductionOutput` and `ProductionLoss` feed reports. | No feedback into MPS, capacity or MRP. | OD-04, OD-06 |

## 4. Process, discrete and hybrid semantics

- **Process.** Mass balance, yield and co-products matter. PEMS records typed inputs, outputs (Primary or ByProduct) and losses. The Difference formula (`ProductionRunService` line 164) is not per material and has no yield standard (F1, F15). ByProduct has no allocation rule.
- **Discrete.** Counted units, operation sequence and serial traceability matter. PEMS has none of these. A batch is a decimal quantity (F14).
- **Hybrid.** Needs both. No mode field exists on `ProductionOrder` (16-59). The mode must be decided before any structure model is built (OD-01).

## 5. Owner decisions needed

- **OD-01** Stage 1 manufacturing mode: Process, Discrete or Hybrid, and which first.
- **OD-02** Product structure: BOM or recipe, yield, versioning, co-products.
- **OD-03** Routing: operations sequence for discrete and process steps.
- **OD-04** Plan hierarchy: APP, MPP (approval, versions, horizon) and MPS buckets.
- **OD-05** Capacity: capacity unit per work center, calendar, shifts, efficiency.
- **OD-06** Demand and release: production demand source, netting with procurement, release gate.
- **OD-07** Scheduling: finite or infinite; PS and APS in Stage 1 or later.
- **OD-08** Quality gate: input from non-Released batch, output to decided batch, recall after release.
- **OD-09** Traceability: lot or serial, batch-level stock, genealogy depth.
- **OD-10** Closure: over and under production tolerance, hold and cancel propagation, per-material reconciliation tolerance.
- **OD-11** Unit conversion policy and unit-safe variance.
