# SMBItPro-Workshop

This repository now includes a .NET 10 implementation baseline for **Helpdesk-Light**.

## Solution Layout

- `src/Helpdesk.Light.Domain`: domain entities and role constants.
- `src/Helpdesk.Light.Application`: contracts and service abstractions.
- `src/Helpdesk.Light.Infrastructure`: EF Core + SQLite, Identity entities, seed data, and tenant/customer services.
- `src/Helpdesk.Light.Api`: ASP.NET Core API with JWT auth, role-based access, customer/domain admin, and tenant resolution endpoints.
- `src/Helpdesk.Light.Web`: Blazor WebAssembly frontend shell and design system.
- `src/Helpdesk.Light.Worker`: worker service project scaffold.
- `tests/Helpdesk.Light.UnitTests`: domain and service unit tests.
- `tests/Helpdesk.Light.IntegrationTests`: API integration tests for auth, role policy, and tenant boundaries.

## Prerequisites

- .NET SDK `10.0.103` (see `global.json`).

## Restore, Build, and Test

```bash
dotnet restore Helpdesk.Light.slnx
dotnet build Helpdesk.Light.slnx -warnaserror
dotnet test Helpdesk.Light.slnx
```

## Run API

```bash
dotnet run --project src/Helpdesk.Light.Api
```

Default API URL (from launch settings): `http://localhost:5035`

Seeded users:

- `admin@msp.local` / `Pass!12345` (`MspAdmin`)
- `tech@contoso.com` / `Pass!12345` (`Technician`, Contoso tenant)
- `tech@fabrikam.com` / `Pass!12345` (`Technician`, Fabrikam tenant)

## Run Blazor WebAssembly UI

```bash
dotnet run --project src/Helpdesk.Light.Web
```

Default UI URL: `http://localhost:5167`.

The API base URL is configured in `src/Helpdesk.Light.Web/wwwroot/appsettings.json` via `ApiBaseUrl`.

## Run Worker

```bash
dotnet run --project src/Helpdesk.Light.Worker
```
