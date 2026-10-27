# PEMS

Enterprise engineering and maintenance platform for oil, gas and petrochemical projects: engineering documents
(EDMS/VDR), CMMS/EAM, permit to work, inventory and procurement, production, reliability (RCM/FMEA), workflows,
and local AI (Ollama, OCR). ASP.NET Core MVC on .NET 10 with EF Core and SQL Server. Persian-first UI (RTL) with
English; AI runs on-premise and nothing leaves the organization.

## Stack

| | |
|---|---|
| Runtime | .NET 10, ASP.NET Core MVC/Razor, SignalR |
| Data | EF Core 10 on SQL Server, code-first (migrations), Hangfire jobs on the same database |
| Auth | ASP.NET Identity, FIDO2 step-up, role and permission based access (RBAC/ABAC) |
| AI | Ollama (local LLM), Tesseract OCR |
| Other | QuestPDF, Serilog, optional Redis cache |
| Tests | xUnit (`PEMS.Tests`) |

## Solution layout

| Project | Responsibility |
|---|---|
| `PEMS.Domain` | entities and enums, no dependencies |
| `PEMS.Application` | DTOs, interfaces, application services |
| `PEMS.Persistence` | `PEMSDbContext`, configurations, migrations, seeders, repositories, database installer/updater |
| `PEMS.Migrations` | EF Core migrations, model snapshot and design-time factory (kept apart so `PEMS.Persistence.dll` changes only with its own code) |
| `PEMS.Infrastructure` | cache, external integrations |
| `PEMS.AI` | Ollama, OCR, document parsing, extraction |
| `PEMS.WorkflowPlatform` | central workflow engine |
| `PEMS.Web` | MVC controllers, views, `Admin` and `Warehouse` areas |
| `PEMS.Shared` | shared helpers |
| `PEMS.Tests` | unit and integration tests |

Other folders: `docs/` (architecture, audits, deployment, per-module specs), `deploy/` (Docker deployment kit and
scripts), `scripts/` (operator data-repair and report tools), `plan/` (backlog and roadmap workbook),
`env/` (settings and secret templates), `legacy-trash/` (retired material kept for reference), `tools/`, `dev/`.

## Settings and secrets

All settings live in `env/` (see [env/README.md](env/README.md)); `appsettings.json` is intentionally empty and the
code has no hard-coded environment settings.

| File | Content | In git |
|---|---|---|
| `env/<name>.env` | non-secret settings | yes |
| `env/<name>.secrets.env` | connection string, admin password, FTP/Redis credentials | no (git-ignored) |
| `env/<name>.secrets.env.example` | template of the secret keys | yes |

Environments: `local`, `docker`, `demo`, `uat`, `production-kangan`. `PEMS_ENV` selects one (Development means `local`);
real environment variables override the files.

## Run locally

Requirements: .NET 10 SDK and a SQL Server instance (LocalDB, SQL Express or a container).

```bash
cp env/local.secrets.env.example env/local.secrets.env    # edit the connection string and the admin password
dotnet run --project PEMS.Web
```

The app listens on `http://localhost:5089`. On an empty or missing database the first start creates it, installs the
full schema and runs the seeders, which takes a few minutes. A System Administrator is created on an empty database
from `Seed__Admin__UserName/Email` (settings file) and `Seed__Admin__Password` (secrets file); change the password at
first sign-in. Local AI needs Ollama (`Ollama__Url`).

### Docker

```bash
cp env/docker.secrets.env.example env/docker.secrets.env   # choose the sa and admin passwords
docker compose --env-file env/docker.secrets.env up --build
```

Starts `pems-web` (port 8080), SQL Server and Ollama, all published on `127.0.0.1` only. Check: `docker compose --env-file env/docker.secrets.env config` lists the three ports as `127.0.0.1:...`, and `docker compose ps` shows `127.0.0.1:1433->1433/tcp`. See
[docs/deployment/LINUX_VM_DEPLOYMENT.md](docs/deployment/LINUX_VM_DEPLOYMENT.md) for the Linux VM kit and
[docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md](docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md) for UAT.

## Database (code-first)

The schema is built and upgraded only from code. No hand-run SQL is needed.

