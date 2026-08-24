# Insaaf — Implementation Plan

**Version:** 1.0  
**Last updated:** 2026-08-24

Phases 0–11. Each phase has a **gate** and requires **founder approval** before the next phase begins.

**Rules:**

- Backend API slice before mobile screens per feature
- MVP reviews: Phase 4 = **read foundation**; Phase 5 = **verified write only**
- No application feature code in Phase 0

## Phase dependency graph

```text
Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7
                              Phase 2 → Phase 8
                              Phase 3,5 → Phase 9
                              Phase 7,8,9 → Phase 10 → Phase 11
```

---

## Phase 0 — Foundation

**Gate:** Founder approves architecture; Phase 0 tasks complete + initial commit

| Task | Deliverable |
|------|-------------|
| 0.1 | `docs/architecture.md`, `product-spec.md`, `implementation-plan.md`, `decisions.md` |
| 0.2 | `git init`, `main`/`develop`, `.gitignore`, `README.md` |
| 0.3 | `.github/workflows/ci.yml` skeleton |
| 0.4 | Figma link in `docs/figma/README.md` |
| 0.5 | `docs/project-progress.md` state tracking |
| 0.6 | `docs/local-development.md` |

**Out of scope:** `src/backend`, `src/mobile`, feature code

---

## Phase 1 — Skeleton

**Gate:** `dotnet build`; Expo app launches with Expo Router shell; Swagger loads

| Task | Deliverable |
|------|-------------|
| 1.1 | Clean Architecture solution (Domain, Application, Infrastructure, API) |
| 1.2 | Health endpoint, Serilog, exception middleware, API envelope |
| 1.3 | Empty `AppDbContext`, first migration, local connection |
| 1.4 | Expo app, Expo Router `app/` layout, feature folder stubs, RTL theme stub |
| 1.5 | Axios base client in `services/api` |

---

## Phase 2 — Authentication

**Gate:** Register → login → refresh E2E; Figma auth screens

Identity + JWT + refresh; mobile auth service + login/register screens.

---

## Phase 3 — Restaurants

**Gate:** Browse, search, filter, detail from app

`Restaurant` entity (generic location); Amman pilot seed; discovery APIs; mobile discovery screens. Detail may show empty reviews until Phase 4.

---

## Phase 4 — Reviews read foundation

**Gate:** Review list + aggregates on restaurant detail; **no review creation**

| Included | Excluded |
|----------|----------|
| `Review` entity (schema extensibility) | Create review use case |
| List + aggregate APIs | `POST` review endpoint |
| Mobile review list (read only) | Review form / write CTA |

---

## Phase 5 — QR verification + verified review creation

**Gate:** Scan QR → verified review; locked restaurant; double-redeem fails

**Sole MVP path for review creation.** VisitToken, redeem+create transaction, verified create API, QR scan + locked-restaurant form (Figma). Entry via QR only.

---

## Phase 6 — Social core

**Gate:** Like and comment on verified reviews

---

## Phase 7 — Feed and following

**Gate:** Following feed shows followed reviewers' content

---

## Phase 8 — Profiles

**Gate:** Public profile matches Figma

---

## Phase 9 — Admin and moderation

**Gate:** Ops can seed restaurants, issue tokens, moderate (no owner app)

---

## Phase 10 — Hardening and beta

**Gate:** Beta checklist; TestFlight/internal distribution ready

Integration tests, perf, UI audit vs Figma, security review.

---

## Phase 11 — Production readiness

**Gate:** Production deployed; monitoring; runbook

**Hosting selection** (Azure App Service + Azure SQL = current candidate). CI/CD for chosen provider. No cloud in Phases 0–10.

---

## Git workflow

- Branches: `main`, `develop`, `feature/*`
- Meaningful conventional commits
- PRs to `develop` even when solo

## Learning workflow

Per non-trivial task: document what, which layer, data flow, why, study topics in PR or `project-progress.md`.

## Related

- [architecture.md](architecture.md)
- [project-progress.md](project-progress.md)
- [decisions.md](decisions.md)
