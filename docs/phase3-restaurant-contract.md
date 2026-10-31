# Phase 3 — Restaurant domain & API contract

**Status:** Finalized (Task 3.1)  
**Date:** 2026-09-13  
**Sources:** Approved Phase 3 plan, `docs/product-spec.md`, `docs/architecture.md`, `docs/decisions.md` (ADR-003, ADR-004, ADR-006, ADR-009), Phase 2 API conventions, Figma Make restaurant screens.

This document is the implementation contract for Tasks 3.2–3.10 (backend) and 3.11–3.15 (mobile). **No code in Task 3.1** — specification only.

---

## 1. Scope

### In scope (Phase 3)

- Domain model: `Restaurant`, `Category`, `RestaurantCategory`, `PriceLevel`
- Read-only discovery APIs (authenticated): list/search/filter + detail
- Amman pilot seed (~10–12 venues)
- Mobile: discovery list, search (restaurants only), filters, detail (no review creation)
- Rating summary DTO fields present but **empty** until Phase 4

### Out of scope (Phase 3)

| Item | Phase / ADR |
|------|-------------|
| `Review` entity or review list APIs | Phase 4 |
| Verified review creation | Phase 5 (ADR-004) |
| Public or user `POST /restaurants` | ADR-003 |
| Admin restaurant CRUD API | Phase 9 |
| Opening hours / `isOpen` / `openNow` filter backend | Deferred (Figma UI may stub) |
| Saved restaurants / share actions | Deferred |
| Community photos tab | Deferred (pending media ADR) |
| People search tab | Later phase |
| Home social feed with restaurant carousels | Phase 7+ |
| Distance sort server-side | Client computes from coordinates |
| Denormalized rating columns on `Restaurant` | Phase 4 uses review aggregate join |

---

## 2. Domain model

### 2.1 `Restaurant`

| Property | Type | Required | Constraints / notes |
|----------|------|----------|------------------------|
| `Id` | `Guid` | yes | PK |
| `Name` | `string` | yes | Arabic display name; max 200 |
| `Description` | `string` | yes | Arabic; max 2000 |
| `City` | `string` | yes | Generic (pilot seed: `"Amman"` / `"عمّان"`) — ADR-006 |
| `Address` | `string?` | no | Area/street for display |
| `Latitude` | `double` | yes | WGS84 |
| `Longitude` | `double` | yes | WGS84 |
| `PriceLevel` | `PriceLevel` | yes | See enum |
| `Phone` | `string?` | no | E.164 or local format |
| `ImageUrl` | `string?` | no | Hero/card image (admin seed); nullable |
| `IsActive` | `bool` | yes | Default `true`; inactive → 404 on API |
| `CreatedAt` | `DateTime` | yes | UTC |

**Not on entity:** `AverageRating`, `ReviewCount`, `Distance`, `IsOpen`, `Hours` — computed, projected, or deferred.

### 2.2 `Category`

| Property | Type | Required | Constraints |
|----------|------|----------|-------------|
| `Id` | `Guid` | yes | PK |
| `Name` | `string` | yes | Arabic display name; max 100 |
| `Slug` | `string` | yes | Unique, lowercase ASCII (e.g. `lebanese`, `burger`); filter key |

**No separate Cuisine entity** — cuisine ≡ category (many-to-many).

### 2.3 `RestaurantCategory` (join)

| Property | Type |
|----------|------|
| `RestaurantId` | `Guid` |
| `CategoryId` | `Guid` |

Composite PK `(RestaurantId, CategoryId)`.

### 2.4 `PriceLevel` enum

| Value | Name | Figma display |
|-------|------|---------------|
| `1` | `Budget` | `$` |
| `2` | `Moderate` | `$$` |
| `3` | `Upscale` | `$$$` |
| `4` | `Premium` | `$$$$` |

Stored as `int` in DB; serialized as numeric enum in JSON.

### 2.5 Repository (domain)

