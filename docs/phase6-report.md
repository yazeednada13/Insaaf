# Phase 6 — Social Core: consolidated report

**Date:** 2026-11-05
**Branch:** `feature/phase6-social-core` (from `develop` after the Phase 5 merge; the founder manages Git)
**References:** the approved Master Plan (Phase 6), the approved Phase 6 plan (decisions D-1…D-14), [decisions.md](decisions.md) ADR-004/ADR-011/ADR-012, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`ReviewCard.tsx`, `ReviewDetailScreen.tsx`)
**Report status:** ✅ **Backend Track complete** · ⏳ Mobile Track not started (waiting for `Phase 6 — Mobile`)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 6 adds the first social layer on verified reviews: users can like a review and comment on it. Engagement must never touch ratings, so likes and comments live in their own tables and no rating query reads them. Review creation stays verified-only, and the verification flow wasn't changed.

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 6.1 | `ReviewLike` + `ReviewComment` + `AddSocialInteractions` migration | ✅ Verified | Build; migration review (additive only); the founder applied it (`migrations list` shows no Pending); SQL Server smoke |
| 6.2 | Like and unlike API; counts and status on reads | ✅ Verified | Service + API tests; smoke: 5 parallel likes → all 200, one like counted |
| 6.3 | Review detail; comment create, list, delete | ✅ Verified | Service + API tests; smoke: owner 200, non-owner 403, repeat 404 |
| 6.5 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 166/166 tests; smoke 18/18 PASS |

### Applied decisions (approved with the plan)
| # | Decision | Implementation |
|---|---|---|
| D-1 | Task IDs | ✅ 6.1–6.5 preserved, with subtasks |
| D-2 | Comments live on Review Detail | ✅ `GET /api/v1/reviews/{id}` added for it |
| D-3 | Repeat-safe likes | ✅ `PUT`/`DELETE` |
| D-4 | Comment deletion | ✅ Author only, permanent. Admin moderation moved to Phase 9 by founder decision |
| D-5 | Comment length | ✅ 1–500 characters |
| D-6 | Comment order | ✅ Oldest first |
| D-9 | Own content | ✅ Users may like and comment on their own reviews (founder decision) |
| D-10 | Rate limits | ✅ `social-like` 60/minute, `social-comment` 10/minute per user, from configuration |
| D-11 | Counts | ✅ Computed on read, no stored counters |
| D-14 | Review photos | ✅ New Phase 9A in the roadmap; no photo work in Phase 6 |

D-7, D-8 and D-12 apply to the Mobile Track. D-13 (Phase 5 task 5.9) remains open.

### Deviations from the plan's wording (same contract, recorded honestly)
1. **Migration timing:** the plan placed the migration stop before 6.2. The founder generated it there, but tasks 6.2 and 6.3 were written while it was being applied, because they don't depend on the database until the smoke test. Nothing that needed the applied schema ran before the founder confirmed it.
2. **Comment deletion rate limit:** `DELETE` on a comment uses the `social-like` policy, not `social-comment`, so removing comments isn't limited to 10 per minute. The plan only specified limits for likes and comment creation.
3. **`ListForRestaurantAsync`** gained a `viewerUserId` parameter, which changed 3 calls in `ReviewServiceTests`. That test's logic didn't change.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, EF Core or ASP.NET Core in Domain and Application. Comment ownership is checked in Application and uses no role.

### Phase 6 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/ReviewLike.cs`, `Entities/ReviewComment.cs` | The new entities. `Review` is unchanged |
| Domain | `Repositories/IReviewLikeRepository.cs`, `IReviewCommentRepository.cs` | Explicit repository contracts |
| Domain | `Repositories/IReviewRepository.cs` (modified) | `GetPublicDetailAsync`, `IsPublicAsync` |
| Domain | `Repositories/Projections/ReviewDetailProjection.cs`, `ReviewCommentProjection.cs`; `ReviewListItemProjection.cs`, `ReviewSearchCriteria.cs` (modified) | Read models |
| Application | `Social/ReviewLikeService.cs` | Like and unlike |
| Application | `Social/ReviewCommentService.cs` | List, create, delete; **the ownership check** |
| Application | `Social/Validation/ReviewCommentValidator.cs` | Text 1–500, paging |
| Application | `Social/SocialException.cs`, `Social/Dtos/SocialDtos.cs` | Errors and contracts |
| Application | `Reviews/ReviewService.cs`, `IReviewService.cs`, `Dtos/ReviewDtos.cs` (modified) | Detail read; social fields on the list |
| Infrastructure | `Configurations/ReviewLikeConfiguration.cs`, `ReviewCommentConfiguration.cs` | Keys, CHECK, FKs, indexes |
| Infrastructure | `Repositories/ReviewLikeRepository.cs`, `ReviewCommentRepository.cs` | Data access; duplicate-like handling |
| Infrastructure | `Repositories/ReviewRepository.cs` (modified) | Counts and like status in the projections |
| Infrastructure | `Migrations/20261105061845_AddSocialInteractions*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/ReviewsController.cs` | The new endpoints; it has no create-review action |
| API | `RestaurantsController.cs`, `RateLimitingExtensions.cs`, `ExceptionHandlingMiddleware.cs`, `appsettings.json` (modified) | Viewer id, policies, `SocialException`, settings |