- First run on an empty database installs everything; later releases apply only the missing migrations.
- A schema that has tables but no migration history is reported and never changed automatically.
- Admin > System Monitoring > **Database Update** shows the state and applies updates on demand.
- `GET /health/database` returns 200 only when every migration is applied.
- Settings (`Database__*` in the env files): `CreateIfMissing`, `MigrateOnStartup`. Set `Database__MigrateOnStartup=false` for
  environments that want reviewed scripts (`deploy/deploy.ps1 -Action MigrationScript`).

Add a migration after changing the model; never edit an applied one:

```bash
dotnet ef migrations add <Name> --project PEMS.Migrations --startup-project PEMS.Web
```

Details: [docs/deployment/CODE_FIRST_DATABASE.md](docs/deployment/CODE_FIRST_DATABASE.md).

## Tests

### Local tests (line 1 of every QA checklist; run them before the merge when you can)

```powershell
dotnet --version                                   # 10.x
dotnet build PEMS.slnx -c Release                  # compile check
dotnet test PEMS.Tests --filter "FullyQualifiedName~EnvironmentFilesTests|FullyQualifiedName~TemporaryPasswordGeneratorTests"   # database-free, seconds
dotnet build tools/LoadTestSeed -c Release        # the tool is not part of the solution
dotnet test PEMS.slnx                              # full suite
```

