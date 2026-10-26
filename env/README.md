# Environment settings and secrets

Every setting of PEMS lives here, never in code and never in `appsettings.json` (which is intentionally empty).
Each environment has two files:

| File | Content | In git? |
|---|---|---|
| `env/<name>.env` | all non-secret settings (URLs, ports, limits, policies, feature flags, culture, log retention) | yes |
| `env/<name>.secrets.env` | secrets only: database connection string, Redis, FTP account, first administrator password | **no** (git-ignored, never packaged) |
| `env/<name>.secrets.env.example` | template listing the secret keys of that environment | yes |

Environments: `local` (developer machine), `docker` (docker-compose), `demo` (online demo), `staging` (pre-production copy of the demo), `uat` (Azure UAT),
`production-kangan` (Kangan production). All files define exactly the same keys; a test enforces it.

## Which environment is used

`PEMS_ENV=<name>` selects it. Without it, `ASPNETCORE_ENVIRONMENT=Development` means `local` and `UAT` means `uat`;
any other host environment loads no file and reads real environment variables only. `PEMS_ENV_DIR` points to the
folder if it is not found by walking up from the application folder.

Precedence, lowest to highest: `env/<name>.env` < `env/<name>.secrets.env` < real environment variables < command line.
A missing `<name>.env` stops the start with a clear message; missing secrets are allowed when they come from
environment variables.

## File format

`KEY=VALUE` per line, `#` for comment lines. `__` is the section separator: `Ollama__Url=...` is `Ollama:Url`,
`Fido2__Origins__0=...` is the first item of `Fido2:Origins`. Values may be quoted; there are no inline comments or
variable expansion, so a password may contain `#`, `$` and `=`.

## Setting up a machine

```bash
cp env/local.secrets.env.example env/local.secrets.env     # then edit the connection string and admin password
dotnet run --project PEMS.Web
```

- **Docker:** `cp env/docker.secrets.env.example env/docker.secrets.env`, then
  `docker compose --env-file env/docker.secrets.env up --build`.
- **Demo and staging:** see [../docs/deployment/DEPLOYMENT_README.md](../docs/deployment/DEPLOYMENT_README.md). The pipeline writes `env/<name>.secrets.env` from GitHub secrets (`DEMO_SQL_CONNECTION_STRING`,
  `DEMO_ADMIN_PASSWORD`) and sets `PEMS_ENV=demo` in `web.config`; `env/demo.env` ships with
  `dotnet publish -p:PemsEnv=demo`.
- **Kangan production:** fill every `REPLACE` value in `env/production-kangan.env` (it is a normal reviewed commit),
  create `env/production-kangan.secrets.env` on the server (mode 600) and start with `PEMS_ENV=production-kangan`.
- **dotnet ef:** (project `PEMS.Migrations`) the design-time factory reads `ConnectionStrings__PEMSConnection` from the environment or from
  `env/<PEMS_ENV or local>.secrets.env`; it has no fallback and always prints its target database.

## Rules

- A new setting is added to **all** `env/*.env` files (and to the secrets templates if it is secret), read through
  `IConfiguration`, and required with `Required*` (`PEMS.Web/Configuration`) instead of an in-code default.
- A value that would be harmful if public (password, token, key, connection string) goes only into the secrets file.
- No customer-specific code or branches: customer differences are the `production-<customer>.env` file and the licence.
- Not every literal is a setting: classify it first with [../docs/design/CONFIGURATION_CONSTANTS.md](../docs/design/CONFIGURATION_CONSTANTS.md) (setting / policy constant / domain rule / tuning).
- Rotate any secret that was ever pasted into chat, a ticket or a commit.
