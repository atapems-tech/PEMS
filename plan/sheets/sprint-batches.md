# Sprint 2 batches (definition-only goal, owner direction 2026-11-19)

Every batch is 10 to 30 issues done together on one branch (guideline 00 section 0: related issues travel together, a blocker travels with what it blocks). Componentization comes first (owner priority). Each batch ends with one PR, one Jira comment per issue with its QA checklist, then In Review.
Order is by dependency: a later batch only uses what an earlier one shipped.

| # | Batch | Issues | Count | Done when |
|---|---|---|---|---|
| 1 | **Components and screen engines** | 589 (done), 600 details page, 599 list page (saved views, export, bulk), 601 dashboards, 602 import wizard, 616 page behaviours, 617 shared layout and partials, 618 result messages, 619 chart, 620 kanban, 621 calendar and timeline, 622 tree, 623 document viewer, 624 rich text and letter editor, 625 barcode and print, 626 OCR and chat, 627 FMEA matrix, 769 pinning and density, 770 virtualization, 771 view toggle, 772 drag to reschedule, 340 component catalog, 56 form component standards, 374 input shaping, 386 masked inputs, 348 designer rendering | 27 | every screen kind the 140 controllers need is a component or a component definition |
| 2 | **Data, action and security engines** | 592 read data sources (entity registry as definitions), 593 multi-table transaction, 594 result binding, 595 step-up, 596 uploads, 615 file results, 590 menu definitions, 591 permission administration, 448 shipped definitions, 515 field ownership, 516 reference-data ownership, 517 shipped workflows, 574 to 577 pipeline optimisation, 372 / 373 / 380 validation catalog, engine and error contract, 628 classification table, 611 ratchet gate | 22 | any read, write, file, step-up and menu entry is a definition |
| 3 | **Designers, governance and migrator** | 439 page designer, 604 form designer, 606 action designer, 607 data-source designer, 608 menu and permission editor, 609 preview and translation, 419 translation completeness, 449 manager screen, 450 references, 451 remaining kinds, 454 API client, 455 storage report, 457 safe publish, 459 republish precheck, 440 governance, 581 package format, 582 migrator center, 583 feed engine, 584 developer panel, 585 change manifest, 614 guideline, 612 browser regression suite, 613 browser QA of converted actions, 503 adoption QA | 25 | a designer builds and ships a page, a form, an action and a menu entry as a package the migrator installs |
| 4 | Conversion: Admin area | 629 to 651 | 23 | no compiled Admin-area screen |
| 5 | Conversion: shell, organization, AI, dashboards | 666 to 669, 672 to 674, 680 to 682, 687 to 688, 697, 703 to 706, 712 to 713, 729 to 731, 733, 740, 743, 747 to 748, 757 | 28 | no compiled shell, organization, AI or dashboard screen |
| 6 | Conversion: Warehouse and procurement | 652 to 665, 671, 709, 726, 735 to 736, 741, 751, 763 | 22 | no compiled warehouse or procurement screen |
| 7 | Conversion: Work, permits, maintenance, tasks, contracts, production | 675 to 677, 696, 699, 702, 707, 710 to 711, 720 to 725, 728, 734, 737 to 739, 749 to 750, 755 to 756, 758 to 759, 764 to 765 | 28 | no compiled work, contract or production screen |
| 8 | Conversion: Assets, reliability, QC | 670, 678, 691 to 695, 698, 700 to 701, 708, 727, 742, 744 to 746, 752, 754 | 18 | no compiled asset or reliability screen |
| 9 | Conversion: Documents, VDR and letters | 679, 683 to 686, 689 to 690, 714 to 719, 732, 753, 760 to 762 | 18 | no compiled document or letter screen |
| 10 | **Cutover and rehearsal** | 766 final cutover, 767 release rehearsal, 614 docs | 3 | the application serves only definitions; the rehearsal runs on demo with no build |

## The generic engines and their inputs (owner decision 2026-11-19)

One generic engine per kind; every page, form, action or menu entry is an input to it, never a new engine. A conversion that needs something new adds an **input** to the generic engine (its own issue, in the batch that needs it), never a provider, handler or controller for one page.

| Engine | Generic inputs it already takes |
|---|---|
| Page (`/p/{key}`) | layout, regions, components with options, data-source keys, row actions, route definitions |
| Form (`/f/{key}`) | fields (kind, rules, messages, optionsFrom, readOnly, static options), action key |
| Action (`/a/{key}`) | handlers `entity.create/update/delete/toggle`, `operation.invoke` (args from field, literal, context; record and options arguments; guard, sectionGuard; `load`), `workflow.execute`; rules `required`, `unique`, `validate`, `deny`, `defaults`, `redirect` |
| Data source (`/d/{key}`, options) | providers `entity`, `entity-options`, `entity-aggregate`, `operation`, `operation-options`, `components` |
| Menu and routes | menu catalog rows, `route` definitions |

Inputs still missing (each is an issue in batch 4; a conversion waits on it; SCRUM-780 result and status display, 781 action preconditions, 782 matrix editor, 783 hierarchy editor, plus 595, 596, 615 for files and step-up): result and status display of an operation (test connection, update status), action preconditions (confirm fields, entitlement), matrix editors (permission, navigation visibility), hierarchy editors (menu, organization) on the tree component, file and JSON endpoints (595, 596, 615).

Rules for every batch
- A started batch has no issue in To Do: claim all of its issues (In Progress, assigned) when it starts and again whenever an issue is created for it (guideline 00 section 0).
- The engines of batch 1 to 3 are written once and generic; conversions (4 to 9) add only definitions, services and feed (skill `pems-definition-engineer`), never handlers or controllers.
- A conversion issue is closed only when its controller, views and inline scripts are deleted and its routes ship as `route` definitions.
- Every batch: tests per issue run through the real handler, definitions validated by the shipped kinds, plan rows updated, the Jira comment lists the browser checks.
- If a batch finds a gap, the gap becomes a separate issue before the batch ends (guideline 04 section 7), never a tail.

## Plan rows with status Partial (checked 2026-11-19)

54 plan rows have the status Partial. **None is open work**: every one belongs to a Jira issue that is Done or In Review (checked by the `plan-<n>` label, none was To Do, In Progress or On Hold). A Partial row is a finished issue whose plan row names a remainder; reopening the issue would undo delivered work, so none was moved back to In Progress and none was added to a batch.
Where the remainder is real, a batch of this sprint already covers it; where it is not, it is its own later issue:
- covered by batch 1: the component library rows 31, 35, 37, 319 to 324, 327, 333, 386, 388, 424, 425, 433, 482, 483, 487, 499, 500, 502, 506, 507, 549, 551 (the specialised components 619 to 627, the page and form engines, the adoption ratchet) and 43 (global search, shell: batch 5);
- covered by batch 2: 183 (upload pipeline: SCRUM-596), 553 and 554 (validation and error contract: SCRUM-373, 380), 16 (auto-numbering: its own later issue), 423 (sidebar: SCRUM-590);
- covered by batch 3: 262 and 309 (migrator, SCRUM-581 to 585); batch 10: 295 and 340 (demo rehearsal, SCRUM-767); batch 9: 550 (letters conversion);
- no batch of this sprint: 112 and 130 (licensing catalogue and price list), 287 (main host platform), 335, 336 and 337 (tracker and plan-sync set-up, owner tasks); 29, 30, 42, 391, 392, 408, 409, 426, 467, 476, 552 are small remainders to be raised as their own issues when their area is next worked.
The plan status of a row is set from Jira by `plan_sync.py pull`; a row that is still Partial after its issue is Done is a statement about its remainder, not about the issue.