**`IRestaurantRepository`** — explicit repository only (ADR-009); no `IRepository<T>`.

```csharp
Task<PagedResult<RestaurantListItemProjection>> SearchAsync(
    RestaurantSearchCriteria criteria,
    CancellationToken cancellationToken);

Task<RestaurantDetailProjection?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken);
```

**`RestaurantSearchCriteria`**

| Property | Type | Notes |
|----------|------|-------|
| `Search` | `string?` | Case-insensitive contains on `Name` and `Description` |
| `City` | `string?` | Case-insensitive equality on `City` |
| `MinRating` | `decimal?` | See §4.3 — Phase 3 excludes all restaurants when set |
| `PriceLevel` | `PriceLevel?` | Exact match |
| `CategorySlug` | `string?` | Filter restaurants linked to category |
| `Page` | `int` | Default 1 |
| `PageSize` | `int` | Default 20, max 50 |

**`PagedResult<T>`:** `Items`, `Page`, `PageSize`, `TotalCount`.

Projections live in Application or Domain as agreed in Task 3.2; repository returns read models, not tracked entities.

---

## 3. Application layer

### 3.1 `IRestaurantService`

- `SearchAsync(RestaurantSearchQuery query)` → `RestaurantListResultDto`
- `GetByIdAsync(Guid id)` → `RestaurantDetailDto`

### 3.2 `RestaurantException`

Mirror `AuthException`:

| Code | HTTP | When |
|------|------|------|
| `not_found` | 404 | Unknown id or inactive restaurant |
| `validation_error` | 400 | Invalid query parameters |

### 3.3 Validation (`RestaurantQueryValidator`)

| Rule | Constraint |
|------|------------|
| `page` | ≥ 1 |
| `pageSize` | 1–50 (default 20) |
| `minRating` | If set: 1.0–5.0 |
| `priceLevel` | If set: 1–4 |
| `category` | If set: non-empty slug |
| `search` | If set: max length 100 |
| `city` | If set: max length 100 |

---

## 4. API contract

**Base path:** `/api/v1/restaurants`  
**Auth:** `[Authorize]` on all endpoints — JWT Bearer (Phase 2).  
**Envelope:** `ApiResponse<T>` with `success`, `data`, `errors`, `meta.requestId` (existing pattern).

### 4.1 `GET /api/v1/restaurants`

**Query parameters** (camelCase in query string):

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `search` | string? | — | Name/description contains |
| `city` | string? | — | City filter |
| `minRating` | decimal? | — | Minimum average rating |
| `priceLevel` | int? | — | `1`–`4` |
| `category` | string? | — | Category slug |
| `page` | int | `1` | Page number |
| `pageSize` | int | `20` | Page size (max 50) |

**Success 200 — `ApiResponse<RestaurantListResultDto>`**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "مطعم السفير",
        "city": "عمّان",
        "address": "جبل عمّان",
        "latitude": 31.9539,
        "longitude": 35.9106,
        "priceLevel": 2,
        "imageUrl": "https://example.com/image.jpg",
        "categories": [
          { "id": "…", "name": "لبناني", "slug": "lebanese" }
        ],
        "averageRating": null,
        "reviewCount": 0
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 12
  },
  "meta": { "requestId": "…" }
}
```

**Pagination:** fields inside `data`, not `ApiMeta` (meta keeps `requestId` only).

**Errors**

| Status | Code | When |
|--------|------|------|
| 401 | `unauthorized` | Missing/invalid JWT |
| 400 | `validation_error` | Invalid query |

### 4.2 `GET /api/v1/restaurants/{id}`

**Path:** `id` — GUID.

**Success 200 — `ApiResponse<RestaurantDetailDto>`**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "مطعم السفير",
    "description": "وجبات لبنانية وشامية أصيلة…",
    "city": "عمّان",
    "address": "جبل عمّان",
    "latitude": 31.9539,
    "longitude": 35.9106,
    "priceLevel": 2,
    "phone": "+962 6 555 0303",
    "imageUrl": "https://example.com/image.jpg",
    "categories": [
      { "id": "…", "name": "لبناني", "slug": "lebanese" },
      { "id": "…", "name": "شامي", "slug": "shami" }
    ],
    "averageRating": null,
    "reviewCount": 0
  },
  "meta": { "requestId": "…" }
}
```

