# Insaaf — Local Development

**Phase:** 2 (authentication backend + mobile auth slice)

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
| SQL Server | 2019+ or Docker | Database (required for auth against real DB) |

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

### Auth endpoints (Phase 2)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/v1/auth/register` | No | Register; returns access + refresh tokens |
| POST | `/api/v1/auth/login` | No | Login; returns access + refresh tokens |
| POST | `/api/v1/auth/refresh` | No | Rotate refresh token; returns new token pair |
| GET | `/api/v1/auth/me` | Bearer JWT | Current user profile |

Swagger (Development): `http://localhost:5232/swagger` — use **Authorize** with `Bearer <accessToken>`.

### Connection string override (User Secrets)

```bash
cd src/backend/Insaaf.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=Insaaf;Trusted_Connection=True;TrustServerCertificate=True"
```

### JWT signing key (User Secrets — required beyond Development defaults)

```bash
cd src/backend/Insaaf.API
dotnet user-secrets set "Jwt:SigningKey" "YourProductionLikeSigningKey_AtLeast32Characters!"
```

Optional JWT overrides: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`.

### Optional development Admin seed (User Secrets only — never hard-code credentials)

```bash
cd src/backend/Insaaf.API
dotnet user-secrets set "Identity:SeedAdmin:Enabled" "true"
dotnet user-secrets set "Identity:SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:SeedAdmin:Password" "YourStrongAdminPassword1!"
```

Roles `User` and `Admin` are always seeded. No normal user account is seeded by default.

### EF Core migrations (founder manual)

Apply all migrations (including `AddIdentityAndRefreshTokens` and `AddApplicationUserFullName`):

```bash
cd src/backend
dotnet ef database update --project Insaaf.Infrastructure/Insaaf.Infrastructure.csproj --startup-project Insaaf.API/Insaaf.API.csproj
```

Auth endpoints against SQL Server require these migrations. Integration tests use in-memory EF and do not need SQL Server.

### Register request body (Phase 2)

```json
{
  "email": "user@example.com",
  "password": "Password1!",
  "fullName": "أحمد الخضيري",
  "username": "ahmed.food"
}
```

### Backend tests

```bash
cd src/backend
dotnet test Insaaf.sln
```

Expected: **19 passing** (7 application + 12 API integration).

## Mobile

```bash
cd src/mobile
npm ci
npx expo start
```

API base URL is configured in `src/mobile/config/env.ts` (default `http://localhost:5232`).

For a physical device, set the LAN IP of your dev machine (same port `5232`).

### Phase 2 auth behavior

- **Access token:** held in memory only (`services/auth/session.ts`)
- **Refresh token:** stored in Expo Secure Store (`services/auth/tokenStorage.ts`)
- **Startup:** `AuthProvider` attempts silent refresh via TanStack Query `useRefreshMutation` if a refresh token exists
- **Auth mutations:** login/register use TanStack Query mutations (`features/auth/hooks/useAuthQueries.ts`)
- **Arabic errors:** API envelope error codes mapped to Arabic messages (`services/auth/authErrors.ts`)
- **401 handling:** Axios interceptor performs single-flight refresh; clears session on failure
- **Routes:** `/login`, `/register` (unauthenticated); `/` redirects to login when logged out

### Lint and typecheck (CI parity)

```bash
cd src/mobile
npm run lint
npm run typecheck
```

Typed Expo Router paths for `/login` and `/register` are declared in `types/expo-router.d.ts` (committed for CI; `npx expo start` may regenerate `.expo/types/router.d.ts` locally).

### npm / SSL on Windows

If `npm install` or `npm ci` fails with `UNABLE_TO_VERIFY_LEAF_SIGNATURE`, your network or proxy may be intercepting TLS. Options:

1. Fix corporate proxy CA in Node/npm (preferred)
2. Temporarily: `$env:NODE_TLS_REJECT_UNAUTHORIZED='0'` before `npm ci` (development only)
3. Regenerate lockfile if needed: `node scripts/generate-lockfile.mjs` (uses registry fetch; same TLS caveat)

CI on GitHub Actions should not hit this issue.

## Environment files

- Use `.env.local` or User Secrets for connection strings, JWT keys, and admin seed credentials.
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
| Auth 500 / Identity tables missing | Run `dotnet ef database update` |
| Auth 500 / `Invalid column name 'FullName'` | Apply `AddApplicationUserFullName` migration via `dotnet ef database update` |
| JWT invalid / 401 on `/auth/me` | `Jwt:SigningKey` set; token not expired; `Authorization: Bearer` header |
| API won't start (OpenApi) | Use Swashbuckle only; do not add `Microsoft.AspNetCore.OpenApi` alongside Swashbuckle |
| Expo cannot reach API | Use machine LAN IP for device testing; match port `5232` |
| RTL layout issues | Test Arabic strings early; see `.cursor/rules/ui-ux.mdc` |
| npm SSL errors | See **npm / SSL on Windows** above |
| Typecheck fails on `/login` route | Ensure `types/expo-router.d.ts` is present; run `npm run typecheck` |

## Related docs

- [architecture.md](architecture.md)
- [implementation-plan.md](implementation-plan.md)
- [figma/README.md](figma/README.md)
- [git-workflow.md](git-workflow.md)
