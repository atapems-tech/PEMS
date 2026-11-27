# Batch 10 preparation (cutover and rehearsal), 2026-11-27

Source: plan/sheets/definition-ratchet.json (controller actions and compiled views left) and plan/sheets/classification.json. Written before any batch 10 work; the owner decides the order.

## What is left in code

| Controller or folder | Actions | Views | Kind |
|---|---|---|---|
| DefinitionsApi | 20 | 0 | generic host or API (stays) |
| Letter | 13 | 6 | screen code: convert |
| MaintenanceStrategy | 12 | 6 | screen code: convert |
| EngineeringDocuments | 10 | 5 | screen code: convert |
| LetterCompose | 10 | 3 | screen code: convert |
| PartCatalogueRequest | 10 | 0 | screen code: convert |
| Account | 9 | 5 | account and security (decide: stays or becomes definitions) |
| TaskManager | 8 | 11 | screen code: convert |
| LetterCartable | 8 | 1 | screen code: convert |
| Notification | 7 | 1 | screen code: convert |
| WorkOrder | 6 | 3 | screen code: convert |
| DisciplinesApi | 6 | 0 | generic host or API (stays) |
| Transmittal | 4 | 3 | screen code: convert |
| TableExtraction | 4 | 3 | screen code: convert |
| Home | 4 | 2 | screen code: convert |
| Team | 4 | 1 | screen code: convert |
| DocumentRegister | 4 | 1 | screen code: convert |
| SearchApi | 4 | 0 | generic host or API (stays) |
| VDR | 3 | 2 | screen code: convert |
| EquipmentImport | 3 | 2 | screen code: convert |
| DCC | 3 | 2 | screen code: convert |
| Help | 3 | 1 | screen code: convert |
| WebAuthn | 3 | 0 | generic host or API (stays) |
| Files | 3 | 0 | generic host or API (stays) |
| VDRImport | 2 | 2 | screen code: convert |
| Task | 2 | 2 | screen code: convert |
| RecoveryCodes | 2 | 2 | account and security (decide: stays or becomes definitions) |
| MDRImport | 2 | 2 | screen code: convert |
| MDB | 2 | 2 | screen code: convert |
| EngineeringDocumentIntelligence | 2 | 2 | screen code: convert |
| BreakGlass | 2 | 2 | account and security (decide: stays or becomes definitions) |
| AccountRecovery | 2 | 2 | account and security (decide: stays or becomes definitions) |
| FormHost | 2 | 1 | generic host or API (stays) |
| DefinitionPreviewHost | 2 | 0 | generic host or API (stays) |
| PageHost | 1 | 1 | generic host or API (stays) |
| MaintenanceCalendar | 1 | 1 | screen code: convert |
| LetterTemplate | 1 | 1 | screen code: convert |
| DocumentUpload | 1 | 1 | screen code: convert |
| ComingSoon | 1 | 1 | screen code: convert |
| Theme | 1 | 0 | generic host or API (stays) |
| NavigationBadge | 1 | 0 | generic host or API (stays) |
| FileHost | 1 | 0 | generic host or API (stays) |
| ExecutiveDashboard | 1 | 0 | screen code: convert |
| Employee | 1 | 0 | screen code: convert |
| DataHost | 1 | 0 | generic host or API (stays) |
| Culture | 1 | 0 | generic host or API (stays) |
| ActionHost | 1 | 0 | generic host or API (stays) |
| Shared | 0 | 22 | generic host or API (stays) |
| Warehouse | 0 | 3 | screen code: convert |
| Admin | 0 | 2 | screen code: convert |
| _ViewStart.cshtml | 0 | 1 | generic host or API (stays) |
| _ViewImports.cshtml | 0 | 1 | generic host or API (stays) |

Screen code to convert: 132 actions and 72 views in 31 areas. Generic hosts and APIs that stay: 47 actions.


- SCRUM-839: visual comparison of the PDF layouts (letter, part catalogue request) against the old output; see plan/rows/0839.md.

- SCRUM-839: barcode label PDF layouts and the print/bulk redirects (see audit-known-gaps.md).

- SCRUM-840: letter compose form, history page, compliance CSV, two-button forms (see audit-known-gaps.md).

- SCRUM-841: file-host redirects (notification open, document open, task space) - see audit-known-gaps.md.

- SCRUM-841: public pages (status page), status-panel and notification-list components - see audit-known-gaps.md (browser checks listed there).
