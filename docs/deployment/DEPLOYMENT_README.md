# Deploying PEMS to demo and staging

How the two online environments are built, deployed, secured, reset and recovered. Everything here was written for the shared Plesk/IIS host
(`parmisata.ir`); the same steps apply to any Windows host with IIS, the ASP.NET Core Module and SQL Server.
Background and failure history of the pipeline: [DEMO_DEPLOYMENT.md](DEMO_DEPLOYMENT.md). Database behaviour: [CODE_FIRST_DATABASE.md](CODE_FIRST_DATABASE.md).

## 1. The model in one table

| Branch | What a push does | Subdomain | Database | Settings file | Secrets prefix |
|---|---|---|---|---|---|
| `main` | **nothing** (no workflow runs) | - | - | - | - |
| any working branch (`feature/...`, `fix/...`, `docs/...`) | **nothing** | - | - | - | - |
| `staging` | builds, tests, deploys | `staging.parmisata.ir` | its own staging database | `env/staging.env` | `STAGING_` |
| `demo` | builds, tests, deploys | `demo.parmisata.ir` | its own demo database | `env/demo.env` | `DEMO_` |

- Only pushes to `staging` and `demo` start a pipeline. `build.yml` and `deploy-uat.yml` are manual (`workflow_dispatch`) only.
- Promotion is a merge, in this order: working branch -> `main` -> `staging` (check it) -> `demo`.
  `git checkout staging && git merge main && git push`, then the same for `demo` once staging is good.
- The workflows live in `.github/workflows`: `deploy-site.yml` (the one reusable pipeline), `deploy-staging.yml` and `deploy-demo.yml` (thin callers that pass the target and the secrets prefix).
- Never commit secrets. Settings are in `env/<name>.env`, secrets only in GitHub secrets and in `env/<name>.secrets.env` on the server (git-ignored).

## 2. One-time setup per environment

Do the same for `demo` and for `staging`; only the names differ.

### 2.1 Create the subdomain (Plesk)
1. Plesk > **Websites & Domains** > the main domain (`parmisata.ir`) > **Add Subdomain**.
2. Subdomain name `demo` (or `staging`). Keep the suggested document root (for example `/demo.parmisata.ir`); it is the home folder of the FTP account in 2.3.
3. If DNS is not managed by Plesk, add an **A record** (`demo`, `staging`) pointing to the server IP at your DNS provider.
4. Subdomain > **Hosting Settings**: make sure the ASP.NET Core Module / IIS site is active; tick **Permanent SEO-safe 301 redirect from HTTP to HTTPS** after the certificate exists.
5. Subdomain > **SSL/TLS Certificates** > **Let's Encrypt** (DNS must already resolve) > get the free certificate. WebAuthn/passkeys need HTTPS.

### 2.2 Create the database
The hosting login usually cannot `CREATE DATABASE`, so the database is created in the panel and is **empty**; the application installs the schema itself on first start.
1. Plesk > **Databases** > **Add Database** (type Microsoft SQL Server): name `pems_demo` / `pems_staging`; create a **database user** with full rights on that database only.
2. Never reuse a database between environments and never point demo or staging at a live database.
3. Connection string (keep it for the secret):
   `Server=<db host>[,1433];Database=pems_demo;User Id=<user>;Password=<password>;TrustServerCertificate=True`
   (use the host or IP shown by Plesk without `:0`; try `,1433` if it does not connect).
4. SQL Full-Text Search is optional (the app only warns without it).

### 2.3 Create the FTP account
Subdomain > **FTP** > **Add FTP Account**: user `pems-demo-deploy` / `pems-staging-deploy`, home directory = the subdomain document root, strong password. One account per site.

### 2.4 GitHub secrets
Repository > **Settings > Secrets and variables > Actions > New repository secret** (or `gh secret set NAME`). Two sets, the prefix is `DEMO_` or `STAGING_`:

| Secret | Value |
|---|---|
| `<P>_FTP_HOST` | FTP host name, for example `parmisata.ir` |
| `<P>_FTP_USER` / `<P>_FTP_PASSWORD` | the FTP account of 2.3 |
| `<P>_FTP_DIR` | remote folder **relative to the FTP account's home**, with trailing slash. If the account home is the subdomain folder (section 2.3) this is `/` (this host). A wrong value fails with `550 The system cannot find the file specified`; the pipeline's early FTP check then lists the folders it can see |
| `<P>_FTP_PROTOCOL` | optional: `ftp`, `ftps` or `ftps-legacy` (default `ftps`; this host works with `ftp` only) |
| `<P>_FTP_PORT` | optional, default 21 |
| `<P>_SQL_CONNECTION_STRING` | connection string of 2.2 |
| `<P>_ADMIN_PASSWORD` | password of the first administrator, used only on an empty database; change it at first sign-in |
| `<P>_SITE_URL` | `https://demo.parmisata.ir` / `https://staging.parmisata.ir` (enables the post-deploy health check) |

