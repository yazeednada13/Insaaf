# Phase 5 — QR Verification + Verified Review Creation: consolidated report

**Date:** 2026-11-05
**Branch:** `feature/phase5-qr-verification` (from `develop` @ `92689f0`; the founder manages Git)
**References:** the approved Master Plan (Phase 5), the approved Phase 5 plan (decisions D-Q1…D-X), [decisions.md](decisions.md) ADR-004, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`QRScreen.tsx`, `BottomNav.tsx`)
**Report status:** ✅ **Backend Track complete** · ✅ **Mobile Track implemented** (device evidence limits in §11.7) · ⏳ Phase 5 awaiting the founder's actions (§12)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 5 builds **the only review-creation path in the MVP**: QR invoice → VisitToken → server-resolved restaurant → verified review. Nobody can publish a "verified" review without a real code from a partner restaurant's invoice. A code works **only once** and only before it expires, and **the client never chooses the restaurant**.

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 5.1 | `VisitToken` + rules + partnership + `AddVisitTokens` migration | ✅ Verified | Build; migration review (additive only); the founder applied it (`migrations list` shows no Pending); SQL Server smoke; 8 rule and eligibility test cases |
| 5.2 | `IVisitTokenRepository` + issuing service + revocation | ✅ Verified | Codec/payload tests + direct `VisitTokenIssuer` tests (Count 1 and 25, restricted principal, non-partner rejection) + smoke |
| 5.3 | Redeem + create in one transaction | ✅ Verified | Concurrency test with two `DbContext`s (InMemory) + **SQL Server smoke: 5 simultaneous requests → one 201 and four 409s** |
| 5.4 | Verified create API | ✅ Verified | API tests + 429 test + smoke |
| 5.5 | Admin API: issue, revoke, summary, partnership | ✅ Verified | API tests (401/403/409/201) + smoke |
| 5.9 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 130/130 tests; smoke 16/16 PASS |

### Applied decisions (approved with the plan)
| # | Decision | Implementation |
|---|---|---|
| D-Q1 | One single-use code per invoice, tied to the restaurant on the server | ✅ |
| D-I1 | Channel-independent issuing | ✅ One `IVisitTokenIssuer` + `IssuanceChannel` (only `Manual = 1` used; `Pos = 2` reserved with no implementation) + nullable `IssuedByUserId` |
| D-I2 | Per-restaurant issuing authorization | ✅ `IssuancePrincipal` + scope check `issuance_forbidden` (403) inside the service; one factory, `ForAdmin` |
| D-PL | Payload format | ✅ `insaaf://visit/<43 base64url chars>`, built and parsed only by `VisitPayload` |
| D-P1 | Partnership | ✅ `IsPartner` + `PartnerSince` + `PUT …/partnership` |
| D-E1 | Validity and batch size | ✅ `expiresInDays` default 90 (1–365), `count` 1–500 |
| D-T1 | The client sends the code itself | ✅ `token` at resolve and create |
| D-F2 | Text 0–500 | ✅ `VerifiedReviewValidator.MaxTextLength = 500` |
| D-R | Rate limiting | ✅ Built-in ASP.NET Core: resolve 20/minute, create 5/minute per user, from configuration |
| D-A1 | Admin account | ✅ The founder enabled `SeedAdmin` through User Secrets; log line: `Development admin user seeded for admin@insaaf.local` |
| D-X | Reviews-per-user limit | ❌ Not implemented (not in the roadmap) |

### Deviations from the plan's wording (same contract, recorded honestly)
1. **The revocation service name:** the plan said `VisitTokenRevoker`; the code uses `VisitTokenLifecycleService` (`IVisitTokenLifecycleService`) because it holds both revocation **and** the summary, with the same scope check. Same behaviour.
2. **Save order inside the transaction:** the plan described one `SaveChanges` (UPDATE + INSERT). The code uses **two saves inside one transaction**: first claim the token (UPDATE with the `ConcurrencyStamp` check), then INSERT the review, then commit. **Why:** the request that lost the race fails before attempting to add a review, and this also behaves correctly on InMemory, which has no transactions. A failing concurrency test exposed this in the first version. On SQL Server both commit or roll back together.
3. **`IVisitTokenCodec`** has only `GenerateToken` and `Hash`. Payload parsing lives in `VisitPayload.TryExtractToken` (static, in Application) instead of `TryParsePayload` on the codec, so the payload format stays in one place, independent of the cryptography.
4. **A test file:** `CustomWebApplicationFactory` is no longer `sealed` and has a virtual `ConfigureTestSettings`, so a host can run with low rate limits.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, EF Core or ASP.NET Core in Domain and Application. The Admin restriction lives **in the controller only**.

