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

## ADR-014: Home tab, in-app notifications and Taste Match in the MVP

**Decision:** The الرئيسية tab, with "مقترح لك" and "رائج بالقرب منك", in-app notifications, and Taste Match are part of the MVP, in new **Phase 8A**. Push notifications stay post-MVP. Restaurant official replies and their notification are not in the MVP (founder decision), so ADR-003 stays unchanged.

**Rationale:** both are in Figma but had no phase. They depend on the feed and on profiles, so they follow Phase 8. The 8A number keeps existing phases and Task IDs unchanged.

---

## ADR-015: Profiles (Phase 8)

**Decision:**
- Usernames change only through Identity's `UserManager`, so normalization and case-insensitive uniqueness stay Identity's. Sign-in stays by email.
- Profile stats are computed on read from verified public reviews.
- Admin accounts aren't public: their profile, lists and follow return 404, they're left out of search and lists, and their names in content stay visible but aren't navigable.
- Sign-out revokes all the user's refresh tokens, which signs out every device, and the app clears all account data from memory.
- Followers and following are page-based, ordered by `CreatedAt DESC, UserId DESC`.
- Avatars, Save and Saved Reviews are out of this phase. Save has no roadmap phase.

**Rationale:** Identity already solves username integrity, and hiding Admin accounts avoids exposing administrative information.

---

## ADR-016: Home, notifications and Taste Match (Phase 8A)

**Decision:**
- The app opens on الرئيسية. Its following section shows the first 2 feed items. "تقييمات حديثة" and the verified-visit sheet aren't built.
- "مقترح لك" recommends active restaurants in categories the user reviewed, not yet reviewed by them, ordered by rating, then review count; with no history, the top-rated.
- "رائج بالقرب منك" ranks active restaurants by public reviews in the last 30 days, then rating. Distance is shown on the device; no radius in the pilot.
- In-app notifications for likes, comments and follows. No notification for your own action; one like notification per actor per review; undoing the action removes it. Text is built on the device. Opening the chip marks all as read. The count refreshes when النشاط opens and on pull-to-refresh, shown only on the chip. Everything is kept for the pilot.
- Taste uses verified public reviews of active restaurants: a category's share sets its level (≥ 40% very high, ≥ 20% high, else medium). Taste Match compares average ratings per shared category, `100 − 25 × |difference|`, weighted by the smaller count. Both need at least 3 reviews.

**Rationale:** simple, explainable rules on the data that exists, adjustable after the pilot.

---

## ADR-017: Admin and moderation (Phase 9)

**Status:** Accepted
**Date:** 2026-11-07

**Decision:**
- Admin stays API-only. The role is checked only by `[Authorize(Roles = Admin)]` on the controllers, which pass the acting Admin's id from the JWT to Application services as `actorId`.
- Restaurants are never deleted, only deactivated. A full update replaces categories and opening hours as sets. `categorySlugs` and `openingHours` are required (explicit `[]` means unknown hours). Periods may run past midnight but may not overlap anywhere in the week. `opensAt == closesAt` is refused. Time zones must be IANA ids.
- Reviews are hidden, not deleted: `ReviewVisibility.IsPublic` is `IsVerified && HiddenAt == null`, so hidden reviews leave every public read and can't be liked or commented on. The visit token stays redeemed. Unhide restores everything.
- Admin deletes any comment permanently, with its notification (ADR-012).
- Suspension uses its own `SuspendedAt` column, never Identity's lockout. It blocks sign-in (after a correct password) and refresh with `account_suspended`, and revokes refresh tokens. Admin accounts can't be suspended. **An access token issued before suspension stays valid until it expires (15 minutes); there is no per-request check in the MVP.**
- Admin actions are recorded as structured log lines through `IAdminAuditLog`: action, Admin id, target type and id, timestamp, result. No text, reasons, emails, names, phone numbers or tokens. No audit table.

**Rationale:** reversible moderation keeps the verified-review guarantee intact; one visibility rule means one change; separating suspension from lockout keeps password-guessing protection intact.

---

## ADR-018: Review photos (Phase 9A)

**Status:** Accepted
**Date:** 2026-11-08

