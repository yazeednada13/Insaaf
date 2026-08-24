# Insaaf — Architecture Decision Records (ADRs)

Significant technical decisions. Read before making architectural changes. Update when new decisions are approved.

---

## ADR-001: Mobile platform — Expo managed workflow

**Status:** Accepted  
**Date:** 2026-08-23

**Decision:** React Native with **Expo (managed workflow)** and TypeScript.

**Rationale:** Faster MVP delivery, built-in camera/QR support, OTA updates for beta.

**Alternatives considered:** Bare React Native CLI (deferred).

---

## ADR-002: Navigation — Expo Router

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** **Expo Router** for file-based navigation under `src/mobile/app/`.

**Rationale:** Aligns with Expo ecosystem; modern RN routing standard.

**Alternatives considered:** React Navigation standalone (rejected for MVP).

---

## ADR-003: Restaurant onboarding — admin-seeded MVP

**Status:** Accepted  
**Date:** 2026-08-23

**Decision:** Restaurants are **admin-seeded** in MVP. No owner self-service app. Roles: `User` and `Admin` only — **no `RestaurantOwner`** in MVP.

**Rationale:** Reduces MVP scope; ops can run closed beta without owner dashboard.

---

## ADR-004: Verified reviews only — MVP

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** **All reviews created in MVP** must flow through QR invoice → `VisitToken` → server-resolved restaurant → verified review creation.

**Rules:**

- No standalone review creation without verification.
- No `POST /reviews` without valid `VisitToken`.
- No generic "Write Review" UX on restaurant detail in MVP.
- Client cannot select or override restaurant on verified review.

**Future extension:** Unverified reviews require explicit founder approval + new ADR. Schema may retain `IsVerified` / `VisitTokenId` for extensibility.

---

## ADR-005: Server state and HTTP — TanStack Query + Axios

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** TanStack Query for server state; Axios with interceptors for HTTP.

**Rationale:** Caching and loading/error for server data without global state libraries.

---

## ADR-006: Pilot geography — Amman seed data only

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** Initial pilot dataset is **Amman, Jordan**. `Restaurant` uses **generic location fields** (`City`, coordinates). Amman is **seed data only** — not hard-coded in domain or architecture.

---

## ADR-007: Production hosting — deferred to Phase 11

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** Production hosting selected in **Phase 11 — Production readiness**. **Azure App Service + Azure SQL** is the current **candidate**, not an MVP architectural commitment.

**Local development:** SQL Server local instance or Docker. **No cloud infrastructure in Phases 0–10** unless explicitly approved.

---

## ADR-008: UI layout — Arabic RTL-first

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** RTL-first implementation for Arabic product. Figma is visual source of truth.

---

## ADR-009: Architecture style — Clean Architecture modular monolith

**Status:** Accepted  
**Date:** 2026-08-24

**Decision:** ASP.NET Core Clean Architecture modular monolith. No CQRS, MediatR, microservices, event sourcing, or generic `IRepository<T>` unless explicitly justified and approved.

---

## Pending decisions

| Item | Default |
|------|---------|
| Media uploads in reviews | Defer post-MVP unless Figma MVP screens require |
| Figma file URL | Recorded in `docs/figma/README.md` (2026-08-24) |
