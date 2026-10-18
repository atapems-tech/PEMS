# PEMS page inventory & status (Phase 3)

227 user-facing pages. Rendered = fetched in EN + FA with a stub SystemAdmin identity from an isolated database (parameterless pages + record-id pages with seeded/created records) and measured in headless Chrome at 1440/1024/820/768/600/480/390/360 px in both directions (automated LTR, RTL and responsive checks apply to every rendered page).
**Visual** = I looked at rendered screenshots of the page. **Interaction** = an interaction/POST flow was actually exercised (see the column). **Hard-coded** = estimated literal (non-Localizer) UI strings in the view. **Language purity** = rendered-DOM scan (headless Chrome) of PEMS-owned UI text: English page must contain no Persian UI text; Persian page no English UI text; master data shown as option/data values (project & discipline names, statuses), identifiers, emails and language names are excluded. **Accepted** requires: structure reviewed, visually inspected, interaction tested, EN purity 0, FA purity 0, no automated flags. Every other page is **Incomplete** (missing gates listed) or **Blocked**.

| Page (view key) | Rendered EN+FA (LTR/RTL, 8 widths) | Structure reviewed | Visual | Interaction | EN purity (violations) | FA purity (violations) | Hard-coded strings (est.) | Status |
|---|---|---|---|---|---|---|---|---|
| AIExtraction/Index | ✓ | – | – | – | 1 | 1 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (1) |
| AIExtraction/Review | – | – | – | – | – | – | 9 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| AIInsights/Index | ✓ | – | – | – | 2 | 0 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2) |
| AISearch/Index | ✓ | – | – | – | 0 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Account/AccessDenied | ✓ | – | – | – | 0 | 3 | 0 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Account/ChangePassword | ✓ | – | – | – | 1 | 2 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| Account/ForgotPassword | ✓ | – | – | – | 0 | 4 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (4) |
| Account/Login | – | – | – | – | – | – | 7 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Account/ManagePasskeys | ✓ | – | – | – | 1 | 3 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| Asset/Details | – | – | – | – | – | – | 12 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Asset/Index | ✓ | ✓ | ✓ | – | 0 | 5 | 7 | Incomplete - missing: interaction test, FA purity (5) |
| Barcode/BulkGenerate | ✓ | – | – | – | 0 | 3 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Barcode/Index | ✓ | – | – | – | 0 | 3 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Barcode/ResolveError | – | – | – | – | – | – | 0 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Barcode/Verify | ✓ | – | – | – | 1 | 1 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (1) |
| ChatAssistant/Index | ✓ | – | – | – | 1 | 1 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (1) |
| CmmsOperations/Backlog | ✓ | – | – | – | 1 | 3 | 10 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| CmmsOperations/Dashboard | ✓ | – | – | – | 1 | 12 | 12 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (12) |
| CmmsReadiness/ClassDetail | ✓ | – | – | – | 0 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| CmmsReadiness/Index | ✓ | – | – | – | 2 | 4 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (4) |
| ComingSoon/Index | ✓ | – | – | – | 1 | 1 | 0 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (1) |
| Contract/Create | ✓ | – | – | – | 0 | 0 | 0 | Incomplete - missing: structure review, visual inspection, interaction test |
| Contract/Details | – | – | – | – | – | – | 5 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Contract/Index | ✓ | – | – | – | 0 | 0 | 1 | Incomplete - missing: structure review, visual inspection, interaction test |
| Crew/Index | ✓ | – | – | – | 0 | 6 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| CriticalityAssessment/Create | ✓ | – | – | – | 1 | 5 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (5) |
| CriticalityAssessment/Dashboard | ✓ | – | – | – | 1 | 7 | 13 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (7) |
| CriticalityAssessment/Details | – | – | – | – | – | – | 17 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| CriticalityAssessment/Index | ✓ | – | – | – | 0 | 1 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| DCC/Index | ✓ | – | – | – | 1 | 10 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (10) |
| DCC/RegisterIncoming | ✓ | – | – | – | 13 | 0 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (13) |
| DashboardSettings/Index | ✓ | – | – | – | 0 | 3 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Department/Index | ✓ | – | – | – | 0 | 0 | 2 | Incomplete - missing: structure review, visual inspection, interaction test |
| Discipline/Details | ✓ | – | – | – | 0 | 7 | 12 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| Discipline/Index | ✓ | – | ✓ | – | 1 | 1 | 3 | Incomplete - missing: structure review, interaction test, EN purity (1), FA purity (1) |
| DocumentRegister/Index | ✓ | – | – | – | 0 | 10 | 16 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (10) |
| DocumentRevisions/Create | – | – | – | – | – | – | 6 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| DocumentRevisions/Index | ✓ | – | – | – | 0 | 2 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| DocumentUpload/Index | ✓ | – | – | – | 1 | 4 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (4) |
| DocumentWorkflow/Index | ✓ | – | – | – | 0 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Employee/Details | ✓ | – | ✓ | – | 0 | 2 | 0 | Incomplete - missing: structure review, interaction test, FA purity (2) |
| Employee/Index | ✓ | – | ✓ | – | 2 | 4 | 9 | Incomplete - missing: structure review, interaction test, EN purity (2), FA purity (4) |
| EngineeringDashboard/Index | ✓ | – | – | – | 0 | 9 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (9) |
| EngineeringDocumentIntelligence/Index | ✓ | – | – | – | 1 | 17 | 10 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (17) |
| EngineeringDocumentIntelligence/Review | ✓ | – | – | – | 0 | 11 | 17 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (11) |
| EngineeringDocuments/Create | ✓ | ✓ | – | POST valid / invalid (validation) / denied(403) | 0 | 1 | 3 | Incomplete - missing: visual inspection, FA purity (1) |
| EngineeringDocuments/Details | ✓ | ✓ | ✓ | – | 25 | 50 | 108 | Incomplete - missing: interaction test, EN purity (25), FA purity (50) |
| EngineeringDocuments/Edit | ✓ | – | – | – | 0 | 14 | 13 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (14) |
| EngineeringDocuments/Index | ✓ | ✓ | ✓ | – | 0 | 1 | 1 | Incomplete - missing: interaction test, FA purity (1) |
| EngineeringDocuments/UploadAttachment | ✓ | – | – | – | 0 | 10 | 14 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (10) |
| Equipment/Create | ✓ | – | – | – | 0 | 0 | 0 | Incomplete - missing: structure review, visual inspection, interaction test |
| Equipment/Details | ✓ | ✓ | ✓ | tabs, chips, dialog | 1 | 24 | 38 | Incomplete - missing: EN purity (1), FA purity (24) |
| Equipment/Index | ✓ | ✓ | ✓ | sidebar, dropdown, modal open/close, search focus | 0 | 1 | 3 | Incomplete - missing: FA purity (1) |
| EquipmentClassification/Coverage | ✓ | – | – | – | 1 | 8 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (8) |
| EquipmentClassification/Index | ✓ | – | – | – | 0 | 10 | 13 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (10) |
| EquipmentClassification/ReviewQueue | ✓ | – | – | – | 1 | 2 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| EquipmentCriticality/Index | ✓ | – | – | – | 1 | 0 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1) |
| EquipmentFunctionalLocationAssignment/Index | ✓ | – | – | – | 0 | 7 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| EquipmentFunctionalLocationAssignment/Review | – | – | – | – | – | – | 8 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| EquipmentImport/Index | ✓ | – | – | – | 0 | 8 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (8) |
| EquipmentImport/Review | – | – | – | – | – | – | 10 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| EquipmentImport/ScanResult | – | – | – | – | – | – | 7 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| FunctionalLocation/Index | ✓ | – | – | – | 1 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| FunctionalLocationImport/Index | ✓ | – | – | – | 0 | 6 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| FunctionalLocationImport/Review | – | – | – | – | – | – | 9 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| GlobalSearch/Index | ✓ | – | – | – | 0 | 1 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| Help/Article | – | – | – | – | – | – | 2 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Help/Index | ✓ | – | – | – | 1 | 1 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (1) |
| Help/Search | ✓ | – | – | – | 0 | 0 | 0 | Incomplete - missing: structure review, visual inspection, interaction test |
| Home/Index | ✓ | – | ✓ | – | 1 | 17 | 9 | Incomplete - missing: structure review, interaction test, EN purity (1), FA purity (17) |
| Home/Privacy | ✓ | – | – | – | 0 | 1 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| ImportCenter/Index | ✓ | ✓ | ✓ | – | 0 | 11 | 0 | Incomplete - missing: interaction test, FA purity (11) |
| Inspection/Index | ✓ | – | – | – | 1 | 2 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| Instrument/Edit | – | – | – | – | – | – | 13 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Instrument/Index | ✓ | – | – | – | 0 | 8 | 10 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (8) |
| Instrument/Passport | – | – | – | – | – | – | 49 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Intake/Index | ✓ | – | – | – | 1 | 3 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| Intake/Review | – | – | – | – | – | – | 6 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Inventory/Index | ✓ | – | – | – | 0 | 5 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| InventoryMasterData/Index | ✓ | – | – | – | 22 | 3 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (22), FA purity (3) |
| InventoryTransaction/Index | ✓ | – | – | – | 0 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| JobPlan/Details | – | – | – | – | – | – | 8 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| JobPlan/Index | ✓ | – | – | – | 1 | 4 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (4) |
| KnowledgeBase/Index | ✓ | – | – | – | 2 | 1 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (1) |
| Kpi/Index | ✓ | – | – | – | 4 | 21 | 23 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (4), FA purity (21) |
| Letter/Create | ✓ | ✓ | ✓ | POST valid -> Details; editor toolbar names | 0 | 0 | 0 | Accepted |
| Letter/Details | ✓ | – | ✓ | – | 0 | 23 | 33 | Incomplete - missing: structure review, interaction test, FA purity (23) |
| Letter/Index | ✓ | ✓ | ✓ | – | 0 | 3 | 10 | Incomplete - missing: interaction test, FA purity (3) |
| Letter/MyReferrals | ✓ | – | – | – | 0 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Letter/Reply | ✓ | – | – | – | 1 | 4 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (4) |
| MDB/Create | ✓ | – | – | – | 0 | 6 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| MDB/Details | – | – | – | – | – | – | 16 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MDB/Index | ✓ | – | – | – | 0 | 7 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| MDRImport/Index | ✓ | – | – | – | 1 | 10 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (10) |
| MDRImport/Review | – | – | – | – | – | – | 7 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MOC/Details | – | – | – | – | – | – | 15 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MOC/Index | ✓ | – | – | – | 0 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| MOC/Raise | ✓ | – | – | – | 4 | 9 | 13 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (4), FA purity (9) |
| MaintenanceHistoryImport/Index | ✓ | – | – | – | 0 | 6 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| MaintenanceHistoryImport/Review | – | – | – | – | – | – | 6 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MaintenanceMasterData/Index | ✓ | – | – | – | 36 | 39 | 49 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (36), FA purity (39) |
| MaintenancePlan/Create | ✓ | – | – | – | 0 | 15 | 16 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (15) |
| MaintenancePlan/Index | ✓ | – | – | – | 0 | 5 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| MaintenanceStrategy/BulkCreate | ✓ | – | – | – | 27 | 27 | 20 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (27), FA purity (27) |
| MaintenanceStrategy/Index | ✓ | – | – | – | 2 | 6 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (6) |
| MaterialItem/Details | – | – | – | – | – | – | 16 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MaterialItem/Index | ✓ | – | – | – | 0 | 8 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (8) |
| MaterialRequisition/Create | ✓ | – | – | – | 0 | 7 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| MaterialRequisition/Details | – | – | – | – | – | – | 13 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MaterialRequisition/Index | ✓ | – | – | – | 0 | 1 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| MaterialRequisition/Quotations | ✓ | – | – | – | 0 | 1 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| Meeting/Create | ✓ | – | – | – | 0 | 1 | 0 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| Meeting/Details | – | – | – | – | – | – | 10 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Meeting/Index | ✓ | – | – | – | 0 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| MrsImport/Index | ✓ | – | – | – | 1 | 3 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| MrsImport/Review | – | – | – | – | – | – | 17 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| MyProfile/Index | ✓ | – | – | – | 2 | 0 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2) |
| Notification/Index | ✓ | – | – | – | 0 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| OCRProcessing/Index | ✓ | – | – | – | 0 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Performance/Index | ✓ | – | – | – | 9 | 8 | 16 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (9), FA purity (8) |
| Permit/Create | ✓ | – | – | – | 0 | 16 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (16) |
| Permit/Details | – | – | – | – | – | – | 72 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Permit/Index | ✓ | – | – | – | 2 | 4 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (4) |
| ProcurementPackage/Create | ✓ | – | – | – | 0 | 1 | 0 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| ProcurementPackage/Details | – | – | – | – | – | – | 16 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ProcurementPackage/Index | ✓ | – | – | – | 1 | 7 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (7) |
| Production/Dashboard | ✓ | – | – | – | 0 | 14 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (14) |
| ProductionOrder/Create | ✓ | – | – | – | 0 | 12 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (12) |
| ProductionOrder/Details | – | – | – | – | – | – | 16 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ProductionOrder/Index | ✓ | – | – | – | 0 | 5 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| ProductionRun/BatchDetails | – | – | – | – | – | – | 8 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ProductionRun/Create | – | – | – | – | – | – | 5 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ProductionRun/Details | – | – | – | – | – | – | 35 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Projects/Create | ✓ | – | – | – | 0 | 0 | 1 | Incomplete - missing: structure review, visual inspection, interaction test |
| Projects/Edit | ✓ | – | – | – | 0 | 9 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (9) |
| Projects/Index | ✓ | – | – | – | 0 | 1 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| QC/Index | ✓ | – | – | – | 0 | 1 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| QC/ItpCreate | ✓ | – | – | – | 0 | 4 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (4) |
| QC/ItpDetails | – | – | – | – | – | – | 21 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| QC/ItpIndex | ✓ | – | – | – | 0 | 0 | 3 | Incomplete - missing: structure review, visual inspection, interaction test |
| QC/Kpi | ✓ | – | – | – | 0 | 0 | 1 | Incomplete - missing: structure review, visual inspection, interaction test |
| QC/NcrCreate | ✓ | – | – | – | 0 | 2 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| QC/NcrDetails | – | – | – | – | – | – | 20 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| QC/NcrIndex | ✓ | – | – | – | 0 | 0 | 2 | Incomplete - missing: structure review, visual inspection, interaction test |
| QuickSetup/Index | ✓ | – | – | – | 9 | 24 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (9), FA purity (24) |
| RcmFmea/ClassDetail | ✓ | – | – | – | 0 | 15 | 39 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (15) |
| RcmFmea/Index | ✓ | – | – | – | 1 | 7 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (7) |
| Relationship/BulkSuggestions | ✓ | – | – | – | 1 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| Relationship/GapReport | ✓ | – | – | – | 1 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2), a11y/responsive flags |
| Relationship/Index | ✓ | – | – | – | 1 | 2 | 11 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| Reliability/AiInsights | ✓ | – | – | – | 1 | 5 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (5) |
| Reliability/ClassDetail | ✓ | – | – | – | 1 | 3 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| Reliability/ClassIntelligence | ✓ | – | – | – | 1 | 16 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (16) |
| Reliability/ConditionMonitoring | ✓ | – | – | – | 1 | 5 | 17 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (5) |
| Reliability/Dashboard | ✓ | – | ✓ | – | 2 | 20 | 15 | Incomplete - missing: structure review, interaction test, EN purity (2), FA purity (20) |
| Reliability/DataQuality | ✓ | – | – | – | 1 | 10 | 24 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (10) |
| Reliability/Details | ✓ | – | – | – | 0 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Reliability/EquipmentProfile | ✓ | – | – | – | 5 | 25 | 21 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (5), FA purity (25) |
| Reliability/Index | ✓ | – | – | – | 5 | 18 | 21 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (5), FA purity (18) |
| Reliability/Intelligence | ✓ | – | – | – | 7 | 26 | 27 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (7), FA purity (26) |
| Reliability/PredictiveMaintenance | ✓ | – | – | – | 3 | 5 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (3), FA purity (5) |
| Reliability/Ram | ✓ | – | – | – | 3 | 15 | 13 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (3), FA purity (15) |
| Reliability/RcmFeedback | ✓ | – | – | – | 2 | 3 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (3) |
| Reliability/Simulate | – | – | – | – | – | – | 5 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Report/Cmms | ✓ | – | – | – | 0 | 8 | 9 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (8) |
| Report/Engineering | ✓ | – | – | – | 0 | 4 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (4) |
| Report/Index | ✓ | – | – | – | 0 | 6 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| Report/Operations | ✓ | – | – | – | 0 | 7 | 12 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| Report/Procurement | ✓ | – | – | – | 0 | 2 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Report/Vdr | ✓ | – | – | – | 0 | 6 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| Settings/Index | ✓ | – | – | – | 1 | 22 | 25 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (22) |
| Shared/Error | – | – | – | – | – | – | 11 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ShiftHandover/Create | ✓ | – | – | – | 0 | 8 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (8) |
| ShiftHandover/Details | – | – | – | – | – | – | 6 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| ShiftHandover/Index | ✓ | – | – | – | 1 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (2) |
| SparePart/Index | ✓ | – | – | – | 0 | 5 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| StoreRequisition/Details | – | – | – | – | – | – | 4 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| StoreRequisition/Index | ✓ | – | – | – | 0 | 3 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| TableExtraction/Analyze | – | – | – | – | – | – | 19 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| TableExtraction/Index | ✓ | – | – | – | 1 | 3 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (1), FA purity (3) |
| TableExtraction/Review | – | – | – | – | – | – | 12 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Tag/Index | ✓ | – | – | – | 0 | 5 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| Task/Index | ✓ | – | – | – | 0 | 4 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (4) |
| Team/Index | ✓ | – | – | – | 2 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (2), FA purity (3) |
| TechnicalQuery/Details | – | – | – | – | – | – | 11 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| TechnicalQuery/Index | ✓ | – | – | – | 0 | 2 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| TechnicalQuery/Raise | ✓ | – | – | – | 4 | 2 | 8 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (4), FA purity (2) |
| Transmittal/Create | ✓ | ✓ | – | – | 0 | 17 | 17 | Incomplete - missing: visual inspection, interaction test, FA purity (17) |
| Transmittal/Details | – | ✓ | – | – | – | – | 0 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Transmittal/Index | ✓ | ✓ | ✓ | – | 0 | 1 | 0 | Incomplete - missing: interaction test, FA purity (1) |
| VDR/Create | ✓ | ✓ | – | – | 0 | 1 | 2 | Incomplete - missing: visual inspection, interaction test, FA purity (1) |
| VDR/Details | – | ✓ | ✓ | – | – | – | 12 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| VDR/Index | ✓ | ✓ | ✓ | – | 0 | 0 | 0 | Incomplete - missing: interaction test |
| VDRImport/Index | ✓ | ✓ | – | – | 0 | 0 | 1 | Incomplete - missing: visual inspection, interaction test |
| VDRImport/Review | – | ✓ | – | – | – | – | 3 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Vendor/Create | ✓ | ✓ | – | POST valid (invalid -> 500, backend) | 0 | 0 | 0 | Incomplete - missing: visual inspection |
| Vendor/Details | ✓ | ✓ | – | – | 0 | 0 | 0 | Incomplete - missing: visual inspection, interaction test |
| Vendor/Edit | ✓ | ✓ | – | – | 0 | 0 | 0 | Incomplete - missing: visual inspection, interaction test |
| Vendor/Index | ✓ | ✓ | ✓ | – | 0 | 0 | 0 | Incomplete - missing: interaction test |
| WorkOrder/Create | ✓ | – | – | – | 0 | 5 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (5) |
| WorkOrder/Details | ✓ | – | ✓ | – | 7 | 12 | 49 | Incomplete - missing: structure review, interaction test, EN purity (7), FA purity (12) |
| WorkOrder/Index | ✓ | ✓ | ✓ | – | 0 | 0 | 0 | Incomplete - missing: interaction test |
| WorkRequest/Create | ✓ | ✓ | ✓ | POST valid / invalid / denied(403) | 1 | 8 | 5 | Incomplete - missing: EN purity (1), FA purity (8) |
| WorkRequest/Details | ✓ | – | ✓ | – | 0 | 7 | 9 | Incomplete - missing: structure review, interaction test, FA purity (7) |
| WorkRequest/Index | ✓ | ✓ | ✓ | – | 1 | 2 | 2 | Incomplete - missing: interaction test, EN purity (1), FA purity (2) |
| Admin/AuditLog/Index | ✓ | – | – | – | 0 | 1 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (1) |
| Admin/Help/Create | ✓ | – | – | – | 6 | 33 | 0 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (6), FA purity (33) |
| Admin/Help/Edit | ✓ | – | – | – | 6 | 37 | 6 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (6), FA purity (37) |
| Admin/Help/History | ✓ | – | – | – | 0 | 2 | 2 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Admin/Help/Index | ✓ | – | – | – | 0 | 0 | 3 | Incomplete - missing: structure review, visual inspection, interaction test |
| Admin/Help/StandardCreate | ✓ | – | – | – | 6 | 44 | 27 | Incomplete - missing: structure review, visual inspection, interaction test, EN purity (6), FA purity (44) |
| Admin/Help/StandardMatrix | ✓ | – | – | – | 0 | 0 | 1 | Incomplete - missing: structure review, visual inspection, interaction test |
| Admin/Help/VisualCreate | – | – | – | – | – | – | 14 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Admin/PermissionMatrix/Index | ✓ | – | – | – | 0 | 19 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (19) |
| Admin/ProjectIntelligence/Index | ✓ | – | – | – | 0 | 901 | 21 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (901) |
| Admin/Roles/Create | ✓ | – | – | – | 0 | 3 | 4 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (3) |
| Admin/Roles/Index | ✓ | – | ✓ | – | 0 | 3 | 6 | Incomplete - missing: structure review, interaction test, FA purity (3) |
| Admin/RoutingRules/Index | ✓ | – | – | – | 0 | 6 | 7 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| Admin/StorageProviders/Create | ✓ | – | – | – | 0 | 12 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (12) |
| Admin/StorageProviders/Edit | – | – | – | – | – | – | 0 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Admin/StorageProviders/Index | ✓ | – | – | – | 0 | 2 | 5 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Admin/StorageRouting/Create | ✓ | – | – | – | 0 | 11 | 1 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (11) |
| Admin/StorageRouting/Edit | – | – | – | – | – | – | 0 | Blocked - not rendered (needs a record / flow fixture that could not be created safely) |
| Admin/StorageRouting/Index | ✓ | – | – | – | 0 | 2 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (2) |
| Admin/Users/Create | ✓ | – | – | – | 0 | 7 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (7) |
| Admin/Users/Edit | ✓ | – | – | – | 0 | 6 | 3 | Incomplete - missing: structure review, visual inspection, interaction test, FA purity (6) |
| Admin/Users/Index | ✓ | – | ✓ | – | 0 | 4 | 8 | Incomplete - missing: structure review, interaction test, FA purity (4) |
