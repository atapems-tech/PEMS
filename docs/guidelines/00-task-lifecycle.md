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
   **When the user approves a batch:** every issue in the package is moved into the **current (active) sprint** and its **assignee is set to the person
   doing the work** (the Jira user of the developer running the session; get the account id with `atlassianUserInfo`). Do this before step 2 of section 2,
   using `editJiraIssue` (assignee) and `transitionJiraIssue` with the active sprint id (`listJiraBoardSprints`, state `active`).
5a. **Ownership check - no collisions.** Before starting any issue (alone or in a batch) look at its assignee and status:
   - assignee is another person, **or** the issue is already In Progress / In Review, **or** a branch/PR for it exists -> **do not touch it**. Ask that person (through the user, or a Jira comment mentioning them)
     and get an explicit yes before starting. Keep the confirmation in a comment (who agreed, when). Until then work on something else.
   - the same applies to files and areas that another person's in-progress issue changes: if your task overlaps with it (same files, same migration, same component), stop and coordinate first.
   - an unassigned To Do issue may be taken; assign it to yourself in the same step.
   Never reassign, move, close or edit an issue that belongs to someone else without their confirmation.
5b. **Adding or changing a task - study first** (Instructions rule 28, owner decision 2026-10-26). Before you add a backlog row and Jira issue, search the backlog and Jira by area, files, goal and keywords (Done and Open rows; held tasks count as not open). If an existing task covers the work or can absorb it, **modify that task**: extend What to do and Done when in the workbook row and the Jira description, and comment what changed and why. Add a new row only when none fits, give it the next free Plan ID, and name the tasks you checked in its Evidence.
6. **Research best practice before designing** ([chapter 03](03-libraries-tools-and-research.md) section 3) and look for what already
   exists in the repo (section 3 below). Write the findings in the plan comment, not in your head.
7. **Plan** in 3-5 lines. If the task is large, uncertain or touches shared engines, share the plan with the user first.

## 2. Starting the issue (Jira, mandatory)

0. **No started issue without an assignee.** Before (or in the same step as) moving an issue to In Progress, it must have an assignee: the person doing the work.
   Assign it to the developer running the session (`atlassianUserInfo` for the account id). If you are unsure whose it is, or it is not the current user's, assign it to **Saber** (Jira user `saberahmadi48`) and tell the user.
   If you find any In Progress / In Review issue with no assignee, fix it the same way (JQL: `project = SCRUM AND statusCategory = "In Progress" AND assignee is EMPTY`) and mention it in your report.
1. Move the issue to **In Progress** with `transitionJiraIssue` *before writing any code*.
   Use the transition named "In Progress"; if that fails, list the issue's transitions and pick the one leading to the In Progress status.
2. Parents: Sub-task with a parent Story still To Do -> move the Story to In Progress too. Story inside an Epic still To Do -> move the Epic too.
3. If the description lacks the line `Step 0: read AGENTS.md and docs/guidelines/README.md ...`, prepend it (add only; never change the other text). This makes the reminder self-healing for issues created before the guideline existed.
4. Add a short comment: **"Started by <agent or person>"** plus your plan in 3-5 lines (include: batch partners if any, the existing code/components you will reuse, the research source you relied on).

Never change the status of issues you were not asked to work on (except the parent rules above and approved batch partners).

## 3. While you work

- **Reuse before you write.** Search the repo first (`graphify query`, grep, existing services/components). Extend or modify what exists; do not create a parallel concept (rule 3). See chapters 01 and 02.
- **One working branch per task, or per approved batch.** Create it from `main` (the only long-lived integration branch since `dev` was retired on 2026-10-25; `staging` and `demo` are deploy branches, see docs/deployment/DEPLOYMENT_README.md) right after the issue(s) move to In Progress and before the first edit; never work directly on `dev`/`main`/`demo`.
  - Single task: `feature/SCRUM-123-short-name` (use `fix/`, `docs/`, `refactor/`, `chore/` by change type).
  - Batch of conceptually related tasks approved together: **one** branch named after the primary issue (`feature/SCRUM-123-short-name`); every batch partner's key appears in the commit messages (`SCRUM-123 SCRUM-124 feat(scope): ...`) and in the PR description. Do not create one branch per partner, and do not mix unrelated tasks into a branch.
  - Commit `SCRUM-123 feat(scope): message`; PR title `SCRUM-123 ...`.
  - Keep the branch short-lived: merge as soon as the issue(s) meet *Done when*; do not stack unrelated work on it.
  Stage specific files only: `git add <path>`, never `git add -A` / `git add .` (the user may commit in another terminal - re-run `git status` before committing).
- **Keep the Jira report current while you work** (Instructions rule 27, owner decision 2026-10-26). Besides the start comment (section 2) and the closing comment (section 4), add a short comment at every milestone, decision, blocker or change of scope and before any hand-over or stop: what is done, what is next, what blocks, tests run and not run. The report is the record the owner reads; the PR description repeats it. Never put a secret value in it (mask to two characters at most).
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

1. **Comment** with: what changed, files/areas touched, tests added, tests run and **not** run (and why), commit hash or PR link, evidence for each *Done when* item, Supportability Matrix rows added, guideline entries added, open items for the owner.
2. **Transition to In Review** with `transitionJiraIssue`; after the merge, **transition to Done** (same comment rules: add the merge commit).
3. **Parent rules** (apply to Done, not In Review): a Story with Sub-tasks is Done only when all its Sub-tasks are Done; an Epic only when all child issues are Done. When you close the last open child, check the parent and close it too if its own "Done when" is met.
4. **Clean up the branch.** When the work is merged into its target (`main`; promotion to `staging` and `demo` is a separate merge the user directs, because those pushes deploy), **delete the working branch** locally (`git branch -d <name>`) and on the remote if it was pushed (`git push origin --delete <name>`); use `-d`, which refuses unmerged work, and never delete `main`, `staging`, `demo` or someone else's branch. Mention the deletion in the Jira comment. Then run `git branch --merged` to see leftovers and report them.
5. **Update the guideline** if the task produced a durable lesson, decision, helper or pattern (chapter 06).
6. **Finish, then stop** (rule 16): propose a short list of next tasks (mention batchable ones) and wait for the user's choice.

## 5. When you cannot finish

- Do **not** move the issue to In Review or Done. Leave it In Progress.
- Comment: what is done, what is left, what blocks.
- Tell the user in the session. If it should wait for a decision, propose Hold (On Hold) rather than silently leaving it.

## 6. Never

- Never mark In Review or Done because the session is ending or the code "looks finished" without passing tests. Never mark Done before the merge.
- Never leave an issue In Progress / In Review without an assignee.
- Never change statuses of issues you were not asked to work on (parent rules and approved batches excepted).
- Never set "Confirmed by PM" yourself except to record a decision the owner just gave (rule 19).
- Never claim a feature externally (sales, website, demo) that is not Done with evidence (rule 13).
- Never commit to `main`/`staging`/`demo` directly or leave merged working branches behind; changes reach them only by merging a working branch (the user decides the merge target and whether a PR is needed); never skip hooks; never force-push shared branches. A push to `staging` or `demo` deploys an environment: do it only when the user asks for that deploy.

## 7. Working with the user

- The user talks Persian; reply in the user's language in chat. Code, comments in infrastructure code, commits, Jira, docs: English. Domain entity XML comments may be Persian (existing convention).
- Finish what you start; report honestly: failing tests, skipped steps and unverified claims are stated, not hidden.
- For hard-to-reverse or outward-facing actions (push, deploy, delete, Jira bulk changes, publishing) confirm first unless the user already authorised that exact action.
