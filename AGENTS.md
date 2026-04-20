# AGENTS.md

## Dev Commands

```bash
# Backend (runs on port 5213)
cd Proxy.Host && dotnet run --launch-profile http

# Run tests
dotnet test

# Build
dotnet build YarpProxyManager.slnx
```

## JWT Secret Required

App will not start without JWT key (min 64 chars):
```bash
cd Proxy.Host
dotnet user-secrets set "Jwt:Key" "your-very-long-random-secret-here!!"
```
Or via env: `JWT__KEY=...`

## First-Run Credentials

- Username: `Admin`
- Password: `Rexadmin1234.`
- Must change password immediately after first login

## Solution Format

This repo uses `.slnx` (modern .NET solution format), not `.sln`.

## Test Config

Run `set_test_config.ps1` to seed test data (requires backend running).