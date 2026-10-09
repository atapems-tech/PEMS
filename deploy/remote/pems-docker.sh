#!/usr/bin/env bash
# pems-docker.sh - VM-side controller for the PEMS Docker deployment (runs as a user in the `docker` group; no root needed).
#
#   preflight                           read-only readiness checks (docker, compose, base image identity, env file, volumes, SQL, disk, health)
#   adopt-env                           ONE-TIME: create the VM-local env file (mode 600) from the running pems-web container
#   prepare-volumes [--keys-dir DIR]    create the persistent filestore / DataProtection-key volumes; seed keys from DIR (never overwrites)
#   build <pkgdir> <releaseId> <commit> build the release image OFFLINE from the extracted package (no registry access)
#   deploy <releaseId>                  preflight -> keep current as rollback target -> recreate pems-web -> health -> auto-rollback on failure
#   rollback                            recreate pems-web from the recorded previous image (database is NOT reverted)
#   status | smoke | verify-runtime      state / health+Caddy checks / OCR + DataProtection-decrypt proof inside the release image
#   export-cipher                       save the stored provider credential ciphertext (mode 600) for verify-runtime (never printed)
#   db-backup | db-apply <file> --confirm-reviewed   explicit database operations; deployment NEVER migrates by itself
#
# Guarantees (enforced by d() and covered by the kit's tests): never `compose down`, never `-v`, never prune, never remove volumes/images,
# never print secrets, never run migrations during deploy.
set -euo pipefail

DOCKER=${DOCKER_BIN:-docker}
CURL=${CURL_BIN:-curl}
DF=${DF_BIN:-df}
SYSTEMCTL=${SYSTEMCTL_BIN:-systemctl}
KIT_DIR=${KIT_DIR:-/opt/pems/deploy}
STATE_DIR=${STATE_DIR:-/opt/pems/deploy-state}
# optional non-secret overrides written once per VM
[ -f "$KIT_DIR/kit.conf" ] && . "$KIT_DIR/kit.conf"

PROJECT=${PROJECT:-pems-cmms-edit-empoleeys-master}
WEB=${WEB_CONTAINER:-pems-web}
SQL=${SQL_CONTAINER:-pems-sqlserver}
CADDY=${CADDY_CONTAINER:-pems-caddy}
NETWORK=${NETWORK_NAME:-pems-cmms-edit-empoleeys-master_default}
BASE_IMAGE=${BASE_IMAGE:-pems-web:0fc34ab}
BASE_IMAGE_ID=${BASE_IMAGE_ID:-sha256:c1c22ddcb6d8bc9d355d664da0a0e8a01b2db6a006db37c92b8b134d77d74ac6}
HEALTH_URL=${HEALTH_URL:-http://127.0.0.1:8080/health}
CADDY_HOST=${CADDY_HOST:-pems.home.arpa}
HEALTH_TIMEOUT=${HEALTH_TIMEOUT:-150}
POLL_SECONDS=${POLL_SECONDS:-3}
MIN_FREE_GB=${MIN_FREE_GB:-5}
COMPOSE_FILE=${COMPOSE_FILE:-$KIT_DIR/docker/compose.pems-web.yml}
DOCKERFILE=${DOCKERFILE:-$KIT_DIR/docker/Dockerfile.release}
ENV_FILE=${PEMS_WEB_ENV_FILE:-$STATE_DIR/pems-web.env}
REQUIRED_KEYS_FILE=$STATE_DIR/required-dp-keys.txt          # one key file name per line (kept by prepare-volumes)
CUR=$STATE_DIR/current-release.env
PREV=$STATE_DIR/previous-release.env
PROBE_DIR=${PROBE_DIR:-$STATE_DIR/probe}
CIPHER_FILE=$STATE_DIR/verify/provider.cipher
VOLUMES="pems-uploads pems-logs pems-filestore pems-dpkeys"

log()  { echo "[pems-docker] $*"; }
die()  { echo "[pems-docker] ERROR: $*" >&2; exit 1; }
pass() { echo "  [PASS] $*"; }
fail() { echo "  [FAIL] $*"; FAILED=1; }

# the ONLY way this script talks to docker: destructive verbs are refused outright (only the docker verb words are inspected,
# never the text of a `sh -c` script that runs inside a throw-away container)
d() {
  local a1=${1:-} a2=${2:-}
  case "$a1" in
    system|rm|rmi|kill) die "refused destructive docker command: docker $a1";;
    container|image|volume|network) case "$a2" in rm|remove|prune|kill) die "refused destructive docker command: docker $a1 $a2";; esac;;
    compose) for w in "$@"; do case "$w" in down|rm|kill|stop) die "refused destructive compose verb: $w";; esac; done;;
  esac
  "$DOCKER" "$@"
}

