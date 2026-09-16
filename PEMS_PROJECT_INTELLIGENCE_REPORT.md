# PEMS Project Intelligence Report

Generated: 2026-09-15T16:55:26.8363863Z - Branch: `main` - Last commit: `68ce0ef 2026-09-15 barcode`

This report is generated evidence, not a manual estimate. Each module below is a
controller found in the repository; each signal is checked directly against source,
EF configuration, migrations, views, sidebar navigation, tests, docs, and git history.
Evidence coverage % = (signals confirmed Present) / (signals checked). It is a
mechanical ratio over the checklist below it, not an estimate of feature completeness -
read the checklist, not just the percentage. Unknown means the scan could not confirm
either way (never treat Unknown as Missing).

## Summary

- Controllers scanned (candidate modules): **107**
- Redirect-shim controllers (no real logic, forward elsewhere): **11**
- Controllers with zero Razor views found: **8** (may be legitimate JSON/API controllers - verify before assuming broken)
- Controllers not linked from the sidebar menu: **17** (may be reachable via deep link, drilldown, or another page)
- Controllers with no reference found in PEMS.Tests: **27**

## Modules (sorted by evidence coverage, lowest first)

| Module | Area | Coverage | Present/Total | Unknown | Actions | Views | Nav | Tests | Docs | Last touched |
|---|---|---|---|---|---|---|---|---|---|---|
| Projects | - | 56% | 5/9 | 0 | 6 | 3 | yes | no | no | 2026-09-05 |
| QC | - | 56% | 5/9 | 2 | 17 | 8 | yes | yes | no | 2026-09-05 |
| QuickSetup | - | 67% | 6/9 | 1 | 1 | 1 | yes | yes | no | 2026-09-08 |
| Production | - | 67% | 6/9 | 0 | 1 | 1 | yes | yes | no | - |
| ProductionOrder | - | 67% | 6/9 | 0 | 11 | 3 | yes | yes | no | - |
| ProductionRun | - | 56% | 5/9 | 0 | 20 | 3 | no | yes | no | - |
| RcmFmea | - | 67% | 6/9 | 0 | 16 | 2 | yes | yes | no | 2026-09-10 |
| SearchApi | - | 22% | 2/9 | 1 | 2 | 0 | no | no | no | 2026-09-05 |
| Settings | - | 56% | 5/9 | 1 | 1 | 1 | yes | yes | no | 2026-09-05 |
| ShiftHandover | - | 67% | 6/9 | 0 | 7 | 3 | yes | yes | no | 2026-09-05 |
| Relationship | - | 67% | 6/9 | 0 | 9 | 3 | yes | yes | no | 2026-09-08 |
| Reliability | - | 67% | 6/9 | 0 | 20 | 14 | yes | yes | no | 2026-09-11 |
| Report | - | 56% | 5/9 | 0 | 5 | 5 | yes | no | no | 2026-09-05 |
| MDRImport | - | 67% | 6/9 | 0 | 8 | 2 | yes | yes | no | 2026-09-08 |
| Meeting | - | 67% | 6/9 | 0 | 8 | 3 | yes | yes | no | 2026-09-06 |
| MOC | - | 67% | 6/9 | 1 | 12 | 3 | yes | yes | no | 2026-09-05 |
| MaterialItem | - | 67% | 6/9 | 0 | 8 | 2 | yes | yes | no | 2026-09-15 |
| MaterialRequisition | - | 67% | 6/9 | 0 | 8 | 4 | yes | yes | no | 2026-09-05 |
| MDB | - | 56% | 5/9 | 1 | 10 | 3 | yes | no | no | 2026-09-06 |
| MrsImport | - | 67% | 6/9 | 0 | 5 | 2 | yes | yes | no | 2026-09-08 |
| Performance | - | 44% | 4/9 | 0 | 1 | 1 | yes | no | no | 2026-09-05 |
| Permit | - | 67% | 6/9 | 0 | 12 | 3 | yes | yes | no | 2026-09-05 |
| ProcurementPackage | - | 67% | 6/9 | 0 | 11 | 3 | yes | yes | no | 2026-09-06 |
| MyProfile | - | 44% | 4/9 | 2 | 3 | 1 | no | yes | no | 2026-09-05 |
| Notification | - | 56% | 5/9 | 0 | 4 | 2 | no | yes | no | 2026-09-05 |
| OCRProcessing (redirect shim) | - | 56% | 5/9 | 1 | 2 | 1 | yes | yes | no | 2026-09-08 |
| Simops | - | 44% | 4/9 | 0 | 4 | 0 | no | yes | no | 2026-09-05 |
| Department (redirect shim) | Admin | 44% | 4/9 | 0 | 1 | 0 | yes | no | no | 2026-09-05 |
| Employee (redirect shim) | Admin | 44% | 4/9 | 0 | 1 | 0 | yes | no | no | 2026-09-05 |
| PermissionMatrix (redirect shim) | Admin | 67% | 6/9 | 1 | 2 | 1 | yes | yes | no | 2026-09-08 |
| WorkOrder | - | 67% | 6/9 | 0 | 21 | 3 | yes | yes | no | 2026-09-11 |
| WorkRequest | - | 67% | 6/9 | 0 | 8 | 3 | yes | yes | no | 2026-09-05 |
| AuditLog | Admin | 67% | 6/9 | 0 | 1 | 1 | yes | yes | no | 2026-09-05 |
| ProjectIntelligence (redirect shim) | Admin | 44% | 4/9 | 1 | 2 | 1 | yes | no | no | 2026-09-05 |
| StorageProviders | Admin | 56% | 5/9 | 0 | 9 | 4 | no | no | no | 2026-09-11 |
| StorageRouting | Admin | 44% | 4/9 | 1 | 6 | 4 | no | no | no | 2026-09-11 |
| Users | Admin | 67% | 6/9 | 0 | 7 | 4 | yes | yes | no | 2026-09-05 |
| Roles | Admin | 56% | 5/9 | 0 | 4 | 2 | yes | no | no | 2026-09-05 |
| RoutingRules | Admin | 67% | 6/9 | 0 | 3 | 1 | yes | no | no | 2026-09-05 |
| Settings (redirect shim) | Admin | 33% | 3/9 | 0 | 1 | 0 | yes | no | no | 2026-09-05 |
| Tag | - | 67% | 6/9 | 1 | 3 | 1 | yes | yes | no | 2026-09-05 |
| Task | - | 67% | 6/9 | 0 | 3 | 1 | yes | yes | no | 2026-09-05 |
| Team | - | 67% | 6/9 | 0 | 6 | 1 | yes | yes | no | 2026-09-08 |
| SparePart | - | 67% | 6/9 | 0 | 4 | 1 | yes | yes | no | 2026-09-05 |
| StoreRequisition | - | 67% | 6/9 | 0 | 7 | 2 | yes | yes | no | 2026-09-05 |
| TableExtraction | - | 67% | 6/9 | 0 | 9 | 3 | yes | yes | no | 2026-09-08 |
| TechnicalQuery | - | 67% | 6/9 | 0 | 8 | 3 | yes | yes | no | 2026-09-05 |
| VDRImport | - | 67% | 6/9 | 0 | 6 | 2 | yes | yes | no | 2026-09-08 |
| Vendor | - | 67% | 6/9 | 0 | 8 | 4 | yes | yes | no | 2026-09-14 |
| WebAuthn | - | 56% | 5/9 | 0 | 3 | 0 | no | yes | no | 2026-09-05 |
| Theme | - | 11% | 1/9 | 1 | 1 | 0 | no | no | no | 2026-09-14 |
| Transmittal | - | 67% | 6/9 | 0 | 9 | 3 | yes | yes | no | 2026-09-12 |
| VDR | - | 67% | 6/9 | 1 | 16 | 3 | yes | yes | no | 2026-09-14 |
| MaintenanceStrategy | - | 67% | 6/9 | 0 | 5 | 2 | yes | yes | no | 2026-09-14 |
| DCC | - | 56% | 5/9 | 1 | 3 | 2 | yes | yes | no | 2026-09-05 |
| Department | - | 56% | 5/9 | 0 | 6 | 1 | yes | no | no | 2026-09-05 |
| Discipline | - | 67% | 6/9 | 0 | 4 | 2 | yes | yes | no | 2026-09-08 |
| CriticalityAssessment | - | 67% | 6/9 | 0 | 10 | 4 | yes | yes | no | 2026-09-10 |
| Culture | - | 11% | 1/9 | 1 | 1 | 0 | no | no | no | 2026-09-05 |
| DashboardSettings | - | 44% | 4/9 | 1 | 3 | 1 | no | yes | no | 2026-09-05 |
| DocumentRegister | - | 56% | 5/9 | 1 | 5 | 1 | yes | yes | no | 2026-09-05 |
| Employee | - | 56% | 5/9 | 0 | 14 | 2 | yes | no | no | 2026-09-06 |
| EngineeringDashboard | - | 44% | 4/9 | 2 | 1 | 1 | yes | yes | no | 2026-09-05 |
| EngineeringDocumentIntelligence | - | 56% | 5/9 | 1 | 10 | 2 | yes | yes | no | 2026-09-08 |
| DocumentRevisions (redirect shim) | - | 44% | 4/9 | 0 | 2 | 2 | yes | no | no | 2026-09-06 |
| DocumentUpload (redirect shim) | - | 56% | 5/9 | 1 | 2 | 1 | yes | yes | no | 2026-09-05 |
| DocumentWorkflow | - | 67% | 6/9 | 0 | 4 | 1 | yes | yes | no | 2026-09-12 |
| AIInsights | - | 44% | 4/9 | 1 | 1 | 1 | yes | no | no | 2026-09-05 |
| AISearch | - | 33% | 3/9 | 2 | 1 | 1 | yes | no | no | 2026-09-05 |
| Asset | - | 67% | 6/9 | 0 | 5 | 2 | yes | yes | no | 2026-09-05 |
| Account | - | 44% | 4/9 | 0 | 9 | 5 | no | no | no | 2026-09-05 |
| AI | - | 33% | 3/9 | 2 | 1 | 0 | no | yes | no | 2026-09-05 |
| AIExtraction | - | 56% | 5/9 | 1 | 6 | 2 | yes | yes | no | 2026-09-08 |
| Barcode | - | 67% | 6/9 | 0 | 15 | 4 | yes | yes | no | 2026-09-15 |
| ComingSoon | - | 22% | 2/9 | 1 | 1 | 1 | no | no | no | 2026-09-05 |
| Contract | - | 56% | 5/9 | 0 | 5 | 3 | yes | no | no | 2026-09-06 |
| Crew | - | 67% | 6/9 | 0 | 4 | 1 | yes | yes | no | 2026-09-08 |
| ChatAssistant | - | 56% | 5/9 | 1 | 3 | 1 | yes | yes | no | 2026-09-05 |
| CmmsOperations | - | 56% | 5/9 | 1 | 2 | 2 | yes | yes | no | 2026-09-11 |
| CmmsReadiness | - | 56% | 5/9 | 1 | 2 | 2 | yes | yes | no | 2026-09-10 |
| EngineeringDocuments | - | 67% | 6/9 | 0 | 28 | 5 | yes | yes | no | 2026-09-14 |
| InventoryMasterData | - | 56% | 5/9 | 1 | 8 | 1 | yes | yes | no | 2026-09-08 |
| InventoryTransaction | - | 56% | 5/9 | 0 | 1 | 1 | yes | no | no | 2026-09-05 |
| Isolation | - | 44% | 4/9 | 0 | 5 | 0 | no | yes | no | 2026-09-05 |
| Instrument | - | 67% | 6/9 | 0 | 14 | 3 | yes | yes | no | 2026-09-05 |
| Intake | - | 56% | 5/9 | 0 | 4 | 2 | yes | yes | no | 2026-09-05 |
| Inventory | - | 56% | 5/9 | 0 | 1 | 1 | yes | no | no | 2026-09-05 |
| JobPlan | - | 67% | 6/9 | 0 | 10 | 2 | yes | yes | no | 2026-09-10 |
| MaintenanceHistoryImport | - | 67% | 6/9 | 0 | 6 | 2 | yes | yes | no | 2026-09-14 |
| MaintenanceMasterData | - | 56% | 5/9 | 0 | 21 | 1 | yes | yes | no | 2026-09-09 |
| MaintenancePlan | - | 67% | 6/9 | 0 | 7 | 2 | yes | yes | no | 2026-09-10 |
| KnowledgeBase | - | 67% | 6/9 | 0 | 1 | 1 | yes | yes | no | 2026-09-05 |
| Kpi | - | 56% | 5/9 | 1 | 1 | 1 | yes | yes | no | 2026-09-11 |
| Letter | - | 67% | 6/9 | 0 | 28 | 5 | yes | yes | no | 2026-09-13 |
| EquipmentFunctionalLocationAssignment | - | 67% | 6/9 | 0 | 7 | 2 | yes | yes | no | 2026-09-14 |
| EquipmentImport | - | 67% | 6/9 | 0 | 8 | 3 | yes | yes | no | 2026-09-08 |
| ExecutionGate (redirect shim) | - | 44% | 4/9 | 0 | 2 | 0 | no | yes | no | 2026-09-05 |
| EquipmentClassification | - | 67% | 6/9 | 0 | 5 | 3 | yes | yes | no | 2026-09-09 |
| Equipment | - | 67% | 6/9 | 0 | 5 | 3 | yes | yes | no | 2026-09-15 |
| EquipmentCriticality (redirect shim) | - | 67% | 6/9 | 0 | 2 | 1 | yes | yes | no | 2026-09-06 |
| Files | - | 33% | 3/9 | 0 | 1 | 0 | no | yes | no | 2026-09-05 |
| GlobalSearch | - | 22% | 2/9 | 1 | 1 | 1 | no | no | no | 2026-09-05 |
| Home | - | 33% | 3/9 | 1 | 3 | 2 | yes | no | no | 2026-09-14 |
| Inspection | - | 67% | 6/9 | 0 | 3 | 1 | yes | yes | no | 2026-09-05 |
| FunctionalLocation | - | 67% | 6/9 | 0 | 3 | 1 | yes | yes | no | 2026-09-05 |
| FunctionalLocationImport | - | 67% | 6/9 | 0 | 6 | 2 | yes | yes | no | 2026-09-11 |
| GasTest (redirect shim) | - | 44% | 4/9 | 0 | 2 | 0 | no | yes | no | 2026-09-05 |

