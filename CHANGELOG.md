# Changelog

All notable changes to PEMS. No system version is defined yet (plan row F_CI), so entries are grouped by date
and theme and were reconstructed from the git history (initial import 2026-09-05). Newest first.

## 2026-10-26 - QA check in the UI, merge before In Review (SCRUM-394)

### Changed
- Workflow (owner decision): the PR is merged into `main` right before the issue moves to In Review (label `qa-pending`); In Review means merged and waiting for the QA check; Done means the QA check in the UI passed.
- New guideline chapter `docs/guidelines/07-qa-check-in-the-ui.md` (who, where, "fully in the UI", checklist template, defects, tasks without a screen); chapters 00, 01, 06, README, `AGENTS.md`, `PLAN_TRACKER_WORKFLOW.md` and the PR template updated.

## 2026-10-26 - Configuration constants second pass (SCRUM-335)

### Changed
- New settings in every `env/*.env`: `Hosting__HstsMaxAgeDays` (30), `Authentication__Cookie__SlidingExpiration` (true), `Database__MigrationCommandTimeoutMinutes` (10); values equal the previous behaviour.
- Production password policy: minimum length 12 in `env/production-kangan.env` (other environments stay at 8); the first administrator password must meet it.
- `docs/design/CONFIGURATION_CONSTANTS.md` classifies the remaining literals (setting, policy constant, domain rule, tuning with trigger).

## 2026-10-26 - Secrets audit: no built-in credentials (SCRUM-17)