### Phase 5 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Enums/VisitTokenIssuanceChannel.cs` | `Manual = 1`, `Pos = 2` (reserved) |
| Domain | `Entities/VisitToken.cs` | The token: restaurant, issuance, channel, hash, dates, `ConcurrencyStamp` |
| Domain | `Entities/Restaurant.cs` (modified) | `IsPartner`, `PartnerSince`, `VisitTokens` |
| Domain | `Rules/VisitTokenRules.cs` | **The single source** for token state (`Valid/Expired/Used/Revoked`) and eligibility (`IsActive && IsPartner`) |
| Domain | `Repositories/IVisitTokenRepository.cs` | The contract: find by hash, add, revoke, summary, **`RedeemWithReviewAsync`** |
| Domain | `Repositories/IRestaurantRepository.cs` (modified) + `Projections/RestaurantPartnershipProjection.cs` | Read and change partnership |
| Application | `Verification/Abstractions/IVisitTokenCodec.cs` | Token generation and hashing (implemented in Infrastructure) |
| Application | `Verification/VisitPayload.cs` | Builds and parses `insaaf://visit/<token>` |
| Application | `Verification/IssuancePrincipal.cs` | Who is issuing, as established by the server |
| Application | `Verification/VisitTokenIssuer.cs` | **The single, channel-independent issuing service** |
| Application | `Verification/VisitTokenLifecycleService.cs` | Revocation and summary |
| Application | `Verification/VerificationService.cs` | Resolve and create |
| Application | `Verification/Validation/VerifiedReviewValidator.cs` | Ratings 1–5, text ≤ 500 |
| Application | `Verification/VerificationException.cs` + `Dtos/*` | Errors and contracts |
| Application | `Admin/RestaurantPartnershipService.cs` | Partnership |
| Infrastructure | `Verification/VisitTokenCodec.cs` | `RandomNumberGenerator` (32 bytes) + SHA-256 |
| Infrastructure | `Persistence/Configurations/VisitTokenConfiguration.cs` | Table, CHECKs, indexes, FKs |
| Infrastructure | `ReviewConfiguration.cs`, `RestaurantConfiguration.cs`, `AppDbContext.cs` (modified) | FK + filtered unique index, `IsPartner` |
| Infrastructure | `Persistence/Repositories/VisitTokenRepository.cs` | **The transaction boundary** |
| Infrastructure | `Persistence/Migrations/20261102022855_AddVisitTokens*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/VerificationController.cs` | `visits/resolve`, `reviews` |
| API | `Controllers/AdminRestaurantsController.cs` | Partnership, issue, revoke, summary |
| API | `Extensions/RateLimitingExtensions.cs` | Rate-limit policies + 429 envelope |
| API | `Program.cs`, `ExceptionHandlingMiddleware.cs`, `ApiResponseExtensions.cs`, `appsettings.json` (modified) | Registration, `VerificationException` handling, `CreatedEnvelope`, settings |

---

## 3. End-to-end data flow

### Issuing: `POST /api/v1/admin/restaurants/{id}/visit-tokens` `{count, expiresInDays}`
1. JwtBearer validates the token; `[Authorize(Roles = "Admin")]` rejects non-Admins (401/403).
2. The controller computes `ExpiresAt = now + expiresInDays` and builds `IssuancePrincipal.ForAdmin(sub)`, **from the JWT, not the body**.
3. `VisitTokenIssuer.IssueAsync`: scope check → `count` 1–500 → expiry in the future → restaurant exists → `IsActive && IsPartner` (otherwise 409 `restaurant_not_partner`).
4. For each token: `codec.GenerateToken()` (256 random bits) → `codec.Hash()` → a `VisitToken` holding only the hash, with `IssuanceChannel`/`IssuedByUserId` from the principal and one `IssuanceId` for the batch.
5. `AddRangeAsync` → EF → `INSERT VisitTokens`.
6. 201 response: `payloads` = `VisitPayload.Build(token)`. **This is the only time raw codes leave the server;** no endpoint returns them again.

