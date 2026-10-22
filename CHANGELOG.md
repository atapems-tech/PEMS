# Changelog

No system version is defined yet (plan row F_CI), so entries are dated.

## 2026-10-22 - Fully code-first database

- The database is installed on first run and updated on later runs by `IDatabaseUpdateService`
  (startup and Admin > Database Update); an unmanaged schema is never touched.
- New `DatabaseUpdateRuns` table (audit of every install/update), migration `FullTextSearch`
  (replaces the hand-run full-text script), anonymous `GET /health/database`.
- Manual schema SQL moved to `legacy-trash/manual-sql/` (kept, not deleted), including the root `InitialCreate.sql`.
- CI model-drift gate; demo pipeline (`deploy-demo.yml`) installs/updates the demo database and waits for `UpToDate`.
- Plan workbook now lives in the repository: `plan/PEMS_Backlog_Roadmap.xlsx`.
