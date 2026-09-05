# PEMS Project Intelligence Report

Generated: 2026-08-14T03:32:27.6970228Z - Branch: `master` - Last commit: `159cfd4 2026-08-13 cluade_AI_CHECK`

This report is generated evidence, not a manual estimate. Each module below is a
controller found in the repository; each signal is checked directly against source,
EF configuration, migrations, views, sidebar navigation, tests, docs, and git history.
Evidence coverage % = (signals confirmed Present) / (signals checked). It is a
mechanical ratio over the checklist below it, not an estimate of feature completeness -
read the checklist, not just the percentage. Unknown means the scan could not confirm
either way (never treat Unknown as Missing).

## Summary

- Controllers scanned (candidate modules): **67**
- Redirect-shim controllers (no real logic, forward elsewhere): **9**
- Controllers with zero Razor views found: **5** (may be legitimate JSON/API controllers - verify before assuming broken)
- Controllers not linked from the sidebar menu: **13** (may be reachable via deep link, drilldown, or another page)
- Controllers with no reference found in PEMS.Tests: **67**

## Modules (sorted by evidence coverage, lowest first)

| Module | Area | Coverage | Present/Total | Unknown | Actions | Views | Nav | Tests | Docs | Last touched |
|---|---|---|---|---|---|---|---|---|---|---|
| Settings | - | 56% | 5/9 | 1 | 1 | 1 | yes | no | yes | 2026-08-12 |
| SparePart | - | 56% | 5/9 | 0 | 4 | 1 | yes | no | yes | 2026-08-12 |
| Report | - | 67% | 6/9 | 0 | 5 | 5 | yes | no | yes | 2026-08-12 |
| SearchApi | - | 22% | 2/9 | 1 | 2 | 0 | no | no | no | 2026-08-12 |
| Task | - | 67% | 6/9 | 0 | 3 | 1 | yes | no | yes | 2026-08-12 |
| Team | - | 56% | 5/9 | 0 | 6 | 1 | yes | no | no | 2026-08-12 |
| TableExtraction | - | 56% | 5/9 | 0 | 9 | 3 | yes | no | no | - |
| Tag | - | 67% | 6/9 | 1 | 3 | 1 | yes | no | yes | 2026-08-12 |
| Notification | - | 56% | 5/9 | 0 | 4 | 2 | no | no | yes | 2026-08-12 |
| OCRProcessing (redirect shim) | - | 44% | 4/9 | 1 | 2 | 1 | yes | no | yes | 2026-08-12 |
| MOC | - | 67% | 6/9 | 1 | 12 | 3 | yes | no | yes | 2026-08-12 |
| MyProfile | - | 33% | 3/9 | 2 | 3 | 1 | no | no | no | 2026-08-12 |
| QC | - | 44% | 4/9 | 2 | 17 | 8 | yes | no | no | 2026-08-12 |
| Relationship | - | 67% | 6/9 | 0 | 9 | 3 | yes | no | yes | 2026-08-12 |
| ProcurementPackage | - | 67% | 6/9 | 0 | 11 | 3 | yes | no | yes | 2026-08-12 |
| Projects | - | 67% | 6/9 | 0 | 6 | 3 | yes | no | yes | 2026-08-12 |
| TechnicalQuery | - | 56% | 5/9 | 0 | 8 | 3 | yes | no | no | 2026-08-12 |
| PermissionMatrix (redirect shim) | Admin | 56% | 5/9 | 1 | 2 | 1 | yes | no | no | 2026-08-13 |
| ProjectIntelligence (redirect shim) | Admin | 44% | 4/9 | 1 | 2 | 1 | yes | no | no | 2026-08-13 |
| Department (redirect shim) | Admin | 56% | 5/9 | 0 | 1 | 0 | yes | no | yes | 2026-08-13 |
| Employee (redirect shim) | Admin | 56% | 5/9 | 0 | 1 | 0 | yes | no | yes | 2026-08-13 |
| Settings (redirect shim) | Admin | 44% | 4/9 | 0 | 1 | 0 | yes | no | yes | 2026-08-12 |
| Users | Admin | 67% | 6/9 | 0 | 7 | 4 | yes | no | yes | 2026-08-13 |
| Roles | Admin | 67% | 6/9 | 0 | 4 | 2 | yes | no | yes | 2026-08-13 |
| RoutingRules | Admin | 78% | 7/9 | 0 | 3 | 1 | yes | no | yes | 2026-08-12 |
| User | - | 33% | 3/9 | 0 | 0 | 0 | no | no | yes | 2026-08-12 |
| VDR | - | 67% | 6/9 | 1 | 13 | 3 | yes | no | yes | 2026-08-12 |
| Theme | - | 22% | 2/9 | 1 | 1 | 0 | no | no | yes | 2026-08-12 |
| Transmittal | - | 67% | 6/9 | 0 | 5 | 3 | yes | no | yes | 2026-08-12 |
| WorkOrder | - | 67% | 6/9 | 0 | 9 | 3 | yes | no | yes | 2026-08-12 |
| AuditLog | Admin | 67% | 6/9 | 0 | 1 | 1 | yes | no | yes | 2026-08-12 |
| VDRImport | - | 56% | 5/9 | 0 | 6 | 2 | no | no | yes | 2026-08-12 |
| Vendor | - | 67% | 6/9 | 0 | 6 | 3 | yes | no | yes | 2026-08-12 |
| Meeting | - | 67% | 6/9 | 0 | 8 | 3 | yes | no | yes | 2026-08-12 |
| DCC | - | 44% | 4/9 | 1 | 3 | 2 | yes | no | no | 2026-08-12 |
| Department | - | 67% | 6/9 | 0 | 6 | 1 | yes | no | yes | 2026-08-13 |
| Culture | - | 22% | 2/9 | 1 | 1 | 0 | no | no | yes | 2026-08-13 |
| DashboardSettings | - | 33% | 3/9 | 1 | 3 | 1 | no | no | no | 2026-08-12 |
| DocumentRevisions (redirect shim) | - | 44% | 4/9 | 0 | 2 | 2 | yes | no | no | 2026-08-12 |
| DocumentUpload (redirect shim) | - | 56% | 5/9 | 1 | 2 | 1 | yes | no | yes | 2026-08-12 |
| Discipline | - | 67% | 6/9 | 0 | 4 | 2 | yes | no | yes | 2026-08-12 |
| DocumentRegister | - | 56% | 5/9 | 1 | 5 | 1 | yes | no | yes | 2026-08-12 |
| AIExtraction | - | 44% | 4/9 | 1 | 6 | 2 | yes | no | no | 2026-08-12 |
| AIInsights | - | 44% | 4/9 | 1 | 1 | 1 | yes | no | no | 2026-08-12 |
| Account | - | 44% | 4/9 | 1 | 8 | 4 | no | no | yes | 2026-08-12 |
| AI | - | 22% | 2/9 | 2 | 1 | 0 | no | no | no | 2026-08-12 |
| ComingSoon | - | 22% | 2/9 | 1 | 1 | 1 | no | no | no | 2026-08-12 |
| Contract | - | 67% | 6/9 | 0 | 5 | 3 | yes | no | yes | 2026-08-12 |
| AISearch | - | 33% | 3/9 | 2 | 1 | 1 | yes | no | no | 2026-08-12 |
| ChatAssistant | - | 56% | 5/9 | 1 | 3 | 1 | yes | no | yes | 2026-08-12 |
| DocumentWorkflow | - | 67% | 6/9 | 0 | 4 | 1 | yes | no | yes | 2026-08-12 |
| KnowledgeBase | - | 67% | 6/9 | 0 | 1 | 1 | yes | no | yes | 2026-08-12 |
| Letter | - | 67% | 6/9 | 0 | 21 | 5 | yes | no | yes | 2026-08-12 |
| Instrument | - | 56% | 5/9 | 0 | 10 | 3 | yes | no | yes | 2026-08-13 |
| Intake | - | 56% | 5/9 | 0 | 4 | 2 | yes | no | yes | 2026-08-12 |
| MaterialRequisition | - | 56% | 5/9 | 0 | 8 | 4 | no | no | yes | 2026-08-12 |
| MDRImport | - | 67% | 6/9 | 0 | 8 | 2 | yes | no | yes | 2026-08-12 |
| MaintenancePlan | - | 56% | 5/9 | 0 | 6 | 2 | yes | no | yes | 2026-08-12 |
| MaintenanceStrategy | - | 56% | 5/9 | 0 | 5 | 2 | yes | no | yes | 2026-08-12 |
| EngineeringDocuments | - | 67% | 6/9 | 0 | 23 | 5 | yes | no | yes | 2026-08-12 |
| Equipment | - | 67% | 6/9 | 0 | 4 | 3 | yes | no | yes | 2026-08-12 |
| Employee | - | 67% | 6/9 | 0 | 8 | 2 | yes | no | yes | 2026-08-13 |
| EngineeringDocumentIntelligence | - | 56% | 5/9 | 1 | 10 | 2 | yes | no | yes | 2026-08-12 |
| GlobalSearch | - | 33% | 3/9 | 1 | 1 | 1 | no | no | yes | 2026-08-12 |
| Home | - | 44% | 4/9 | 1 | 3 | 2 | yes | no | yes | 2026-08-12 |
| EquipmentCriticality (redirect shim) | - | 44% | 4/9 | 0 | 2 | 1 | yes | no | no | 2026-08-12 |
| EquipmentImport | - | 67% | 6/9 | 0 | 8 | 3 | yes | no | yes | 2026-08-12 |

