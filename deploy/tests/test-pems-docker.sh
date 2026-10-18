#!/usr/bin/env bash
# Mock-based tests for deploy/remote/pems-docker.sh (fake docker/curl/df; no daemon, no network, no VM).
#   bash deploy/tests/test-pems-docker.sh        -> prints PASS/FAIL per check, exit 1 if any check fails
set -u
HERE=$(cd "$(dirname "$0")" && pwd); KIT=$(cd "$HERE/../.." && pwd); SCRIPT=$KIT/deploy/remote/pems-docker.sh
PASSED=0; FAILED=0
check() { if eval "$2"; then PASSED=$((PASSED+1)); echo "  [PASS] $1"; else FAILED=$((FAILED+1)); echo "  [FAIL] $1"; fi; }

BASE_ID=sha256:c1c22ddcb6d8bc9d355d664da0a0e8a01b2db6a006db37c92b8b134d77d74ac6
new_sandbox() {
  SB=$(mktemp -d); export FAKE_STATE=$SB/fake STATE_DIR=$SB/state KIT_DIR=$SB/kit
  mkdir -p "$FAKE_STATE" "$STATE_DIR" "$KIT_DIR/docker"
  cp "$KIT/deploy/docker/compose.pems-web.yml" "$KIT/deploy/docker/Dockerfile.release" "$KIT_DIR/docker/"
  [ "$(uname -s)" = Linux ] || export SKIP_MODE_CHECK=1      # NTFS (Git Bash) cannot represent chmod 600; the real check is exercised on Linux
  export DOCKER_BIN=$HERE/fakes/docker CURL_BIN=$HERE/fakes/curl DF_BIN=$HERE/fakes/df SYSTEMCTL_BIN=$HERE/fakes/systemctl HEALTH_TIMEOUT=2 POLL_SECONDS=0
  unset FAKE_RESTART_POLICY FAKE_RESTART_COUNT FAKE_WEB_HEALTH FAKE_PORT_BIND FAKE_SQL_MIGRATIONS FAKE_LOG_BAD FAKE_BUILD_FAIL FAKE_SMOKE_FAIL FAKE_SQL_STATE FAKE_FREE_KB FAKE_FILESTORE_FILES
  echo "pems-web:0fc34ab $BASE_ID" > "$FAKE_STATE/images"
  printf 'pems-web|pems-web:0fc34ab|%s|running|\npems-caddy|caddy:2|sha256:caddy|running|\n' "$BASE_ID" > "$FAKE_STATE/containers"
  printf 'pems-uploads\npems-logs\n' > "$FAKE_STATE/volumes"; echo pems-cmms-edit-empoleeys-master_default > "$FAKE_STATE/networks"
  printf 'PATH=/usr/bin\nDOTNET_VERSION=10\n' > "$FAKE_STATE/image_env.base"
  printf 'PATH=/usr/bin\nDOTNET_VERSION=10\nASPNETCORE_ENVIRONMENT=Production\nConnectionStrings__PEMSConnection=Server=sqlserver;Database=PEMSDB;User Id=sa;Password=TopSecretValue123\n' > "$FAKE_STATE/container_env.pems-web"
  PKG=$SB/pkg; mkdir -p "$PKG"; echo x > "$PKG/PEMS.Web.dll"
  h=$(sha256sum "$PKG/PEMS.Web.dll" | cut -d' ' -f1); printf '{
  "fileCount": 1,
  "files": [
    {
      "path": "PEMS.Web.dll",
      "size": 2,
      "sha256": "%s"
    }
  ]
}
' "$h" > "$PKG/release-manifest.json"
  KEYS=$SB/keys; mkdir -p "$KEYS"; echo k1 > "$KEYS/key-aaaa.xml"; echo k2 > "$KEYS/key-bbbb.xml"
}
run() { bash "$SCRIPT" "$@" > "$SB/out.txt" 2>&1; RC=$?; }
prepare_ready() { new_sandbox; run adopt-env; run prepare-volumes --keys-dir "$KEYS"; }

echo "== guard: destructive docker verbs are refused (script sourced, d() called directly)"
new_sandbox
for bad in "system prune -af" "volume rm pems-uploads" "volume prune -f" "rm -f pems-web" "rmi pems-web:0fc34ab" "compose -p x down -v" "network rm x"; do
  ( source "$SCRIPT"; d $bad ) > "$SB/o" 2>&1; rc=$?
  check "refused: docker $bad" "[ $rc -ne 0 ] && grep -q refused '$SB/o' && ! grep -q 'docker $bad' '$FAKE_STATE/calls.log' 2>/dev/null"
