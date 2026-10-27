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
   - a To Do issue labelled `qa-failed` keeps its previous assignee: if that is you or the user told you to take it, move it to In Progress and start from the defect comment; otherwise ask the assignee first.
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
| **Conditional done** | **In Review** | Work is complete and verified by the author: every *Done when* item met, **the PR is merged into `main`**, and the build and tests have run green **or** the comment says exactly why they could not run (see "Tests and review are one step" below). Waiting for the QA check in the UI ([chapter 07](07-qa-check-in-the-ui.md)). | The agent or developer moves it **right after the merge** and sets the label `qa-pending` |
| **Done** | **Done** | The QA check passed (label `qa-passed`) and the plan row Evidence / Test Plan status are updated. | Moved by the tester or the owner after the QA verdict |

Order (owner decision 2026-10-26): tests (if you can run them) -> PR -> **merge into `main`** -> In Review + `qa-pending` -> QA check, whose **line 1 is the local test run** -> Done. An issue never skips In Review.
A task whose *Done when* needs the owner's or a member's approval of a document gets that approval as the review of the PR; the merge follows the approval.
If the QA check fails, the issue goes **back to To Do** with the label `qa-failed` and the defects listed in a comment (owner decision 2026-10-26); it keeps its assignee and its QA comments. Whoever takes it up again moves it to In Progress (section 2), the fix reaches `main` through a new branch and PR (fix forward), then it goes to In Review again.

**Move to In Review (conditional done)** only when **all** are true:

