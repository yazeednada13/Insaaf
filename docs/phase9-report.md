# Phase 9 — Admin and moderation: consolidated report

**Date:** 2026-11-07
**Branch:** `feature/phase9-admin-moderation` (from `develop` after the Phase 8A merge; the founder manages Git)
**References:** the roadmap (Phase 9), the approved Phase 9 plan (revision 2, decisions D-1…D-10), [decisions.md](decisions.md) ADR-003/ADR-012/ADR-017
**Report status:** ✅ **Backend Track complete** · No Mobile Track planned (D-7: Admin is API-only)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Before this phase, ops could issue visit tokens (Phase 5) but couldn't add or fix a restaurant, remove abusive content or stop an abusive account without editing the database. Phase 9 gives Admin a protected API for all of it, with no owner app or dashboard (ADR-003). The roadmap gate is: "Ops can seed restaurants, issue tokens, moderate (no owner app), including removing any user's comment."

### Task status (Backend)
The roadmap had no Phase 9 task table, so these IDs are new (D-1). No existing ID was changed.

| ID | Task | Status | Evidence |
|---|---|---|---|
| 9.1 | Restaurant management: create, full update, activate/deactivate, categories, opening hours, time zone | ✅ Verified | 32 tests; smoke; command counts |
| 9.2a | Moderation fields; migration `AddModeration` | ✅ Verified | Migration review; the founder applied it; `migrations list` shows no Pending; 5 columns confirmed on SQL Server |
| 9.2b | Hide / unhide a review | ✅ Verified | Tests on every public surface; API flow on a QR-created review; smoke |
| 9.3 | Admin deletes any comment | ✅ Verified | Tests; API test; smoke |
| 9.4 | Suspend / unsuspend an account | ✅ Verified | Tests, including lockout preservation; API sign-in tests; smoke |
| 9.5 | Admin read endpoints: reviews and users | ✅ Verified | Tests; smoke; command counts |
| 9.6 | Tests, Release build, layer check, smoke, report | ✅ | 307/307 tests; 0 warnings; smoke 19/19 PASS |

**Issuing tokens** was already done in Phase 5 (`AdminRestaurantsController`) and is unchanged.

### Applied decisions
| # | Decision | Implementation |
|---|---|---|
| D-1 | Task IDs | ✅ New 9.1–9.6 |
| D-2 | Restaurant removal | ✅ Deactivate only; no delete route |
| D-3 | Review moderation | ✅ Hide/unhide; excluded from ratings and interaction; the visit token stays redeemed |
| D-4 | Suspension | ✅ Separate `SuspendedAt`; blocks sign-in and refresh; the existing access token lives until it expires |
| D-5 | Comment moderation | ✅ Permanent delete, notification removed |
| D-6 | Audit | ✅ Structured log lines through `IAdminAuditLog`; no table |
| D-7 | Mobile track | ✅ None |
| D-8 | User reports | ✅ Not built |
| D-9 | Restaurant images | ✅ `imageUrl` string only |
| D-10 | `opensAt == closesAt` | ✅ Refused |

### Deviations from the plan's wording (same contract, recorded honestly)
1. **Audit logging goes through a port.** Application has no logging package. The services build each `AdminAuditEntry` and call `IAdminAuditLog` (Application). `AdminAuditLog` in Infrastructure writes it with `ILogger`. So the rules for what is logged stay in Application, and no dependency was added.
2. **Admin restaurant routes live in a new `AdminRestaurantManagementController`.** The Phase 5 controller is untouched; both share the `api/v1/admin/restaurants` prefix.
3. **Unknown JSON properties are refused (400)** on every Admin request body, like the comment body in Phase 6. So a body that tries to send `actorId` fails instead of being silently ignored.
4. **Suspension is reported only after a correct password.** A wrong password on a suspended account still gives `invalid_credentials` (401), so suspension doesn't reveal whether an account exists.
5. **`HiddenReason` is shown to Admin** in the admin review list and the visibility response. It never appears in public JSON or in logs.
6. **A second hide doesn't overwrite the reason;** it's recorded as `no_change`.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, `HttpContext`, `ClaimsPrincipal`, JWT claim names, EF Core, ASP.NET Core or `ILogger` in Domain and Application. The Admin role is checked only by `[Authorize(Roles = Admin)]` on the controllers. The controllers read the acting Admin's id from the JWT `sub` and pass it as `actorId`.

