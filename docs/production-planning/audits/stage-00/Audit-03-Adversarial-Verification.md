# PPC Stage 0 - Audit Pass 3: Independent Adversarial Verification

Status: read-only audit. Plan rows 0561-0564. Reviewer role: adversarial internal reviewer (AI agent, fresh context, repository evidence only).

**INDEPENDENT THIRD-PARTY AUDIT NOT PERFORMED - REVIEWS ARE ROLE-BASED INTERNAL REVIEWS.**

## 1. Method

- Read the project rules index (AGENTS.md, docs/guidelines/README.md). No code, database, migration, Git state or Jira was changed. No database was connected. No test was run.
- The brief's proposed direction (PPC subsystem on existing ProductionOrder, ProductionRun, ProductionBatch, MaterialStock, InventoryTransaction, Equipment, workflow, Hangfire and AuditLog) was attacked point by point.
- Evidence = repo-relative path plus class, method or line. Worktree copies under `.claude/worktrees` and `.kilo/worktrees` were excluded; only the main tree was used.
- No design document for the PPC proposal exists in the repo: `docs/production-planning/audits/stage-00/` was empty before this report. Anything attributed to the proposal text itself is marked NOT VERIFIED.

## 2. Attack points

Severity: H = blocks design or produces wrong plan numbers; M = needs a decision or a test; L = hygiene.

| ID | Sev | Claim attacked | Evidence | Outcome |
|---|---|---|---|---|
| X1 | H | MRP offsets use lead time | `PEMS.Domain/Entities/MaterialItem.cs` has no lead-time field. `PEMS.Application/Inventory/NetDemandCalculator.cs` ~L140 emits `ReorderPolicyNotEvaluated` ("lead time undecided"). | CONFIRMED defect (gap) |
| X2 | H | Unit conversion via `MaterialUOM.ConversionFactor` | `PEMS.Domain/Entities/MaterialUOM.cs` comment says the field is not used yet. No read of ConversionFactor outside migrations and designers. `ProductionRunService` uses `Uom.Code` for display only (~L95-122). `ProductionOrder.UomId`, `ProductionOutput.UomId` and `ProductionInput` UOM are nullable. | CONFIRMED |
| X3 | H | Reservations double-counted in netting | `NetDemandCalculator.cs` header L7-11 and L107: usable = OnHand - Reserved, netted once. Cross-source SR/WO overlap: `PEMS.Persistence/Servises/NetDemandCollector.cs` header L21 says there is no FK between an SR line and a WorkOrderMaterial. | REFUTED for the calculator. Cross-source overlap NOT VERIFIED |
| X4 | H | Incoming supply netted | `NetDemandCalculator.cs` ~L84-124: `CompleteNetDemand` only when `KnownIncomingSupply` is given; otherwise `StockOnlyShortageEstimate`. No production PO or supply source is wired in. | CONFIRMED gap |
| X5 | H | NetDemand extends to production demand | `NetDemandCollector.cs` L18-20, L50: demand sources are WorkOrderMaterial (CMMS) only. No BillOfMaterial, Recipe or Formula entity in PEMS.Domain, PEMS.Persistence or PEMS.Application (grep). | CONFIRMED |
| X6 | H | Production consumption can take reserved stock | `PEMS.Persistence/Servises/MaterialStockService.cs` ~L176-200: consumption refused if OnHand + delta < 0, and refused if the new OnHand would be below ReservedQuantity. | REFUTED |
| X7 | M | Posting idempotency holds under concurrency | `PEMS.Persistence/Servises/PostingScope.cs` L25: the sp_getapplock path runs only when `IsSqlServer()` and no ambient transaction; otherwise the body runs unlocked. `ProductionRunServiceTests` (15 facts) and `ProductionOrderServiceTests` (9) use EF InMemory. Lock and idempotency are tested only in `PEMS.Tests/Inventory/ProductionPostingIdempotencyTests.cs` (2 facts, SQL Server). Whether that suite runs in CI: NOT VERIFIED. | CONFIRMED test gap |
| X8 | M | Duplicate runs and double start are prevented | `ProductionRunService.CreateAsync` (~L179-208) allows any number of Planned runs per order; there is no active-run check. `StartAsync` (~L210-232) is read-check-write with no transaction. A RowVersion conflict is mapped by `PEMS.Web/Diagnostics/PemsErrorMapper.cs`. | CONFIRMED (multiple Planned runs). Start-conflict handling REFUTED |
| X9 | H | Capacity overbooking is controlled | `PEMS.Domain/Entities/ProductionWorkCenter.cs` has no capacity, hours, efficiency or calendar fields. `ShiftDefinition.cs` has StartTime and EndTime only. | CONFIRMED missing |
| X10 | M | Resource compatibility and scheduling deadlocks are handled | No scheduling, resource-assignment or calendar entity found. `PEMS.Domain/Entities/RoutingRule.cs` exists; not inspected, and its name does not show a manufacturing routing. | NOT VERIFIED (design gap, nothing to test) |
| X11 | M | Batch and campaign conflicts are handled | No Campaign entity (grep). `ProductionRunService.RecordOutputAsync` checks that a reused batch belongs to the same run and material. | Campaign CONFIRMED missing; batch check REFUTED |
| X12 | H | Yield, scrap and co-product are modelled | `ProductionOutput.OutputClassification` is a free string defaulting to "Primary". `ProductionLoss.LossCategory` is a free string. `ProductionOrder` has no expected quantity or yield. | CONFIRMED gap |
| X13 | M | Lot genealogy exists | `PEMS.Domain/Entities/ProductionInput.cs` L31 `SourceProductionBatchId` links one level. `ProductionOutput.ProductionBatchId` is required. | REFUTED as "absent"; genealogy is single-level only |
| X14 | H | Plan versions are kept | `PEMS.Persistence/Servises/ProductionOrderService.cs` has no update method. `ProductionOrder.cs` has no version field. Only status transitions and `ProductionEvent` log entries exist. | CONFIRMED absent |
| X15 | H | Rollback of posted inventory | `ProductionRunService.CancelAsync` (~L274-290) sets status only. There is no reversal of posted ProductionInput or ProductionOutput (grep Reverse, Void, Undo: no hit in production services). | CONFIRMED |
| X16 | M | Production tables carry RowVersion and tenant | `PEMS.Domain/Common/BaseEntity.cs` L24-27 defines RowVersion and TenantId. `ProductionInput`, `ProductionOutput`, `ProductionLoss` and `ProductionEvent` do not derive from BaseEntity. | CONFIRMED |
| X17 | H | Production actions are permission-checked | `ProductionOrderController` 11/11 actions and `ProductionRunController` 20/20 actions call `HasPermissionAsync`; `ProductionController` 1/1. `ProductionOrderService` and `ProductionRunService` have no IPermissionService. | Controller bypass REFUTED. Service-layer gap CONFIRMED (defence in depth) |
| X18 | M | AuditLog is reused for production | Zero AuditLog references in `ProductionRunService`, `ProductionOrderService`, `MaterialStockService` and `ProductionDowntimeService`. Only `ProductionEvent` is written. | CONFIRMED |
| X19 | L | Hangfire is reused for planning jobs | Hangfire and BackgroundJob users: LetterReminder, OCR, Tender, DocumentUpload, Licensing. None in production services. | CONFIRMED (no production job pattern exists) |
| X20 | L | Calendar time basis | `ProductionRunService.cs`: 11 uses of `DateTime.Now`, 0 of `UtcNow`. | CONFIRMED risk |
| X21 | H | Regression tests cover the design | Production tests: ProductionOrderServiceTests, ProductionRunServiceTests, ProductionInventoryQualityGateTests (8), ProductionDowntimeServiceTests and ProductionTargetServiceTests, all InMemory (`PEMS.Tests/Cmms`). ProductionPostingIdempotencyTests (SQL Server) and NetDemandSqlTests (SQL Server) in `PEMS.Tests/Inventory`. NetDemandCalculatorTests are pure. No MRP, CRP, MPS, scheduling or BOM tests exist. | CONFIRMED |
| X22 | M | Compliance claims in rows 0092, 0250 and the supportability matrix | Row 0092 has no compliance claim. The row 0250 "ISO" hit is the word "Isolation". Row 0250 status is `Done` while its requirement says acceptance tests have not run. `plan/sheets/supportability-matrix.md` SM-24 says "Supported (unverified)". | Compliance claim REFUTED. Row 0250 "Done" CONFIRMED overstated |
| X23 | M | Plant WorkOrder kept separate from ProductionOrder | No FK from WorkOrder entities to ProductionOrder. Both reference Equipment (`ProductionOrder.EquipmentId`, `ProductionWorkCenter.EquipmentId`). | REFUTED (separation holds today) |
| X24 | M | Discrete support (serial, lot) | `MaterialItem.IsSerialized` and `IsBatchControlled` are flags only. No serial or lot entity. | CONFIRMED gap |
| X25 | L | Workflow platform reuse for production approvals | Not inspected. | NOT VERIFIED |
| X26 | L | Process-first sequencing and MRP II feedback | Proposal text is not in the repo. | NOT VERIFIED |

