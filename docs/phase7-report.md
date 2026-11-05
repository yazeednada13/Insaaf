# Phase 7 — Feed and Following: consolidated report

**Date:** 2026-11-05
**Branch:** `feature/phase7-feed-following` (from `develop` after the Phase 6 merge; the founder manages Git)
**References:** the approved Master Plan (Phase 7), the approved Phase 7 plan (decisions D-1…D-9), [decisions.md](decisions.md) ADR-004/ADR-013, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`ActivityScreen.tsx`, `ProfileScreen.tsx`, `ReviewCard.tsx`)
**Report status:** ✅ **Backend Track complete** · ⏳ Mobile Track not started (waiting for `Phase 7 — Mobile`)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 7 lets a user follow reviewers whose taste they trust and read their verified reviews in one feed. The feed shows trusted content only: it reuses the existing review visibility rule, so it adds no second definition of "public".

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 7.1 | `UserFollow` + `AddUserFollows` migration | ✅ Verified | Build; migration review (additive only); the founder applied it; SQL Server smoke |
| 7.2 | Follow and unfollow API; follow state on review detail | ✅ Verified | Service + API tests; smoke: 5 parallel follows → all 200 |
| 7.3 | Cursor-paginated feed; query plan check | ✅ Verified | Service + API tests; smoke; one SQL command per feed request; plan inspected |
| 7.6 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 193/193 tests; smoke 16/16 PASS |

### Applied decisions (approved with the plan)
| # | Decision | Implementation |
|---|---|---|
| D-1 | Follow entry point | ✅ Backend ready: `isReviewerFollowedByMe` on review detail. The button is Mobile |
| D-2 | Task IDs | ✅ 7.1–7.6 preserved, with subtasks |
| D-4 | Feed content | ✅ Verified public reviews of active restaurants by followed users, excluding the viewer's own, newest first |
| D-6 | Follow API | ✅ Repeat-safe `PUT`/`DELETE` |
| D-7 | Rate limit | ✅ `social-follow`, 30/minute per user, from configuration |
| D-8 | Reviews index | ✅ No index added; evidence in §7 |

D-3, D-5 and D-9 apply to the Mobile Track.

### Deviations from the plan's wording (same contract, recorded honestly)
1. **No `IFeedRepository`:** the feed query is `ListFollowingFeedAsync` on `IReviewRepository`. It has to share the private visibility query there, and a separate repository would have needed a second copy of that rule.
2. **`UserExistsAsync`** sits on `IUserFollowRepository`, because Domain has no user repository and following is its only use so far.
3. **N+1 measurement:** the plan said EF command logging on the API. API logging doesn't show commands at the current level, so a temporary read-only probe outside the repository ran the real `ReviewRepository` on SQL Server with a command-counting interceptor.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, EF Core or ASP.NET Core in Domain and Application.

### Phase 7 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/UserFollow.cs` | The follow relationship |
| Domain | `Repositories/IUserFollowRepository.cs` | Follow data contract |
| Domain | `Repositories/FeedCursor.cs`, `Projections/FeedItemProjection.cs` | Feed paging and read model |
| Domain | `Repositories/IReviewRepository.cs`, `ReviewDetailProjection.cs` (modified) | `ListFollowingFeedAsync`; follow state |
| Application | `Social/UserFollowService.cs` | Follow rules |
| Application | `Social/FeedService.cs` | Limit validation, opaque cursor, next page |
| Application | `Social/Dtos/FeedDtos.cs`; `Reviews/*` (modified) | Contracts; `IsReviewerFollowedByMe` |
| Infrastructure | `Configurations/UserFollowConfiguration.cs` | Key, CHECK, FKs, index |
| Infrastructure | `Repositories/UserFollowRepository.cs` | Follow data access; duplicate handling |
| Infrastructure | `Repositories/ReviewRepository.cs` (modified) | The feed query |
| Infrastructure | `Migrations/20261105072156_AddUserFollows*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/UsersController.cs`, `FeedController.cs` | The new endpoints |
| API | `RateLimitingExtensions.cs`, `appsettings.json` (modified) | `social-follow` policy |

