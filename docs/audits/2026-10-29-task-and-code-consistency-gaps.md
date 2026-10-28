# Task-to-task and code-to-code consistency audit (2026-10-29)

Plan only; no code changed. Follow-up of `2026-10-28-code-vs-plan-gaps.md`. Owner request: look for gaps between tasks themselves and between code features themselves.

## 1. Changes made to the plan

| Finding | Action |
|---|---|
| #10 (registry, M1) and #28 (cache) repeat what #434 and ADR-0010 built (envelope, versions, stamp cache) | #10 now registers kinds on the existing envelope; #28 extends the existing stamp cache; no second set of tables or cache |
| #11 field types are the source of the type-pair matrix also needed by #458 and #13 | #11 states it is the one matrix source |
| Missing dependencies | #12 -> #436; #20, #44, #56, #57 -> #451; #437 -> #431, #433; #438 -> #15, #319; #439 -> #434, #451; #440 -> #60; #451 -> #325, #30, #448; #455 -> #174; #460 -> #60 |
| P0 rows depend on P1 rows | #454, #451 and #432 raised to P0 |
| Group names drifted (12 rows) | Dynamic form builder -> Form builder; Metadata & dynamic core -> Dynamic data core; Persian & localisation -> Persian localization; Quality & testing -> Quality & performance; Reporting & export -> Reporting; Process & documentation -> Support & documentation |
| Old rows still said "workbook" | #280, #336 reworded |
| Code-to-code duplication | Folded into existing rows (below), no new rows |

Not added: #45 -> #460, because #460 already depends on #45 (cycle). `plan_sync.py check`: 435 rows, 0 findings.

## 2. Code-to-code duplication (folded into existing rows)

| Duplication in the code | Row that now carries it |
|---|---|
| Five permission styles: HasPermissionAsync about 522 uses, RequirePermission 22, MenuAccess 33, AuthorizeForRolesAsync 32, IsInRole 14 | #461 (choose one mechanism while reviewing; do not add a sixth) |
| About 20 services with their own numbering code next to NumberingService | #315 (move them over; ratchet test) |
| 17 status-history entities and several audit stores next to the generic audit | #17 (decide which stay as domain tables) |
| Letters have their own approval, routing, delegation and reminder entities and do not use IWorkflowRuntime; other modules (Contract, Procurement, Permit, MOC, Document IDC) have their own approval entities | #85 (classify each module "workflow-hybrid" or "moves to runtime") |
| 7+ import batch/row entity pairs | Already covered by #25, #217, #360 |
| Dashboards, searches and notifications built several times | Covered by the component work (#323, #326, #328 waves); no separate row |

## 3. For the owner to decide (report only)

* Higher-priority rows that depend on lower-priority ones: #223 -> #89, #309 -> #311, #327 -> #38, #328 -> #326, #442 -> #426.
* 14 open rows depend on On Hold rows: #141, #146-#150, #292-#301, #347 depend on #139, #291, #305.
* Four rows with no dependency and no dependants: #135, #443, #444, #462.
