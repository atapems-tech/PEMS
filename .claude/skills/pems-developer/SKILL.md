---
name: pems-developer
description: PEMS developer entry: where the rules are, the always-on constraints (no hand-written UI markup, no logic in controllers, offline, migrations only, specific git add), and the order of a task. Use at the start of any coding task.
---

# PEMS developer

Order: read AGENTS.md and docs/guidelines/README.md (chapters 0, 1, 6 always) -> Jira issue and dependencies -> plan row -> branch `feature/SCRUM-n-name-<person>` -> code -> `python tools/test-gates/gates.py` -> PR (title and commit `SCRUM-n: summary (<person>)`, trailers per the session attribution) -> merge (squash, delete branch) -> Jira In Review plus QA checklist -> guideline update if durable.

Always: components from `PEMS.UI` only; no controller logic (services plus definitions, skill `pems-definition-engineer`); schema only by new migrations (skill `pems-migration-guard`); offline (no CDN or telemetry); settings in `env/*.env`, secrets in `*.secrets.env`; `git add <path>`; research and library before hand-written code; Persian-first strings via resources or TranslatableText (skill `pems-localization`).

Roles: owner decisions `pems-owner`; planning `pems-product-manager`; board and review `pems-scrum-master`; checks `pems-qa`; definitions `pems-definition-engineer`.

- **Fixes and changes (owner 2026-11-28):** only in an engine, a component or an input of these; name the layer of the cause in the Bug, fix it once for every screen, regression test at that level ([chapter 0 section 3a point 2a](../../../docs/guidelines/00-task-lifecycle.md), [chapter 4 section 7](../../../docs/guidelines/04-platform-direction.md)).
