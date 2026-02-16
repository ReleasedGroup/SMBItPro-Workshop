#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<USAGE
Usage: $(basename "$0") --archive PATH [--db PATH] [--attachments PATH]

Restores a backup created by backup-helpdesk.sh.
Stops are not automatic: stop API/Worker before restoring.

Defaults:
  --db           ./helpdesk-light.db
  --attachments  ./storage/attachments
USAGE
}

ARCHIVE_PATH=""
DB_PATH="./helpdesk-light.db"
ATTACHMENTS_PATH="./storage/attachments"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --archive)
      ARCHIVE_PATH="$2"
      shift 2
      ;;
    --db)
      DB_PATH="$2"
      shift 2
      ;;
    --attachments)
      ATTACHMENTS_PATH="$2"
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

if [[ -z "$ARCHIVE_PATH" ]]; then
  echo "--archive is required." >&2
  usage
  exit 1
fi

if [[ ! -f "$ARCHIVE_PATH" ]]; then
  echo "Archive not found: $ARCHIVE_PATH" >&2
  exit 1
fi

if ! command -v sqlite3 >/dev/null 2>&1; then
  echo "sqlite3 is required for integrity checks." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

tar -xzf "$ARCHIVE_PATH" -C "$WORK_DIR"
BACKUP_ROOT="$(find "$WORK_DIR" -maxdepth 1 -type d -name 'helpdesk-backup-*' | head -n 1)"

if [[ -z "$BACKUP_ROOT" ]]; then
  echo "Archive structure invalid; expected helpdesk-backup-* directory." >&2
  exit 1
fi

if [[ ! -f "$BACKUP_ROOT/helpdesk.db" ]]; then
  echo "Backup is missing helpdesk.db" >&2
  exit 1
fi

if [[ -f "$BACKUP_ROOT/checksums.sha256" ]]; then
  (
    cd "$BACKUP_ROOT"
    shasum -a 256 --check checksums.sha256
  )
fi

INTEGRITY_RESULT="$(sqlite3 "$BACKUP_ROOT/helpdesk.db" 'PRAGMA integrity_check;')"
if [[ "$INTEGRITY_RESULT" != "ok" ]]; then
  echo "SQLite integrity check failed in archive: $INTEGRITY_RESULT" >&2
  exit 1
fi

TS="$(date -u +%Y%m%dT%H%M%SZ)"
if [[ -f "$DB_PATH" ]]; then
  cp "$DB_PATH" "$DB_PATH.pre-restore-$TS"
fi

if [[ -d "$ATTACHMENTS_PATH" ]]; then
  mv "$ATTACHMENTS_PATH" "$ATTACHMENTS_PATH.pre-restore-$TS"
fi

mkdir -p "$(dirname "$DB_PATH")"
mkdir -p "$(dirname "$ATTACHMENTS_PATH")"

cp "$BACKUP_ROOT/helpdesk.db" "$DB_PATH"
if [[ -d "$BACKUP_ROOT/attachments" ]]; then
  cp -R "$BACKUP_ROOT/attachments" "$ATTACHMENTS_PATH"
else
  mkdir -p "$ATTACHMENTS_PATH"
fi

RESTORED_INTEGRITY="$(sqlite3 "$DB_PATH" 'PRAGMA integrity_check;')"
if [[ "$RESTORED_INTEGRITY" != "ok" ]]; then
  echo "SQLite integrity check failed after restore: $RESTORED_INTEGRITY" >&2
  exit 1
fi

echo "Restore completed successfully."
echo "Database: $DB_PATH"
echo "Attachments: $ATTACHMENTS_PATH"
