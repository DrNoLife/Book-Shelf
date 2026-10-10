#!/usr/bin/env bash
# Proves the newest nightly backup can actually be restored: the dump goes into a throwaway
# Postgres, the core tables must have rows, and the cover archive must unpack. A backup that has
# never been read back is a hope, not a backup. Run it by hand after any change to the backup,
# and now and then besides.
set -euo pipefail
export PATH=/usr/local/bin:/usr/bin:/bin:/snap/bin
export LC_ALL=C

LNSHELF_DIR="${LNSHELF_DIR:-/home/neko/docker/lnshelf}"
BACKUP_DIR="${BACKUP_DIR:-$LNSHELF_DIR/backups/nightly}"
POSTGRES_IMAGE="postgres:17-alpine"
ALPINE_IMAGE="alpine:3.22"
container="lnshelf-restore-test-$$"

newest() {
    find "$BACKUP_DIR" -maxdepth 1 -name "$1" -printf '%T@ %p\n' | sort -rn | head -n 1 | cut -d' ' -f2-
}

dump="$(newest 'lnshelf-*.dump')"
covers="$(newest 'covers-*.tar')"
[[ -n "$dump" && -n "$covers" ]] || { echo "No complete backup pair in $BACKUP_DIR."; exit 1; }

cleanup() { docker rm -f "$container" > /dev/null 2>&1 || true; }
trap cleanup EXIT

echo "Restoring $(basename "$dump") into a throwaway database."
# No network: the restore needs nothing from outside, and nothing outside should reach it.
docker run -d --name "$container" --network none \
    -e POSTGRES_PASSWORD=restore-test -e POSTGRES_DB=restoretest "$POSTGRES_IMAGE" > /dev/null
for _ in $(seq 1 30); do
    docker exec "$container" pg_isready -U postgres -d restoretest > /dev/null 2>&1 && break
    sleep 1
done
docker exec -i "$container" pg_restore -U postgres -d restoretest --no-owner --no-acl --exit-on-error < "$dump"

for table in app_user series volume; do
    rows="$(docker exec "$container" psql -U postgres -d restoretest -tAc "SELECT count(*) FROM \"$table\"")"
    echo "  $table: $rows rows"
    (( rows > 0 )) || { echo "Table $table is empty after the restore."; exit 1; }
done

files="$(docker run --rm --network none -i "$ALPINE_IMAGE" sh -c 'mkdir /restore && tar -C /restore -xf - && find /restore -type f | wc -l' < "$covers")"
echo "  covers: $files files unpacked from $(basename "$covers")"
(( files > 0 )) || { echo "The cover archive unpacked to nothing."; exit 1; }

echo "Restore test passed."
