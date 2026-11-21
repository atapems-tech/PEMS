# 01 Repository baseline (PPC Stage 0)

**Audit date and time:** 2026-11-13, 23:53 (+03:30)
**Repository root:** `D:\PEMS\03-SOURCE\PEMS`
**Commit:** `1185588f` (main, PR #264 merged)
**Branch at audit time:** `feature/SCRUM-561-ppc-stage0-audit-saber` (created for this batch; no commit on it yet)
**Working tree:** not clean. Untracked: `BUG_LOG.md` (pre-existing, not part of this work), `docs/production-planning/` (this package), `plan/rows/0561.md` to `0564.md` (this batch). Modified tracked files: none from this batch.

## Solution and projects
- Solution: `PEMS.slnx`.
- Projects (20 `.csproj` in the solution tree): `PEMS.AI`, `PEMS.Application`, `PEMS.Domain`, `PEMS.Infrastructure`, `PEMS.Licensing`, `PEMS.Migrations`, `PEMS.Persistence`, `PEMS.Shared`, `PEMS.UI`, `PEMS.UI.Abstractions`, `PEMS.Web`, `PEMS.WorkflowPlatform`, `PEMS.Tests`, `PEMS.UI.Tests`, `PEMS.UI.Tests.ExtensionPack`.
- Tools (not part of the product runtime): `tools/LoadTestSeed`, `tools/PemsLinuxProbe`, `tools/PemsVendorTool`, `tools/TestAccountProvisioner`.
- **Stray project:** `WebApplication1/WebApplication1.csproj`. Known open item (Jira SCRUM-462, repository hygiene). Not used by this PPC work.

## Frameworks and packages
- Target framework: `net10.0` in all 20 project files.
- Entity Framework Core: 10.0.10 (`Microsoft.EntityFrameworkCore.SqlServer`, `.Design`, `.Tools`).
- Database provider: SQL Server (EF Core SqlServer). Local test instance: SQL Server Express.

## Migrations
- Migration files (excluding Designer and model snapshot): **136**.
- Latest migration: `20261110051551_Scrum559VdrHoldGrants`.
- Earliest production-core migration referenced in the audit: `20260915130204_AddProductionCore`.
- Model snapshot: `PEMS.Migrations/Migrations/PEMSDbContextModelSnapshot.cs`. Read only; no database was connected for this audit.

## Tests
- Projects: `PEMS.Tests` (main), `PEMS.UI.Tests` (components and UI contracts), `PEMS.UI.Tests.ExtensionPack` (no test files).
- Test files (`*Tests.cs`) in `PEMS.Tests` and `PEMS.UI.Tests`: **445**.
- Test database: SQL Server through `PEMS_TEST_CONNECTION`. Some production tests use EF InMemory (see Audit-03, X7 and X21).
- Test gates tool: `tools/test-gates/gates.py`.
- No test was run for this package. No code changed.

## Git and process state (this batch)
- Local branch created: `feature/SCRUM-561-ppc-stage0-audit-saber` from main 1185588f. **Write action** (Git branch created, not pushed).
- No commit, no push, no PR on this branch.
- Plan rows created: `0561`, `0562`, `0563`, `0564` (via `plan_sync.py new` and filled by hand). `plan_sync.py check`: 530 rows, 0 findings. **Write action** (plan files).
- Jira issues created: SCRUM-561, 562, 563, 564, parent SCRUM-11 (M6), with progress comments added. **Write action** (Jira).
- Local planning file outside the repository, `C:\Users\SUPCON\.claude\plans\virtual-honking-scone.md`: edited. Not part of the repository.

## Read-only confirmations
- No source file, configuration, database, migration or secret was changed.
- No `dotnet ef` command was run against a database.
- Standards sites were read through the web tools only; no account or credentials were used.