image_id()      { d image inspect --format '{{.Id}}' "$1" 2>/dev/null; }
container_state() { d container inspect --format '{{.State.Status}}' "$1" 2>/dev/null || echo absent; }
http_code()     { "$CURL" -s -o /dev/null -m 6 -w '%{http_code}' "$@" 2>/dev/null || echo 000; }

wait_healthy() {   # $1 = label ; returns 0 when /health = 200 and the container is running
  local deadline=$((SECONDS + HEALTH_TIMEOUT)) code=000 st
  while [ $SECONDS -lt $deadline ]; do
    st=$(container_state "$WEB")
    case "$st" in exited|dead|restarting) log "container state is '$st' while waiting ($1)"; return 1;; esac
    code=$(http_code "$HEALTH_URL")
    if [ "$code" = "200" ]; then log "health OK ($HEALTH_URL -> 200) [$1]"; return 0; fi
    sleep "$POLL_SECONDS"
  done
  log "health check FAILED after ${HEALTH_TIMEOUT}s (last code $code) [$1]"; return 1
}

caddy_ok() {   # route through the existing Caddy proxy (internal TLS) without relying on DNS
  [ "$(container_state "$CADDY")" = "running" ] || { log "caddy container not running - route check skipped"; return 0; }
  local code; code=$(http_code -k --resolve "$CADDY_HOST:443:127.0.0.1" "https://$CADDY_HOST/health")
  [ "$code" = "200" ] && { log "Caddy route OK (https://$CADDY_HOST/health -> 200)"; return 0; }
  log "Caddy route FAILED (https://$CADDY_HOST/health -> $code)"; return 1
}

cmd_preflight() {
  FAILED=0; echo "== preflight (read-only)"
  d info --format '{{.ServerVersion}}' >/dev/null 2>&1 && pass "docker daemon reachable" || { fail "docker daemon not reachable"; return 1; }
  v=$(d compose version --short 2>/dev/null) && pass "docker compose $v" || fail "docker compose plugin missing"
  id=$(image_id "$BASE_IMAGE" || true)
  if [ -z "$id" ]; then fail "base image $BASE_IMAGE not present locally (offline build impossible)"
  elif [ "$id" != "$BASE_IMAGE_ID" ]; then fail "base image $BASE_IMAGE has ID $id, expected $BASE_IMAGE_ID (tag moved - refuse to build on an unknown base)"
  else pass "base image $BASE_IMAGE identity verified ($id)"; fi
  [ -f "$COMPOSE_FILE" ] && pass "compose file $COMPOSE_FILE" || fail "compose file missing: $COMPOSE_FILE"
  [ -f "$DOCKERFILE" ] && pass "release Dockerfile present" || fail "Dockerfile.release missing: $DOCKERFILE"
  if [ -f "$ENV_FILE" ]; then
    mode=$(stat -c %a "$ENV_FILE"); if [ "$mode" = "600" ] || [ "${SKIP_MODE_CHECK:-0}" = "1" ]; then pass "env file mode 600"; else fail "env file mode is $mode (must be 600)"; fi   # SKIP_MODE_CHECK is for tests on non-POSIX filesystems only
    grep -q '^ConnectionStrings__PEMSConnection=' "$ENV_FILE" && pass "env file defines the connection string (value not shown)" || fail "env file lacks ConnectionStrings__PEMSConnection"
  else fail "env file $ENV_FILE missing - run: pems-docker.sh adopt-env"; fi
  for vol in $VOLUMES; do d volume inspect "$vol" >/dev/null 2>&1 && pass "volume $vol exists" || fail "volume $vol missing (run prepare-volumes / existing data volume lost?)"; done
  d network inspect "$NETWORK" >/dev/null 2>&1 && pass "network $NETWORK" || fail "network $NETWORK missing"
  sqlh=$(d container inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$SQL" 2>/dev/null || echo absent)
  case "$sqlh" in healthy|running) pass "SQL Server container $SQL is $sqlh";; *) fail "SQL Server container $SQL is '$sqlh'";; esac
  root=$(d info --format '{{.DockerRootDir}}' 2>/dev/null || echo /var/lib/docker); free_kb=$("$DF" -Pk "$root" 2>/dev/null | awk 'NR==2{print $4}')
  free_gb=$(( ${free_kb:-0} / 1024 / 1024 )); [ "$free_gb" -ge "$MIN_FREE_GB" ] && pass "free disk ${free_gb} GB on $root (min ${MIN_FREE_GB})" || fail "free disk ${free_gb} GB < ${MIN_FREE_GB} GB"
  if [ -f "$REQUIRED_KEYS_FILE" ] && d volume inspect pems-dpkeys >/dev/null 2>&1; then
    present=$(d run --rm --network none -v pems-dpkeys:/k:ro --entrypoint sh "$BASE_IMAGE" -c 'ls /k' 2>/dev/null || true)
    missing=0; while IFS= read -r k; do [ -z "$k" ] && continue; echo "$present" | grep -qx "$k" || { fail "DataProtection key $k is missing from volume pems-dpkeys"; missing=1; }; done < "$REQUIRED_KEYS_FILE"
    [ $missing -eq 0 ] && pass "all required DataProtection keys present in pems-dpkeys ($(wc -l < "$REQUIRED_KEYS_FILE") listed)"
  else fail "required DataProtection key list not recorded yet (prepare-volumes --keys-dir)"; fi
  code=$(http_code "$HEALTH_URL"); [ "$code" = "200" ] && pass "current application healthy ($HEALTH_URL -> 200)" || echo "  [WARN] current application not healthy ($code)"
  [ "$FAILED" -eq 0 ] && { echo "preflight PASSED"; return 0; } || { echo "preflight FAILED"; return 1; }
}

