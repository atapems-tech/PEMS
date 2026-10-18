# PEMS Linux VM — remote preflight and safety audit (2026-10-09)

> **UPDATE (same day): superseded by `PEMS_LINUX_VM_REMEDIATION_REPORT_2026-10-09.md`** — the gates below were re-evaluated after the Docker-Compose remediation (most are now PASS; remaining items are administrator-only).

Target: `arsin@192.168.1.136` (hostname `pems-server`), key-based SSH, strict host-key verification.
**Read-only audit.** No deployment, restart, stop, migration, database write, file change or service change was performed on the VM. No secret was printed (passwords/connection strings were never displayed; SQL was queried through the container's own environment variable without echoing it).

## Final decision: **NOT READY** for deployment

Reasons, in order of importance: (1) the VM's live deployment is **Docker Compose**, not systemd — the deployment kit built so far targets the wrong topology; (2) `filestore` and the ASP.NET DataProtection key ring are **not on persistent volumes**; (3) **9 pending migrations** and **no current database backup**; (4) the VM has **no outbound internet**, so images/packages cannot be pulled; (5) Linux **OCR is not functional**; (6) `pems-release`/env file/sudo rule are not installed and `arsin` needs a sudo password.

## Gate results

| # | Gate | Result | Evidence (redacted) |
|---|---|---|---|
| 1 | Local preflight `deploy.ps1 -Action Preflight` | **FAIL** (expected) | PASS: local tools, network, SSH key + strict host key, arch, .NET, disk. FAIL: `pems-release` not installed, `/etc/pems/pems.env` absent, no passwordless sudo rule. (A false PASS on the sudo check was found and fixed in `deploy.ps1`.) |
| 2 | SSH key auth + host identity | **PASS** | stored ED25519 fingerprint `SHA256:WTg8BSVp…` equals the one presented; key login works |
| 3 | VM identity / runtime | **PASS** | Ubuntu 24.04.5 LTS, kernel 7.0.0-38, x86_64, 4 CPU, 10 GB RAM (7.6 GB free), `/` 58 GB with 27 GB free (53 %), inodes 8 %; native ASP.NET Core 10.0.12 runtime + SDK 10.0.112 (match the net10.0 publish) |
| 4 | Deployment topology | **BLOCKED – decision needed** | Live stack = Compose project `pems-cmms-edit-empoleeys-master` (source `/opt/pems/source/...`, commit `0fc34ab` of 2026-10-06) with `pems-web` (8080), `pems-sqlserver` (1433), plus a separate `pems-caddy` (80/443). `pems.service` (systemd, user `arsin`, runs from the source tree's `bin/Release`) is **enabled but crash-looping** (39 restarts, core-dump) because it connects with the Windows-style `appsettings.json` connection string. Two systemd/Docker instances of the same app must not coexist. |
| 5 | SQL Server readiness | **PASS (reachable) / data preservation: see 6** | SQL Server 2022 Developer (16.0.4295) in container `pems-sqlserver`, healthy; mixed-mode auth enabled; only login `sa`. App endpoint = Docker network `sqlserver:1433`, database `PEMSDB`, SQL authentication as `sa` (least-privilege concern). `pems-web` `/health` = 200 → the app reaches the DB. `localhost\SQLEXPRESS` (what the systemd unit uses) is not valid on this VM. DB size 720 MB. |
| 6 | Existing data to preserve | **PASS (identified)** | `PEMSDB`: 94 migrations applied; rows: EngineeringDocuments 9,191; DocumentAttachments 4; Employees 2; AspNetUsers 3; AspNetRoles 19; Departments 13; WorkOrders 1; StorageProviders 1 (has an encrypted password). Only backup found: `PEMSDB_before_0fc34ab.bak` (36 MB, 2026-10-07) — **stale; a fresh backup is mandatory before any migration**. |
| 7 | Migrations state | **BLOCKED – action required** | VM history (94) is an exact prefix of this repo (103): **no drift**. Pending (9): Notification_DismissedDate, RoleCatalogMetadata, PartCatalogue_Schema, EnterpriseActorResolution, StoreRequisitionLineTraceability, StoreRequisitionTraceabilityIdempotency, RevalidateStoreRequisitionConstraints, OrganizationPositionHierarchy, OrganizationAssignmentEmployeeIndex. Up() ops: 5 purely additive; 4 contain raw `Sql()` and/or `DropIndex` (StoreRequisition ×3, OrganizationPositionHierarchy). The tables they touch are **empty on this VM** (StoreRequisitions/Lines 0, Positions 0, EmployeeAssignments 0, Notifications 0) → low data risk. Earlier manual scripts exist in `/opt/pems/deploy-backup/` (the previous release was migrated by hand). The new code **requires** these migrations; the app does not migrate itself. |
| 8 | Persistent storage – uploads | **PASS** | `wwwroot/uploads` = volume `pems-uploads` (24 MB, 16 files) |
| 9 | Persistent storage – logs | **PASS** | `/app/logs` = volume `pems-logs` (311 MB, 3 files) |
| 10 | Persistent storage – `filestore` | **FAIL** | `/app/filestore` is in the container's **writable layer** (0 files today). Recreating the container (any redeploy) would lose every engineering/part-catalogue file stored there from now on. |
| 11 | DataProtection keys | **FAIL** | key ring (1 key) is in `/root/.aspnet/DataProtection-Keys` inside the container layer, not a volume. Recreating the container loses it → the encrypted `StorageProviders` password becomes undecryptable and auth cookies/antiforgery tokens are invalidated. |
| 12 | Health endpoint | **PASS** | `GET /health` → 200 directly (`127.0.0.1:8080`) and through Caddy. It runs `SELECT 1`, so it doubles as a DB-connectivity gate. It is unauthenticated and returns only Healthy/Unhealthy. |
| 13 | Reverse proxy / DNS for `pems.home.arpa` | **FAIL (stale)** | Caddy (`/opt/pems/caddy/Caddyfile`: `pems.home.arpa { tls internal; reverse_proxy pems-web:8080 }`) already serves it. But the VM's `/etc/hosts` maps `pems.home.arpa` → **192.168.1.109** (the old VM address), the VM's DNS servers include `192.168.1.109`, and the name does **not resolve** from the Windows PC. Clients must reach `192.168.1.136`; Caddy's internal CA must be trusted by browsers. |
| 14 | OCR / Tesseract on Linux | **FAIL** | Container has CLI `tesseract 5.3.4` + `tessdata` (`eng`, `fas`, `osd`) under `/usr/share/tesseract-ocr/*/tessdata` and `libtesseract.so.5`/`liblept.so.5`. The .NET wrapper (`Tesseract 5.2.0`, NuGet ships Windows DLLs only) loads `tesseract50`/`leptonica-1.82.0` → not present on Linux; and `OCR__TessDataPath` is not set (default `/app/tessdata` is absent). OCR/AI-ingestion jobs cannot run; the rest of the app is unaffected. |
| 15 | Outbound internet from the VM | **FAIL** | DNS lookups and HTTPS probes to mcr.microsoft.com, Docker Hub, Ubuntu and NuGet all fail. No `aspnet` runtime image is stored locally (only `dotnet/sdk:10.0`, `pems-web:*`, `caddy:2`, `mssql/server:2022-latest`). New images must be built **offline from an existing local image**, or loaded as a tar. |
| 16 | Privileges | **BLOCKED** | `arsin` is in `sudo` (password required) and `docker` (root-equivalent). `bootstrap-vm.sh`/`pems-release` need root → must be run by you interactively. |
| 17 | Ports to preserve | **INFO** | Listening: 22, 80, 443 (Caddy), 8080 (pems-web), 1433 (SQL), DNS/CUPS on loopback. 5089, 7065, 5000 are free. |
| 18 | Resources | **PASS** | pems-web 481 MB, sqlserver 990 MB, caddy 61 MB of 7.75 GB; CPU idle |

## Existing services that must be preserved
`pems-web` (8080), `pems-sqlserver` (1433, volume `pems-cmms-edit-empoleeys-master_pems-sql-data`, 823 MB data), `pems-caddy` (80/443, volumes `pems-caddy-data/config`), volumes `pems-uploads`, `pems-logs`. The stopped `pems-web-old` image/container is the previous rollback candidate.

## Findings about the kit built so far
* `deploy/` implements **systemd + published files** — correct for a bare host, **not** for this VM, which already runs the app in Docker. Using both would create the competing architecture the project rules forbid. The systemd path must not be applied to this VM; the next iteration should retarget `deploy/` at the existing Compose stack (below). Package building (exclusions, manifest, checksums), preflight and test assets remain reusable.
* The legacy `pems.service` should be **disabled** after approval (it only produces core-dump restarts and competes for CPU/log noise).

## Remediation (NOT performed — each needs your approval)
1. **Decide the strategy** (recommended: *reuse the existing Docker Compose stack*): ship the verified package, build the release image **on the VM, offline, `FROM pems-web:0fc34ab`** (or the SDK image) copying the new publish output over `/app`, tag it `pems-web:<release>`, keep `pems-web:0fc34ab` as rollback, switch with `docker compose up -d pems-web`. Update `deploy/` accordingly (Dockerfile.release, compose override, rollback by tag).
2. **Persistence before any recreate:** add volumes for `/app/filestore` and the DataProtection key ring (e.g. `/root/.aspnet/DataProtection-Keys`, or set an explicit key path), and **copy the current key out of the running container into the new volume first** so the stored provider password stays decryptable.
3. **Fresh full backup** of `PEMSDB` (verified restore), then generate the idempotent script (`deploy.ps1 -Action MigrationScript`), review the 4 raw-SQL/index migrations, apply in a maintenance window.
4. **Set `OCR__TessDataPath=/usr/share/tesseract-ocr/5/tessdata`** and provide `libtesseract50.so` / `leptonica-1.82.0` names (symlinks to the system libraries are not guaranteed ABI-compatible with Tesseract 5.3.4 — needs a test) — or accept OCR disabled on Linux.
5. **DNS:** update `/etc/hosts`/DNS (`pems.home.arpa` → `192.168.1.136`) on the VM and clients; install Caddy's internal CA on client browsers.
6. Disable the crash-looping `pems.service` (`sudo systemctl disable --now pems.service`) — after you confirm Docker is the intended runtime.
7. Replace the `sa` login in the connection string with a least-privilege SQL login (separate task).

## Actions already performed (this task)
Local only: ran preflight; fixed the false-PASS sudo check in `deploy/deploy.ps1`; wrote this report. Remote: read-only commands (`systemctl`, `ss`, `ps`, `docker ps/inspect/diff/logs/images`, `df`, `ls`, `stat`, `cat` of Caddyfile and the redacted compose file, `curl` to `/health`, SQL `SELECT`s of counts/migration ids through the container). **No deployment, migration, database modification, service restart or destructive operation was performed.**

## Exact next safe action
Choose the strategy (recommended: Docker, step 1 above). Then, as the first *write* step with your approval: take a verified backup of `PEMSDB` and copy the current DataProtection key to a new persistent volume — nothing else changes until those two protections exist.
