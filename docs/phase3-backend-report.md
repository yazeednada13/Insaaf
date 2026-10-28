# Phase 3 — Restaurants: Backend track report

**Date:** 2026-10-29
**Track:** Backend (Tasks 3.1 [BE portion] → 3.10)
**Branch:** `feature/phase3-restaurants` (the founder manages Git)
**References:** the approved Master Architecture and Roadmap Plan, the Phase 3 plan (`phase_3_restaurants_ad6e74eb.plan.md`), [phase3-restaurant-contract.md](phase3-restaurant-contract.md), [decisions.md](decisions.md)

> This report separates three things: what was **verified by running it** (tests / smoke), what was **checked by reading the code**, and what is **not verified**. Mobile has not started.

---

## 1. Overview

### Business purpose
Phase 3 lets an authenticated user **discover restaurants**: browse a list, search, filter (price, category, city, minimum rating) and view a restaurant's details. The data comes from an Amman pilot seed (the restaurant catalogue is admin-seeded — ADR-003). There is **no review creation** and no review data: `averageRating` is always `null` and `reviewCount` is `0` until Phase 4.

### Task status (evidence-based)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 3.1 (BE) | Domain/API contract | ✅ Verified | The code matches the contract §2–§4; the two mismatches (empty 401, ProblemDetails) were fixed by founder decision (A) |
| 3.1 (Mobile) | Figma tokens document | ⏳ Belongs to the Mobile track | `docs/figma/restaurant-tokens.md` exists; not compared with Figma yet |
| 3.2 | Domain entities, enum, repository interface | ✅ Verified | Build + Domain has no package dependencies |
| 3.3 | EF configs + `AddRestaurants` migration | ✅ Verified | The snapshot matches the configs; the founder ran `migrations list` (no Pending) + `database update` (`Done.`); queries against SQL Server work |
| 3.4 | `RestaurantRepository` | ✅ Verified | 5 repository tests + smoke against SQL Server |
| 3.5 | Service, DTOs, validator, DI | ✅ Verified | 3 service tests + 2 validator tests |
| 3.6 | `GET /api/v1/restaurants` | ✅ Verified | API tests + smoke against InMemory and SQL Server + Swagger |
| 3.7 | `GET /api/v1/restaurants/{id}` | ✅ Verified | API tests (200/404/invalid GUID/inactive) + smoke against SQL Server |
| 3.8 | Amman seed (`RestaurantSeed`) | ✅ Verified | 2 seed tests + "Seeded 12 Amman restaurants." against SQL Server + no re-seed after restart |
| 3.9 | Backend tests | ✅ Verified | **43/43 passing** (Debug, repeated, and Release) |
| 3.10 | Backend verification gate | ✅ Verified | (a) Release build/test like CI ✅ (b) Swagger + InMemory smoke ✅ (c) smoke against SQL Server ✅ |

**Not verified:** CI on GitHub (it runs after the founder pushes).

---

## 2. Architecture

### Projects and dependency direction
```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                 ▲                    ▲
    └──► Insaaf.Infrastructure ────────────┘
```
- **Domain** (`Insaaf.Domain.csproj`): **no** packages at all. Checked: no `Microsoft.EntityFrameworkCore` or `Microsoft.AspNetCore` usage in Domain/Application.
- **Application**: depends only on Domain + `DependencyInjection.Abstractions`.
- **Infrastructure**: EF Core, SQL Server, Identity, JWT — implements the interfaces defined in Domain/Application.
- **API**: Controllers, Middleware, Swagger, HTTP error-response shaping. It references Infrastructure only for wiring (composition root).