cmd_adopt_env() {
  [ ! -e "$ENV_FILE" ] || die "$ENV_FILE already exists (refusing to overwrite)"
  [ "$(container_state "$WEB")" != "absent" ] || die "no $WEB container to read the environment from"
  umask 077; mkdir -p "$STATE_DIR"; local img tmp; tmp=$(mktemp)
  img=$(d container inspect --format '{{.Image}}' "$WEB")
  d container inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$WEB" | sort -u > "$tmp.c"
  d image inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$img" | sort -u > "$tmp.i"
  comm -23 "$tmp.c" "$tmp.i" | grep -v '^$' > "$tmp.d" || true
  : > "$ENV_FILE"
  while IFS= read -r line; do
    k=${line%%=*}; v=${line#*=}
    case "$v" in *"'"*) rm -f "$tmp" "$tmp.c" "$tmp.i" "$tmp.d" "$ENV_FILE"; die "value of $k contains a single quote - cannot be written safely";; esac
    printf "%s='%s'\n" "$k" "$v" >> "$ENV_FILE"
  done < "$tmp.d"
  chmod 600 "$ENV_FILE"; rm -f "$tmp" "$tmp.c" "$tmp.i" "$tmp.d"
  log "created $ENV_FILE (mode 600) with variables: $(cut -d= -f1 "$ENV_FILE" | tr '\n' ' ')- values not shown"
}