**Not modified:** verification, `VisitToken*`, and rating aggregates.

---

## 3. End-to-end data flow

### `PUT /api/v1/users/{id}/follow`
1. `[Authorize]`, then the `social-follow` rate limit.
2. `UsersController` takes the follower from the JWT, never from the request.
3. `UserFollowService`: following yourself → 400; unknown user → 404.
4. `UserFollowRepository.AddIfMissingAsync` checks, then inserts. If a concurrent request wins, the primary key rejects the insert (2601/2627), and that counts as already following.

### `GET /api/v1/feed?cursor=&limit=20`
1. `[Authorize]`; `FeedService` validates `limit` and decodes the cursor.
2. `ReviewRepository.ListFollowingFeedAsync` starts from `PublicReviewsOfActiveRestaurants()`, then keeps reviews whose author the viewer follows and removes the viewer's own.
3. With a cursor, it continues strictly after `(CreatedAt, Id)`.
4. One statement returns reviewer, restaurant, counts and like status. It asks for `limit + 1` rows.
5. If the extra row exists, `nextCursor` is built from the last returned item.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| Composite key `(FollowerId, FolloweeId)` | A duplicate follow can't exist |
| `CK_UserFollows_NotSelf` | The database refuses a self-follow, even if a bug reached it |
| `PublicReviewsOfActiveRestaurants` | The single visibility rule for detail, likes, comments and now the feed |
| `review.UserId != viewerUserId` | Keeps the viewer's own reviews out, per D-4 |
| `FeedService` cursor | Opaque to clients, so its format can change without touching the API |

---

## 5. API contract

All routes need `[Authorize]` and use the envelope `{ success, data, errors: [{code, message}], meta: { requestId } }`.

| Method | Route | Success |
|---|---|---|
| PUT | `/api/v1/users/{id}/follow` | 200 `{ userId, isFollowedByMe: true }` |
| DELETE | `/api/v1/users/{id}/follow` | 200 `{ userId, isFollowedByMe: false }` |
| GET | `/api/v1/feed?cursor=&limit=20` | 200 `{ items: [{ id, rating, text, createdAt, isVerified, reviewer, restaurant: { id, name, city, address }, likeCount, commentCount, isLikedByMe }], nextCursor }` |

- `nextCursor` is opaque, and `null` on the last page. `limit` is 1–50.
- **Feed content:** verified public reviews of active restaurants by users the caller follows, newest first. The caller's own reviews are excluded.

### Changes to existing contracts
`GET /api/v1/reviews/{id}` gains `isReviewerFollowedByMe` (additive). Review creation is unchanged.

### Errors
| Status | code | When |
|---|---|---|
| 400 | `validation_error` | Bad cursor or limit |
| 400 | `cannot_follow_self` | Following or unfollowing yourself |
| 401 | `unauthorized` | No token |
| 404 | `not_found` | Unknown user |
| 429 | `rate_limited` | Limit exceeded |

---

## 6. Database

### Migration `20261105072156_AddUserFollows` (additive only)
`UserFollows`: `FollowerId`, `FolloweeId`, `CreatedAt`. PK `(FollowerId, FolloweeId)`. CHECK `CK_UserFollows_NotSelf`. FKs to `AspNetUsers` (Restrict) on both. Index on `FolloweeId`. `Reviews` isn't changed.

### Business rules
- One follow per pair; following is one-directional.
- A user can't follow themselves.
- The feed never contains hidden reviews, reviews of inactive restaurants, or the viewer's own reviews.
- Unfollowing removes that user's reviews from the feed immediately.
- Following never affects ratings.

---

## 7. Verification

### Run (2026-11-05)
| Check | Result |
|---|---|
| Baseline before changes (Release) | ✅ 166/166 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 130/130 |
| `Insaaf.API.Tests` | ✅ 63/63 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only, `Down` correct |
| Migration applied | ✅ By the founder |
| **SQL Server smoke** | ✅ **16/16 PASS** |
| **N+1 check on SQL Server** | ✅ **1 SQL command** for a feed page of 2 items |
| Query plan (7.3c) | ✅ Inspected; no index needed now |

