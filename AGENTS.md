# AGENTS.md - read this before any task

This file is the entry point for every AI agent (Claude Code, Codex, Cursor, Copilot, ...) and every developer working in PEMS.
It is short on purpose. The rules live in [docs/guidelines/](docs/guidelines/README.md).

## Mandatory order for every task

1. **READ** - open [docs/guidelines/README.md](docs/guidelines/README.md) and the chapters it marks as required for your kind of task.
   Then read the Jira issue (and its parent, dependencies and similar open issues). Say in your first message that you did both.
2. **PLAN** - research best practice, look for existing code/components to reuse, check for similar tasks to batch
   (the user approves a batch before you start; an approved batch goes into the current sprint, assigned to you). Never start or edit an issue that is assigned to someone else or already started without that person's confirmation - no collisions. Move the issue to In Progress and comment your plan.
3. **WORK** - on **one branch per task or approved batch** (`feature/SCRUM-123-short-name`, from `main`); follow the guideline while you code. Reuse and modify what exists; do not duplicate.
4. **CLOSE** - after merging, **delete the working branch** (local and remote); tests (if the session can run them) -> PR -> **merge into `main`** -> Jira **In Review** (conditional done, label `qa-pending`) -> QA check in the UI, whose line 1 is the local test run ([chapter 7 of the guideline](docs/guidelines/07-qa-check-in-the-ui.md)) -> **Done**; six artefacts updated (rule 22), Jira comment (kept current while you work, rule 27), Jira status. Then **update the guideline** if you learned
   something durable ([chapter 6](docs/guidelines/06-knowledge-and-guideline-maintenance.md)). Propose next tasks and wait.

## Five rules you must never forget

- Jira project `SCRUM` (https://atapems.atlassian.net). Never write code before the issue is In Progress, and never leave a started issue without an assignee (yourself, or Saber if it is not yours). Never mark Done before the tests are green and the QA check passed; never mark In Review before the merge into `main`, and when you could not run the tests say exactly which and why (tests and review are one step for now).
- No new static module; no new hand-written UI markup when a component exists (check the [component registry](docs/guidelines/02-ui-and-components.md#component-registry-check-this-before-writing-any-markup) first; modify the component, never write it again); no logic in controllers.
- Offline product: no CDN, no external call, no telemetry. Settings only in `env/*.env`, secrets only in `*.secrets.env`.
- Schema only through new migrations (never edit an applied one). Stage specific files only (`git add <path>`, never `-A`).
- Research before building, and a library before hand-written code (licence recorded).

## Who did the work (shared GitHub account)

Branches, commits and pull requests made through the shared GitHub account `atapems-tech` do not show which person asked for the work.
When the GitHub user is `atapems-tech`, name the real user - **Saber** or **Hamid** - at the end of:

- the **branch name**: `feature/SCRUM-123-short-name-hamid`, `fix/SCRUM-456-short-name-saber`;
- the **commit subject**: `SCRUM-123: short summary (hamid)`.

Find the real user from the Jira account the session works for or from the chat; if it is not clear, ask once before the first commit. Another GitHub user (their own account) needs no suffix.
Owner decision 2026-10-27.

If a rule here conflicts with what the user says in chat, the user wins - then tell them which rule you are overriding and offer to
record the decision in the guideline.