## Detailed evidence checklist per module

### Projects 

- Controller: `PEMS.Web/Controllers/ProjectsController.cs`
- Injected services: IProjectService
- Views path checked: `PEMS.Web/Views/Projects` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### QC 

- Controller: `PEMS.Web/Controllers/QCController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/QC` (8 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### QuickSetup 

- Controller: `PEMS.Web/Controllers/QuickSetupController.cs`
- Injected services: ISetupService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/QuickSetup` (1 .cshtml files)
- Git: last touched 2026-09-08, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### Production 

- Controller: `PEMS.Web/Controllers/ProductionController.cs`
- Injected services: IProductionOrderService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Production` (1 .cshtml files)
- Git: last touched unknown, 0 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ProductionOrder 

- Controller: `PEMS.Web/Controllers/ProductionOrderController.cs`
- Injected services: IProductionOrderService, ICurrentUserService, IPermissionService, IAccessControlService
- Views path checked: `PEMS.Web/Views/ProductionOrder` (3 .cshtml files)
- Git: last touched unknown, 0 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ProductionRun 

- Controller: `PEMS.Web/Controllers/ProductionRunController.cs`
- Injected services: IProductionRunService, ICurrentUserService, IPermissionService, IAccessControlService
- Views path checked: `PEMS.Web/Views/ProductionRun` (3 .cshtml files)
- Git: last touched unknown, 0 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### RcmFmea 

- Controller: `PEMS.Web/Controllers/RcmFmeaController.cs`
- Injected services: IRcmFmeaReadinessService, IFMEAService, IRCMService, IRcmOperationalBridgeService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/RcmFmea` (2 .cshtml files)
- Git: last touched 2026-09-10, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### SearchApi 

- Controller: `PEMS.Web/Controllers/SearchApiController.cs`
- Injected services: IGlobalSearchService
- Views path checked: `PEMS.Web/Views/SearchApi` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Settings 

- Controller: `PEMS.Web/Controllers/SettingsController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/Settings` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ShiftHandover 

- Controller: `PEMS.Web/Controllers/ShiftHandoverController.cs`
- Injected services: IShiftHandoverService, IRoleModuleActionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/ShiftHandover` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Relationship 

- Controller: `PEMS.Web/Controllers/RelationshipController.cs`
- Injected services: IRelationshipService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Relationship` (3 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Reliability 

- Controller: `PEMS.Web/Controllers/ReliabilityController.cs`
- Injected services: IReliabilityService, IReliabilityAnalyticsService, IReliabilityDataQualityService, IReliabilityIntelligenceService, IRcmFeedbackService, ICurrentUserService, IPermissionService, IRamWeibullService, IReliabilitySimulationService, IConditionMonitoringService, IPredictiveMaintenanceService, IAIReliabilityInsightService, IClassIntelligenceService, IEquipmentIntelligenceService
- Views path checked: `PEMS.Web/Views/Reliability` (14 .cshtml files)
- Git: last touched 2026-09-11, 4 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Report 

- Controller: `PEMS.Web/Controllers/ReportController.cs`
- Injected services: ICorrespondenceReportService, IEngineeringReportService, IVDRReportService, ICmmsReportService, IProcurementReportService
- Views path checked: `PEMS.Web/Views/Report` (5 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### MDRImport 

- Controller: `PEMS.Web/Controllers/MDRImportController.cs`
- Injected services: IMDRImportService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/MDRImport` (2 .cshtml files)
- Git: last touched 2026-09-08, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Meeting 

- Controller: `PEMS.Web/Controllers/MeetingController.cs`
- Injected services: IMeetingService, ITaskService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Meeting` (3 .cshtml files)
- Git: last touched 2026-09-06, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MOC 

- Controller: `PEMS.Web/Controllers/MOCController.cs`
- Injected services: IMOCService, ICurrentUserService, IFileStorageService
- Views path checked: `PEMS.Web/Views/MOC` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaterialItem 

- Controller: `PEMS.Web/Controllers/MaterialItemController.cs`
- Injected services: IMaterialItemService, IMaterialStockService, ICurrentUserService, IPermissionService, IBarcodeService
- Views path checked: `PEMS.Web/Views/MaterialItem` (2 .cshtml files)
- Git: last touched 2026-09-15, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaterialRequisition 

- Controller: `PEMS.Web/Controllers/MaterialRequisitionController.cs`
- Injected services: IMaterialRequisitionService, IPermissionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/MaterialRequisition` (4 .cshtml files)
- Git: last touched 2026-09-05, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MDB 

- Controller: `PEMS.Web/Controllers/MDBController.cs`
- Injected services: IManufacturerDataBookService
- Views path checked: `PEMS.Web/Views/MDB` (3 .cshtml files)
- Git: last touched 2026-09-06, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### MrsImport 

- Controller: `PEMS.Web/Controllers/MrsImportController.cs`
- Injected services: IMrsImportService, ICurrentUserService, IFileStorageService, IPermissionService
- Views path checked: `PEMS.Web/Views/MrsImport` (2 .cshtml files)
- Git: last touched 2026-09-08, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Performance 

- Controller: `PEMS.Web/Controllers/PerformanceController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Performance` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### Permit 

- Controller: `PEMS.Web/Controllers/PermitController.cs`
- Injected services: IPermitService, IRoleModuleActionService, ICurrentUserService, IFunctionalLocationService, IIsolationService, IGasTestService, ISimopsService, IExecutionGateService, IPermitAuthorityService
- Views path checked: `PEMS.Web/Views/Permit` (3 .cshtml files)
- Git: last touched 2026-09-05, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ProcurementPackage 

- Controller: `PEMS.Web/Controllers/ProcurementPackageController.cs`
- Injected services: IProcurementPackageService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/ProcurementPackage` (3 .cshtml files)
- Git: last touched 2026-09-06, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MyProfile 

- Controller: `PEMS.Web/Controllers/MyProfileController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/MyProfile` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### Notification 

- Controller: `PEMS.Web/Controllers/NotificationController.cs`
- Injected services: INotificationService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Notification` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### OCRProcessing 

- Controller: `PEMS.Web/Controllers/OCRProcessingController.cs`
- Injected services: ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/OCRProcessing` (1 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Simops 

- Controller: `PEMS.Web/Controllers/SimopsController.cs`
- Injected services: ISimopsService, IRoleModuleActionService, ICurrentUserService, IPermitAuthorityService
- Views path checked: `PEMS.Web/Views/Simops` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Department (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/DepartmentController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Department` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### Employee (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/EmployeeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Employee` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### PermissionMatrix (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/PermissionMatrixController.cs`
- Injected services: IPermissionService
- Views path checked: `PEMS.Web/Areas/Admin/Views/PermissionMatrix` (1 .cshtml files)
- Git: last touched 2026-09-08, 7 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### WorkOrder 

- Controller: `PEMS.Web/Controllers/WorkOrderController.cs`
- Injected services: IWorkOrderReviewService, ICurrentUserService, IPermissionService, IWorkOrderLaborService, IWorkOrderLifecycleService, ICrewService, INumberingService, IMaterialStockService
- Views path checked: `PEMS.Web/Views/WorkOrder` (3 .cshtml files)
- Git: last touched 2026-09-11, 4 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### WorkRequest 

- Controller: `PEMS.Web/Controllers/WorkRequestController.cs`
- Injected services: IWorkRequestService, IPermissionService, ICurrentUserService, IFunctionalLocationService
- Views path checked: `PEMS.Web/Views/WorkRequest` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### AuditLog (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/AuditLogController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/AuditLog` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### ProjectIntelligence (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/ProjectIntelligenceController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/ProjectIntelligence` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### StorageProviders (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/StorageProvidersController.cs`
- Injected services: IStorageProviderService
- Views path checked: `PEMS.Web/Areas/Admin/Views/StorageProviders` (4 .cshtml files)
- Git: last touched 2026-09-11, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### StorageRouting (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/StorageRoutingController.cs`
- Injected services: IStorageRouteService, IStorageProviderService
- Views path checked: `PEMS.Web/Areas/Admin/Views/StorageRouting` (4 .cshtml files)
- Git: last touched 2026-09-11, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### Users (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/UsersController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Users` (4 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### Roles (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/RolesController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Roles` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### RoutingRules (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/RoutingRulesController.cs`
- Injected services: IRoutingRuleService
- Views path checked: `PEMS.Web/Areas/Admin/Views/RoutingRules` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### Settings (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/SettingsController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Settings` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Tag 

- Controller: `PEMS.Web/Controllers/TagController.cs`
- Injected services: ITagService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Tag` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Task 

- Controller: `PEMS.Web/Controllers/TaskController.cs`
- Injected services: ITaskService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Task` (1 .cshtml files)
- Git: last touched 2026-09-05, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Team 

- Controller: `PEMS.Web/Controllers/TeamController.cs`
- Injected services: ITeamService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Team` (1 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### SparePart 

- Controller: `PEMS.Web/Controllers/SparePartController.cs`
- Injected services: ISparePartService
- Views path checked: `PEMS.Web/Views/SparePart` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### StoreRequisition 

- Controller: `PEMS.Web/Controllers/StoreRequisitionController.cs`
- Injected services: IStoreRequisitionLifecycleService, ICurrentUserService, IPermissionService, INumberingService
- Views path checked: `PEMS.Web/Views/StoreRequisition` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### TableExtraction 

- Controller: `PEMS.Web/Controllers/TableExtractionController.cs`
- Injected services: IDocumentAIService, ITableExtractionImportService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/TableExtraction` (3 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### TechnicalQuery 

- Controller: `PEMS.Web/Controllers/TechnicalQueryController.cs`
- Injected services: ITechnicalQueryService, ICurrentUserService, IFileStorageService
- Views path checked: `PEMS.Web/Views/TechnicalQuery` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### VDRImport 

- Controller: `PEMS.Web/Controllers/VDRImportController.cs`
- Injected services: IVDRImportService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/VDRImport` (2 .cshtml files)
- Git: last touched 2026-09-08, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Vendor 

- Controller: `PEMS.Web/Controllers/VendorController.cs`
- Injected services: IVendorService, IVendorRiskService, IPermissionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Vendor` (4 .cshtml files)
- Git: last touched 2026-09-14, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### WebAuthn 

- Controller: `PEMS.Web/Controllers/WebAuthnController.cs`
- Injected services: IWebAuthnService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/WebAuthn` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Present |
| Documented | Missing |

### Theme 

- Controller: `PEMS.Web/Controllers/ThemeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Theme` (0 .cshtml files)
- Git: last touched 2026-09-14, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Transmittal 

- Controller: `PEMS.Web/Controllers/TransmittalController.cs`
- Injected services: ITransmittalService, IPermissionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Transmittal` (3 .cshtml files)
- Git: last touched 2026-09-12, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### VDR 

- Controller: `PEMS.Web/Controllers/VDRController.cs`
- Injected services: IVDRService, IDocumentCommentService, IVdrWorkflowService, ILetterService, ICurrentUserService, IVdrDocumentNumberingService, IFtpUploadService, IFileStorageService, IPermissionService
- Views path checked: `PEMS.Web/Views/VDR` (3 .cshtml files)
- Git: last touched 2026-09-14, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaintenanceStrategy 

- Controller: `PEMS.Web/Controllers/MaintenanceStrategyController.cs`
- Injected services: IMaintenanceStrategyService, IFunctionalLocationService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/MaintenanceStrategy` (2 .cshtml files)
- Git: last touched 2026-09-14, 4 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### DCC 

- Controller: `PEMS.Web/Controllers/DCCController.cs`
- Injected services: ILetterService, ILetterNumberingService, ICurrentUserService, IFileStorageService
- Views path checked: `PEMS.Web/Views/DCC` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Department 

- Controller: `PEMS.Web/Controllers/DepartmentController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Department` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### Discipline 

- Controller: `PEMS.Web/Controllers/DisciplineController.cs`
- Injected services: IDisciplineWorkspaceService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Discipline` (2 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### CriticalityAssessment 

- Controller: `PEMS.Web/Controllers/CriticalityAssessmentController.cs`
- Injected services: ICriticalityAssessmentService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/CriticalityAssessment` (4 .cshtml files)
- Git: last touched 2026-09-10, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Culture 

- Controller: `PEMS.Web/Controllers/CultureController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Culture` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | AllowAnonymous |
| TestCoverage | Missing |
| Documented | Missing |

### DashboardSettings 

- Controller: `PEMS.Web/Controllers/DashboardSettingsController.cs`
- Injected services: IUserDashboardService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DashboardSettings` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### DocumentRegister 

- Controller: `PEMS.Web/Controllers/DocumentRegisterController.cs`
- Injected services: IEngineeringDocumentService, IExcelExportService, ISavedFilterService, ICurrentUserService, IAccessControlService
- Views path checked: `PEMS.Web/Views/DocumentRegister` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Employee 

- Controller: `PEMS.Web/Controllers/EmployeeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Employee` (2 .cshtml files)
- Git: last touched 2026-09-06, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### EngineeringDashboard 

- Controller: `PEMS.Web/Controllers/EngineeringDashboardController.cs`
- Injected services: IEngineeringDocumentService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/EngineeringDashboard` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EngineeringDocumentIntelligence 

- Controller: `PEMS.Web/Controllers/EngineeringDocumentIntelligenceController.cs`
- Injected services: IEngineeringDocumentIntelligenceService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/EngineeringDocumentIntelligence` (2 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### DocumentRevisions 

- Controller: `PEMS.Web/Controllers/DocumentRevisionsController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/DocumentRevisions` (2 .cshtml files)
- Git: last touched 2026-09-06, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### DocumentUpload 

- Controller: `PEMS.Web/Controllers/DocumentUploadController.cs`
- Injected services: IIntakeService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DocumentUpload` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### DocumentWorkflow 

- Controller: `PEMS.Web/Controllers/DocumentWorkflowController.cs`
- Injected services: IDocumentWorkflowService, ICurrentUserService, IPermissionService, IAccessControlService
- Views path checked: `PEMS.Web/Views/DocumentWorkflow` (1 .cshtml files)
- Git: last touched 2026-09-12, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### AIInsights 

- Controller: `PEMS.Web/Controllers/AIInsightsController.cs`
- Injected services: IEngineeringCopilotService
- Views path checked: `PEMS.Web/Views/AIInsights` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### AISearch 

- Controller: `PEMS.Web/Controllers/AISearchController.cs`
- Injected services: IDocumentSearchAIService
- Views path checked: `PEMS.Web/Views/AISearch` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Asset 

- Controller: `PEMS.Web/Controllers/AssetController.cs`
- Injected services: IAssetService, ICurrentUserService, IFunctionalLocationService
- Views path checked: `PEMS.Web/Views/Asset` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Account 

- Controller: `PEMS.Web/Controllers/AccountController.cs`
- Injected services: IWebAuthnService
- Views path checked: `PEMS.Web/Views/Account` (5 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Missing |

### AI 

- Controller: `PEMS.Web/Controllers/AIController.cs`
- Injected services: IOllamaService
- Views path checked: `PEMS.Web/Views/AI` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Unknown |
| EntityOrDbSet | Present |
| MigrationBacked | Unknown |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### AIExtraction 

- Controller: `PEMS.Web/Controllers/AIExtractionController.cs`
- Injected services: IDocumentAIIngestionService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/AIExtraction` (2 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Barcode 

- Controller: `PEMS.Web/Controllers/BarcodeController.cs`
- Injected services: IBarcodeService, IBarcodeRenderingService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Barcode` (4 .cshtml files)
- Git: last touched 2026-09-15, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ComingSoon 

- Controller: `PEMS.Web/Controllers/ComingSoonController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/ComingSoon` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Contract 

- Controller: `PEMS.Web/Controllers/ContractController.cs`
- Injected services: IContractService
- Views path checked: `PEMS.Web/Views/Contract` (3 .cshtml files)
- Git: last touched 2026-09-06, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Crew 

- Controller: `PEMS.Web/Controllers/CrewController.cs`
- Injected services: ICrewService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Crew` (1 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ChatAssistant 

- Controller: `PEMS.Web/Controllers/ChatAssistantController.cs`
- Injected services: IChatAssistantService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/ChatAssistant` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### CmmsOperations 

- Controller: `PEMS.Web/Controllers/CmmsOperationsController.cs`
- Injected services: ICmmsOperationsService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/CmmsOperations` (2 .cshtml files)
- Git: last touched 2026-09-11, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### CmmsReadiness 

- Controller: `PEMS.Web/Controllers/CmmsReadinessController.cs`
- Injected services: IEquipmentReadinessService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/CmmsReadiness` (2 .cshtml files)
- Git: last touched 2026-09-10, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EngineeringDocuments 

- Controller: `PEMS.Web/Controllers/EngineeringDocumentsController.cs`
- Injected services: IEngineeringDocumentService, IDocumentWorkflowService
- Views path checked: `PEMS.Web/Views/EngineeringDocuments` (5 .cshtml files)
- Git: last touched 2026-09-14, 5 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### InventoryMasterData 

- Controller: `PEMS.Web/Controllers/InventoryMasterDataController.cs`
- Injected services: ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/InventoryMasterData` (1 .cshtml files)
- Git: last touched 2026-09-08, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### InventoryTransaction 

- Controller: `PEMS.Web/Controllers/InventoryTransactionController.cs`
- Injected services: IInventoryTransactionService
- Views path checked: `PEMS.Web/Views/InventoryTransaction` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Isolation 

- Controller: `PEMS.Web/Controllers/IsolationController.cs`
- Injected services: IIsolationService, IRoleModuleActionService, ICurrentUserService, IPermitAuthorityService
- Views path checked: `PEMS.Web/Views/Isolation` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Instrument 

- Controller: `PEMS.Web/Controllers/InstrumentController.cs`
- Injected services: INumberingService, IWorkRequestService, ICurrentUserService, IInstrumentService
- Views path checked: `PEMS.Web/Views/Instrument` (3 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Intake 

- Controller: `PEMS.Web/Controllers/IntakeController.cs`
- Injected services: IIntakeService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Intake` (2 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Inventory 

- Controller: `PEMS.Web/Controllers/InventoryController.cs`
- Injected services: IMaterialStockService
- Views path checked: `PEMS.Web/Views/Inventory` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### JobPlan 

- Controller: `PEMS.Web/Controllers/JobPlanController.cs`
- Injected services: IJobPlanService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/JobPlan` (2 .cshtml files)
- Git: last touched 2026-09-10, 4 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaintenanceHistoryImport 

- Controller: `PEMS.Web/Controllers/MaintenanceHistoryImportController.cs`
- Injected services: IMaintenanceHistoryImportService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/MaintenanceHistoryImport` (2 .cshtml files)
- Git: last touched 2026-09-14, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaintenanceMasterData 

- Controller: `PEMS.Web/Controllers/MaintenanceMasterDataController.cs`
- Injected services: ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/MaintenanceMasterData` (1 .cshtml files)
- Git: last touched 2026-09-09, 4 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### MaintenancePlan 

- Controller: `PEMS.Web/Controllers/MaintenancePlanController.cs`
- Injected services: INumberingService, IMaintenancePlanService, IPMGenerationService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/MaintenancePlan` (2 .cshtml files)
- Git: last touched 2026-09-10, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### KnowledgeBase 

- Controller: `PEMS.Web/Controllers/KnowledgeBaseController.cs`
- Injected services: IKnowledgeBaseService
- Views path checked: `PEMS.Web/Views/KnowledgeBase` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Kpi 

- Controller: `PEMS.Web/Controllers/KpiController.cs`
- Injected services: IKpiService, IReliabilityService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/Kpi` (1 .cshtml files)
- Git: last touched 2026-09-11, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Letter 

- Controller: `PEMS.Web/Controllers/LetterController.cs`
- Injected services: ILetterService, ILetterReferralService, ICurrentUserService, IPermissionService, ITagService, IRichTextSanitizerService, ICacheService, ILetterNumberingService, IFileStorageService
- Views path checked: `PEMS.Web/Views/Letter` (5 .cshtml files)
- Git: last touched 2026-09-13, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EquipmentFunctionalLocationAssignment 

- Controller: `PEMS.Web/Controllers/EquipmentFunctionalLocationAssignmentController.cs`
- Injected services: IEquipmentFunctionalLocationAssignmentService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/EquipmentFunctionalLocationAssignment` (2 .cshtml files)
- Git: last touched 2026-09-14, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EquipmentImport 

- Controller: `PEMS.Web/Controllers/EquipmentImportController.cs`
- Injected services: IEquipmentImportService, IEquipmentPdfScanService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/EquipmentImport` (3 .cshtml files)
- Git: last touched 2026-09-08, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### ExecutionGate 

- Controller: `PEMS.Web/Controllers/ExecutionGateController.cs`
- Injected services: IExecutionGateService, IRoleModuleActionService, ICurrentUserService, IPermitAuthorityService
- Views path checked: `PEMS.Web/Views/ExecutionGate` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EquipmentClassification 

- Controller: `PEMS.Web/Controllers/EquipmentClassificationController.cs`
- Injected services: IEquipmentClassificationService, IEquipmentClassCoverageService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/EquipmentClassification` (3 .cshtml files)
- Git: last touched 2026-09-09, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Equipment 

- Controller: `PEMS.Web/Controllers/EquipmentController.cs`
- Injected services: IEquipmentService, IFunctionalLocationService, ICurrentUserService, IReliabilityService, IInspectionService, IEquipmentClassificationService, IMaintenanceIntelligenceService, IPermissionService, IBarcodeService
- Views path checked: `PEMS.Web/Views/Equipment` (3 .cshtml files)
- Git: last touched 2026-09-15, 5 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### EquipmentCriticality 

- Controller: `PEMS.Web/Controllers/EquipmentCriticalityController.cs`
- Injected services: IPermissionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/EquipmentCriticality` (1 .cshtml files)
- Git: last touched 2026-09-06, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### Files 

- Controller: `PEMS.Web/Controllers/FilesController.cs`
- Injected services: ICurrentUserService, IAccessControlService, IFileStorageService
- Views path checked: `PEMS.Web/Views/Files` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Missing |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### GlobalSearch 

- Controller: `PEMS.Web/Controllers/GlobalSearchController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/GlobalSearch` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Home 

- Controller: `PEMS.Web/Controllers/HomeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Home` (2 .cshtml files)
- Git: last touched 2026-09-14, 3 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Missing |

### Inspection 

- Controller: `PEMS.Web/Controllers/InspectionController.cs`
- Injected services: IInspectionService, IWorkRequestService, IPermissionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Inspection` (1 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### FunctionalLocation 

- Controller: `PEMS.Web/Controllers/FunctionalLocationController.cs`
- Injected services: IFunctionalLocationService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/FunctionalLocation` (1 .cshtml files)
- Git: last touched 2026-09-05, 2 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### FunctionalLocationImport 

- Controller: `PEMS.Web/Controllers/FunctionalLocationImportController.cs`
- Injected services: IFunctionalLocationImportService, ICurrentUserService, IPermissionService
- Views path checked: `PEMS.Web/Views/FunctionalLocationImport` (2 .cshtml files)
- Git: last touched 2026-09-11, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Present |
| SidebarReachable | Present |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

### GasTest 

- Controller: `PEMS.Web/Controllers/GasTestController.cs`
- Injected services: IGasTestService, IRoleModuleActionService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/GasTest` (0 .cshtml files)
- Git: last touched 2026-09-05, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Present |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Present |
| Documented | Missing |

## Non-negotiable reminders for anyone acting on this report

1. A green checklist item means evidence was found in the repo - it does not mean the
   feature was manually verified working end-to-end. Distinguish *Implemented* from *Verified*.
2. dotnet build succeeding is a health indicator, not a completion signal.
3. Zero views or zero nav links can be entirely correct for API/AJAX-only controllers -
   confirm by reading the controller before treating it as unfinished.
4. Re-run this generator after any structural change: `pwsh scripts/project-intelligence/Generate-ProjectIntelligence.ps1`