**First smoke run:** it failed with 500 on follow and feed calls, because the migration was still Pending. After the founder applied it, the same script passed 16/16. No code changed between the runs.

### The new tests (27)
21 Application cases and 6 API tests. They cover repeat-safe follows, self-follow, unknown users, feed content, own reviews, hidden reviews, inactive restaurants, unfollow, cursor walks, `CreatedAt` ties, a new review between pages, bad cursors and limits, and rate limits.

### SQL Server smoke results
- Feed empty before following; anonymous 401; self-follow 400; unknown user 404.
- **5 simultaneous follows → `200,200,200,200,200`**, with the review shown once.
- Feed items carry the restaurant and counts; review detail shows the follow state.
- A `limit=1` cursor walk returned every item with no repeats.
- Bad cursor and bad limit 400; following a user without reviews → empty feed.
- Unfollow is repeat-safe and empties the feed; restaurant ratings unchanged (`avg=4.0 count=2`).

### Query plan and index evidence (D-8)
- **Existing `Reviews` indexes:** `PK_Reviews`, `IX_Reviews_RestaurantId_IsVerified_CreatedAt`, `IX_Reviews_UserId`, `IX_Reviews_VisitTokenId`.
- **Actual plan:** Clustered Index Scan on `Reviews`; seeks on `Restaurants`, `UserFollows`, `AspNetUsers`, `ReviewLikes` and `ReviewComments`.
- **Reading it:** `Reviews` has 2 rows, so a scan is the normal choice and proves nothing about a large table.
- **Decision:** no index added. `IX_Reviews_UserId` exists for a larger table. Measure again with realistic data in Phase 10.
- **Note:** the plan's seek on `UserFollows` used `IX_UserFollows_FolloweeId`, with the small row counts.

### Not verified
- **Feed performance with large data:** the table has 2 rows.
- **Whether the duplicate-key path ran:** the parallel test proves the outcome, not which path handled it.
- **Rate limits on SQL Server:** API tests only.
- **Cursor ties on SQL Server:** tests only, on InMemory. `uniqueidentifier` sorts differently from .NET, but both the order and the comparison run in SQL Server, so they agree.

---

## 8. Mobile handoff
- **Follow button:** read `isReviewerFollowedByMe` from `GET /reviews/{id}`. Hide it on the user's own review, by comparing `reviewer.id` with the session user's id.
- **Feed paging:** send `nextCursor` back as `cursor`; stop when it's `null`.
- **Feed items** match the review list fields, plus `restaurant`.
- **After a follow change:** refresh the feed and the review detail.
- **Errors:** `validation_error`, `cannot_follow_self`, `not_found`, `rate_limited`, `unauthorized`.
- **Risks:** like and comment caches must include the feed; Review Detail needs a route inside the النشاط tab so Back returns to the feed. No EAS build is expected.

---

## 9. Known limitations (Backend)
- No followers or following lists or counts (Phase 8).
- No people search (Phase 8).
- No notifications when someone follows you.
- **Test data in the development database:** `smoke7-…@example.com` accounts and a few follows between them and the seeded reviewers.

---

## 10. Educational summary (Backend)
- **Why a cursor?** With page numbers, a new review pushes items down, so the next page repeats one. A cursor says "continue after this item", which stays correct as the data changes.
- **Why `(CreatedAt, Id)` and not time alone?** Two reviews can share a timestamp; the Id breaks the tie, so nothing is skipped.
- **Why `limit + 1`?** Fetching one extra row shows whether more exist, without a second count query.
- **What N+1 means:** one query for the list, then one more per row. Here one SQL statement returns everything, measured on SQL Server.
- **Why no index yet?** An index costs on every write. On 2 rows the plan can't justify one, so the decision waits for real data.

---

## 11. Mobile Track
⏳ **Not started.** Waiting for the founder's instruction: `Phase 7 — Mobile`.
