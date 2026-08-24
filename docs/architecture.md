# Insaaf — System Architecture

**Version:** 1.0  
**Last updated:** 2026-08-24  
**Status:** Approved by founder

## Overview

Insaaf is a **mobile-first** Arabic food discovery and **verified review** platform backed by a **modular monolith** ASP.NET Core API and SQL Server database.

```text
Expo (React Native + Expo Router)  →  Insaaf.API  →  Application  →  Domain
                                              ↓
                                    Infrastructure  →  SQL Server (local/Docker)
```

## Product pillars

1. **Verified reviews (MVP)** — QR invoice scan → `VisitToken` → review with server-resolved restaurant
2. **Social discovery** — likes, comments, follows, following-oriented feed
3. **Restaurant discovery** — browse, search, filter, detail, ratings, read reviews

## Founder-locked MVP policies

| Policy | Rule |
|--------|------|
| Review creation | **Verified only** — no unverified path, endpoint, or UX in MVP |
| Restaurant on review | Resolved **server-side** from `VisitToken`; client cannot override |
| Roles | `User`, `Admin` — no `RestaurantOwner` in MVP |
| Restaurant supply | Admin-seeded catalog + protected admin API |
| Pilot data | Amman, Jordan in seed scripts — **generic `City`/location on entity** |
| Hosting | Local/Docker until Phase 11; Azure is candidate, not MVP dependency |
| UI | Figma source of truth; **RTL-first** Arabic |

## Technology stack

### Mobile

- Expo managed workflow, React Native, TypeScript
- **Expo Router** (`src/mobile/app/`)
- TanStack Query (server state), Axios (HTTP)
- expo-secure-store, react-hook-form, zod
- No Redux/Zustand/MobX without approval

### Backend

- ASP.NET Core 9, C#, Clean Architecture
- SQL Server, Entity Framework Core, LINQ
- ASP.NET Core Identity, JWT (+ refresh)
- No CQRS, MediatR, microservices, generic `IRepository<T>` without approval

## Backend layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `Insaaf.Domain` | Entities, enums, domain exceptions, repository interfaces |
| Application | `Insaaf.Application` | Use cases, DTOs, validation, orchestration |
| Infrastructure | `Insaaf.Infrastructure` | EF Core, Identity, JWT, repositories, migrations |
| API | `Insaaf.API` | Controllers, middleware, auth, Swagger |

### Dependency direction

```text
API → Application → Domain
Infrastructure → Application, Domain
```

- Domain never depends on Infrastructure or API
- Controllers stay thin — no business logic

## Feature modules

| Module | Owns (MVP) | Must NOT own |
|--------|------------|--------------|
| Authentication | Register, login, refresh | Review content |
| Restaurants | Catalog, discovery, filters | Rating calculation |
| Reviews | Read, aggregates, list projections | QR logic; **no unverified create** |
| Verification | QR, tokens, redeem, **verified review create** | Review list UI |
| Social | Likes, comments, follows, feed | Verification, ratings |
| Profiles | Public reviewer stats | Credentials |
| Admin | Seed, tokens, moderate | — |

**Anti-corruption:**

- Likes/comments do **not** change star ratings
- `IsVerified = true` for all MVP-created reviews via Verification module only

## API conventions

- Base path: `/api/v1/`
- Envelope: `{ "success", "data", "errors", "meta" }`
- Global exception handling — no raw stack traces to clients
- **MVP review create:** single path via Verification (e.g. `POST` with `visitTokenId`) — **no** standalone unverified `POST /reviews`

## Mobile structure

```text
src/mobile/
  app/           # Expo Router
  features/      # auth, restaurants, reviews, social, profile
  components/    # shared UI only
  services/      # api (Axios), auth
  hooks/, utils/, constants/, types/, config/
```

QR → verified review UI lives under `features/reviews/`. No generic write-review screen in MVP.

## Database (overview)

- SQL Server + EF Core fluent configurations
- Core entities: User, Restaurant, Review, VisitToken, Comment, Like, Follow
- MVP review create requires `VisitToken`; transaction on redeem + create
- Migrations per vertical slice; projections for list endpoints

See [decisions.md](decisions.md) for ADRs.

## Development discipline

1. **Vertical slices** — backend API before mobile per feature
2. **Phase gates** — founder approval between phases
3. **Documentation** — update `project-progress.md` and ADRs on significant changes
4. **Definition of Done** — compile, tests, Figma alignment (UI), docs updated

## Repository layout (target)

```text
Insaaf/
  docs/
  src/backend/     # Phase 1+
  src/mobile/      # Phase 1+
  tests/           # Phase 1+
  .github/workflows/
```

## Related documents

- [product-spec.md](product-spec.md)
- [implementation-plan.md](implementation-plan.md)
- [project-progress.md](project-progress.md)
- [decisions.md](decisions.md)
- [local-development.md](local-development.md)
- [figma/README.md](figma/README.md)
- `.cursor/rules/` — Cursor agent rules (do not modify without approval)

## Agent orchestration

| Role | Responsibility |
|------|----------------|
| Lead Agent | Architecture, roadmap, consistency, ADRs |
| Backend Agent | Domain, EF, API, tests |
| Mobile Agent | Expo screens, Figma fidelity, API integration |
| QA / Review Agent | PR review before merge to `develop` |

All agents follow `.cursor/rules/` and this document.
