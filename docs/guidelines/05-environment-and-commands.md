# 05 - Environment and commands

## Prerequisites
.NET 10 SDK, SQL Server (LocalDB, Express or container), Node only for tools in `tools/UiAudit`, Python 3 (standard library only) for plan scripts, `gh` CLI, graphify.

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
## Licensing settings and the vendor tool
`Licensing__*` keys are in every `env/*.env` (see LICENSING_TECHNICAL_DESIGN.md section 14.1); `Licensing__Enforce` stays `false` until rollout. The vendor tool runs on an owner computer only: `cp tools/PemsVendorTool/vendor.env.example vendor.env`, then `VENDOR_ENV_FILE=./vendor.env dotnet run --project tools/PemsVendorTool` (loopback only, http://127.0.0.1:5290). Its tests run with the normal `dotnet test PEMS.slnx` (`PEMS.Tests/Licensing`, no SQL Server needed).

## Cloud sessions
A fresh cloud container has no .NET SDK: `apt-get update && apt-get install -y dotnet-sdk-10.0` works through the proxy (the `dot.net` install script host is blocked). `dotnet ef` needs `dotnet tool install -g dotnet-ef --version 10.0.10` and a Debug build of `PEMS.Web` first; `ConnectionStrings__PEMSConnection` can be a dummy value because `migrations add` does not connect. A full build takes about 4 minutes.
Verified 2026-10-27 in a cloud container: `apt-get update && apt-get install -y dotnet-sdk-10.0` (about 2 minutes), `dotnet build PEMS.slnx -c Release` (0 errors), the database-free tests (about 20 seconds), `dotnet tool install dotnet-ef --tool-path <scratch folder> --version 10.0.*` (use `--tool-path` so nothing global is touched), and the drift gate below. **So a cloud session can run everything except the tests that need SQL Server**; only those are listed as "not run" in the report.

### Local gates (the same as CI; run them before the merge)
```bash
dotnet build PEMS.slnx -c Release                                  # 0 errors
dotnet test PEMS.Tests -c Release --no-build --filter "FullyQualifiedName~EnvironmentFilesTests|FullyQualifiedName~TemporaryPasswordGeneratorTests"   # database-free
export ConnectionStrings__PEMSConnection='Server=localhost;Database=PEMS_ModelCheck;Trusted_Connection=True;TrustServerCertificate=True'   # dummy, the gate does not connect
dotnet ef migrations has-pending-model-changes --project PEMS.Migrations --startup-project PEMS.Web --configuration Release --no-build   # must print: No changes have been made to the model since the last migration.
```
`dotnet test` without `--no-build` rebuilds and disturbs a test run that is already in progress: build once, then test and run `ef` with `--no-build`.

## Branch audit (run at the start of a session and once a week)
```bash
git fetch --prune origin
for b in $(git for-each-ref --format='%(refname:short)' refs/remotes/origin | sed 's#origin/##' | grep -v -e '^HEAD$' -e '^main$'); do
  echo "$b | ahead $(git rev-list --count origin/main..origin/$b) behind $(git rev-list --count origin/$b..origin/main) | $(git log -1 --format='%ad %an' --date=short origin/$b)"; done
```
Rules for what it shows are in [chapter 00](00-task-lifecycle.md) section 4a.

## Docker / deploy
`docker compose --env-file env/docker.secrets.env up --build`; docs in `docs/deployment/` (CODE_FIRST_DATABASE, DEMO_DEPLOYMENT, LINUX_VM_DEPLOYMENT, PEMS_UAT_DEPLOYMENT_RUNBOOK). Pipelines in `.github/workflows`: only pushes to `demo` and `staging` start one (`deploy-demo.yml`, `deploy-staging.yml`, both calling the reusable `deploy-site.yml`); `build.yml` and `deploy-uat.yml` are manual; the only check that runs on pull requests is `commit-key-check.yml` (the Jira key `SCRUM-<n>` must be in the PR title, the branch name and every commit subject; merge and bot commits and `main`/`staging`/`demo` heads are exempt; logic and tests in `tools/commit-key-check`, no secrets, no deploy; SCRUM-349); pushes to `main` and working branches run nothing. Full setup and manual deploy/reset: `docs/deployment/DEPLOYMENT_README.md`. Do not deploy, push to protected branches or change pipeline secrets without the user's explicit go-ahead.

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
