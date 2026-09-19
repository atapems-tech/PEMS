# PEMS — Import Center (single source of truth for "where is every Import?")

The **Import Center** is one sidebar entry (`/ImportCenter`) that lists every Import in PEMS. It is a **navigation/discovery layer only**:
each card links to the *existing* Controller/Action (or the existing page whose Import dialog it opens). No Import engine, file format,
validation, staging/review/commit logic, route, permission or database object was changed.

Code: `PEMS.Web/Security/ImportCatalog.cs` (registry — add new imports here), `PEMS.Web/Controllers/ImportCenterController.cs`,
`PEMS.Web/Views/ImportCenter/Index.cshtml`, sidebar entry in `Views/Shared/_SidebarMenu.cshtml`.
Guard test: `PEMS.Tests/Authorization/ImportCenterNavigationTests.cs` (fails if a controller that accepts file uploads is neither in the catalog nor in the documented non-import list).

> History note: an earlier refactor ("Quick Setup & Domain Import Architecture Refactoring") had moved imports **out of** a sidebar "Import Center" into their domain sections.
> This task deliberately reverses that navigation decision on request. Module ownership is unchanged — only navigation is centralised.

## A. Complete Import inventory (11)

| # | Import | Module | Controller / Action (unchanged) | File types | Workflow | Backend permission (module → View) | Legacy sidebar location (removed) | Import Center category |
|---|---|---|---|---|---|---|---|---|
| 1 | MDR Import | Engineering | `MDRImport/Index` | .xlsx | Upload → Review (toggle rows) → Commit / Discard; + Rematch Disciplines, Fix Legacy Statuses | `MDRImport` (View/Create/Approve/Delete) | Engineering ▸ MDR Import | Engineering Data |
| 2 | VDR Import | Engineering | `VDRImport/Index` | .xlsx | Upload → Review → Commit / Discard | `VDR` | Engineering ▸ VDR Import | Engineering Data |
| 3 | Equipment Import | CMMS | `EquipmentImport/Index` | .xlsx; .pdf (tag scan → CreateFromTags) | Upload → Review → Commit / Discard; PDF scan → confirm tags → create | `EquipmentImport` | CMMS ▸ Equipment Import | CMMS / Maintenance |
| 4 | Functional Location Import | CMMS | `FunctionalLocationImport/Index` | .xlsx | Upload → Review → Commit / Discard | `FunctionalLocationImport` | CMMS ▸ Functional Location Import | CMMS / Maintenance |
| 5 | Equipment ↔ FL Assignment | CMMS | `EquipmentFunctionalLocationAssignment/Index` | .xlsx | Upload → Review (+ ReplaceExisting) → Commit / Discard | `EquipmentFunctionalLocationAssignment` | CMMS ▸ Equipment ↔ FL Assignment | CMMS / Maintenance |
| 6 | Maintenance History Import | CMMS | `MaintenanceHistoryImport/Index` | .xlsx | Upload → Review (paged/filtered) → Commit / Discard | `MaintenanceHistoryImport` | CMMS ▸ Maintenance History Import | CMMS / Maintenance |
| 7 | Instrument Import | CMMS | `Instrument/Index` → POST `Instrument/Import` (dialog `#importInstrumentModal`) | .xlsx | **Direct** import (no staging), result summary; same columns as Export | none in controller (see gaps) | CMMS ▸ Instrument Passport (dialog on that page) | CMMS / Maintenance |
| 8 | MRS Import | Inventory | `MrsImport/Index` | .xlsx, .xlsm (+ sheet name) | Upload → Review (match filter) → BulkCommit / BulkReject | `MrsImport` | Inventory & Materials ▸ MRS Import | Inventory & Materials |
| 9 | Departments Import | Organization | `Department/Index` → POST `Department/Import` (dialog `#importDeptModal`) | .xlsx | **Direct** import, duplicates skipped | `[Authorize(SystemAdmin, EngineeringManager)]` | Administration ▸ Departments (dialog) | Organization |
| 10 | Employees Import | Organization | `Employee/Index` → POST `Employee/Import` (dialog `#importEmpModal`) | .xlsx | **Direct** import, duplicates/invalid skipped | `[Authorize(SystemAdmin, EngineeringManager)]` | Administration ▸ Employees (dialog) | Organization |
| 11 | Document Upload (AI intake) | AI & Knowledge | `DocumentUpload/Index` → POST `Upload` | .pdf .docx .xlsx images (multi-file) | Upload → AI intake pipeline | none in controller | AI & Knowledge ▸ Document Upload | AI & Knowledge |