### Preview: `POST /api/v1/verification/visits/resolve` `{token}`
1. `[Authorize]` + rate limit `verification-resolve`.
2. `VerificationService.ResolveAsync` → `GetRedeemableTokenAsync`: `TryExtractToken` → hash → `FindByHashAsync` (with the restaurant) → `VisitTokenRules.Evaluate` → eligibility.
3. Restaurant details come from `IRestaurantRepository.GetByIdAsync` (average and count). **The token is not redeemed.**

### Create: `POST /api/v1/verification/reviews` `{token, rating, …, text}`
1. `[Authorize]` + rate limit `verification-create`. Any extra field (such as `restaurantId`) → 400 from the JSON deserializer (`JsonUnmappedMemberHandling.Disallow`).
2. `VerifiedReviewValidator` runs **before** the token is touched.
3. The same `GetRedeemableTokenAsync` (the token is tracked).
4. The `Review` is built: `RestaurantId = token.RestaurantId`, `IsVerified = true`, `VisitTokenId = token.Id`, `UserId` from the JWT.
5. `RedeemWithReviewAsync`:
   ```text
   BEGIN TRAN
     UPDATE VisitTokens SET RedeemedAt, RedeemedByUserId, ConcurrencyStamp=@new
       WHERE Id=@id AND ConcurrencyStamp=@old      ← 0 rows = lost the race → DbUpdateConcurrencyException
     INSERT Reviews (…, VisitTokenId)              ← filtered UNIQUE index = last guard (2601/2627)
   COMMIT
   ```
   Either failure → rollback → `AlreadyRedeemed` → 409 `token_used`.
6. 201 `{reviewId, restaurantId}`. Public reads still go through `ReviewVisibility.IsPublic`, so the review appears immediately in the list and the aggregates.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| `VisitTokenRules` | One rule for token state with a fixed precedence: used → revoked → expired → valid. Nothing else decides whether a token is valid |
| `IssuancePrincipal` + `VisitTokenIssuer` | Issuing knows nothing about "Admin"; it sees a channel, an optional user and an optional restaurant scope. A future POS channel builds a principal restricted to its restaurant and the service refuses any other. Tested with a restricted principal built in the test only |
| `VisitTokenCodec` | The `RefreshTokenService` pattern: cryptographic randomness, hash-only storage. A database leak exposes no usable codes |
| `VisitPayload` | One place for the format; moving to an `https` link later doesn't touch the API |
| `VisitTokenRepository.RedeemWithReviewAsync` | The core of the "one review per token" guarantee |
| `RevokeUnusedAsync` | Also changes `ConcurrencyStamp`, so revocation can't overwrite a simultaneous redemption; retries up to 3 times on conflict |
| `RateLimitingExtensions` | Fixed window partitioned by `sub` (IP when anonymous), with a 429 in the standard envelope |

---

## 5. API contract

All responses use the envelope `{ success, data, errors: [{code, message}], meta: { requestId } }`.

### Customer (`[Authorize]`, User or Admin)
| Method | Route | Body | Success |
|---|---|---|---|
| POST | `/api/v1/verification/visits/resolve` | `{ "token": "<raw code or full payload>" }` | 200 `{ restaurant: { id, name, city, address, imageUrl, averageRating, reviewCount } }` |
| POST | `/api/v1/verification/reviews` | `{ token, rating, foodRating, serviceRating, valueRating, atmosphereRating, text }` | 201 `{ reviewId, restaurantId }` |