cmd_prepare_volumes() {
  local keys_dir=""; while [ $# -gt 0 ]; do case "$1" in --keys-dir) keys_dir=$2; shift;; *) die "unknown option $1";; esac; shift; done
  for vol in pems-filestore pems-dpkeys; do
    if d volume inspect "$vol" >/dev/null 2>&1; then log "volume $vol already exists (kept)"; else d volume create "$vol" >/dev/null; log "created volume $vol"; fi
  done
  if [ -n "$keys_dir" ]; then
    [ -d "$keys_dir" ] || die "keys dir not found: $keys_dir"
    ls "$keys_dir"/*.xml >/dev/null 2>&1 || die "no key files (*.xml) in $keys_dir"
    d run --rm --network none -v "$keys_dir":/src:ro -v pems-dpkeys:/dst --entrypoint sh "$BASE_IMAGE" -c '
      set -eu
      for f in /src/*.xml; do b=$(basename "$f")
        if [ -e "/dst/$b" ]; then cmp -s "$f" "/dst/$b" && echo "kept   $b (identical)" || { echo "CONFLICT $b differs from the volume copy - refusing to overwrite" >&2; exit 3; }
        else cp -p "$f" "/dst/$b"; chmod 600 "/dst/$b"; echo "seeded $b"; fi
      done'
    umask 077; ( cd "$keys_dir" && ls -1 *.xml ) > "$REQUIRED_KEYS_FILE"; chmod 600 "$REQUIRED_KEYS_FILE"
    log "recorded $(wc -l < "$REQUIRED_KEYS_FILE") required key file name(s) in $REQUIRED_KEYS_FILE"
  fi
  # filestore: carry over existing content (currently none) before the container is ever recreated
  if [ "$(container_state "$WEB")" != "absent" ]; then
    src=$(d run --rm --network none --volumes-from "$WEB:ro" --entrypoint sh "$BASE_IMAGE" -c 'find /app/filestore -type f 2>/dev/null | wc -l' 2>/dev/null | tr -d ' ' || echo 0)
    dst=$(d run --rm --network none -v pems-filestore:/dst:ro --entrypoint sh "$BASE_IMAGE" -c 'find /dst -type f | wc -l' | tr -d ' ')
    log "filestore files: live container=$src, persistent volume=$dst"
    if [ "${src:-0}" -gt 0 ]; then
      [ "${dst:-0}" -eq 0 ] || die "filestore volume already has files and the live container has $src - resolve manually"
      d run --rm --network none --volumes-from "$WEB:ro" -v pems-filestore:/dst --entrypoint sh "$BASE_IMAGE" -c 'cp -a /app/filestore/. /dst/'
      dst=$(d run --rm --network none -v pems-filestore:/dst:ro --entrypoint sh "$BASE_IMAGE" -c 'find /dst -type f | wc -l' | tr -d ' ')
      [ "$dst" = "$src" ] || die "filestore copy verification failed (src=$src dst=$dst)"
      log "copied and verified $dst filestore file(s)"
    fi
  fi
}

verify_manifest() {   # every file of the extracted package must match release-manifest.json (fails closed on an unparseable manifest)
  local dir=$1 flat want bad=0 checked=0 line rel_path h
  flat=$(tr -d '\r\n' < "$dir/release-manifest.json")
  want=$(echo "$flat" | grep -o '"fileCount": *[0-9]*' | grep -o '[0-9]*$' || true)
  while IFS= read -r line; do
    rel_path=$(echo "$line" | sed -E 's/.*"path": *"([^"]+)".*/\1/'); h=$(echo "$line" | sed -E 's/.*"sha256": *"([0-9a-f]+)".*/\1/')
    [ -f "$dir/$rel_path" ] || { bad=$((bad+1)); continue; }
    checked=$((checked+1))
    [ "$(sha256sum "$dir/$rel_path" | cut -d' ' -f1)" = "$h" ] || bad=$((bad+1))
  done < <(echo "$flat" | grep -o '"path": *"[^"]*", *"size": *[0-9]*, *"sha256": *"[0-9a-f]*"')
  [ "$checked" -gt 0 ] && [ "$checked" = "${want:-0}" ] || die "manifest check inconclusive (verified $checked of ${want:-?} files)"
  [ "$bad" -eq 0 ] || die "$bad file(s) missing or not matching the manifest"
  log "manifest verified ($checked files)"
}

cmd_build() {
  local pkg=${1:-} rel=${2:-} commit=${3:-unknown}
  [ -d "$pkg" ] && [ -f "$pkg/PEMS.Web.dll" ] && [ -f "$pkg/release-manifest.json" ] || die "package dir invalid: $pkg"
  [[ "$rel" =~ ^[0-9]{14}-[A-Za-z0-9-]+$ ]] || die "invalid release id: $rel"
  verify_manifest "$pkg"
  id=$(image_id "$BASE_IMAGE" || true); [ "$id" = "$BASE_IMAGE_ID" ] || die "base image identity check failed ($BASE_IMAGE -> ${id:-absent})"
  tag="pems-web:rel-$rel"; [ -z "$(image_id "$tag" || true)" ] || die "image $tag already exists (release ids are never reused)"
  log "building $tag offline from $BASE_IMAGE ($BASE_IMAGE_ID)"
  d build --pull=false -f "$DOCKERFILE" --build-arg "BASE_IMAGE=$BASE_IMAGE" --build-arg "RELEASE_ID=$rel" --build-arg "RELEASE_COMMIT=$commit" -t "$tag" "$pkg"
  new=$(image_id "$tag") || die "build produced no image"
  # structural smoke test of the image itself (no network): the app, the OCR links and tessdata must be present
  d run --rm --network none --entrypoint sh "$tag" -c 'test -f /app/PEMS.Web.dll && test -e /app/x64/libtesseract50.so && test -e /app/x64/libleptonica-1.82.0.so && test -e /usr/lib/x86_64-linux-gnu/libdl.so && ls /usr/share/tesseract-ocr/5/tessdata/eng.traineddata >/dev/null' \
    || die "image smoke test failed (application or OCR wiring missing) - $tag was NOT deployed"
  mkdir -p "$STATE_DIR/releases"; printf 'IMAGE_TAG=%s\nIMAGE_ID=%s\nRELEASE_ID=%s\nCOMMIT=%s\nBUILT_AT=%s\nBASE_IMAGE_ID=%s\n' "$tag" "$new" "$rel" "$commit" "$(date -u +%FT%TZ)" "$BASE_IMAGE_ID" > "$STATE_DIR/releases/$rel.env"
  log "built $tag = $new"
}

compose_up() {   # $1 = image reference
  ( export PEMS_WEB_IMAGE="$1" PEMS_WEB_ENV_FILE="$ENV_FILE"; d compose -p "$PROJECT" -f "$COMPOSE_FILE" up -d --no-deps --no-build --force-recreate pems-web )
}

do_rollback() {
  [ -f "$PREV" ] || { log "no previous release recorded - cannot roll back"; return 1; }
  # shellcheck disable=SC1090
  . "$PREV"
  local ref="$PREV_TAG"; cur_id=$(image_id "$PREV_TAG" || true)
  [ "$cur_id" = "$PREV_ID" ] || { log "tag $PREV_TAG no longer maps to $PREV_ID - using the image ID"; ref="$PREV_ID"; }
  log "rolling back application to $ref"
  if compose_up "$ref" && wait_healthy "rollback"; then
    umask 077; printf 'RELEASE_ID=rolled-back-to:%s
IMAGE_TAG=%s
IMAGE_ID=%s
DEPLOYED_AT=%s
' "$PREV_TAG" "$PREV_TAG" "$PREV_ID" "$(date -u +%FT%TZ)" > "$CUR"
    log "rollback OK (database was NOT reverted); current-release.env now records $PREV_TAG"; return 0
  fi
  if [ -n "${PREV_CONTAINER:-}" ] && [ "$(container_state "$PREV_CONTAINER")" != "absent" ]; then
    log "compose rollback unhealthy - restoring the original container $PREV_CONTAINER"
    d stop "$WEB" >/dev/null 2>&1 || true; d rename "$WEB" "$WEB-failed-$(date -u +%Y%m%dT%H%M%SZ)" 2>/dev/null || true
    d rename "$PREV_CONTAINER" "$WEB"; d start "$WEB" >/dev/null
    wait_healthy "original-container" && { log "original container restored"; return 0; }
  fi
  log "ROLLBACK FAILED - manual intervention required"; return 1
}

cmd_deploy() {
  local rel=${1:-}; [ -f "$STATE_DIR/releases/$rel.env" ] || die "release $rel was not built (run build first)"
  . "$STATE_DIR/releases/$rel.env"
  cmd_preflight || die "preflight failed - nothing was changed"
  local was_state img_tag img_id managed
  was_state=$(container_state "$WEB")
  if [ "$was_state" != "absent" ]; then
    img_tag=$(d container inspect --format '{{.Config.Image}}' "$WEB"); img_id=$(d container inspect --format '{{.Image}}' "$WEB")
    managed=$(d container inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$WEB" 2>/dev/null || true)
    umask 077; { echo "PREV_TAG=$img_tag"; echo "PREV_ID=$img_id"; echo "PREV_MANAGED=${managed:-manual}"; echo "PREV_RELEASE_AT=$(date -u +%FT%TZ)"; } > "$PREV"
    if [ -z "$managed" ]; then
      pre="$WEB-pre-$rel"; log "current $WEB is not Compose-managed: stopping and keeping it as $pre (rollback fallback)"
      d stop "$WEB" >/dev/null; d rename "$WEB" "$pre"; echo "PREV_CONTAINER=$pre" >> "$PREV"
    fi
  else log "no existing $WEB container (first start)"; rm -f "$PREV"; fi
  log "starting release $rel image $IMAGE_TAG"
  if compose_up "$IMAGE_TAG" && wait_healthy "new release" && caddy_ok; then
    umask 077; printf 'RELEASE_ID=%s\nIMAGE_TAG=%s\nIMAGE_ID=%s\nDEPLOYED_AT=%s\n' "$rel" "$IMAGE_TAG" "$IMAGE_ID" "$(date -u +%FT%TZ)" > "$CUR"
    log "release $rel is live. Database was NOT touched. Previous: ${img_tag:-none} (${img_id:-})"; return 0
  fi
  log "new release failed verification - rolling back"
  d logs --tail 25 "$WEB" 2>&1 | sed -E 's/(Password|Pwd)=[^;" ]*/\1=<redacted>/Ig; s/^/[container] /' || true
  do_rollback && die "release $rel FAILED and was rolled back" || die "release $rel FAILED and rollback FAILED"
}

# Recreate pems-web from the SAME release image with the CURRENT Compose file (port binding, healthcheck, restart policy ...). No build,
# no image change, no database change. $1 = optional backup copy of the previous Compose file, restored if the new configuration is unhealthy.
cmd_reapply() {
  local backup=${1:-}
  [ -f "$CUR" ] || die "no current release recorded in $CUR - nothing to re-apply"
  # shellcheck disable=SC1090
  . "$CUR"
  local ref="$IMAGE_TAG"
  [ "$(image_id "$IMAGE_TAG" || true)" = "$IMAGE_ID" ] || { log "tag $IMAGE_TAG no longer maps to $IMAGE_ID - using the image ID"; ref="$IMAGE_ID"; }
  cmd_preflight || die "preflight failed - nothing was changed"
  log "re-applying $COMPOSE_FILE to $WEB using the SAME image $ref"
  if compose_up "$ref" && wait_healthy "reapply" && caddy_ok; then
    log "reapply OK: $WEB recreated from the unchanged image; database untouched"; return 0
  fi
  log "reapply failed verification"
  d logs --tail 25 "$WEB" 2>&1 | sed -E 's/(Password|Pwd)=[^;" ]*/\1=<redacted>/Ig; s/^/[container] /' || true
  if [ -n "$backup" ] && [ -f "$backup" ]; then
    log "restoring the previous Compose file from $backup and recreating"
    cp "$backup" "$COMPOSE_FILE"
    if compose_up "$ref" && wait_healthy "reapply-restore"; then die "reapply FAILED; previous configuration restored and healthy"; fi
  fi
  die "reapply FAILED and could not be restored automatically - run: rollback"
}

# Post-boot / steady-state evidence that the stack recovers by itself (read-only). Run it before and after a reboot.
# Passes only when: docker+containerd enabled, every container has restart=unless-stopped, all required containers are running AND
# healthy (not just "running"), no restart loop (RestartCount), SQL answers a query, the app /health (SELECT 1) and the Caddy route return 200,
# and the obsolete pems.service is not enabled.
cmd_boot_check() {
  FAILED=0
  local c st hl rp rc
  log "boot: $(uptime -s 2>/dev/null || echo unknown)   now: $(date '+%F %T')   up: $(uptime -p 2>/dev/null || true)"
  for u in docker containerd; do
    [ "$("$SYSTEMCTL" is-enabled "$u" 2>/dev/null)" = "enabled" ] && pass "systemd: $u enabled at boot" || fail "systemd: $u is NOT enabled at boot"
  done
  for c in "$SQL" "$WEB" "$CADDY"; do
    st=$(container_state "$c"); rp=$(d container inspect --format '{{.HostConfig.RestartPolicy.Name}}' "$c" 2>/dev/null || echo none)
    hl=$(d container inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}n/a{{end}}' "$c" 2>/dev/null || echo n/a)
    rc=$(d container inspect --format '{{.RestartCount}}' "$c" 2>/dev/null || echo ?)
    [ "$st" = "running" ] && pass "$c running (started $(d container inspect --format '{{.State.StartedAt}}' "$c" | cut -c1-19))" || fail "$c state=$st"
    case "$rp" in unless-stopped|always) pass "$c restart policy = $rp";; *) fail "$c restart policy = $rp (would not return after a reboot)";; esac
    case "$hl" in healthy|n/a) pass "$c health = $hl";; *) fail "$c health = $hl";; esac
    [ "$rc" = "?" ] || [ "$rc" -le 3 ] 2>/dev/null && pass "$c RestartCount = $rc (<=3, no restart loop)" || fail "$c RestartCount = $rc (restart loop?)"
  done
  local sq; sq=$(d exec "$SQL" bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM PEMSDB.dbo.__EFMigrationsHistory"' 2>/dev/null | tr -d '\r ' | head -1)
  case "$sq" in ''|*[!0-9]*) fail "SQL Server did not answer a query on PEMSDB";; *) pass "SQL Server answers; PEMSDB has $sq applied migrations";; esac
  [ "$(http_code "$HEALTH_URL")" = "200" ] && pass "app /health 200 (runs SELECT 1 on the database)" || fail "app /health"
  caddy_ok && pass "Caddy serves https://$CADDY_HOST (200)" || fail "Caddy route"
  [ "$(http_code "http://127.0.0.1:80/")" != "000" ] && pass "Caddy answers on port 80" || fail "Caddy port 80"
  d port "$WEB" 2>/dev/null | grep -q '0.0.0.0:8080' && fail "app port 8080 is published on all interfaces (expected 127.0.0.1 only)" || pass "app port 8080 is not published on the LAN"
  case "$("$SYSTEMCTL" is-enabled pems.service 2>/dev/null)" in enabled|enabled-runtime) fail "obsolete pems.service is ENABLED (a second ASP.NET process would start at boot) - sudo systemctl disable --now pems.service";; *) pass "obsolete pems.service is not enabled";; esac
  local errs; errs=$(d logs "$WEB" 2>&1 | grep -c -E 'SqlException|Cannot open database|A network-related|Unhandled exception|DataProtection.*(error|fail)' || true)
  [ "${errs:-0}" -eq 0 ] && pass "app log: no database-readiness / unhandled-exception lines" || fail "app log has $errs database-readiness / unhandled-exception line(s) (see: docker logs $WEB)"
  [ "${FAILED:-0}" -eq 0 ] && { log "boot-check PASSED"; return 0; } || { log "boot-check FAILED"; return 1; }
}