Direct imports (7, 9, 10) are launched from a dialog on the Import Center page itself (see the Update section); the old in-page buttons, dialogs and `#import` deep links were removed.

### Reviewed and deliberately NOT Imports
Discovered by the same audit (all accept a file or "bulk" but are not data imports): `DCC/RegisterIncoming`, `EngineeringDocuments/UploadAttachment`, `Letter/UploadAttachment|UploadEditorImage`, `MOC/UploadAttachment`,
`TechnicalQuery/UploadAttachment` (attachments); `MyProfile/UploadPhoto`; `VDR/UploadToFtp|UploadCommentSheet|UploadReplySheet` (files on an existing VDR item); `MaintenanceStrategy/BulkCreate`
(form-based bulk creation, no file); `Barcode/BulkGenerate` (generation); `Relationship/BulkSuggestions|BulkConfirm`, `TableExtraction/StageForReview|BulkConfirm` (AI results already inside PEMS);
export-only Excel use (`UsersController`, `ExcelExportService`).

## B/C. Sidebar audit and consolidation mapping

```
OLD LOCATION                                  → IMPORT CENTER →  EXISTING PAGE (unchanged route)
Engineering ▸ MDR Import                      → Engineering Data → /MDRImport
Engineering ▸ VDR Import                      → Engineering Data → /VDRImport
CMMS ▸ Equipment Import                       → CMMS            → /EquipmentImport
CMMS ▸ Functional Location Import             → CMMS            → /FunctionalLocationImport
CMMS ▸ Equipment ↔ FL Assignment              → CMMS            → /EquipmentFunctionalLocationAssignment
CMMS ▸ Maintenance History Import             → CMMS            → /MaintenanceHistoryImport
(dialog on) CMMS ▸ Instrument Passport        → CMMS            → /ImportCenter (dialog → POST /Instrument/Import)
Inventory & Materials ▸ MRS Import            → Inventory       → /MrsImport
(dialog on) Administration ▸ Departments      → Organization    → /ImportCenter (dialog → POST /Department/Import)
(dialog on) Administration ▸ Employees        → Organization    → /ImportCenter (dialog → POST /Employee/Import)
AI & Knowledge ▸ Document Upload              → AI & Knowledge  → /DocumentUpload
```
Not moved: `Barcode` (not an import; keeps its original role gate). The former in-page shortcuts (MDR/MRS/VDR buttons, Quick Setup cards, Intake upload button, Departments/Employees/Instrument dialogs) were **removed** — see the Update section.

## E. Permissions model
* **Sidebar entry**: shown iff the user could previously see ≥1 scattered import link (`ImportCatalog.VisibleByRole`; no DB call).
* **Landing page cards**: each card requires the *same role gate as its old link* — section roles ∩ `MenuAccess.ImportCenter` (imports 1–6, 8), section roles (7), `Administration` (9, 10), `AIIntelligence` (11) —
  **plus**, for staged imports, the module's `View` permission from `IPermissionService`. So nothing is visible that wasn't before; dead links are dropped.
* **Backend stays authoritative**: every import controller still performs its own permission check; the tests hit the URLs directly with unauthorised roles (QAManager → MDR/MRS, ContractManager → Equipment, Viewer → Departments, MaintenanceEngineer → Employees): none returns 200.
* Effective visibility per role (test-verified): SystemAdmin 11; EngineeringManager 9; DocumentController 3; MaintenanceEngineer 6; Viewer 2; ContractManager 1; DisciplineEngineer 1; QAManager 0.