### Phase 9 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Review.cs` (modified) | `HiddenAt`, `HiddenByUserId`, `HiddenReason` |
| Domain | `Rules/ReviewVisibility.cs` (modified) | `IsVerified && HiddenAt == null`, the single public rule |
| Domain | `Repositories/IRestaurantRepository.cs`, `IReviewRepository.cs` (modified); `IUserModerationRepository.cs` (new) | Admin contracts |
| Domain | `Repositories/Projections/Phase9Projections.cs` | `AdminRestaurantProjection`, `RestaurantWriteModel`, `AdminReviewProjection`, `AdminUserProjection` |
| Application | `Admin/RestaurantInputValidator.cs` | Body validation, the week-circle overlap check, the IANA check |
| Application | `Admin/RestaurantAdminService.cs`, `ReviewModerationService.cs`, `UserModerationService.cs` | The rules; every mutation takes `actorId` and records an audit entry |
| Application | `Admin/AdminAudit.cs`, `AdminException.cs`, `Dtos/AdminRestaurantDtos.cs`, `Dtos/AdminModerationDtos.cs` | Audit port, errors, contracts |
| Application | `Auth/AuthService.cs`, `Abstractions/IUserAccountService.cs`, `IRefreshTokenService.cs`, `AuthException.cs` (modified) | `account_suspended` (403) at sign-in and refresh |
| Infrastructure | `Identity/ApplicationUser.cs` (modified) | `SuspendedAt`, `SuspendedByUserId` |
| Infrastructure | `Configurations/ReviewConfiguration.cs` (modified) | `HiddenReason` max 500; `HiddenByUserId` FK (Restrict) |
| Infrastructure | `Repositories/RestaurantRepository.cs`, `ReviewRepository.cs` (modified); `UserModerationRepository.cs` (new) | Admin reads and writes |
| Infrastructure | `Auth/IdentityUserAccountService.cs`, `RefreshTokenService.cs` (modified) | Suspension checks; lockout check unchanged |
| Infrastructure | `Admin/AdminAuditLog.cs` | Structured log line |
| Infrastructure | `Migrations/20261107140942_AddModeration*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/AdminRestaurantManagementController.cs`, `AdminModerationController.cs`; `Extensions/ClaimsPrincipalExtensions.cs` | Routes; `actorId` from the JWT |

---

## 3. End-to-end data flow

### `PUT /api/v1/admin/restaurants/{id}`
`[Authorize(Roles = Admin)]` → the controller reads `actorId` from `sub` → `RestaurantAdminService.UpdateAsync(actorId, id, body)` → `RestaurantInputValidator` checks fields, hours and time zone → the repository resolves category slugs (unknown slugs give 400) → `RestaurantRepository.UpdateAsync` updates the row, replaces hours, keeps unchanged category links and swaps the rest, all in one `SaveChanges` → audit entry → the Admin view is returned.

### `PUT /api/v1/admin/reviews/{id}/visibility`
`ReviewModerationService` validates `hidden` and the reason → `ReviewRepository.SetHiddenAsync` sets or clears the three columns. The visit token is never touched. Every public query already filters through `ReviewVisibility.IsPublic`, so ratings, lists, detail, feed, Home, taste and notifications follow, and so do `IsPublicAsync`, the guard used by likes and comments.

