# 02 Existing capability inventory (PPC Stage 0)

Status key: **Implemented** (entity, service and UI present), **Partial** (some parts present), **Absent** (not found in code; grep and file reads). Paths relative to `D:\PEMS\03-SOURCE\PEMS`.

| # | Capability | Where (path, class) | Status | Notes |
|---|---|---|---|---|
| C1 | Production order | `PEMS.Domain/Entities/ProductionOrder.cs`; `PEMS.Persistence/Servises/ProductionOrderService.cs`; `PEMS.Web/Controllers/ProductionOrderController.cs` | Implemented | Single order, PlannedQuantity, dates, status transitions in a dictionary (`LegalTransitions`). No version, no product family. |
| C2 | Production run | `ProductionRun.cs`; `ProductionRunService.cs` | Implemented | Start, pause, resume, hold, complete, cancel. Several Planned runs allowed per order (Audit-03 X8). |
| C3 | Batch with QC gate | `ProductionBatch.cs`; `ProductionRunService` | Implemented | Quality status Produced, AwaitingQC, Released, Blocked, Rejected. Decimal quantity, no serial. |
| C4 | Inputs, outputs, losses, events | `ProductionInput.cs`, `ProductionOutput.cs`, `ProductionLoss.cs`, `ProductionEvent.cs` | Implemented | Output and input post to inventory; output only for Released batch. Classification and loss category are free strings (X12). |
| C5 | Inventory posting | `PEMS.Persistence/Servises/MaterialStockService.cs`; `InventoryTransaction.cs` | Implemented | Refuses consumption that breaks reserved stock (X6, refuted). `ProductionBatchId` on transactions. |
| C6 | Production targets | `ProductionTarget.cs`; `ProductionTargetService.cs` | Partial | Service and tests exist; no screen found in this pass. Material not checked against order (F19). |
| C7 | Downtime events | `ProductionDowntimeEvent.cs`; `ProductionDowntimeService.cs` | Implemented | Source work order link available. |
| C8 | Production dashboard | `PEMS.Web/Controllers/ProductionController.cs`; `PEMS.Web/Views/Production/Dashboard.cshtml`; `ProductionDashboardDto` in `PEMS.Application/DTOs/ProductionDtos.cs` | Partial | Placeholder value for unresolved reconciliations (F20); sums mixed units (F16, F20). |
| C9 | Operations report and KPI | `PEMS.Persistence/Servises/OperationsReportService.cs` | Partial | Sums quantities across units and materials (F16). |
| C10 | Work center | `ProductionWorkCenter.cs` | Partial | Entity only: no capacity, hours, efficiency or calendar fields (F3, X9). |
| C11 | Shift definition | `ShiftDefinition.cs` | Partial | Clock range only; no calendar (F6). |
| C12 | Production calendar | — | Absent | Only maintenance calendar (`CmmsCalendarService`, `MaintenanceCalendarController`). |
| C13 | Routing | `PEMS.Domain/Entities/RoutingRule.cs` | Partial / unverified | Exists; read in Audit-03 only for its name. Not a manufacturing routing (A11, X10 NOT VERIFIED). Other "routing" types are workflow, letter, storage, part catalogue. |
| C14 | BOM, formula, recipe | — | Absent | Grep: no type in Domain, Persistence, Application (X5). |
| C15 | Units of measure | `PEMS.Domain/Entities/MaterialUOM.cs` | Partial | `ConversionFactor` stored but unused (F17, X2). Production UOM fields nullable (A9). |
| C16 | Procurement net demand | `PEMS.Application/Inventory/NetDemandCalculator.cs`; `PEMS.Persistence/Servises/NetDemandService.cs`, `NetDemandCollector.cs` | Partial | Demand from CMMS WorkOrderMaterial only (X5). No lead time (X1). No incoming supply source (X4). |
| C17 | Replenishment recommendations | `ReplenishmentRecommendation.cs` | Partial | Procurement side only. |
| C18 | Inventory ledger and stock | `MaterialStock.cs`, `InventoryTransaction.cs`, `Warehouse.cs`, `StorageLocation.cs` | Implemented | Stock per material and warehouse; no batch dimension on stock (F13). Reserved quantity tracked. |
| C19 | Inventory receiving (IRN) | `IrnReceipt.cs`, `IrnReceiptLine.cs`, `IrnDispositionRecord.cs`; `IrnReceiptService.cs` | Implemented | Accepted, rejected and hold quantities; hold resolution. |
| C20 | Material requisition and PO | `MaterialRequisition.cs`, `PurchaseOrder.cs`, `PurchaseOrderAuthorityRule.cs` | Implemented | PO approval by authority table; uses WorkflowPlatform. |
| C21 | Tenders and vendors | `Tender.cs`, `Bid.cs`, `Vendor.cs`, `VendorQualificationService.cs` | Implemented | Vendor qualification and risk. |
| C22 | Maintenance work orders | `WorkOrder.cs`, `WorkRequest.cs`, `JobPlan.cs` | Implemented | CMMS only. No link to ProductionOrder (by design, A7, X23). |
| C23 | PM plans and RCM | `MaintenancePlan.cs`, `MaintenanceStrategy.cs`, `PMTemplate.cs` | Implemented | Interval and meter triggers. |
| C24 | Equipment hierarchy | `Equipment.cs` | Implemented | Parent and child; serial number. Shared by CMMS and production (A10). |
| C25 | Shutdown or turnaround | — | Absent | Only SIMOPS and permit entities (`SimopsConflict.cs`, `Permit.cs`, `MOC.cs`). |
| C26 | Workflow platform | `PEMS.WorkflowPlatform/` (definitions, versions, stages, instances, history) | Implemented | Used by PO, IRN, contract, EDMS, WO, permit, NCR. **Not used by production order service** (A2). |
| C27 | Quality: NCR, inspection | `NonConformanceReport.cs`; `InspectionRecord.cs`, `InspectionTestPlan.cs`, `ITPLineItem.cs`; `QCController.cs` | Implemented | Critical NCR multi-person approval deferred (QCController comment). |
| C28 | EDMS documents and revisions | `Document.cs`, `DocumentRevision.cs`, `DocumentWorkflow.cs`, `Transmittal.cs`; `DocumentWorkflowService.cs` | Implemented | No recipe or drawing entity. |
| C29 | Lot and serial | `MaterialItem.IsSerialized`, `IsBatchControlled` | Partial | Flags only (X24). |
| C30 | Genealogy | `ProductionInput.SourceProductionBatchId`; `ProductionBatch.ConsumedAsSourceIn` | Partial | One level (X13). Hook reserved for later phases. |
| C31 | Audit trail | `AuditLog.cs`; `PEMS.Persistence/Context/PEMSDbContext.cs` (SaveChanges, PrepareAuditEntries) | Implemented | Second save, not atomic with data (A3). Production services write `ProductionEvent` and not `AuditLog` (X18). |
| C32 | Authorization | `IPermissionService`; controller checks in `ProductionOrderController`, `ProductionRunController`, `ProductionController` | Implemented | Controller checks present for every action (X17). Services have no permission check (defence in depth gap). |
| C33 | Background jobs | `PEMS.Web/Program.cs` (AddHangfire, AddHangfireServer); `PEMS.Web/Licensing/LicensingExtensions.cs` | Implemented | Hangfire in use for letters, OCR, tender mail, document upload, licensing. **No production or planning job** (A1, X19). |
| C34 | Excel export | `PEMS.Web/Helpers/ExcelExportHelper.cs`; `PartCatalogueReportExport.cs` | Implemented | Reusable helper. |
| C35 | KPI catalogue | `PEMS.Application/Common/KpiCatalog.cs`; `ExecutiveKpiService.cs` | Implemented | ISO 22400 mapping not declared (not assessed). |
| C36 | Plan master | `plan/rows/*.md` (rows 0092, 0095, 0227, 0033, 0250) | Partial | Production rows Not Started; 0250 "Done" with unrun acceptance tests (X22). |
| C37 | MRP / MPS / RCCP / CRP / PS / APS / APP / MPP | — | Absent | No code match for these terms (F23). |
| C38 | OT connectivity (PLC, SCADA, DCS) | — | Absent | No OPC UA, Modbus, S7, MQTT package in .csproj (A6). Instrument, IOPoint, ControlLoop entities exist (registry only). |
| C39 | Event contracts (external) | — | Absent | No published production event schema (A5 and pass 2 section 6). |
| C40 | External keys for integration | — | Absent | No ERP or MES key on ProductionOrder, InventoryTransaction, MaterialItem (A5). |

## Reuse decisions proposed (see document 06)
- Inventory ledger, equipment, workflow, audit and Hangfire: **reuse**. Gaps listed in document 15.
- Production order, run, batch: **reuse and extend**; no parallel order entity.
- Maintenance WorkOrder and ProductionOrder: **keep separate**.