cmd_status() {
  echo "current : $( [ -f "$CUR" ] && grep -E '^(RELEASE_ID|IMAGE_TAG)=' "$CUR" | tr '\n' ' ' || echo none )"
  echo "previous: $( [ -f "$PREV" ] && grep -E '^(PREV_TAG|PREV_MANAGED|PREV_CONTAINER)=' "$PREV" | tr '\n' ' ' || echo none )"
  for c in "$WEB" "$SQL" "$CADDY"; do echo "container $c: $(container_state "$c") ($(d container inspect --format '{{.Config.Image}}' "$c" 2>/dev/null || echo -))"; done
  echo "health  : app=$(http_code "$HEALTH_URL")  caddy=$(http_code -k --resolve "$CADDY_HOST:443:127.0.0.1" "https://$CADDY_HOST/health")"
}

cmd_smoke() { local rc=0; [ "$(http_code "$HEALTH_URL")" = "200" ] && pass "app /health 200" || { fail "app /health"; rc=1; }; caddy_ok && pass "Caddy route" || rc=1; return $rc; }

cmd_export_cipher() {
  umask 077; mkdir -p "$(dirname "$CIPHER_FILE")"
  d exec "$SQL" bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -W -h -1 -d PEMSDB -Q "SET NOCOUNT ON; SELECT TOP 1 EncryptedPassword FROM StorageProviders WHERE EncryptedPassword IS NOT NULL"' > "$CIPHER_FILE" 2>/dev/null
  chmod 600 "$CIPHER_FILE"; log "stored ciphertext ($(stat -c %s "$CIPHER_FILE") bytes, mode 600; not displayed)"
}

