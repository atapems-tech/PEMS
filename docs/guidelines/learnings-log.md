# Learnings log

Append-only while a lesson is young; promote into a chapter when it becomes a rule. Format:
`YYYY-MM-DD | SCRUM-n | area | lesson | what to do`. Newest first. Cross-cutting history before this log is in the workbook *Lessons* sheet (29 entries).

2026-10-25 | SCRUM-368 | DEV | GitHub runs the workflow file of the pushed branch, and a reusable workflow reads prefixed secrets with `secrets[format('{0}_X', inputs.prefix)]` plus `secrets: inherit`; one deploy workflow serves demo and staging with DEMO_*/STAGING_* secrets. A workflow with no push/PR trigger runs nothing on main or working branches. | Keep deploy logic only in `deploy-site.yml`; add an environment by adding an env file, a caller workflow and its secrets.
2026-10-25 | guideline | process | The team had rules spread over the workbook Instructions, README, `.claude/CLAUDE.md` and per-person agent memory; agents of other developers could not see them. | Put shared knowledge in `docs/guidelines/`, point `AGENTS.md`, Jira description and PR template to it.
2026-10-24 | demo pipeline | DEV | IIS file locks, FTP control-socket timeouts and TLS-less health probes broke the first demo deploys (see `docs/deployment/DEMO_DEPLOYMENT.md`). | Upload `app_offline.htm` first, 180 s timeout with one retry, probe with `--insecure`.
2026-10-23 | UI audit | CMP | Source-level audit gave hard numbers that sized the component waves. | Rerun `tools/UiAudit/pattern-audit.py` per wave; counts may only go down.
