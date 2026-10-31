# Phase 3 — Restaurants: consolidated report (Backend + Mobile)

**Date:** 2026-11-01
**Branch:** `feature/phase3-restaurants` (the founder manages Git)
**References:** the approved Master Architecture and Roadmap Plan, the Phase 3 plan (`phase_3_restaurants_ad6e74eb.plan.md`), [phase3-restaurant-contract.md](phase3-restaurant-contract.md), [decisions.md](decisions.md), Figma Make `DfKJchmPebiHH9dRYaYMuS`
**Replaces:** `phase3-backend-report.md` (merged here at the founder's request: one report per Phase)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **founder-verified on the device** (reported by the founder, not observed by the agent).

---

## 1. Overview

### Business purpose
Phase 3 lets an authenticated user discover restaurants: browse a list, search, filter (category, price, minimum rating, open now), see distance from their location, and view a restaurant's details with its open/closed status and today's hours. The data is an admin-seeded Amman pilot (ADR-003, ADR-006). **There is no review creation** (ADR-004): `averageRating = null` and `reviewCount = 0` until Phase 4.

### The Master Plan gate
> **"Browse, search, filter, detail from app"**: ✅ met. The backend is verified by tests and against SQL Server. The Mobile journey was verified on the device by the founder (§7).

### Task status (original IDs preserved)
| ID | Task | Track | Status | Evidence |
|---|---|---|---|---|
| 3.1 | Contract + Figma tokens | Cross-track | ✅ | Contract matches the code; tokens compared with the Figma Make source (read directly through Figma MCP) |
| 3.2 | Domain | Backend | ✅ | Build; Domain has no packages |
| 3.3 | EF + `AddRestaurants` migration | Backend | ✅ | Applied (founder output: no Pending) |
| 3.4 | `RestaurantRepository` | Backend | ✅ | 6 repository tests + SQL Server smoke |
| 3.5 | Service, DTOs, validator, DI | Backend | ✅ | Service + validator + calculator tests |
| 3.6 | `GET /api/v1/restaurants` | Backend | ✅ | API tests + smoke on InMemory and SQL Server |
| 3.7 | `GET /api/v1/restaurants/{id}` | Backend | ✅ | API tests + smoke |
| 3.8 | Amman seed | Backend | ✅ | 12 restaurants written to SQL Server; idempotent; opening-hours backfill verified on SQL Server |
| 3.9 | Backend tests | Backend | ✅ | **61/61** (Release) |
| 3.10 | Backend verification gate | Backend | ✅ | Release build/test + Swagger + SQL Server smoke |
| 3.11 | Mobile API integration | Mobile | ✅ | typecheck; app-generated URLs against SQL Server |
| 3.12 | Explore screen | Mobile | ✅ | Code + bundles + founder-verified on the device |
| 3.13 | Search and filters | Mobile | ✅ | Same |
| 3.14 | Restaurant detail | Mobile | ✅ | Same; **no write-review button** |
| 3.15 | Mobile verification | Mobile | ✅ (with limitations in §9) | typecheck, lint, Android/iOS bundles, founder-verified on the device |

### Additions approved by the founder during the Phase (beyond the original plan)
| Addition | Decision |
|---|---|
| Envelope for 401 and malformed query (instead of an empty body / ProblemDetails) | Decision (A) |
| Open/closed status + today's hours + the `openNow` filter (backend + migration + mobile) | "Add it to Phase 3 now" |
| Distance from the user (`expo-location`) | Approved |
| Inter font for numbers | Approved |
| `EXPO_PUBLIC_API_BASE_URL` for device testing | Approved |
| Fix for a Phase 2 defect in `AuthProvider` (refresh running more than once) | Approved |
| RTL fix across the whole app, including the auth screens | Founder request |

---

## 2. Architecture

```text
Mobile (Expo Router screens → TanStack Query hooks → services → Axios)
        │  HTTPS/JSON + Bearer JWT
        ▼
Insaaf.API  ──►  Insaaf.Application  ──►  Insaaf.Domain
    │                    ▲                      ▲
    └──► Insaaf.Infrastructure (EF Core, SQL Server, Identity, JWT) ┘
```
- **Domain:** no packages. Checked: no `EntityFrameworkCore`/`AspNetCore` usage in Domain or Application.
- **Explicit repository:** `IRestaurantRepository` (ADR-009). No generic repository, CQRS or MediatR.
- **Mobile:** no global state. Server data and device location go through TanStack Query.

### Backend files (Phase 3)
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Restaurant.cs`, `Category.cs`, `RestaurantCategory.cs`, `RestaurantOpeningHours.cs` | Entities |
| Domain | `Enums/PriceLevel.cs` | 1–4 |
| Domain | `Rules/OpeningHoursRules.cs` | The "open at a given time" rule, including periods past midnight |
| Domain | `Repositories/IRestaurantRepository.cs`, `RestaurantSearchCriteria.cs` (+ `LocalTimeInZone`), `PagedResult.cs`, `Projections/*` | Repository contract + read models |
| Application | `Restaurants/RestaurantService.cs` | Validation → criteria → repository → DTOs + status calculation |
| Application | `Restaurants/OpeningHoursCalculator.cs` | Converts to the restaurant's local time, `isOpen`, Figma-format hours text |
| Application | `Restaurants/Validation/RestaurantQueryValidator.cs`, `RestaurantException.cs`, `Dtos/RestaurantDtos.cs` | Validation, errors, contract |
| Infrastructure | `Persistence/Configurations/Restaurant*.cs`, `CategoryConfiguration.cs`, `RestaurantOpeningHoursConfiguration.cs` | Fluent configs |
| Infrastructure | `Persistence/Repositories/RestaurantRepository.cs` | LINQ + `AsNoTracking` + projections + the `openNow` filter |
| Infrastructure | `Persistence/Seeding/RestaurantSeed.cs` | The Amman seed + idempotent opening-hours backfill |
| Infrastructure | `Migrations/20260914144128_AddRestaurants`, `20261031231834_AddRestaurantOpeningHours` | Schema |
| API | `Controllers/RestaurantsController.cs` | Two endpoints with `[Authorize]` |
| API | `Extensions/ApiErrorResponseExtensions.cs` | Envelope for 401 and model-binding 400 |

### Mobile files (Phase 3)
| Area | File | Responsibility |
|---|---|---|
| Routes | `app/index.tsx` | Redirect to `/restaurants` or `/login` |
| Routes | `app/restaurants/_layout.tsx` | Auth guard + Stack |
| Routes | `app/restaurants/index.tsx`, `search.tsx`, `[id].tsx` | Re-export the screens |
| Screens | `features/restaurants/screens/ExploreScreen.tsx`, `SearchScreen.tsx`, `RestaurantDetailScreen.tsx` | The three screens |
| Components | `RestaurantCard`, `StarRating`, `FilterChip`, `RestaurantFilterSheet`, `RestaurantListStates`, `RestaurantIcons`, `FadeInView` | UI from Figma |
| Hooks | `useRestaurantQueries.ts`, `useRestaurantDistance.ts` | TanStack Query |
| Services | `services/restaurants/restaurantsApi.ts`, `restaurantErrors.ts`, `services/location/deviceLocation.ts` | API + GPS |
| Utilities | `utils/geo.ts`, `types/restaurant.ts`, `constants/theme.ts`, `config/env.ts` | Distance, types, tokens, address |

---

## 3. End-to-end data flow

### Example: Explore with the "برجر" chip
```text
ExploreScreen (category = 'burger')
  → useRestaurantListQuery({ category, pageSize: 50 })     ['restaurants','list',filters]
  → restaurantsApi.searchRestaurants → Axios (+ Bearer from the interceptor)
  → GET /api/v1/restaurants?category=burger&pageSize=50
     ExceptionHandlingMiddleware → JwtBearer (401 envelope if no token)
     → model binding (400 envelope if the type is wrong) → RestaurantsController.Search
     → RestaurantService: Validator → criteria → IRestaurantRepository.SearchAsync
     → EF Core (AsNoTracking + projection with categories + hours) → SQL Server
     → per item: OpeningHoursCalculator.Evaluate(TimeZoneId, hours, UtcNow) → isOpen
  ← 200 { success, data: { items, page, pageSize, totalCount }, meta }
  → unwrapRestaurantResponse → RestaurantCard × n (+ distance from useRestaurantDistance)
```

### openNow
The service fetches the restaurants' distinct time zones (`GetTimeZoneIdsAsync`) and computes the local day and time in each zone. The repository then applies the same domain rule in LINQ, one term per zone joined with OR. Restaurants without a time zone don't match, because their status is unknown.

### Distance
`useRestaurantDistance` → `useQuery(['device','location'])` → `getDeviceCoordinates` (while-in-use permission). Distance is computed on the device (Haversine) from the API coordinates, in the format `"1.2 كم"`. **No permission or no location → the line is hidden; there are no fake values.**

---

## 4. API contract (actual)

**Auth:** `Authorization: Bearer <accessToken>` on every endpoint. **Envelope:** `{ success, data, errors[{code,message}], meta{requestId} }`. JSON is camelCase.

### `GET /api/v1/restaurants`
| Param | Rule |
|---|---|
| `search` | ≤ 100; contains on `name` and `description`, case-insensitive |
| `city` | ≤ 100; **exact match** after lowercasing |
| `minRating` | 1.0–5.0; **always returns an empty list in Phase 3** |
| `priceLevel` | 1–4 (one value) |
| `category` | slug, case-insensitive |
| `openNow` | `true` → only restaurants open now in their local time |
| `page` / `pageSize` | ≥1 / 1–50 (defaults 1 / 20) |

List item: `id, name, city, address, latitude, longitude, priceLevel, imageUrl, categories[], averageRating (null), reviewCount (0), isOpen (bool|null)`.

### `GET /api/v1/restaurants/{id}`
The list fields + `description`, `phone`, `todayHours` (e.g. `"10:00 ص — 1:00 ص"`, or `null`).

### Errors (verified by running them)
| Status | code | When |
|---|---|---|
| 401 | `unauthorized` | No token, or an invalid/expired one |
| 400 | `validation_error` | A value outside the rules, or a wrong type |
| 404 | `not_found` | Unknown id, invalid GUID, or inactive restaurant |
| 500 | `internal_error` | Unexpected (no stack trace) |

**Not present (by design):** POST/PUT/DELETE restaurants, reviews, `GET /categories`, admin API (Phase 9).

---

## 5. Database and business rules

| Table | Key points |
|---|---|
| `Restaurants` | Name 200, Description 2000, City 100 (indexed), PriceLevel int, IsActive (default 1, indexed), **TimeZoneId nvarchar(64) NULL** |
| `Categories` | Slug **unique** |
| `RestaurantCategories` | Composite PK, 2 FKs with Cascade |
| `RestaurantOpeningHours` | DayOfWeek int, OpensAt/ClosesAt `time`, FK Cascade, index (RestaurantId, DayOfWeek) |

**Rules:**
- An inactive restaurant isn't shown, and its detail returns 404.
- The rating isn't stored on `Restaurant`.
- Opening periods: `ClosesAt <= OpensAt` means the period runs past midnight. A day with no rows is closed. **A restaurant with no time zone or no hours has `isOpen = null` (unknown).**

**Seed (verified against SQL Server):**
- **Restaurants:** 12 (11 active + 1 inactive), City `"عمّان"`, `TimeZoneId = "Asia/Amman"`.
- **Categories:** 10. **Prices:** 1–4.
- **Hours:** from Figma design data for 3 restaurants: سوشي هيكاري 12:00–23:00, مطعم السفير 10:00–01:00, Tonino Italiano 13:00–23:00, every day. **These are design data, not verified real hours.**
- **Burger Makers:** no hours (Figma's "12:00 م" is ambiguous and wasn't settled).
- **Backfill:** idempotent, for databases seeded before the hours existed.

---

## 6. Mobile

### Screens and journeys
| Screen | Route | States |
|---|---|---|
| Explore | `/restaurants` | Shimmer skeleton; empty "لا توجد نتائج" + hint; error + "إعادة المحاولة"; list with fade-in |
| Search | `/restaurants/search` | Idle hint; skeleton; `لا نتائج لـ "{query}"`; error + retry; 300ms delay |
| Detail | `/restaurants/[id]` | Hero skeleton; 404 with no retry; other errors with retry; details tab / reviews tab (empty) |

**Journey:** Login → `app/index.tsx` → `/restaurants` → chip / filter / search → detail → back. The `restaurants/_layout.tsx` guard redirects to `/login` if there's no session.

### State
- **Server data:** TanStack Query with keys `['restaurants','list',filters]` and `['restaurants','detail',id]`, `staleTime` 30s.
- **Location:** `['device','location']`, `staleTime` 5 minutes, no retry.
- **Filters:** local `useState` in Explore. **Session:** `AuthProvider` (Phase 2).
- **Price:** one value → server; several values → OR filter on the client (contract §6.5).

### Arabic and RTL
- **The cause found:** React Native flips `textAlign` `'left'`/`'right'` when layout is RTL (`TextLayoutManager.kt`, `RCTAttributedTextUtils.mm`). So `'right'` was rendering on the left.
- **The fix:** a documented `textAlignStart = 'left'` constant (start = right in Arabic), applied in 19 places, including the auth screens and `TextInput`. The toggle uses `start`/`end`. `StarRating` uses `direction: 'ltr'` as in Figma.
- **On the device:** the founder confirmed on 2026-11-01 that Arabic and RTL work correctly on the device. **No RTL configuration change was made in the code** (SDK 57, the same `I18nManager` calls), and the diagnostic output wasn't recorded. So the reason it works now isn't documented in the code; see the limitations.

### Figma
- **Sources:** the screens were compared with the Figma Make source (`ExploreScreen`, `SearchScreen`, `RestaurantScreen`, `RestaurantCard`, `StarRating`, `BottomSheet`, `index.css`, `App.tsx`, `types.ts`, `mock.ts`), read directly through Figma MCP.
- **Applied:** colours, sizes, line heights, exact SVG icons, chips, card, sheet animation, card fade-in, safe area, and open/closed status.
- **Approved deviations:**
  - No `BottomNav` (deferred until the phase that adds a second tab).
  - The "all" title is hidden.
  - The people tabs in search are hidden.
  - Save/share, the photos tab and the sub-ratings are deferred.
  - The rating filter's default is empty instead of 4.
  - The hero gradient doesn't appear on web.
  - The location permission message is our own text.

---

## 7. Verification

### Run in this session (2026-11-01)
| Check | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 warnings, 0 errors |
| `dotnet test -c Release` | ✅ **61/61** (Application 34, API 27) |
| Mobile `tsc --noEmit` | ✅ clean |
| Mobile lint | ✅ 0 errors, 1 warning (`client.ts`, Phase 2) |
| `expo export` Android + iOS | ✅ bundles 3.6MB / 3.3MB |
| Migration review | ✅ `AddRestaurantOpeningHours` additive only; Designer = snapshot |
| API against SQL Server | ✅ `isOpen` for 3 restaurants, `null` for the rest, `openNow` consistent |
| App-generated URLs against SQL Server | ✅ list 11, burger 1, `$$$$` 2, minRating 0, search 1, detail ✓ |

### Earlier in the Phase
- **SQL Server smoke:** register/login/refresh/me ✅; every filter; 401/400/404 envelopes; seed idempotent on restart. This also closed Phase 2's missing E2E gate.
- **Fixed test failure:** the parallel API test classes were colliding over Serilog's bootstrap logger (fix: `AssemblyInfo.cs`).

### Founder-verified on the device (not observed by the agent)
- The founder confirmed (2026-11-01) that **"everything is working correctly on the device"**, including Arabic and RTL. The device type and per-step details weren't recorded in this session.
- Before that, the founder's log showed a defect (refresh being called repeatedly). It was fixed in `AuthProvider`.

### Not verified
- GitHub CI (it runs after push).
- iOS on a real device (Expo Go on the iPhone needs SDK 58).
- Performance and load.

---

## 8. Phase 2 defects found and fixed in this Phase
| Defect | Cause | Fix |
|---|---|---|
| Logged out after a reload | `useSetCurrentUser` returned a new function every render → `bootstrap` ran repeatedly → the same refresh token was reused → the server revoked all of the user's tokens (reuse detection) | `useCallback` + a `useRef` guard so it runs once |
| 401 with an empty body | No `OnChallenge` | 401 envelope (decision A) |
| Malformed query → ProblemDetails | `InvalidModelStateResponseFactory` was the default | 400 `validation_error` envelope |
| Text on the left in RTL | `textAlign: 'right'` is flipped in RTL | `textAlignStart` across the whole app |

---

## 9. Known limitations and remaining items
| # | Item | Details |
|---|---|---|
| 1 | **How RTL works on the device isn't documented** | No code change; the founder confirmed it works. Expo's documentation says Expo Go before SDK 58 disables RTL. **Before production:** decide how to force RTL natively (`expo-localization` with `forcesRTL`, or SDK 58) and verify it on a real build |
| 2 | Exact matching with Arabic diacritics | `city=عمان` ≠ `عمّان`; the same in `search`. A founder decision (no normalisation) |
| 3 | `minRating` always returns an empty list | Until Phase 4 |
| 4 | Opening hours are design data | 3 restaurants only; Burger Makers is pending the interpretation of "12:00 م"; real hours need ops/admin (Phase 9) |
| 5 | No `BottomNav` | Deferred, by founder decision |
| 6 | Pages up to 50 restaurants in Explore | Enough for the pilot (11); no infinite scroll |
| 7 | iOS SDK mismatch | The iPhone's Expo Go is SDK 58 and the project is SDK 57 |
| 8 | `forceRTL` only takes effect after a restart | On a device that isn't in Arabic, the first launch only |
| 9 | API tests run sequentially | The root cause (Serilog bootstrap logger) is in `Program.cs` |
| 10 | `appsettings.json` contains `Server=DESKTOP-N5OFDOV` | Pre-existing; needs a decision later (the dev `SigningKey` is excluded by `.gitignore`) |
| 11 | `dotnet-ef` 9.0.6 < runtime 9.0.9 | Warning only |
| 12 | A smoke-test user in SQL Server | `smoke-1793227258@example.com`, can be deleted |
| 13 | 39 npm audit warnings | From existing dependencies, not handled |
| 14 | `README.md` is out of date | Outside the scope |

**Does Phase 3 satisfy its acceptance criteria?** Yes for the Master Plan gate and every original Task, with the limitations above. The most important item before production is #1.

---

## 10. Mobile integration notes (summary)
- **Address:** `EXPO_PUBLIC_API_BASE_URL`, falling back to `http://localhost:5232`. On a device, use the computer's LAN IP and run the backend with `--urls http://0.0.0.0:5232`.
- **401:** the interceptor refreshes once (single-flight) and retries. The startup refresh now runs only once.

---

## 11. Educational summary
1. **Clean Architecture:** the request comes in from the outside (API), the business logic lives in Application, and the rules and interfaces in Domain. EF Core is one implementation of an interface, which is why we tested the service with Moq and the repository with InMemory.
2. **A rule in one place, two forms:** "is it open now?" lives in `OpeningHoursRules` (C#), and the repository applies it in LINQ for the `openNow` filter. Tests protect them from diverging.
3. **Time zones:** the server stores hours in the restaurant's local time plus the zone name (IANA), converts "now" to that time, and doesn't depend on the server's or the phone's time.
4. **The envelope:** every response has one shape, so the app reads `success` then `data` or `errors[0].code`.
5. **TanStack Query instead of global state:** server data and even device location are cached under clear keys and shared between screens.
6. **RTL in React Native:** `left`/`right` mean "start/end" when layout is RTL. That's why you write `textAlignStart` and `start`/`end` instead of thinking in physical directions.
7. **Verification levels:** build → tests → running the API → SQL Server → the device. Each level caught something the one before couldn't (the shadda, the collation, repeated refresh, RTL).

---

## 12. Files changed in this Phase (summary)
- **Backend:** `src/backend/**` (Restaurants across all four layers, `ApiErrorResponseExtensions`, `Program.cs`, the two migrations).
- **Tests:** `tests/Insaaf.API.Tests/{AssemblyInfo.cs, RestaurantEndpointsTests.cs, AuthEndpointsTests.cs}`, `tests/Insaaf.Application.Tests/Restaurants/*`.
- **Mobile:** `src/mobile/app/**`, `features/restaurants/**`, `features/auth/{hooks/useAuthQueries.ts, context/AuthProvider.tsx, components/*, screens/*}` (RTL + refresh fix), `services/{restaurants,location}/**`, `utils/geo.ts`, `types/*`, `constants/theme.ts`, `config/env.ts`, `app.json`, `package.json`, `package-lock.json`.
- **Docs:** `docs/phase3-report.md` (this report), `docs/phase3-restaurant-contract.md` (contract addendum), `docs/project-progress.md`, `CLAUDE.md`. `docs/phase3-backend-report.md` was removed because it's merged here.
