# PEMS Linux VM — Docker Compose remediation report (2026-10-09, TEST environment)

Follows `PEMS_LINUX_VM_REMOTE_PREFLIGHT_2026-10-09.md` (all of its FAIL/BLOCKED gates are re-evaluated below).
Target: `arsin@192.168.1.136` / `pems-server`, designated TEST VM. Evidence = commands run in this session; nothing below is inferred.

## 1. Outcome

**The application on the VM is now deployed from this repository's build, healthy, and updatable by a repeatable, tested kit.** Verified live: health 200 directly and through Caddy, OCR extraction, DataProtection decryption of the stored credential, persistence across container recreation, rollback and forward redeploy, 103/103 migrations applied.
**Routine test deployments are ready** (`.\deploy\deploy.ps1`). **Not yet done** (administrator rights needed): disable the obsolete `pems.service`, fix `pems.home.arpa` name resolution, install Caddy's CA on clients, reboot test (see §6).

## 2. Gates

| Gate | Before | After | Evidence |
|---|---|---|---|
| Topology / kit mismatch | BLOCKED | **PASS** | kit retargeted to the existing Compose project; systemd artifacts deleted; Azure UAT files untouched (test `AzureUat_ConfigurationIsUntouchedByTheLinuxKit`) |
| Recovery point (DB) | FAIL (36 MB stale backup) | **PASS** | `PEMSDB_pre_deploy_20261009T123135Z.bak` (36,454,400 B, sha256 `cb84166c…`) and `PEMSDB_pre_deploy_20261009T125746Z.bak` (36,450,304 B, sha256 `e6d54561…`), both `CHECKSUM` + `VERIFYONLY`; both restored into scratch DB `PEMSDB_RestoreCheck`: `DBCC CHECKDB` clean, 9 key tables identical row counts; scratch DB dropped; old `PEMSDB_before_0fc34ab.bak` untouched |
| Recovery point (keys) | FAIL | **PASS** | key `4ba2ca45-…` (998 B) and key `5cf51e3f-…` (1001 B, found only in stopped `pems-web-old`) copied to `/opt/pems/deploy-state/20261009T123216Z/` (dir 700, files 600), SHA-256 identical to originals, contents never printed; container config snapshot saved there (mode 600) |
| DataProtection persistence | FAIL | **PASS** | volume `pems-dpkeys` mounted at `/root/.aspnet/DataProtection-Keys`; ring = 2 keys before/after recreation |
| Stored credential decryptable | **unknown → was FAIL** | **PASS** | before: the live container could NOT decrypt `StorageProviders.EncryptedPassword` (key `5cf51e3f` missing); after: `DP_DECRYPT=OK` (11 chars, value never shown) with keys from the volume, also after recreation. Negative controls: empty key ring, only live key, wrong app name → all FAIL |
| `filestore` persistence | FAIL | **PASS** | volume `pems-filestore`; was 0 files (re-verified before mount); marker survived recreation |
| Uploads persistence | PASS | **PASS** | `pems-uploads`, 16 files before and after |
| Linux OCR | FAIL | **PASS** | root causes: wrapper loads `libleptonica-1.82.0.so`/`libtesseract50.so` from `<app>/x64` and imports `libdl`; image has `liblept.so.5`, `libtesseract.so.5`, `libdl.so.2`; tessdata path unset. Fixed in `Dockerfile.release`; probe through the real wrapper: `PEMS OCR TEST 12345`, confidence 0.91 (`eng`, `fas+eng`); wrong path → fails |
| Offline image build | BLOCKED | **PASS** | VM has no internet; `docker build --pull=false` from local base `pems-web:0fc34ab` (ID verified); 127 MB context built in ~12 s |
| Migrations (9 pending) | BLOCKED | **PASS** | reviewed (§4); 103/103 applied; CK_* constraints enabled+trusted; new indexes present; data counts unchanged |
| Controlled update + health | – | **PASS** | release `20261009132022-13ee6b603-dirty`; health 200 (direct + Caddy); `docker ps` healthy |
| Rollback | – | **PASS** | executed for real: `pems-web` back on `pems-web:0fc34ab`, health 200 on the migrated schema; then forward redeploy OK |
| Reboot policy | – | **PARTIAL** | docker + containerd enabled; all 3 containers `restart=unless-stopped`; actual reboot **not performed** (needs root) |
| Obsolete `pems.service` | – | **BLOCKED (sudo)** | identity confirmed (source-tree build, user `arsin`, Windows connection string, 161 restarts); not stopped/removed |
| `pems.home.arpa` | FAIL | **BLOCKED (admin)** | VM `/etc/hosts` still maps `.109`; Windows hosts not writable (not elevated); route works with `curl --resolve` (200); Caddy CA not trusted on this client |

## 3. Container / image versions

| | before | after |
|---|---|---|
| `pems-web` | `pems-web:0fc34ab` (`sha256:c1c22ddc…`, source commit `0fc34ab`, hand-started, 2 volumes) | `pems-web:rel-20261009132022-13ee6b603-dirty` (`sha256:6cd6eb17…`, this repo at HEAD `13ee6b6` + working tree, Compose-managed, 4 volumes) |
| rollback target | – | `pems-web:0fc34ab` image + stopped container `pems-web-pre-20261009132022-…`; `pems-web-old` (`pems-web:test`) untouched |
| `pems-sqlserver`, `pems-caddy` | unchanged | unchanged (not recreated) |
| migrations | 94 | **103** |

