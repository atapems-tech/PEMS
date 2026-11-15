# Row-loop tables that stay `pems-table` (SCRUM-570)

Generated 2026-11-15 from the converter's refusals; the ratchet counter `table-row` holds exactly these. A table moves to `pems-data-table` when the feature it needs exists (row attributes, headless table, header markup). Tables inside `pems-list-table` and `pems-related-table` are not counted: they are those families' rows.

## loop-row (16): row has C# statements, a conditional row, a partial row or row attributes
- Areas/Admin/Views/NavigationMenu/Index.cshtml:31
- Areas/Admin/Views/Users/Details.cshtml:219
- Areas/Admin/Views/Users/Details.cshtml:235
- Areas/Warehouse/Views/MrsImport/Index.cshtml:29
- Areas/Warehouse/Views/MrsImport/Review.cshtml:110
- Areas/Warehouse/Views/PartCatalogueAdmin/Index.cshtml:246
- Views/Barcode/BulkGenerate.cshtml:68
- Views/EngineeringDashboard/Index.cshtml:236
- Views/EngineeringDocuments/Details.cshtml:475
- Views/EngineeringDocuments/Details.cshtml:573
- Views/EngineeringDocuments/Details.cshtml:623
- Views/Kpi/Executive.cshtml:124
- Views/Letter/Index.cshtml:561
- Views/Relationship/BulkSuggestions.cshtml:35
- Views/Reliability/Dashboard.cshtml:92
- Views/TableExtraction/Review.cshtml:69

## no-head-shape (15): no header row (body only) or a different head layout
- Areas/Admin/Views/ProjectIntelligence/Index.cshtml:122
- Views/CmmsOperations/Dashboard.cshtml:117
- Views/EquipmentClassification/Index.cshtml:106
- Views/EquipmentClassification/Index.cshtml:121
- Views/EquipmentClassification/Index.cshtml:136
- Views/Letter/Dashboard.cshtml:95
- Views/Letter/Dashboard.cshtml:106
- Views/Letter/Dashboard.cshtml:114
- Views/Letter/Dashboard.cshtml:122
- Views/MaintenanceStrategy/BulkCreate.cshtml:215
- Views/MaintenanceStrategy/Tag.cshtml:24
- Views/MaintenanceStrategy/Tag.cshtml:38
- Views/Reliability/Dashboard.cshtml:119
- Views/Reliability/Dashboard.cshtml:133
- Views/TaskManager/AdminSpace.cshtml:90

## head-extra (6): header cells carry markup, colspan or controls
- Areas/Admin/Views/PermissionMatrix/Index.cshtml:122
- Areas/Warehouse/Views/StoreRequisition/Details.cshtml:78
- Views/MaintenanceCalendar/Index.cshtml:143
- Views/PurchaseOrder/Details.cshtml:66
- Views/TaskManager/AdminSpace.cshtml:159
- Views/TaskManager/_ItemTable.cshtml:18

## colcount (5): cells and headers do not line up (colspan or a spanning cell)
- Areas/Admin/Views/Organization/Positions.cshtml:61
- Views/Discipline/Details.cshtml:35
- Views/Discipline/Manage.cshtml:33
- Views/MaterialRequisition/Details.cshtml:52
- Views/TaskManager/AdminSpace.cshtml:50

## loop-shape (4): the loop is not the only thing in the body (if/else around it, dynamic items)
- Views/Barcode/Index.cshtml:37
- Views/DashboardSettings/Index.cshtml:14
- Views/TableExtraction/Analyze.cshtml:86
- Views/Team/TeamDetails.cshtml:165

## header (3): header text has markup
- Areas/Admin/Views/Numbering/_VdrNumbers.cshtml:31
- Areas/Admin/Views/Organization/Index.cshtml:37
- Views/VDR/Details.cshtml:350

## unbalanced (2): markup the converter cannot balance (inline C#)
- Areas/Warehouse/Views/IrnReceipt/Osd.cshtml:32
- Views/EquipmentClassification/Coverage.cshtml:42

## attr:data-grid-key (2): cell or row carries data-grid-key for the grid script
- Views/MaterialRequisition/Quotations.cshtml:13
- Views/TableExtraction/Index.cshtml:21

## char& (1): a literal ampersand entity in a cell
- Views/EquipmentImport/Review.cshtml:43

## cells-extra (1): text outside cells in the row
- Views/Equipment/Details.cshtml:482

## attr (1): cell attributes other than class, dir, title
- Views/Team/TeamDetails.cshtml:285
