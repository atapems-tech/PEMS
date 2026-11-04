---
name: pems-work-block
description: On any go-ahead in PEMS (continue, next, yes, a task): pull main, audit the whole backlog, batch related tasks, state the plan.
---

# PEMS work block (guideline 00 section 0; token rules: guideline 09)

Run before the first edit of every go-ahead, say in your first message that you did. Cheap by design: one `git`, one JQL, one plan.

1. **Pull.** `git fetch origin && git checkout main && git pull`; `git log --oneline -15` to see what other sessions merged. A dirty tree you did not make is left alone and reported.
2. **Audit the open backlog.** One JQL, minimal fields (never `parent`, `description`, `comment`):
   `project = SCRUM AND statusCategory != Done AND issuetype != Epic ORDER BY key` with `fields: ["summary","status","labels"]`, `maxResults` 50, page on. Read in full only what touches your area or dependency chain (`python tools/plan-sync/plan_sync.py show <id>` is cheaper than Jira). Check each against the code (ratchet `tools/UiAudit/adoption-baseline.json`, greps, merged PRs). One verdict per issue: **go / modify / split / merge / cancel (owner's yes) / blocked**.
3. **Batch.** Group by area, files, pattern, dependency chain; a To Do blocker travels with the task it blocks and is done first. One branch per batch, one PR per safe slice, merge each at once.
4. **State the plan** in one message: batches, what the audit changed or proposes to cancel (evidence in a phrase), what is blocked and by what. The go-ahead is the approval; a cancellation still needs the owner's yes.
5. **Start in Jira before the first commit** (skill `pems-jira-handover`), then work. Test with `pems-test-gates`.
6. **Close.** Every issue to In Review with a QA checklist, or one line why not. Update the guideline if a procedure was learned (chapter 06).

Never: start the named issue without its blockers and neighbours; merge under an issue that is not In Progress; plan from a stale checkout.
