# Code-first database

The database is built and upgraded entirely from code. An empty SQL Server login is enough to get a working PEMS:
no `InitialCreate.sql`, no `dotnet ef database update`, no SSMS script. The old hand-run SQL is kept for reference
in `legacy-trash/manual-sql/` (see its README); nothing there is executed.

## First run vs update

`IDatabaseUpdateService` (`PEMS.Persistence/Servises/DatabaseUpdateService.cs`) looks at the migration history and
decides what the next run is:

| State | Meaning | Applied automatically? |
|---|---|---|
| `FirstRun` | no history, no tables: empty database | yes - installs the whole schema |
| `UpdateAvailable` | history exists, shipped migrations are missing | yes - applies only the missing ones |
| `UpToDate` | every shipped migration is applied | nothing to do |
| `UnmanagedSchema` | tables exist but there is no history (not created by this migrator) | **never** - reported, left untouched |
| `DatabaseNewerThanApplication` | history holds migrations this build does not know | **never** - deploy the matching/newer build |
| `Unreachable` | server or database cannot be reached | no |

Every attempt is written to `DatabaseUpdateRuns` (trigger, mode, from/to migration, result, error, who).

## Where it runs

1. **Startup** (`PEMS.Web/Program.cs`, `DatabaseInitializer`):
   - `EnsureDatabaseExistsAsync` creates the database if it is missing, before anything connects (Hangfire installs
     its own schema as soon as it is configured). If the login cannot create databases a warning is logged and
     startup continues (the database may be pre-created in a hosting panel).
   - `MigrateAsync` runs the service (`FirstRun` -> install, `UpdateAvailable` -> update) before the seeders.
     A failed install/update stops startup with the error; `UnmanagedSchema` / `DatabaseNewerThanApplication`
     only log a warning.
2. **Admin > System Monitoring > Database Update** (`/Admin/DatabaseUpdate`, role `SystemAdmin`): shows the state,
   pending migrations and recent runs, and applies the install/update on demand (backup confirmation required).
3. **`GET /health/database`** (anonymous, no details): `200` + `UpToDate`, otherwise `503` + the state name. Used by
   the demo pipeline to wait for the install/update to finish.

## Settings (`Database__*` in `env/<name>.env`)

| Key | Default | Meaning |
|---|---|---|
| `CreateIfMissing` | `true` | create the database when absent |
| `MigrateOnStartup` | `true` | install/update automatically at startup |

Environment variable form: `Database__MigrateOnStartup=false`. With it off, the schema changes only from the
Admin page (or an external process); earlier docs describe the reviewed-script flow (`deploy.ps1 -Action
MigrationScript` / `Migrate`) which still works for such environments. Take a backup before updating a database that
holds real data; an application rollback does not undo a migration.

## Full-text search

It is the migration `FullTextSearch` (full-text objects cannot run in a transaction, so it is non-transactional).
When SQL Server Full-Text Search is not installed the statements do nothing and search falls back to `LIKE`.
Install the feature **before** the first run: an applied migration is not re-run.

## Demo pipeline

`.github/workflows/deploy-demo.yml` (push/merge to `demo`): build, model-drift gate, tests, publish, inject the demo
connection string and `Database` settings, FTP upload, then poll `/health/database` until `UpToDate`
(first deploy = install, later deploys = update).

## Rules for developers

- Change the model, then add a migration:
  `dotnet ef migrations add <Name> --project PEMS.Persistence --startup-project PEMS.Web`
- Never edit an applied migration; always add a new one (also for data fixes and non-model DDL such as the
  full-text objects: use `migrationBuilder.Sql`).
- CI fails (`Model drift gate`) when an entity change has no migration, and EF Core refuses to migrate a drifted
  model at startup.
- Reference data belongs in the seeders (`PEMS.Persistence/Seed`), not in hand-run SQL.
- Read-only diagnostics and data-repair tools under `scripts/` are operator tools, not schema setup.
