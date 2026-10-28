# Changelog

All notable changes to PEMS. No system version is defined yet (plan row F_CI), so entries are grouped by date
and theme and were reconstructed from the git history (initial import 2026-09-05). Newest first.

## 2026-10-28 - Searchable dropdowns on every form (SCRUM-57)

### Added
- `PEMS.UI` searchable select (`pems-select.js`/`.css`, on the vendored Tom Select), loaded once by the layout: every single `<select class="form-select">` with 8 or more options becomes searchable; `data-pems-select-source="<type>"` searches the permission-aware `/SearchApi` as you type for large lists; `data-pems-select` forces it, `data-pems-select="off"` opts out. Opens in place inside modals and dropdown panels, re-reads options a page replaces, keeps a "-- None --"/"All" choice only where the page has one, RTL and dark theme, fa/en texts from the layout (`Sel_*`).

### Changed
- The user Create/Edit pages no longer load Tom Select themselves (the layout does).

## 2026-10-28 - Automatic numbering of procurement documents (SCRUM-40)

### Added
- Admin > Numbering: the system administrator sets the numbering pattern of each procurement document (MR, procurement package, tender, bid, purchase order, contract): tokens `{YYYY}` `{YY}` (Jalali or Gregorian year), `{PROJECT}`, `{DISC}`, `{SEQ}`, counter digits, yearly or no reset, active, manual entry allowed. Live preview, validation, a version per change (and the generic audit log).
- New table `NumberingSchemes` (migration `NumberingSchemes`); a startup seeder adds an active Jalali yearly default for each type that has none (`MR-1405-0001`, `PP-…`, `TND-…`, `BID-…`, `PO-…`, `CT-…`).

### Changed
- Procurement package, MR, purchase order and contract numbers (also the contract from a winning bid) are assigned automatically on save; tender and bid numbers follow the pattern too. A typed number is kept only when the pattern allows manual entry; with the pattern switched off the form asks for the number as before. The atomic counter is separate per year/project/discipline as the pattern says, a number that already exists is skipped, existing numbers never change, imports keep their numbers.
- The create forms show the next number (or the pattern shape when it depends on the project) with the `DocumentNumberField` view component.

## 2026-10-27 - Procurement & supply chain section in the sidebar (SCRUM-423)

### Changed
- One sidebar section "Procurement & Supply Chain" (fa: تدارکات و زنجیره تأمین), like CMMS: Vendors, then the sub-headings Purchasing (Material Requisition, Procurement Package, Tender, Purchase Orders), Contracts (Contracts, Contract Milestones, Claims) and Warehouse (all former warehouse links). Engineering Management no longer lists the purchasing pages; the Contract Management and Warehouse sections are gone. Both sidebars changed (data-driven `NavigationMenuDefaults` and the classic `_SidebarMenu.cshtml` used in demo, UAT and production). Each link keeps its former gate plus its former section module, so no role sees more or less. Existing databases: the startup sync adds the new menu rows and deactivates the old system rows (not deleted); no migration.

## 2026-10-28 - Metadata registry and UI definition model designed (SCRUM-25, SCRUM-430)

### Added
- `docs/architecture/METADATA_REGISTRY_DESIGN.md`, ADR-0010 (one definition envelope with Draft/Validated/Published/Retired versions, rollback by republish, shipped vs customer definitions, cache stamp) and ADR-0011 (pages, layouts, component instances, data sources and actions as definitions), with a compile-checked C# sketch. Status: proposed, waiting for the owner. Design only; no code or migration.

## 2026-10-28 - REST API standard written (SCRUM-431)

