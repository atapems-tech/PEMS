# 07 - QA check in the UI (after the merge, while the task is In Review)

Owner decision 2026-10-26. Read it when you move a task to In Review, when you are asked to QA a task, and when you review a pull request.

**In Review = conditional done**: the author verified the work as far as the session allows, the PR is **merged into `main`**, and the tests are either green or listed as not run (section 2a) ([chapter 00](00-task-lifecycle.md) section 4). **QA check = an independent
person confirms it in the real user interface of what is now on `main`, before Done.** Automated tests (chapter 01) and this check complement each other; **for now they are one step** (section 2a): the test run is line 1 of the checklist.
Instructions rules 13 (no Done without evidence) and 14 (a row whose Test Plan status is "Needs QA" is not trustworthy) are what it serves.

## 1. Flow

`In Progress` -> author finishes, runs the tests if the session can, PR open -> **merge into `main`** -> **`In Review` + label `qa-pending`** (right after the merge) -> QA check (this chapter) ->
`qa-passed`: `Done` | `qa-failed`: back to **`To Do`** with the defects listed (owner decision 2026-10-26); whoever takes it up moves it to In Progress, the fix goes through a new branch and PR to `main`, then In Review again.

**Timing (owner decision 2026-10-27):** the author does not wait for this check. The author hands over with `In Review` + `qa-pending` and a test note ("Tests: passed" with counts, or "Tests: not run / not done" with the reason), then continues with the next task. The QA checks are run by someone else, as a block at the end of the sprint once all sprint tasks are In Review; Done comes only from that verdict.

Labels: `qa-pending`, `qa-passed`, `qa-failed` (one at a time; the author sets `qa-pending` when moving to In Review after the merge, the tester replaces it).

## 1a. Entering and re-entering In Review (author's duty)

Whoever moves the issue to In Review - the first time **and every time after a failed QA** - checks, in this order, and says so in the move comment:

1. **QA checklist present.** The issue has a Jira comment "QA checklist" (template in section 5) with line 1 = local tests, one line per *Done when* item, the standard lines, and the **how to test** as references to the README sections of section 5a (never copied steps). The author drafts it; the tester completes and runs it.
2. **Test note present, and the test lines marked.** The checklist contains the task's automated tests as lines and the author's results are posted as "QA checklist - author results" (section 5): `Pass (author, date)` for what ran, `not done - reason` for the rest. The move comment has a "Tests" paragraph: the commands that were run with their counts, or the exact commands that were **not** run and why (section 2a). Docs-only tasks say "n/a (docs only)" and name the read-through of section 6.
2a. **Not-run lines present** (section 1b): the SQL Server tests, the HTTP smoke tests and the browser check appear as lines marked `not done - <reason>` whenever the author did not run them.
3. **README up to date.** Any new manual check or local test of this task is already in the right existing README (section 5a) and the checklist refers to it.
4. **Re-entry after `qa-failed`** (the issue came back through To Do): do not edit or delete the earlier checklist. Re-check it against the new commit and add a new comment "QA checklist - round N" with: every failed line to retest, one regression line per defect (Bug keys), lines for the areas the fix touched, and two neighbouring checks; update the Tests paragraph with the new commit and counts; replace `qa-failed` by `qa-pending`.
5. If one of these is missing the issue is not ready: the tester starts nothing, marks the checklist line "Blocked: no QA/test note" and asks the author (the issue does not count as In Review for planning until fixed).

## 1b. Nothing that was not run lives only in a message (owner decision 2026-10-27)

Jira is the only place a tester, or an agent that has no access to the chat, can read. Therefore:

- **Every test, check or step the author did not run is a line of the QA checklist** with the result `not done - <reason>` (never a sentence only in the move comment, the PR or the chat). The line names the exact command or the README check ID, so it can be run without asking.
- **The standard "not run" lines** a cloud-session author adds unless they really ran them: (a) the tests that need SQL Server: `dotnet test PEMS.slnx` (full suite), with the known failures of README.md "Local tests"; (b) the HTTP smoke tests (`FileSecurityHttpTests` and the other tests that start the application); (c) the browser check: golden path, fa and en, desktop and 375 px, console and network clean; (d) any manual check ID (M-n) the task added or touched; (e) for a docs-only or design task, the human read-through of section 6.
- **Open owner decisions are lines too** (`open - owner decides: <question>, default <value>`), and so is every part of the *Done when* that is not met yet (`open - <what is missing>`), so the issue says what remains until Done.
- **The issue is self-contained.** From the Jira issue alone a reader must see: what was merged (PR, commit), what was run (counts), every line still open and why, and which follow-up issues exist. If the issue stays In Progress because only a first step is merged, post the same checklist as a draft and say which lines belong to which step.
- A checklist with a line marked `not done` is complete; a checklist that omits what the author could not run is incomplete (section 1a, point 5).