| Status | code | When |
|---|---|---|
| 400 | `token_invalid` | Unknown, malformed, revoked, or restaurant not active / not a partner (the difference isn't revealed) |
| 410 | `token_expired` | `ExpiresAt <= now` |
| 409 | `token_used` | Redeemed before, or lost the race |
| 400 | `validation_error` | Rating outside 1–5, text > 500, an extra field (such as `restaurantId`) |
| 401 | `unauthorized` | No token |
| 429 | `rate_limited` | Limit exceeded |

**There is no** `POST /api/v1/reviews` (404) or `POST /api/v1/restaurants/{id}/reviews` (405).

### Admin (`[Authorize(Roles = "Admin")]`)
| Method | Route | Body | Success |
|---|---|---|---|
| PUT | `/api/v1/admin/restaurants/{id}/partnership` | `{ "isPartner": true }` | 200 `{ restaurantId, isActive, isPartner, partnerSince }` |
| POST | `/api/v1/admin/restaurants/{id}/visit-tokens` | `{ "count": 1–500, "expiresInDays": 1–365 (default 90) }` | 201 `{ issuanceId, restaurantId, expiresAt, payloads[] }` |
| POST | `/api/v1/admin/restaurants/{id}/visit-tokens/revoke` | `{ "issuanceId": "…" }` or `{}` | 200 `{ revokedCount }` |
| GET | `/api/v1/admin/restaurants/{id}/visit-tokens/summary` | — | 200 `{ active, redeemed, expired, revoked }` |

Errors: 401, 403, 404 `not_found`, 409 `restaurant_not_partner`, 400 `validation_error`. 403 `issuance_forbidden` exists in the service, but can't occur for Admin, which is unrestricted.

---

## 6. Database

### Migration `20261102022855_AddVisitTokens` (additive only)
- **`Restaurants`:** `IsPartner bit NOT NULL DEFAULT 0`, `PartnerSince datetime2 NULL`. All existing restaurants start as non-partners.
- **`VisitTokens`:** columns as in the plan.
  - CHECK `CK_VisitTokens_ExpiresAfterIssued`: `[ExpiresAt] > [IssuedAt]`
  - CHECK `CK_VisitTokens_RedeemerRequiresRedemption`: `[RedeemedByUserId] IS NULL OR [RedeemedAt] IS NOT NULL`
  - CHECK `CK_VisitTokens_ManualRequiresIssuer`: `[IssuanceChannel] <> 1 OR [IssuedByUserId] IS NOT NULL`
  - FKs: `Restaurants` (Cascade); `AspNetUsers` ×2 (Restrict).
  - Indexes: `TokenHash` UNIQUE, `IssuanceId`, `(RestaurantId, RedeemedAt, RevokedAt)`, + the FK indexes.
- **`Reviews`:** FK `VisitTokenId → VisitTokens` (Restrict) + `IX_Reviews_VisitTokenId` UNIQUE `WHERE [VisitTokenId] IS NOT NULL`. The Phase 4 CHECK `IsVerified = 0 OR VisitTokenId IS NOT NULL` is unchanged.

### Business rules
- Eligibility `IsActive && IsPartner` is checked at **issue, resolve and create**; ending a partnership invalidates all its existing tokens immediately.
- State precedence: used → revoked → expired → valid. At `ExpiresAt == now` the token is expired.
- A token's restaurant never changes; a review takes its restaurant from the token only.
- No tokens or partnerships are seeded.

---

## 7. Verification

### Run (2026-11-02)
| Check | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 83/83 |
| `Insaaf.API.Tests` | ✅ 47/47 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only, `Down` correct |
| Migration applied | ✅ By the founder; `migrations list` shows no Pending |
| Admin seed | ✅ API log |
| **SQL Server smoke** (script run by the founder, API on 5232) | ✅ **16/16 PASS** |

### The new tests (35)
27 Application cases (17 primitives + 10 flow, including theory cases) and 8 API tests.

### SQL Server smoke results
- Admin login 200; issue as User 403; anonymous 401.
- Issue for a non-partner 409 `restaurant_not_partner`; set partnership 200 with `partnerSince`; issue 2 tokens 201.
- Resolve 200 with the token's restaurant (Burger Makers).
- `restaurantId` in the body → 400 `validation_error`; `POST /reviews` → 404.
- **5 simultaneous creates with one token → `409,409,409,409,201`.** This is the real proof that the transaction + `ConcurrencyStamp` work on SQL Server.
- Exactly one public verified review; resolve afterwards 409 `token_used`.
- Revoke 200 (`revoked=1`); resolve of a revoked token 400 `token_invalid`.
- Summary: `active=0 redeemed=1 expired=0 revoked=1`.

### Not verified
- **410 on SQL Server:** API tests (InMemory) only; the smoke can't create an expired token without editing the database.
- **429 on SQL Server:** API tests only.
- **CHECK constraints present in the database:** confirmed from the migration file, not by querying `sys.check_constraints`. Optional founder query:
  ```sql
  SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('VisitTokens');
  SELECT name, has_filter, filter_definition FROM sys.indexes WHERE name = 'IX_Reviews_VisitTokenId';
  ```
- **The unique-violation path (2601/2627):** not hit in the smoke because `ConcurrencyStamp` stopped the duplicates first; it remains as the last guard.
- **`issuance_forbidden`:** service-level tests only, because no restricted channel exists.

---

## 8. Mobile handoff
- **Base URL:** as in Phases 3/4 (`EXPO_PUBLIC_API_BASE_URL`, paths under `/api/v1`), with Bearer through the existing Axios interceptor.
- **QR reading:** accept only `insaaf://visit/` + 43 `[A-Za-z0-9_-]` characters; anything else → "رمز QR غير صالح لإنصاف" without calling the server.
- **Figma screens:** `token_invalid` → invalid; `token_expired` (410) → "انتهت صلاحية رمز الفاتورة"; `token_used` (409) → "تم استخدام هذا الرمز مسبقًا"; `rate_limited` and network → error with retry.
- **Create:** never send `restaurantId` or any extra field (400).
- **Risks:** `expo-camera` needs a new EAS development build; a single submit protects the code; the 5 creates/minute limit.

### Temporary pilot distribution process (outside the code, not part of the architecture)
1. Admin enables the restaurant's partnership after the agreement.
2. Admin issues a batch (`count`, `expiresInDays`).
3. Operations turn each payload into a separate QR sticker.
4. They are delivered to the restaurant securely.
5. Staff attach a **different** code to each invoice.
6. A lost batch is revoked by `IssuanceId` and reissued.

### Future POS direction (not implemented)
A POS channel later is a new caller (an integration endpoint with a credential tied to one restaurant, plus per-provider adapters) that calls the same `IVisitTokenIssuer` with `Count = 1` and a restricted principal. Resolve, create, redemption and the token table **don't change**. Phase 5 has no restaurant-owner role and no POS-specific modules.

---

## 9. Known limitations (Backend)
- A payload can't be retrieved after issuing.
- There is no endpoint listing individual tokens.
- A single token can't be revoked on its own: only by issuance or for the whole restaurant.
- Reviews per user per restaurant aren't limited (D-X).
- Issuing and distribution are manual for the pilot.
- **Smoke side effects in the development database:** Burger Makers (`e7146302-9d94-45dc-aa19-7bed4b44c48e`) is a partner; one verified review "Phase 5 smoke test"; two `smoke-…@example.com` accounts; test tokens.

---

## 10. Educational summary (Backend)
- **Why a code per invoice, not a fixed restaurant QR?** A fixed QR photographed once becomes a permanent licence for fake reviews. A single-use, expiring code ties the review to a real visit.
- **Why hashes only?** The same logic as passwords: the database can check a code but can't give it back. The raw code lives on the invoice and in the customer's hand.
- **Why does the server pick the restaurant?** The code is the proof; the restaurant is a property of the code, not a user choice. So the DTO rejects `restaurantId` outright instead of ignoring it, which makes tampering visible.
- **Optimistic concurrency:** instead of locking the row, each token has a `ConcurrencyStamp`. The UPDATE requires the stamp to be unchanged since it was read; the first request succeeds and changes it, and the rest find 0 rows → 409. The transaction keeps token and review together, and the filtered unique index is a final database guarantee even if the code changes.
- **Separating who issues from how issuing works:** the controller establishes the identity, and the service applies the rules to any identity. That makes POS later an addition rather than a rewrite.
- **Clean Architecture in practice:** rules (Domain) don't know EF; services (Application) don't know HTTP or roles; cryptography and the transaction (Infrastructure) sit behind interfaces; the API stays thin: authentication, rate limiting and error mapping.

---

## 11. Mobile Track

**Completed:** 2026-11-05.

### 11.1 Overview
| ID | Task | Status | Evidence |
|---|---|---|---|
| 5.6 | Camera + scan screen | ✅ Implemented | typecheck, lint, bundles, `visitPayload` tests; founder device test (§11.7) |
| 5.7 | Verified review form | ✅ Implemented | Same checks; keyboard fix; founder device test (§11.7) |
| 5.8 | Entry point (BottomNav) | ✅ Implemented | Same checks; founder device test (§11.7) |
| 5.9 (Mobile) | Tests and verification | ⚠️ Partly documented | Automated checks recorded; itemized device results not received |

**Applied decisions:** D-N1 (two tabs only), D-F1 (all five ratings required), D-F2 (text 0–500), D-M1 (`expo-camera` + a new EAS build), D-M3 (a message and a button when the camera is denied).

**Deviations from the plan's wording:**
1. **The route:** `app/(tabs)/scan.tsx` instead of `app/scan/index.tsx`, because the screen is now a tab. The URL is still `/scan`.
2. **Component names:** `VerifiedReviewForm` instead of `ReviewForm`; `ScanStatusViews` holds `ScanErrorState` and `ScanSuccessState`; `VerifiedRestaurantResult` instead of `VerifiedRestaurantCard`; `useVerificationMutations` instead of `useVerifiedReviewMutation`.
3. **The guard:** the session check moved from `restaurants/_layout.tsx` to `app/(tabs)/_layout.tsx` so it protects both tabs.

### 11.2 Application structure (new/modified)
| File | Responsibility |
|---|---|
| `app/(tabs)/_layout.tsx` | Session guard + `Tabs` with `BottomNav` |
| `app/(tabs)/scan.tsx` | The `/scan` route |
| `app/(tabs)/restaurants/*` | Discovery screens, moved with URLs unchanged |
| `features/navigation/BottomNav.tsx` | Tab bar per Figma; hidden while the keyboard is open |
| `features/verification/screens/ScanScreen.tsx` | State machine: `scanner / result / write / success / error` |
| `features/verification/components/ScannerView.tsx` | Camera, viewfinder, scan line, permission states |
| `features/verification/components/VerifiedRestaurantResult.tsx` | The locked restaurant before reviewing |
| `features/verification/components/VerifiedReviewForm.tsx` + `RatingStarsInput.tsx` | The form and the stars |
| `features/verification/components/ScanStatusViews.tsx` | Error and success views |
| `features/verification/hooks/useVerificationMutations.ts` | `useMutation` for resolve and create |
| `services/verification/verificationApi.ts` | Axios calls and error-code mapping |
| `utils/visitPayload.ts` | QR format parsing, mirroring the server's `VisitPayload` |
| `types/verification.ts` | Contracts |

Small changes: `VerifiedBadge` gained `size`; `RestaurantDetailScreen` accepts `tab=reviews`; `app.json` has the camera plugin; `types/expo-router.d.ts` has `/scan`.

### 11.3 User journeys and states
```text
Scan → local format check → resolve → locked restaurant → form → submit → success
            │                  │                                    │
            ▼                  ▼                                    ▼
        "invalid"      error view by code                 error view, or message in the form
```
- **Loading:** a spinner over the camera during resolve, and inside the submit button.
- **Errors:** `token_invalid`, `token_expired` and `token_used` open the Figma views. A network failure at resolve opens "تعذّر التحقق من الرمز".
- **Submit failure from the network or a rate limit:** the form keeps its content and shows a message, because the code wasn't redeemed.
- **Success:** "عرض التقييم" opens the reviews tab on the restaurant page; "العودة للرئيسية" returns to `/restaurants` (there is no Home before Phase 7).
- **There is no** write-review button on the restaurant page; scanning is the only path.

### 11.4 State management
- **TanStack Query:** `useMutation` for resolve and create. On success, invalidate `['restaurants','detail',id]`, `['restaurants','list']` and `['reviews','restaurant',id]`.
- **Local state:** everything is inside `ScanScreen`. The raw code stays in memory only, never in the URL or storage.
- **`scanLocked` (ref):** the camera reports the same read repeatedly; only the first is handled.
- **Session:** the existing Axios interceptor refreshes the token; if refresh fails, the guard returns to login.

### 11.5 API data flow
`CameraView` → `ScanScreen.handleScanned` → `extractVisitToken` → `useResolveVisitMutation` → `verificationApi.resolveVisit` → `apiClient` (Axios + Bearer) → `POST /api/v1/verification/visits/resolve`. The response returns the server's restaurant to the screen. Submit follows the same path to `POST /api/v1/verification/reviews`, **without `restaurantId`**. Errors become `VerificationApiError` with a kind of `expired / used / invalid / failed`.

### 11.6 RTL and Figma
- **RTL:** `textAlignStart` and `start/end` for corners; stars are always `direction: 'ltr'`.
- **Not built from Figma:** the simulate-scan buttons (prototype only), "إضافة صور" (hidden), and 3 tabs (الرئيسية, النشاط, ملفي).
- **Our copy (no Figma equivalent):** camera permission messages, the `failed` view, and submit failure messages.
- **Intentional differences:** the result and form footers use 16 bottom padding instead of 40 because the tab bar doesn't overlay the content; `BottomNav` hides with the keyboard (Figma doesn't define this).
- **Figma note:** Figma allows submitting with only the overall rating; the app requires all five (D-F1).

