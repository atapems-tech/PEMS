# Changelog

All notable changes to PEMS. No system version is defined yet (plan row F_CI), so entries are grouped by date
and theme and were reconstructed from the git history (initial import 2026-09-05). Newest first.

## 2026-10-22 / 2026-10-23 - Fully code-first database

### Added
- `IDatabaseUpdateService`: tells a first run (empty database) from an update (pending migrations), reports an
  unmanaged schema (tables without migration history, never touched) and a database newer than the application,
  applies the install/update and records every run in the new `DatabaseUpdateRuns` table.
- Admin > System Monitoring > **Database Update** page (SystemAdmin, backup confirmation) and anonymous
  `GET /health/database` (200 only when every migration is applied).
- Migration `FullTextSearch` (full-text catalog and indexes, non-transactional, idempotent).
- Model-drift gate in `build.yml`; Persian and English UI strings for the new page.
- Plan workbook in the repository: `plan/PEMS_Backlog_Roadmap.xlsx`.

### Changed
- Startup now creates the database if missing and installs/updates it before the seeders run
  (`Database:CreateIfMissing`, `Database:MigrateOnStartup`; set `Database__MigrateOnStartup=false` to keep the
  reviewed-script flow).
- Demo pipeline (`deploy-demo.yml`): drift gate, runtime configuration, FTP upload, then waits for
  `/health/database` to report up to date.

### Removed
- Hand-run schema SQL moved to `legacy-trash/manual-sql/` (kept, not deleted): `InitialCreate.sql`, the full-text
  script, the procurement and TaskManager migration scripts.

## 2026-10-20 / 2026-10-21 - Correspondence, accessibility, themes, KPI
- Formal internal letter compose, approval chain and compliance; letter list filters, bulk actions, export,
  dashboard, response-due tracking, notifications, templates and reminders (schema migrations included).
- Accessibility: skip link, AA-safe status text colours, sidebar menu search, friendly status pages.
- Theme audit (Turquoise, Light, Dark) for contrast and cascade.
- Executive KPI dashboard, permission UI auto-save, Part Catalogue rules and settings, org-chart vacancy views.
- Fix: null-key crash for class-level RCM strategies in the criticality matrix.

## 2026-10-15 / 2026-10-19 - Navigation, task manager, procurement gates, merges
- Data-driven sidebar with per-item permissions, live badges and admin management (enabled in Development).
- Enterprise Task Manager on `TaskItem` (spaces, backlog, board, sprints) and a task IDOR fix.
- Procurement: read-view gates (P0-09) and segregation of duties (P0-10) per owner decisions D-11..D-13.
- Fix: user creation is atomic when role assignment fails; employee/discipline/role-management work merged;
  personal source merged into the company repository.

## 2026-10-08 / 2026-10-14 - Organization, procurement, CMMS audit fixes, deployment kit
- Enterprise organization hierarchy, actor resolution, org chart and test isolation.
- Employee account and authorization panel, role synchronization, discipline master-data UI and gating,
  employee imports rejected without a valid discipline.
- Procurement/inventory: Phase 1 hardening, Phase 2 net demand (P0-P5), part-catalogue duplicate detection,
  contract-creation hardening (P0.4/P0.5); CMMS audit fixes and Work Order section policy; RCM strategy work.
- My Work inbox and a UI repair program.
- Docker Compose deployment kit for the Linux test VM.
- Graphify AST cache no longer versioned.

## 2026-09-28 / 2026-10-07 - RBAC-ABAC, warehouse, maintenance planning
- Part Catalogue Request module, Warehouse area, Role Catalog, notification lifecycle.
- Navigation Visibility architecture with a hierarchical admin UI; RBAC-ABAC phases 3-5 and Work Order governance fixes.
- Work Order scheduling and Maintenance Calendar; dynamic maintenance organization, Work Order discipline scoping.
- Package Procurement contract finalization and IRN receiving.
- Fix: Admin-area ambient route values leaking into application links; sidebar hover-intent delay.

## 2026-09-19 / 2026-09-27 - Workflow platform, Help, UI redesign
- Central Workflow Platform: hardening (phase 1), Work Request migration (phase 2), Work Order lifecycle (phase 3B),
  Workflow Administration and Visual Designer (phase 4, closure audit accepted with documented limitations).
- Bilingual Help system with a knowledge system and standards references; Reliability Analytics audit.
- VDR/Vendor redesign, single Import Center, filtered Engineering Documents export; global compact page header and
  Digital Equipment Passport Work Request history.
- Fixes: MRS import 500-row ceiling and description truncation; VDR duplicate-vendor-key crash on commit.

## 2026-09-13 / 2026-09-18 - Operations, barcode, EDMS lifecycle, UAT readiness
- Operations and Production core with CMMS boundary correction.
- Enterprise Barcode/QR core with Warehouse/Inventory integration.
- Resource Header pilot, Equipment/Functional Location assignment, bulk strategy scope, history import.
- Controlled letter revision lifecycle and Gmail-inspired workspace.
- UAT hosting audit, hardening and an Azure Container Apps runbook.
- Portal shell consistency, search security, authorization wiring.

## 2026-09-08 / 2026-09-12 - CMMS intelligence, reliability, EDMS integrity, file storage
- Maintenance master data and standards taxonomy; Equipment Classification, ISO 14224 failure taxonomy and PM
  template intelligence; hierarchy-aware maintenance intelligence with human-reviewed classification.
- RCM/FMEA/FMECA decision intelligence, Data Quality coverage gate, Reliability Analytics, Failure Pareto, Bad Actors.
- CMMS execution lifecycle closure, operational readiness, engineering criticality assessment, operations dashboard,
  RAM/predictive intelligence, Functional Location master data import.
- EDMS: revision-file binding, revision-scoped client review, transmittal revision and acknowledgement integrity,
  self-approval segregation-of-duties enforcement.
- Enterprise File Storage Management; Quick Setup onboarding.
- RBAC hardening: Team/Crew/Discipline/Master Data authorization, navigation restructure, AI pipeline endpoints.

## 2026-09-05 / 2026-09-07 - Initial import and hardening
- Initial import of the PEMS engineering and CMMS platform.
- Phase 6B hardening waves: authorization remediation, Material Request state machine, WorkOrderTask ownership,
  database-level uniqueness, server-side pagination for the highest-volume grids, final repository audit.
- Engineering Documents: consolidated search and compact density.