Tally: CONFIRMED 17; REFUTED 6; NOT VERIFIED 3 (total 26).

## 3. Strongest five risks

1. **No lead time, no incoming supply, no BOM** (X1, X4, X5). MRP net requirements cannot be correct; NetDemand is a CMMS demand calculator today.
2. **No capacity or calendar model** (X9, X10, X20). RCCP, CRP and scheduling have nothing to check against, and timestamps use local time only.
3. **No plan versioning and no reversal of posted production stock** (X14, X15). Revisions and cancellations leave plan and stock history unrecoverable.
4. **Unit and yield model is missing** (X2, X12). Nullable UOMs, an unused conversion factor and free-text loss and output classes produce silent quantity errors.
5. **Concurrency and regression coverage is thin** (X7, X8, X16, X21). Production services are tested in memory, locking runs only on SQL Server, and child records lack RowVersion.

## 4. Owner decisions required

1. Lead-time source and unit: per item, per supplier, or per routing step.
2. UOM model: pair-based conversion table or per-UOM factor; whether conversion across dimensions is allowed.
3. Netting rule: reserved stock is free-only, or reservations are consumed by production.
4. Incoming supply sources: which PO and production lines count, and whether unconfirmed supply is netted.
5. Capacity model: hours per shift, efficiency, and calendar basis (local or UTC).
6. Campaign and batch rules, including one-level versus multi-level genealogy.
7. Yield, scrap and co-product rules, with cost allocation.
8. Plan versioning (frozen MPS and MRP revisions) and reversal policy for posted stock.
9. Whether service-layer permission checks are required.
10. Compliance scope for PPC: the repo makes no PPC compliance claim; any claim needs a separate owner decision.
