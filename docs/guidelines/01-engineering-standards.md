# 01 - Engineering standards (read for every task)

Origin of each rule is given in brackets: `R<n>` = Instructions sheet rule n in `plan/PEMS_Backlog_Roadmap.xlsx`.

## 1. Architecture

Clean Architecture; dependencies point inward.

| Project | Responsibility |
|---|---|
| `PEMS.Domain` | entities, enums, `BaseEntity`; **no dependencies** |
| `PEMS.Application` | DTOs (`DTOs/`), interfaces (`Interfaces/`), application services (`Services/`) |
| `PEMS.Persistence` | `PEMSDbContext`, EF configurations (`Configurations/`), seeders, repositories, installer/updater |
| `PEMS.Migrations` | EF migrations, model snapshot, design-time factory (references Persistence; Web and Tests reference it; the context defaults `MigrationsAssembly` to it) |
| `PEMS.Infrastructure` | cache, external integrations |
| `PEMS.AI` | Ollama, OCR, parsing, extraction (local only) |
| `PEMS.WorkflowPlatform` | the one workflow engine |
| `PEMS.Web` | MVC controllers, views, areas `Admin`, `Warehouse` |
| `PEMS.Shared` | shared helpers |
| `PEMS.Tests` | unit + integration tests |

Rules:

- **Business logic never in controllers or views.** Controllers are thin: bind, authorize, call a service, map to a view model.
- **Engines once** [R3]: metadata, forms, workflow, access/RLS, events, reporting and licensing each have exactly one implementation. Modules call it. Parallel concepts (a second workflow, a second permission model, a second grid) are forbidden.
- **No new static modules** [R4]: a new module starts as metadata (form/view/workflow). If impossible, write an ADR ([plan/adr](../../plan/adr/README.md)) stating the reason and the missing extension point *before* building.
- **Extend, don't duplicate.** Inspect existing entities and controllers first; follow existing folder structure and naming exactly. (Example: Equipment -> Instrument hierarchy is extended, not paralleled.)
- **Central RLS** [R12]: every data path (list, search, report, export, API, AI) goes through the central row-level filter. No direct role checks.
- **Go forward, not backward-compatible** [R6]: no compatibility shims unless a live customer needs one.
- **No customer-specific forks** [R17]: differences live in licence, configuration, customer metadata packages and extension packs only.

## 2. Code style

- Match the surrounding code: naming, comment density, idiom. Read two neighbouring files before writing a new one.
- English identifiers; C# naming per .NET conventions; one type per file; async all the way (`Async` suffix, pass `CancellationToken` in service methods that do I/O).
- Domain entities inherit `BaseEntity` (`CreatedBy`, `ModifiedBy`, `RowVersion`, `TenantId`) unless there is a stated reason. Soft delete via `IsActive` where the module uses it. **History records (calibration, maintenance, audit) are append-only - never hard-delete or overwrite.**
- Lists use `PagedResult<T>` and one server-side paging contract (Lessons 4). Prefer enums or lookup tables over free strings.
- Persian XML comments on domain entities explaining business/ISO rules are the house style; everything else is English.
- Comments explain *why*, not *what*. No commented-out code, no TODO without a Jira key.
- Fix clearly poor code you pass through, within scope [R15].
- Dead or superseded files go to `legacy-trash/`, not deleted, until the owner agrees [R23].

## 3. Database [R6, R23]

- **Schema only through migrations.** `dotnet ef migrations add <Name> --project PEMS.Migrations --startup-project PEMS.Web`. Never edit an applied migration; add a new one. DDL EF cannot model goes into `migrationBuilder.Sql`.
- The app applies migrations itself (startup migrator / Admin > Database Update). No hand-run SQL for schema setup or update.
- CI "model drift" gate: `dotnet ef migrations has-pending-model-changes` must be clean - an entity change without a migration fails the build.
- Seeders are idempotent. Reference data goes in seeders or migrations, not in hand scripts.
- Dynamic/BPMS storage direction (hybrid: real tables for user-built entities, JSON column for custom fields) - see chapter 04.

## 4. Settings and secrets [R24, R10]

- Every setting lives in `env/<name>.env` for **every** environment (`local`, `docker`, `demo`, `uat`, `production-<customer>`) and is read with `Required*`.
- Secrets only in `env/<name>.secrets.env` (git-ignored) or environment variables; add the key to `*.secrets.env.example`.
- No environment-dependent value (URL, host, credential, port, lifetime, culture, policy flag) hard-coded. `appsettings.json` stays empty.
- No real secret, key or connection string anywhere in the repo, package, log, Jira comment or chat. The licence-signing private key never touches a development machine.
- A test keeps env key sets identical and scans committed files for secrets; a new setting that is missing from one file fails it.

## 5. Offline product [R1]

Production has no internet: no CDN, no external call, no telemetry, no online licence/update check. A new library must run fully offline and must not need an online source at build time. Static assets are served from `wwwroot`. (Website and demo are the only online parts and are separate deployables [R18].)

## 6. Security

- Validate on the server; encode output (Razor does by default - never `Html.Raw` on user data); antiforgery on every state-changing request (including `fetch`).
- Authorization at the service/policy level, not only by hiding UI. Check RLS (section 1) on every new query.
- No string-built SQL; use EF/parameters. Uploads: type/size/extension checks, stored outside `wwwroot` (FileStorage abstraction).
- Never log secrets or personal data; use Serilog structured logging.
- AI features run on-premise (Ollama/Tesseract); nothing leaves the organisation.

## 7. Localization [R5]

Product default is Persian, RTL; displayed dates Jalali, stored Gregorian/UTC. All user-visible text through localization keys (resources) - none hard-coded in views or JS. Test every screen in `fa` (RTL) and `en`.

## 8. Tests and Definition of done

Tests: `dotnet test PEMS.slnx`. Many need SQL Server and use the isolated database `PEMSDB_RegressionTest` (never `PEMSDB`); set `PEMS_TEST_CONNECTION` for another server.

A task is **done** only when:

1. Every *Done when* item is met and demonstrated (file:line, commit, test name or screenshot) [R13].
2. New behaviour has tests: unit tests for logic, integration tests for persistence/query paths, a regression test for each fixed bug. UI changes are verified in a real browser (EN + FA, desktop + 375 px) - not just compiled.
3. `dotnet build PEMS.slnx -c Release` and `dotnet test PEMS.slnx` are green; the model-drift gate is clean; nothing previously passing is now skipped or deleted.
4. Offline, settings, RLS, localization checks above hold.
5. The six artefacts (R22) are updated: guideline/memory, plan file, **Supportability Matrix** [R21], Instructions (only if a rule changed), `CHANGELOG.md`, commit.
6. Test Plan: a Done row whose Test Plan status is "Needs QA" is not trustworthy [R14] - add or update the case.

## 9. Git

- One logical change per commit; message `SCRUM-123 type(scope): summary` (types: feat, fix, refactor, docs, test, chore, ci). *Note:* Instructions rule 7 still says `vX.Y.Z: description`; rule 25 (2026-10-23, issue key first) is newer and was confirmed by the owner on 2026-10-25 (chapter 06, decisions).
- One short-lived working branch per task or approved batch, created from `main`; delete it (local and remote) once merged - details in chapter 00 sections 3-4.
- Stage specific files only. Re-run `git status` before every commit.
- Version bumps: system = CalVer `YYYY.MM.PATCH`, components = SemVer; editing the plan workbook does not change a version [R9].
- Plan workbook changes only by pull request; regenerate `plan/backlog.csv` with `python tools/plan-sync/plan_sync.py export`.
