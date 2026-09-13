# PEMS UAT Deployment — Phase 1-3 Report (Repository Audit, Hosting Compatibility, Proposed Architecture)

**Status: Phases 1-3 complete + safe Phase 4 hardening applied. Phases 5-14 (actual account provisioning, live deployment, real multi-user testing) are BLOCKED — see §5 "Stop Condition" at the end of this document. This is not a "PEMS UAT READY" report; no live deployment has occurred and none is claimed.**

---

## 1. Phase 1 — Repository Audit (no modifications during this phase)

**Starting state:** branch `main`, HEAD `6b40d99`, remote `https://github.com/saberahmadi48-collab/PEMS-cmms-edit-empoleeys-master.git`. Working tree already carried substantial uncommitted changes from two prior, unrelated sessions (the Letter/Correspondence Workspace phases) — these were not touched or reviewed as part of this audit; they are noted here only so the starting state is accurately recorded per §33.

### Solution
- `.NET 10` (`net10.0`) across all projects, SDK `10.0.400` installed locally.
- `PEMS.slnx` (new-style slnx solution file), 9 projects (`Domain`, `Application`, `Persistence`, `Infrastructure`, `AI`, `Shared`, `Web`, `WorkflowPlatform`, `Tests`).
- `PEMS.Web` is `Microsoft.NET.Sdk.Web`, no unusual MSBuild customization (no `Directory.Build.props`, no `FrameworkReference` overrides).

### Web (`Program.cs`, `appsettings.json`)
- **Global authentication gate**: every controller requires an authenticated user by default (`AuthorizeFilter` with `RequireAuthenticatedUser()`), only `[AllowAnonymous]` actions (Login) are public. This is the existing, correct security posture — preserved.
- **Identity**: real ASP.NET Core Identity (`ApplicationUser : IdentityUser<int>`, `ApplicationRole`), 9 seeded roles (`SystemRoles`: `SystemAdmin`, `EngineeringManager`, `EngineeringSupervisor`, `DisciplineEngineer`, `DocumentController`, `MaintenanceEngineer`, `ContractManager`, `QAManager`, `Viewer`) plus a full custom Permission/RolePermissionGrant/SoD authorization layer on top (`AccessControlService`, multiple `AccessControlSeeder` phases). An Admin UI already exists (`Areas/Admin/Controllers/UsersController.cs`, `RolesController.cs`) for user/role management — **no new user-management mechanism is needed**.
- **Database**: `AddDbContext<PEMSDbContext>` reads `ConnectionStrings:PEMSConnection` from configuration (no hardcoded value in `Program.cs` itself — only the local dev default lives in `appsettings.json`, and that default uses `Trusted_Connection=True`, i.e. no password at all, so **no secret is committed anywhere in this repository today**). Confirmed no `Database.Migrate()` or `Database.EnsureCreated()` call anywhere at startup — schema must be migrated explicitly before first run, which is the safer of the two designs and requires no change.
- **Background jobs**: Hangfire, using the *same* SQL Server connection for its own job-storage tables, plus `AddHangfireServer()` (an always-running worker). Dashboard at `/hangfire`, gated by a custom `HangfireDashboardAuthFilter` (SystemAdmin-only) — already secure, no public exposure.
- **Caching**: Redis is fully optional — falls back to in-process `IMemoryCache` automatically when `ConnectionStrings:Redis` is empty (already the default). No external Redis dependency required for UAT.
- **AI/Ollama**: an `HttpClient` for `IOllamaService` is registered pointing at `http://localhost:11434/` by default (overridable via `Ollama:Url`). This registration is lazy — it does **not** attempt a connection at startup, so an unreachable Ollama does not prevent the app from starting. It **will** cause failures the moment a feature that calls it is used (Chat Assistant, Engineering Copilot, Document AI ingestion/semantic search). Confirmed via code reading, not guessed.
- **File storage — two systems coexist, both real, neither duplicated by this audit:**
  1. `IFileStorageService` → `LocalFileStorageProvider`, rooted at `Path.Combine(Directory.GetCurrentDirectory(), "filestore")` — **local disk only**, used by (among others) `LetterController.UploadAttachment`. No cloud backend exists for this path today.
  2. The newer "Enterprise File Storage Management" system (`IStorageProviderClientFactory` → `StorageProviderClientFactory`) supports exactly four provider types: `Local`, `NetworkShare`, `Ftp`, `Sftp`. **No S3 or Azure Blob provider exists in this codebase today.** Credentials for FTP/SFTP providers are encrypted at rest via `ICredentialProtector` (ASP.NET Core Data Protection) — already a sound design, reused as-is.
  - Neither system was modified. This is the key input to the Phase 3 storage decision below.
