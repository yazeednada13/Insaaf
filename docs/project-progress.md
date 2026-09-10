# Insaaf — Project Progress

**Last updated:** 2026-09-06

## Current state

| Field | Value |
|-------|-------|
| **CURRENT PHASE** | 2 — Authentication (**implementation complete** — founder gates pending) |
| **CURRENT FEATURE** | — |
| **CURRENT TASK** | — |
| **COMPLETED** | Phase 2 — tasks 2.1–2.11 (implementation + gap fixes) |
| **NEXT** | Founder: apply EF migrations (`AddIdentityAndRefreshTokens` + `AddApplicationUserFullName`), SQL Server E2E smoke test, manual Figma device review, commit/PR; then **Phase 3 — Restaurants** (awaiting approval) |
| **BLOCKERS** | Founder must run `dotnet ef database update` before auth works against SQL Server; manual Figma runtime verification on device/emulator |

## Phase 2 checklist

| Task | Description | Status |
|------|-------------|--------|
| 2.1 | Domain: `RefreshToken` entity, `Roles` constants | Done |
| 2.2 | Application: `IAuthService`, DTOs, abstractions, validation, `AuthException` | Done |
| 2.3 | Infrastructure: Identity, JWT, refresh-token service (hash/rotate/reuse), seed roles, **lockout enforcement** | Done |
| 2.4 | API: `AuthController`, `AuthMeController`, JWT middleware, Swagger auth | Done |
| 2.5 | EF migration `AddIdentityAndRefreshTokens` + `AddApplicationUserFullName` (founder applies to DB) | Done (files); founder applies |
| 2.6 | Application unit tests (`AuthService`, `RefreshTokenService`) | Done (7 tests) |
| 2.7 | API integration tests (`AuthEndpointsTests`) | Done (12 tests) |
| 2.8 | Mobile auth service (Secure Store, in-memory access token, Axios interceptors) | Done |
| 2.9 | Mobile `AuthProvider` + TanStack Query auth mutations + startup refresh bootstrap | Done |
| 2.10 | Expo Router wiring (`(auth)` group, root providers, redirects) | Done |
| 2.11 | Login/Register screens (Figma-aligned Arabic RTL, RHF + Zod, Arabic API errors, registration fields) | Done (static Figma parity); manual runtime review pending |

**Phase 2 implementation is complete.** Founder EF migration, SQL Server E2E, manual Figma device review, Git commit/PR, and CI green after push are the remaining gate items.

### Phase 2 UI note (Figma)

Login/Register screens are implemented against the Figma Make source (`AuthScreen.tsx` in file `DfKJchmPebiHH9dRYaYMuS`). Static token/copy/layout parity verified against `docs/figma/auth-tokens.md` and Figma Make source. Runtime pixel review on device/emulator is a founder gate item.

### Phase 2 gap-fix summary (2026-09-06)

- **Lockout:** `SignInManager.CheckPasswordSignInAsync` with `lockoutOnFailure: true`; integration test added
- **Refresh API tests:** expired token, independently revoked token, reuse detection
- **TanStack Query:** `useLoginMutation`, `useRegisterMutation`, `useRefreshMutation` in `AuthProvider`
- **Arabic errors:** `AuthApiError` maps API envelope codes to Arabic UI messages
- **Registration:** `fullName` + `username` persisted (`ApplicationUser.FullName`, Identity `UserName`)
- **Lint:** `AuthFooterLink` fixed

---

## Phase 1 checklist

| Task | Description | Status |
|------|-------------|--------|
| 1.1 | Backend solution (Domain, Application, Infrastructure, API) + DI | Done |
| 1.2 | API baseline (Serilog, envelope, exception middleware, health, Swagger) | Done |
| 1.3 | Empty `AppDbContext`, SQL Server config, `InitialCreate` migration | Done |
| 1.4 | Expo + Expo Router scaffold, RTL, feature folders, theme stub, CI scripts | Done |
| 1.5 | Axios client stub in `services/api/` | Done |
| 1.6 | Empty xUnit test projects under `tests/` | Done |
| 1.7 | Build, test, health endpoint verification | Done |
| 1.8 | Update `project-progress.md` and `local-development.md` | Done |