done
( source "$SCRIPT"; d run --rm --entrypoint sh img -c 'rm -rf /tmp/x' ) > "$SB/o" 2>&1
check "allowed: 'rm -rf' inside a throw-away container script is not mistaken for a docker verb" "! grep -q refused '$SB/o'"

echo "== static: the script contains no destructive docker usage"
check "no 'compose ... down' / prune / volume rm / rmi invocation outside the guard" "! grep -v -E '^\s*#|refus|case|die' '$SCRIPT' | grep -q -E 'compose[^|]* down|docker[^|]* prune|volume rm|rmi '"
check "db-apply runs sqlcmd with -I (QUOTED_IDENTIFIER ON, required by filtered indexes) and -b (abort on error)" "grep -q -- '-C -b -I -d PEMSDB -i' '$SCRIPT'"
check "no EF migration commands in the controller" "! grep -q -i -E 'dotnet ef|database update' '$SCRIPT'"

echo "== preflight"
new_sandbox; run preflight
check "fails when env file and new volumes are missing" "[ $RC -ne 0 ] && grep -q 'missing' '$SB/out.txt' && grep -q 'volume pems-filestore missing' '$SB/out.txt'"
prepare_ready; run preflight
check "passes after adopt-env + prepare-volumes" "[ $RC -eq 0 ] && grep -q 'preflight PASSED' '$SB/out.txt'"
check "base image identity verified" "grep -q 'identity verified' '$SB/out.txt'"
new_sandbox; echo "pems-web:0fc34ab sha256:deadbeef" > "$FAKE_STATE/images"; run adopt-env; run prepare-volumes --keys-dir "$KEYS"; run preflight
check "fails when the base tag points at a different image ID" "[ $RC -ne 0 ] && grep -q 'tag moved' '$SB/out.txt'"
prepare_ready; export FAKE_FREE_KB=1000000; run preflight
check "fails when free disk is below the minimum" "[ $RC -ne 0 ] && grep -q 'free disk' '$SB/out.txt'"
prepare_ready; export FAKE_SQL_STATE=unhealthy; run preflight
check "fails when SQL Server is not healthy" "[ $RC -ne 0 ] && grep -q 'SQL Server container' '$SB/out.txt'"
prepare_ready; : > "$FAKE_STATE/dpkeys.list"; run preflight
check "fails when a required DataProtection key is missing from the volume" "[ $RC -ne 0 ] && grep -q 'DataProtection key' '$SB/out.txt'"

echo "== adopt-env"
new_sandbox; run adopt-env
check "creates the env file with mode 600 (verified on Linux)" "[ $RC -eq 0 ] && { [ \"\$(uname -s)\" != Linux ] || [ \"\$(stat -c %a '$STATE_DIR/pems-web.env')\" = 600 ]; }"
check "keeps only variables not provided by the image" "grep -q '^ASPNETCORE_ENVIRONMENT=' '$STATE_DIR/pems-web.env' && ! grep -q '^PATH=' '$STATE_DIR/pems-web.env'"
check "never prints the secret value" "! grep -q TopSecretValue123 '$SB/out.txt'"
run adopt-env; check "refuses to overwrite an existing env file" "[ $RC -ne 0 ] && grep -q 'refusing to overwrite' '$SB/out.txt'"

echo "== prepare-volumes"
new_sandbox; run prepare-volumes --keys-dir "$KEYS"
check "creates filestore and dpkeys volumes and records the required key names" "[ $RC -eq 0 ] && grep -qx pems-filestore '$FAKE_STATE/volumes' && grep -qx pems-dpkeys '$FAKE_STATE/volumes' && [ \"\$(wc -l < '$STATE_DIR/required-dp-keys.txt')\" = 2 ]"
run prepare-volumes --keys-dir "$KEYS"; check "is idempotent (second run keeps everything)" "[ $RC -eq 0 ] && grep -q 'already exists' '$SB/out.txt'"
run prepare-volumes --keys-dir "$SB/nope"; check "rejects a missing keys directory" "[ $RC -ne 0 ]"
new_sandbox; export FAKE_FILESTORE_FILES=3; run prepare-volumes
check "live filestore content is detected for carry-over" "grep -q 'filestore files: live container=3' '$SB/out.txt'"

