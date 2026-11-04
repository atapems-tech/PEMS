---
name: pems-test-gates
description: Choose and run the tests for a PEMS change (tools/test-gates/gates.py), keep to the time budget, and list what was skipped as QA lines.
---

# PEMS test gates (guideline 05 "Test budget", 07 section 2b)

Run every test that fits; leave the very large suites to the end-of-sprint QA. Each task keeps its own QA line, nothing is combined or "covered by" another task.

1. `python tools/test-gates/gates.py` prints, from your changed files: the build, `PEMS.UI.Tests`, the guard classes (Persian keys, themes, list pages, API, error handling) and the exact test classes that mention your files. `--run` executes them and stops at the first failure.
2. Budget: about two minutes per command. Filters name exact classes, `FullyQualifiedName~.Class.`; never a word or a namespace (`~Letter` pulls in SQL tests, `~PEMS.Tests.Web` is 7 to 11 minutes).
3. After a layout, view text or resource change the Persian-key test is mandatory: a missing Persian entry for a `Localizer["..."]` key fails `LocalizationKeyCoverageTests`.
4. A failure in markup, a component or a guard is always fixed. A failure that needs SQL Server (names with Sql, LiveDb, Concurrency, Migration, Numbering, Atomicity, Retry, RealSql) is `not run - no SQL Server`; `DynamicSidebarSqlTests` is the known red on main.
5. Write what you did not run on the issue's QA checklist: line d2, `not done - end-of-sprint QA`, with the exact command (`dotnet test PEMS.Tests --filter "FullyQualifiedName~PEMS.Tests.Web"`, `dotnet test PEMS.slnx`, the SQL families, the browser check).
6. Do not wait for what you cannot run: merge, then In Review (skill `pems-jira-handover`).

Recording flags: `PEMS_RATCHET_RECORD=1 dotnet test PEMS.UI.Tests --filter AdoptionRatchet` rewrites the baseline (re-record after merging main). Build once, then `--no-build`.