**Not modified:** `VisitToken*`, `VerificationService`, `VerificationController`, `VisitTokenRules`, and the aggregate queries in `RestaurantRepository`.

---

## 3. End-to-end data flow

### `PUT /api/v1/reviews/{id}/like`
1. `[Authorize]`, then the `social-like` rate limit.
2. `ReviewsController` reads the user id from the JWT, never from the request.
3. `ReviewLikeService.LikeAsync` → `IReviewRepository.IsPublicAsync`: passes `ReviewVisibility.IsPublic` and the restaurant is active, otherwise 404.
4. `ReviewLikeRepository.AddIfMissingAsync`: checks for the row, then inserts. If a concurrent request wins, the composite primary key rejects the insert (SQL error 2601/2627) and that's treated as already liked.
5. The service returns the fresh count from `CountAsync`.

### `POST /api/v1/reviews/{id}/comments`
1. `[Authorize]`, then the `social-comment` rate limit. Extra body fields → 400 (`JsonUnmappedMemberHandling.Disallow`).
2. `ReviewCommentService.CreateAsync` validates the trimmed text, then checks the review is public.
3. The comment is saved, then read back with its author through a join.
4. 201 with the comment; `isMine` is true.

### `DELETE /api/v1/reviews/{id}/comments/{commentId}`
1. The review must be public.
2. The comment must exist **under that review**, otherwise 404.
3. `AuthorId != userId` → 403 `comment_forbidden`. There's no role exception.
4. A permanent delete, then the new `commentCount` is returned.

### `GET /api/v1/restaurants/{id}/reviews`
The existing query now adds, in one SQL statement per page, `COUNT` subqueries for likes and comments and an `EXISTS` for the viewer's like.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| Composite key `(ReviewId, UserId)` | A duplicate like can't exist, whatever the application does |
| `AddIfMissingAsync` | Turns the race on that key into a harmless no-op, so a retry or double tap never gives an error |
| `IsPublicAsync` | One rule for "can this review be interacted with": a hidden review, or one under an inactive restaurant, behaves as not found |
| `ReviewCommentService.DeleteAsync` | The only place that decides comment ownership |
| `WithAuthor` in `ReviewCommentRepository` | One join for list and read-back, so author data has one shape |

---

## 5. API contract

