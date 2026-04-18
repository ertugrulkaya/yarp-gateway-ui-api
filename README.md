# YARP Gateway — UI & API

A full-stack reverse proxy management platform built on **ASP.NET Core 10** and **Angular 21**.
Configure, monitor and audit a [YARP](https://microsoft.github.io/reverse-proxy/) reverse proxy through a polished Material Design dashboard — no config file restarts required.

---

## Screenshots

> Dashboard · Route Editor · Cluster Editor · Audit History · Raw JSON Editor

---

## Features

### Backend (ASP.NET Core 10 + YARP 2.3)
| Area | Details |
|------|---------|
| **Hot reload** | Routes and clusters applied instantly via `IProxyConfigProvider` — zero downtime |
| **JWT auth** | HMAC-SHA512 tokens; secret loaded from env var / user-secrets (never in source) |
| **First-run security** | Default admin is created with `MustChangePassword = true`; all API calls return 403 until password is changed |
| **Account lockout** | 5 failed logins → 15-minute lock; sliding-window rate limiter (10 req/min per IP) |
| **Atomic config writes** | LiteDB `BeginTrans / Commit / Rollback` — no half-written state |
| **Thread-safe provider** | `ReaderWriterLockSlim` + proper `IDisposable` / `CancellationTokenSource` management |
| **Async log queue** | `Channel<LogEntry>` (bounded 10 k, DropOldest) drained by a `BackgroundService` — middleware never blocks |
| **Audit trail** | Every CRUD operation recorded in `config_history` (who, when, before/after JSON) |
| **Health check** | `GET /health` (anonymous) — verifies both LiteDB files are reachable |
| **Backup / Restore** | `GET /backup` downloads a timestamped JSON snapshot; `POST /restore` imports atomically |
| **YARP validation** | Routes and clusters are validated by YARP's own `IConfigValidator` before saving; invalid entries are skipped at startup with a log warning instead of crashing |
| **Startup resilience** | `RoutePatternFactory.Parse` guards `LoadFromDb` — a bad path in DB never prevents startup |
| **ClusterId cross-ref** | Routes that reference a non-existent cluster are rejected with 422 |
| **Standardised errors** | Global `UseExceptionHandler` + `ApiError(Code, Message)` record on every endpoint |
| **LiteDB indexes** | `routes.Config.ClusterId`, `logs.ClusterId`, `logs.StatusCode`, `logs.ClientIp` |
| **OpenAPI / Scalar** | Development: `/openapi/v1.json` + interactive UI at `/scalar/v1` |

### Frontend (Angular 21 + Angular Material)
| Area | Details |
|------|---------|
| **Signal-based state** | `signal<RouteConfig[]>`, `signal<ClusterConfig[]>`, `computed()` — no manual `markForCheck` needed |
| **OnPush everywhere** | All components use `ChangeDetectionStrategy.OnPush` |
| **Route dialog** | 5-tab form: Basic · Match · Transforms · Policies · Metadata |
| **Cluster dialog** | 5-tab form: Basic · Destinations · Session Affinity · Health Check · Http Client |
| **Service Wizard** | Auto-generate a cluster + common routes for a new backend service in one dialog |
| **Raw JSON editor** | Edit full config as JSON with live YARP-format import (supports full `appsettings.json`, `ReverseProxy` section, or routes-only dict) |
| **Import / Export** | Export a dated JSON backup; restore from file with a single click |
| **Audit history page** | Paginated history table with before/after JSON diff dialog |
| **Request logs** | Paginated request log table (method, path, cluster, status, duration) |
| **Material Confirm dialog** | Replaces native `confirm()` for delete actions |
| **401 guard** | Interceptor returns `EMPTY` on 401 — no stale snackbar errors on logout |
| **Change-password guard** | Blocks all routes until first-run password is set |

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| Proxy engine | YARP 2.3 |
| Database | LiteDB 5 (embedded, no server) |
| Auth | JWT Bearer (HMAC-SHA512) |
| Rate limiting | .NET built-in `System.Threading.RateLimiting` |
| API docs | `Microsoft.AspNetCore.OpenApi` + Scalar |
| Frontend framework | Angular 21 (standalone components, zoneless) |
| UI library | Angular Material 21 |
| Build tool | Vite (via Angular CLI) |

---

## Project Structure

```
Proxy/
├── Proxy.Host/                     # ASP.NET Core backend
│   ├── Controllers/
│   │   ├── AuthController.cs       # Login, logout, change password
│   │   └── ProxyConfigController.cs# Routes, clusters, raw, backup, restore, history
│   ├── Middleware/
│   │   └── YarpLoggingMiddleware.cs# Per-request proxy log
│   ├── Models/
│   │   ├── ApiError.cs             # Standardised error record
│   │   ├── ConfigDtos.cs           # LiteDB wrappers + all DTOs
│   │   ├── ConfigHistory.cs        # Audit trail model
│   │   ├── LogEntry.cs
│   │   └── User.cs
│   ├── Providers/
│   │   └── LiteDbProxyConfigProvider.cs  # Hot-reload YARP provider
│   ├── Services/
│   │   ├── LiteDbHealthCheck.cs
│   │   ├── LiteDbService.cs        # DB init, seed, indexes
│   │   ├── LogService.cs           # Channel-based async log queue
│   │   └── LogWriterService.cs     # BackgroundService draining the channel
│   └── Program.cs
│
└── Proxy.UI/                       # Angular 21 frontend
    └── src/app/
        ├── core/
        │   ├── auth-interceptor.ts
        │   └── change-password-guard.ts
        ├── pages/
        │   ├── dashboard/           # Routes & clusters tables + dialogs
        │   │   └── dialogs/         # Route, Cluster, RawEdit, ServiceWizard
        │   ├── history/             # Audit trail + diff dialog
        │   ├── login/
        │   ├── change-password/
        │   ├── logs/
        │   └── raw-editor/          # JSON editor + YARP import
        ├── services/
        │   ├── auth.ts
        │   ├── logs.ts
        │   └── proxy-config.ts
        └── shared/
            └── confirm-dialog/
```

---

## Getting Started

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.x |
| Node.js | 22.x |
| npm | 10.x |

### 1 — Set the JWT secret

The application **will not start** without a JWT key of at least 64 characters.

**Local development (user-secrets):**
```bash
cd Proxy.Host
dotnet user-secrets set "Jwt:Key" "your-very-long-random-secret-at-least-64-characters-long-here!!"
```

**Production (environment variable):**
```bash
export JWT__KEY="your-very-long-random-secret-at-least-64-characters-long-here!!"
```

### 2 — Run the backend

```bash
cd Proxy.Host
dotnet run --launch-profile http
# Listening on http://localhost:5213
# API docs:  http://localhost:5213/scalar/v1   (dev only)
# Health:    http://localhost:5213/health
```

### 3 — Run the frontend

```bash
cd Proxy.UI
npm install
npm start
# Open http://localhost:4200
```

### 4 — First login

| Field | Value |
|-------|-------|
| Username | `Admin` |
| Password | `Rexadmin1234.` |

You will be redirected to the **Change Password** screen immediately.
Set a new password — then you're in.

---

## API Reference

All endpoints require `Authorization: Bearer <token>` except where noted.

### Auth

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/login` | — | Returns JWT + `mustChangePassword` flag |
| POST | `/api/auth/change-password` | ✓ | Change password, returns new token |

### Proxy Config

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/proxyconfig/routes` | List all routes |
| POST | `/api/proxyconfig/routes` | Add route (validated) |
| PUT | `/api/proxyconfig/routes/{id}` | Update route |
| DELETE | `/api/proxyconfig/routes/{id}` | Delete route |
| GET | `/api/proxyconfig/clusters` | List all clusters |
| POST | `/api/proxyconfig/clusters` | Add cluster |
| PUT | `/api/proxyconfig/clusters/{id}` | Update cluster |
| DELETE | `/api/proxyconfig/clusters/{id}` | Delete cluster |
| GET | `/api/proxyconfig/raw` | Full config as JSON |
| POST | `/api/proxyconfig/raw` | Overwrite full config |
| GET | `/api/proxyconfig/backup` | Download JSON backup file |
| POST | `/api/proxyconfig/restore` | Restore from backup payload |
| GET | `/api/proxyconfig/history` | Audit history (paginated) |

### System

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | — | LiteDB health check |
| GET | `/openapi/v1.json` | — | OpenAPI spec (dev) |
| GET | `/scalar/v1` | — | Interactive API docs (dev) |

---

## Configuration

`appsettings.json` (non-secret values only):

```json
{
  "Jwt": {
    "Issuer": "AntiGravityProxyAuth"
  },
  "LiteDb": {
    "Path": "proxy.db",
    "LogPath": "proxy-log.db"
  }
}
```

---

## Error Format

All error responses use a consistent shape:

```json
{
  "code": "NOT_FOUND",
  "message": "Route 'my-route' not found."
}
```

Common codes: `BAD_REQUEST` · `NOT_FOUND` · `CONFLICT` · `UNPROCESSABLE` · `INVALID_ROUTE` · `INVALID_CLUSTER` · `INTERNAL_SERVER_ERROR`

---

## License

MIT
