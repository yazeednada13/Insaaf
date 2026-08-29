# Insaaf — Local Development

**Phase:** 1 (backend + mobile skeleton in `src/`)

## Principles

- **Local-first** for MVP development phases (0–10).
- **No cloud infrastructure** in early phases unless explicitly approved by founder.
- Production hosting is selected in **Phase 11** (Azure App Service + Azure SQL is current candidate).

## Prerequisites

| Tool | Version (verified on founder machine) | Purpose |
|------|----------------------------------------|---------|
| .NET SDK | 9.0.x | Backend |
| Node.js | 22.x | Mobile tooling |
| Git | 2.47+ | Version control |
| SQL Server | 2019+ or Docker | Database (optional for Phase 1 health-only work) |

Optional for mobile:

- Expo Go app on physical device, or Android emulator / iOS simulator
- Watchman (macOS) for React Native file watching

## SQL Server — local options

### Option A: Local SQL Server instance

Install SQL Server Developer Edition or use an existing local instance.

Example connection string pattern (use User Secrets locally — **never commit secrets**):

```text
Server=localhost;Database=Insaaf;Trusted_Connection=True;TrustServerCertificate=True
```

### Option B: SQL Server in Docker

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name insaaf-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

Use a strong password and store credentials outside the repository.

## Backend

```bash
cd src/backend
dotnet build Insaaf.sln
dotnet run --project Insaaf.API
```

Default URLs (see `Insaaf.API/Properties/launchSettings.json`):

- HTTP: `http://localhost:5232`
- HTTPS: `https://localhost:7128`

### Health check

```bash
curl http://localhost:5232/api/v1/health
```

Expected envelope: `{ "success": true, "data": { "status": "healthy" }, "meta": { ... } }`

### Swagger (Development only)

Open `http://localhost:5232/swagger`

### Connection string override (User Secrets)

```bash
cd src/backend/Insaaf.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=Insaaf;Trusted_Connection=True;TrustServerCertificate=True"
```

### EF Core migrations (founder manual — when SQL Server is available)

```bash
cd src/backend
dotnet ef database update --project Insaaf.Infrastructure/Insaaf.Infrastructure.csproj --startup-project Insaaf.API/Insaaf.API.csproj
```

Phase 1 health endpoint does **not** require the database to be running.

## Mobile

```bash
cd src/mobile
npm ci
npx expo start
```

API base URL is configured in `src/mobile/config/env.ts` (default `http://localhost:5232`).

### Lint and typecheck (CI parity)

```bash
cd src/mobile
npm run lint
npm run typecheck
```

### npm / SSL on Windows

If `npm install` or `npm ci` fails with `UNABLE_TO_VERIFY_LEAF_SIGNATURE`, your network or proxy may be intercepting TLS. Options:

1. Fix corporate proxy CA in Node/npm (preferred)
2. Temporarily: `$env:NODE_TLS_REJECT_UNAUTHORIZED='0'` before `npm ci` (development only)
3. Regenerate lockfile if needed: `node scripts/generate-lockfile.mjs` (uses registry fetch; same TLS caveat)

CI on GitHub Actions should not hit this issue.

## Environment files

- Use `.env.local` or User Secrets for connection strings and API keys.
- `.gitignore` excludes `.env`, `*.env.local`, and secrets.
- Never commit real credentials.

## CI vs local

GitHub Actions CI (`.github/workflows/ci.yml`) runs:

- Foundation doc checks
- `dotnet build` / `dotnet test` on `src/backend/Insaaf.sln`
- `npm ci`, `npm run lint`, `npm run typecheck` in `src/mobile`

## Troubleshooting

| Issue | Check |
|-------|-------|
| SQL connection fails | Server running, port 1433, firewall, User Secrets override |
| API won't start (OpenApi) | Use Swashbuckle only; do not add `Microsoft.AspNetCore.OpenApi` alongside Swashbuckle |
| Expo cannot reach API | Use machine LAN IP for device testing; match port `5232` |
| RTL layout issues | Test Arabic strings early; see `.cursor/rules/ui-ux.mdc` |
| npm SSL errors | See **npm / SSL on Windows** above |

## Related docs

- [architecture.md](architecture.md)
- [implementation-plan.md](implementation-plan.md)
- [figma/README.md](figma/README.md)
- [git-workflow.md](git-workflow.md)