### 11.7 Testing and integration
| Check | Result |
|---|---|
| `tsc --noEmit` | ✅ (rerun after the keyboard fix) |
| `expo lint` | ✅ 0 errors; 1 older warning in `services/api/client.ts` (last full run before the keyboard fix; afterwards `features/verification` only) |
| `expo export` Android | ✅ (rerun after the fix) |
| `expo export` iOS | ✅ (before the keyboard fix, not rerun) |
| `visitPayload` with Node | ✅ 12 cases + 1000 random tokens (temporary script, not in the repository) |
| Android permissions | ✅ `CAMERA` present, `RECORD_AUDIO` absent |
| Founder device test | The founder reported "Test Done" (2026-11-05) after the keyboard fix. **No item-by-item results were received.** |

**Two issues found during device testing:**
1. **`Network Error` at login:** checks showed the API was reachable on `192.168.100.3:5232`. The proposed fix was restarting Metro with `EXPO_PUBLIC_API_BASE_URL` and `--clear`. **No code change.**
2. **Review text hidden under the keyboard:** Android runs edge-to-edge, so the window doesn't shrink for the keyboard, and the form had no keyboard handling. Fix: `KeyboardAvoidingView` with `padding` + `scrollToEnd` on focus and on viewport or content size changes. No new library or build. This machine has no `adb` or emulator, so **device testing was the founder's**.

