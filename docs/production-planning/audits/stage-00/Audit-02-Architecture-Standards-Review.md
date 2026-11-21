# PPC Stage 0 - Audit Pass 2: Architecture and Standards Review

Audit date: 2026-11-13. Repo commit: 1185588f (branch feature/SCRUM-561-ppc-stage0-audit-saber).

## 1. Scope and independence

- Scope: domain boundaries, reuse of existing PEMS capability, database model and referential integrity, API and event contracts, integration boundaries (ERP, CMMS, inventory, procurement, quality, MES, OT), security and segregation of duties, performance, versioning and effectivity.
- Method: role-based internal review by an architect/integration reviewer persona. Read-only file inspection. No database connection. No source, config, migration or Git change.
- **No independent external audit occurred.** This is a role-based internal review and is not a certification or conformity statement.
- Project rules read first: AGENTS.md and docs/guidelines/README.md. No Jira issue was named in the brief.

## 2. Repo baseline facts used

- Domain entities (PEMS.Domain/Entities): ProductionOrder, ProductionRun, ProductionBatch, ProductionWorkCenter, RoutingRule, WorkOrder, Equipment, InventoryTransaction, MaterialStock, AuditLog, Instrument, IOPoint, ControlLoop.
- ProductionOrder: string Status (default "Draft"), optional EquipmentId and UomId, no FK to WorkOrder, no RowVersion, no external-system key.
- ProductionOrder mapping: PEMS.Persistence/Configurations/ProductionConfigurations.cs, unique index on OrderNumber (line 58), Restrict deletes.
- Migrations snapshot: PEMS.Migrations/Migrations/PEMSDbContextModelSnapshot.cs, ProductionOrders table (around line 14707). Core migration 20260915130204_AddProductionCore.cs. Snapshot read only; no database connected.
- Audit: PEMS.Persistence/Context/PEMSDbContext.cs, SaveChanges (line 582) and SaveChangesAsync (line 605). Audit rows are written in a second SaveChanges call (lines 593-598, 615-620). Audit entries built in PrepareAuditEntries (around line 684) and AuditLogs.Add (around line 801).
- Service: PEMS.Persistence/Servises/ProductionOrderService.cs. CreateAsync (line 244) saves the order, then calls LogEventAsync in a separate save. ReleaseAsync (line 290) and the other transitions save directly. No WorkflowPlatform call. Legal transitions are in a dictionary.
- Controller: PEMS.Web/Controllers/ProductionOrderController.cs. Permission checks by Production View, Create, Edit and Approve (line 120).
- Hangfire: PEMS.Web/Program.cs (lines 181-187, AddHangfire and AddHangfireServer). Only recurring job found: licensing-periodic-check (PEMS.Web/Licensing/LicensingExtensions.cs line 56).
- WorkflowPlatform: PEMS.WorkflowPlatform referenced by PEMS.Application, PEMS.Persistence and PEMS.Web. Used by document, contract and MOC services; not used by ProductionOrderService.
- Transactions: explicit BeginTransactionAsync found in ContractCreationScope, MDRImportService and NumberingService, not in ProductionOrderService or the audit path.
- Concurrency: no RowVersion or IsConcurrencyToken found on ProductionOrder, MaterialStock or InventoryTransaction.
- OT connectivity: no OPC UA, Modbus, S7 or MQTT package reference found in any main-tree .csproj.
- PPC planning engine (MRP, scheduler, capacity or forecast class): none found.
- Plan: docs/production-planning contains only audits/stage-00 (this folder). No PPC specification document was found in the repo.

## 3. Standards register

Verification date for all rows: 2026-11-13. Official sites ISO, IEC, GS1 and some ISA pages returned HTTP 403 or 404 to the fetch tool. Rows marked NOT VERIFIED rely on secondary sources only.

