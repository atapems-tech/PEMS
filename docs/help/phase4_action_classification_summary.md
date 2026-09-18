# Phase 4 — Help-Coverage Action Classification Summary

> **Updated after Increment 3A** (2026-09-18): the matrix CSV has been patched (PowerShell-verified, not manually estimated) to reflect the 21 actions that gained a HelpKey across Increment 1 (7: MOC/QC/TechnicalQuery/Letter/Contract/Vendor.Index + Production.Dashboard) and Increment 2 (14: CriticalityAssessment.Dashboard, Instrument.Passport, RcmFmea.Index, SparePart.Index, JobPlan.Index, Permit.Details, StoreRequisition.Index, DocumentRevisions.Index, and 6 importer controllers sharing `Imports.StagedBatchOverview`). `HelpKeyExisting` is now populated on **44** rows total (was 23 at the matrix's original generation) — verified via `Import-Csv` (proper CSV-aware count, not a naive comma split), with **39** distinct HelpKey values (44 rows − 5 duplicate rows from the 6 importers sharing one key). No other classification/reason data was changed; this is a targeted patch to 21 specific rows, not a full re-audit.

> **Updated after Increment 3B** (2026-09-18): +2 more rows patched — `ReliabilityController.Index` and `.Dashboard()`, both sharing HelpKey `Reliability.Overview` (same shared-key pattern as the import controllers). `HelpKeyExisting` now populated on **46** rows total, **40** distinct HelpKey values — verified via `Import-Csv`.

**Total actions classified: 686**

> Source audit's final count was 690 actions; this matrix classifies **686** individually-named actions (difference: 4). The gap is almost entirely explained by `MaintenanceMasterDataController`: the source table explicitly names 9 `CreateX`/`ToggleXActive` reference-data pairs (18 actions) plus 2 standalone toggles (`ToggleFailureModeActive`, `TogglePMTemplateActive`) = 20 actions + `Index()` = 21 rows, but the source's own footnote states "(14 create/toggle action pairs total, all POST...)" = 28 actions, i.e. **5 additional CRUD-lite pairs (10 actions) exist in the codebase but were not individually named** in the source audit, so they cannot be listed as distinct CSV rows. All 21 named MaintenanceMasterDataController rows are classified G (reference-data CRUD-lite) or A (Index); the unnamed 10 would certainly also be G, following the exact same CreateX/ToggleXActive pattern, if listed. The remaining small variance comes from a few other condensed rows in the source (e.g. combined verb lists) where exact method boundaries were inferred from the prose rather than an explicit table row.

## Counts by Classification

| Letter | Category | Count | % of Total |
|---|---|---|---|
| A | User-Facing Page | 213 | 31.0% |
| B | User-Facing Action | 162 | 23.6% |
| C | Workflow Transition | 146 | 21.3% |
| D | Supporting Endpoint | 27 | 3.9% |
| E | Infrastructure/Internal | 17 | 2.5% |
| F | Duplicate/Redirect | 3 | 0.4% |
| G | Embedded Capability | 115 | 16.8% |
| H | Needs Review | 3 | 0.4% |

## Classification A — User-Facing Page candidates (future Help increments)

Total A rows: **213** — already has HelpKey: **46** (was 23 at original generation; +21 across Increments 1-2, +2 in Increment 3B), no HelpKey yet (candidates): **167**

### Engineering (86)

| Controller | Action | Has HelpKey |
|---|---|---|
| EngineeringDocumentsController | Index(filter) | Y |
| EngineeringDocumentsController | Create() | N |
| EngineeringDocumentsController | Details(id) | Y |
| EngineeringDocumentsController | Edit(id) | N |
| EngineeringDocumentsController | UploadAttachment(id) | N |
| EngineeringDocumentIntelligenceController | Index() | N |
| EngineeringDocumentIntelligenceController | Review(id) | N |
| EngineeringDashboardController | Index(projectId, disciplineId, minRevisionCount) | N |
| DocumentRegisterController | Index(project, disciplines, status, q, page, pageSize) | Y |
| DocumentRevisionsController | Index(documentId, page, pageSize) | Y |
| DocumentRevisionsController | Create(documentId) | N |
| DocumentWorkflowController | Index(status, page, pageSize) | N |
| DocumentUploadController | Index() | N |
| DisciplineController | Index() | N |
| DisciplineController | Details(id, projectId) | N |
| ProjectsController | Index() | N |
| ProjectsController | Create() | N |
| ProjectsController | Edit(id) | N |
| FunctionalLocationController | Index(page, pageSize) | Y |
| FunctionalLocationImportController | Index() | Y |
| FunctionalLocationImportController | Review(batchId) | N |
| EquipmentController | Index(projectId, disciplineId, equipmentType, status, q, page, pageSize) | Y |
| EquipmentController | Details(id) | Y |
| EquipmentController | Create() | N |
| EquipmentImportController | Index() | Y |
| EquipmentImportController | Review(batchId) | N |
| EquipmentClassificationController | Index() | N |
| EquipmentClassificationController | ReviewQueue(status, page) | N |
| EquipmentClassificationController | Coverage() | N |
| EquipmentCriticalityController | Index(criticalityClass, page, pageSize) | N |
| EquipmentFunctionalLocationAssignmentController | Index() | N |
| EquipmentFunctionalLocationAssignmentController | Review(batchId, page, pageSize, status) | N |
| CriticalityAssessmentController | Dashboard() | Y |
| CriticalityAssessmentController | Index(status, equipmentClassId, page) | N |
| CriticalityAssessmentController | Details(id) | N |
| CriticalityAssessmentController | Create(equipmentId) | N |
| MDRImportController | Index() | Y |
| MDRImportController | Review(batchId) | N |
| MDBController | Index(projectId, vendorId, page, pageSize) | N |
| MDBController | Details(id) | N |
| MDBController | Create() | N |
| VDRController | Index(projectId, vendorId, status, page, pageSize) | Y |
| VDRController | Details(id) | N |
| VDRController | Create() | N |
| VDRImportController | Index() | Y |
| VDRImportController | Review(batchId) | N |
| TransmittalController | Index(page, pageSize) | Y |
| TransmittalController | Details(id) | N |
| TransmittalController | Create() | N |
| LetterController | Index(letterType, letterNumber, subject,...) | Y |
| LetterController | MyReferrals() | N |
| LetterController | Details(id) | N |
| LetterController | Reply(id) | N |
| LetterController | Create(letterType) | N |
| TechnicalQueryController | Index(queryType, status, projectId, page, pageSize) | Y |
| TechnicalQueryController | Raise() | N |
| TechnicalQueryController | Details(id) | N |
| MOCController | Index(riskLevel, status, projectId, page, pageSize) | Y |
| MOCController | Raise(relatedEquipmentId) | N |
| MOCController | Details(id) | N |
| DCCController | Index() | N |
| DCCController | RegisterIncoming() | N |
| ContractController | Index(page, pageSize) | Y |
| ContractController | Details(id) | N |
| ContractController | Create() | N |
| VendorController | Index(page, pageSize) | Y |
| VendorController | Details(id) | N |
| VendorController | Create() | N |
| VendorController | Edit(id) | N |
| ProcurementPackageController | Index(page, pageSize) | N |
| ProcurementPackageController | Details(id) | N |
| ProcurementPackageController | Create() | N |
| RelationshipController | Index(q) | N |
| RelationshipController | Equipment(id) | N |
| RelationshipController | Document(id) | N |
| RelationshipController | Vendor(id) | N |
| RelationshipController | GapReport(projectId) | N |
| RelationshipController | BulkSuggestions(projectId) | N |
| TableExtractionController | Index(page, pageSize) | N |
| TableExtractionController | Review(batchId) | N |
| OCRProcessingController | Index(status, page, pageSize) | N |
| AIExtractionController | Index() | N |
| AIExtractionController | Review(attachmentId) | N |
| AISearchController | Index(q, discipline, documentType, status) | N |
| AIInsightsController | Index(projectId) | N |
| KnowledgeBaseController | Index(q) | N |

### CMMS (43)

| Controller | Action | Has HelpKey |
|---|---|---|
| WorkOrderController | Index(status, equipmentId, assignedToMe, page, pageSize) | Y |
| WorkOrderController | Create(equipmentId) | N |
| WorkOrderController | Details(id) | Y |
| WorkRequestController | Index(status, page, pageSize) | Y |
| WorkRequestController | Create() | N |
| WorkRequestController | Details(id) | N |
| JobPlanController | Index(page, pageSize) | Y |
| JobPlanController | Details(id) | N |
| MaintenanceStrategyController | Index(equipmentId, page, pageSize) | Y |
| MaintenanceStrategyController | BulkCreate(disciplineIds, equipmentTypes,...) | N |
| MaintenancePlanController | Index(page, pageSize) | Y |
| MaintenancePlanController | Create(strategyId, equipmentId) | N |
| MaintenanceMasterDataController | Index() | N |
| MaintenanceHistoryImportController | Index() | Y |
| MaintenanceHistoryImportController | Review(batchId, page, pageSize, status) | N |
| SparePartController | Index(q, lowStockOnly, page, pageSize) | Y |
| InstrumentController | Index(status, q, page, pageSize) | N |
| InstrumentController | Passport(id) | Y |
| InstrumentController | Edit(id) | N |
| InspectionController | Index(page, pageSize) | Y |
| CrewController | Index() | N |
| TeamController | Index() | N |
| RcmFmeaController | Index() | Y |
| RcmFmeaController | ClassDetail(id) | N |
| ReliabilityController | Index(from, to) | Y |
| ReliabilityController | Details(id) | N |
| ReliabilityController | ClassDetail(id) | N |
| ReliabilityController | DataQuality() | N |
| ReliabilityController | Intelligence(from, to, equipmentClassId) | N |
| ReliabilityController | RcmFeedback(status) | N |
| ReliabilityController | Dashboard() | Y |
| ReliabilityController | EquipmentProfile(id) | N |
| ReliabilityController | ClassIntelligence(id) | N |
| ReliabilityController | Ram(id) | N |
| ReliabilityController | ConditionMonitoring(equipmentId) | N |
| ReliabilityController | PredictiveMaintenance() | N |
| ReliabilityController | AiInsights(status) | N |
| CmmsReadinessController | Index() | N |
| CmmsReadinessController | ClassDetail(id, page) | N |
| CmmsOperationsController | Dashboard() | N |
| CmmsOperationsController | Backlog(status, priority, equipmentId,...) | N |
| AssetController | Index(status, q, page, pageSize) | N |
| AssetController | Details(id) | N |

### Operations (45)

| Controller | Action | Has HelpKey |
|---|---|---|
| PermitController | Index(status, pendingMyApproval, page, pageSize) | Y |
| PermitController | Create() | N |
| PermitController | Details(id) | Y |
| ShiftHandoverController | Index(status) | N |
| ShiftHandoverController | Create() | N |
| ShiftHandoverController | Details(id) | N |
| ProductionController | Dashboard() | Y |
| ProductionOrderController | Index(page, pageSize, status, search) | N |
| ProductionOrderController | Details(id) | N |
| ProductionOrderController | Create() | N |
| ProductionRunController | Details(id) | N |
| ProductionRunController | Create(productionOrderId) | N |
| ProductionRunController | BatchDetails(id) | N |
| QCController | Index() | Y |
| QCController | ItpIndex() | N |
| QCController | ItpCreate() | N |
| QCController | ItpDetails(id) | N |
| QCController | NcrIndex() | N |
| QCController | NcrCreate() | N |
| QCController | NcrDetails(id) | N |
| QCController | Kpi() | N |
| InventoryController | Index() | N |
| InventoryMasterDataController | Index() | N |
| InventoryTransactionController | Index(materialItemId, warehouseId, transactionType, fromDate, toDate, page, pageSize) | N |
| MaterialItemController | Index(q, categoryId, lowStockOnly, criticalOnly, page) | Y |
| MaterialItemController | Details(id) | N |
| MaterialRequisitionController | Index(page, pageSize) | Y |
| MaterialRequisitionController | Details(id) | N |
| MaterialRequisitionController | Create() | N |
| MaterialRequisitionController | Quotations(page, pageSize) | N |
| StoreRequisitionController | Index(status, page, pageSize) | Y |
| StoreRequisitionController | Details(id) | N |
| MrsImportController | Index() | Y |
| MrsImportController | Review(batchId, matchStatusFilter) | N |
| MeetingController | Index(page, pageSize) | N |
| MeetingController | Details(id) | N |
| MeetingController | Create() | N |
| TaskController | Index(tab, page, pageSize) | N |
| KpiController | Index(fromDate, toDate) | N |
| ReportController | Index(projectId, disciplineId, fromDate, toDate) | N |
| ReportController | Engineering(...) | N |
| ReportController | Vdr(...) | N |
| ReportController | Cmms(status, priority, fromDate, toDate) | N |
| ReportController | Procurement(...) | N |
| ReportController | Operations(periodType, anchorOrFrom, toDate, productionWorkCenterId, materialItemId) | N |

### Administration (29)

| Controller | Action | Has HelpKey |
|---|---|---|
| Admin/UsersController | Index(search, page, pageSize) | Y |
| Admin/UsersController | Create() | N |
| Admin/UsersController | Edit(id) | N |
| Admin/RolesController | Index() | Y |
| Admin/RolesController | Create() | N |
| Admin/PermissionMatrixController | Index() | Y |
| Admin/AuditLogController | Index(entityName, userName, page, pageSize) | Y |
| Admin/RoutingRulesController | Index() | N |
| Admin/StorageProvidersController | Index() | N |
| Admin/StorageProvidersController | Create() | N |
| Admin/StorageProvidersController | Edit(id) | N |
| Admin/StorageRoutingController | Index() | N |
| Admin/StorageRoutingController | Create() | N |
| Admin/StorageRoutingController | Edit(id) | N |
| Admin/ProjectIntelligenceController | Index() | N |
| Admin/HelpController | Index(categoryId, articleType, isPublished) | N |
| Admin/HelpController | Create() | N |
| Admin/HelpController | Edit(id) | N |
| Admin/HelpController | VisualCreate(articleId) | N |
| Admin/HelpController | VisualEdit(id) | N |
| Admin/HelpController | History(id) | N |
| DepartmentController | Index() | Y |
| EmployeeController | Details(id) | Y |
| EmployeeController | Index(departmentId, disciplineId, q, page, pageSize) | N |
| MyProfileController | Index() | N |
| SettingsController | Index() | N |
| DashboardSettingsController | Index() | N |
| NotificationController | Index() | N |
| QuickSetupController | Index() | N |

### Help (4)

| Controller | Action | Has HelpKey |
|---|---|---|
| HelpController | Index() | N |
| HelpController | Category(id) | N |
| HelpController | Article(code) | N |
| HelpController | Search(q) | N |

### Other (6)

| Controller | Action | Has HelpKey |
|---|---|---|
| HomeController | Index() | N |
| GlobalSearchController | Index(q) | N |
| BarcodeController | Index() | N |
| BarcodeController | BulkGenerate(disciplineIds, equipmentTypes, tagSearch) | N |
| ChatAssistantController | Index() | N |
| TagController | Index() | N |

## Classification F — Duplicate/Redirect cases

| Module | Controller | Action | Reason |
|---|---|---|---|
| Administration | Admin/DepartmentController | Index() | redirect stub to root controller |
| Administration | Admin/EmployeeController | Index() | redirect stub to root controller |
| Administration | Admin/SettingsController | Index() | redirect stub to root controller |

## Classification H — Needs Review (ambiguous cases)

| Module | Controller | Action | Why it's ambiguous |
|---|---|---|---|
| Engineering | RelationshipController | Search(q) | ambiguous: full search page vs. AJAX fragment for Index |
| Administration | PerformanceController | Index() | ambiguous: real feature page vs. internal dev tool |
| Other | ComingSoonController | Index(title, section) | ambiguous: real page vs. non-content placeholder |
