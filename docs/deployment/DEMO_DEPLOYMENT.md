# Demo deployment (branch `demo`, https://demo.parmisata.ir)

Pipeline: `.github/workflows/deploy-demo.yml`. Push or merge to `demo` builds, runs the model-drift gate and a database-free test gate, publishes
a self-contained win-x64 build with `env/demo.env`, writes `env/demo.secrets.env` from GitHub secrets, sets `PEMS_ENV=demo` in `web.config`,
uploads by FTP and waits for `GET /health/database` to report `UpToDate` (first deploy installs the database, later deploys update it).

Secrets: `DEMO_FTP_HOST`, `DEMO_FTP_USER`, `DEMO_FTP_PASSWORD`, `DEMO_FTP_DIR` (`/`), `DEMO_FTP_PROTOCOL` (`ftp`; FTPS fails on this host),
`DEMO_FTP_PORT`, `DEMO_SQL_CONNECTION_STRING`, `DEMO_ADMIN_PASSWORD`, `DEMO_SITE_URL`.

## Status on 2026-10-24

| Step | Result |
|---|---|
| Build, drift gate, database-free tests, publish, runtime configuration | pass |
| FTP upload from the GitHub runner to `parmisata.ir` | **pass** (the runner can reach the host) |
| `/health/database` on `demo.parmisata.ir` | **fails: no valid answer** (see below) |

## Why the first runs failed and what to do

1. **Run 1 - drift gate:** the design-time EF factory needs a connection string and CI had none. Fixed: the gate gets a dummy value (no connection is opened).
2. **Run 2 - health check, HTTP 000 for 60 attempts.** Two causes found by probing the site:
   - **No valid TLS certificate.** `http://` answers `301` to `https://`, but the `https://` certificate fails verification (Plesk shows "Domain not secured").
     The health probe now uses `--insecure` so the app state is still visible, and a separate non-fatal step reports the certificate.
     **Action (owner):** Plesk > the subdomain > SSL/TLS Certificates > get a free Let's Encrypt certificate for `demo.parmisata.ir`. WebAuthn/passkeys also need HTTPS.
   - **The application does not answer.** With verification off the connection is accepted, then a request hangs and is reset after ~60 s. That is what a first start
     that is still installing, or one that fails/hangs on startup, looks like (the first run applies ~116 migrations and the seeders).
     The pipeline now enables stdout logging (`logs/stdout*` on the server) and the app writes `logs/pems-*.log`.
     **Action:** after the next deploy read those two files in Plesk > Files > `logs`. The usual causes, in order: the database is not reachable from the web host with
     the connection string in `DEMO_SQL_CONNECTION_STRING` (the host shows `10.45.12.3`; use `Server=10.45.12.3` without `:0`, try `,1433`); the ASP.NET Core Module is not
     installed or the app pool is stopped (HTTP 500.19/502.5 in the log); SQL Full-Text is not installed (only warns).
3. **Upload time:** the first upload is ~280 MB over plain FTP; later runs send only changed files. The step now has a 60-minute limit.

## Checking by hand

```bash
curl -sSk -m 120 https://demo.parmisata.ir/health/database
```

`UpToDate` = done. `UpdateAvailable` / `FirstRun` = installing or waiting for an administrator to apply it on Admin > Database Update.