## Detailed evidence checklist per module

### Settings 

- Controller: `PEMS.Web/Controllers/SettingsController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/Settings` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Present |

### SparePart 

- Controller: `PEMS.Web/Controllers/SparePartController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/SparePart` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Report 

- Controller: `PEMS.Web/Controllers/ReportController.cs`
- Injected services: ICorrespondenceReportService, IEngineeringReportService, IVDRReportService, ICmmsReportService, IProcurementReportService
- Views path checked: `PEMS.Web/Views/Report` (5 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### SearchApi 

- Controller: `PEMS.Web/Controllers/SearchApiController.cs`
- Injected services: IGlobalSearchService
- Views path checked: `PEMS.Web/Views/SearchApi` (0 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### Task 

- Controller: `PEMS.Web/Controllers/TaskController.cs`
- Injected services: ITaskService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Task` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Team 

- Controller: `PEMS.Web/Controllers/TeamController.cs`
- Injected services: ITeamService
- Views path checked: `PEMS.Web/Views/Team` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### TableExtraction 

- Controller: `PEMS.Web/Controllers/TableExtractionController.cs`
- Injected services: IDocumentAIService, ITableExtractionImportService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/TableExtraction` (3 .cshtml files)
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
| TestCoverage | Missing |
| Documented | Missing |

### Tag 

- Controller: `PEMS.Web/Controllers/TagController.cs`
- Injected services: ITagService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Tag` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Notification 

- Controller: `PEMS.Web/Controllers/NotificationController.cs`
- Injected services: INotificationService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Notification` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Present |

### OCRProcessing 

- Controller: `PEMS.Web/Controllers/OCRProcessingController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/OCRProcessing` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### MOC 

- Controller: `PEMS.Web/Controllers/MOCController.cs`
- Injected services: IMOCService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/MOC` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### MyProfile 

- Controller: `PEMS.Web/Controllers/MyProfileController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/MyProfile` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Missing |

### QC 

- Controller: `PEMS.Web/Controllers/QCController.cs`
- Injected services: ICurrentUserService
- Views path checked: `PEMS.Web/Views/QC` (8 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Missing |

### Relationship 

- Controller: `PEMS.Web/Controllers/RelationshipController.cs`
- Injected services: IRelationshipService
- Views path checked: `PEMS.Web/Views/Relationship` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### ProcurementPackage 

- Controller: `PEMS.Web/Controllers/ProcurementPackageController.cs`
- Injected services: IProcurementPackageService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/ProcurementPackage` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Projects 

- Controller: `PEMS.Web/Controllers/ProjectsController.cs`
- Injected services: IProjectService
- Views path checked: `PEMS.Web/Views/Projects` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### TechnicalQuery 

- Controller: `PEMS.Web/Controllers/TechnicalQueryController.cs`
- Injected services: ITechnicalQueryService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/TechnicalQuery` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### PermissionMatrix (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/PermissionMatrixController.cs`
- Injected services: IPermissionService
- Views path checked: `PEMS.Web/Areas/Admin/Views/PermissionMatrix` (1 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Missing |

### ProjectIntelligence (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/ProjectIntelligenceController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/ProjectIntelligence` (1 .cshtml files)
- Git: last touched 2026-08-13, 1 commit(s) touching this file
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

### Department (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/DepartmentController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Department` (0 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### Employee (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/EmployeeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Employee` (0 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### Settings (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/SettingsController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Settings` (0 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Users (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/UsersController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Users` (4 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### Roles (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/RolesController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/Roles` (2 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### RoutingRules (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/RoutingRulesController.cs`
- Injected services: IRoutingRuleService
- Views path checked: `PEMS.Web/Areas/Admin/Views/RoutingRules` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### User 

- Controller: `PEMS.Web/Controllers/UserController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/User` (0 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Present |
| MigrationBacked | Yes |
| RazorViews | Missing |
| SidebarReachable | Missing |
| ExplicitAuthZ | GlobalPolicyOnly |
| TestCoverage | Missing |
| Documented | Present |

### VDR 

- Controller: `PEMS.Web/Controllers/VDRController.cs`
- Injected services: IVDRService, IDocumentCommentService, IVdrWorkflowService, ILetterService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/VDR` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Theme 

- Controller: `PEMS.Web/Controllers/ThemeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Theme` (0 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Transmittal 

- Controller: `PEMS.Web/Controllers/TransmittalController.cs`
- Injected services: ITransmittalService
- Views path checked: `PEMS.Web/Views/Transmittal` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### WorkOrder 

- Controller: `PEMS.Web/Controllers/WorkOrderController.cs`
- Injected services: IWorkOrderReviewService, ICurrentUserService, IPermissionService, IWorkOrderLaborService
- Views path checked: `PEMS.Web/Views/WorkOrder` (3 .cshtml files)
- Git: last touched 2026-08-12, 2 commit(s) touching this file
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
| Documented | Present |

### AuditLog (Area: Admin)

- Controller: `PEMS.Web/Areas/Admin/Controllers/AuditLogController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Areas/Admin/Views/AuditLog` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### VDRImport 

- Controller: `PEMS.Web/Controllers/VDRImportController.cs`
- Injected services: IVDRImportService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/VDRImport` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Present |

### Vendor 

- Controller: `PEMS.Web/Controllers/VendorController.cs`
- Injected services: IVendorService, IVendorRiskService
- Views path checked: `PEMS.Web/Views/Vendor` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Meeting 

- Controller: `PEMS.Web/Controllers/MeetingController.cs`
- Injected services: IMeetingService, ITaskService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Meeting` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### DCC 

- Controller: `PEMS.Web/Controllers/DCCController.cs`
- Injected services: ILetterService, ILetterNumberingService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DCC` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### Department 

- Controller: `PEMS.Web/Controllers/DepartmentController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Department` (1 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### Culture 

- Controller: `PEMS.Web/Controllers/CultureController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Culture` (0 .cshtml files)
- Git: last touched 2026-08-13, 4 commit(s) touching this file
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
| Documented | Present |

### DashboardSettings 

- Controller: `PEMS.Web/Controllers/DashboardSettingsController.cs`
- Injected services: IUserDashboardService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DashboardSettings` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Missing |

### DocumentRevisions 

- Controller: `PEMS.Web/Controllers/DocumentRevisionsController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/DocumentRevisions` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Discipline 

- Controller: `PEMS.Web/Controllers/DisciplineController.cs`
- Injected services: IDisciplineWorkspaceService
- Views path checked: `PEMS.Web/Views/Discipline` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### DocumentRegister 

- Controller: `PEMS.Web/Controllers/DocumentRegisterController.cs`
- Injected services: IEngineeringDocumentService, IExcelExportService, ISavedFilterService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DocumentRegister` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### AIExtraction 

- Controller: `PEMS.Web/Controllers/AIExtractionController.cs`
- Injected services: IDocumentAIIngestionService
- Views path checked: `PEMS.Web/Views/AIExtraction` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### AIInsights 

- Controller: `PEMS.Web/Controllers/AIInsightsController.cs`
- Injected services: IEngineeringCopilotService
- Views path checked: `PEMS.Web/Views/AIInsights` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### Account 

- Controller: `PEMS.Web/Controllers/AccountController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Account` (4 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
- Graphify connectivity (edges touching this file in the graph): 0

| Signal | Result |
|---|---|
| Controller | Present |
| ServiceLayer | Missing |
| EntityOrDbSet | Missing |
| MigrationBacked | Unknown |
| RazorViews | Present |
| SidebarReachable | Missing |
| ExplicitAuthZ | Present |
| TestCoverage | Missing |
| Documented | Present |

### AI 

- Controller: `PEMS.Web/Controllers/AIController.cs`
- Injected services: IOllamaService
- Views path checked: `PEMS.Web/Views/AI` (0 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Missing |

### ComingSoon 

- Controller: `PEMS.Web/Controllers/ComingSoonController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/ComingSoon` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### AISearch 

- Controller: `PEMS.Web/Controllers/AISearchController.cs`
- Injected services: IDocumentSearchAIService
- Views path checked: `PEMS.Web/Views/AISearch` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### ChatAssistant 

- Controller: `PEMS.Web/Controllers/ChatAssistantController.cs`
- Injected services: IChatAssistantService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/ChatAssistant` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### DocumentWorkflow 

- Controller: `PEMS.Web/Controllers/DocumentWorkflowController.cs`
- Injected services: IDocumentWorkflowService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/DocumentWorkflow` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### KnowledgeBase 

- Controller: `PEMS.Web/Controllers/KnowledgeBaseController.cs`
- Injected services: IKnowledgeBaseService
- Views path checked: `PEMS.Web/Views/KnowledgeBase` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Letter 

- Controller: `PEMS.Web/Controllers/LetterController.cs`
- Injected services: ILetterService, ILetterReferralService, ICurrentUserService, IPermissionService, ITagService, IRichTextSanitizerService, ICacheService, ILetterNumberingService
- Views path checked: `PEMS.Web/Views/Letter` (5 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Instrument 

- Controller: `PEMS.Web/Controllers/InstrumentController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Instrument` (3 .cshtml files)
- Git: last touched 2026-08-13, 1 commit(s) touching this file
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
| Documented | Present |

### Intake 

- Controller: `PEMS.Web/Controllers/IntakeController.cs`
- Injected services: IIntakeService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/Intake` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Present |

### MaterialRequisition 

- Controller: `PEMS.Web/Controllers/MaterialRequisitionController.cs`
- Injected services: IMaterialRequisitionService
- Views path checked: `PEMS.Web/Views/MaterialRequisition` (4 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| TestCoverage | Missing |
| Documented | Present |

### MDRImport 

- Controller: `PEMS.Web/Controllers/MDRImportController.cs`
- Injected services: IMDRImportService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/MDRImport` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### MaintenancePlan 

- Controller: `PEMS.Web/Controllers/MaintenancePlanController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/MaintenancePlan` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### MaintenanceStrategy 

- Controller: `PEMS.Web/Controllers/MaintenanceStrategyController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/MaintenanceStrategy` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### EngineeringDocuments 

- Controller: `PEMS.Web/Controllers/EngineeringDocumentsController.cs`
- Injected services: IEngineeringDocumentService, IDocumentWorkflowService
- Views path checked: `PEMS.Web/Views/EngineeringDocuments` (5 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Equipment 

- Controller: `PEMS.Web/Controllers/EquipmentController.cs`
- Injected services: IEquipmentService
- Views path checked: `PEMS.Web/Views/Equipment` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Employee 

- Controller: `PEMS.Web/Controllers/EmployeeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Employee` (2 .cshtml files)
- Git: last touched 2026-08-13, 2 commit(s) touching this file
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
| Documented | Present |

### EngineeringDocumentIntelligence 

- Controller: `PEMS.Web/Controllers/EngineeringDocumentIntelligenceController.cs`
- Injected services: IEngineeringDocumentIntelligenceService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/EngineeringDocumentIntelligence` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### GlobalSearch 

- Controller: `PEMS.Web/Controllers/GlobalSearchController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/GlobalSearch` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### Home 

- Controller: `PEMS.Web/Controllers/HomeController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/Home` (2 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

### EquipmentCriticality 

- Controller: `PEMS.Web/Controllers/EquipmentCriticalityController.cs`
- Injected services: (none detected)
- Views path checked: `PEMS.Web/Views/EquipmentCriticality` (1 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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

### EquipmentImport 

- Controller: `PEMS.Web/Controllers/EquipmentImportController.cs`
- Injected services: IEquipmentImportService, IEquipmentPdfScanService, ICurrentUserService
- Views path checked: `PEMS.Web/Views/EquipmentImport` (3 .cshtml files)
- Git: last touched 2026-08-12, 1 commit(s) touching this file
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
| Documented | Present |

## Non-negotiable reminders for anyone acting on this report

1. A green checklist item means evidence was found in the repo - it does not mean the
   feature was manually verified working end-to-end. Distinguish *Implemented* from *Verified*.
2. dotnet build succeeding is a health indicator, not a completion signal.
3. Zero views or zero nav links can be entirely correct for API/AJAX-only controllers -
   confirm by reading the controller before treating it as unfinished.
4. Re-run this generator after any structural change: `pwsh scripts/project-intelligence/Generate-ProjectIntelligence.ps1`

