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
dotnet ef migrations add <Name> --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef migrations has-pending-model-changes --project PEMS.Persistence --startup-project PEMS.Web   # drift gate
```

## Docker / deploy
`docker compose --env-file env/docker.secrets.env up --build`; docs in `docs/deployment/` (CODE_FIRST_DATABASE, DEMO_DEPLOYMENT, LINUX_VM_DEPLOYMENT, PEMS_UAT_DEPLOYMENT_RUNBOOK). Pipelines in `.github/workflows`: `build.yml` (restore, build, drift gate, tests), `deploy-demo.yml` (push to `demo`), `deploy-uat.yml` (manual). Do not deploy, push to protected branches or change pipeline secrets without the user's explicit go-ahead.

## Windows notes
Primary shell is PowerShell on Windows 11; Git Bash is available. Quote paths with spaces (`0 - refs`, `task manager`). Line endings follow `.gitattributes`.

## Knowledge graph
`graphify query "<question>"` before raw grep for architecture questions; `graphify update .` after code changes. `.claude/settings.json` hooks call a graphify binary at a per-user path - fix the path on your machine locally, do not commit personal paths.

## Atlassian MCP
Site `atapems.atlassian.net`, cloudId `9290a713-17c1-44bd-99fc-f1b83705cc69`, project `SCRUM`. Tools: `getJiraIssue`, `searchJiraIssuesUsingJql`, `transitionJiraIssue`, `addOrEditJiraIssueComment`, `createJiraIssue`, `editJiraIssue`. Slim large responses with `responseFields`. Custom fields (Plan ID) need `view: evidence/full` or the site's `customfield_*` id.