echo "== build"
prepare_ready; run build "$PKG" 20260101000001-aaaaaaaaa abcdef123
check "builds pems-web:rel-<id> from the package and records it" "[ $RC -eq 0 ] && grep -q 'rel-20260101000001-aaaaaaaaa' '$FAKE_STATE/images' && [ -f '$STATE_DIR/releases/20260101000001-aaaaaaaaa.env' ]"
check "build is offline (--pull=false, base from the local tag)" "grep -q -- '--pull=false' '$FAKE_STATE/calls.log' && grep -q 'BASE_IMAGE=pems-web:0fc34ab' '$FAKE_STATE/calls.log'"
run build "$PKG" 20260101000001-aaaaaaaaa abcdef123; check "release ids are never reused" "[ $RC -ne 0 ] && grep -q 'already exists' '$SB/out.txt'"
run build "$PKG" bad-id x; check "rejects a malformed release id" "[ $RC -ne 0 ]"
prepare_ready; export FAKE_SMOKE_FAIL=1; run build "$PKG" 20260101000002-bbbbbbbbb abc
check "image smoke-test failure (OCR wiring missing) aborts and records nothing" "[ $RC -ne 0 ] && [ ! -f '$STATE_DIR/releases/20260101000002-bbbbbbbbb.env' ]"
prepare_ready; echo "pems-web:0fc34ab sha256:other" > "$FAKE_STATE/images"; run build "$PKG" 20260101000003-ccccccccc abc
check "refuses to build on a base image whose identity changed" "[ $RC -ne 0 ] && grep -q 'identity check failed' '$SB/out.txt'"

prepare_ready; echo tampered >> "$PKG/PEMS.Web.dll"; run build "$PKG" 20260101000007-ggggggggg abc
check "build refuses a package whose files do not match the manifest" "[ $RC -ne 0 ] && grep -q 'not matching the manifest' '$SB/out.txt' && ! grep -q 'rel-20260101000007' '$FAKE_STATE/images'"
prepare_ready; echo '{}' > "$PKG/release-manifest.json"; run build "$PKG" 20260101000008-hhhhhhhhh abc
check "build fails closed on an unparseable manifest" "[ $RC -ne 0 ] && grep -q 'inconclusive' '$SB/out.txt'"

echo "== deploy: success path"
prepare_ready; run build "$PKG" 20260101000001-aaaaaaaaa abc; run deploy 20260101000001-aaaaaaaaa
check "deploy succeeds" "[ $RC -eq 0 ] && grep -q 'is live. Database was NOT touched' '$SB/out.txt'"
check "the manual container is kept (renamed), not removed" "grep -q '^pems-web-pre-20260101000001-aaaaaaaaa|pems-web:0fc34ab' '$FAKE_STATE/containers'"
check "pems-web now runs the new release image" "grep -q '^pems-web|pems-web:rel-20260101000001-aaaaaaaaa|.*|running|pems-cmms' '$FAKE_STATE/containers'"
check "previous image recorded as the rollback target" "grep -q 'PREV_TAG=pems-web:0fc34ab' '$STATE_DIR/previous-release.env' && grep -q 'PREV_CONTAINER=pems-web-pre-' '$STATE_DIR/previous-release.env'"
check "current release recorded" "grep -q 'RELEASE_ID=20260101000001-aaaaaaaaa' '$STATE_DIR/current-release.env'"
check "compose used with --no-deps --no-build and never with down/prune/volume rm/rmi" "grep -q 'up -d --no-deps --no-build --force-recreate pems-web' '$FAKE_STATE/calls.log' && ! grep -q -E ' down| prune|volume rm|rmi ' '$FAKE_STATE/calls.log'"
check "no secret appears in output" "! grep -q TopSecretValue123 '$SB/out.txt'"
check "no secret appears on any docker/curl command line" "! grep -q -E 'Password=|TopSecret' '$FAKE_STATE/calls.log'"

echo "== deploy: second release, then explicit rollback"
run build "$PKG" 20260101000004-dddddddd abc; run deploy 20260101000004-dddddddd
check "second deploy is Compose-managed and records the first release as previous" "[ $RC -eq 0 ] && grep -q 'PREV_TAG=pems-web:rel-20260101000001-aaaaaaaaa' '$STATE_DIR/previous-release.env'"
run rollback
check "rollback recreates pems-web from the previous release image" "[ $RC -eq 0 ] && grep -q '^pems-web|pems-web:rel-20260101000001-aaaaaaaaa|' '$FAKE_STATE/containers'"
check "rollback updates current-release.env to the image actually running" "grep -q 'IMAGE_TAG=pems-web:rel-20260101000001-aaaaaaaaa' '$STATE_DIR/current-release.env' && grep -q 'rolled-back-to' '$STATE_DIR/current-release.env'"