### `PUT /api/v1/admin/users/{id}/suspension`
`UserModerationService` refuses Admin targets (403 `admin_target`) → `UserModerationRepository.SetSuspendedAsync` sets only `SuspendedAt`/`SuspendedByUserId` → `IRefreshTokenService.RevokeAllForUserAsync`. On the next sign-in, `IdentityUserAccountService` reports `IsSuspended` after a correct password, and `AuthService` returns 403 `account_suspended`. On refresh, the revoked token gives 401. A live token for a suspended account gives 403 `account_suspended`.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| `ReviewVisibility.IsPublic` | One rule for "public", so hiding needed one change instead of 16 |
| `RestaurantInputValidator.HasOverlap` | Puts every period on a 7-day circle of minutes. An overnight period is compared with the next day's periods, and Saturday night with Sunday. Touching ends are allowed |
| `IsIanaTimeZone` | Accepts an id only if it converts from IANA to Windows **and** the server can load it, so `Arab Standard Time` is refused even on Windows |
| Nullable request properties | A missing `categorySlugs` or `openingHours` is an error, never a silent "clear" |
| `SuspendedAt` instead of `LockoutEnd` | Lifting a suspension can't clear a failed-login lockout |
| `IAdminAuditLog` | Application decides what is logged; Infrastructure decides how |

---

## 5. API contract

