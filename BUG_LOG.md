# PEMS Bug Log - Repo Audit
**Date:** 2026-11-06
**Auditor:** Kilo
**Branch:** main (up to date with origin/main)
**Commit:** 115caee5 (Merge PR #234 - SCRUM-512 dom-builders)

## Repository State
- Working tree: clean
- Status: `nothing to commit, working tree clean`
- dotnet SDK: 10.0.400

## Stashed Changes (not applied)
4 stashes exist:

| Stash | Description |
|-------|-------------|
| stash@{0} | WIP on main: 0db68b5c (Merge PR #159 SCRUM-337 tree-t2-saber) |
| stash@{1} | On feature/SCRUM-472-warehouse-intake-saber: WIP before pulling main 2026-10-29 |
| stash@{2} | On feature/SCRUM-429-tender-email-saber: WIP before pulling main 2026-10-29 |
| stash@{3} | On feature/correspondence-cartable-ui: backup before force pull main 2026-10-26 |

## Known Issues on `main` (from docs/guidelines/05-environment-and-commands.md:72)
1. `ShellAccessibilityTests.ProgramConfiguration_ReExecutesEmptyErrorResponsesToTheStatusPage` — reads `Program.cs` text; known red on main (2026-11-03).
2. All SQL-family tests fail without SQL Server (expected; end-of-sprint QA only).

## Remote Branches (origin)
- feature/SCRUM-225-jalali-date-picker-hamid
- feature/SCRUM-337-tree-t2-saber
- feature/SCRUM-514-hardcoded-data-audit-hamid
- main, staging, demo

## How to Run
Per docs/guidelines/05-environment-and-commands.md:

```powershell
cp env/local.secrets.env.example env/local.secrets.env
dotnet run --project PEMS.Web   # http://localhost:5089
```

First start creates the database, installs schema, runs seeders (a few minutes). SQL Server required (LocalDB, Express, or container).

## Local Gates (before merge)
```powershell
dotnet build PEMS.slnx -c Release
dotnet test PEMS.Tests -c Release --no-build --filter "FullyQualifiedName~EnvironmentFilesTests|FullyQualifiedName~TemporaryPasswordGeneratorTests"
```
