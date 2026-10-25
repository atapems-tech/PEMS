# 00 - Task lifecycle (read for every task)

Jira site: https://atapems.atlassian.net - cloudId `9290a713-17c1-44bd-99fc-f1b83705cc69` - project **`SCRUM`**.
Use the Atlassian MCP tools (or the Jira UI for humans) for every step. Never skip a status change.
Field ownership between the plan workbook and Jira: [PLAN_TRACKER_WORKFLOW.md](../process/PLAN_TRACKER_WORKFLOW.md).

## 1. Before you start (the "Step 0" checklist)

1. **Read this guideline** (README reading map). Skim the last entries of the [learnings log](learnings-log.md).
2. **Read the issue** (`getJiraIssue`): *Why*, *What to do*, *Done when*, *Depends on*, Plan ID, parent, sprint, comments.
   Also read the matching backlog row in `plan/backlog.csv` (Evidence, Prompt, Requirement, Depends) - Jira shows a copy, the workbook owns scope.
3. **Check dependencies.** If any blocker is not Done, stop and tell the user before writing code.
4. **Study related Jira issues.** Search the project (JQL on Plan ID, area label `area-<name>`, summary keywords, same epic/milestone):
   open, in-progress and recently Done issues that touch the same area. Read their comments and commits. Goal: no duplicated work,
   no contradicting design, and reuse of what a neighbour task already decided.
5. **Look for similar tasks to batch.** If two or more open issues are close (same component, same files, same migration, same
   library, same research), propose doing them together. **Ask the user and wait for approval before starting a batch.** State: which
   issues, why they belong together, what changes in scope, and what each issue's "Done when" will be checked against.
   Never silently widen the scope of one issue to swallow another.
6. **Research best practice before designing** ([chapter 03](03-libraries-tools-and-research.md) section 3) and look for what already
   exists in the repo (section 3 below). Write the findings in the plan comment, not in your head.
7. **Plan** in 3-5 lines. If the task is large, uncertain or touches shared engines, share the plan with the user first.

## 2. Starting the issue (Jira, mandatory)

1. Move the issue to **In Progress** with `transitionJiraIssue` *before writing any code*.
   Use the transition named "In Progress"; if that fails, list the issue's transitions and pick the one leading to the In Progress status.
2. Parents: Sub-task with a parent Story still To Do -> move the Story to In Progress too. Story inside an Epic still To Do -> move the Epic too.
3. If the description lacks the line `Step 0: read AGENTS.md and docs/guidelines/README.md ...`, prepend it (add only; never change the other text). This makes the reminder self-healing for issues created before the guideline existed.
4. Add a short comment: **"Started by <agent or person>"** plus your plan in 3-5 lines (include: batch partners if any, the existing code/components you will reuse, the research source you relied on).

Never change the status of issues you were not asked to work on (except the parent rules above and approved batch partners).

## 3. While you work

- **Reuse before you write.** Search the repo first (`graphify query`, grep, existing services/components). Extend or modify what exists; do not create a parallel concept (rule 3). See chapters 01 and 02.
- **Branch / commits / PR** start with the Jira key: branch `feature/SCRUM-123-short-name`; commit `SCRUM-123 feat(scope): message`; PR title `SCRUM-123 ...`.
  Stage specific files only: `git add <path>`, never `git add -A` / `git add .` (the user may commit in another terminal - re-run `git status` before committing).
- **Fix anti-patterns in scope** (rule 15). Large or risky fixes get their own issue - create or propose it, do not hide it in this one.
- **Stay in scope; record discoveries.** Anything useful you notice outside scope goes to the user as a proposed new issue, not into the diff.
- **Unclear requirement?** Ask the user one concrete question before building on a guess. Open decisions are listed in the plan (Decision Needed rows).
- **Never edit the issue's description** to make "Done when" easier to meet; propose the change to the user instead.

## 4. Finishing the issue - two levels of "done"

| Level | Jira status | Meaning | Who/when |
|---|---|---|---|
| **Conditional done** | **In Review** | Work is complete and verified by the author: every *Done when* item met, build and tests green, committed, PR opened. Waiting for review and merge. | The agent or developer moves it when the PR is opened |
| **Done** | **Done** | The PR is merged into its target branch and the closing PR updated the plan row Evidence. | Moved after merge (by the reviewer/merger, or by the agent when it performs the merge on the user's instruction) |

An issue never skips In Review when a PR exists. If the user tells you to merge yourself, do both moves in order and say so in the comment.

**Move to In Review (conditional done)** only when **all** are true:

- every item in *Done when* is met (check them one by one in your closing comment);
- build passes and **all** tests pass, including the new tests the issue asks for (chapter 01, "Definition of done");
- changes are committed, pushed, and a PR is opened (our flow);
- the six artefacts of rule 22 are updated (chapter 06): memory/guideline, plan file, Supportability Matrix, Instructions (only if a rule changed), CHANGELOG, commit.

Then:

1. **Comment** with: what changed, files/areas touched, tests added, commit hash or PR link, Supportability Matrix rows added, guideline entries added.
2. **Transition to In Review** with `transitionJiraIssue`; after the merge, **transition to Done** (same comment rules: add the merge commit).
3. **Parent rules** (apply to Done, not In Review): a Story with Sub-tasks is Done only when all its Sub-tasks are Done; an Epic only when all child issues are Done. When you close the last open child, check the parent and close it too if its own "Done when" is met.
4. **Update the guideline** if the task produced a durable lesson, decision, helper or pattern (chapter 06).
5. **Finish, then stop** (rule 16): propose a short list of next tasks (mention batchable ones) and wait for the user's choice.

## 5. When you cannot finish

- Do **not** move the issue to In Review or Done. Leave it In Progress.
- Comment: what is done, what is left, what blocks.
- Tell the user in the session. If it should wait for a decision, propose Hold (On Hold) rather than silently leaving it.

## 6. Never

- Never mark In Review or Done because the session is ending or the code "looks finished" without passing tests. Never mark Done before the merge.
- Never change statuses of issues you were not asked to work on (parent rules and approved batches excepted).
- Never set "Confirmed by PM" yourself except to record a decision the owner just gave (rule 19).
- Never claim a feature externally (sales, website, demo) that is not Done with evidence (rule 13).
- Never push to `main`/`develop`/`demo` directly; never skip hooks; never force-push shared branches.

## 7. Working with the user

- The user talks Persian; reply in the user's language in chat. Code, comments in infrastructure code, commits, Jira, docs: English. Domain entity XML comments may be Persian (existing convention).
- Finish what you start; report honestly: failing tests, skipped steps and unverified claims are stated, not hidden.
- For hard-to-reverse or outward-facing actions (push, deploy, delete, Jira bulk changes, publishing) confirm first unless the user already authorised that exact action.