cmd_verify_runtime() {   # proof, inside the CURRENT release image: OCR extraction + existing credential decrypts with the volume's keys
  local img rc=0; img=$(d container inspect --format '{{.Config.Image}}' "$WEB") || die "no $WEB container"
  [ -d "$PROBE_DIR" ] || die "probe not installed at $PROBE_DIR"
  echo "== OCR (fixture text must be extracted by the Tesseract .NET wrapper using the image's /app/x64 wiring)"
  d run --rm --network none --entrypoint sh -v "$PROBE_DIR":/probe:ro "$img" -c 'cp -r /probe /tmp/p && rm -rf /tmp/p/x64 && ln -s /app/x64 /tmp/p/x64 && dotnet /tmp/p/PemsLinuxProbe.dll ocr /tmp/p/ocr_fixture.png "${OCR__TessDataPath:-/usr/share/tesseract-ocr/5/tessdata}" "${OCR__Languages:-eng}" PEMS 12345' 2>&1 | grep -E 'OCR_(TEXT=|RESULT)' || rc=1
  echo "== DataProtection (keys from the persistent volume must decrypt the stored provider credential)"
  if [ -f "$CIPHER_FILE" ]; then
    d run --rm --network none --entrypoint dotnet -v "$PROBE_DIR":/probe:ro -v pems-dpkeys:/keys:ro -v "$CIPHER_FILE":/cipher:ro "$img" /probe/PemsLinuxProbe.dll dp-decrypt /keys /app/ /cipher 2>&1 | grep -E 'DP_DECRYPT' || rc=1
  else echo "DP_DECRYPT=SKIPPED (run export-cipher first)"; rc=1; fi
  return $rc
}