Many tests need a real SQL Server and use the isolated database `PEMSDB_RegressionTest` (never `PEMSDB`). Set
`PEMS_TEST_CONNECTION` to use another server, for example LocalDB. On a clean `main` three tests are known to fail (plan row #1):
the LF check in `LinuxDeploymentArtifactsTests`, a SQLEXPRESS-only sidebar test and `ImportCenterNavigationTests`; any other red test is new.
Put the counts (passed, failed, skipped) in the Jira report.

### Manual checks in the UI

Used by the QA check ([guideline chapter 07](docs/guidelines/07-qa-check-in-the-ui.md)); a task that adds a manual check adds it here (or to the README named in the last column), never to a new file.

| ID | Check | Steps | Expected result | Details |
|---|---|---|---|---|
| M-1 | One-time password for users created from an employee | Sign in as a System Administrator, Employees > add an employee, tick create user, save. Add a second one. Sign in as the first new user. | The success message shows a random 16-character one-time password once; a reload does not show it again; the two users get different passwords; the new user must change the password at first sign-in. | - |
| M-2 | Password policy | Admin > Users > create or edit a user with a password one character shorter than `Authentication__Password__RequiredLength`, then with the exact length. | The short password is rejected with the policy message, the exact one is accepted. Production env files require 12, the others 8. | [env/README.md](env/README.md) "Verifying settings" |
| M-3 | Load-test seed refuses to run without a password | `dotnet run --project tools/LoadTestSeed` with neither `PEMS_LOADTEST_PASSWORD` nor `LoadTest__Password` in `env/local.secrets.env`. | It exits with "Set PEMS_LOADTEST_PASSWORD or LoadTest__Password ..." and creates nothing. With the value set (local database only) it creates `loadtest*` users; deactivate them afterwards in Admin > Users. | - |
| M-4 | Docker ports are loopback only | See the Docker section above. | All three ports show `127.0.0.1`. | - |
| M-5 | Default-credential exposure report | Run `scripts/security/Report-DefaultCredentialExposure.sql` on the database. | Only accounts you recognise, with changed passwords. | [docs/deployment/CREDENTIAL_ROTATION_RUNBOOK.md](docs/deployment/CREDENTIAL_ROTATION_RUNBOOK.md) |
| M-6 | A changed setting takes effect | Cookie, HSTS, migration timeout, password policy. | See the table in [env/README.md](env/README.md) "Verifying settings". | env/README.md |
| M-7 | A deployed environment works | Health, sign-in, HSTS header, smoke pages. | See [docs/deployment/DEPLOYMENT_README.md](docs/deployment/DEPLOYMENT_README.md) section 8. | DEPLOYMENT_README.md |
| M-8 | Last-admin guard and second-admin reset (SCRUM-354, SCRUM-357) | With two active System Administrators A and B: as A, Admin > Users > deactivate B, then try to deactivate or demote A (now the only one). Re-activate B. As A, click the key button on B > Reset account. Sign in as B with the temporary password; wait 24 h (or set the expiry in the past on a test database) and try again. Try the key button on A's own row. Open Admin > Security Events. | Deactivating/demoting the last active administrator is refused. The reset shows a 16-character temporary password once (not after reload); B must change it at sign-in, B's other sessions end within about a minute, every active administrator gets a notification; an expired temporary password is refused with the normal sign-in message; resetting your own account is refused. Security Events lists every attempt (success and failure) with method, actor, target and IP. | - |
| M-9 | One-time recovery codes (SCRUM-355) | As a System Administrator: user menu > Recovery codes > enter the current password > Generate new codes; try copy, print, download; reload. Sign out, sign-in page > Use a recovery code: user name + one code + new password. Repeat with the same code, then with a code from an older set after generating a new one. After 15 minutes (every POST counts toward the per-IP limit), enter wrong codes until the account locks, then a valid one, then keep posting. | Ten codes are shown once (gone after reload) and the page asks for the current password. A valid code sets the new password once (the page shows how many codes are left); the same code, an old-set code and wrong codes get the same generic message; after the lockout limit even a valid code is refused until the lockout ends; after 5 POSTs from one IP within 15 minutes the page answers 429. Each attempt is in Admin > Security Events (method RecoveryCode). | Settings: [env/README.md](env/README.md) (`Security__Recovery__RateLimit__*`) |
| M-10 | Server break-glass recovery (SCRUM-356) | On the server open `/break-glass` (no request file). Put an empty file `pems-recovery.request` into the folder of `Security__BreakGlass__RequestFolder` (default `break-glass` next to the application) and reload; reset one administrator; reload again. Repeat with a new file and open the page from a PC outside `Security__BreakGlass__ManagementNetworks`; and once more, waiting 16 minutes before resetting. | Without the file, from another network, after use or after 15 minutes the page is a 404 (the file is gone afterwards). With the file, from the server itself, the page lists the active administrators; the reset shows a temporary password once, forces a password change and is recorded in Security Events (method BreakGlass, actor server-operator). The vendor-token field never appears unless `Security__BreakGlass__VendorRecovery__Enabled=true`. | - |
| M-11 | Searchable multi-select for user roles (SCRUM-407) | Admin > Users > New user (and Edit): click Roles, type part of a role name (for example `mnt`), tick several roles, remove one with its ×, save; open Edit again. Repeat in en and fa, dark theme, 375 px. | The list filters while typing and keeps the groups (responsible, expert, custom); the chosen roles appear as chips; after saving, Edit shows exactly the saved roles; the list is not cut off by the form card; no request leaves the server (Network tab). | - |
| M-12 | Engineering Documents list has the correspondence look (SCRUM-408) | Open Documents > Documents next to Correspondence. Use search (magnifier), filters (funnel: discipline, status, area), the KPI button, Excel and reset columns; remove a filter chip; page through. Repeat in en and fa, dark theme, 375 px. | The table fills the page with a sticky header and the pager at the bottom, like Correspondence; one icon toolbar; filters open in a dropdown and show a count badge and chips; the KPI row is hidden until the KPI button is pressed and stays as chosen after reload; search opens over the title. | - |
| M-13 | VDR list has the Engineering Documents look (SCRUM-409) | Open Documents > Vendor documents. Search a document number, a description word and a vendor name; filter by project, vendor and status (funnel); remove a chip; press the KPI button and open the vendor risk view; export Excel with a filter on; page through. Repeat in en and fa, dark theme, 375 px. | Same frame and toolbar as Engineering Documents; overdue rows are red; the badge and chips match the filters; KPIs are hidden until pressed and stay as chosen after reload; the xlsx holds only the filtered rows. | - |
| M-14 | An update keeps the administrator password (SCRUM-410) | On a staging, demo, local or test-client environment: sign in as the System Administrator with the password you chose after first install (or change it once). Deploy the next version (push to `staging`, or run the client update kit with `deploy.ps1`), wait for the health check. Sign in again with the same password. Also restart the application without changing anything and sign in again. | The sign-in works with the chosen password and does not ask for a password change. The seed password from `*.secrets.env` is refused. Admin > Security Events shows no reset for the administrator. Only after a fresh install or an explicit database reset does the first-install flow (seed password, forced change) start again. | [docs/deployment/DEPLOYMENT_README.md](docs/deployment/DEPLOYMENT_README.md) section 9 |
| M-15 | Migration chain gate (SCRUM-413) | GitHub > Actions > *PEMS Migration Chain Gate* > Run workflow on the branch to check, `base_ref` = `origin/main`. For the negative case run it on a branch whose newest migration contains `migrationBuilder.Sql("SELECT 1/0");`. | The run installs the chain on an empty SQL Server, upgrades from the last migration of `origin/main`, and ends green. The broken branch fails and the log names the migration (`Applying migration '...'`). | [docs/deployment/CODE_FIRST_DATABASE.md](docs/deployment/CODE_FIRST_DATABASE.md) "Migration chain gate" |
| M-16 | Upgrade from every supported release, backup before update (SCRUM-280) | (a) GitHub > Actions > *PEMS Migration Chain Gate* > Run workflow on `main`; open the step "Upgrade from every supported release". (b) On a copy of a database that is behind (or a local DB after `dotnet ef database update <older migration>`), start the app with `$env:Database__BackupBeforeUpdate="true"`, open Admin > Database Update and apply; then once with `$env:Database__BackupDirectory="C:\pems-missing"`. (c) Restore the .bak with the commands in CODE_FIRST_DATABASE.md "Rollback". | (a) green; the log lists each release with "N/M tables hold data" and no schema diff. (b) the page shows the automatic-backup notice instead of the checkbox; the run row shows the `.bak` path and the file exists; with the missing folder the update is refused with "pre-update backup failed" and the state stays "Update available". (c) after the restore the page shows the pre-update migration again. | - |
| M-17 | PDF viewers work offline (SCRUM-20) | Open an Engineering Document and a VDR submission that have a PDF attached. In the browser DevTools > Network set *Offline* (or unplug the network), reload each page. | The PDF page renders on the canvas, the pins and page buttons work, the Network tab lists no request to another host, the console shows no error. | [PEMS.Web/wwwroot/lib/pdfjs/README-PEMS.txt](PEMS.Web/wwwroot/lib/pdfjs/README-PEMS.txt) |
| M-18 | Password history (SCRUM-412) | Admin > Users > Reset password for a test user (P1). Sign in as that user: the forced change asks for a new password; enter the same P1, then P2. Change to P3, then try P1 and P2 again; repeat the same attempt after an administrator reset and in fa and en. | The forced change refuses P1 ("must differ from your current password and your last 5 passwords"), accepts P2; P1 and P2 are refused later; after more than 5 changes the oldest becomes allowed again; the reset path refuses a password the user had; nothing else about the sign-in changes. | [env/README.md](env/README.md) "Verifying settings" |

## CI and demo

- Only pushes to `demo` and `staging` start a pipeline; `main` and working branches run nothing.
- `.github/workflows/deploy-site.yml`: the reusable pipeline (restore, build, model-drift gate, tests, publish, FTP upload, wait for the database to report up to date).
  `deploy-demo.yml` (push to `demo`) and `deploy-staging.yml` (push to `staging`) call it; secrets are `DEMO_*` and `STAGING_*`.
- `.github/workflows/build.yml`: manual build and test run (`workflow_dispatch`).
- `.github/workflows/deploy-uat.yml`: manual UAT deployment to Azure Container Apps.

## Conventions

- Business logic lives in `PEMS.Application` and `PEMS.Persistence`, never in controllers.
- Code, identifiers and documentation are in English; domain comments may be Persian.
- New entities inherit `BaseEntity` when possible; every schema change comes with a migration.
- No hard-coded settings in code: add each new setting to every `env/*.env` file and read it with `Required*`; secrets only in `env/*.secrets.env` or environment variables.

## More

- [CHANGELOG.md](CHANGELOG.md) - what changed and when.
- [docs/deployment/DEPLOYMENT_README.md](docs/deployment/DEPLOYMENT_README.md) - demo and staging: subdomains, databases, secrets, GitHub Actions, manual deploy and reset.
- `plan/PEMS_Backlog_Roadmap.xlsx` - backlog, roadmap, test plan, supportability matrix; `plan/backlog.csv` is its text mirror.
- [docs/design/COMPONENT_LIBRARY_PLAN.md](docs/design/COMPONENT_LIBRARY_PLAN.md) - UI component library plan; [docs/design/UI_PATTERN_AUDIT.md](docs/design/UI_PATTERN_AUDIT.md) - audit of all views.
- [docs/process/PLAN_TRACKER_WORKFLOW.md](docs/process/PLAN_TRACKER_WORKFLOW.md) - how the plan file, Jira and developers stay consistent (`tools/plan-sync`).
- `docs/` - architecture notes, audits and module specifications.
