# Run Helpdesk-Light Locally

This guide explains how to run the full Helpdesk-Light stack on a local machine, including:

- API (`src/Helpdesk.Light.Api`)
- Worker (`src/Helpdesk.Light.Worker`)
- Web UI (`src/Helpdesk.Light.Web`)

Two local workflows are covered:

1. Native `.NET` processes (recommended for development/debugging)
2. Docker Compose containers (recommended for environment parity)

## 1. Prerequisites

Install:

- `.NET SDK 10.0.103` (see `global.json`)
- `git`
- `curl` (for smoke checks)
- `jq` (for token extraction in smoke checks)
- `sqlite3` (optional, helpful for DB checks and backup scripts)
- `docker` + `docker compose` (only if using containers)

Clone and enter the repository:

```bash
git clone https://github.com/ReleasedGroup/SMBItPro-Workshop.git
cd SMBItPro-Workshop
```

## 2. Quick Architecture Notes

The API and Worker share the same SQLite database and attachment storage path.  
The Web UI is a separate Blazor WebAssembly app that calls the API over HTTP.

Default local data paths:

- Development DB: `helpdesk-light.development.db`
- Default DB: `helpdesk-light.db`
- Attachments: `storage/attachments/dev` (Development) or `storage/attachments` (default)

## 3. Option A: Run with Native .NET Processes

### 3.0 One-command Windows startup

For Windows, use the bundled bootstrap script:

```powershell
.\scripts\run-helpdesk-windows.ps1
```

What it does:

- validates tooling (`dotnet`),
- configures local Web API URL defaults,
- restores and builds the solution,
- launches API, Worker, and Web in separate PowerShell windows.

Useful flags:

- `-StopExisting` to terminate existing Helpdesk dotnet processes first.
- `-SkipBrowser` to avoid auto-opening `http://localhost:5006`.
- `-SkipRestore` / `-SkipBuild` for faster reruns when dependencies are already prepared.

### 3.1 Restore and build once

```bash
dotnet restore Helpdesk.Light.slnx
dotnet build Helpdesk.Light.slnx -warnaserror
```

### 3.2 API base URL behavior

The Web app is pre-configured for local development:

- `src/Helpdesk.Light.Web/wwwroot/appsettings.Development.json` points to `http://localhost:5283/`

When a value is not explicitly configured, the Web app auto-selects local API URLs:

- Web on `http://localhost:5006` or `https://localhost:7262` -> API `http://localhost:5283/`
- Web on `http://localhost:8082` -> API `http://localhost:8080/`

If you run API on a custom URL, set it in `src/Helpdesk.Light.Web/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "http://localhost:<custom-port>/"
}
```

### 3.3 Start API (terminal 1)

```bash
dotnet run --project src/Helpdesk.Light.Api
```

Expected:

- API URL: `http://localhost:5283`
- On first start, DB schema and seed data are created automatically.

### 3.4 Start Worker (terminal 2)

```bash
dotnet run --project src/Helpdesk.Light.Worker
```

Expected:

- Worker dispatch loop runs continuously (30-second cycle)
- Uses same DB/storage settings as API

### 3.5 Start Web UI (terminal 3)

```bash
dotnet run --project src/Helpdesk.Light.Web
```

Expected:

- UI URL: `http://localhost:5006` (and `https://localhost:7262` if using HTTPS profile)

### 3.6 Login credentials (seeded)

- `admin@msp.local` / `Pass!12345` (`MspAdmin`)
- `tech@contoso.com` / `Pass!12345` (`Technician`, Contoso)
- `tech@fabrikam.com` / `Pass!12345` (`Technician`, Fabrikam)
- `user@contoso.com` / `Pass!12345` (`EndUser`, Contoso)
- `user@fabrikam.com` / `Pass!12345` (`EndUser`, Fabrikam)

## 4. Option B: Run with Docker Compose

### 4.1 Create environment file

```bash
cp .env.example .env
```

Set at least:

- `JWT_SIGNING_KEY` to a secure value with 32+ characters
- `OPENAI_API_KEY` (optional; leave empty to run without live AI provider calls)
- `AI_ENABLED` (`true` or `false`)

### 4.2 Start containers

```bash
docker compose up --build
```

Endpoints:

- API: `http://localhost:8080`
- Web: `http://localhost:8082`

Container data persistence:

- DB and attachments are stored in Docker volume `helpdesk_data`

Stop:

```bash
docker compose down
```

Stop and remove volume (destructive local reset):

```bash
docker compose down -v
```

## 5. Smoke Test the Running Stack

### 5.1 Health endpoints

Native API run:

```bash
curl http://localhost:5283/health/live
curl http://localhost:5283/health/ready
```

Note: browsing API root (`/`) can return `401 Unauthorized` because most API routes require authentication.  
Use `/health/live` or `/health/ready` for anonymous reachability checks.

Docker run:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

### 5.2 Login and inspect current user via API

```bash
TOKEN=$(curl -s http://localhost:5283/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@msp.local","password":"Pass!12345"}' | jq -r '.accessToken')

curl -s http://localhost:5283/api/v1/auth/me \
  -H "Authorization: Bearer $TOKEN"
```

If using Docker, replace `5283` with `8080`.

### 5.3 Create and list a ticket

```bash
curl -s -X POST http://localhost:5283/api/v1/tickets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"customerId":null,"subject":"Local smoke test","description":"Created from local setup guide","priority":"Medium"}'

curl -s http://localhost:5283/api/v1/tickets?take=10 \
  -H "Authorization: Bearer $TOKEN"
```

## 6. Local Reset and Data Hygiene

For native runs, stop API/Worker/Web then remove local state:

```bash
rm -f helpdesk-light.development.db helpdesk-light.db
rm -rf storage/attachments/dev storage/attachments
```

On next API start, seed data is recreated.

For backup/restore workflows, use:

- `scripts/backup-helpdesk.sh`
- `scripts/restore-helpdesk.sh`
- `docs/operations-runbook.md`

## 7. Troubleshooting

### Symptom: Web login fails or API calls return network errors

Cause: `ApiBaseUrl` in `src/Helpdesk.Light.Web/wwwroot/appsettings.json` does not match running API URL.

Fix: Update `ApiBaseUrl` and restart Web app.

### Symptom: Browser shows CORS errors

Cause: Web origin not in API `Cors:Frontend:AllowedOrigins`.

Fix: Use default Web URLs (`http://localhost:5006`, `https://localhost:7262`) or update API CORS config.

### Symptom: `401 Unauthorized` on API calls

Cause: Missing/expired JWT or invalid credentials.

Fix: Re-run login endpoint and use returned `accessToken`.

### Symptom: API root (`/`) returns `401`, but health endpoints are `200`

Cause: Expected behavior. The API uses authenticated fallback policy for most routes.

Fix: Use `/health/live` and `/health/ready` as anonymous checks; use `/api/v1/auth/login` for authenticated flows.

### Symptom: Worker appears idle

Cause: No pending/failed outbound email records to process.

Fix: Trigger ticket activity that generates outbound events, then watch worker logs.

### Symptom: AI features not returning live model output

Cause: Missing or invalid `Ai:OpenAIApiKey` (or `OPENAI_API_KEY` in Docker).

Fix: Configure the key and restart services.

## 8. Recommended Development Loop

Use this loop for feature work:

```bash
dotnet restore Helpdesk.Light.slnx
dotnet build Helpdesk.Light.slnx -warnaserror
dotnet test Helpdesk.Light.slnx
```

Then run API + Worker + Web in separate terminals and validate behavior through UI plus API smoke checks.