| Standard | Edition (as found) | Official source | Lifecycle status | Applicability to PEMS | PEMS evidence | Gap | Assessment |
|---|---|---|---|---|---|---|---|
| ANSI/ISA-95.00.01-2025 (IEC 62264-1 Mod) | 2025 | https://www.isa.org/products/ansi-isa-95-00-01-2025-iec-62264-1-mod-enterprise (identifier confirmed; edition and status not stated on page) | Current per a retailer listing; ISA page itself does not state status: NOT VERIFIED | Level 3/4 object model for production orders | ProductionOrder, ProductionRun, ProductionBatch | No written object mapping; no Parts 2-5 check | PARTIALLY CONFORMANT (informal mapping, not a certification) |
| IEC 62264 Parts 2-5 | NOT VERIFIED | not fetched | NOT VERIFIED | Object and activity models | none reviewed | full mapping | NOT ASSESSED |
| ISA-88 / IEC 61512-1 | Claimed IEC 61512-1:2026 Ed.2 (secondary source only; the ISA edition is ISA-88.00.01-2010) | webstore.iec.ch and isa.org returned 404/403 | NOT VERIFIED | Only if recipe-driven batch control is in scope; ProductionBatch exists, no recipe or phase model found in this pass | ProductionBatch | Edition unconfirmed | NOT ASSESSED |
| ISO 22400-1 | 2014 (2025 review per ISO snippet; a revision work item is also reported) | iso.org/standard/56847.html returned 403 | Current per snippet; revision conflicting: NOT VERIFIED | KPI definitions for production dashboards | ProductionDashboardDto, GetDashboardAsync (ProductionOrderService.cs line 146) | No KPI definition register mapping | NOT ASSESSED |
| ISO 22400-2 | 2014 with Amd 1:2017 (energy KPIs) | secondary source only | NOT VERIFIED | As above | as above | as above | NOT ASSESSED |
| ISO 9001 | 2026 revision claimed; FDIS ballot closed 2026-07-09; publication date conflicting (planned 2026-09-16) | iso.org/standard/62085.html returned 403 | NOT VERIFIED (status conflicting) | Organisational QMS, not product scope | none reviewed | none assessed | NOT ASSESSED |
| ISO 19011 | 2018 third edition; catalogue shows Withdrawn per snippet; replacement not identified | iso.org/standard/70017.html returned 403 | NOT VERIFIED | Auditor guidance for internal audits | none | none | NOT ASSESSED |
| ISO/IEC 19987 (EPCIS) | 2024 Edition 3 (ISO listing snippet) | iso.org/standard/85557.html returned 403; GS1 page 403 | Current per ISO snippet; EPCIS 2.0.1 NOT VERIFIED | Traceability event exchange | no EPCIS event model identified | none assessed | NOT ASSESSED |
| ISA/IEC 62443-2-1 | 2024 Ed.2.0 (secondary) | secondary source only | NOT VERIFIED | Asset-owner security program | AuthorizationAuditLog, RoleAssignment, permission checks in controllers | no OT security program evidence | NOT ASSESSED |
| ISA/IEC 62443-3-3 | 2013 Ed.1.0 (secondary) | secondary source only | NOT VERIFIED (2nd edition unconfirmed) | System security requirements; only if IACS is connected | no OT connection found (see A6) | n/a | NOT APPLICABLE while no OT link exists; re-check at integration |
| ISA/IEC 62443-2-4 and 1-5 | 2023 Ed.2.0; TS 62443-1-5:2023 | secondary source only | NOT VERIFIED | Service-provider and profile guidance | none | n/a | NOT APPLICABLE (no IACS service provider role identified) |
| ASCM CPIM | not a standard | n/a | Professional body of knowledge, not a normative ISO standard | Reference only | none | n/a | NOT APPLICABLE (not a standard) |

## 4. PEMS architecture findings

