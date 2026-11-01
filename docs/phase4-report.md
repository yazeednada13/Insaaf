# Phase 4 — Reviews Read Foundation: consolidated report

**Date:** 2026-11-01
**Branch:** `feature/phase4-reviews-read` (from `develop` @ `daa4489`; the founder manages Git)
**References:** the approved Master Plan (Phase 4), the approved Phase 4 plan (decisions D1–D10), [decisions.md](decisions.md) ADR-004/ADR-009, Figma Make `DfKJchmPebiHH9dRYaYMuS`
**Report status:** ✅ **Backend Track complete** · ⏳ Mobile Track hasn't started yet (it starts on the founder's explicit request)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 4 lays the read-only foundation for reviews. A restaurant's detail shows its verified reviews and their aggregates, and `averageRating` / `reviewCount` / the `minRating` filter now work from real data instead of `null` / `0` / an empty list. **There is no review creation in this Phase:** no POST, no form, no QR. Creation is in Phase 5, through VisitToken only (ADR-004).

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 4.1 | The `Review` entity + config + `AddReviews` migration | ✅ Verified | Build; migration review (additive only, Designer = snapshot); the founder applied it; queries against SQL Server read the table successfully |
| 4.2 | `IReviewRepository` + projections + aggregates | ✅ Verified | 7 repository tests (InMemory) + SQL Server smoke |
| 4.3 | Read-only reviews list endpoint | ✅ Verified | Service + API tests + SQL Server smoke (200/400/401/404/405) |
| 4.4 | Aggregates on restaurant detail | ✅ Verified | Rounding tests + API (`averageRating`, `ratingBreakdown`, `minRating`) |
| 4.5 | Mobile review list | ⏳ Mobile Track | — |
| 4.6 | Empty state | ⏳ Mobile Track | — |
| 4.7 | Tests + verification + report | 🟡 Backend part complete | 95/95 Release; the Mobile part pending |

### Applied decisions (approved with the plan)
| # | Decision | Implementation |
|---|---|---|
| D1 | `VisitTokenId` nullable with no FK | ✅ `uniqueidentifier NULL`; the FK + unique index come in Phase 5 |
| D2 | Data integrity + public results | ✅ `IsVerified` default 0 + CHECK `IsVerified = 0 OR VisitTokenId IS NOT NULL` + `ReviewVisibility.IsPublic` as the single source |
| D3 | Detailed sub-ratings 1–5 required | ✅ 4 columns + CHECK on each |
| D4 | Aggregates in the existing detail | ✅ `ratingBreakdown` in `GET /restaurants/{id}` |
| D8 | No reviews in the real seed | ✅ The seed wasn't touched; review data exists only in the tests |
| D10 | Text 2000 | ✅ `nvarchar(2000)` |
| D5, D6, D7, D9 | Mobile decisions | ⏳ Mobile Track |

### Deviations from the plan's wording (same contract, recorded honestly)
1. **The aggregates** are computed inside the restaurant detail query (`RestaurantRepository.GetByIdAsync`) instead of a separate `GetRatingSummaryAsync` method on `IReviewRepository`. **Why:** one query to the database instead of two, and it serves 4.4 directly.
2. **The errors** reuse `RestaurantException` (`not_found`/`validation_error`) instead of a new `ReviewException`. **Why:** reviews are a sub-resource of the restaurant with the same codes, and the existing handling in the controller and middleware covers them without duplication.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified:** 0 references to EF/ASP.NET/Infrastructure in Domain and Application; `Insaaf.Domain.csproj` has 0 packages.