- every item in *Done when* is met (check them one by one in your closing comment);
- the build and **all** tests passed, including the new tests the issue asks for (chapter 01, "Definition of done"), **or** they could not be run in your session (for example a cloud session without the .NET SDK or SQL Server): then the comment says so, lists the commands that were not run, and the test run becomes line 1 of the QA checklist (README.md "Local tests"). **Tests and review are one step for now** (owner decision 2026-10-26, no CI gate exists yet, #7): a person runs the tests and the UI checks together; Done needs both green;
- the issue carries the **QA checklist comment and a Tests paragraph** (how to test by README references; commands run or not run) - on the first move and on every re-entry after a failed QA, re-checked and updated as in [chapter 07](07-qa-check-in-the-ui.md) section 1a;
- changes are committed, pushed, a PR is opened and **merged into `main`** (the merge is the step right before In Review; promotion to `staging`/`demo` stays a separate decision);
- the six artefacts of rule 22 are updated (chapter 06): memory/guideline, plan file, Supportability Matrix, Instructions (only if a rule changed), CHANGELOG, commit.

Then:

1. **Comment** with: what changed, files/areas touched, tests added, tests run and **not** run (and why), commit hash or PR link, evidence for each *Done when* item, Supportability Matrix rows added, guideline entries added, open items for the owner.
2. **Transition to In Review** with `transitionJiraIssue` right after the merge, add the label `qa-pending` and add the merge commit to the comment; the QA check ([chapter 07](07-qa-check-in-the-ui.md)) then decides: `qa-passed` -> **Done**, `qa-failed` -> back to To Do.
3. **Parent rules** (apply to Done, not In Review; Done means the QA check passed): a Story with Sub-tasks is Done only when all its Sub-tasks are Done; an Epic only when all child issues are Done. When you close the last open child, check the parent and close it too if its own "Done when" is met.
4. **Clean up the branch.** When the work is merged into its target (this happens before In Review) (`main`; promotion to `staging` and `demo` is a separate merge the user directs, because those pushes deploy), **delete the working branch** locally (`git branch -d <name>`) and on the remote if it was pushed (`git push origin --delete <name>`); use `-d`, which refuses unmerged work, and never delete `main`, `staging`, `demo` or someone else's branch. Mention the deletion in the Jira comment. Then run `git branch --merged` to see leftovers and report them. **Since 2026-10-26 the repository deletes the head branch automatically when its pull request is merged** (GitHub setting "Automatically delete head branches"); a cloud session cannot delete remote branches, so it deletes only its local branch and checks with `git fetch --prune` that the remote one is gone.
5. **Update the guideline** if the task produced a durable lesson, decision, helper or pattern (chapter 06).
6. **Finish, then stop** (rule 16): propose a short list of next tasks (mention batchable ones) and wait for the user's choice.

## 4a. Branch hygiene and long-lived branches (owner decision 2026-10-27)

Run the branch audit ([chapter 05](05-environment-and-commands.md)) at the start of a session and once a week, and compare it with Jira:

- A branch **ahead of `main` whose issue is In Review or Done** is a violation: the work must be on `main`. Merge it now: open a PR, resolve the conflicts, run the local gates, merge; GitHub deletes the head branch. Do the same for a branch whose issue key you cannot find, but ask the owner first if it is not your own work.
- **Merging a branch that is far behind `main`:** make a working branch from `origin/main`, merge the old branch into it, resolve conflicts by keeping both sides when both only added lines (`.gitignore`, `.resx`, guideline lists, learnings log), then re-check what auto-merged: EF migration order and the model snapshot (drift gate must be clean), `Program.cs`, env key parity in all six files, no duplicate resource keys. Never resolve a snapshot or a binary (`plan/*.xlsx`) conflict by guessing: regenerate it.
- A branch with **0 commits ahead** of `main` is finished: delete it (never `main`, `staging`, `demo`; a branch named `fix` blocks every `fix/...` name, so delete such stale names).
- **A cloud session started from an old brief must restart from `main`.** If its issue was already merged, it does not recreate the old branch name or redo the work: it reads the issue comments and the merged PR first (the Jira step 0 check) and continues from `main`. A branch that reappears after its PR was merged is audited like any other.
- **No direct Done:** an issue never goes from To Do or In Progress to Done. The only way to Done is In Review, then the QA verdict. A parent whose children are all Done may be closed by the owner; say so in its comment. (Enforcement is a Jira workflow setting, see chapter 06 open points.)

## 5. When you cannot finish

- Do **not** move the issue to In Review or Done. Leave it In Progress.
- Comment: what is done, what is left, what blocks.
- Tell the user in the session. If it should wait for a decision, propose Hold (On Hold) rather than silently leaving it.

## 6. Never

- Never move an issue to Done from To Do or In Progress; Done is reached only from In Review after the QA verdict (section 4a).
- Never mark In Review because the session is ending or the code "looks finished" unless the tests passed or the comment states exactly which tests could not run and why. Never mark In Review before the PR is merged into `main`, and never mark Done before the tests are green and the QA check passed.
- Never delete, overwrite or collapse a QA checklist or QA result comment, not even after Done; they stay on the issue as organisational knowledge ([chapter 07](07-qa-check-in-the-ui.md) section 7).
- Never leave an issue In Progress / In Review without an assignee.
- Never change statuses of issues you were not asked to work on (parent rules and approved batches excepted).
- Never set "Confirmed by PM" yourself except to record a decision the owner just gave (rule 19).
- Never claim a feature externally (sales, website, demo) that is not Done with evidence (rule 13).
- Never commit to `main`/`staging`/`demo` directly or leave merged working branches behind; changes reach them only by merging a working branch (the user decides the merge target and whether a PR is needed); never skip hooks; never force-push shared branches. A push to `staging` or `demo` deploys an environment: do it only when the user asks for that deploy.

## 7. Working with the user

- The user talks Persian; reply in the user's language in chat. Code, comments in infrastructure code, commits, Jira, docs: English. Domain entity XML comments may be Persian (existing convention).
- Finish what you start; report honestly: failing tests, skipped steps and unverified claims are stated, not hidden.
- For hard-to-reverse or outward-facing actions (push, deploy, delete, Jira bulk changes, publishing) confirm first unless the user already authorised that exact action.
