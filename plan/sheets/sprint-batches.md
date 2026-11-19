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

Rules for every batch
- The engines of batch 1 to 3 are written once and generic; conversions (4 to 9) add only definitions, services and feed (skill `pems-definition-engineer`), never handlers or controllers.
- A conversion issue is closed only when its controller, views and inline scripts are deleted and its routes ship as `route` definitions.
- Every batch: tests per issue run through the real handler, definitions validated by the shipped kinds, plan rows updated, the Jira comment lists the browser checks.
- If a batch finds a gap, the gap becomes a separate issue before the batch ends (guideline 04 section 7), never a tail.
