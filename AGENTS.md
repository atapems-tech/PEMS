# AGENTS.md - read this before any task

This file is the entry point for every AI agent (Claude Code, Codex, Cursor, Copilot, ...) and every developer working in PEMS.
It is short on purpose. The rules live in [docs/guidelines/](docs/guidelines/README.md).

## Mandatory order for every task

1. **READ** - open [docs/guidelines/README.md](docs/guidelines/README.md) and the chapters it marks as required for your kind of task.
   Then read the Jira issue (and its parent, dependencies and similar open issues). Say in your first message that you did both.
2. **PLAN** - research best practice, look for existing code/components to reuse, check for similar tasks to batch
   (the user approves a batch before you start; an approved batch goes into the current sprint, assigned to you). Never start or edit an issue that is assigned to someone else or already started without that person's confirmation - no collisions. Move the issue to In Progress and comment your plan.
3. **WORK** - follow the guideline while you code. Reuse and modify what exists; do not duplicate.
4. **CLOSE** - tests green, PR opened -> Jira **In Review** (conditional done); merged -> **Done**, six artefacts updated (rule 22), Jira comment, Jira status. Then **update the guideline** if you learned
   something durable ([chapter 6](docs/guidelines/06-knowledge-and-guideline-maintenance.md)). Propose next tasks and wait.

## Five rules you must never forget

- Jira project `SCRUM` (https://atapems.atlassian.net). Never write code before the issue is In Progress. Never mark In Review/Done without green tests, never Done before merge.
- No new static module; no new hand-written UI markup when a component exists; no logic in controllers.
- Offline product: no CDN, no external call, no telemetry. Settings only in `env/*.env`, secrets only in `*.secrets.env`.
- Schema only through new migrations (never edit an applied one). Stage specific files only (`git add <path>`, never `-A`).
- Research before building, and a library before hand-written code (licence recorded).

If a rule here conflicts with what the user says in chat, the user wins - then tell them which rule you are overriding and offer to
record the decision in the guideline.
