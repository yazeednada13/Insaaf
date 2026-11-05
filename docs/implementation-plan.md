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
                              Phase 7,8 → Phase 8A
                              Phase 6,7,8,9 → Phase 9A
                              Phase 7,8,8A,9,9A → Phase 10 → Phase 11
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

| Task | Track | Scope |
|------|-------|-------|
| 6.1 | Backend | `ReviewLike`, `ReviewComment` entities, `AddSocialInteractions` migration |
| 6.2 | Backend | Like and unlike API; like counts and status on review reads |
| 6.3 | Backend | Review detail read; comment create, list and delete (own comments only) |
| 6.4 | Mobile | Like button; Review Detail screen; comments (Figma) |
| 6.5 | Both | Tests: likes and comments don't affect ratings; comment ownership |

Likes and comments never change ratings. Admin moderation of comments is Phase 9.

---

## Phase 7 — Feed and following

**Gate:** Following feed shows followed reviewers' verified content

| Task | Track | Scope |
|------|-------|-------|
| 7.1 | Backend | `UserFollow` entity, `AddUserFollows` migration |
| 7.2 | Backend | Follow and unfollow API; follow state on review detail |
| 7.3 | Backend | Cursor-paginated feed; query plan check |
| 7.4 | Mobile | Follow/Unfollow on Review Detail |
| 7.5 | Mobile | Feed in the النشاط tab (Figma) |
| 7.6 | Both | Tests: pagination, no N+1 |

The feed contains verified public reviews of active restaurants, by followed users, excluding the viewer's own. Profiles and people search are Phase 8.

---

## Phase 8 — Profiles

**Gate:** Public profile matches Figma; stats from verified reviews

| Task | Track | Scope |
|------|-------|-------|
| 8.1 | Backend | `Bio`, `AddUserBio` migration; profile read and edit; username through Identity; `POST /auth/logout` |
| 8.2 | Backend | User reviews, followers and following lists, people search, `hasPublicProfile` |
| 8.3 | Mobile | ملفي tab, other profiles, lists, Edit Profile, Settings with sign-out, people in Search |
| 8.4 | Both | Tests and report |

Admin accounts aren't public. Avatars, Save and Saved Reviews aren't in this phase.

---

## Phase 8A — Home, notifications and Taste Match

**Gate:** The الرئيسية tab shows the following section, "مقترح لك" and "رائج بالقرب منك", the النشاط tab shows in-app notifications, and profiles show Taste Match from real review history

**Why here:** Home reuses the feed (Phase 7) and links to people (Phase 8), and notifications point to reviews and profiles. Numbered 8A so existing phases and Task IDs stay unchanged. Added by founder decision (ADR-014).

| Task | Track | Scope |
|------|-------|-------|
| 8A.1 | Backend | Notification entity, created on like, comment and follow; list and mark-as-read API |
| 8A.2 | Backend | Suggested and trending restaurant queries |
| 8A.3 | Mobile | الرئيسية tab per Figma: greeting, search bar, following section, "مقترح لك", "رائج بالقرب منك" |
| 8A.4 | Mobile | "الإشعارات" chip in النشاط with an unread count |
| 8A.5 | Backend | Taste profile and Taste Match, computed from verified review history |
| 8A.6 | Mobile | "ذوقي في الطعام" on My Profile; "توافق في الذوق" and its sheet on other profiles (Figma) |
| 8A.7 | Both | Tests and report |

**Excluded:** push notifications (post-MVP default); restaurant official replies and their notification (not in the MVP).

**Decided at its planning gate:** how "مقترح لك" and "رائج بالقرب منك" rank restaurants, the Taste Match formula and its minimum review count, and how long notifications are kept.

---

## Phase 9 — Admin and moderation

**Gate:** Ops can seed restaurants, issue tokens, moderate (no owner app), including removing any user's comment

Admin deletion of other users' comments was moved here from Phase 6 by founder decision.

---

## Phase 9A — Review photos

**Gate:** A verified review can include photos, shown on review cards and Review Detail, and Admin can remove any photo

**Why here:** photos need moderation from day one (Phase 9), must appear on the feed and profile cards (Phases 7–8), and must exist before beta because a QR code is single-use and reviews can't be edited (ADR-011). Numbered 9A so existing phases and Task IDs stay unchanged.

| Task | Track | Scope |
|------|-------|-------|
| 9A.1 | Backend | `ReviewPhoto` entity, migration, storage interface |
| 9A.2 | Backend | Photo upload for a valid, unredeemed visit token; upload does not redeem the token |
| 9A.3 | Backend | Optional photo IDs on `POST /verification/reviews`, attached in the redeem transaction; photos on review reads; cleanup of unattached uploads |
| 9A.4 | Backend | Admin removal of a photo |
| 9A.5 | Mobile | "إضافة صور" in the review form: pick, preview, remove, upload (new EAS build) |
| 9A.6 | Mobile | Photos on `ReviewCard` (up to 3 with "+N") and Review Detail |
| 9A.7 | Both | Tests and report |

**Requirements:** photos are optional and added only during review creation; only the holder of a valid visit token can upload; each photo belongs to one review; photos never affect ratings; the server validates real file content; location metadata is removed; failed uploads don't consume the token.

**Proposed limits, confirmed at its planning gate:** up to 5 photos per review, JPEG/PNG/WebP, up to 5 MB each after client compression.

**Excluded:** restaurant photos, community photos, avatars, video, adding photos to existing reviews, full-screen gallery.

**Decided at its planning gate:** storage provider (production hosting is Phase 11, ADR-007) and the image-processing library.

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
