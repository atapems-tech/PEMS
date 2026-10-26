# 06 - Knowledge and guideline maintenance (read at the end of every task)

## 1. Where knowledge goes

Agent "memory" is per person and per machine - the team never sees it. Anything the team needs lives in the repository:

| What you learned | Where it goes |
|---|---|
| A rule every task must follow | this guideline (right chapter); if it is a standing rule, also the workbook *Instructions* sheet (owner decides) |
| A durable fact, pitfall or pattern from a task | [learnings-log.md](learnings-log.md) (append), and workbook *Lessons* if it is cross-cutting |
| What is now supported / limited | workbook *Supportability Matrix* [R21] |
| Scope, evidence, report of the task | workbook Backlog row (Evidence/Report) via PR; status stays in Jira |
| A design decision | ADR/spec under `docs/` + Decision row in the plan |
| What changed for users/devs | `CHANGELOG.md` |
| Personal preference of one developer | your own agent memory, not the repo |

## 2. End-of-task update of the guideline

At the end of each task ask: *did I discover something the next person would want before starting?* If yes, add it **in the same PR**:

1. New lesson / gotcha / decision -> append to `learnings-log.md`: `YYYY-MM-DD | SCRUM-n | area | lesson | what to do`. Two or three lines.
2. A lesson that repeats or became a rule -> promote it into the right chapter and replace the log entry with a pointer.
3. A new shared helper, component, tool or command -> add to chapter 02 (components), 03 (tools) or 05 (commands).
4. A rule in the guideline turned out wrong or outdated -> fix it, say why in the PR; if it contradicts the Instructions sheet, raise it with the owner instead of silently diverging.
5. Nothing to add -> say "guideline: nothing to add" in the closing comment (the PR checkbox covers this).

Keep entries factual and short; no task diaries. Delete or merge entries that are stale. Never record secrets or personal data.

## 3. Changing the guideline

- Normal improvements: any contributor, by pull request, reviewed like code.
- Changes to rules marked as owner decisions (chapter 04 table, Instructions-derived rules): only with the owner's explicit decision; cite the date in the text.
- Bump the version/date line in README on structural changes. Keep each chapter under ~300 lines; split rather than grow.

## 4. Keeping enforcement alive

Four layers (README section 3). Maintainers check periodically:

- `AGENTS.md` / `CLAUDE.md` still point to the guideline and the first step is "read".
- `tools/plan-sync` still writes the *Step 0* line into issue descriptions.
- PR template still carries the guideline checkboxes.
- Optional hard gate: a CI step that fails PRs whose description lacks ticked "guideline read" / "guideline updated or nothing to add" boxes; optional Claude Code hook (`UserPromptSubmit` or `SessionStart`) in `.claude/settings.json` that prints the reminder. Add them when the team agrees; they are intentionally not on by default because they cost every prompt.

## 5. Decisions recorded on 2026-10-25 (owner)

1. Issue key prefix is `SCRUM` everywhere (docs, templates, branches, commits).
2. Commit messages and PR titles start with the Jira key (`SCRUM-123 feat(scope): ...`), following Instructions rule 25. Rule 7's `vX.Y.Z:` wording in the workbook is superseded; update the workbook Instructions sheet at the next plan PR.
3. Two levels of done: **In Review** = conditional done (everything verified, PR opened); **Done** = merged. The SCRUM workflow today has the statuses To Do, In Progress, In Review, Done, Bugs.
4. `.claude/CLAUDE.md` is a pointer to `AGENTS.md`; the old Instrument Passport directive moved to `docs/cmms/INSTRUMENT_PASSPORT_DIRECTIVE.md`.
5. `.claude/settings.json` calls `graphify` from PATH instead of a per-user absolute path.

## 5a. Decisions recorded on 2026-10-26 (owner)

1. Instructions rule 27: the Jira report of a task is kept current during the work and at the end (chapter 00, section 3).
2. A held task has workbook Status `Hold` and, in Jira, the status `On Hold` (added to the SCRUM workflow by the owner on 2026-10-26) plus the label `on-hold` (kept for the plan sync), with the reason and date in its Report.
3. Instructions rule 28: study existing tasks before adding one; modify a fitting task instead (chapter 00, step 5b). Held tasks count as not open.
4. Secrets-audit follow-ups: history purge (#374) and FTPS certificate verification (#375) are held; test review (#376), test-maintenance procedure (#377) and review of Claude's roles and setup (#378) are open.

## 6. Open points

- `PLAN_TRACKER_WORKFLOW.md` maps statuses On Hold, Backlog and Won't Do. SCRUM now has On Hold (2026-10-26; the 13 held issues were moved into it); Backlog and Won't Do are still missing (project statuses: To Do, On Hold, In Progress, In Review, Done, Bugs). Add them in Jira or change the mapping.
