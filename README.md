# Insaaf

An Arabic, RTL-first mobile app for trustworthy restaurant reviews and social food discovery. A review can only be written after a visit is verified: QR invoice scan → `VisitToken` → server-resolved restaurant. The pilot uses Amman seed data; the data model is ready for other cities.

## Status

**Last updated:** 2026-11-07. Details: [docs/project-progress.md](docs/project-progress.md).

| Phase | Scope | State |
|-------|-------|-------|
| 0–1 | Foundation, skeleton | Complete |
| 2 | Authentication | Complete |
| 3 | Restaurants | Complete |
| 4 | Reviews read foundation | Complete |
| 5 | QR verification and verified review creation | Complete |
| 6 | Social core: likes, comments, follows | Complete |
| 7 | Feed and following | Complete |
| 8 | Profiles | Complete with an open item: device checklist not run |
| 8A | Home, notifications and Taste Match | Complete with an open item: device checklist not run |
| 9 | Admin and moderation | Backend complete (not merged); Admin is API-only |
| 9A | Review photos | Not started |
| 10 | Hardening and beta (including iOS testing) | Not started |
| 11 | Production readiness | Not started |

**Open items:** the Phase 8 and 8A device checklists have not been run; iOS is untested (planned for Phase 10).

## Stack

| Layer | Technology |
|-------|------------|
| Mobile | Expo SDK 57 (managed), React Native, TypeScript, Expo Router, TanStack Query, Axios |
| Backend | ASP.NET Core (.NET 9), Clean Architecture, EF Core, LINQ, SQL Server |
| Auth | ASP.NET Core Identity, JWT access tokens with rotating refresh tokens |
| UI | Figma (source of truth), Arabic RTL-first |

## Features built so far

- **Accounts:** register, login, token refresh, sign-out from all devices.
- **Restaurants:** browse, search, filter, restaurant details with distance computed on the device.
- **Verified reviews only:** scan a QR invoice, receive a single-use `VisitToken`, and write a review for the restaurant the server resolves. The client never chooses the restaurant.
- **Social:** likes, comments, follows, and a following feed.
- **Profiles:** My Profile, other users' profiles, followers and following lists, edit profile, settings, and people in search.
- **Home (8A):** greeting, search bar, reviews from people you follow, "مقترح لك" and "رائج بالقرب منك".
- **Notifications (8A):** in-app notifications for likes, comments and follows, with an unread count. Push notifications are not included.
- **Taste (8A):** "ذوقي في الطعام" and Taste Match, built from verified reviews (at least 3).
- **Roles:** `User` and `Admin` only. Admin accounts are not public profiles.

## Repository structure

```text
docs/                        # Architecture, plan, decisions, phase reports
src/backend/
  Insaaf.Domain/             # Entities, enums, repository interfaces
  Insaaf.Application/        # Services, DTOs, business rules
  Insaaf.Infrastructure/     # EF Core, migrations, repositories, Identity
  Insaaf.API/                # Controllers (/api/v1), auth, rate limiting
  Insaaf.sln
src/mobile/
  app/                       # Expo Router routes; (tabs): home, restaurants, scan, activity, profile
  features/                  # Screens, components and hooks per feature
  services/                  # Axios client and API modules
  config/ constants/ types/ utils/
tests/
  Insaaf.Application.Tests/
  Insaaf.API.Tests/
.github/                     # CI workflow, PR template
```

Dependency direction: `API → Application → Domain`, and `Infrastructure → Application/Domain`. No CQRS, MediatR or generic repositories.

## API overview

All routes are under `/api/v1` and return the `ApiResponse` envelope (`success`, `data`, `error`, `meta`).

| Area | Routes |
|------|--------|
| Health | `GET /health` |
| Auth | `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me` |
| Restaurants | `/restaurants`, `/restaurants/{id}`; Admin only: `/admin/restaurants/{id}` |
| Reviews | `/reviews/{id}` (read, likes, comments) |
| Verification | `/verification/...` (QR → `VisitToken` → verified review) |
| Feed | `/feed` |
| Users | `/users/...` (profiles, lists, search, `taste`, `taste-match`) |
| Home | `/home/suggested`, `/home/trending` |
| Notifications | `/notifications`, `/notifications/unread-count`, `/notifications/read-all` |
| Admin (Admin role) | `/admin/restaurants`, `/admin/reviews`, `/admin/comments/{id}`, `/admin/users`, plus Phase 5 partnership and visit tokens |

The full contracts are in the phase reports under `docs/`.

## Local development

Full setup: [docs/local-development.md](docs/local-development.md).

**Prerequisites:** .NET 9 SDK, Node.js 22, Git, and SQL Server (local or Docker). Keep connection strings and JWT secrets in User Secrets, never in the repository.

**Backend** (default URL `http://localhost:5232`):

```bash
cd src/backend
dotnet build Insaaf.sln
dotnet run --project Insaaf.API
```

**Database:** migrations are reviewed and applied by the founder only (`dotnet ef database update`).

**Tests:**

```bash
dotnet test src/backend/Insaaf.sln
```

**Mobile:**

```bash
cd src/mobile
npm install
npm start
```

For a physical device, set `EXPO_PUBLIC_API_BASE_URL` to the computer's LAN address, for example `http://192.168.1.20:5232`. Checks: `npm run typecheck` and `npm run lint`.

## Documentation

| Document | Purpose |
|----------|---------|
| [docs/architecture.md](docs/architecture.md) | System architecture |
| [docs/product-spec.md](docs/product-spec.md) | MVP product scope |
| [docs/implementation-plan.md](docs/implementation-plan.md) | Phase roadmap |
| [docs/project-progress.md](docs/project-progress.md) | Current phase and task status |
| [docs/decisions.md](docs/decisions.md) | Architecture decision records |
| [docs/local-development.md](docs/local-development.md) | Local setup |
| [docs/git-workflow.md](docs/git-workflow.md) | Founder-controlled Git workflow |
| [docs/figma/README.md](docs/figma/README.md) | Figma design source |
| `docs/phase3-report.md` … `docs/phase8a-report.md` | Per-phase reports: architecture, API, verification |

## Git workflow

```text
main          — production-ready releases
develop       — integration (default working branch)
feature/*     — one branch per phase or slice (e.g. feature/phase8a-home-notifications)
```

Flow: `feature/*` → PR to `develop` → `main`. The founder performs all Git operations. See [docs/git-workflow.md](docs/git-workflow.md) and ADR-010.

## License

Private — Insaaf project.
