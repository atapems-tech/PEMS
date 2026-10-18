# Organization import finding: department "Executive Office"

Status: **finding only. No department was created, no cell was changed, no data was modified.**

## What happens
`docs/OrganizationStructure_Petrochemical1_Completed.xlsx` (edited by the data owner) now has `Executive Office` in the Department column of row 2 (`PC-CEO`). PEMS has no department with that name or code (14 real departments exist), so the Organization Import preview reports exactly one error, `Row 2: Unknown department 'Executive Office'.`, and refuses the whole file (all-or-nothing). This is the intended strict foreign-key behaviour: the import never creates departments.

## Model facts (verified in code)
* `Position.DepartmentId` is **nullable**; the import accepts a blank Department. A supplied Department must exist and be active.
* `Department` is load-bearing for Work Order governance (`Employee.DepartmentId` → `OrganizationDomain`) and addressing; it is not required for the manager hierarchy (Position → ParentPosition → holder).
* The preview never writes (`commit=false`), so the finding is reproducible and traceable (the workbook is the audit record; the committed import result lists created/updated counts).

## Two valid resolutions - owner decision
| Option | When to choose | How |
|---|---|---|
| **A. Create the department through the official master-data workflow** | "Executive Office" is a real organizational unit (own head/staff/reporting boundary, will hold executive positions or assistants). | Administration → Organization → Departments → create `Executive Office` (code e.g. `EXEC`, no `OrganizationDomain` needed). Then re-run the import preview; the file is clean (246 positions). |
| **B. Correct the source workbook** | The CEO is only a position; no executive department is wanted. | Blank the Department cell of row 2 (allowed, Department is optional) or choose an existing department. The change is made by the data owner in the workbook, not silently by the tool. |

Recommendation: **B unless Executive Office will own other positions/employees** (a department with a single position adds a unit without organizational meaning, and employees' required `Employee.DepartmentId` would need that department as well).

## Guarantees kept
* No automatic department creation, no silent blanking: the preview error stays until the owner chooses A or B.
* Strict validation preserved: unknown/inactive department still rejects the file; a blank department never clears an existing position's department.
* `PetrochemicalCompletedFile_PassesImportPreview_*` documents this: with the file as supplied the preview yields exactly that one error; with the department present the file is clean.