---

## Phase 0 checklist

| Task | Description | Status |
|------|-------------|--------|
| 0.1 | Core documentation | Done (2026-08-24) |
| 0.2 | `.gitignore`, `README.md`, git `main`/`develop` | Done (2026-08-24) |
| 0.3 | CI skeleton (`ci.yml`, PR template) | Done (2026-08-24) |
| 0.4 | Figma link in `docs/figma/README.md` | Done (2026-08-24) |
| 0.5 | `project-progress.md` | Done |
| 0.6 | `local-development.md` | Done |
| 0.7 | Initial commit on `develop` | Done (2026-08-24) |

---

## Completed work log

### 2026-09-06 — Phase 2 gap fixes

- **Lockout:** Identity lockout enforced via `SignInManager`; failed attempts increment; locked accounts rejected
- **Refresh tests:** API integration coverage for expired, independently revoked, and reused refresh tokens
- **Mobile TanStack Query:** auth mutations wired through `useAuthQueries` hooks
- **Arabic errors:** structured API error envelope mapped to Arabic messages on Login/Register
- **Registration fields:** `fullName` + `username` in API contract and persistence
- **Migration:** `20260906120000_AddApplicationUserFullName` (additive `FullName` column on `AspNetUsers`)
- **Tests:** 19 passing (`dotnet test Insaaf.sln` — 7 application + 12 API integration)
- **Mobile:** typecheck + lint clean

### 2026-09-02 — Phase 2 authentication implemented

- **Backend:** ASP.NET Core Identity + JWT + refresh-token rotation; `AuthController` (`register`, `login`, `refresh`), `AuthMeController` (`GET /api/v1/auth/me`); `AuthException` handled in controllers and `ExceptionHandlingMiddleware`
- **Infrastructure:** `ApplicationUser`, `JwtTokenService`, `RefreshTokenService`, `IdentitySeed` (roles only; optional dev Admin via User Secrets)
- **Migration:** `20260902083744_AddIdentityAndRefreshTokens`
- **Mobile:** `services/auth/` (Secure Store refresh token, in-memory access token, Axios Bearer + single-flight refresh on 401); `AuthProvider` with startup refresh; Login/Register screens (Arabic RTL, react-hook-form + zod); Expo Router `(auth)` group

### 2026-08-26 — Phase 1 skeleton implemented

- `src/backend/Insaaf.sln` — Clean Architecture (Domain, Application, Infrastructure, API)
- API: `/api/v1/health`, Serilog, envelope types, exception middleware, Swagger at `/swagger`
- EF Core: empty `AppDbContext`, `InitialCreate` migration (no business entities)
- `src/mobile/` — Expo Router shell, RTL stub, feature folder stubs, theme/env config
- `services/api/` — Axios client + envelope types (no JWT interceptors)
- `tests/Insaaf.Application.Tests`, `tests/Insaaf.API.Tests` — empty xUnit projects for CI

### 2026-08-24 — Phase 0 complete

- Founder approved Master Architecture and Roadmap Plan
- Created core docs, CI, git branches, initial commit on `develop`

---

## Phase roadmap status

| Phase | Name | Status |
|-------|------|--------|
| 0 | Foundation | **Complete** |
| 1 | Skeleton | **Complete** |
| 2 | Authentication | **Complete** (founder gates pending) |
| 3 | Restaurants | Not started |
| 4 | Reviews read foundation | Not started |
| 5 | QR verified review write | Not started |
| 6 | Social core | Not started |
| 7 | Feed and following | Not started |
| 8 | Profiles | Not started |
| 9 | Admin and moderation | Not started |
| 10 | Hardening and beta | Not started |
| 11 | Production readiness | Not started |

---

## Notes

- Do **not** start Phase 3 without explicit founder approval.
- **Git:** Founder performs all state-changing Git operations; see [git-workflow.md](git-workflow.md).
- **EF:** Founder runs `dotnet ef database update` after reviewing migrations `AddIdentityAndRefreshTokens` and `AddApplicationUserFullName`.
- After each phase: summarize, report build/test status, stop for approval.
- Update this file when tasks complete.