- **Static file security**: a custom middleware blocks direct `/uploads/documents`, `/uploads/letters`, `/uploads/vdr`, etc. at the HTTP layer (defense-in-depth, independent of `IFileStorageService`'s already-secure root). Confirmed intact, unaffected by this phase.
- **Missing before this phase, now added (see §4 "Phase 4" below)**: no `/health` endpoint, no forwarded-headers handling for a reverse-proxy deployment.
- **`appsettings.json` — confirmed clean**: no passwords, no API keys, no SMTP/FTP credentials (`Ftp:Password` is an empty string placeholder), `Fido2` points at `localhost` (dev-only, must be reconfigured for any real domain — see §3.3).

### Persistence
- 1 `DbContext` (`PEMSDbContext`), migrations tracked in `PEMS.Persistence/Migrations/` starting `20260812075929_InitialCreate` through the current session's uncommitted `20260912135019_LetterControlledLifecycle` — a long, continuous, apparently-linear history (no evidence of destructive/reset migrations).
- `PEMSDbContextFactory` (the design-time factory used by `dotnet ef` commands) was already fixed in a prior session this month to correctly honor a `ConnectionStrings__PEMSConnection` environment-variable override rather than always targeting the live local `PEMSDB` — this directly satisfies this phase's own §2 requirement ("audit `PEMSDbContextFactory` before performing any migration") and required no further change.
- ~40 idempotent seeders run on **every application startup** inside the `using (var scope = ...)` block in `Program.cs` (roles, permissions, departments, disciplines, maintenance taxonomy, workflow definitions, numbering backfills, etc.). This is a real, measurable cold-start cost — see §7 performance note below. All are explicitly commented as idempotent in the source; none were audited line-by-line for correctness in this phase (out of scope — they are pre-existing, working code, not something this UAT task should modify).

### Application/Domain modules
Confirmed present and untouched: Identity/RBAC/SoD, the generic Workflow Platform, Engineering Documents + revisions + comments + hold, Letters/Correspondence (this session's own prior work), Document Register, VDR, full CMMS (Equipment, Functional Location, Maintenance Strategy/Plan, PM generation, RCM/FMEA/FMECA, Work Request/Work Order + release gates, Store Requisition, Permit-to-Work + Gas Test + SIMOPS + Isolation), Inventory/Material Requisition, Reliability/RAM/Predictive Maintenance, and the reporting services for each domain. **No module was recreated, simplified, or removed.**

### Existing DevOps assets found (reused, not recreated)
- **`Dockerfile`** — an already-correct multi-stage build: SDK `10.0` → `dotnet publish` → `aspnet:10.0` runtime, installs `tesseract-ocr` (needed for the OCR feature), listens on port `8080`, sets `ASPNETCORE_ENVIRONMENT=Production` by default. This Dockerfile needed **zero changes** to be usable for a Docker-based host.
- **`docker-compose.yml`** — bundles its own SQL Server + Ollama containers for local/on-prem use. Note for the record (not a UAT blocker, just a hygiene finding): it has a hardcoded fallback SA password (`Pems@2026StrongPass`) used only if `SQL_SA_PASSWORD` is unset — this file must never be deployed to any Internet-reachable host without explicitly overriding that variable; it is not proposed for UAT use at all (see §3).
- **`.github/workflows/build.yml`** — an existing CI workflow (restore/build/test on push+PR to `main`/`develop`). Reused as the pattern for the new UAT deploy workflow (see §4) rather than inventing a different CI convention.

---

## 2. Phase 2 — Hosting Compatibility Audit (evidence-based, via live web search September 2026)

| Requirement | Azure Container Apps (consumption) | Azure App Service F1 (Free) | Render (free web service) | Railway |
|---|---|---|---|---|
| ASP.NET Core / .NET 10 (Docker) | Yes — any Linux container, x64 | Yes, but F1 tier is documented as 32-bit only and "not recommended for reliable container deployments" per Microsoft's own guidance | Yes — Docker is a first-class deploy method | Yes |
| Real SQL Server | Pairs with **Azure SQL Database free offer** (same TDS/T-SQL engine `Microsoft.Data.SqlClient` already targets — no provider change) | Same pairing possible | **No managed SQL Server product at all** — only Postgres/Redis | **No managed SQL Server product at all** |
| EF Core / Identity | Unaffected — runs unmodified in any Linux container | Unaffected | Unaffected (app layer only) | Unaffected (app layer only) |
| HTTPS | Automatic TLS on the default `*.azurecontainerapps.io` domain | Automatic on `*.azurewebsites.net` | Automatic on `*.onrender.com` | Automatic |
| GitHub deployment | `az containerapp up` / GitHub Actions (official Azure actions) | GitHub Actions supported | Native Git-based auto-deploy or Docker | Native Git-based auto-deploy |
| Background jobs (Hangfire always-on worker) | **Conflict**: consumption plan scale-to-zero kills the Hangfire server between requests; keeping 1 replica warm 24/7 to avoid this would consume far more than the entire monthly free grant (see math in §2.1) | 60 CPU-min/**day** hard cap — also insufficient for an always-on worker | Free web services "spin down after 15 min of inactivity" — same conflict | Similar constraint, and no genuine ongoing free tier exists any more (see below) |
| Environment variables/secrets | Yes — Container Apps secrets + env vars | Yes — App Service Configuration | Yes — Render environment groups | Yes |
| Persistent file storage | **No** — container filesystem is not persisted across restarts/scale events in the consumption plan; would need a paid Azure Files mount | Persistent-ish (App Service has a persistent `/home` share) but combined with the other F1 limits, not a realistic candidate | **No** — free tier disk is ephemeral, persistent disks are a paid add-on | Has a "1GB disk" mentioned in 2026 pricing summaries, but the platform itself is no longer meaningfully free (see below) |
| Concurrent users | Should be fine for a handful of UAT testers on either Azure option (compute is the real constraint, not concurrency) | Severely constrained by the 60 CPU-min/day cap under any real multi-user session with ~40 startup seeders | Should be fine, but cold-start after 15 min idle will surprise testers with a slow first request each session | N/A — not free |
| Free-tier limitations | Generous **monthly pool** (180,000 vCPU-seconds + 360,000 GiB-seconds + 2M requests/month, ongoing, not a trial) — but only if the app is allowed to scale to zero between test sessions, which breaks Hangfire | Extremely tight **daily** cap; F1 capacity is documented as sometimes unavailable in a region; 32-bit-only | Free web service explicitly "useful for development but not suitable for production"-grade availability | Converted from a real free tier to a one-time $5 trial credit, then $1/month minimum — **no longer a genuine ongoing free tier** as of 2026 |
| UAT suitability | **Best available fit, with disclosed trade-offs (file storage, Hangfire)** | Not suitable — resource caps too tight for this app's real weight | Suitable for the *app* only; cannot host the *database* for free | Not suitable — not genuinely free any more |

**Sources:** [Azure SQL Database free offer FAQ](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer-faq?view=azuresql) · [Enhanced Azure SQL Database Free Offer — GA announcement](https://techcommunity.microsoft.com/blog/azuresqlblog/introducing-the-enhanced-azure-sql-database-free-offer-now-generally-available/4372418) · [Azure Container Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/) · [Azure App Service update: Free Linux Tier](https://azure.microsoft.com/en-us/blog/azure-app-service-update-free-linux-tier-python-and-java-support-and-more/) · [Quickstart: Run a Custom Container on App Service](https://learn.microsoft.com/en-us/azure/app-service/quickstart-custom-container) · [Render: platforms with a real free tier in 2026](https://render.com/articles/platforms-with-a-real-free-tier-for-developers-in-2026) · [Render Pricing 2026 — srvrlss.io](https://www.srvrlss.io/provider/render/) · [Railway Free Tier in 2026 — Medium](https://medium.com/@kuberns/railway-free-tier-in-2026-what-you-get-and-when-it-runs-out-2101fdca0998) · [Railway Pricing 2026 — srvrlss.io](https://www.srvrlss.io/provider/railway/)

### 2.1 Why "just keep it always-on" doesn't work on the free grant
Azure Container Apps' free grant is 180,000 vCPU-seconds/month. Running even a modest 0.5 vCPU replica continuously for a month is `0.5 × 2,628,000 seconds ≈ 1,314,000 vCPU-seconds` — over 7× the entire monthly free allowance. This is exactly the kind of limitation §3 of the brief instructs me not to paper over: **the free tier is only free if the app is allowed to idle/scale to zero**, which conflicts with Hangfire's need for a continuously-running worker process and with wanting zero cold-start delay for UAT testers. This is disclosed, not hidden.

### 2.2 Recommendation
**Azure Container Apps (consumption plan) for the app + Azure SQL Database (free offer) for the database**, using the existing `Dockerfile` unmodified. This is the only combination found that satisfies "real SQL Server, real HTTPS, real ASP.NET Core/.NET 10, genuinely free, GitHub-deployable" simultaneously, without switching database providers (forbidden by §7) and without forcing an unsuitable platform (forbidden by §3). Two trade-offs are accepted and disclosed rather than hidden:
- **File storage will be ephemeral** unless a paid Azure Files mount is added later. For a UAT environment whose purpose is testing *workflows and authorization*, not long-term document retention, this is a reasonable, disclosed trade-off — not a silent one.
- **Hangfire's background worker will only run reliably while the container is warm.** Recommendation: set `--min-replicas 1` for the UAT Container App so Hangfire and SignalR behave predictably during actual test sessions, and accept that this uses much more of the monthly free grant than an idle app would (rough estimate: 1 always-on 0.5 vCPU / 1 GiB replica ≈ 1.3M vCPU-seconds + 2.6M GiB-seconds per month, **exceeding** the free grant — meaning **this specific choice will incur a small real cost** unless the Container App is deliberately started only for scheduled UAT windows and scaled to zero the rest of the time via `az containerapp update --min-replicas 0` before/after each session). This is stated plainly per §30 "no fake success" — I am not going to claim this configuration is $0 when kept always-on; it is $0 only if used in bounded sessions.

---

## 3. Phase 3 — Proposed UAT Architecture

```text
GitHub (saberahmadi48-collab/PEMS-cmms-edit-empoleeys-master)
  |
  | workflow_dispatch (manual only) -> .github/workflows/deploy-uat.yml
  v
GitHub Actions runner
  | dotnet restore/build/test (must pass - no continue-on-error)
  | az login (service principal secret)
  | az containerapp up  --source .  (builds the EXISTING Dockerfile)
  v
Azure Container Apps  (pems-uat, Consumption plan, scale 0-1 or pinned to 1 during test windows)
  | ASPNETCORE_ENVIRONMENT=UAT  ->  loads appsettings.UAT.json (no secrets) + env-var overrides
  | ConnectionStrings__PEMSConnection  <-  Container App secret (never in Git)
  v
Azure SQL Database — "PEMSDB-UAT"  (free offer: 100,000 vCore-s/mo, 32GB)
  |  completely separate database/server from the developer's local PEMSDB
  |  migrated explicitly via: dotnet ef database update --connection <UAT string>
  v
9 real UAT user accounts, created through the EXISTING Admin > Users UI,
assigned the EXISTING 9 SystemRoles (no new roles invented)
```

### 3.1 Isolation guarantee (§2, §8)
The UAT database is a **separate Azure SQL Database resource** with its own server/connection string. Nothing in this plan connects the public UAT app to `localhost\SQLEXPRESS` or the developer's local `PEMSDB` — those remain fully local and are never reachable from the Internet-facing container in any scenario.

### 3.2 UAT User Matrix (using the repository's real, existing roles — §11)

| Username (example) | Real `SystemRoles` value | Purpose |
|---|---|---|
| uat.admin | `SystemAdmin` | Administration, user/role management, Hangfire dashboard |
| uat.eng.manager | `EngineeringManager` | Engineering approval workflows |
| uat.eng.supervisor | `EngineeringSupervisor` | Engineering review/routing |
| uat.mech.engineer | `DisciplineEngineer` (Discipline = Mechanical) | Mechanical discipline work |
| uat.elec.engineer | `DisciplineEngineer` (Discipline = Electrical) | Electrical discipline work |
| uat.inst.engineer | `DisciplineEngineer` (Discipline = Instrumentation) | Instrumentation discipline work |
| uat.maint.engineer | `MaintenanceEngineer` | CMMS: Work Request/Work Order/PM |
| uat.doc.controller | `DocumentController` | Document Register, Letters/Correspondence |
| uat.viewer | `Viewer` | Read-only cross-module verification |

Note: the brief's example list names "Mechanical/Electrical/Instrumentation Engineer" as if they were three separate roles — the repository does not have three separate roles for this; it has one `DisciplineEngineer` role differentiated by the user's `DisciplineId`. Per §11's own instruction ("if the repository already contains equivalent roles, reuse them"), three separate `DisciplineEngineer` accounts with different assigned disciplines are used instead of inventing `MechanicalEngineer`/`ElectricalEngineer`/`InstrumentationEngineer` roles that don't exist in this codebase. `ContractManager`/`QAManager` exist in the repo too and can be added to the matrix if procurement/QA workflows are in UAT scope — not included above only to match the brief's example count.

### 3.3 Configuration that must change per-environment (no secrets, just environment-specific values)
- `Fido2:ServerDomain` / `Fido2:Origins` → the real `https://pems-uat.<region>.azurecontainerapps.io` domain, once known (only knowable after first deploy — chicken-and-egg, handled by deploying once, reading the assigned domain, then updating this one setting).
- `ASPNETCORE_ENVIRONMENT=UAT` (loads `appsettings.UAT.json`, created this phase — see §4).

---

## 4. Phase 4 — Configuration Hardening Applied This Session (code changes made, all justified by this task)

| File | Change | Why |
|---|---|---|
| `PEMS.Web/Program.cs` | Added a minimal inline middleware that copies `X-Forwarded-Proto` into `context.Request.Scheme` | Every candidate host terminates HTTPS at a reverse proxy and forwards plain HTTP internally; without this, `UseHttpsRedirection`/`UseHsts`/secure-cookie logic would misdetect the scheme (§14). Implemented by hand rather than via the standard `Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions` API because that type failed to compile in this project with a pre-existing, unrelated dependency-graph conflict (documented in project memory) — the hand-rolled version achieves the identical practical effect with zero new dependencies. |
| `PEMS.Web/Program.cs` | Added `GET /health` returning plain `"Healthy"`/`"Unhealthy"` (503) based on `SELECT 1` against `PEMSDbContext`, publicly reachable (bypasses the global MVC `AuthorizeFilter` since it's a Minimal API endpoint, not a controller) | §20 — needed by any host's load balancer/uptime check; deliberately returns no connection strings, versions, or internal details (§20's own "do not expose sensitive diagnostics"). |
| `PEMS.Web/appsettings.UAT.json` (new) | Environment-specific config with **no secrets** — empty connection string (must come from env var), Ollama pointed at a deliberately-unreachable placeholder (fails fast and clearly instead of silently pretending AI works — §18), FTP left empty, Fido2 domain left as an explicit `REPLACE-WITH-ACTUAL-UAT-DOMAIN` placeholder | §4, §14 — a per-environment settings file that is safe to commit because it contains no secrets. |
| `.github/workflows/deploy-uat.yml` (new) | A manual-only (`workflow_dispatch`, requires typing `UAT` to confirm) GitHub Actions workflow: restore → build → **test (no `continue-on-error`, unlike the existing generic `build.yml`)** → `az login` → `az containerapp up` using the existing Dockerfile → set the DB connection as a Container App secret | §27/§28 — reproducible, explicitly distinguishable from any production/main auto-deploy (there is none), and does not run on every push. **This workflow will not function until the four GitHub repo secrets it documents in its own header comment are added by the repository owner** — no attempt was made to add them, since doing so requires Azure credentials this session does not have. |

**Build/test verification of these four changes:** `dotnet build PEMS.Web/PEMS.Web.csproj` → succeeded, 0 errors. Full regression suite re-run after these changes: **1151/1151 passed, 0 failed, 0 skipped** (5m24s) — identical to the pre-change baseline, confirming no regression. This run includes `PEMS.Tests/Security/FileSecurityHttpTests.cs`, which uses `WebApplicationFactory<Program>` to boot the entire real HTTP pipeline (including the new forwarded-headers middleware and `/health` endpoint) in-process — this is the closest thing to a live smoke test achievable without a real deployed host, and it passed cleanly.

---

## 5. STOP CONDITION (per the brief's own §31)

This session has **no Azure account, no Azure CLI (`az`) installed, no `gh` CLI authentication, and no credentials of any kind** for any cloud provider. Concretely blocked, per §31's own list:

- *"credentials are missing"* — true. No Azure subscription, no service principal, no SQL Server admin credentials exist yet.
- *"deployment access is unavailable"* — true. `gh auth status` confirms this session is not authenticated to GitHub at all (cannot push, cannot set repo secrets, cannot trigger the workflow above).
- Phase 10 ("multi-user UAT... use at least 5 different UAT accounts... perform concurrent activity") requires real human colleagues at keyboards — this is not something achievable from an automated coding session under any circumstances, regardless of hosting access.

**I have not deployed anything, connected to any live UAT database, created any cloud resource, or tested with multiple users — and I am not reporting any of those as done, per §30.** Everything in §1-4 above is genuinely complete and verifiable in this repository right now; everything from Phase 5 onward requires you.