sqlc() { d exec "$SQL" bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -C -W -h -1 -b -d \"${SQL_DB:-PEMSDB}\" -Q \"SET NOCOUNT ON; $1\""; }

cmd_db_backup() {
  local ts bk; ts=$(date -u +%Y%m%dT%H%M%SZ); bk="/var/opt/mssql/backup/PEMSDB_pre_deploy_$ts.bak"
  sqlc "BACKUP DATABASE [PEMSDB] TO DISK=N'$bk' WITH COPY_ONLY, CHECKSUM, COMPRESSION, INIT, NAME=N'PEMSDB pre-deploy $ts'" >/dev/null
  sqlc "RESTORE VERIFYONLY FROM DISK=N'$bk' WITH CHECKSUM" | grep -q 'valid' || die "backup verification failed"
  sum=$(d exec "$SQL" sh -c "sha256sum '$bk' | cut -d' ' -f1"); size=$(d exec "$SQL" sh -c "stat -c %s '$bk'")
  umask 077; { echo "BACKUP_FILE=$bk"; echo "SHA256=$sum"; echo "SIZE=$size"; echo "VERIFIED_AT=$(date -u +%s)"; } > "$STATE_DIR/last-verified-backup.env"
  log "backup $bk size=$size sha256=$sum (VERIFYONLY ok). Existing backups untouched."
}

