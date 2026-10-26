# 07 - QA check in the UI (after the merge, while the task is In Review)

Owner decision 2026-10-26. Read it when you move a task to In Review, when you are asked to QA a task, and when you review a pull request.

**In Review = conditional done**: the author verified the work as far as the session allows, the PR is **merged into `main`**, and the tests are either green or listed as not run (section 2a) ([chapter 00](00-task-lifecycle.md) section 4). **QA check = an independent
person confirms it in the real user interface of what is now on `main`, before Done.** Automated tests (chapter 01) and this check complement each other; **for now they are one step** (section 2a): the test run is line 1 of the checklist.
Instructions rules 13 (no Done without evidence) and 14 (a row whose Test Plan status is "Needs QA" is not trustworthy) are what it serves.

## 1. Flow

`In Progress` -> author finishes, runs the tests if the session can, PR open -> **merge into `main`** -> **`In Review` + label `qa-pending`** (right after the merge) -> QA check (this chapter) ->
`qa-passed`: `Done` | `qa-failed`: back to **`To Do`** with the defects listed (owner decision 2026-10-26); whoever takes it up moves it to In Progress, the fix goes through a new branch and PR to `main`, then In Review again.

Labels: `qa-pending`, `qa-passed`, `qa-failed` (one at a time; the author sets `qa-pending` when moving to In Review after the merge, the tester replaces it).

## 2. Who and where

- **Who:** a person other than the author: the owner, the other member, or a named tester. An AI agent may write the checklist and drive a browser to collect evidence, but the verdict
  on work an agent produced is given by a person.