### Phase 4 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Review.cs` | The review (UserId with no navigation to the Identity user) |
| Domain | `Entities/Restaurant.cs` | + `ICollection<Review> Reviews` |
| Domain | `Rules/ReviewVisibility.cs` | **The single source** for the public-review rule (`IsVerified`) as a LINQ expression |
| Domain | `Repositories/IReviewRepository.cs`, `ReviewSearchCriteria.cs` (+ `ReviewSort`) | The repository contract |
| Domain | `Repositories/Projections/ReviewListItemProjection.cs`, `RatingBreakdownProjection.cs` | Read models |
| Domain | `Repositories/IRestaurantRepository.cs` | + `IsActiveAsync` |
| Application | `Reviews/ReviewService.cs`, `IReviewService.cs` | Validation → restaurant exists → query → DTOs (UTC) |
| Application | `Reviews/Validation/ReviewQueryValidator.cs` | sort/page/pageSize |
| Application | `Reviews/RatingMath.cs` | One-decimal rounding (half away from zero) |
| Application | `Reviews/Dtos/ReviewDtos.cs` | The list contract |
| Application | `Restaurants/RestaurantService.cs`, `Dtos/RestaurantDtos.cs` | Rounding the aggregates + `RatingBreakdownDto` |
| Infrastructure | `Persistence/Configurations/ReviewConfiguration.cs` | CHECK, FKs, indexes |
| Infrastructure | `Persistence/Repositories/ReviewRepository.cs` | List + sorting + paging + reviewer join |
| Infrastructure | `Persistence/Repositories/RestaurantRepository.cs` | Real aggregates + `minRating` + `IsActiveAsync` |
| Infrastructure | `Migrations/20261101001634_AddReviews` | The `Reviews` table |
| API | `Controllers/RestaurantsController.cs` | + `GET {id}/reviews` |

---

## 3. End-to-end data flow

### `GET /api/v1/restaurants/{id}/reviews?sort=highest&page=1`
```text
JwtBearer (401 envelope) → model binding (400 envelope) → RestaurantsController.GetReviews
  → Guid.TryParse (404 if invalid)
  → ReviewService.ListForRestaurantAsync
      ├─ ReviewQueryValidator (400 validation_error)
      ├─ IRestaurantRepository.IsActiveAsync (404 not_found if unknown/inactive)
      └─ IReviewRepository.ListByRestaurantAsync
           Reviews.AsNoTracking().Where(ReviewVisibility.IsPublic).Where(RestaurantId)
           → COUNT
           → ORDER BY Rating DESC, CreatedAt DESC, Id → OFFSET/FETCH
           → JOIN AspNetUsers (reviewer in the same query)
  → DTO: CreatedAt marked UTC → 200 { items, page, pageSize, totalCount }
```

### `GET /api/v1/restaurants/{id}` (aggregates)
A single SQL statement contains correlated subqueries:
- `AVG(CAST(Rating AS decimal))` and `COUNT(*)` over public reviews.
- `EXISTS`, plus 4 `AVG` for the sub-ratings.

The service rounds them to one decimal. **There's no N+1:** the number of queries doesn't grow with the number of restaurants or reviews.

---

## 4. Important code

- **`ReviewVisibility.IsPublic`:** the most important protection in this Phase. A static expression `review => review.IsVerified` in Domain. Every read query uses it (the list, the count, the average, the breakdown, and `minRating`). If unverified reviews are ever allowed (it needs an ADR), the change happens in one place, and they can't leak through a query that forgot the filter.
- **`.AsQueryable().Where(ReviewVisibility.IsPublic)` inside the projection:** that's how we reuse the same expression inside EF subqueries. EF translates it to SQL, and the InMemory tests confirmed the same behaviour.
- **`CK_Reviews_VerifiedRequiresVisitToken`:** the database itself prevents any "verified" review that isn't tied to a token, even if a code path is written wrong in the future.
- **The reviewer join:** `ApplicationUser` lives in Infrastructure, so Domain doesn't know it. The relationship is configured in `ReviewConfiguration` (`HasOne<ApplicationUser>()`), and the reviewer's name comes from a join in the repository.
- **Stable sorting:** every sort ends with `Id`, so reviews with the same date or rating don't appear on two pages or disappear between them (tested with 5 reviews that share a timestamp).
- **UTC:** `datetime2` comes back from SQL Server without a "kind", so `ReviewService` marks it UTC. The JSON then goes out ending in `Z` (tested).

---

## 5. API contract

### New: `GET /api/v1/restaurants/{id}/reviews` `[Authorize]`
| Param | Rule |
|---|---|
| `sort` | `newest` (default) \| `highest` \| `lowest`, case-insensitive |
| `page` | ≥ 1 (default 1) |
| `pageSize` | 1–50 (default 20) |

```json
{ "success": true,
  "data": { "items": [ { "id": "…", "rating": 5, "text": "…", "createdAt": "2026-10-30T12:00:00Z",
                         "isVerified": true, "reviewer": { "id": "…", "fullName": "…", "userName": "…" } } ],
            "page": 1, "pageSize": 20, "totalCount": 1 },
  "meta": { "requestId": "…" } }
```
| Status | code | When |
|---|---|---|
| 401 | `unauthorized` | No token or an invalid one |
| 400 | `validation_error` | Invalid sort, page or pageSize, or a wrong type |
| 404 | `not_found` | Unknown restaurant, invalid GUID, or inactive |
| 405 | — | Any method other than GET (verified: POST → 405) |

### Changes to the Phase 3 contract
| Field | Before | Now |
|---|---|---|
| `averageRating` (list and detail) | Always `null` | The average of public reviews, rounded to one decimal; `null` when there are none |
| `reviewCount` | Always `0` | The number of public reviews |
| `minRating` | Always an empty list | Restaurants with average ≥ the value (compared with the **unrounded** average; restaurants without reviews are excluded) |
| `ratingBreakdown` (detail only) | Didn't exist | `{ food, service, value, atmosphere }` rounded, or `null` |

---

## 6. Database

**`Reviews` table** (`AddReviews` migration, applied):
- **Columns:** `Id` PK; `RestaurantId` FK → `Restaurants` **Cascade**; `UserId` FK → `AspNetUsers` **Restrict**; `Rating` + `FoodRating` / `ServiceRating` / `ValueRating` / `AtmosphereRating` int; `Text` nvarchar(2000); `IsVerified` bit default 0; `VisitTokenId` uniqueidentifier NULL; `CreatedAt` datetime2.
- **CHECK constraints:** each rating `BETWEEN 1 AND 5` (5 constraints) + `IsVerified = 0 OR VisitTokenId IS NOT NULL`.
- **Indexes:** `(RestaurantId, IsVerified, CreatedAt)` for the list and aggregates; `UserId`.
- **Why Cascade and Restrict:**
  - Deleting a restaurant takes its reviews with it.
  - Deleting a user account is blocked while they have reviews, so they don't disappear silently. Account deletion and moderation policy is a Phase 9 decision.

**Seed:** unchanged. **There are no reviews in SQL Server** (D8). Review data exists only inside the tests.

---

## 7. Verification

### Run (2026-11-01)
| Check | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 warnings, 0 errors |
| `dotnet test -c Release` (twice) | ✅ **95/95** (Application 56 + API 39); there were 61 |
| Migration review | ✅ Additive only; Designer = snapshot |
| Migration applied | ✅ The founder ran `database update` (`Done.`); the queries against SQL Server read `Reviews` without errors |
| SQL Server smoke | ✅ List 11 (`null`/`0`), `minRating=4` → 0, detail `ratingBreakdown: null`, reviews 200 empty, `sort=bad` 400, unknown 404, no token 401 envelope, **POST 405**, Swagger shows `/restaurants/{id}/reviews` |
| Layer boundaries | ✅ 0 cross references |

### The new tests (34)
- **Application (22):** the validator (known/invalid sort, paging); `RatingMath` (4.25→4.3, null); the service (404 before querying, validation before checking existence, passing criteria, UTC, reviewer); rounding the detail aggregates and breakdown.
- **Repository (7):** **excluding unverified reviews** from the list, the aggregates and `minRating`; the 3 sorts; stable pagination with identical timestamps; the reviewer join; a restaurant with no reviews → `null`/`0`/`null`.
- **API (12):** 401; 404 ×3; 400 ×3; empty list + detail without aggregates; sorting and paging with an unverified review excluded; `createdAt` ends in `Z`; rounded detail aggregates; `minRating`.

### Not verified
- **The CHECK constraints at runtime in SQL Server:** InMemory doesn't enforce CHECK, and there's no creation path that would try a wrong value. Confirmed by migration review only. The founder can run the optional read-only query `SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('Reviews');`, which should return 6 rows.
- **A list with real data in SQL Server:** there are no reviews in the database (D8). The list with data is verified through tests only.
- **GitHub CI:** after push.

---

## 8. Mobile handoff
| Item | Value |
|---|---|
| New endpoint | `GET /api/v1/restaurants/{id}/reviews?sort=newest&page=n&pageSize=20` (Bearer) |
| Paging | `page` / `pageSize` / `totalCount` inside `data`; there are more pages while `page * pageSize < totalCount` |
| Reviewer | `fullName` can be `null` (registration fields from Phase 2); `userName` likewise. The avatar shows the first letter of the name (no image, as in Figma's `UserAvatar`) |
| Date | `createdAt` is UTC ISO with `Z`. `timeAgo` is computed on the device (D6) |
| Detail | `averageRating` (number\|null), `reviewCount`, `ratingBreakdown` (object\|null) → the "متوسط التقييمات" card is shown only when it isn't `null` |
| `isVerified` | Always `true` in public results (MVP) |
| Errors | `not_found` (inactive restaurant), `validation_error`, `unauthorized` |
| **Expected on the device** | **Empty** lists and aggregates, because there are no reviews in SQL Server (D8) |

---

## 9. Known limitations (Backend)
1. **No real data** in SQL Server until Phase 5. The list on the device will always be empty.
2. **`VisitTokenId` has no FK** until Phase 5 creates `VisitTokens` (D1).
3. **`minRating` compares the unrounded average:** a restaurant whose average is 3.96 (shown as 4.0) doesn't appear under `minRating=4`. A conscious choice to keep it simple; it can be changed if you want.
4. **Deleting a user with reviews** is blocked by the FK (Restrict); the policy goes to Phase 9.
5. **Carried over from Phase 3:** native RTL before production, `BottomNav`, Burger Makers' hours, `appsettings.json` machine name, `dotnet-ef` 9.0.6.

---

## 10. Educational summary (Backend)
1. **The "public" rule as an expression, not as copied code:** `Expression<Func<Review,bool>>` is a description of a condition that EF can translate into SQL. Writing it once in Domain and reusing it everywhere means one decision applies across the whole system.
2. **Two-level protection:** the code filters with `IsPublic`, and the database refuses a "verified" review without a token (CHECK). Even a mistake in a future code path can't break the rule.
3. **Aggregates in the database, not in memory:** `AVG`/`COUNT` run inside SQL Server as subqueries, so we don't load thousands of reviews to compute an average.
4. **Join instead of a navigation:** Domain doesn't know Identity (Clean Architecture), but the repository in Infrastructure knows both and joins them in one query.
5. **Stable pagination:** without a tie-breaker, the database is free to return equal rows in any order, so the same review can appear on two pages. `ThenBy(Id)` fixes the order.
6. **Time:** store everything in UTC, mark it UTC on the way out, and let the device show "منذ يومين" in its own time.

---

## 11. Mobile Track
⏳ Not started. It starts only on the founder's explicit request ("Phase 4 — Mobile"). The plan: 4.5 (the review list per Figma), 4.6 (the empty state), 4.7 (QA + device), with decisions D5/D6/D7/D9 as approved.