| ID | Severity | Evidence | Risk | Required action | Owner decision |
|---|---|---|---|---|---|
| A1 | Major | No MRP, scheduler, capacity or forecast class found (section 2). Hangfire runs only the licensing job (LicensingExtensions.cs line 56). ProductionOrderService is a manual status machine. | PPC planning run cannot be scheduled or traced; scope gap against brief | Define planning engine boundary and run model before build; use Hangfire for runs with idempotent job keys | Yes |
| A2 | Major | ProductionOrderService.ReleaseAsync (line 290) and the other transitions change Status directly. WorkflowPlatform not called. Controller checks an Approve permission (ProductionOrderController.cs line 120). CreateAsync stores createdByEmployeeId but no check prevents the creator from approving. | No maker-checker control; approval not auditable as a workflow | Route Release and Cancel through WorkflowPlatform; enforce creator is not approver | Yes |
| A3 | Major | PEMSDbContext.SaveChanges (lines 582-600) and SaveChangesAsync (lines 605-622) write data, then audit in a second save, with no shared transaction. CreateAsync logs its event in another save (ProductionOrderService.cs lines 280-282). | A failed second save leaves data with no audit row; order and event log not atomic | Wrap data and audit in one transaction (execution strategy as in NumberingService) and make CreateAsync event atomic | Yes |
| A4 | Major | No RowVersion or concurrency token on ProductionOrder, MaterialStock or InventoryTransaction. | Lost updates on stock and order status under concurrent use | Add row-version columns through a new migration; test concurrent release and stock issue | Yes |
| A5 | Major | No ERP, MES or external-source key on ProductionOrder, InventoryTransaction or MaterialItem (grep of entity files). | No idempotent integration; duplicates on re-import | Add source system and external key with unique constraint per source | Yes |
| A6 | Observation | No OPC UA, Modbus, S7 or MQTT package reference found. Domain holds Instrument, IOPoint, ControlLoop (instrumentation registry only). | Positive: no direct OT control path found. Future connector would be a new risk | Keep OT as read-only data source; any write-back needs a separate design gate and 62443 review | No |
| A7 | Moderate | ProductionOrder has no FK to WorkOrder (domain separation kept). InventoryTransaction carries both WorkOrderId and ProductionBatchId (InventoryTransaction.cs lines 35, 58). | Double attribution of material use between maintenance and production | Define one attribution rule per transaction type; add check constraint or service rule | Yes |
| A8 | Moderate | ProductionOrder.Status is a string (HasMaxLength 20) with no check constraint; transitions live only in service code (LegalTransitions). | Invalid status values possible from non-service writers | Add check constraint in migration; keep transition table in one place | No |
| A9 | Moderate | ProductionOrder.UomId is nullable while PlannedQuantity is decimal(18,3) (ProductionConfigurations.cs line 54). | Quantity without unit | Make UomId required for new orders in a migration with backfill | Yes |
| A10 | Minor | Equipment reused from CMMS (Equipment.cs; ProductionOrder.EquipmentId optional). | Positive: reuse aligned | Document the reuse rule | No |
| A11 | NOT VERIFIED | Effectivity and BOM or routing versioning: RoutingRule exists (PEMS.Domain/Entities/RoutingRule.cs) but was not read in this pass. | Unknown versioning risk | Read RoutingRule and BOM entities in next pass | Yes |

## 5. Reuse and duplication matrix

| Capability | Existing PEMS asset | Decision | Status |
|---|---|---|---|
| Inventory ledger | InventoryTransaction, MaterialStock | Reuse; add concurrency (A4) | Aligned with gap |
| Maintenance work | WorkOrder (CMMS) | Keep separate from ProductionOrder | Aligned (A7 for shared ledger) |
| Production order | ProductionOrder, ProductionRun | Reuse; do not create a parallel order entity | Aligned |
| Equipment | Equipment (CMMS) | Reuse | Aligned (A10) |
| Approvals | PEMS.WorkflowPlatform | Must be used for production release | Gap (A2) |
| Background runs | Hangfire | Reuse for planning runs | Gap (A1) |
| Audit | AuditLog via PEMSDbContext.SaveChanges | Reuse; make atomic | Gap (A3) |
| Dashboards | ProductionDashboardDto | Reuse; map to ISO 22400 KPIs if claimed | NOT ASSESSED |

## 6. Integration contract gaps

- ERP (orders, materials): no external key, no idempotency key, no contract version (A5).
- CMMS: WorkOrder link to production is by ledger row only; no event contract (A7).
- Inventory and procurement: no reservation concurrency control (A4).
- Quality: no quality hold field on ProductionOrder reviewed; NOT VERIFIED.
- MES and OT (PLC, SCADA, DCS): no interface defined; OT is read-only by design until a gate approves otherwise (A6).
- Events: no published event schema found for production status change; a versioned event contract is required before any integration.

## 7. Owner decisions required

- OD-01 (A1): approve planning engine boundary and Hangfire run model.
- OD-02 (A2): approve WorkflowPlatform routing for production release and maker-checker rule.
- OD-03 (A3): approve transactional audit change in PEMSDbContext.
- OD-04 (A4): approve row-version migration for stock and order tables.
- OD-05 (A5): approve external key and source-system columns.
- OD-06 (A7): approve attribution rule for shared inventory transactions.
- OD-07 (A9): approve UomId required with backfill.
- OD-08 (A11): approve reading of effectivity and routing versioning in next pass.

## 8. Limitations

- Official ISO, IEC, GS1 and some ISA pages were unreachable for verification; their rows are NOT VERIFIED.
- No Jira or plan row was read for this pass.
- No database was connected; migration evidence is from the snapshot only.
- No independent external audit occurred.
