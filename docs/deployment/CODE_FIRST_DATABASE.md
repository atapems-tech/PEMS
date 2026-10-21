# Code-first database

The database is built entirely from code. An empty SQL Server login is enough to get a working PEMS: no
`InitialCreate.sql`, no `dotnet ef database update`, no SSMS script.

## What happens at startup

`PEMS.Web/Program.cs` calls `PEMS.Persistence.Initialization.DatabaseInitializer` twice:

1. **Before any service connects** - `EnsureDatabaseExistsAsync` creates the database named in
   `ConnectionStrings:PEMSConnection` if it is missing (connects to `master`). If the login cannot create
   databases, a warning is logged and startup continues (the database may already exist, e.g. created in a
   hosting panel). This runs first because Hangfire installs its own schema as soon as it is configured.
2. **After the host is built, before the seeders** - `MigrateAsync` applies every pending EF Core migration
   and then runs the embedded `PEMS.Persistence/Scripts/EnableFullTextSearch.sql` (idempotent; skipped with a
   warning when SQL Server Full-Text Search is not installed - search falls back to `LIKE`).

The seeders already in `Program.cs` then run against the finished schema.

## Settings (`appsettings*.json`, section `Database`)

| Key | Default | Meaning |
|---|---|---|
| `CreateIfMissing` | `true` | create the database when absent |
| `MigrateOnStartup` | `true` | apply pending migrations at startup |
| `ApplyFullTextSearch` | `true` | create the full-text catalog and indexes |
| `CommandTimeoutSeconds` | `600` | timeout while migrating (the first run builds the whole schema) |

Environment variable form: `Database__MigrateOnStartup=false`.

## Production note

Earlier deployment documents describe a reviewed-script flow (`deploy.ps1 -Action MigrationScript` / `Migrate`)
and "deployment never migrates". That flow still works: set `Database__MigrateOnStartup=false` and
`Database__CreateIfMissing=false` for an environment that must apply schema changes through a reviewed script.
Take a backup before upgrading a database that holds real data; an application rollback does not undo a
migration.

## Rules for developers

- Change the model, then add a migration:
  `dotnet ef migrations add <Name> --project PEMS.Persistence --startup-project PEMS.Web`
- Never edit an applied migration; always add a new one.
- CI fails (`Model drift gate` in `build.yml`) when an entity change has no migration, and EF Core refuses to
  migrate a drifted model at startup.
- Reference data belongs in the seeders (`PEMS.Persistence/Seed`), not in hand-run SQL.
