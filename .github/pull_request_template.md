## What and why
<!-- one or two sentences -->

Jira: SCRUM-___    Plan ID (#): ___

## Checklist (Instructions rules 7, 22, 25)
- [ ] I read `docs/guidelines/` (README reading map) and the related Jira issues before starting
- [ ] Existing code/components were reused or modified; any new component was added to the library first
- [ ] Guideline updated (`docs/guidelines/learnings-log.md` or a chapter), or: nothing to add
- [ ] Branch, commits and this title start with the Jira key
- [ ] Plan workbook row updated (Status via Jira; Evidence/Report written if closing) and `python tools/plan-sync/plan_sync.py export` rerun so `plan/backlog.csv` is current
- [ ] Supportability Matrix row added or changed
- [ ] CHANGELOG.md entry
- [ ] Tests added or updated; the affected groups pass
- [ ] Schema change has a migration (never edit an applied one); new settings are in every `env/*.env` file
- [ ] New UI uses PEMS.UI components only (no new raw form/table/modal/badge markup, inline styles or CDN references)
