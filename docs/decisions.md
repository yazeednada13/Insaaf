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

## ADR-010: Founder-controlled Git

**Status:** Accepted  
**Date:** 2026-08-26

**Decision:** The **founder** executes all Git operations that change repository state (add, commit, push, pull, merge, branches, remotes, history). The **Lead Agent** guides via Git Checkpoints only.

**Read-only inspection allowed for agent:** `git status`, `git branch`, `git log`, `git remote -v`, `git diff`, `git diff --stat`.

**Workflow:** `feature/*` → `develop` → `main`. Conventional Commits. PRs created and merged by founder.

**Reference:** [git-workflow.md](git-workflow.md), [.cursor/rules/git.mdc](../.cursor/rules/git.mdc)

**Rationale:** Founder owns repository history and learns Git while building; agent remains orchestrator, not Git operator.

**Historical note:** Phase 0 initial commit was agent-executed under prior approval; this ADR applies going forward.

---

## ADR-011: Review photos — separate feature before beta

**Decision:** Review photos are not part of Phase 5. The "إضافة صور" button stays hidden, and photos are delivered as a separate feature with its own approved plan before beta. The founder approved photos for the MVP, in new **Phase 9A** (see [implementation-plan.md](implementation-plan.md)).

**Rationale:** Figma includes the button, but photos need storage, a data model and migration, an upload endpoint with validation, moderation of public content, a native image picker (a new EAS build) and display in reviews. That is its own slice of work.

**Consequence:** A QR code is single-use and reviews can't be edited, so reviews created before this feature ships will stay without photos.

---

## ADR-012: Social core — likes and comments (Phase 6)

**Decision:**
- Likes and comments are separate tables; counts are computed on read, with no stored counters.
- `ReviewLikes` has a composite primary key `(ReviewId, UserId)`, so a duplicate like is impossible. `PUT` and `DELETE` on the like are safe to repeat.
- Users may like and comment on their own reviews.
- Users delete only their own comments, permanently. Admin moderation of comments is Phase 9.
- Comments are 1–500 characters, flat (no replies), oldest first.
- A Review Detail screen hosts comments, per Figma, which adds `GET /api/v1/reviews/{id}`.

**Rationale:** social engagement must never touch ratings (`product-spec.md`), so nothing is written near rating data, and no new role or permission is needed.

---

## ADR-013: Following and feed (Phase 7)

**Decision:**
- `UserFollows` has a composite primary key `(FollowerId, FolloweeId)` and a CHECK against self-follow. `PUT` and `DELETE` on a follow are safe to repeat.
- Follow and unfollow live on Review Detail's reviewer row until profiles arrive in Phase 8.
- The feed lives in the النشاط tab's "المتابعة" chip. Notifications and the Home sections are out of scope.
- The feed returns verified public reviews of active restaurants by followed users, excluding the viewer's own, newest first, with cursor pagination.
- It reuses the existing review visibility query, with no second visibility rule.
- A `Reviews` index is added only with query plan evidence and founder approval.

**Rationale:** one visibility rule can't drift, and a cursor stays stable when new reviews arrive.

---

## Pending decisions

| Item | Default |
|------|---------|
| Figma file URL | Recorded in `docs/figma/README.md` (2026-08-24) |