All routes need `[Authorize]` (User or Admin) and use the envelope `{ success, data, errors: [{code, message}], meta: { requestId } }`.

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/v1/reviews/{id}` | — | 200 `{ id, rating, foodRating, serviceRating, valueRating, atmosphereRating, text, createdAt, isVerified, reviewer, restaurant: { id, name, city, address }, likeCount, commentCount, isLikedByMe }` |
| PUT | `/api/v1/reviews/{id}/like` | — | 200 `{ reviewId, likeCount, isLikedByMe: true }` |
| DELETE | `/api/v1/reviews/{id}/like` | — | 200 `{ reviewId, likeCount, isLikedByMe: false }` |
| GET | `/api/v1/reviews/{id}/comments?page=1&pageSize=20` | — | 200 `{ items: [{ id, text, createdAt, author: { id, fullName, userName }, isMine }], page, pageSize, totalCount }` |
| POST | `/api/v1/reviews/{id}/comments` | `{ "text": "…" }` | 201 the comment item |
| DELETE | `/api/v1/reviews/{id}/comments/{commentId}` | — | 200 `{ reviewId, commentCount }` |

### Changes to existing contracts
Each item in `GET /api/v1/restaurants/{id}/reviews` gains `likeCount`, `commentCount` and `isLikedByMe` (additive).

`POST /api/v1/reviews` is still 404, and `POST /api/v1/restaurants/{id}/reviews` is still 405. `POST /api/v1/reviews/{id}` is 405.

### Errors
| Status | code | When |
|---|---|---|
| 400 | `validation_error` | Empty text, text > 500, bad paging, an extra body field |
| 401 | `unauthorized` | No token |
| 403 | `comment_forbidden` | Deleting someone else's comment, including as Admin or as the review's author |
| 404 | `not_found` | Unknown or hidden review; unknown comment, or a comment under a different review |
| 429 | `rate_limited` | Limit exceeded |

---

## 6. Database

### Migration `20261105061845_AddSocialInteractions` (additive only)
- **`ReviewLikes`:** `ReviewId`, `UserId`, `CreatedAt`. PK `(ReviewId, UserId)`. FKs: `Reviews` (Cascade), `AspNetUsers` (Restrict). Index on `UserId`.
- **`ReviewComments`:** `Id`, `ReviewId`, `UserId`, `Text nvarchar(500)`, `CreatedAt`. CHECK `CK_ReviewComments_TextNotBlank` `LEN([Text]) > 0`. FKs: `Reviews` (Cascade), `AspNetUsers` (Restrict). Indexes `(ReviewId, CreatedAt)` and `UserId`.
- **No changes** to `Reviews`, `Restaurants` or `VisitTokens`.

### Business rules
- A social action needs a public review of an active restaurant.
- One like per user per review.
- Comments are flat, 1–500 characters after trimming, oldest first.
- Only the author can delete a comment, and the delete is permanent.
- Users may like and comment on their own reviews.
- Likes and comments never change ratings or aggregates.
- Ending a restaurant's partnership doesn't hide its reviews, so social actions stay available; only an inactive restaurant blocks them.

---

## 7. Verification

### Run (2026-11-05)
| Check | Result |
|---|---|
| Baseline before changes (Release) | ✅ 130/130 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 109/109 |
| `Insaaf.API.Tests` | ✅ 57/57 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only, `Down` correct |
| Migration applied | ✅ By the founder; `migrations list` shows no Pending |
| **SQL Server smoke** (real API on 5232, new test accounts) | ✅ **18/18 PASS** |

### The new tests (36)
26 Application cases and 10 API tests. They cover duplicate likes, per-user status, text boundaries, ownership (including Admin and the review's author), hidden reviews, paging, rate limits, no new create path, and rating isolation at both service and API level.

### SQL Server smoke results
- The review list includes the social fields; review detail 200 with sub-ratings; unknown review 404; anonymous like 401.
- **5 simultaneous likes by one user → `200,200,200,200,200`, `likeCount` = 1.**
- A second user's like → 2; status is per user; unlike repeated twice → 200, count 1.
- Comment 201, trimmed, `isMine`; blank 400; extra field 400; listed for another user with `isMine` false.
- Non-owner delete 403 `comment_forbidden`; owner delete 200; repeat 404.
- `POST /reviews` still 404.
- Restaurant ratings unchanged (`avg=4.0 count=2`); like count back to its start value.

### Not verified
- **Whether the duplicate-key path ran:** the parallel test proves the outcome, but it doesn't show whether requests reached the 2601/2627 handler or were stopped by the earlier check.
- **Rate limits on SQL Server:** API tests only.
- **CHECK constraint present in the database:** confirmed from the migration file, not by querying `sys.check_constraints`.
- **List query performance with large data:** not measured; the pilot has little data.

---

## 8. Mobile handoff
- **Base URL and auth:** as in earlier phases, with Bearer through the existing Axios interceptor.
- **Like:** use the response's `likeCount` and `isLikedByMe` as the final state; both verbs are safe to repeat.
- **Lists:** `isLikedByMe`, `likeCount`, `commentCount` are on each review item.
- **Delete:** show it only when `isMine` is true. Handle 403 and 404 anyway.
- **After a comment change:** refresh the comment list, the review detail and the restaurant's review list.
- **Paging:** `pageSize` is 1–50; comments are oldest first.
- **Errors:** `validation_error`, `not_found`, `comment_forbidden`, `rate_limited`, `unauthorized`.
- **Risks:** no new native library is expected, so no EAS build; the comment input needs the Phase 5 keyboard approach.

---

## 9. Known limitations (Backend)
- Admin can't remove an offensive comment until Phase 9 (founder decision).
- Comments can't be edited.
- There are no comment likes, replies or notifications.
- `ReviewLikes` has no public list of who liked a review.
- **Smoke side effects in the development database:** two `smoke6-…@example.com` accounts. Their likes and test comment were removed.

---

## 10. Educational summary (Backend)
- **Why a composite primary key for likes?** The rule "one like per user per review" lives in the database itself. Code can check first, but two simultaneous requests can both pass that check; the key stops the second one.
- **Why PUT and DELETE instead of a toggle?** A toggle depends on the current state, so a retry can undo the action. `PUT` means "this should be liked", which stays true however many times it's sent.
- **Why no stored like counter?** A stored number can drift from reality. Counting from the rows is always right, and it keeps social writes away from the `Reviews` table and its ratings.
- **Why check ownership in Application?** The rule belongs to the business, not to HTTP. Any future caller gets the same rule.
- **Why reuse `ReviewVisibility`?** It's the single definition of a public review, so a hidden review can't be liked through a side door.

---

## 11. Mobile Track
⏳ **Not started.** Waiting for the founder's instruction: `Phase 6 — Mobile`.
