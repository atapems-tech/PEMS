# Plan workbook, Jira and developers: one consistent workflow

Status: **decided 2026-10-23** (owner chose "field ownership + scripted sync"). Backlog rows #335-#337.

## 1. Principle

Two systems, one record per task. The backlog row number is the permanent **Plan ID**; the Jira issue key is stored in the workbook
and the Plan ID is stored in Jira. Each field has exactly **one owner**; the other system only displays it. Anything that can drift is
checked by a script in CI, not by memory.

## 2. Field ownership

| Concept | Workbook column | Jira field | Owner | Direction |
|---|---|---|---|---|
| Identity | # (Plan ID) | custom field "Plan ID" (text, unique) | Workbook | workbook -> Jira |
| Jira key | - (the workbook stays tracker-free) | issue key | Jira | kept in Jira and in `plan/tracker-map.csv` written by `push` |
| Title | Task | Summary | Workbook | -> Jira |
| What and why | Evidence, Prompt | Description | Workbook | -> Jira |
| Acceptance criteria | Requirement | Description section "Acceptance" | Workbook | -> Jira |
| Area | Area | Component (company-managed projects) or label `area-<name>` (team-managed, e.g. SCRUM) | Workbook | -> Jira |
| Type | Track | Issue type (Feature->Story, Bug Fix->Bug, other->Task) | Workbook | -> Jira |
| Priority | Priority P0-P3 | Priority Highest/High/Medium/Low | Workbook | -> Jira |
| Milestone | Milestone M0-M6 | Fix version / Epic | Workbook | -> Jira |
| Dependencies | Depends | Issue links "is blocked by" | Workbook | -> Jira |
| Effort estimate | Effort S/M/L/XL | label `effort-M` (estimate), actuals stay in Jira | Workbook | -> Jira |
| Doer | Doer | label `doer-user` / `doer-claude` | Workbook | -> Jira |
| Status | Status | Status | **Jira** | Jira -> workbook |
| Assignee, sprint, time spent, comments | - | same | **Jira** | not copied |
| Done evidence | Evidence/Report (written at close) | comment + development panel | Developer | workbook (in the closing PR) |
| PM confirmation | Confirmed by PM | label `pm-confirmed` | Workbook | -> Jira |

Status mapping: In Review (conditional done: work verified, PR merged into `main`, waiting for the tests and the QA check in the UI, which are one step for now) = workbook In Progress; Not Started = To Do; In Progress and Partial = In Progress (Partial adds label `partial`); Done = Done;
Hold = On Hold; Later = Backlog (label `later`); Cancelled and Obsolete = Won't Do. A row becomes Done in the workbook only through the
pull from Jira **and** a closing PR that fills Evidence (rule 13: no Done without evidence).

## 3. Developer workflow

1. Pick the Jira issue (it carries the Plan ID). Transition it to In Progress (this is the "set the row In Progress" step of rule 7).
2. Branch: `feature/PEMS-123-short-name` (key first).
3. Commits start with the key: `PEMS-123 feat(ui): ...` (Jira links them; `#comment`/`#time` smart commands are optional;
   avoid smart commands in squash-merge titles, which re-run them).
4. Pull request title: `PEMS-123 ...`; description lists the Plan ID and ticks the checklist: plan row updated (Evidence/Report),
   Supportability Matrix row, CHANGELOG, tests, migration/seed if the schema changed (rule 22).
5. PR merged into `main`: Jira moves to In Review (conditional done, label `qa-pending`). QA check in the UI failed: Jira moves back to To Do (label `qa-failed`; the workbook shows Not Started, the label tells why). QA check passed (`docs/guidelines/07-qa-check-in-the-ui.md`): Jira moves to Done; the nightly sync writes the status back; a small PR updates Evidence and the Test Plan status.
6. Changing scope, priority or milestone happens **in the workbook** by pull request; the sync pushes it to Jira. Never edit those
   fields in Jira (the sync would overwrite them and report the conflict).

## 4. Automation (`tools/plan-sync`, backlog #336)