cmd_db_apply() {
  local f=${1:-} flag=${2:-}; [ -f "$f" ] || die "sql file not found: $f"; [ "$flag" = "--confirm-reviewed" ] || die "refusing to apply without --confirm-reviewed (the script must have been reviewed)"
  [ -f "$STATE_DIR/last-verified-backup.env" ] || die "no verified backup recorded - run db-backup first"
  . "$STATE_DIR/last-verified-backup.env"; age=$(( $(date -u +%s) - VERIFIED_AT )); [ "$age" -le 3600 ] || die "last verified backup is ${age}s old (> 3600s) - take a fresh one"
  d cp "$f" "$SQL:/tmp/pems-migration.sql"
  d exec "$SQL" bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -d PEMSDB -i /tmp/pems-migration.sql' || die "migration script FAILED - restore from $BACKUP_FILE if the schema is inconsistent"
  log "applied $f; migration count now: $(sqlc 'SELECT COUNT(*) FROM __EFMigrationsHistory' | tail -1)"
}

main() {
case "${1:-}" in
  preflight)       cmd_preflight;;
  adopt-env)       cmd_adopt_env;;
  prepare-volumes) shift; cmd_prepare_volumes "$@";;
  build)           shift; cmd_build "$@";;
  deploy)          shift; cmd_deploy "$@";;
  rollback)        do_rollback;;
  reapply)         shift; cmd_reapply "$@";;
  boot-check)      cmd_boot_check;;
  status)          cmd_status;;
  smoke)           cmd_smoke;;
  export-cipher)   cmd_export_cipher;;
  verify-runtime)  cmd_verify_runtime;;
  db-backup)       cmd_db_backup;;
  db-apply)        shift; cmd_db_apply "$@";;
  *) echo "usage: pems-docker.sh {preflight|adopt-env|prepare-volumes|build|deploy|rollback|reapply|boot-check|status|smoke|export-cipher|verify-runtime|db-backup|db-apply}"; exit 2;;
esac
}

# run only when executed (tests source this file to exercise single functions)
if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi
