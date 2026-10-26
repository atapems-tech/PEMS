# 07 - QA check in the UI (after the merge, while the task is In Review)

Owner decision 2026-10-26. Read it when you move a task to In Review, when you are asked to QA a task, and when you review a pull request.

**In Review = conditional done**: the author verified the work, tests are green and **the PR is merged into `main`** ([chapter 00](00-task-lifecycle.md) section 4). **QA check = an independent
person confirms it in the real user interface of what is now on `main`, before Done.** Automated tests (chapter 01) do not replace it, and it does not replace them.
Instructions rules 13 (no Done without evidence) and 14 (a row whose Test Plan status is "Needs QA" is not trustworthy) are what it serves.

## 1. Flow

`In Progress` -> author finishes, tests green, PR open -> **merge into `main`** -> **`In Review` + label `qa-pending`** (right after the merge) -> QA check (this chapter) ->
`qa-passed`: `Done` | `qa-failed`: back to `In Progress` with the defects listed; the fix goes through a new branch and PR to `main`, then In Review again.

Labels: `qa-pending`, `qa-passed`, `qa-failed` (one at a time; the author sets `qa-pending` when moving to In Review after the merge, the tester replaces it).

## 2. Who and where

- **Who:** a person other than the author: the owner, the other member, or a named tester. An AI agent may write the checklist and drive a browser to collect evidence, but the verdict
  on work an agent produced is given by a person.
- **Where:** `main` at the merge commit, run locally (`PEMS_ENV=local`, [chapter 05](05-environment-and-commands.md)), or `staging` when the owner has promoted `main` to it (a push to `staging` deploys: only on the owner's request).
  Record the commit hash and the environment in the report.
- **Accounts:** one per role the task touches (`tools/TestAccountProvisioner`), never the real administrator.

## 3. "Fully in the UI"

Every step is done through the browser exactly as an end user or administrator would: no SQL, no command line, no API calls, no reading of code.
Set-up that has a screen (users, roles, settings) is done on that screen. If a step can only be done outside the UI, that is a finding (Instructions rule 20 requires licensing, installation,
update and version operations to be UI-completable) and is recorded as a defect or, for infrastructure work, as a named exception.

## 4. Procedure

1. **Prepare (10 min).** Read the issue (*Done when*), the PR description and the Test Plan rows of the backlog row. Fresh browser profile; open the console and network tab.
2. **Write the checklist as a Jira comment "QA checklist"** (template in section 5) before testing: one line per *Done when* item, plus the standard lines below.
3. **Execute in the browser** and mark each line Pass / Fail / Blocked with evidence (screenshot file name, Test Plan case id, or the URL of the rendered page).
   Standard lines for any UI change: golden path; one edge case and one invalid input; every role that must be allowed and one that must be denied; **fa (RTL) and en**;
   **desktop and 375 px**; reload and back-button state; console and network free of errors; two neighbouring screens still work; keyboard focus and Escape on dialogs.
4. **Defects.** Each failure becomes a Jira Bug (or a one-line comment if trivial), linked to the task; set `qa-failed`, move the task back to In Progress; the fix is a new PR to `main` and the task returns to In Review. Retest only the failed lines and their neighbours.
5. **Pass.** Set `qa-passed`, add the result comment, update the Test Plan row (`Needs QA` -> `Passed` with date and tester) in a small PR to `main`, and move the issue to Done.
6. **Time-box.** Effort S: 15-30 minutes; M: about 1 hour; L: split into sessions. QA that finds the checklist cannot be executed is a defect in the task description.

## 5. Templates

**QA checklist comment**
```
QA checklist - <issue key> - commit <hash> - environment <local|staging> - tester <name> - <date>
| # | Check (from Done when / standard) | Result | Evidence |
| 1 | <Done when item 1, as a user action and expected result> | Pass/Fail/Blocked | <screenshot / case id> |
| 2 | Golden path fa + en | | |
| 3 | Role allowed / role denied | | |
| 4 | 375 px and desktop | | |
| 5 | Console and network clean | | |
Verdict: qa-passed | qa-failed - defects: <keys>
```

## 6. Tasks without a screen (docs, plan, CI, design)

The check is a read-through **in the UI that shows the result**: the rendered Markdown in the PR "Files changed" view, the Jira issue, the workflow run page, the board.
Checklist lines: each *Done when* item found; links and tables render; no secret value, internal address or personal data; the text agrees with the guideline and the plan row;
numbers and names are consistent between documents. Record it in the same template.

## 7. Related

Test Plan sheet (cases and QA status), #200 UAT scripts per epic and role (they reuse these checklists), #376-#377 (automated test review and maintenance, the other half of verification).