**Errors**

| Status | Code | When |
|--------|------|------|
| 401 | `unauthorized` | Missing/invalid JWT |
| 404 | `not_found` | Invalid GUID, unknown id, or `IsActive == false` |

### 4.3 Rating behavior (Phase 3)

| Field | Phase 3 value | Phase 4+ |
|-------|---------------|----------|
| `averageRating` | always `null` | `decimal?` from verified reviews aggregate |
| `reviewCount` | always `0` | count of published reviews |

**`minRating` filter (Phase 3):** When `minRating` is provided, repository excludes restaurants where `reviewCount == 0`. Result: **empty list** for any `minRating` until Phase 4 populates reviews. Document in API tests and mobile empty state.

**`minRating` filter (Phase 4+):** `averageRating >= minRating` for restaurants with reviews.

### 4.4 DTO shapes (C# records)

```csharp
public sealed record CategoryDto(Guid Id, string Name, string Slug);

public sealed record RestaurantListItemDto(
    Guid Id,
    string Name,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? ImageUrl,
    IReadOnlyList<CategoryDto> Categories,
    decimal? AverageRating,
    int ReviewCount);

public sealed record RestaurantListResultDto(
    IReadOnlyList<RestaurantListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RestaurantDetailDto(
    Guid Id,
    string Name,
    string Description,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? Phone,
    string? ImageUrl,
    IReadOnlyList<CategoryDto> Categories,
    decimal? AverageRating,
    int ReviewCount);
```

JSON property names: **camelCase** (ASP.NET Core default).

### 4.5 Explicitly excluded endpoints

- `POST /api/v1/restaurants`
- `PUT` / `PATCH` / `DELETE` restaurants
- `GET /api/v1/restaurants/{id}/reviews`
- `GET /api/v1/categories` (optional future; Phase 3 can derive categories from list or seed constants on mobile)

---

## 5. Seed data (Task 3.8 preview)

**Pilot:** Amman, Jordan (ADR-006).  
**Count:** 10–12 restaurants.  
**Idempotent:** Skip if any row exists in `Restaurants` (same pattern as `IdentitySeed`).

**Coverage targets:**

| Dimension | Target |
|-----------|--------|
| `PriceLevel` | At least one per level 1–4 |
| Categories | 6–8 slugs, e.g. `lebanese`, `shami`, `italian`, `burger`, `sushi`, `coffee`, `desserts`, `jordanian` |
| Coordinates | Distinct points within Amman bounds |
| `ImageUrl` | Optional URLs for Figma fidelity |
| `IsActive` | ≥1 inactive (excluded from API, useful for 404 tests) |

**Category display names (Arabic)** align with Figma explore chips where applicable.

---

## 6. Mobile contract (Tasks 3.11–3.15)

### 6.1 Types (`types/restaurant.ts`)

Mirror DTOs above. `priceLevel` as `1 | 2 | 3 | 4`. Helper: `formatPriceLevel(level) → '$'…'$$$$'`.

### 6.2 API client (`services/restaurants/restaurantsApi.ts`)

- `searchRestaurants(params)` → `GET /api/v1/restaurants`
- `getRestaurant(id)` → `GET /api/v1/restaurants/{id}`

Uses existing Axios client + Bearer interceptor (Phase 2).

### 6.3 Query keys

- `['restaurants', 'list', filters]`
- `['restaurants', 'detail', id]`

### 6.4 UI mapping

