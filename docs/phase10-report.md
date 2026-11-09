# Phase 10 — Hardening (local): consolidated report

**Date:** 2026-11-09
**Branch:** `feature/phase10-hardening` (the founder manages Git)
**References:** the roadmap (Phase 10, ADR-019), the approved Phase 10 plan (tasks 10.1–10.10, decisions D-1…D-11), [decisions.md](decisions.md) ADR-020, [runbook.md](runbook.md)
**Report status:** ✅ **Backend and Mobile work complete and verified locally** · ⏳ **Open:** CI result on GitHub (needs the founder's push), the Android device check of the thumbnails and the Figma pass, the login footer fix on the device

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.
> Phase 10 mixes Backend and Mobile work by area (approved plan, §2), so it has one consolidated report.

---

## 1. Overview

### Business purpose
Phase 10 makes the project safe and repeatable before any release, using only what exists today: one Windows machine, SQL Server and an Android phone (ADR-019). Hosting, iOS and the beta wait in Phase 11.

The gate:
- The security review is done, and its findings are fixed or recorded.
- SQL Server integration tests can be re-run.
- The performance and Figma reviews are done on Android.
- CI runs build, tests and lint.
- The project is prepared for hosting, without deploying it.

### Task status
| ID | Task | Track | Status | Evidence |
|---|---|---|---|---|
| 10.1 | Security review; fix F-2, F-3, F-7, F-8 | Backend | ✅ Verified | Findings table §7.1; `SecurityHardeningTests` (headers, signing-key guard ×4, auth 429 ×3); a production-shaped startup run locally |
| 10.2 | Dependency checks | Both | ✅ Verified | Backend: no vulnerable packages. Mobile: the founder ran `npm audit fix` (non-breaking). Typecheck, lint and both bundles pass; triage §7.2 |
| 10.3 | Deferred decisions D-3, D-4, D-5 | Backend | ✅ Recorded | ADR-020 (all three kept as they were); suspension behaviour covered by an integration test |
| 10.4 | SQL Server integration tests | Backend | ✅ Verified | `tests/Insaaf.IntegrationTests`: all passed on `Insaaf_IntegrationTests` in 3 full runs (21, 21, then 22 after the thumbnail test was added); skipped without a connection string; refuses any other database |
| 10.5 | Performance and thumbnails (D-7) | Both | ✅ Verified (Backend) · ⏳ device | Command counts §7.4; no missing-index suggestions; thumbnails tested (Application, API, SQL Server). Not yet seen on the device |
| 10.6 | UI review against Figma | Mobile | ✅ Code review · ⏳ device | Per-screen table §7.5. Four Figma items aren't built; the founder decided to build them in the next phase |
| 10.7 | CI | Both | ✅ Changed · ⏳ not run on GitHub | `ci.yml` steps §5. Not run yet, because the founder pushes |
| 10.8 | Prepare for hosting (no deployment) | Backend | ✅ Verified locally | Health probes tested; production config template; startup with environment variables; Dockerfile **not built** (no Docker on this machine) |
| 10.9 | Test-data cleanup | Backend | ✅ Verified | The founder ran the script: dry run, then `-Apply`, one transaction committed, 5 photo folders removed |
| 10.10 | Tests and report | Both | ✅ | Release build: 0 warnings, 0 errors. 353/353 tests, plus 22/22 integration tests. Both bundles built. This report |

### Applied decisions
| # | Decision | Implementation |
|---|---|---|
| D-1 | Task IDs 10.1–10.10 | ✅ |
| D-2 | Non-breaking `npm audit fix` only | ✅ Run by the founder; zod 4 and Expo SDK 58 deferred |
| D-3 | Suspension: keep the 15-minute access token | ✅ ADR-020 |
| D-4 | Admin audit: keep log lines | ✅ ADR-020 |
| D-5 | Token lifetimes: 15 min / 7 days | ✅ ADR-020 |
| D-6 | A separate `Insaaf_IntegrationTests` database, created by the founder; the tests never migrate | ✅ |
| D-7 | 640 px card thumbnails, no migration | ✅ |
| D-8 | Cleanup script with a dry run, run by the founder | ✅ |
| D-9 | No mobile unit-test framework now | ✅ (typecheck, lint, bundles) |
| D-10 | Auth limits per IP: login 10, register 5, refresh 30 per minute | ✅ |
| D-11 | Stray folders deleted by the founder | ⏳ Founder (F-9) |

---

## 2. Architecture

Phase 10 adds no new layer and keeps the dependency direction: Domain ← Application ← Infrastructure ← API.

| Area | Files | Layer |
|---|---|---|
| Rate limits | `Insaaf.API/Extensions/RateLimitingExtensions.cs` (`auth-login`, `auth-register`, `auth-refresh`, per IP), `AuthController.cs` | API |
| Signing-key guard, security headers | `Insaaf.API/Extensions/SecurityExtensions.cs`, `Program.cs` | API |
| Health checks | `Insaaf.Infrastructure/Health/DatabaseHealthCheck.cs`; `/health/live`, `/health/ready` in `Program.cs` | Infrastructure / API |
| Reverse proxy, HSTS | `Program.cs` (forwarded headers only for `ReverseProxy:KnownProxies`; HSTS outside Development) | API |
| Configuration | `appsettings.json` (`Server=localhost`), new `appsettings.Production.json` (no secrets) | API |
| Container | `src/backend/Dockerfile`, `.dockerignore` | — |
| Linux images | `SkiaSharp.NativeAssets.Linux.NoDependencies` in `Insaaf.Infrastructure.csproj` | Infrastructure |
| Thumbnails | `IImageProcessor` (`ProcessedImage.ThumbnailJpeg`), `IPhotoStorage.SaveThumbnailAsync`, `ReviewPhotos.ThumbnailKey`, `ReviewPhotoDto.ThumbnailUrl`, `ReviewPhotoService`, `ReviewPhotoCleanupService`, `SkiaImageProcessor`, `LocalPhotoStorage`, `ReviewPhotosController` | Application / Infrastructure / API |
| Integration tests | `tests/Insaaf.IntegrationTests/` (new project in the solution) | Tests |
| Mobile | `types/review.ts` (`thumbnailUrl`), `ReviewPhotoStrip.tsx` (cards use it), `package.json` (lint covers `utils`; audit-fix versions) | Mobile |
| Operations | `docs/runbook.md` (draft), `docs/scripts/cleanup-test-data.ps1` | Docs |

---

## 3. End-to-end data flow

### A sign-in request
1. Phone → `POST /api/v1/auth/login`.
2. The rate limiter partitions by the client IP. With known proxies, forwarded headers set the real client IP first.
3. The 11th request in a minute from one IP gets `429` with the usual envelope (`rate_limited`).
4. Otherwise `AuthService` runs as before: lockout, then suspension (after a correct password), then tokens.
5. Every response gets `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and the CSP `default-src 'none'; frame-ancestors 'none'` (not on Swagger).

### A photo for a card
1. **Upload:** `SkiaImageProcessor` decodes and re-encodes as before. It also draws the oriented, metadata-free result at most 640 px on its longest side (JPEG q80). Images already that small reuse the same bytes.
2. **Writing:** `ReviewPhotoService.RunAttemptAsync` writes `{leaseId}.thumb.jpg`, then `{leaseId}.jpg`. Then the fenced `CompleteAsync` decides, as before. If either write fails, the attempt is abandoned.
3. **Reading:** `ReviewPhotoDto` now carries `thumbnailUrl = /api/v1/review-photos/{id}?size=thumb`.
4. **Serving:** `GET …?size=thumb` → `GetServableKeyAsync`, with the same moderation rules → derived thumbnail key → file. If the file is missing (photos from before Phase 10), the full image is served. Any other `size` is 404.
5. **Cleanup:** keeps the served file and its thumbnail, and sweeps both for superseded attempts. Folder deletion is unchanged.

### An integration test
1. `SqlServerApiFactory` refuses to start unless the connection string's database name contains `IntegrationTests`. After the host starts it checks again: is the `DbContext` really on that database? Are there pending migrations? Either problem stops the run with a message, and nothing is applied.
2. Tests drive the real API over HTTP, as the smoke scripts did. Concurrency tests call the photo repository directly, one `DbContext` per caller.
3. Every account is `it-{run}-…@example.com` and every restaurant `IT-{run} …`. At the end, one transaction deletes them and their dependents in foreign-key order. Leftovers fail the run.

---

## 4. Important code

| Code | Why it exists |
|---|---|
| `SecurityExtensions.EnsureProductionSigningKey` | A host started outside Development with the committed key, or a short key, would sign tokens anyone can forge. It now fails at startup instead |
| `RateLimitingExtensions.PerIpFixedWindow` | Per-account lockout doesn't stop password spraying across accounts or mass sign-ups |
| `DatabaseHealthCheck` | Readiness for a future host: `Unhealthy` with no details when SQL Server can't be reached |
| `ReviewPhotos.ThumbnailKey` | Derives the thumbnail's key from the stored key, so no column or migration is needed |
| `SqlServerApiFactory` | The real API on a dedicated database, with two guards and run-tagged cleanup |
| `TestDataCleanup` | The same deletion order as the founder's cleanup script, limited to one run's tags |
| `SqlCommandCounter` | Counts EF Core commands per request, for the N+1 test |

---

## 5. API contract changes

| Change | Details |
|---|---|
| `POST /auth/login`, `/auth/register`, `/auth/refresh` | Can now return `429` `rate_limited` (per IP, per minute: 10 / 5 / 30; configurable under `RateLimiting:Auth*`) |
| `ReviewPhotoDto` | **Added** `thumbnailUrl` (relative). Existing fields unchanged |
| `GET /api/v1/review-photos/{id}?size=thumb` | **New query value.** Same rules, `private, no-cache`, its own `ETag`. Any other `size` → 404 |
| `GET /health/live`, `GET /health/ready` | New. Plain text `Healthy` / `Unhealthy`, no details. Anonymous |
| All responses | Security headers (above) |
| Production | Swagger off; HSTS and HTTPS redirection on; the API refuses a weak or committed key |

**CI (`.github/workflows/ci.yml`):**
- Backend: Release build and tests, as before. Integration tests are skipped there because there's no connection string.
- Report-only: `dotnet list package --vulnerable`.
- Mobile: typecheck and lint as before, plus `expo export` for Android and iOS.
- Report-only: `npm audit --omit=dev --audit-level=critical`.

---

## 6. Database

- **No migration in Phase 10.**
- **The founder created `Insaaf_IntegrationTests`** with `dotnet ef database update --connection …` (13 migrations; `READ_COMMITTED_SNAPSHOT` on, which a test checks). After each run it holds only the 12 seeded restaurants and 10 categories.
- **Development database after 10.9:** 2 accounts, 12 restaurants, 0 reviews. The founder chose to also remove the 2 test-account reviews on the seeded restaurant `e7146302`.
- **New business rule (D-7):** a thumbnail is never served for a photo that isn't servable. The same `GetServableKeyAsync` decides for both sizes.

---

## 7. Verification

### Run (2026-11-09)
| Check | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 warnings, 0 errors |
| `Insaaf.Application.Tests` | ✅ 253/253 |
| `Insaaf.API.Tests` | ✅ 100/100 |
| `Insaaf.IntegrationTests` without a connection string | ✅ 22 skipped |
| `Insaaf.IntegrationTests` on SQL Server | ✅ 22/22 (last full run; earlier runs 21/21 ×2) |
| Integration tests pointed at `Insaaf` | ✅ Refused before touching anything |
| Mobile `tsc` | ✅ |
| Mobile lint | ✅ 0 errors, 1 warning (`import/no-named-as-default-member` in `services/api/client.ts`, an untouched file; likely from the updated lint plugin) |
| `expo export` Android and iOS | ✅ |
| Production-shaped startup (environment variables) | ✅ Dev key refused; strong key: probes 200, Swagger 404, headers present |

**Correction:** the earlier chat message said 349 tests after the health tests. The real count then was **348** (249 + 99). It is 353 now, because of the thumbnail tests.

### 7.1 Security findings
| # | Finding | Result |
|---|---|---|
| F-1 | SkiaSharp had no Linux natives, so CI would fail | Fixed (package added); ⏳ confirmed only by the CI run |
| F-2 | Machine name in the committed connection string | Fixed (`localhost`) |
| F-3 | No rate limit on auth routes | Fixed and tested |
| F-4 | Mobile advisories | Triaged (§7.2) |
| F-5 | Backend packages | None vulnerable |
| F-6 | Full-size images on cards | Fixed (thumbnails) |
| F-7 | Committed dev key usable outside Development | Guarded and tested |
| F-8 | No security headers | Fixed and tested |
| F-9 | Stray folders `/Insaaf.Infrastructure/obj`, `src/backend/src/backend` | ⏳ Founder deletes them |
| F-10 | No mobile unit tests | Accepted (D-9) |

### 7.2 Remaining mobile advisories (after `npm audit fix`: 18, 5 high, 13 moderate, none critical)
| Package | Runtime? | Exploitable here? | Fix |
|---|---|---|---|
| `zod` 3 (high) | Yes: form validation | No: it only validates what the user types on their own phone | zod 4 (breaking) |
| `query-string` / `decode-uri-component` via `expo-router` (high) | Yes: URL parsing | Low: a crafted deep link could freeze the app on the device that opens it | Expo SDK 58 |
| `glob`, `minimatch`, `@expo/fingerprint` (moderate) | No: build tooling | No | Expo SDK 58 |
| `brace-expansion`, ESLint chain, `@expo/ngrok`/`uuid` | No: development only | No | Tooling updates |

`npm audit fix` placed `expo-router@58` **nested under `@expo/cli`** (used for export and the dev server). The app's own `expo-router` is still 57.0.30, and both bundles build. `expo install --check` lists four patch updates (`expo-dev-client`, `expo-location`, `expo-secure-store`, `expo-splash-screen`); not applied, because that's outside scope.

### 7.3 Integration tests (22)
- **Database:** readiness probe; RCSI on.
- **Auth:** refresh rotation and reuse; logout; username uniqueness regardless of case (SQL collation); suspension and lifting it.
- **Verification:** ratings update and a token is single-use; **5 parallel redemptions of one token → exactly one review**.
- **Social:** 5 parallel likes → 1; comments and ownership; parallel follows → 1; feed cursor paging without repeats; notifications.
- **Moderation:** hide/unhide with rating effects; Admin comment delete; deactivation; role checks.
- **Photos over HTTP:** idempotency, 415, claim, limit, freed slot; attach order, serving, `ETag`/304, thumbnail, removal, hidden review.
- **Photo concurrency** (the Phase 9A probes): 10 parallel reservations → 5; 5 repeats of one upload id → 1 row; 3 lease takes → 1 winner; parallel tombstone and purge release every slot.
- **N+1** (§7.4).

### 7.4 Command counts per read (1 reviewer → 4 reviewers with photos, likes, comments and follows)
| Read | Commands |
|---|---|
| `GET /restaurants` | 2 → 2 |
| `GET /restaurants/{id}` | 1 → 1 |
| `GET /restaurants/{id}/reviews` | 3 → 3 |
| `GET /reviews/{id}` | 1 → 1 |
| `GET /reviews/{id}/comments` | 3 → 3 |
| `GET /feed` | 1 → 1 |
| `GET /notifications` | 1 → 1 |
| `GET /notifications/unread-count` | 1 → 1 |
| `GET /users/{id}` | 1 → 1 |
| `GET /users/{id}/reviews` | 2 → 2 |
| `GET /users/{id}/followers` | 3 → 3 |
| `GET /users/{id}/taste` | 3 → 3 |
| `GET /home/suggested` | 3 → 3 |
| `GET /home/trending` | 2 → 2 |
| `GET /admin/reviews` | 1 → 1 |

No count grows with the data, so there is no N+1. SQL Server reported no missing-index suggestions for either database. **Limitation:** the data is small, so query plans at real volume are not verified.

**Mobile lists:** every long list is a `FlatList` with `keyExtractor` and server paging (feed, notifications, profile reviews, followers, search, comments). No memoisation was added without a measured problem on the device.

### 7.5 Figma review (code against the Figma Make source, all 16 screens and the shared components)
| Screen | Result |
|---|---|
| Home | Matches. Recorded differences: no "تقييمات حديثة" (D-3), no verified sheet (D-9), "عرض الكل" only where a destination exists |
| ReviewCard | Matches. No save or official reply; the verified badge isn't tappable (D-9) |
| Restaurant | Matches, **except the photos tab "صور المجتمع"** (not built; deferred while there were no photos) |
| Review Detail | Matches (strings, ratings, photos, comments, input). No save, reply or comment-like |
| Activity | Matches. The unread indicator follows the founder's 8A.3 decision |
| Profile / other profile | Matches. No المحفوظات or "مشاركة الملف" (Phase 8 D-8) |
| Edit Profile | Matches, including the bio counter |
| Settings | Only sign-out (Phase 8 D-6) |
| Explore, Search, QR flow, Auth (login, register, forgot), Bottom nav | Matches: every Figma string is present |
| **Onboarding** | **Not built** (no recorded decision) |
| **Splash** (designed screen) | **Not built.** The native splash is used |
| **Taste Setup** | **Not built** (no recorded decision; needs Backend storage) |

**Founder decision (2026-11-09):** build all the missing screens in a new, final phase.

### Not verified
- The CI run on GitHub (no push yet), including F-1 on Linux.
- Thumbnails and the whole Figma pass on the Android device.
- The login footer fix (from Phase 9A) on the device.
- The Docker image (Docker isn't installed).
- iOS (Phase 11).
- Query plans at production volume.

---

## 8. Mobile handoff
- `ReviewPhotoDto.thumbnailUrl` is always present for new responses. Cards use it; Review Detail uses `url`.
- No native change in Phase 10, so no new EAS build is needed. A Metro reload and an API restart are enough.
- Auth requests can now get `429`. The existing error envelope message is shown.

---

## 9. Educational summary

**Defence in layers:**
- Identity lockout protects one account.
- The new per-IP limits protect against attacks across many accounts.
- The signing-key guard protects every token.
- Security headers stop browsers from misusing responses, even though the main client is a phone.

Each layer is small, and none depends on the others.

**Liveness versus readiness:**
- `live` says the process answers.
- `ready` says it can do its job, because it can reach the database.

A host restarts on the first failing and stops sending traffic on the second, so the API itself does nothing differently.

**Why integration tests need a real database:**
- InMemory can't show races, unique-index behaviour, collation or locking.
- The new tests prove them on SQL Server: one token redeemed once among five parallel attempts, one like among five, five photo slots among ten.

The two guards (the database name, and the check after startup) make a dangerous misconfiguration fail loudly instead of quietly writing to development data.

**N+1 as a count, not a guess:** counting commands at two data sizes turns "it looks fine" into a test that fails when someone later adds a lazy query inside a loop.

**Thumbnails without a migration:** the thumbnail's location is a pure function of the photo's stored key. No column is added, and every existing rule applies unchanged, because the database still names only one file per photo:
- the fence decides;
- moderation uses the same servable check;
- cleanup works per folder.

---

## 10. Consolidated Phase 10 status

### Open items and founder actions
1. Push the branch, so CI runs; send the result (10.7, F-1).
2. Android: a new photo on a review card loads (thumbnail); the login footer line; a general look against Figma.
3. Delete the stray folders (F-9).
4. After every future migration, also apply it to `Insaaf_IntegrationTests` (see the runbook).
5. Next: the new final phase for the missing Figma screens (founder decision), planned and approved before any code.

### Files changed in Phase 10 (summary)
- **Backend:** `AuthController.cs`, `RateLimitingExtensions.cs`, `SecurityExtensions.cs` (new), `Program.cs`, `appsettings.json`, `appsettings.Production.json` (new), `Insaaf.Infrastructure.csproj`, `Health/DatabaseHealthCheck.cs` (new), photo files listed in §2, `ReviewPhotosController.cs`, `Dockerfile` and `.dockerignore` (new), `Insaaf.sln` (new test project).
- **Tests:** `SecurityHardeningTests.cs`, `HealthEndpointsTests.cs` (new), `CustomWebApplicationFactory.cs`, `PhotoEndpointsTests.cs`, `ImageProcessingTests.cs`, `ReviewPhotoTests.cs`, `FakePhotoStorage.cs`, `tests/Insaaf.IntegrationTests/**` (new).
- **Mobile:** `types/review.ts`, `ReviewPhotoStrip.tsx`, `package.json`, `package-lock.json`.
- **CI and docs:** `.github/workflows/ci.yml`, `docs/runbook.md`, `docs/scripts/cleanup-test-data.ps1`, `docs/decisions.md` (ADR-020), this report, `project-progress.md`, `implementation-plan.md`.