**Decision:**
- Photos are uploaded with a valid, unredeemed visit token before the review is submitted, then attached by id inside the review's redeem transaction. Uploads never redeem the token.
- The first upload claims the token for the uploader. While they have a live photo, nobody else can upload or redeem with it. Tokens without photos keep Phase 5 behaviour.
- Up to 5 photos per review. JPEG, PNG or WebP up to 5 MB are decoded and re-encoded with SkiaSharp as JPEG (q85, longest side 2048 px). No metadata survives, GPS included.
- Files are stored on local disk behind `IPhotoStorage`, outside the repository (the API refuses a root inside its content root). Cloud storage comes in Phase 11.
- **Concurrency:** the 5-photo limit and the claim are enforced by a conditional UPDATE on the token row. Every multi-statement transaction locks the token first, then photos. Deadlocks are retried whole.
- **Files:**
  - Uploads are idempotent per `clientUploadId`.
  - Each processing attempt writes its own file, and only the lease holder's conditional UPDATE (the fence) chooses the served file.
  - Rows track their folder until it's confirmed empty, then stay as `Purged` tombstones for 30 days.
  - No foreign key on `ReviewPhotos` cascades.
- **Serving:** images are served anonymously, only for Ready photos of public reviews, with `private, no-cache` and an `ETag`. The app's correctness relies on data refresh, not on HTTP caching.
- **Removal:** reports `fileDeletion: completed | pending` honestly, and the Admin audit records `succeeded` or `removed_file_pending`.

**Rationale:** photos must ship before beta because tokens are single-use. Every rule above keeps the verified-review guarantee, prevents lost or leaked files, and keeps moderation effective on every request.

---

## ADR-019: Roadmap — local hardening now, release later

**Status:** Accepted
**Date:** 2026-11-09

**Decision:** Phases 10 and 11 are re-cut by what can be done without hosting or an iPhone.
- **Phase 10 — Hardening (local):** security review, dependency checks, the deferred decisions, SQL Server integration tests, performance and Figma reviews on Android, CI without deployment, preparation for hosting, test-data cleanup.
- **Phase 11 — Release (deferred):** hosting, deployment, cloud storage, monitoring, iOS testing, TestFlight and internal distribution, and the external beta. It starts only when hosting and an iPhone are available.

**Rationale:** the founder has no hosting and no iPhone for now. This keeps every remaining task doable today, and leaves only work that truly needs those resources.

**Consequence:** the original Phase 10 gate (TestFlight, internal distribution) moves to Phase 11. iOS stays untested until Phase 11.

---

## ADR-020: Hardening decisions (Phase 10)

**Status:** Accepted
**Date:** 2026-11-09

**Decision:**
- **Suspension (D-3):** a suspended account keeps its current access token until it expires (at most 15 minutes). Sign-in and refresh are refused at once. There is no per-request database check.
- **Admin audit (D-4):** structured log lines stay the audit record. An audit table is reconsidered with hosting (Phase 11), when log retention is chosen.
- **Token lifetimes (D-5):** access token 15 minutes, refresh token 7 days, with rotation and reuse detection.
- **Auth rate limits (D-10):** per IP, per minute: login 10, register 5, refresh 30. Behind a proxy, `ReverseProxy:KnownProxies` must be set, or every user shares the proxy's IP.
- **Signing key:** outside Development the API refuses to start with a key under 32 bytes or the committed development key.
- **Card thumbnails (D-7):** each upload also stores a JPEG of at most 640 px next to the photo (`{leaseId}.thumb.jpg`), with no migration. Cards request `?size=thumb`; Review Detail keeps the full image. Photos uploaded before Phase 10 fall back to the full image.
- **Integration tests (D-6):** SQL Server tests run against a separate database whose name must contain `IntegrationTests`. They never migrate, and they are skipped without a connection string (as in CI until Phase 11).

**Rationale:** these keep the pilot simple and safe. They close the gaps found by the Phase 10 review without adding infrastructure that only makes sense once hosting exists.

---

## Pending decisions

| Item | Default |
|------|---------|
| Figma file URL | Recorded in `docs/figma/README.md` (2026-08-24) |