### Phase 3 files and responsibilities
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Restaurant.cs`, `Category.cs`, `RestaurantCategory.cs` | Entities (many-to-many restaurant ↔ category) |
| Domain | `Enums/PriceLevel.cs` | `Budget=1 … Premium=4` |
| Domain | `Repositories/IRestaurantRepository.cs` | **Explicit** repository (no generic `IRepository<T>` — ADR-009) |
| Domain | `Repositories/RestaurantSearchCriteria.cs`, `PagedResult.cs`, `Projections/*` | Search criteria + read models (plain records with no EF) |
| Application | `Restaurants/RestaurantService.cs` | Validation → criteria mapping → repository call → DTO mapping |
| Application | `Restaurants/Validation/RestaurantQueryValidator.cs` | Query validation rules |
| Application | `Restaurants/RestaurantException.cs` | `validation_error` (400) / `not_found` (404) errors |
| Application | `Restaurants/Dtos/RestaurantDtos.cs` | The API contract (records) |
| Infrastructure | `Persistence/Configurations/Restaurant*.cs`, `CategoryConfiguration.cs` | Fluent configs: lengths, indexes, keys |
| Infrastructure | `Persistence/Repositories/RestaurantRepository.cs` | LINQ queries + `AsNoTracking` + projections |
| Infrastructure | `Persistence/Seeding/RestaurantSeed.cs` | The idempotent Amman seed |
| Infrastructure | `Persistence/Migrations/20260914144128_AddRestaurants.cs` | The `Restaurants`, `Categories`, `RestaurantCategories` tables |
| API | `Controllers/RestaurantsController.cs` | Two thin endpoints, `[Authorize]` |
| API | `Extensions/ApiErrorResponseExtensions.cs` | **New in this track:** envelope for 401 and model-binding errors |
| API | `Middleware/ExceptionHandlingMiddleware.cs` | Catches `RestaurantException` / `AuthException` / anything unexpected → envelope |

---

## 3. End-to-end data flow

### Example: `GET /api/v1/restaurants?category=lebanese&page=1`
```text
Mobile (Axios + Bearer)
  │
  ▼
ExceptionHandlingMiddleware ── catches any exception and turns it into an envelope
  ▼
Authentication (JwtBearer) ── no token or an invalid one → OnChallenge → 401 envelope {code: unauthorized}
  ▼
Authorization ([Authorize])
  ▼
Model binding → RestaurantSearchQuery ── wrong type (priceLevel=abc) → 400 envelope {validation_error}
  ▼
RestaurantsController.Search
  ▼
RestaurantService.SearchAsync
  ├─ RestaurantQueryValidator.Validate ── error → RestaurantException(validation_error) → 400 envelope
  ├─ ToSearchCriteria (trim + empty→null + int→PriceLevel)
  ▼
IRestaurantRepository.SearchAsync  (implemented by RestaurantRepository)
  ├─ ActiveRestaurants(): AsNoTracking + IsActive == true
  ├─ ApplyFilters: search / city / priceLevel / category / minRating
  ├─ CountAsync → TotalCount
  ├─ OrderBy(Name).ThenBy(Id) → Skip/Take → Select(projection)
  ▼
EF Core → SQL Server (COUNT query + a SELECT with categories in the projection)
  ▼
Projection → DTO mapping (in the Service)
  ▼
OkEnvelope → 200 { success: true, data: { items, page, pageSize, totalCount }, meta: { requestId } }
```

### Detail: `GET /api/v1/restaurants/{id}`
The controller first tries `Guid.TryParse` → invalid → 404 `not_found` without reaching the Service. Otherwise the Service calls `GetByIdAsync`; the repository looks only among **active** restaurants → `null` → `RestaurantException(not_found, 404)` → the controller turns it into an envelope.

---

## 4. Important code — why it exists and how the parts interact

- **`IRestaurantRepository` in Domain, `RestaurantRepository` in Infrastructure:** the Application layer only knows the contract (interface). That keeps the business logic free of EF, makes it testable with Moq (`RestaurantServiceTests`), and keeps the dependency direction inward.
- **Projections instead of returning entities:** the query reads only the needed columns, with no tracking (`AsNoTracking`). That's faster and prevents accidental edits to read-only data.
- **`minRating` in Phase 3:** `query.Where(_ => false)` — a deliberate decision in the contract (§4.3). There are no reviews yet, so no restaurant has a rating, and the honest answer is an empty list. Phase 4 will replace this line with a join on the Reviews aggregate.
- **`RestaurantSeed` at `ApplicationStarted`:** it runs after the server starts, seeds the categories (only the ones that don't exist yet), then the restaurants **only if** the `Restaurants` table is empty. That's what makes it idempotent.
- **`ApiErrorResponseExtensions` (new):**
  - `InvalidModelStateResponseFactory`: when ASP.NET fails to bind a query value (e.g. a word in place of a number), it returns our envelope instead of the default ProblemDetails.
  - `JwtBearerEvents.OnChallenge`: when the token is missing or invalid, it writes the envelope `{code: "unauthorized"}` with 401 and keeps the `WWW-Authenticate: Bearer` header.
  - Placed in the **API layer**, because shaping the HTTP response is that layer's job, not Infrastructure's.
  - **Impact on Phase 2 (by founder approval):** `/api/v1/auth/me` now also returns the envelope on 401. The mobile interceptor relies only on `status === 401`, so the refresh behaviour doesn't change.
- **`tests/Insaaf.API.Tests/AssemblyInfo.cs` (new):** disables parallel test execution in the API test project. **The cause:** `Program.cs` uses Serilog's static `CreateBootstrapLogger()`, which can only be "frozen" once per process; when the two classes `AuthEndpointsTests` and `RestaurantEndpointsTests` build two hosts at the same time, the second fails (`The entry point exited without ever building an IHost`). This showed up in Phase 3 when the second test class was added. The fix is in the tests only; `Program.cs` didn't change because of it.

---

## 5. API contracts (actual, as run)

**Auth:** every restaurant endpoint requires `Authorization: Bearer <accessToken>` (from `/api/v1/auth/login` or `/register`).
**Envelope:** `{ "success": bool, "data": T | null, "errors": [{ "code", "message" }] | null, "meta": { "requestId" } }`
**JSON:** camelCase. `priceLevel` is a number (1–4).

### `GET /api/v1/restaurants`
| Param | Type | Default | Rule |
|---|---|---|---|
| `search` | string | — | ≤ 100 characters; case-insensitive contains on `name` **and** `description` |
| `city` | string | — | ≤ 100; **exact match** after lowercasing (see limitations) |
| `minRating` | decimal | — | 1.0–5.0; **Phase 3: always returns an empty list** |
| `priceLevel` | int | — | 1–4 (one value per request) |
| `category` | string | — | slug, case-insensitive (`LEBANESE` = `lebanese`) |
| `page` | int | 1 | ≥ 1 |
| `pageSize` | int | 20 | 1–50 |

Ordering: by `name` then `id`. Pagination is **inside `data`**: `items`, `page`, `pageSize`, `totalCount`.

**List item:** `id, name, city, address, latitude, longitude, priceLevel, imageUrl, categories[{id,name,slug}], averageRating (null), reviewCount (0)` — **no `description`**.

### `GET /api/v1/restaurants/{id}`
Detail = the list fields + `description` + `phone`.

### Errors (all verified by running them)
| Status | `code` | When |
|---|---|---|
| 401 | `unauthorized` | No token, or an invalid/expired one |
| 400 | `validation_error` | A value outside the rules (`page=0`, `pageSize=99`, `priceLevel=9`) **or** a wrong type (`priceLevel=abc`) |
| 404 | `not_found` | Unknown id, invalid GUID, or an inactive restaurant |
| 500 | `internal_error` | Unexpected (via the middleware, no stack trace) |

**Error messages are in English;** the mobile app maps `code` to Arabic text.

### Explicitly not present (per the contract and the ADRs)
`POST/PUT/PATCH/DELETE /restaurants`, `GET /restaurants/{id}/reviews`, `GET /categories`, an admin API (Phase 9).

---

## 6. Database and business rules

### Tables (`AddRestaurants`)
| Table | Columns / constraints |
|---|---|
| `Restaurants` | `Id` PK, `Name` nvarchar(200), `Description` nvarchar(2000), `City` nvarchar(100), `Address` nvarchar(500)?, `Latitude`/`Longitude` float, `PriceLevel` int, `Phone` nvarchar(32)?, `ImageUrl` nvarchar(2048)?, `IsActive` bit default 1, `CreatedAt` datetime2. Indexes: `City`, `IsActive` |
| `Categories` | `Id` PK, `Name` nvarchar(100), `Slug` nvarchar(100) **unique** |
| `RestaurantCategories` | PK (`RestaurantId`, `CategoryId`), two FKs with Cascade, index on `CategoryId` |

### Rules
- An inactive restaurant (`IsActive = false`) doesn't appear in the list, and its detail returns 404.
- The rating isn't stored on `Restaurant` (no denormalised columns); it will come from Reviews in Phase 4.
- `City` is generic (ADR-006); the seed puts `"عمّان"`.

### Seed (verified against SQL Server)
- **12 restaurants:** 11 active + 1 inactive (`مطعم مغلق للتجارب`), all with City = `"عمّان"`, distinct coordinates inside Amman.
- **Prices:** Budget, Moderate, Upscale, Premium — all present.
- **10 categories** (approved by the founder): `burger, pizza, sushi, coffee, chicken, desserts, italian, lebanese, shami, jordanian`.
- **On SQL Server:** it wrote 12 restaurants on first start; after a restart there was no re-seeding (11 active).

---

## 7. Verification

### Tests (run)
| Project | Before the track | After the track | Result |
|---|---|---|---|
| `Insaaf.Application.Tests` | 15 | 19 | ✅ 19/19 |
| `Insaaf.API.Tests` | 20 | 24 | ✅ 24/24 |
| **Total** | 35 | **43** | ✅ Debug (repeated 3+ times) and Release |

**Before the fix:** 1 failure on every run of the full suite (parallel hosts + Serilog) — fixed with `AssemblyInfo.cs`.

**New/extended tests:**
- **Seed:** idempotent when run twice; pilot coverage targets (prices, one inactive, City, categories, coordinates).
- **Repository:** search in the description + city filter case-insensitively; pagination with ordering by name.
- **API:** price filter; page 2; 401 envelope without a token and with an invalid one; 400 envelope for a malformed query; `/auth/me` 401 envelope.

### Smoke test (run)
- **InMemory (`UseInMemoryDatabase=true`):** all the endpoints + Swagger UI (200) + Bearer in `security`.
- **SQL Server (local, with founder approval):** register → login → refresh → me ✅; the list and every filter ✅; detail ✅; 404 ✅; 401/400 envelopes ✅; inactive restaurant hidden ✅; seed idempotent after restart ✅.

### Migration (founder-executed, from their output)
`dotnet ef migrations list` showed all four migrations with no `(Pending)`, and `database update` printed `Done.` with no `Applying` → **the database was already up to date** before this track.

### Not verified
- GitHub CI (after push).
- Load or performance.
- `403` (no role-protected endpoints in Phase 3).

---

## 8. Mobile handoff

| Item | Value |
|---|---|
| Base URL | `config/env.ts` = `http://localhost:5232` (matches the `http` profile in `launchSettings.json`). **On a physical device it needs the computer's LAN IP** (any change to `config/env.ts` needs approval) |
| Endpoints | `GET /api/v1/restaurants` and `GET /api/v1/restaurants/{id}` — both require Bearer |
| Query keys (contract) | `['restaurants','list', filters]`, `['restaurants','detail', id]` |
| 401 | Now returns an envelope with `unauthorized`; the existing interceptor refreshes based on the status → no change needed |
| Errors | `validation_error`, `not_found`, `unauthorized`, `internal_error` + network errors |
| Rating | `averageRating: null`, `reviewCount: 0` → empty state "لا توجد تقييمات بعد" per the contract |
| `minRating` | Always returns an empty list in Phase 3 → it needs an honest empty state |
| Price | One `priceLevel` per request; the Figma multi-select is filtered on the client (contract §6.5) |
| Categories | The Figma chips (8 + "الكل") don't include `shami` or `jordanian`; they appear in the restaurant's `categories` only. **Per the founder's decision: the chips match Figma exactly** |
| `imageUrl` | Can be `null` → placeholder `#E9DFC9` |
| `description` | Only in the detail response |
| City/search | Exact matching with diacritics (see limitations) |

**Note:** the mobile types in `types/restaurant.ts` match the DTOs above (checked by reading; typecheck is part of the Mobile track).

---

## 9. Known limitations and remaining items

| # | Item | Type | Details |
|---|---|---|---|
| 1 | Exact matching with Arabic diacritics | Known limitation (founder decision: no normalisation) | On SQL Server: `city=عمّان` → 11, `city=عمان` → 0, `city=Amman` → 0; `search=عمّان` → 4, `search=عمان` → 0 |
| 2 | `minRating` always returns an empty list | By design (contract §4.3) | Until Phase 4 |
| 3 | Swagger shows parameter names in PascalCase (`Search`, `PriceLevel`) | Documentation | They work in camelCase too (binding is case-insensitive) |
| 4 | Startup with a database that doesn't have the migration | Risk (inference, not tested) | The seed runs at `ApplicationStarted` and would fail if the tables don't exist |
| 5 | API tests run sequentially | Workaround | The root cause (Serilog bootstrap logger) is in `Program.cs`, outside the scope |
| 6 | `appsettings.json` contains `Server=DESKTOP-N5OFDOV`, `appsettings.Development.json` contains a dev `SigningKey` | Pre-existing (D8: report only) | Conflicts with CLAUDE.md §13; needs a founder decision later |
| 7 | `dotnet-ef` 9.0.6 is older than the runtime 9.0.9 | Warning | Harmless; updating is optional for the founder |
| 8 | Test user in SQL Server | Smoke-test leftover | `smoke-1793227258@example.com` — the founder can delete it if they want |
| 9 | `README.md` is out of date (says Phase 0) | Documentation | Not updated (outside the agreed scope); `project-progress.md` was updated |
| 10 | The Phase 2 E2E gate against SQL Server | Was missing evidence | Now verified as part of the smoke test (register/login/refresh/me) |

---

## 10. Educational summary

1. **Clean Architecture in practice:** the request goes in from the outside (API), passes through the use-case logic (Application), which talks only to **interfaces** defined on the inside (Domain). EF Core and SQL Server are just one implementation of those interfaces (Infrastructure). So we can test the Service with a fake repository (Moq) and the repository with an in-memory database, without a server.
2. **Explicit repository vs generic:** `IRestaurantRepository` has only two methods, both written for real product needs (search + detail). That avoids a generic `IRepository<T>` that would open operations nobody needs (like updating restaurants from the app).
3. **Projections and AsNoTracking:** discovery queries are read-only. Choosing the columns in `Select` and turning off tracking saves memory and time, and stops EF from fetching entire tables.
4. **The API envelope:** every response, success or failure, has the same shape. That makes the mobile app simpler: it reads `success`, then `data` or `errors[0].code`. In this track we closed the last two gaps where the API was returning a different shape (empty 401 and ProblemDetails).
5. **Layered validation:** type errors are caught at binding (400 envelope), the business rules in `RestaurantQueryValidator` (400), and existence/activity in the repository (404). The server never trusts the client.
6. **The idempotent seed:** checking "is there data already?" before inserting means restarting the server doesn't duplicate data. That's essential with a real database.
7. **Verification levels:** a passing build ≠ working code. We moved from a build → unit/integration tests (InMemory) → a smoke test against the API running for real → a smoke test against SQL Server. Each level catches problems the one before can't (like the shadda behaviour, and the collation on SQL Server).

---

## Files changed in this track
| File | Change |
|---|---|
| `src/backend/Insaaf.API/Extensions/ApiErrorResponseExtensions.cs` | New — 401 envelope + model-binding 400 envelope (decision A) |
| `src/backend/Insaaf.API/Program.cs` | One line: `AddInsaafApiErrorResponses()` |
| `tests/Insaaf.API.Tests/AssemblyInfo.cs` | New — disables parallel test execution |
| `tests/Insaaf.API.Tests/RestaurantEndpointsTests.cs` | 4 new tests + an extended 401 test |
| `tests/Insaaf.API.Tests/AuthEndpointsTests.cs` | `Me_ReturnsUnauthorized` extended with an envelope assertion |
| `tests/Insaaf.Application.Tests/Restaurants/RestaurantSeedTests.cs` | New — 2 seed tests |
| `tests/Insaaf.Application.Tests/Restaurants/RestaurantRepositoryTests.cs` | 2 new tests |
| `docs/phase3-backend-report.md` | This report |
| `docs/project-progress.md` | Phase 3 status updated |
