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

Starts `pems-web` (port 8080), SQL Server and Ollama. See
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

```bash
dotnet test PEMS.slnx
```

Many tests need a real SQL Server and use the isolated database `PEMSDB_RegressionTest` (never `PEMSDB`). Set
`PEMS_TEST_CONNECTION` to use another server, for example LocalDB.

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