- **Where:** `main` at the merge commit, run locally (`PEMS_ENV=local`, [chapter 05](05-environment-and-commands.md)), or `staging` when the owner has promoted `main` to it (a push to `staging` deploys: only on the owner's request).
  Record the commit hash and the environment in the report.
- **Accounts:** one per role the task touches (`tools/TestAccountProvisioner`), never the real administrator.

## 2a. Tests and review are one step (for now)

Owner decision 2026-10-26, until a CI gate exists (plan row #7): the local test run is **line 1 of every QA checklist** ([README.md](../../README.md) "Local tests"), done by the tester together with the UI checks.
- An author who can run the tests does so before the merge and writes the counts in the report; the tester repeats the run (line 1).
- An author who cannot (cloud session without the .NET SDK or SQL Server) merges, moves the issue to In Review and lists the exact commands that were not run; line 1 then carries the first run.
- **Done needs both**: line 1 green (only the known failures of README.md "Local tests" may stay red) and all other lines Pass. A red test is a defect like any other: `qa-failed`, back to To Do, fix by a new PR.
- When CI runs the tests on every merge, line 1 becomes "CI run on the merge commit is green" and this section is removed.

## 3. "Fully in the UI"

Every step is done through the browser exactly as an end user or administrator would: no SQL, no command line, no API calls, no reading of code.
Set-up that has a screen (users, roles, settings) is done on that screen. If a step can only be done outside the UI, that is a finding (Instructions rule 20 requires licensing, installation,
update and version operations to be UI-completable) and is recorded as a defect or, for infrastructure work, as a named exception.

## 4. Procedure

1. **Prepare (10 min).** Read the issue (*Done when*), the PR description and the Test Plan rows of the backlog row. Fresh browser profile; open the console and network tab.
2. **Write the checklist as a Jira comment "QA checklist"** (template in section 5) before testing: one line per *Done when* item, plus the standard lines below.
   **Do not copy steps that already exist: refer to them** (section 5a). If the task introduces a manual check or a local test, add it to the right existing README (never a new file) and refer to it from the checklist.
3. **Execute in the browser** and mark each line Pass / Fail / Blocked with evidence (screenshot file name, Test Plan case id, or the URL of the rendered page).
   Line 1 is always the local test run (section 2a). Standard lines for any UI change: golden path; one edge case and one invalid input; every role that must be allowed and one that must be denied; **fa (RTL) and en**;
   **desktop and 375 px**; reload and back-button state; console and network free of errors; two neighbouring screens still work; keyboard focus and Escape on dialogs.
4. **Defects.** Each failure becomes a Jira Bug (or a one-line comment if trivial), linked to the task; set `qa-failed` (replacing `qa-pending`), add the defect list as a comment and move the task **back to To Do**; it keeps its assignee and all QA comments. The fix is a new PR to `main` and the task returns to In Review. Retest only the failed lines and their neighbours.
5. **Pass.** Set `qa-passed`, add the result comment, update the Test Plan row (`Needs QA` -> `Passed` with date and tester) in a small PR to `main`, and move the issue to Done.
6. **Time-box.** Effort S: 15-30 minutes; M: about 1 hour; L: split into sessions. QA that finds the checklist cannot be executed is a defect in the task description.

## 5. Templates

**QA checklist comment**
```
QA checklist - <issue key> - commit <hash> - environment <local|staging> - tester <name> - <date>
| # | Check (from Done when / standard) | Result | Evidence |
| 1 | Local tests green; only the known failures stay red (README.md "Local tests") | Pass/Fail/Blocked | <counts, console output file> |
| 2 | <Done when item 1, as a user action and expected result; refer to README steps> | | <screenshot / case id> |
| 3 | Golden path fa + en | | |
| 4 | Role allowed / role denied | | |
| 5 | 375 px and desktop | | |
| 6 | Console and network clean | | |
Verdict: qa-passed | qa-failed - defects: <keys>
```

## 5a. Where the checks and local tests live (refer, do not copy)

| What | Where (existing files only) |
|---|---|
| Local tests before the merge (build, database-free subset, full suite, known failures) | [README.md](../../README.md) section "Local tests"; commands also in [chapter 05](05-environment-and-commands.md) |
| Manual UI checks of product behaviour (M-1 ... M-7: one-time password, password policy, tools, Docker ports, exposure report) | [README.md](../../README.md) section "Manual checks in the UI" |
| Settings and policy checks (key parity, bad value, password policy, cookie, HSTS, migration timeout) | [env/README.md](../../env/README.md) section "Verifying settings (manual checks)" |
| A deployed environment (health, sign-in, HSTS header, Database Update page, smoke in fa/en, 375 px) | [docs/deployment/DEPLOYMENT_README.md](../deployment/DEPLOYMENT_README.md) section 8 |
| Credential rotation and exposure report | [docs/deployment/CREDENTIAL_ROTATION_RUNBOOK.md](../deployment/CREDENTIAL_ROTATION_RUNBOOK.md) |
| Standard UI lines (RTL/LTR, widths, console) | [chapter 02](02-ui-and-components.md) section 5 "Verifying UI work" and section 4 of this chapter |

In the checklist write "M-2 (README.md, Manual checks in the UI)" or "env/README.md, Verifying settings, HSTS" instead of repeating the steps. A new check gets the next free ID in the
README table it belongs to.

## 6. Tasks without a screen (docs, plan, CI, design)

The check is a read-through **in the UI that shows the result**: the rendered Markdown in the PR "Files changed" view, the Jira issue, the workflow run page, the board.
Checklist lines: each *Done when* item found; links and tables render; no secret value, internal address or personal data; the text agrees with the guideline and the plan row;
numbers and names are consistent between documents. Record it in the same template.

## 7. Keep the record

The "QA checklist" and the QA result comments stay on the Jira issue **after Done** (and after the issue is closed): they are organisational knowledge for the next release, the next tester and audits.
Never delete, overwrite or collapse them; correct a mistake with a new comment that says what changed. The PR description keeps the same text. Re-run an old checklist when the area changes again.

## 8. Related

Test Plan sheet (cases and QA status), #200 UAT scripts per epic and role (they reuse these checklists), #376-#377 (automated test review and maintenance, the other half of verification).