## Known pre-existing gaps (recorded, intentionally not changed)
1. `Instrument/Import` (POST) and `DocumentUpload` had **no controller-level permission check**; they now enforce the same role gate as their Import Center card (`ImportCatalog.CanUse`). A module-permission model for them is still a separate change.
2. `Department`/`Employee` controllers allow `EngineeringManager` by attribute while the sidebar exposed them to `SystemAdmin` only; the Import Center keeps the narrower (sidebar) exposure.
3. Import pages still set breadcrumb "Section" to their domain (e.g. "Engineering Management"); left unchanged.

---
## Update — Import Center is now the ONLY initiation point

**Removed from module pages** (import engines untouched): Departments/Employees/Instrument "Import" buttons + in-page dialogs + `#import` deep-link scripts; "Import (MDR)" on DocumentRegister and EngineeringDocuments; "MRS Import" on Inventory; "Import Excel" on VDR; "Upload New File" on AI Intake; the four "Start Import" cards on Quick Setup (replaced by one pointer to the Import Center).
Kept on purpose: `MaterialItem/Details` → `MrsImport/Review` (opens an *existing* batch from history, does not start an import); attachment uploads (DCC, EngineeringDocuments, Letter, MOC, TQ, MyProfile, VDR comment/FTP) — not data imports.

**Direct imports launch from the Import Center**: Instrument, Departments, Employees render a dialog on `/ImportCenter` that posts to the existing `Instrument|Department|Employee` `Import` actions (same `file` field, antiforgery, `.xlsx`). Result messages still appear on the owning list page (existing redirect).

**Reconciliation (11 imports)**
| Import | Import Center entry | In-page controls before | Removed | Workflow preserved | Verified |
|---|---|---|---|---|---|
| MDR | ✓ | DocumentRegister, EngineeringDocuments, QuickSetup | ✓ | ✓ | test + browser |
| VDR | ✓ | VDR list, QuickSetup | ✓ | ✓ | test + browser |
| Equipment | ✓ | QuickSetup | ✓ | ✓ | test + browser |
| Functional Location | ✓ | — | n/a | ✓ | test + browser |
| Equipment↔FL Assignment | ✓ | — | n/a | ✓ | test + browser |
| Maintenance History | ✓ | — | n/a | ✓ | test + browser |
| Instrument | ✓ (dialog) | Instrument page button+dialog+hash | ✓ | ✓ | test + browser |
| MRS | ✓ | Inventory, QuickSetup | ✓ | ✓ | test + browser |
| Departments | ✓ (dialog) | Departments button+dialog+hash | ✓ | ✓ | test + browser |
| Employees | ✓ (dialog) | Employees button+dialog+hash | ✓ | ✓ | test + browser |
| Document Upload | ✓ | AI Intake button | ✓ | ✓ | test + browser |

Guard: `ImportCenterNavigationTests.NoModulePage_ContainsAnImportInitiationControl` scans every view outside the import pages for import launchers/`#import`/import modals/`asp-action="Import"`/links to dedicated import controllers.

**Security review (focused)**
| Import | Import Center gate | Backend authorization | Direct-URL access for unauthorised role |
|---|---|---|---|
| 7 staged imports | section roles ∩ ImportCenter roles **and** module `View` permission | module permission per action (View/Create/Approve/Delete) | blocked (verified: QAManager→MDR/MRS, ContractManager→Equipment) |
| Instrument | CMMS roles | **was: none** → now `ImportCatalog.CanUse("Instrument")` (same role gate) in `Instrument/Import` | blocked for other roles |
| Document Upload | AIIntelligence roles | **was: none** → now the same role gate on `Index` and `Upload` | verified: QAManager/Viewer/MaintenanceEngineer forbidden; DocumentController/SystemAdmin open |
| Departments / Employees | SystemAdmin | `[Authorize(SystemAdmin, EngineeringManager)]` (broader than the card; card stays narrower) | blocked for other roles (verified Viewer→Departments, MaintenanceEngineer→Employees) |
The Import Center never shows a link the backend rejects for that role. Remaining, deliberately untouched: module-level permission model for Instrument/DocumentUpload (would need new permission seeds).