All routes need `[Authorize(Roles = Admin)]`: 401 anonymous, 403 for a User. They use the standard envelope. Unknown JSON properties give 400.

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/v1/admin/restaurants?includeInactive=true&page=1&pageSize=20` | — | `{ items: [admin restaurant], page, pageSize, totalCount }` (page size 1–50) |
| POST | `/api/v1/admin/restaurants` | restaurant body | 201 admin restaurant |
| PUT | `/api/v1/admin/restaurants/{id}` | restaurant body (full) | 200 admin restaurant |
| PUT | `/api/v1/admin/restaurants/{id}/status` | `{ "isActive": false }` | 200 admin restaurant |
| GET | `/api/v1/admin/reviews?restaurantId=&userId=&cursor=&limit=20` | — | `{ items: [admin review], nextCursor }` (limit 1–50) |
| PUT | `/api/v1/admin/reviews/{id}/visibility` | `{ "hidden": true, "reason": "…" }` | 200 admin review |
| DELETE | `/api/v1/admin/comments/{id}` | — | `{ commentId, deleted: true }` |
| GET | `/api/v1/admin/users?query=` | — | `{ items: [{ id, userName, fullName, email, isAdmin, isSuspended, suspendedAt }] }` (query 2–100 characters, up to 20 results) |
| PUT | `/api/v1/admin/users/{id}/suspension` | `{ "suspended": true }` | 200 admin user |

**Restaurant body:**
```json
{
  "name": "…", "description": "…", "city": "Amman", "address": "…",
  "latitude": 31.95, "longitude": 35.91, "priceLevel": 2,
  "phone": null, "imageUrl": "https://…", "timeZoneId": "Asia/Amman",
  "categorySlugs": ["burger"],
  "openingHours": [
    { "dayOfWeek": 0, "opensAt": "12:00", "closesAt": "23:00" },
    { "dayOfWeek": 5, "opensAt": "18:00", "closesAt": "02:00" }
  ]
}
```
- **Required:** `name` (1–200), `city` (1–100), `latitude` (−90…90), `longitude` (−180…180), `priceLevel` (1–4), `categorySlugs` (1–10 existing, no repeats), `openingHours` (array, up to 50).
- **Optional; null clears it:** `description` (≤ 2000), `address` (≤ 500), `phone` (≤ 32), `imageUrl` (absolute http/https, ≤ 2048), `timeZoneId`.
- **`dayOfWeek`:** 0 = Sunday … 6 = Saturday. Times are `HH:mm`, local.
- **Overnight:** `closesAt < opensAt` means the period runs past midnight. `opensAt == closesAt` is refused.
- **Overlaps:** no overlap anywhere in the week; touching ends are allowed.
- **Hours and time zone:** an explicit `[]` means unknown hours. `timeZoneId` must be IANA, and it's required when there are hours.

**Admin restaurant:** `{ id, name, description, city, address, latitude, longitude, priceLevel, phone, imageUrl, timeZoneId, isActive, isPartner, createdAt, categories: [{ id, name, slug }], openingHours: [{ dayOfWeek, opensAt, closesAt }], publicReviewCount }`.
**Admin review:** `{ id, restaurantId, restaurantName, authorId, authorUserName, rating, text, createdAt, isHidden, hiddenAt, hiddenReason, commentCount }`.

**Errors:**
- 400 `validation_error`, with all messages joined.
- 401 `unauthorized`.
- 403 `admin_target` (suspending an Admin).
- 404 `not_found`.

**Auth change:** `POST /auth/login` and `POST /auth/refresh` return **403 `account_suspended`** for a suspended account (login only after a correct password).

---

## 6. Database

### Migration `20261107140942_AddModeration` (additive only)
| Table | Column | Type |
|---|---|---|
| `Reviews` | `HiddenAt` | datetime2 NULL |
| `Reviews` | `HiddenByUserId` | uniqueidentifier NULL, FK → `AspNetUsers` (Restrict), index `IX_Reviews_HiddenByUserId` |
| `Reviews` | `HiddenReason` | nvarchar(500) NULL |
| `AspNetUsers` | `SuspendedAt` | datetime2 NULL |
| `AspNetUsers` | `SuspendedByUserId` | uniqueidentifier NULL, no FK |

- Existing data is unchanged: every value starts null, so every review stays visible and every account stays active.
- Restaurants, opening hours and the time zone used existing columns (`Restaurants.TimeZoneId`, table `RestaurantOpeningHours`).

### Business rules
- Restaurants are never deleted; a deactivated restaurant and its reviews disappear from public reads, and Admin still sees it.
- Created restaurants start active and not partners; partnership stays a separate Phase 5 action.
- A hidden review is excluded from ratings, lists, detail, feed, Home, taste and notifications. It can't be liked, unliked or commented on, or have its comments listed. Its visit token stays redeemed, so it can't be rewritten.
- Admin comment deletion is permanent and removes its notification.
- Suspension doesn't touch content, `LockoutEnd` or `AccessFailedCount`. Admin accounts can't be suspended.

---

## 7. Verification

### Run (2026-11-07)
| Check | Result |
|---|---|
| Baseline before changes | ✅ 255/255 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 221/221 |
| `Insaaf.API.Tests` | ✅ 86/86 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only |
| Migration applied | ✅ By the founder; `migrations list` shows no Pending; 5 columns exist |
| **SQL Server smoke** | ✅ **19/19 PASS** |
| **SQL commands on SQL Server** | ✅ Admin restaurants page 2; restaurant by id 1; reviews page 1; reviews by restaurant 1; user search 1 |
| State after smoke (SQL) | ✅ 0 hidden reviews, 0 suspended accounts; the test restaurant is inactive |

### The new tests (52)
- **46 Application cases:**
  - **Hours:** same-day overlap, Sunday 22:00–02:00 with Monday 01:00–03:00, Saturday into Sunday, touching ends, overnight, equal times, bad days and times.
  - **Time zone:** `Asia/Amman` and `Europe/London` accepted; `Arab Standard Time`, `Mars/Phobos`, empty, blank and free text refused.
  - **Full PUT:** omitted fields refused with existing data kept; explicit `[]` clears; unknown slug; sets replaced.
  - **Deactivate and reactivate.**
  - **Hide on every public surface; refused likes, unlikes, comments and comment lists; token kept; unhide restores everything; idempotent hide.**
  - **Comment delete, with notification, on a hidden review.**
  - **Suspension:** revokes tokens; refresh refused as `Suspended`; **unsuspend keeps `LockoutEnd` and `AccessFailedCount`**; Admin target refused.
  - **Admin lists:** paging, filters, bad cursor, user search.
  - **Audit entries:** checked exactly.
- **6 API tests:**
  - 401 and 403 on all 9 routes.
  - Restaurant create, update and deactivate, with Windows zone, overlap, omitted field and `actorId` in the body all refused.
  - Hide/unhide on a QR-created review: detail, like, comment and comment list 404, then OK. No reason or `hidden` in public JSON.
  - Admin deletes another user's comment.
  - Suspended login 403 `account_suspended`; refresh 401; the live access token still 200 (documented); Admin can't be suspended; unsuspend restores login.
  - Admin lists, with no `suspended` in public profile JSON.

### SQL Server smoke results (19/19)
- **Access:** Admin login 200; User on an Admin route 403.
- **Restaurants:**
  - Create 201 with 2 periods and `Asia/Amman`; public 200.
  - Windows zone 400; cross-day overlap 400.
  - Omitted `openingHours` 400 with hours kept; explicit `[]` clears hours and zone.
  - Deactivate gives public 404, and the Admin list still has it.
- **Hide on a seeded review:**
  - The restaurant's count went 2 → 1; detail, like and comment gave 404.
  - The Admin list showed it hidden.
  - Unhide restored count and average (4.0 → 4.0), with no moderation fields in public JSON.
- **Comment:** Admin deleted a user's comment.
- **Suspension:**
  - Login 403 `account_suspended`; refresh 401.
  - The existing access token was still 200 (documented).
  - Unsuspend gave login 200.
  - Search by email found the account.
- **End state:** the restaurant's rating was unchanged.

### Not verified
- **Audit log lines on the running API:** checked in tests through `RecordingAuditLog`, not read from the API console.
- **That a suspended user's existing access token stops after 15 minutes:** follows from `AccessTokenMinutes: 15`, not waited for.
- **Large data:** the pilot has 2 reviews and 12 restaurants; command counts don't grow with rows.
- **The mobile app's message for `account_suspended`:** not changed and not tested (see §8).

---

## 8. Mobile handoff (no Mobile Track; recorded for later)
- **Hidden content:** the app already shows "هذا المحتوى غير متاح." for `not_found`, so a hidden review opened from an old list shows that. Lists update on their next refresh.
- **Suspended user, signed in:**
  - The current access token keeps working for up to 15 minutes.
  - The next refresh fails (401), and the existing interceptor clears the session.
  - The app returns to login.
- **Suspended user at login:** `account_suspended` isn't in `services/auth/authErrors.ts`, so **the app shows the generic "حدث خطأ غير متوقع."**. A one-line mapping (e.g. "تم إيقاف هذا الحساب.") would fix it. **It needs your decision**, because Phase 9 has no Mobile Track.
- **No Admin UI**, per the spec.

---

## 9. Known limitations (Backend)
- **A suspended account keeps API access until its access token expires** (up to 15 minutes). There's no per-request check in the MVP (ADR-017); revisit in Phase 10.
- **Audit is log lines only, with no table.** Nothing is queryable later unless logs are kept by the host (Phase 11).
- Admin works through Swagger or HTTP tools; there's no dashboard.
- No "open 24 hours" form (`opensAt == closesAt` is refused).
- **Test data:**
  - `smoke9-…@example.com` accounts and one inactive restaurant, "Smoke9 …" (`e359d1d4-c844-4913-9aef-af33ae6cea9d`), remain in the development database.
  - Its hours and time zone were cleared by the smoke test.

### Limitations and founder actions (open release items)
1. **The Phase 8 and Phase 8A device checklists are still not run** (account isolation first). Phases 8 and 8A stay "Complete with an open item".
2. **iOS is untested** (Phase 10).
3. **Decide** whether to add the `account_suspended` message in the app (§8).

---

## 10. Educational summary (Backend)
- **One rule, many screens:** because every public query used `ReviewVisibility.IsPublic`, hiding a review needed one line there, and ratings, feed, Home, taste, notifications, likes and comments all followed. Tests prove each one.
- **Why hide instead of delete?** Hiding is reversible and keeps the visit token redeemed, so a removed review can't be written again with the same QR.
- **Why a week circle for hours?** "Sunday 22:00–02:00" really ends on Monday. Placing periods on one 7-day line, with a wrap from Saturday to Sunday, makes every overlap the same simple comparison.
- **Why `SuspendedAt` and not Identity's lockout?** The lockout also protects against password guessing. Sharing it would let an unsuspend wipe a real security lock.
- **Why `actorId` from the controller?** Application code stays testable and free of HTTP. The id can only come from the signed JWT, never from the body.
- **Why a logging port?** Application decides what an audit entry contains, without personal data, and Infrastructure decides where it goes. Each can change alone.