| Command | What it does |
|---|---|
| `export` | writes `plan/backlog.csv` (text mirror, reviewable diff) and `plan/jira-import.csv` (Jira CSV import format: Summary, Description, Priority, Issue type, Component, Fix version, labels, Plan ID, Work item key for updates; parents before children; max ~1,500 rows per file) |
| `push` (needs Jira API) | creates/updates issues by Plan ID, sets links; writes the Plan ID -> issue key map to `plan/tracker-map.csv` (the workbook itself is never given tracker fields) |
| `pull` (needs Jira API) | reads Status (and key) of every issue and updates the workbook Status column via a bot pull request |
| `check` (CI, nightly + on PR) | fails when: a Plan ID is duplicated; a Done row has no Evidence; a workbook row has no Jira issue (checked through `plan/tracker-map.csv`); a Jira issue has no Plan ID; statuses disagree; a commit/PR in the range has no Jira key |

Today only `export` exists (no Jira connector is installed here). Credentials for push/pull/check are **CI secrets, never app settings or
files in git**: `JIRA_BASE_URL`, `JIRA_PROJECT_KEY`, `JIRA_USER_EMAIL`, `JIRA_API_TOKEN`.

## 5. Keeping the workbook trustworthy

- The workbook changes only by pull request; two people never edit it in parallel branches (it is a binary file). `plan/backlog.csv`
  is regenerated on every change so reviewers see a text diff, and a CI check fails if the CSV is stale.
- A weekly plan review reads the `check` report; the Summary sheet and Roadmap formulas are the reporting view, Jira boards the execution view.
- Rows are never reused or renumbered (permanent Plan ID); cancelled work stays as Cancelled/Obsolete.
- If the workbook ever becomes the bottleneck, revisit: make `plan/backlog.csv` the canonical file and generate the workbook from it.

## 6. One-time Jira setup (owner task, #337)

Site and project (2026-10-24): `https://atapems.atlassian.net`, project key **SCRUM** (a team-managed Scrum space). Team-managed spaces have
no Components, so the plan area is exported as the label `area-<name>`; custom fields, statuses and releases (fix versions) are available.
Secrets already set in GitHub: `JIRA_BASE_URL`, `JIRA_PROJECT_KEY`; still to add by the owner: `JIRA_USER_EMAIL`, `JIRA_API_TOKEN`.


Project key; issue types Story/Task/Bug; workflow statuses To Do, In Progress, On Hold, Done, Won't Do; custom field "Plan ID" (text,
unique); components = the 27 plan areas; fix versions = M0-M6; GitHub-for-Jira connection so commits and PRs show on issues; an API token
for a bot user stored as the CI secrets above.

## 7. Sprints, owners and the generated import file

The workbook is tracker-free: it carries only planning facts. Two planning columns exist on the Backlog sheet: **Sprint** (`Sprint 1`,
`Sprint 1 stretch`, empty = backlog) and **Owner** (the accountable person, `Member A` / `Member B` until real names are mapped). The `Sprint 1`
sheet holds the goal, dates, capacity and the committed list.

`python tools/plan-sync/plan_sync.py export` turns the workbook into the Jira import file (`plan/jira-import.csv`, not committed):
- every row becomes a work item whose summary is `[#<Plan ID>] <task>` and whose description has fixed sections: identity line (area, group,
  milestone, priority, effort, owner), *Why*, *What to do*, *Done when*, *Depends on* (with the titles of the blocking tasks) and *Report*;
- milestones M0-M6 become epics and parents; rows with Sprint `Sprint 1` get the Sprint field, `Sprint 1 stretch` rows stay in the backlog with
  the label `sprint-1-stretch`;
- Assignee is filled from `plan/members.json` (copy `plan/members.example.json`; the real file with e-mail addresses is git-ignored);
- the area becomes the label `area-<name>` (team-managed spaces have no Components); effort, doer, PM confirmation and sales-blocker become labels.

Import: Jira -> project SCRUM -> Space settings (or the "..." menu) -> Import work items -> CSV; map Summary, Description, Priority, Status,
Fix Version, Plan ID, Sprint, Assignee and the repeated Labels columns; keep "Work item ID / Parent" so tasks attach to their epic.