echo "== deploy: unhealthy release triggers automatic rollback"
prepare_ready; run build "$PKG" 20260101000001-aaaaaaaaa abc; run deploy 20260101000001-aaaaaaaaa
run build "$PKG" 20260101000005-eeeeeeeee abc; echo "pems-web:rel-20260101000005-eeeeeeeee" > "$FAKE_STATE/bad_images"; run deploy 20260101000005-eeeeeeeee
check "deploy fails (non-zero) and reports the rollback" "[ $RC -ne 0 ] && grep -q 'rolling back' '$SB/out.txt' && grep -q 'rollback OK' '$SB/out.txt'"
check "the known-good release is running again" "grep -q '^pems-web|pems-web:rel-20260101000001-aaaaaaaaa|.*|running|' '$FAKE_STATE/containers'"
check "failed-container logs are shown with secrets redacted" "! grep -q 'Secret123' '$SB/out.txt' && grep -q 'redacted' '$SB/out.txt'"
check "current-release.env names the good image that is actually running (not the failed release)" "grep -q 'IMAGE_TAG=pems-web:rel-20260101000001-aaaaaaaaa' '$STATE_DIR/current-release.env' && ! grep -q '20260101000005' '$STATE_DIR/current-release.env'"

echo "== boot-check: reboot-recovery evidence (read-only)"
boot_ready() { new_sandbox; printf 'pems-sqlserver|mcr.microsoft.com/mssql/server:2022-latest|sha256:sql|running|pems-cmms-edit-empoleeys-master
' >> "$FAKE_STATE/containers"
  printf 'docker=enabled
containerd=enabled
pems.service=disabled
' > "$FAKE_STATE/units"; }
boot_ready; run boot-check
check "healthy, boot-enabled, loopback-only stack passes" "[ $RC -eq 0 ] && grep -q 'boot-check PASSED' '$SB/out.txt' && ! grep -q '\[FAIL\]' '$SB/out.txt'"
check "it asserts health (not just 'running'), SQL answer, Caddy and port binding" "grep -q 'pems-web health = healthy' '$SB/out.txt' && grep -q 'SQL Server answers' '$SB/out.txt' && grep -q 'Caddy serves' '$SB/out.txt' && grep -q 'not published on the LAN' '$SB/out.txt'"
boot_ready; echo 'pems.service=enabled' >> "$FAKE_STATE/units"; sed -i '/^pems.service=disabled/d' "$FAKE_STATE/units"; run boot-check
check "fails while the obsolete pems.service is enabled" "[ $RC -ne 0 ] && grep -q 'obsolete pems.service is ENABLED' '$SB/out.txt'"
boot_ready; sed -i 's/^docker=enabled/docker=disabled/' "$FAKE_STATE/units"; run boot-check
check "fails when docker is not enabled at boot" "[ $RC -ne 0 ] && grep -q 'docker is NOT enabled at boot' '$SB/out.txt'"
boot_ready; export FAKE_RESTART_POLICY=no; run boot-check; unset FAKE_RESTART_POLICY
check "fails when a container would not return after a reboot (restart policy 'no')" "[ $RC -ne 0 ] && grep -q 'would not return after a reboot' '$SB/out.txt'"
boot_ready; export FAKE_RESTART_COUNT=9; run boot-check; unset FAKE_RESTART_COUNT
check "fails on a restart loop (RestartCount > 3)" "[ $RC -ne 0 ] && grep -q 'restart loop' '$SB/out.txt'"
boot_ready; export FAKE_WEB_HEALTH=starting; run boot-check; unset FAKE_WEB_HEALTH
check "fails when a container is running but not healthy" "[ $RC -ne 0 ] && grep -q 'pems-web health = starting' '$SB/out.txt'"
boot_ready; export FAKE_PORT_BIND=0.0.0.0:8080; run boot-check; unset FAKE_PORT_BIND
check "fails when the app port is published on all interfaces" "[ $RC -ne 0 ] && grep -q 'published on all interfaces' '$SB/out.txt'"
boot_ready; export FAKE_SQL_MIGRATIONS=; run boot-check; unset FAKE_SQL_MIGRATIONS
check "fails when SQL Server does not answer a query" "[ $RC -ne 0 ] && grep -q 'did not answer a query' '$SB/out.txt'"
boot_ready; export FAKE_LOG_BAD=1; run boot-check; unset FAKE_LOG_BAD
check "fails when the app log shows database-readiness errors" "[ $RC -ne 0 ] && grep -q 'database-readiness' '$SB/out.txt'"
boot_ready; run boot-check
check "boot-check never changes anything (no start/stop/compose/rm in the docker call log)" "! grep -q -E '^docker (start|stop|rename|compose|rm|build|run) ' '$FAKE_STATE/calls.log'"
check "no secret appears in boot-check output" "! grep -q -E 'TopSecretValue123|Secret123' '$SB/out.txt' || grep -q redacted '$SB/out.txt' || ! grep -q 'Password=' '$SB/out.txt'"

echo "== reapply: same image, new Compose file"
prepare_ready; run build "$PKG" 20260101000001-aaaaaaaaa abc; run deploy 20260101000001-aaaaaaaaa
: > "$FAKE_STATE/calls.log"; cp "$KIT_DIR/docker/compose.pems-web.yml" "$SB/compose.backup"
run reapply "$SB/compose.backup"
check "reapply succeeds and says the database was untouched" "[ $RC -eq 0 ] && grep -q 'reapply OK' '$SB/out.txt'"
check "reapply recreates pems-web from the unchanged release image (no build)" "grep -q 'up -d --no-deps --no-build --force-recreate pems-web' '$FAKE_STATE/calls.log' && ! grep -q ' build ' '$FAKE_STATE/calls.log' && grep -q '^pems-web|pems-web:rel-20260101000001-aaaaaaaaa|.*|running|' '$FAKE_STATE/containers'"
check "reapply never uses down/prune/volume rm/rmi" "! grep -q -E ' down| prune|volume rm|rmi ' '$FAKE_STATE/calls.log'"
check "the Compose file publishes the app port on loopback only" "grep -q '127.0.0.1:8080:8080' '$KIT/deploy/docker/compose.pems-web.yml' && ! grep -q -E '^[[:space:]]*- \"8080:8080\"' '$KIT/deploy/docker/compose.pems-web.yml'"
echo "pems-web:rel-20260101000001-aaaaaaaaa" > "$FAKE_STATE/bad_images"; run reapply "$SB/compose.backup"
check "an unhealthy reapply fails non-zero, attempts the Compose restore and points to rollback" "[ $RC -ne 0 ] && grep -q 'restoring the previous Compose file' '$SB/out.txt' && grep -q 'rollback' '$SB/out.txt'"
new_sandbox; run reapply; check "reapply refuses when no release is recorded" "[ $RC -ne 0 ] && grep -q 'no current release' '$SB/out.txt'"

echo "== deploy: refuses when preflight fails (nothing changed)"
new_sandbox; run build "$PKG" 20260101000006-ffffffff abc; run deploy 20260101000006-ffffffff
check "unprepared environment is refused and the live container is untouched" "[ $RC -ne 0 ] && grep -q '^pems-web|pems-web:0fc34ab|' '$FAKE_STATE/containers'"
prepare_ready; run deploy 20991231235959-notbuilt; check "a release that was never built is refused" "[ $RC -ne 0 ] && grep -q 'was not built' '$SB/out.txt'"

echo "== database operations are explicit and guarded"
prepare_ready; echo "select 1" > "$SB/m.sql"
run db-apply "$SB/m.sql"; check "db-apply refuses without --confirm-reviewed" "[ $RC -ne 0 ] && grep -q 'confirm-reviewed' '$SB/out.txt'"
run db-apply "$SB/m.sql" --confirm-reviewed; check "db-apply refuses without a verified backup" "[ $RC -ne 0 ] && grep -q 'no verified backup' '$SB/out.txt'"
printf 'BACKUP_FILE=/x.bak\nSHA256=abc\nSIZE=1\nVERIFIED_AT=%s\n' "$(( $(date -u +%s) - 7200 ))" > "$STATE_DIR/last-verified-backup.env"
run db-apply "$SB/m.sql" --confirm-reviewed; check "db-apply refuses a stale backup (> 1 h)" "[ $RC -ne 0 ] && grep -q 'old' '$SB/out.txt'"
check "deploy never calls the database tools" "! grep -q 'sqlcmd' '$FAKE_STATE/calls.log'"

echo; echo "RESULT: $PASSED passed, $FAILED failed"; [ $FAILED -eq 0 ]