### Added
- ADR-0009 and guideline chapter 08: resource-oriented JSON under `/api/v1`, verbs and status codes, ETag with If-Match, Idempotency-Key, RFC 9457 errors, one list query contract with offset paging for grids and cursor paging for exports and very large tables, OpenAPI 3.1 as the contract. Status: proposed, waiting for the owner to accept. No code yet (the walking skeleton is #435).

## 2026-10-28 - Title row on five pages that showed no heading (SCRUM-425)

### Changed
- Findings (list), Import Center, New Shift Handover, New Criticality Assessment and Verify Barcode now show the shared title row (page title, plus the module label when it differs) above their content; before, they set a title but showed no heading, so it appeared only in the browser tab. The layout still does not render the row; every other page is unchanged.

## 2026-10-28 - Persian digits in the pagination footer (SCRUM-426)

### Changed
- On Persian pages the pagination footer shows Persian digits in the "showing X to Y of Z" text, the page numbers and the rows-per-page buttons (new `IPemsLocale.Digits`, which swaps digits without reformatting). Links, the `page` and `pageSize` parameters and `aria-label` numbers stay Latin; English pages are unchanged byte for byte.

## 2026-10-28 - Page title row moved into PEMS.UI (SCRUM-425)

### Changed
- The page title row is now the `PEMS.UI` partial `_PemsPageHeader` (`PageHeaderModel`: Title, Section, texts as inputs); `_PageHeader` in `PEMS.Web` stays as an adapter that localizes the texts and honours `HidePageTitle`. Output is byte-identical (7 golden cases, en and fa). No page calls it yet: the layout does not render it by owner decision, so adding callers waits for the owner's choice of pages.

## 2026-10-27 - Unified loading, empty and error states (SCRUM-424)

### Added
- Component inputs are now named-property objects (`new ButtonOptions { Label = ... }`) instead of positional arguments, for buttons, badges, the new states and the field/column/view/form descriptors; one shared property-name table is in guideline chapter 02. Rendered output is unchanged (goldens identical).
- `PemsEmptyState`, `PemsErrorState` and `PemsLoadingState` in `PEMS.UI` with a `_PemsState` partial for existing pages: one look, one accessibility contract (status, alert, busy), texts as inputs, registry keys `empty-state`, `error-state`, `loading-state`. No existing page changes yet.

## 2026-10-27 - List page component, wave 0 (SCRUM-337)

### Added
- `PEMS.UI` list-page component: `<pems-list-page>`, `-panel`, `-toolbar`, `-search`, `-tools`, `-filters`, `-kpi-toggle`, `-export`, `-reset-columns`, `-new`, `-kpis`, `-chips`, `-chip`, `-grid`, `-table`, `-footer` tag helpers with the Engineering Documents look; texts are inputs (`PemsListTexts`, `ListPageTexts.From(Localizer)`). `docs/design/LIST_PAGE_COMPONENT.md` (usage, what stays with each page, waves).
- Five generic resource keys (`Lp_Kpis`, `Lp_ApplyFilters`, `Lp_Total`, `Lp_RemoveFilter`, `Lp_ResetAll`).

### Changed
- Engineering Documents and VDR lists are built from the component (same look and behaviour; about 80 lines less each, no page script).
- The list styles and script moved into the component library: `PEMS.Web/wwwroot/css/pems-letter-index.css` -> `PEMS.UI/wwwroot/pems-list.css` (the Letter pages only changed the link), `pems-list-page.js` -> `PEMS.UI/wwwroot/pems-list.js` (now also wires the search autocomplete, column widths and reset-columns for every list page).

## 2026-10-27 - Pagination footer moved into PEMS.UI (SCRUM-418)

### Changed
- The list pagination footer is now the component `_PemsPagination` in `PEMS.UI`; its texts are inputs (`PaginationTexts`). `_Pagination` in `PEMS.Web` stays as a thin adapter, so the 54 list pages are unchanged. The HTML output is byte-identical (8 golden cases, en and fa).

## 2026-10-27 - Persian pages accept decimals and Persian digits (SCRUM-421)

### Fixed
- A decimal such as 12.5 posted by a number input, and Persian or Arabic-Indic digits typed by the user, are now accepted when the page language is Persian; one model binder (`PEMS.UI.Binding`) is registered for numeric and date types. English binding and MVC's error messages are unchanged. Known-issue tests became regression tests; ADR-0008, guideline chapter 02 and manual check M-19 updated.

## 2026-10-27 - Bugs are normal tasks; gate works on Windows (SCRUM-422)

### Changed
- Guideline chapter 00 section 3a: a reported bug gets its own Jira issue and plan row first, a regression test, a PR and QA; chapter 07 and AGENTS.md point to it.

### Fixed
- `LiteralTextGateTests` no longer fails on Windows (CRLF) checkouts (fix merged in atapems-tech/PEMS#44).

## 2026-10-27 - Every module workflow editable in Workflow Management, guarded by module contracts (SCRUM-103)

### Added
- Module contracts on the workflow engine (`IWorkflowModuleContractRegistry`, `PemsWorkflowContracts`): for each of the 13 module workflows, the actions, stages, dynamic-participant hand-overs and BusinessStatus its code relies on. The validator refuses a version that breaks them (`CONTRACT_*` errors), so Validate/Publish cannot break a module.
- Startup seeder `WorkflowBindingBackfillSeeder`: an explicit binding, pinned to today's published version, for every module workflow that had none (same version the legacy path started, so behaviour is unchanged).
- `docs/workflow/WORKFLOW_PLATFORM_MODULE_CONTRACTS.md`.

### Changed
- Admin > Workflow Management: all 13 definitions are editable (was 3 by whitelist); the reason and the designer show which parts the module contract keeps.
- The legacy module action names moved from `Program.cs` to `PemsWorkflowContracts.CreateActionRegistry()` (one list for the app, seeders and tests).

## 2026-10-27 - Component texts are inputs (SCRUM-416)

### Added
- `TranslatableText` (JSON map of language slug to text, fallback chain, completeness check), `IPemsLocale` with the default `PemsLocale` (Jalali dates and Persian digits for Persian cultures, ISO dates otherwise, number formatting, options), `PemsJson`, and a culture parameter on the request-free renderer. ADR-0008.

### Changed
- `PEMS.UI` descriptors and components take `TranslatableText` for every user-visible text and hold none; table rows carry values of any type formatted by the locale. A test fails when a literal word appears in a component. `PEMS.Web` pages are unchanged.

## 2026-10-27 - PEMS.Web consumes PEMS.UI (SCRUM-29, step 1)

### Changed
- `PEMS.Web` references `PEMS.UI` (component registry and renderer registered, `<pems-field>` tag helper available, `_content/PEMS.UI/pems-ui.css` published but not linked, so no page changes). The shared partials `_KpiCard` and `_PemsLoadingOverlay` and `KpiCardViewModel` moved into `PEMS.UI`; their output is byte-identical (golden tests in `PEMS.Tests/Ui`). ADR-0007 accepted.

## 2026-10-27 - Database upgrade from every supported release, pre-update backup (SCRUM-280)

### Added
- `deploy/db/supported-releases.json`: the database states PEMS must upgrade from (2026-10-22 self-installing build, 2026-10-25 demo), each as its exact migration set; `deploy/db/released-migrations.sha256` fixes the shipped migrations.
- `DatabaseUpgradePathSqlTests` (category `DbUpgrade`): each supported release is upgraded with representative data through the real migrator and must equal a fresh install (schema) and keep its rows; also proves the pre-update backup and its restore. Runs in the Migration Chain Gate workflow after the chain script.
- `MigrationCompatibilityRulesTests`: released migrations unchanged, manifest valid, unique ids with Designer files.
- Automatic pre-update backup: `Database__BackupBeforeUpdate` (true in production/uat) and `Database__BackupDirectory` in every `env/*.env`; an update first writes a COPY_ONLY `<db>_preupdate_<utc>.bak` and is not started when that fails. The file is recorded in the run history (migration `DatabaseUpdateRunBackupFile`) and shown on Admin > Database Update.

### Changed
- Admin > Database Update: with the automatic backup on, the manual "backup taken" box is replaced by a notice; the run table shows the backup file.
- `CODE_FIRST_DATABASE.md`: supported releases, compatibility rules (immutable released migrations, expand/contract) and the rollback (restore) procedure; guideline chapter 01 section 3 updated.

## 2026-10-27 - Component library spike (SCRUM-23)

### Added
- `PEMS.UI.Abstractions` (descriptor contract), `PEMS.UI` (Razor class library: button, badge, skeleton, field, table, form, descriptor host, component registry, request-free renderer, `<pems-field>` Tag Helper facade, View Component for comparison) and `PEMS.UI.Tests` with an extension-pack fixture. Standalone: `PEMS.Web` does not reference them yet, so no page changes.
- ADR-0007 (proposed): static Razor components with a Tag Helper facade, with the measured comparison.

## 2026-10-27 - Password history (SCRUM-412)

### Added
- A new password must differ from the current one and from the last N (`Authentication__Password__HistoryCount`, default 5, 0 = off) on every path that sets a password: forced first change, voluntary change, administrator reset, second-administrator reset, recovery code and break-glass. Replaced hashes are kept in the new table `PasswordHistory` (migration `PasswordHistory`, hashes only). Message in fa and en.
- README manual check M-19; `PasswordHistoryTests`.

## 2026-10-27 - Offline PDF viewers and an external-URL check (SCRUM-20)

### Changed
- The PDF viewers of Engineering Documents and VDR load pdf.js 4.0.379 from `wwwroot/lib/pdfjs` through `wwwroot/js/pdfjs-loader.js` instead of cdnjs.cloudflare.com (the product is offline).

### Added
- `OfflineAssetsTests`: no view, component, script or stylesheet may reference an external host (XML namespaces and one placeholder text are allow-listed); `tools/UiAudit/pattern-audit.py` now exits with an error on a CDN reference.
- README manual check M-17.

## 2026-10-27 - Migration chain gate (SCRUM-413)

### Added
- Workflow `migration-chain.yml` and script `scripts/ci/test-migration-chain.sh`: the migration chain is applied to a real SQL Server (empty install, upgrade from `origin/main`, model agrees) and a failure names the migration.
- `AccountSafetyOfMigrationsAndPipelinesTests`: no migration, workflow or deploy script touches accounts or passwords.
- Guideline chapter 01 rules (a model change ships its migration; the gate before staging/demo), `CODE_FIRST_DATABASE.md` section, `DEPLOYMENT_README.md` note, manual check M-15.

## 2026-10-27 - Deploys and updates never reset the administrator password (SCRUM-410)

### Added
- `AdminPasswordSurvivesUpdateTests`: the start-up seeder changes nothing when users exist and the development reset needs Development plus its variable.
- Rule in guideline chapter 01 (security), `DEPLOYMENT_README.md` section 9, a note in `LINUX_VM_DEPLOYMENT.md` and manual check M-14.

## 2026-10-27 - VDR list with the Engineering Documents look (SCRUM-409)

### Added
- VDR list: quick search (document number, description, vendor) and an Excel export of the filtered list (`VDR/Export`, same columns as the table, capped at 20,000 rows).

### Changed
- VDR list: same frame as Engineering Documents - full-page table, one icon toolbar, project/vendor/status filters in a dropdown with a count badge and chips, KPI chips and the vendor delay/risk view hidden until the KPI button is pressed (remembered per browser).

## 2026-10-26 - Architecture decision log (SCRUM-14)

### Added
- `plan/adr`: ADR template, index and six accepted ADRs (hybrid storage, Razor + PEMS.UI library, code-first database, env/secrets files, licensing threat model and VM policy, demo/staging branch policy), linked from the docs that explain them.

## 2026-10-26 - Engineering Documents list with the correspondence look (SCRUM-408)

### Changed
- Engineering Documents list: same frame as the correspondence list - the table fills the window (sticky header, scrolls inside, pager at the bottom), one icon toolbar (title with count, search over the title, filters, KPIs, Excel, reset columns, New), filters in a dropdown with a count badge and chips instead of the side panel, KPI chips hidden by default and shown on demand (remembered per browser).
- `pems-letter-index.css` gained a generic full-page frame `.lp-page` (the cartable frame without the folder column) and `wwwroot/js/pems-list-page.js` (search open/close, KPI toggle) for list pages that follow this look.

## 2026-10-26 - Searchable multi-select for user roles (SCRUM-407)

### Added
- Tom Select 2.6.2 (Apache-2.0) vendored offline in `PEMS.Web/wwwroot/lib/tom-select` (complete build, Bootstrap 5 theme, source maps, licence).
- `wwwroot/js/pems-multiselect.js` + `wwwroot/css/pems-multiselect.css`: any `<select multiple data-pems-multiselect>` becomes a searchable multi-select with removable chips and option groups, in the PEMS themes and in RTL/LTR; the list opens on <body> so cards cannot clip it.

### Changed
- Admin > Users create/edit: the role checkboxes are one grouped multi-select (responsible, expert, custom roles) with search. The posted field (`SelectedRoles`) is unchanged and works without JavaScript. The old `_RoleCheckboxes` partial moved to `legacy-trash/views`.

## 2026-10-26 - Administrator recovery: one-time recovery codes and server break-glass (SCRUM-355, SCRUM-356)

### Added
- One-time recovery codes for administrators (migration `AdminRecoveryCodes`): 10 codes per set, 80 bits each, stored only as PBKDF2 hash + salt, append-only rows (use/revoke recorded once). User menu > Recovery codes shows how many are left and generates a new set (current password required; unused old codes are revoked); the codes are shown once (no-store) with copy, print and download.
- `/AccountRecovery` (link on the sign-in page): user name + recovery code + new password. The code is consumed once, the password is set, MFA is reset for re-enrolment, all sessions are revoked, the event is audited and all administrators are notified. Wrong codes count toward the Identity lockout; a locked account is refused even with a valid code.
- Rate limit for the sign-in-free recovery pages: 5 POSTs per client IP per 15 minutes (`Security:Recovery:RateLimit:*`), answer 429.
- Server break-glass (`/break-glass`): placing `pems-recovery.request` in `Security:BreakGlass:RequestFolder` opens the page for 15 minutes, reachable only from localhost or `Security:BreakGlass:ManagementNetworks` (every X-Forwarded-For/Forwarded hop must be allowed too); anything else is 404. One administrator is reset (temporary password shown once, forced change, MFA reset); the file is deleted after use or on expiry.
- Optional vendor-signed recovery token (ECDSA P-256, at most 24 hours, bound to a machine fingerprint, single use), off by default (`Security:BreakGlass:VendorRecovery:Enabled=false`).

### Changed
- The MFA reset and unlock step of the second-admin reset moved into a shared helper used by all three recovery paths.

### Notes
- The first-run wizard (SCRUM-188) does not exist yet; it will call the same recovery-code service. Until a licence engine exists, the vendor-token machine fingerprint is a setting.

## 2026-10-27 - Branch hygiene and local gates (SCRUM-394)

### Changed
- Guideline: weekly branch audit and rules for In Review/Done branches that are not on `main` (chapter 00 section 4a, chapter 05), no direct Done, restart cloud sessions from `main`, local gates and the verified cloud-session toolchain (SDK, `dotnet-ef --tool-path`, drift gate) in chapter 05; chapter 07 now expects cloud authors to run everything except the SQL Server tests.

## 2026-10-26 - QA check in the UI, merge before In Review (SCRUM-394)

### Changed
- Workflow (owner decision): the PR is merged into `main` right before the issue moves to In Review (label `qa-pending`); In Review means merged and waiting for the QA check; Done means the QA check in the UI passed.
- Entering or re-entering In Review requires a QA checklist comment (how to test by README references) and a Tests paragraph, re-checked and updated after every failed QA (chapter 07 section 1a).
- A failed QA check sends the issue back to To Do (label `qa-failed`, defects in a comment, assignee kept).
- Tests and review are one step for now: the local test run is line 1 of every QA checklist, an issue may move to In Review with tests listed as not run, Done needs the tests green and the QA check passed.
- Manual checks and local tests are documented in the existing READMEs (root `README.md` "Local tests" and "Manual checks in the UI", `env/README.md` "Verifying settings", `docs/deployment/DEPLOYMENT_README.md` section 8); QA checklists refer to them and stay on the Jira issue after Done.
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

## 2026-10-25 - Administrator recovery: audit trail, last-admin guard, second-admin reset (SCRUM-357, SCRUM-354)

### Added
- `SecurityEvents` table (migration `SecurityEventsRecoveryAudit`): append-only record of every administrator recovery attempt (actor, target, method, IP, UTC time, result, fixed failure-reason code); only the SIEM marker `ForwardedToSiemAt` may change. Read-only list at Admin > Security Events (fa/en).
- `IRecoveryEventService`: one service for all recovery paths. A successful recovery revokes all sessions and remember-me cookies of the target (security stamp), writes the audit record, notifies every active SystemAdmin and logs under `PEMS.Security`; a failure only audits.
- Last-admin guard: the last active SystemAdmin cannot be deactivated or removed from the role (checked on the server in the user edit and activate/deactivate actions).
- Second-admin reset (Admin > Users, key button on other administrators): random temporary password shown once and valid for 24 hours, forced password change, lockout cleared, passkeys and authenticator key removed, `MfaEnrollmentRequired` set (migration `AdminRecoveryUserFlags`). Resetting your own account is refused. An expired temporary password is refused at sign-in with the generic message.

### Changed
- A password change of an administrator in the user edit form is audited (method `AdminConsole`) and its Identity result is no longer ignored.

### Notes
- `MfaEnrollmentRequired` is only a flag until MFA at sign-in exists (SCRUM-188 / SCRUM-206).
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
