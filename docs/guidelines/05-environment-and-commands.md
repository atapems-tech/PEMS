# 05 - Environment and commands

## Prerequisites
.NET 10 SDK, SQL Server (LocalDB, Express or container), Node only for tools in `tools/UiAudit`, Python 3 + `openpyxl` for plan scripts, `gh` CLI, graphify.

## Run
```bash
cp env/local.secrets.env.example env/local.secrets.env   # connection string + admin password
dotnet run --project PEMS.Web                            # http://localhost:5089
```
First start on an empty database creates it, installs the schema and runs seeders (a few minutes). `PEMS_ENV` selects `local|docker|demo|uat|production-<customer>`; real environment variables override files.

## Build / test / migrate
```bash
dotnet build PEMS.slnx -c Release
dotnet test PEMS.slnx                       # many tests need SQL Server; DB PEMSDB_RegressionTest; PEMS_TEST_CONNECTION overrides
dotnet test PEMS.Tests --filter "FullyQualifiedName~EnvironmentFilesTests|FullyQualifiedName~TemporaryPasswordGeneratorTests"   # database-free: env files, compose ports, one-time passwords
dotnet ef migrations add <Name> --project PEMS.Migrations --startup-project PEMS.Web
dotnet ef migrations has-pending-model-changes --project PEMS.Migrations --startup-project PEMS.Web   # drift gate
```

## Verification pointers (no copies here)
Local tests and the known failures: [../../README.md](../../README.md) "Local tests". Manual UI checks: README.md "Manual checks in the UI". Settings checks: [../../env/README.md](../../env/README.md) "Verifying settings". Deployed environment: [../deployment/DEPLOYMENT_README.md](../deployment/DEPLOYMENT_README.md) section 8. QA procedure: [07](07-qa-check-in-the-ui.md).

## Docker / deploy
`docker compose --env-file env/docker.secrets.env up --build`; docs in `docs/deployment/` (CODE_FIRST_DATABASE, DEMO_DEPLOYMENT, LINUX_VM_DEPLOYMENT, PEMS_UAT_DEPLOYMENT_RUNBOOK). Pipelines in `.github/workflows`: only pushes to `demo` and `staging` start one (`deploy-demo.yml`, `deploy-staging.yml`, both calling the reusable `deploy-site.yml`); `build.yml` and `deploy-uat.yml` are manual; `main` and working branches run nothing. Full setup and manual deploy/reset: `docs/deployment/DEPLOYMENT_README.md`. Do not deploy, push to protected branches or change pipeline secrets without the user's explicit go-ahead.

## Windows notes
Primary shell is PowerShell on Windows 11; Git Bash is available. Quote paths with spaces (`0 - refs`, `task manager`). Line endings follow `.gitattributes`.

## Knowledge graph
`graphify query "<question>"` before raw grep for architecture questions; `graphify update .` after code changes. `.claude/settings.json` hooks call a graphify binary at a per-user path - fix the path on your machine locally, do not commit personal paths.

## Atlassian MCP
Site `atapems.atlassian.net`, cloudId `9290a713-17c1-44bd-99fc-f1b83705cc69`, project `SCRUM`. Tools: `getJiraIssue`, `searchJiraIssuesUsingJql`, `transitionJiraIssue`, `addOrEditJiraIssueComment`, `createJiraIssue`, `editJiraIssue`. Slim large responses with `responseFields`. Custom fields (Plan ID) need `view: evidence/full` or the site's `customfield_*` id.

## Slack notifications (set up 2026-10-25)
Workspace `pems` (pems-corp.slack.com).
- `#pems-jira`: Jira Cloud app, project SCRUM, all event groups (issue created/updated/deleted including description and sprint changes, comments, assignee, status, issue links, worklogs), full-text style. Manage it in Jira: space PEMS > Slack integration.
- `#pems-github`: GitHub app, repo `atapems-tech/PEMS`: issues, pulls, reviews, comments, commits on all branches, branches, releases, deployments, workflows (push, pull_request, workflow_dispatch). Change it with `/github subscribe|unsubscribe atapems-tech/PEMS <features>` in the channel; `/github subscribe list features` lists the options.
- New team members join both channels; the GitHub app links per person with `/github signin`.