**Not verified:**
- iOS on a real device: not tested at all.
- The expired-code view on a device: 410 is covered by API tests only.
- Itemized device cases (rescanning a used code, a foreign QR, camera denial, RTL): not recorded individually.
- There are no automated tests for Mobile components; the project has no test framework for them.

### 11.8 Educational summary (Mobile)
- **Expo Router** builds navigation from files. The `(tabs)` group adds the tab bar without changing URLs.
- **One screen with internal states** instead of several routes keeps the raw code in memory and out of URLs.
- **`useMutation`** fits because resolve and submit are actions triggered by a scan or a press, not recurring reads. `invalidateQueries` makes the restaurant page fetch fresh data on its own.
- **The server is the source of truth:** the app checks only the format to save a request; validity, the restaurant and redemption are all server decisions.
- **Edge-to-edge:** when the app draws under the system bars, the system doesn't shrink the window for the keyboard, so the screen must handle it.

---

## 12. Consolidated Phase 5 status
| ID | Track | Status |
|---|---|---|
| 5.1–5.5 | Backend | ✅ Verified (130/130, smoke 16/16 on SQL Server) |
| 5.6–5.8 | Mobile | ✅ Implemented; the founder reported testing done |
| 5.9 | Both | Backend ✅; Mobile ⚠️ itemized device results not recorded |

**The Phase 5 gate:** scan → a verified review on the token's restaurant; double redemption fails; client restaurant overrides rejected; no unverified create path; codes issued only by Admin for partners. All are proven at API level; on the device, only as far as the founder reported.

### Limitations and founder actions
1. **Review photos decision:** "إضافة صور" is in Figma, while `decisions.md` and `product-spec.md` make the deferral conditional on Figma not requiring it. It is currently deferred per the approved plan, with no phase assigned. Recommendation: a separate feature with its own plan before beta.
2. **Confirm device results** item by item to close 5.9 fully.
3. **Git:** commit, push, then a PR into `develop`.
4. **Test data in the development database:** Burger Makers as a partner, test reviews, `smoke-…` accounts.

### Files changed in Phase 5 (summary)
Backend files in §2, Mobile files in §11.2, plus `docs/phase5-report.md` and `docs/project-progress.md`. One migration: `AddVisitTokens`. New dependency: `expo-camera`.
