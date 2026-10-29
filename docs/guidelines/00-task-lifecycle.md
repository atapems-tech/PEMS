# 00 - Task lifecycle (read for every task)

Jira site: https://atapems.atlassian.net - cloudId `9290a713-17c1-44bd-99fc-f1b83705cc69` - project **`SCRUM`**.
Use the Atlassian MCP tools (or the Jira UI for humans) for every step. Never skip a status change.
Field ownership between the plan files and Jira: [PLAN_TRACKER_WORKFLOW.md](../process/PLAN_TRACKER_WORKFLOW.md).

## 1. Before you start (the "Step 0" checklist)

1. **Read this guideline** (README reading map). Skim the last entries of the [learnings log](learnings-log.md).
2. **Read the issue** (`getJiraIssue`): *Why*, *What to do*, *Done when*, *Depends on*, Plan ID, parent, sprint, comments.
   Also read the matching backlog row `plan/rows/NNNN.md` (`python tools/plan-sync/plan_sync.py show <id>`: Evidence, Prompt, Requirement, Depends) - Jira shows a copy, the plan owns scope.
3. **Check dependencies.** If any blocker is not Done, stop and tell the user before writing code.
4. **Study related Jira issues.** Search the project (JQL on Plan ID, area label `area-<name>`, summary keywords, same epic/milestone):
   open, in-progress and recently Done issues that touch the same area. Read their comments and commits. Goal: no duplicated work,
   no contradicting design, and reuse of what a neighbour task already decided.
5. **Look for similar tasks to batch.** If two or more open issues are close (same component, same files, same migration, same
   library, same research), propose doing them together. **Ask the user and wait for approval before starting a batch.** State: which
   issues, why they belong together, what changes in scope, and what each issue's "Done when" will be checked against.
   Never silently widen the scope of one issue to swallow another.
   **When a batch starts** (owner decision 2026-10-29; approval alone changes nothing in Jira): in one step every issue of the batch is moved into the **current (active) sprint**
   (`transitionJiraIssue` with the active sprint id from `listJiraBoardSprints`, state `active`), moved to **In Progress** and **assigned** to the person doing the work (the Jira user of the
   developer running the session; account id from `atlassianUserInfo`, `editJiraIssue`). Planned, approved or newly created issues stay **unassigned and outside any sprint**: a To Do issue
   is never in a sprint and never has an assignee (the exception is `qa-failed`, section 5a). Do this before step 2 of section 2.
5a. **Ownership check - no collisions.** Before starting any issue (alone or in a batch) look at its assignee and status:
   - assignee is another person, **or** the issue is already In Progress / In Review, **or** a branch/PR for it exists -> **do not touch it**. Ask that person (through the user, or a Jira comment mentioning them)
     and get an explicit yes before starting. Keep the confirmation in a comment (who agreed, when). Until then work on something else.
   - the same applies to files and areas that another person's in-progress issue changes: if your task overlaps with it (same files, same migration, same component), stop and coordinate first.
   - an unassigned To Do issue may be taken; assign it to yourself in the same step.
   - a To Do issue labelled `qa-failed` keeps its previous assignee: if that is you or the user told you to take it, move it to In Progress and start from the defect comment; otherwise ask the assignee first.
   Never reassign, move, close or edit an issue that belongs to someone else without their confirmation.
5b. **Adding or changing a task - study first** (Instructions rule 28, owner decision 2026-10-26). Before you add a backlog row and Jira issue, search the backlog and Jira by area, files, goal and keywords (Done and Open rows; held tasks count as not open). If an existing task covers the work or can absorb it, **modify that task**: extend What to do and Done when in the plan row file and the Jira description, and comment what changed and why. Add a new row only when none fits. **A new row's Plan ID is the number of its Jira key** (owner decision 2026-10-27: SCRUM-423 gets `#423`, so the two numbers read the same): create the Jira issue first, then write the row with `#` = that number; gaps in the `#` column are fine. Rows created before this date keep their old numbers (SCRUM-418 is `#388`) - never renumber them. Give it and name the tasks you checked in its Evidence.
5c. **Your task overlaps a started task of someone else** (owner decision 2026-10-27, SCRUM-413 / SCRUM-280). Do not edit it. Build the near-term slice as its own task, say in the issue what the slice covers and what stays with the other task, and ask the owner of the other task (comment mentioning them, or through the user) to confirm the link; reuse the same files and workflow so there is one pipeline, not two. When the other task merges, re-read its comments and diff and **narrow your not-started rows** to what it does not cover (plan row file, Jira description, a comment saying why); do not leave a plan that repeats finished work.
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

