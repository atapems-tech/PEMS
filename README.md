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
| `PEMS.Infrastructure` | cache, external integrations |
| `PEMS.AI` | Ollama, OCR, document parsing, extraction |
| `PEMS.WorkflowPlatform` | central workflow engine |
| `PEMS.Web` | MVC controllers, views, `Admin` and `Warehouse` areas |
| `PEMS.Shared` | shared helpers |
| `PEMS.Tests` | unit and integration tests |

Other folders: `docs/` (architecture, audits, deployment, per-module specs), `deploy/` (Docker deployment kit and
scripts), `scripts/` (operator data-repair and report tools), `plan/` (backlog and roadmap workbook),
`legacy-trash/` (retired material kept for reference), `tools/`, `dev/`.

## Run locally

Requirements: .NET 10 SDK and a SQL Server instance (LocalDB, SQL Express or a container).

```bash
dotnet run --project PEMS.Web
```

The app listens on `http://localhost:5089`. The default connection string in `PEMS.Web/appsettings.json` targets
`localhost\SQLEXPRESS`, database `PEMSDB`; override it without editing the file:

```bash
ConnectionStrings__PEMSConnection="Server=(localdb)\MSSQLLocalDB;Database=PEMSDB;Trusted_Connection=True;TrustServerCertificate=True" dotnet run --project PEMS.Web
```

On an empty or missing database the first start creates it, installs the full schema and runs the seeders, which
takes a few minutes. When no user exists a default System Administrator is seeded; change its password at first
sign-in. Local AI needs Ollama (`Ollama:Url`, default `http://localhost:11434`).

### Docker

`docker-compose.yml` starts `pems-web` (port 8080), SQL Server and Ollama. See
[docs/deployment/LINUX_VM_DEPLOYMENT.md](docs/deployment/LINUX_VM_DEPLOYMENT.md) for the Linux VM kit and
[docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md](docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md) for UAT.

## Database (code-first)

The schema is built and upgraded only from code. No hand-run SQL is needed.

- First run on an empty database installs everything; later releases apply only the missing migrations.
- A schema that has tables but no migration history is reported and never changed automatically.
- Admin > System Monitoring > **Database Update** shows the state and applies updates on demand.
- `GET /health/database` returns 200 only when every migration is applied.
- Settings (`Database` section): `CreateIfMissing`, `MigrateOnStartup`. Set `Database__MigrateOnStartup=false` for
  environments that want reviewed scripts (`deploy/deploy.ps1 -Action MigrationScript`).

Add a migration after changing the model; never edit an applied one:

```bash
dotnet ef migrations add <Name> --project PEMS.Persistence --startup-project PEMS.Web
```

Details: [docs/deployment/CODE_FIRST_DATABASE.md](docs/deployment/CODE_FIRST_DATABASE.md).

## Tests

```bash
dotnet test PEMS.slnx
```

Many tests need a real SQL Server and use the isolated database `PEMSDB_RegressionTest` (never `PEMSDB`). Set
`PEMS_TEST_CONNECTION` to use another server, for example LocalDB.

## CI and demo

- `.github/workflows/build.yml`: restore, build, model-drift gate, tests.
- `.github/workflows/deploy-demo.yml`: push or merge to the `demo` branch builds, publishes and uploads to the demo
  subdomain by FTP, then waits for the database to report up to date (secrets are listed in the file header).
- `.github/workflows/deploy-uat.yml`: manual UAT deployment to Azure Container Apps.

## Conventions

- Business logic lives in `PEMS.Application` and `PEMS.Persistence`, never in controllers.
- Code, identifiers and documentation are in English; domain comments may be Persian.
- New entities inherit `BaseEntity` when possible; every schema change comes with a migration.
- Secrets never go into the repository; use environment variables or the CI secret store.

## More

- [CHANGELOG.md](CHANGELOG.md) - what changed and when.
- `plan/PEMS_Backlog_Roadmap.xlsx` - backlog, roadmap, test plan, supportability matrix.
- `docs/` - architecture notes, audits and module specifications.