Rotate any secret that was ever pasted into chat, a ticket or a commit.

### 2.5 Branches
`demo` and `staging` are long-lived. Protect them in GitHub (**Settings > Branches**: pull request or restricted pushes, no force push). `main` is protected the same way; none of them runs a build on `main`.

## 3. What the pipeline does (`deploy-site.yml`)

1. Restore, build, **model-drift gate** (`dotnet ef migrations has-pending-model-changes`), database-free test gate.
2. `dotnet publish -r win-x64 --self-contained -p:PemsEnv=<target>` (ships `env/<target>.env`).
3. Writes `env/<target>.secrets.env` from the `<P>_SQL_CONNECTION_STRING` and `<P>_ADMIN_PASSWORD` secrets and sets `PEMS_ENV=<target>` and a 15-minute startup limit in `web.config`.
4. Uploads `app_offline.htm` (the app releases its file locks), uploads the files by FTP (180 s timeout, one retry), removes `app_offline.htm`.
5. Polls `GET <SITE_URL>/health/database` until `200 UpToDate` (the first start installs the schema, later starts apply new migrations), then reports the TLS certificate state.

Watch a run: GitHub > **Actions**, or `gh run watch`. Slack (`#pems-github`) posts every run. Documentation-only pushes (`docs/**`, `plan/**`, `*.md`) do not redeploy.
Run it by hand: Actions > the workflow > **Run workflow** (choose the branch), or `gh workflow run deploy-staging.yml --ref staging`.

## 4. Manual deploy (when the host is unreachable from GitHub or the pipeline is broken)

```powershell
# 1. On a build machine, from the branch you want to ship
git checkout staging            # or demo
dotnet publish PEMS.Web/PEMS.Web.csproj -c Release -r win-x64 --self-contained true -p:PemsEnv=staging -o .\publish
# 2. Create publish\env\staging.secrets.env (never commit it):
#      ConnectionStrings__PEMSConnection=<connection string>
#      Seed__Admin__Password=<admin password>
# 3. In publish\web.config set  PEMS_ENV=staging  (environmentVariables under <aspNetCore>) and startupTimeLimit="900"
# 4. Upload app_offline.htm to the site root, wait ~30 s, upload the contents of .\publish (FileZilla/WinSCP/Plesk File Manager), delete app_offline.htm
# 5. Open https://staging.parmisata.ir/health/database  -> 200 UpToDate
```
If the site keeps showing the "being updated" page, delete `app_offline.htm` in Plesk > **Files**. Logs: `logs/stdout*` and `logs/pems-*.log` in the site folder.

## 5. Manual reset of an environment's data

A reset returns demo or staging to an empty database that reinstalls itself. **It destroys all data of that environment only.**

1. Put the site offline: upload `app_offline.htm` (section 4).
2. Empty the database, either way:
   - **Preferred:** Plesk > **Databases** > drop the database and add it again with the same name, user and password (the connection string stays valid).
   - **If the panel cannot recreate it:** run this once as the database user to drop everything, then continue.
     ```sql
     DECLARE @sql nvarchar(max) = N'';
     SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
       FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id = t.object_id JOIN sys.schemas s ON t.schema_id = s.schema_id;
     SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id;
     EXEC sp_executesql @sql;   -- repeat once if full-text catalogs or views remain
     ```
3. Delete `app_offline.htm`. The first request starts the app, which detects an empty database, installs the schema, runs the seeders and creates the administrator from `<P>_ADMIN_PASSWORD`.
4. Wait for `GET /health/database` = `200 UpToDate` (a few minutes), sign in as `admin`, change the password.
5. Reload the demo content (team task, plan row #304) from the demo overlay or by importing the team's Excel files in **Import Center**.

Not available yet (planned): a UI to export the current data as a versioned, hashed package, import it, and reset to a baseline package on every push, with a version-compatibility precheck (plan rows #352 and #353, SCRUM-369 and SCRUM-370; nightly/admin reset #141). Until they exist, use this manual procedure.

## 6. Roll back
Re-run the deploy workflow on the previous good commit (`git revert` on the branch, or `gh workflow run ... --ref <branch>` after resetting the branch to the good commit). The schema moves forward only (rule 6): if a bad migration was applied, restore the database from the host's backup and redeploy the previous build.

## 7. Checklist for a new environment
- [ ] subdomain, DNS and certificate (2.1)
- [ ] empty database and user (2.2), FTP account (2.3)
- [ ] `env/<name>.env` and `env/<name>.secrets.env.example` exist and have the same keys as the others (a test checks this)
- [ ] seven required GitHub secrets (2.4; protocol and port are optional), branch created and protected (2.5)
- [ ] first push -> green run -> `/health/database` is `UpToDate` -> administrator signs in