## 3a. A reported bug is a normal task (owner decision 2026-10-27)

A bug that a person reports (in chat, in Jira, in a pull request, in another session, or found by a tester or by you outside the task you are finishing) goes through the **same flow as any task**: no silent fixes and no fix that is only a comment on another issue.

1. **Create the Jira issue first and put it in the status `Bugs`** (owner decision 2026-10-27: `Bugs` is a status of the SCRUM workflow, like In Review; no label and no special issue type are needed, the issue is a normal Task). Its flow is `Bugs` -> In Progress -> In Review -> Done. Create it with a plan row file in `plan/rows` (`plan_sync.py new <id> "title" track="Bug Fix"`; Track `Bug Fix`, `#` = the Jira key's number, a Plan ID label; the plan row keeps its own Status until the issue moves, Jira owns the real status) and the usual description: *Why* with the reproduction (steps, expected and actual result, culture/browser/role), *What to do*, *Done when* (the reproduction no longer fails), *Evidence* (a failing test, log or screenshot), priority and parent epic. Link the issue that exposed it.
2. **Ownership and planning as for any task** (section 1, 5a): assignee, sprint decision, In Progress, "Started by" comment with the plan.
3. **Reproduce with a test first**: a regression test that fails before the fix and passes after it; the bug is not fixed without one (a manual-only bug gets a manual check in the README table instead). If the test cannot run in your session, say so as an unchecked QA line (chapter 07 section 1b).
4. **One branch and one PR** (`fix/SCRUM-n-short-name-<user>`), merge into `main`, then **In Review + `qa-pending` + the QA checklist** with a regression line for the bug and two neighbouring checks; **Done only after QA** ([chapter 07](07-qa-check-in-the-ui.md)).
5. **Exception, only this one:** a bug you find in your own task *before* it is merged is fixed in that task's pull request and named in its comment. After the merge it is a Bug issue like any other. A defect found during a QA check follows chapter 07 section 4 (back to To Do) and is also linked as a Bug issue when it is more than a one-line fix.

## 4. Finishing the issue - two levels of "done"

| Level | Jira status | Meaning | Who/when |
|---|---|---|---|
| **Conditional done** | **In Review** | Work is complete and verified by the author: every *Done when* item met, **the PR is merged into `main`**, and the build and tests have run green **or** the comment says exactly why they could not run (see "Tests and review are one step" below). Waiting for the QA check in the UI ([chapter 07](07-qa-check-in-the-ui.md)). | The agent or developer moves it **right after the merge** and sets the label `qa-pending` |
| **Done** | **Done** | The QA check passed (label `qa-passed`) and the plan row Evidence / Test Plan status are updated. | Moved by the tester or the owner after the QA verdict |

Order (owner decision 2026-10-26): tests (if you can run them) -> PR -> **merge into `main`** -> In Review + `qa-pending` -> QA check, whose **line 1 is the local test run** -> Done. An issue never skips In Review.
A task whose *Done when* needs the owner's or a member's approval of a document gets that approval as the review of the PR; the merge follows the approval.
If the QA check fails, the issue goes **back to To Do** with the label `qa-failed` and the defects listed in a comment (owner decision 2026-10-26); it keeps its assignee and its QA comments. Whoever takes it up again moves it to In Progress (section 2), the fix reaches `main` through a new branch and PR (fix forward), then it goes to In Review again.

**The author does not wait for QA** (owner decision 2026-10-27). In Review is the hand-over: after the merge the author moves the issue to In Review with `qa-pending` and **goes on to the next task**. The QA check and the Done are done by someone else, at the end of the sprint if need be, once all sprint tasks are In Review. The author's only duty at the hand-over is an honest test note in the move comment: **"Tests: passed"** with the commands and counts, or **"Tests: not run / not done"** with the exact commands and the reason (no SQL Server, no browser, needs a real deploy, ...). The author never moves an issue to Done, not even when every test passed, and never keeps a task open waiting for a reviewer.

**Move to In Review (conditional done)** only when **all** are true:

- every item in *Done when* is met (check them one by one in your closing comment);
- the build and **all** tests passed, including the new tests the issue asks for (chapter 01, "Definition of done"), **or** they could not be run in your session (for example a cloud session without the .NET SDK or SQL Server): then the comment says so, lists the commands that were not run, and the test run becomes line 1 of the QA checklist (README.md "Local tests"). **Tests and review are one step for now** (owner decision 2026-10-26, no CI gate exists yet, #7): a person runs the tests and the UI checks together; Done needs both green;
- the issue carries the **QA checklist comment and a Tests paragraph** (how to test by README references; commands run or not run) - on the first move and on every re-entry after a failed QA, re-checked and updated as in [chapter 07](07-qa-check-in-the-ui.md) section 1a;
- changes are committed, pushed, a PR is opened and **merged into `main`** (the merge is the step right before In Review; promotion to `staging`/`demo` stays a separate decision);
- the six artefacts of rule 22 are updated (chapter 06): memory/guideline, plan file, Supportability Matrix, Instructions (only if a rule changed), CHANGELOG, commit.

Then:

1. **Comment** with: what changed, files/areas touched, tests added, tests run and **not** run (and why), commit hash or PR link, evidence for each *Done when* item, Supportability Matrix rows added, guideline entries added, open items for the owner. **Also post the QA checklist and the "QA checklist - author results" comments** ([chapter 07](07-qa-check-in-the-ui.md) sections 1a and 1b): every test or check you did not run (SQL Server tests, HTTP smoke tests, browser check) is an unchecked line there, `not done - <reason>`; a sentence in the move comment is not enough, because the next person may only have Jira.
2. **Transition to In Review** with `transitionJiraIssue` right after the merge, add the label `qa-pending` and add the merge commit to the comment; the QA check ([chapter 07](07-qa-check-in-the-ui.md)) then decides: `qa-passed` -> **Done**, `qa-failed` -> back to To Do.
3. **Parent rules** (apply to Done, not In Review; Done means the QA check passed): a Story with Sub-tasks is Done only when all its Sub-tasks are Done; an Epic only when all child issues are Done. When you close the last open child, check the parent and close it too if its own "Done when" is met.
4. **Clean up the branch.** When the work is merged into its target (this happens before In Review) (`main`; promotion to `staging` and `demo` is a separate merge the user directs, because those pushes deploy), **delete the working branch** locally (`git branch -d <name>`) and on the remote if it was pushed (`git push origin --delete <name>`); use `-d`, which refuses unmerged work, and never delete `main`, `staging`, `demo` or someone else's branch. Mention the deletion in the Jira comment. Then run `git branch --merged` to see leftovers and report them. **Since 2026-10-26 the repository deletes the head branch automatically when its pull request is merged** (GitHub setting "Automatically delete head branches"); a cloud session cannot delete remote branches, so it deletes only its local branch and checks with `git fetch --prune` that the remote one is gone.
5. **Update the guideline** if the task produced a durable lesson, decision, helper or pattern (chapter 06).
6. **Finish, then stop** (rule 16): propose a short list of next tasks (mention batchable ones) and wait for the user's choice.

## 4a. Branch hygiene and long-lived branches (owner decision 2026-10-27)

Run the branch audit ([chapter 05](05-environment-and-commands.md)) at the start of a session and once a week, and compare it with Jira:

- A branch **ahead of `main` whose issue is In Review or Done** is a violation: the work must be on `main`. Merge it now: open a PR, resolve the conflicts, run the local gates, merge; GitHub deletes the head branch. Do the same for a branch whose issue key you cannot find, but ask the owner first if it is not your own work.
- **Merging a branch that is far behind `main`:** make a working branch from `origin/main`, merge the old branch into it, resolve conflicts by keeping both sides when both only added lines (`.gitignore`, `.resx`, guideline lists, learnings log), then re-check what auto-merged: EF migration order and the model snapshot (drift gate must be clean), `Program.cs`, env key parity in all six files, no duplicate resource keys. Never resolve a snapshot or a generated file conflict by guessing: regenerate it. Plan rows are one file each, so a plan conflict is a real conflict on the same row: keep both sides' facts.
- A branch with **0 commits ahead** of `main` is finished: delete it (never `main`, `staging`, `demo`; a branch named `fix` blocks every `fix/...` name, so delete such stale names).
- **A cloud session started from an old brief must restart from `main`.** If its issue was already merged, it does not recreate the old branch name or redo the work: it reads the issue comments and the merged PR first (the Jira step 0 check) and continues from `main`. A branch that reappears after its PR was merged is audited like any other.
- **Pull `main` before you restart or resume a task after a pause** (owner decision 2026-10-29; it applies to every agent and person, in any session, local or cloud): run `git fetch` and fast-forward `main` (`git pull --ff-only origin main`) before you read the issue, create or reuse a branch, or continue old work. Other people merge while you are away (plan rows, guideline, migrations). If you resume on an existing branch, merge the fresh `main` into it first and re-read the issue comments and the plan row, then continue. Never start from a stale checkout.
- **In Review never blocks you** (owner decision 2026-10-29): when your task is In Review (merged, waiting for the QA check, or PR waiting for approval) do not stop and wait. Move on to the next approved task or the next task of the batch and keep working. While the sprint is open, In Review counts as done for planning: a task whose dependencies are merged into `main` (they are In Review) may start right after that merge, without waiting for the QA verdict (note the dependency's state in your plan comment, and if the QA later fails and the dependency returns to To Do, fix forward and tell the dependent task's owner). Only the final Done stays tied to the QA verdict (next rule) and is settled when the sprint closes.
- **Merging your pull request into `main` needs no extra approval** (owner decision 2026-10-29): once the task is approved and started, the author (person or agent) merges its own PR into `main` as soon as the PR checks are green or their gaps are listed in the PR, then moves the issue to In Review; do not stop to ask for a merge approval. This covers only working branches into `main`. It does not cover force pushes, deleting branches other than your own merged one, rewriting history, pushing or merging to `staging`/`demo` (these deploy), changing secrets or settings, or a PR whose checks fail. A permission prompt of the tool is a separate matter: if the tool itself blocks the merge, say so in the report and leave the PR open; never work around it.
- **Dependencies: resolve, wait or split - never stop** (owner decision 2026-10-29). Before and during the work read the `depends` field of the plan row and the state of each dependency in Jira, then act by its state:
  - *Merged or In Review* (merged into `main`): the dependency is satisfied; start (see the previous rule) and record the dependency's state in your plan comment.
  - *To Do and yours, or unassigned*: start it first. Add it to the current batch, move it to In Progress and assign it to the person doing the work in the same step (section 1, step 5), do it, merge it, then continue with the task that needed it. The ownership check (5a) still applies to anything with another assignee.
  - *To Do or In Progress and assigned to someone else*: do not touch it and do not wait idle. Comment on the dependency (mention the assignee) that your task needs it, work on the part that does not need it or on another task, and check its state again at the next pause (together with pulling `main`).
  - *Blocked by a loop or by something that cannot be done now* (circular dependency, a dependency that waits for a decision, a missing external input, a held task): do every part that can be done, deliver and merge it, and create a **follow-up task** for the part that needs the blocked dependency. Before creating it apply rule 28 (study existing tasks; modify a fitting one instead of adding a duplicate). The follow-up names the blocker, the finished part and the exact remaining scope, depends on the blocker, and is created unassigned and outside any sprint. Report the split in the Jira comment of the original task and keep moving to the next task.
  A task is closed against its own *Done when*; a dependency split off this way must be reflected in the *Done when* of both tasks (edit the plan row, not only the Jira text). Keep the plan rows' `depends` fields true (`plan_sync.py check` rejects a milestone conflict).
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