## 2. Who and where

- **Who:** a person other than the author: the owner, the other member, or a named tester. An AI agent may write the checklist and drive a browser to collect evidence, but the verdict
  on work an agent produced is given by a person.
- **Where:** `main` at the merge commit, run locally (`PEMS_ENV=local`, [chapter 05](05-environment-and-commands.md)), or `staging` when the owner has promoted `main` to it (a push to `staging` deploys: only on the owner's request).
  Record the commit hash and the environment in the report.
- **Accounts:** one per role the task touches (`tools/TestAccountProvisioner`), never the real administrator.

## 2a. Tests and review are one step (for now)

Owner decision 2026-10-26, until a CI gate exists (plan row #7): the local test run is **line 1 of every QA checklist** ([README.md](../../README.md) "Local tests"), done by the tester together with the UI checks.
- An author who can run the tests does so before the merge and writes the counts in the report; the tester repeats the run (line 1).
- An author in a cloud session installs the SDK first ([chapter 05](05-environment-and-commands.md) "Cloud sessions") and runs everything that does not need SQL Server (build, database-free tests, drift gate). Only the tests that need SQL Server may be reported as not run: the author lists their exact commands and the failure reason, merges, and moves the issue to In Review; line 1 then carries the first SQL run.
- **Done needs both**: line 1 green (only the known failures of README.md "Local tests" may stay red) and all other lines Pass. A red test is a defect like any other: `qa-failed`, back to To Do, fix by a new PR.
- When CI runs the tests on every merge, line 1 becomes "CI run on the merge commit is green" and this section is removed.

## 3. "Fully in the UI"

Every step is done through the browser exactly as an end user or administrator would: no SQL, no command line, no API calls, no reading of code.
Set-up that has a screen (users, roles, settings) is done on that screen. If a step can only be done outside the UI, that is a finding (Instructions rule 20 requires licensing, installation,
update and version operations to be UI-completable) and is recorded as a defect or, for infrastructure work, as a named exception.

## 4. Procedure

1. **Prepare (10 min).** Read the issue (*Done when*), the PR description and the Test Plan rows of the backlog row. Fresh browser profile; open the console and network tab.
2. **Complete the checklist** the author drafted when moving to In Review (section 1a; write it yourself if it is missing, and tell the author): one line per *Done when* item, plus the standard lines below.
   **Do not copy steps that already exist: refer to them** (section 5a). If the task introduces a manual check or a local test, add it to the right existing README (never a new file) and refer to it from the checklist.
3. **Execute in the browser** and mark each line Pass / Fail / Blocked with evidence (screenshot file name, Test Plan case id, or the URL of the rendered page).
   Line 1 is always the local test run (section 2a). Standard lines for any UI change: golden path; one edge case and one invalid input; every role that must be allowed and one that must be denied; **fa (RTL) and en**;
   **desktop and 375 px**; reload and back-button state; console and network free of errors; two neighbouring screens still work; keyboard focus and Escape on dialogs.
4. **Defects.** Each failure becomes a Jira Bug (or a one-line comment if trivial) that goes through the normal task flow ([chapter 00](00-task-lifecycle.md) section 3a), linked to the task; set `qa-failed` (replacing `qa-pending`), add the defect list as a comment and move the task **back to To Do**; it keeps its assignee and all QA comments. The fix is a new PR to `main` and the task returns to In Review. Retest only the failed lines and their neighbours.
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

**Tests are lines of the checklist, and the author pre-marks what was run** (owner decision 2026-10-27). Every automated test that proves a *Done when* item gets its own line (class or filter, expected count) next to line 1, and a mechanical proof (a green and a red pipeline run, a mutation run) gets a line too. The author fills the `Result` column only for lines the author really ran, in the form `Pass (author, <date>)` or `Fail (author)` with the evidence (counts, run link, commit); every line not run says `not done - <reason>`. The tester re-runs the pre-marked lines that are cheap (line 1 always) and records `Pass (tester, <date>)`; a line the author could not run stays open until a tester runs it. Because a posted checklist is never edited (section 7), the author adds the marks as a new comment "QA checklist - author results" that repeats the table with the results filled in, and later rounds repeat it again.

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