| API | Figma UI |
|-----|----------|
| List + filters | `ExploreScreen` |
| `search` param | `SearchScreen` (restaurants section/tab only) |
| Detail | `RestaurantScreen` — details tab only; reviews tab empty; no write-review CTA (ADR-004) |
| `averageRating` null | Empty star state / “لا توجد تقييمات بعد” |
| `imageUrl` null | `#E9DFC9` placeholder |
| Distance | Haversine from device GPS (permission optional; hide if unavailable) |

### 6.5 Figma filters → API (Phase 3)

| Figma control | API param | Phase 3 |
|---------------|-----------|---------|
| Category chip | `category` (slug) | **Wired** |
| Price multi-select | `priceLevel` | **Single request per selection** or client-side multi-filter via repeated calls — **recommend:** API accepts one `priceLevel`; mobile applies one at a time or extend API with `priceLevels` in Phase 3.1 **decision: single `priceLevel` only** per request (simplest); multi-select = OR client-side filter on cached list for MVP. |
| Min rating | `minRating` | Wired; expect empty until Phase 4 |
| Open now | — | **UI disabled/hidden** in Phase 3 |

---

## 7. Backend file plan (reference for Tasks 3.2+)

| Layer | New artifacts |
|-------|---------------|
| Domain | `Entities/Restaurant.cs`, `Category.cs`, `RestaurantCategory.cs`, `Enums/PriceLevel.cs`, `Repositories/IRestaurantRepository.cs`, criteria + `PagedResult` |
| Application | `Restaurants/*` service, DTOs, validator, `RestaurantException` |
| Infrastructure | EF configs, `RestaurantRepository`, `RestaurantSeed`, migration `AddRestaurants` |
| API | `RestaurantsController.cs` |
| Tests | Endpoint + service + repository tests |

---

## 8. Task 3.1 decisions log

| Decision | Rationale |
|----------|-----------|
| Add optional `Phone`, `ImageUrl` on `Restaurant` | Figma detail + cards; admin-seeded only |
| No `IsOpen` / hours on entity | Reduces scope; Figma filter deferred |
| Single `priceLevel` query param | Matches plan; Figma multi-select handled client-side or sequential |
| `category` slug filter | Matches plan + Figma chips |
| List item omits `description` | Smaller payload; detail endpoint for full text |
| Authenticated reads only | Founder-locked Phase 3 decision |
| No `GET /categories` in Phase 3 | Categories embedded in restaurant DTOs; seed slugs documented |

---

## 8a. Approved contract changes (2026-11-01, founder-approved during Phase 3)

| Change | Detail |
|--------|--------|
| Opening status | `isOpen: boolean \| null` on list items and detail; `null` when the restaurant has no time zone or no opening hours |
| Today's hours | `todayHours: string \| null` on detail only, e.g. `"10:00 ص — 1:00 ص"` (restaurant local time) |
| `openNow` query param | `GET /api/v1/restaurants?openNow=true` returns only restaurants open now in their local time |
| Error envelopes | 401 now returns `{ code: "unauthorized" }`; malformed query values return 400 `validation_error` (was ProblemDetails) |
| Data model | `Restaurants.TimeZoneId` (IANA, nullable) + `RestaurantOpeningHours` (day, opens, closes; closes ≤ opens = past midnight) — migration `AddRestaurantOpeningHours` |

Section 1 "Opening hours / `isOpen` / `openNow` filter backend — Deferred" is superseded by this addendum. Full detail: [phase3-report.md](phase3-report.md).

---

## 9. Related documents

- [figma/restaurant-tokens.md](figma/restaurant-tokens.md) — UI tokens and screen copy
- [figma/auth-tokens.md](figma/auth-tokens.md) — shared palette
- [architecture.md](architecture.md) — layers and modules
- [product-spec.md](product-spec.md) — discovery filters
- [decisions.md](decisions.md) — ADR-003, ADR-004, ADR-006
- Phase 3 plan: `.cursor/plans/phase_3_restaurants_ad6e74eb.plan.md`
