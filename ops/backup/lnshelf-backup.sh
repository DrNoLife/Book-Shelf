#!/usr/bin/env bash
# Nightly backup of LnShelf: a custom-format pg_dump of the database and a tar of the cover
# store, the newest 14 of each kept. Runs from the server account's crontab at 03:00, an hour
# before the nightly sync (README.md, Backups):
#
#   0 3 * * * /home/neko/docker/lnshelf/ops/backup/lnshelf-backup.sh >> /home/neko/docker/lnshelf/backups/backup.log 2>&1
#
# Exits non-zero on any failure and only then skips the heartbeat ping, so a broken night shows
# up in Uptime Kuma as a missing ping rather than as a log line nobody reads.
set -euo pipefail

# cron starts with an almost empty environment; name everything this script needs. Docker on
# this host is the snap, hence /snap/bin.
export PATH=/usr/local/bin:/usr/bin:/bin:/snap/bin
export LC_ALL=C

LNSHELF_DIR="${LNSHELF_DIR:-/home/neko/docker/lnshelf}"
BACKUP_DIR="${BACKUP_DIR:-$LNSHELF_DIR/backups/nightly}"
# The cover store's Docker volume. Until MinIO is retired it is lnshelf_lnshelf-minio; the
# crontab line sets it while that is so.
COVERS_VOLUME="${COVERS_VOLUME:-lnshelf_lnshelf-covers}"
KEEP="${KEEP:-14}"
# A dump is a few MB today; refusing to start below this keeps a full disk from also
# truncating the last good backup's neighbours.
MINIMUM_FREE_MEGABYTES="${MINIMUM_FREE_MEGABYTES:-2048}"
# Shared with the deploy script, so a nightly dump and a pre-deploy dump never overlap.
LOCK_FILE="${LOCK_FILE:-$LNSHELF_DIR/.ops.lock}"
ALPINE_IMAGE="alpine:3.22"

# The Postgres container, found by its compose labels rather than through a compose file: the
# live compose file moves with each release (releases/<version>/) and needs the image digests
# to interpolate, neither of which a database dump has any business knowing.
postgres_container() {
    docker ps --quiet         --filter label=com.docker.compose.project=lnshelf         --filter label=com.docker.compose.service=postgres
}

log() {
    printf '%s %s\n' "$(date -Is)" "$*"
}

# The heartbeat URL carries Uptime Kuma's push token, so it lives in the server's .env next to
# the other secrets. Read the one key rather than sourcing the file: .env is compose syntax,
# not shell, and sourcing would execute it.
heartbeat_url() {
    local line
    line="$(grep -E '^BACKUP_HEARTBEAT_URL=' "$LNSHELF_DIR/.env" 2>/dev/null | tail -n 1 || true)"
    line="${line#BACKUP_HEARTBEAT_URL=}"
    line="${line%\"}"
    printf '%s' "${line#\"}"
}

umask 077
mkdir -p "$BACKUP_DIR"
chmod 700 "$LNSHELF_DIR/backups" "$BACKUP_DIR"

exec 9>"$LOCK_FILE"
if ! flock -w 600 9; then
    log "Another backup or deploy has held the lock for ten minutes; giving up."
    exit 1
fi

# A run that died half way leaves a .tmp behind; it was never a backup.
find "$BACKUP_DIR" -maxdepth 1 -name '*.tmp' -delete

free_megabytes="$(df -Pm "$BACKUP_DIR" | awk 'NR == 2 { print $4 }')"
if (( free_megabytes < MINIMUM_FREE_MEGABYTES )); then
    log "Only ${free_megabytes} MB free under $BACKUP_DIR (minimum ${MINIMUM_FREE_MEGABYTES} MB); not backing up."
    exit 1
fi

stamp="$(date +%Y%m%d-%H%M%S)"
dump="$BACKUP_DIR/lnshelf-$stamp.dump"
covers="$BACKUP_DIR/covers-$stamp.tar"

postgres="$(postgres_container)"
if [[ -z "$postgres" || "$postgres" == *$'
'* ]]; then
    log "Expected exactly one running lnshelf Postgres container."
    exit 1
fi

log "Dumping the database."
docker exec "$postgres" pg_dump -U lnshelf -d lnshelf -Fc > "$dump.tmp"
# A dump pg_restore cannot list is not a backup; check before it can replace a good one.
docker exec -i "$postgres" pg_restore --list < "$dump.tmp" > /dev/null
mv "$dump.tmp" "$dump"

log "Archiving the cover store ($COVERS_VOLUME)."
if ! docker volume inspect "$COVERS_VOLUME" > /dev/null 2>&1; then
    log "Volume $COVERS_VOLUME does not exist."
    exit 1
fi
# Plain tar: covers are webp, which does not compress further. Read-only mount, and the store's
# in-flight temp files are not covers.
docker run --rm --network none -v "$COVERS_VOLUME:/covers:ro" "$ALPINE_IMAGE" \
    tar -C /covers --exclude=./.tmp -cf - . > "$covers.tmp"
mv "$covers.tmp" "$covers"

# Off-site: a restic copy of $dump and $covers goes here once a target exists (docs/backlog.md).

# Rotate only after a complete run, and only completed files.
prune() {
    local pattern="$1"
    find "$BACKUP_DIR" -maxdepth 1 -name "$pattern" -printf '%T@ %p\n' \
        | sort -rn | tail -n +"$((KEEP + 1))" | cut -d' ' -f2- | xargs -r rm -f
}
prune 'lnshelf-*.dump'
prune 'covers-*.tar'

log "Backed up: $(du -h "$dump" | cut -f1) database, $(du -h "$covers" | cut -f1) covers."

url="$(heartbeat_url)"
if [[ -n "$url" ]]; then
    curl -fsS -m 10 --retry 2 -o /dev/null "$url?status=up&msg=backup-ok" || log "Heartbeat ping failed (the backup itself succeeded)."
fi
