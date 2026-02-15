# Helpdesk-Light Operations Runbook

## 1. Scope

This runbook covers:

- SQLite and attachment backup/restore
- Background email reliability (retry + dead-letter)
- Operational checks for API, worker, mail adapter, and AI provider

## 2. Backup Strategy

### Frequency

- Production: hourly incremental filesystem snapshot + daily backup archive.
- Non-production: daily backup archive.

### What to back up

- SQLite database file (`helpdesk-light.db`)
- Attachment directory (`storage/attachments`)

### Backup command

```bash
./scripts/backup-helpdesk.sh \
  --db ./helpdesk-light.db \
  --attachments ./storage/attachments \
  --out ./backups
```

### Validation included

- `PRAGMA integrity_check` runs before and after DB copy.
- SHA-256 checksum file is generated for DB, metadata, and attachment files.

## 3. Restore Procedure

### Preconditions

- Stop API and Worker services.
- Confirm archive path and restore target paths.

### Restore command

```bash
./scripts/restore-helpdesk.sh \
  --archive ./backups/helpdesk-backup-<timestamp>.tar.gz \
  --db ./helpdesk-light.db \
  --attachments ./storage/attachments
```

### Validation included

- Optional checksum verification from `checksums.sha256`.
- `PRAGMA integrity_check` runs on extracted DB and restored DB.
- Existing DB and attachment directories are preserved with `.pre-restore-<timestamp>` suffix.

## 4. Background Job Reliability

### Retry and dead-letter rules

- Outbound messages are retried up to `Email:MaxRetryCount`.
- Messages that exceed retry threshold move to `DeadLetter` status.
- Dead letters are visible via `GET /api/v1/ops/dead-letters`.

### Retrying dead letters

```bash
curl -X POST "http://localhost:5283/api/v1/email/outbound/retry-dead-letter?take=50" \
  -H "Authorization: Bearer <admin-token>"
```

## 5. Health and Diagnostics

### Health endpoints

- Liveness: `GET /health/live`
- Readiness: `GET /health/ready`

Readiness includes checks for:

- SQLite connectivity
- Mail adapter configuration
- AI provider configuration

### Runtime metrics endpoint

- `GET /api/v1/ops/metrics`

Includes:

- API request count, errors, and average latency
- worker queue depth
- email outcome counters
- AI duration and failure counters
- recent worker failures

## 6. Rollback Guidance

If a deployment fails:

1. Stop services.
2. Restore latest verified archive with `restore-helpdesk.sh`.
3. Validate `/health/ready` and login flow.
4. Replay dead-letter emails if required.
5. Document incident timeline and root cause in postmortem notes.
