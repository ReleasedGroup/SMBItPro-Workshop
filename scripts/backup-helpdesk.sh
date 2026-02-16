#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<USAGE
Usage: $(basename "$0") [--db PATH] [--attachments PATH] [--out PATH]

Creates a timestamped backup archive containing:
- SQLite database file
- Attachments directory
- metadata and checksums

Defaults:
  --db           ./helpdesk-light.db
  --attachments  ./storage/attachments
  --out          ./backups
USAGE
}

DB_PATH="./helpdesk-light.db"
ATTACHMENTS_PATH="./storage/attachments"
OUT_DIR="./backups"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --db)
      DB_PATH="$2"
      shift 2
      ;;
    --attachments)
      ATTACHMENTS_PATH="$2"
      shift 2
      ;;
    --out)
      OUT_DIR="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ ! -f "$DB_PATH" ]]; then
  echo "Database file not found: $DB_PATH" >&2
  exit 1
fi

if ! command -v sqlite3 >/dev/null 2>&1; then
  echo "sqlite3 is required for integrity checks." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
TS="$(date -u +%Y%m%dT%H%M%SZ)"
WORK_DIR="$(mktemp -d)"
PAYLOAD_DIR="$WORK_DIR/helpdesk-backup-$TS"
ARCHIVE_PATH="$OUT_DIR/helpdesk-backup-$TS.tar.gz"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

mkdir -p "$PAYLOAD_DIR"

INTEGRITY_RESULT="$(sqlite3 "$DB_PATH" 'PRAGMA integrity_check;')"
if [[ "$INTEGRITY_RESULT" != "ok" ]]; then
  echo "SQLite integrity check failed before backup: $INTEGRITY_RESULT" >&2
  exit 1
fi

sqlite3 "$DB_PATH" ".backup '$PAYLOAD_DIR/helpdesk.db'"

if [[ -d "$ATTACHMENTS_PATH" ]]; then
  cp -R "$ATTACHMENTS_PATH" "$PAYLOAD_DIR/attachments"
else
  mkdir -p "$PAYLOAD_DIR/attachments"
fi

cat > "$PAYLOAD_DIR/metadata.txt" <<META
created_utc=$TS
source_db=$DB_PATH
source_attachments=$ATTACHMENTS_PATH
META

(
  cd "$PAYLOAD_DIR"
  shasum -a 256 helpdesk.db metadata.txt > checksums.sha256
  if [[ -d attachments ]]; then
    find attachments -type f -print0 | sort -z | xargs -0 shasum -a 256 >> checksums.sha256 || true
  fi
)

POST_COPY_INTEGRITY="$(sqlite3 "$PAYLOAD_DIR/helpdesk.db" 'PRAGMA integrity_check;')"
if [[ "$POST_COPY_INTEGRITY" != "ok" ]]; then
  echo "SQLite integrity check failed on backup copy: $POST_COPY_INTEGRITY" >&2
  exit 1
fi

tar -czf "$ARCHIVE_PATH" -C "$WORK_DIR" "helpdesk-backup-$TS"

echo "Backup created: $ARCHIVE_PATH"