### Security
- Removed every built-in password found in the audit: users created from an employee now get a random one-time password (`PEMS.Shared/Security/TemporaryPasswordGenerator.cs`); no default administrator password in the guides; the load-test password comes from `LoadTest__Password` in `env/local.secrets.env` or `PEMS_LOADTEST_PASSWORD`; the example administrator password is a placeholder.
- `docker-compose.yml` publishes SQL Server, Ollama and the app on `127.0.0.1` only (a test keeps it so).
- Audit report `docs/audits/SECRETS_AUDIT_2026-10.md` (HEAD plus all 202 commits of all refs), rotation runbook `docs/deployment/CREDENTIAL_ROTATION_RUNBOOK.md` and the read-only `scripts/security/Report-DefaultCredentialExposure.sql`. Environments created before 2026-10-23 still need rotation (plan row #317); the old values remain in git history (held, #374).

### Changed
- Workflow: the SCRUM project has an On Hold status; the Jira report is kept current during the work (Instructions rule 27); existing tasks are studied before a new one is added (rule 28).

## 2026-10-25 - Staging environment live

### Added
- Staging deploys from the `staging` branch: run 40208748572 is green, the database installed (HTTP 200, up to date) and only the changed files (33.8 MB) were uploaded. Root causes found on the way: stale migration-chain test, FTP protocol default, SQL login with a wrong user name.

## 2026-10-25 - Faster deploys: no repeated tests, health check shows the real error

### Changed
- The deploy pipeline records `gate-pass/<source tree hash>` after build, drift gate and tests pass and skips them when the same tree is deployed again (for example demo after staging); NuGet packages are cached; `force_tests` runs the gate anyway.
- The post-deploy health check prints the readable error page and, after 20 consecutive 5xx answers, the newest `logs/stdout*` and `logs/pems-*.log` lines read over FTP, then fails.

## 2026-10-25 - EF migrations in their own assembly (SCRUM-371)

### Changed
- New project `PEMS.Migrations` holds the 119 migrations, the model snapshot and the design-time factory; `PEMS.Persistence.dll` shrinks from 29 MB to 6 MB and `PEMS.Migrations.dll` (23 MB) stays byte-identical when only data-layer code changes, so deploys no longer re-upload the migrations.
- `PEMSDbContext` defaults `MigrationsAssembly` to `PEMS.Migrations`; Web and Tests reference the project. Add a migration with `dotnet ef migrations add <Name> --project PEMS.Migrations --startup-project PEMS.Web`; the drift gate, `deploy.ps1`, the Dockerfile and the docs use the new project.
- Tests: the update-run test no longer assumes the last two migrations are the code-first ones; a new database-free test checks that a plain `UseSqlServer` finds the migrations.

## 2026-10-25 - Smaller deployment package

### Changed
- `Directory.Build.props`: the git commit hash is no longer written into assembly versions, so unchanged PEMS assemblies stay byte-identical between commits and the FTP difference upload skips them.
- Environment packages (`-p:PemsEnv=<name>`) drop debug symbols, satellite resources of languages other than en/fa and the x86 native libraries: 285 MB -> 245 MB. `wwwroot/uploads` is excluded from publishes.
- `wwwroot/uploads` (17 runtime files incl. signatures and documents) is no longer tracked: added to `.gitignore`, files stay on disk.
- FTP steps treat an empty `<P>_FTP_PROTOCOL` as `ftps` everywhere; the pipeline checks FTP login, folder and write access before the long build; the migration-chain test no longer requires `FullTextSearch` to be the last migration.

## 2026-10-25 - Pipelines only on demo and staging, staging environment, deployment README

### Changed
- No workflow runs on pushes or pull requests to `main` or working branches: `build.yml` is manual (`workflow_dispatch`).
- The demo pipeline is now a reusable workflow `deploy-site.yml` (target and secrets prefix as inputs); `deploy-demo.yml` (push to `demo`) and the new `deploy-staging.yml` (push to `staging`) call it.

### Added
- `env/staging.env` and `env/staging.secrets.env.example` (same keys as every environment; the environment-files test includes `staging`); secrets `STAGING_*`.
- `docs/deployment/DEPLOYMENT_README.md`: subdomains, databases, FTP, secrets, Actions, manual deploy and manual reset, rollback, new-environment checklist.
- Plan rows #351 (this work), #352 (versioned, hashed, importable data package in the admin UI) and #353 (reset on deploy with compatibility precheck); the two data features are not built yet.
- Branch `staging` (main + demo + dev); branch `dev` retired after merging into `main`.

## 2026-10-24 - Demo pipeline works end to end

### Fixed
- Model-drift gate failed in CI (design-time factory needs a connection string): dummy value for the model-only check.
- FTP upload failed with `550 ... being used by another process` (IIS file locks): `app_offline.htm` is uploaded first and removed after a successful upload.
- FTP `Timeout (control socket)`: 180 s timeout and one automatic retry.
- Health check could not see the app state behind a missing TLS certificate: probe uses `--insecure`; the certificate is reported separately.
- `demo` pushes that only change docs/plan no longer redeploy.

### Added
- Startup output of the demo app is logged to `logs/stdout*`; `docs/deployment/DEMO_DEPLOYMENT.md` records the runs, causes and fixes.
- Result: run 5 on `demo` is green and the runner's check reports `HTTP 200 UpToDate` (demo database installed). Open: TLS certificate (#340).

## 2026-10-24 - Tracker-free plan workbook, Sprint 1, clearer task text

### Changed
- The plan workbook no longer mentions or stores anything tracker-specific (the `Jira Key` column and all Jira wording are gone);
  tracker-specific export lives only in `tools/plan-sync`. `docs/process/PLAN_JIRA_WORKFLOW.md` is now `PLAN_TRACKER_WORKFLOW.md`.
- Backlog columns `Sprint` and `Owner`; new `Sprint 1` sheet (goal, dates, capacity, committed and stretch items).
- Sprint 1 tasks rewritten with an unambiguous *What to do* and *Done when*; new rows #338 (metadata registry design) and #339 (licensing technical design).
- Export: summaries `[#<Plan ID>] <task>`, structured descriptions (why, what to do, done when, dependencies with titles), Sprint and Assignee
  columns (assignees from the git-ignored `plan/members.json`), epics per milestone.

## 2026-10-23 - Component library plan, UI audit, plan/Jira workflow

### Added
- `tools/UiAudit/pattern-audit.py`: static audit of every view (pattern, target components, migration wave, hand-written markup counts);
  results in `docs/design/UI_PATTERN_AUDIT.md` and `docs/design/ui-pattern-audit.csv`.
- `docs/design/COMPONENT_LIBRARY_PLAN.md`: research, decisions, taxonomy, Definition of Done and delivery plan for the PEMS.UI library.
- `docs/process/PLAN_TRACKER_WORKFLOW.md` and `tools/plan-sync/plan_sync.py` (export to Jira CSV, `plan/backlog.csv` mirror, offline check);
  `.github/pull_request_template.md`.
- Plan workbook: backlog #319-#337 (component program, migration waves, Jira workflow), a `Jira Key` column, instructions 25-26.

## 2026-10-23 - Environment settings and secrets files

### Added
- `env/<name>.env` (all non-secret settings, committed) and `env/<name>.secrets.env` (secrets, git-ignored) for
  `local`, `docker`, `demo`, `uat` and `production-kangan`, plus `*.secrets.env.example` templates and `env/README.md`.
- `PEMS_ENV` selects the environment; precedence is env file < secrets file < real environment variables < command line.
  Loader: `PEMS.Shared.Configuration.EnvFiles` and `AddPemsEnvFiles` (fails fast on a missing file or required setting).
- Demo pipeline writes `env/demo.secrets.env` from GitHub secrets (`DEMO_SQL_CONNECTION_STRING`, `DEMO_ADMIN_PASSWORD`)
  and sets `PEMS_ENV=demo` in `web.config`; `dotnet publish -p:PemsEnv=<name>` ships the matching non-secret file.
- Tests: file format, precedence, identical key sets in every environment, no secrets in the committed files.
- Backlog #314: "What's new" panel on the login page (planned).

### Changed
- `appsettings.json` is empty; `appsettings.Development.json` and `appsettings.UAT.json` are removed (moved to env files).
- Hard-coded settings moved out of the code: log level/path/retention, password policy, auth cookie name/lifetime,
  security-stamp interval, WebAuthn domain/name/origins/drift, Ollama URL/models/timeout, default and supported
  cultures, Work Request segregation-of-duties flag, and the first administrator (user name, email, password).
- No first administrator is created unless `Seed:Admin:*` is configured (the built-in default account is gone).
- The design-time EF factory reads the env files and no longer has a hard-coded connection string.
- `docker-compose.yml` reads `env/docker.env` and `env/docker.secrets.env`; the default SQL Server password is gone.
- Linux release package (`deploy/Build-Package.ps1`) ships `env/*.env`; the VM must set `PEMS_ENV`.

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
