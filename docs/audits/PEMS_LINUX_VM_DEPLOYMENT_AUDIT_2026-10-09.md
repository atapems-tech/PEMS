# PEMS — Linux VM deployment: audit findings (2026-10-09)

> **STATUS (2026-10-09, after the remote audit): this systemd-based procedure does NOT apply to the VM at 192.168.1.136.**
> That VM already runs PEMS as a Docker Compose stack (pems-web + pems-sqlserver + Caddy). Do not install `pems.service`/`pems-release` there.
> See `docs/audits/PEMS_LINUX_VM_REMOTE_PREFLIGHT_2026-10-09.md`; `deploy/` will be retargeted at the existing Compose stack after you approve that strategy.


Evidence-based; "verified" = observed on this machine or in the repository today. **Nothing on the Ubuntu VM was verified: it is unreachable from this machine.**

## Environment
* Dev machine: Windows, VS Code; network `10.54.22.85/24` (Wi-Fi). Branch `feat/cmms-passport-global-page-header`, HEAD `13ee6b6`, working tree dirty (unrelated feature work).
* VM (historical): `192.168.1.109`, Ubuntu 24.04. **From this machine: no ping, TCP 22 closed/unreachable, no ARP entry** (different subnet/VPN, or VM off). `~/.ssh` has no keys or config; `known_hosts` holds the VM host key from an earlier session.
* No WSL distribution and no Docker locally → Linux behaviour of the installer could not be exercised end-to-end; verification below is limited to what runs on Windows/Git-Bash.

## Existing deployment architecture (found)
| Item | Finding |
|---|---|
| `Dockerfile`, `docker-compose.yml` | Multi-stage build, Compose bundles SQL Server + Ollama + app. Used by the Azure **UAT** workflow (`.github/workflows/deploy-uat.yml`, manual only). **Defect fixed:** the restore layer omitted `PEMS.WorkflowPlatform.csproj` (referenced by PEMS.Web) → `dotnet restore` fails before sources are copied. Not built (no Docker here); covered by a regression test. |
| Azure runbook `docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md` | Azure Container Apps + Azure SQL; unrelated to the VM. |
| systemd unit / scripts for the VM | **None in the repository** (`/etc/systemd/system/pems.service` exists only on the VM, unverified). |
| Strategy chosen | **systemd + framework-dependent `dotnet publish` of PEMS.Web** (linux-x64 by default). Reasons: the VM already uses a systemd unit; the database is an external SQL Server (Compose's bundled SQL Server would be a *second, empty* database — explicitly out of scope); no duplicate service/proxy is introduced; rollback = symlink switch. Docker stays as is for UAT. |

## Runtime facts from the code
* Startup runs idempotent **seeders** but **no migrations** (`Migrate()` is never called) → schema must be migrated separately (matches the safety rule).
* `filestore` and `logs/` are relative to the **working directory**; `wwwroot/uploads` lives inside the publish tree → per-release symlinks to `/var/lib/pems` are mandatory or each release would lose user files.
* `/health` executes `SELECT 1` (200 / 503) → used as the deployment gate and DB-connectivity gate.
* Forwarded headers: a hand-written middleware trusts `X-Forwarded-Proto` from any caller (no known-proxy check) → Kestrel must listen on loopback only. Cookies are `Secure` outside Development.
* Hangfire (SQL Server storage), SignalR, Serilog file sink (`logs/pems-.log`), QuestPDF 2025.7 (native `libQuestPdfSkia.so` ships in the linux-x64 publish), Redis optional (empty → in-process cache), Ollama via HTTP (`Ollama:Url`).
* `appsettings.json` carries `Server=localhost\SQLEXPRESS;…;Trusted_Connection=True` — a **Windows-local** value; production must override `ConnectionStrings__PEMSConnection` (SQL authentication). Not a secret, but wrong for Linux.
* `ProjectIntelligenceController` reads `../graphify-out` (developer feature) — absent on the server; that admin page degrades.

## Findings in the raw `dotnet publish` output (why a filtered package is required)
1. **17 user-uploaded files (24 MB) from the developer's `wwwroot/uploads` were swept into the publish** → excluded (user data; the server keeps its own).
2. `x64/`, `x86/` contain only **Windows PE** OCR DLLs (verified by header) → excluded for Linux targets; Linux OCR needs system libraries.
3. `appsettings.Development.json`, `appsettings.UAT.json`, 8 `*.pdb` → excluded.
4. No `tessdata` anywhere in the repo → OCR needs the OS package + `OCR__TessDataPath`.
5. Required items verified present: `PEMS.Web.dll`, `*.deps.json`, `*.runtimeconfig.json`, `wwwroot/css|js|lib`, satellite `fa/PEMS.Web.resources.dll` (Persian UI), `LatoFont/`, native `.so` files.
Result: 434 files / 121 MB / 43 MB compressed; manifest with per-file SHA-256; package verification fails closed on forbidden files or secret-like values.

## Risks
| Risk | Severity | Status |
|---|---|---|
| VM unreachable from this machine; no SSH keys configured | blocker for steps E–H | open — see "Minimum actions" |
| SQL Server location/auth for Linux unknown (`localhost\SQLEXPRESS` is Windows-integrated) | high | open — needs TCP 1433 + SQL login on the real DB host |
| OCR on Linux (Tesseract 5.2.0 wrapper native library names) | medium (OCR/AI ingestion only) | unverified, documented |
| DataProtection keys: Windows-protected credentials in the dev DB are not decryptable on Linux | medium | documented (re-enter provider credentials once) |
| `X-Forwarded-Proto` trusted from any caller | low if Kestrel is loopback-only | documented; enforced by env template |
| Task Scheduler / Windows services, Windows auth, Windows paths | none found | verified by code search |
| Hangfire AI/OCR job concurrency (see architecture audit) | medium | unchanged |

## What was and was not verified
* **Verified:** Release publish for linux-x64; package contents/exclusions/manifest; checksum + manifest verification logic (accepts the real archive, rejects a tampered file, fails closed on an unparseable manifest); LF endings; shell syntax (`bash -n`); preflight fail-closed behaviour against the unreachable VM; 8 automated checks (`LinuxDeploymentArtifactsTests`).
* **Not verified (blocked):** SSH, transfer, remote install, service start/restart/reboot, health on the VM, SQL connectivity from Linux, reverse proxy/HTTPS, rollback on the VM.

## Minimum actions required from you
1. Put this machine on a network that can reach the VM (same `192.168.1.x` LAN or VPN) — or give the VM's current reachable address — and confirm `Test-NetConnection <vm> -Port 22`.
2. Create an ed25519 SSH key, authorize it (bootstrap does this for a `pemsdeploy` user), connect once manually to store the host key.
3. Tell me where SQL Server actually runs and provide the SQL login via `/etc/pems/pems.env` on the VM (never in chat).