## 4. Remote changes performed (complete list)

1. Created `/opt/pems/deploy` (kit), `/opt/pems/deploy-state` (700), `/opt/pems/incoming`; copied the kit and `PemsLinuxProbe` there. Temporary `/tmp` test dirs removed.
2. SQL: two `COPY_ONLY` backups; two scratch restores (dropped); the **nine pending migrations** (see below). No other data change.
3. Docker: created volumes `pems-filestore`, `pems-dpkeys` (seeded with the two keys); built image `pems-web:rel-20261009132022-…`; stopped + renamed the hand-started `pems-web` to `pems-web-pre-…`; started `pems-web` from `deploy/docker/compose.pems-web.yml` (project `pems-cmms-edit-empoleeys-master`); one recreation + one rollback + one forward redeploy; marker files created and removed.
4. Created `/opt/pems/deploy-state/pems-web.env` (mode 600) from the running container's environment.
Nothing else: no image/volume/container removed, `docker-compose.yml`, `Caddyfile`, SQL container, `pems-web-old`, `pems.service`, `/etc/hosts` untouched.

**Migration review (all nine):** destructive = none (no DROP TABLE/COLUMN). 2 `DROP INDEX` replaced by new indexes on empty tables; 4 data `UPDATE`s on empty tables (`MaterialReservations`, `EmployeeAssignments` = 0 rows); new unique indexes on empty tables; new columns nullable/defaulted (largest: `WorkflowInstanceStages` 748 rows, `AspNetRoles` 19). First attempts failed safely and were rolled back (§5 of the runbook): `Invalid column name` because EF emits raw SQL in the same batch as the `ADD COLUMN`; resolved by wrapping 10 raw statements in `EXEC(N'…')` (`Convert-EfScript.ps1`, output byte-identical to the reviewed script).

## 5. Tests

| Check | Result |
|---|---|
| `deploy/tests/test-pems-docker.sh` (fake docker/curl/df) | **55 passed, 0 failed** on Windows (Git Bash) **and on the VM** (Linux, real `stat`/permissions) |
| `PEMS.Tests` `Deployment` (12 tests, incl. one that runs the harness above) | **12 passed, 0 failed** |
| Live: preflight (16 checks), smoke, runtime proofs (OCR + DataProtection), persistence, rollback, redeploy | all PASS (§2) |
| Earlier today: Part Catalogue 116/116; full suite 2,407 passed + 1 known failure (`MrsImportBatchCapacityTests`, timing) | recorded in the previous reports; the full suite was **not** re-run after the deployment-kit changes (no application code changed) |
Defects found by testing and fixed: GNU-`tar` vs Windows tar when launched from Git Bash; `scp` wildcard; `sqlc` helper querying `master`; rollback not updating `current-release.env`; manifest-parser CRLF (earlier); destructive-verb guard matching script text; Dockerfile restore layer missing `PEMS.WorkflowPlatform`.

## 6. Remaining blockers / risks (all need an administrator or an owner decision)

1. **Admin steps A–E** in `docs/deployment/LINUX_VM_DEPLOYMENT.md` §6 (disable `pems.service`; VM `/etc/hosts`; DHCP reservation + DNS record; trust Caddy CA; reboot test). Until C/C′: reach the app by `https://192.168.1.136` (Host mismatch) or `curl --resolve`; plain `http://192.168.1.136:8080` also works on the LAN (published port).
2. **Application credentials were not available**, so authenticated business workflows (Part Catalogue request lifecycle etc.) were **not** exercised on the VM; verified instead: seeded data (4,161 technical classes, 23 sections, published workflow, roles 19→21), anonymous routes, logs without errors. Recommended next: log in with a real account (or run the earlier `TEST-PCAT` provisioner against the VM database after approval) and walk the Part Catalogue flow.
3. The app connects with the `sa` login (compose-inherited); replace with a least-privilege login (separate task). The repository `docker-compose.yml` still carries a default SA password literal for dev use.
4. The release image is `base + app` (969 MB) and keeps `pems-web:0fc34ab` as an immutable base — **do not delete or retag it**. The key `5cf51e3f` also still lives in `pems-web-old`; keep that container until the off-VM key backup exists.
5. Package version carries `-dirty` (uncommitted working tree). Nothing was committed or pushed.
6. The VM address is DHCP; the Windows target file (`deploy/targets.local.json`, git-ignored) must follow it.

## 7. Rollback procedure (verified)

`.\deploy\deploy.ps1 -Action Rollback` — recreates `pems-web` from the recorded previous image (`PREV_TAG`/`PREV_ID` in `/opt/pems/deploy-state/previous-release.env`), checks health, updates `current-release.env`. The database is not reverted (restore from the verified backups if needed).
Last-resort fallback (first release only): `docker stop pems-web && docker rename pems-web pems-web-failed && docker rename pems-web-pre-20261009132022-13ee6b603-dirty pems-web && docker start pems-web` (the controller does this automatically if the compose rollback itself fails).

## 8. Is the environment ready for routine test deployments?

**Yes for application releases** — preflight, offline build, controlled recreate, health/Caddy checks, automatic and manual rollback, backup + guarded migration, runtime proofs are implemented, mock-tested, and demonstrated on the VM.
**Conditionally for users** — name resolution/TLS trust (admin steps) and a reboot test remain; functional UAT with real accounts remains.
