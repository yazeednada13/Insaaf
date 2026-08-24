# Insaaf

Mobile application for trustworthy restaurant reviews and social food discovery — Arabic-first, verified reviews via QR invoice scan.

## Status

| Item | State |
|------|-------|
| Phase | **0 — Foundation** (in progress) |
| Application code | Not started (`src/` does not exist yet) |
| Architecture | Approved by founder (2026-08-24) |

## Stack

| Layer | Technology |
|-------|------------|
| Mobile | Expo, React Native, TypeScript, Expo Router, TanStack Query, Axios |
| Backend | ASP.NET Core 9, Clean Architecture, EF Core, SQL Server |
| Auth | ASP.NET Core Identity, JWT |
| UI | Figma (source of truth), RTL-first Arabic |

## MVP highlights

- **Verified reviews only** — QR → VisitToken → server-resolved restaurant
- Restaurant discovery (browse, search, filter)
- Social: likes, comments, follows, following feed
- Admin-seeded restaurants; Amman pilot seed data
- Roles: `User`, `Admin` (no `RestaurantOwner` in MVP)

## Documentation

| Document | Purpose |
|----------|---------|
| [docs/architecture.md](docs/architecture.md) | System architecture |
| [docs/product-spec.md](docs/product-spec.md) | MVP product scope |
| [docs/implementation-plan.md](docs/implementation-plan.md) | Phase roadmap |
| [docs/project-progress.md](docs/project-progress.md) | Current phase and tasks |
| [docs/decisions.md](docs/decisions.md) | Architecture decision records |
| [docs/local-development.md](docs/local-development.md) | Local dev setup |
| [docs/figma/README.md](docs/figma/README.md) | Figma design source |

## Git workflow

```text
main          — production-ready releases
develop       — integration (default working branch)
feature/*     — vertical slices (e.g. feature/authentication)
```

Use meaningful conventional commits. Open PRs to `develop` even when working solo.

## Local development

See [docs/local-development.md](docs/local-development.md).

**Prerequisites:** .NET 9 SDK, Node.js 22, Git, SQL Server (local or Docker — Phase 1+).

**No cloud infrastructure** in early MVP phases.

## Repository structure (target)

```text
docs/
src/backend/    # Phase 1+
src/mobile/     # Phase 1+
tests/          # Phase 1+
.github/workflows/
```

## License

Private — Insaaf project.
