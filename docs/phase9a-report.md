# Phase 9A — Review photos: consolidated report

**Date:** 2026-11-08
**Branch:** `feature/phase9a-review-photos` (from `develop` after the Phase 9 merge; the founder manages Git)
**References:** the roadmap (Phase 9A), the approved Phase 9A plan (revision 5, decisions D-1…D-17), [decisions.md](decisions.md) ADR-011/ADR-017/ADR-018
**Report status:** ✅ **Backend Track complete** · ✅ **Mobile Track complete, device-tested on Android** (§11) · ✅ Phase 9A merged into `develop` (PR #11, `a7ddaa0`)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
A QR code is single-use and reviews can't be edited (ADR-011), so photos must ship before beta, or early reviews stay photo-less forever. Phase 9A lets the holder of a valid visit token attach up to five photos to the verified review it creates. Photos are moderated from day one: hiding a review hides its photos, and Admin can remove any photo. The gate: "A verified review can include photos, shown on review cards and Review Detail, and Admin can remove any photo."

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 9A.1 | `ReviewPhoto`, migration, storage and processing | ✅ Verified | Migration `AddReviewPhotos` reviewed, SQL parsed, applied by the founder; 10 processing/storage tests; smoke |
| 9A.2 | Upload for a valid, unredeemed token; uploader's own delete | ✅ Verified | 18 service tests; 5 API tests; SQL Server probes; smoke |
| 9A.3 | `photoIds` on review creation; photos on reads; cleanup | ✅ Verified | Tests; probes (race and command counts); smoke |
| 9A.4 | Admin removal of a photo | ✅ Verified | Tests (honest `fileDeletion` and audit); smoke |
| 9A.7 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 340/340 tests; probes 7/7 (×4 runs); smoke 16/16 PASS |

### Applied decisions
| # | Decision | Implementation |
|---|---|---|
| D-1 | Storage | ✅ Local disk behind `IPhotoStorage`; cloud in Phase 11 |
| D-2 | Image library | ✅ SkiaSharp 4.155.0 (MIT) |
| D-3 | Limits | ✅ 5 photos; JPEG/PNG/WebP in, JPEG q85 out; 5 MB; longest side 2048 px |
| D-6 | Image route | ✅ Anonymous, served only for public reviews |
| D-7 | Unattached lifetime | ✅ 24 h; hourly cleanup |
| D-8 | Tapping a photo | Mobile (no gallery) |
| D-9 | Token claim | ✅ The first upload claims the token; others can't upload or redeem it |
| D-10 | Uploader's own delete | ✅ `DELETE /verification/photos/{id}` |
| D-11 | Cache policy | ✅ `private, no-cache` + `ETag` + `nosniff` |
| D-13 | Idempotency | ✅ `clientUploadId`, unique index, processing lease |
| D-15 | Delete response and audit | ✅ `removed` + `fileDeletion`; `succeeded` / `removed_file_pending` |
| D-17 | Tombstones | ✅ `Purged` rows kept 30 days, pruned only with an empty folder |

D-4, D-5, D-12, D-14 and D-16 apply to the Mobile Track.

### Deviations from the plan's wording (same contract, recorded honestly)
1. **Storage keys are `{photoId}/{leaseId}.jpg` relative to the root.** The plan wrote `review-photos/{photoId}/{leaseId}.jpg`. The default root already ends in `review-photos`.
2. **The cleanup index is unfiltered,** on `(Status, ReviewId, UpdatedAt)`. SQL Server filtered indexes don't allow `OR`, which made the first `database update` fail. SQL Server rolled that attempt back; the migration was regenerated and applied.
3. **Counter repair runs per token, with the token locked first** (see §7, "Defect found and fixed").
4. **Application code doesn't log upload failures.** Application has no logging package; failures leave tracked rows that cleanup recovers. The hosted cleanup logs counts only.
5. **The ETag is derived from the storage key** (photo id and attempt id); it contains no path.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to SkiaSharp, file-system types, `IFormFile`, `HttpContext`, claims, EF Core, ASP.NET Core or `ILogger` in Domain and Application.

### Phase 9A Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/ReviewPhoto.cs`, `Enums/ReviewPhotoStatus.cs`; `Entities/VisitToken.cs` (modified) | Photo row; `PhotoClaimedByUserId`, `PhotoSlotsUsed` |
| Domain | `Repositories/IReviewPhotoRepository.cs` (+ `PhotoReservationResult`, `RepositoryBusyException`); `IVisitTokenRepository.cs` (modified) | Contracts; redemption takes `photoIds` |
| Domain | `Projections/ReviewPhotoProjection.cs`; review projections (modified) | Photos on reads |
| Application | `Photos/Abstractions/IPhotoStorage.cs`, `IImageProcessor.cs` | Ports |
| Application | `Photos/ReviewPhotoService.cs` | Upload, idempotency, fenced attempts, own delete, serving |
| Application | `Photos/ReviewPhotoRemoval.cs` | Tombstone → folder delete → purge |
| Application | `Photos/ReviewPhotoCleanupService.cs` | Recovery, sweep, prune, repair |
| Application | `Verification/VerificationService.cs`, `Admin/ReviewModerationService.cs` (modified) | `photoIds`; Admin removal |
| Infrastructure | `Photos/LocalPhotoStorage.cs`, `SkiaImageProcessor.cs`, `PhotoStorageOptions.cs`, `ReviewPhotoCleanupHostedService.cs` | Disk, processing, root guard, timer |
| Infrastructure | `Repositories/ReviewPhotoRepository.cs`, `SqlDeadlockRetry.cs`; `VisitTokenRepository.cs`, `ReviewRepository.cs` (modified) | Conditional SQL, lock order, retries, atomic redemption, photos on reads |
| Infrastructure | `Configurations/ReviewPhotoConfiguration.cs`; `VisitTokenConfiguration.cs` (modified); migration `20261108051926_AddReviewPhotos` | Schema |
| API | `VerificationController.cs` (modified), `ReviewPhotosController.cs`, `AdminModerationController.cs` (modified), `Program.cs` (root guard), `RateLimitingExtensions.cs` | Routes |

---

## 3. End-to-end data flow

### Upload: `POST /api/v1/verification/photos`
1. The controller checks the caller and the 5 MB limit, then reads the file into memory.
2. `ReviewPhotoService` validates `clientUploadId` and the token, using the same rules as review creation.
3. It looks up `(token, user, clientUploadId)`:
   - `Ready` → returns the same photo (200).
   - `Pending` → takes over through the lease, or 409.
   - `Deleting` / `Purged` → 410.
4. For a new photo, SkiaSharp processes it in memory; a bad file gets 415 before anything is written.
5. One transaction locks the token row and reserves a slot. It takes the claim with a conditional `UPDATE`, then inserts the `Pending` row with its lease.
6. The file is written as this attempt's own `{leaseId}.jpg`, via a temp file and a no-overwrite rename.
7. **The fence:** a conditional `UPDATE` sets `Ready` and `StorageKey`, only while the attempt holds the lease. The response is 201.
8. If the save fails, one statement tombstones the row and clears its own lease. Then the folder is deleted and the row purged.

### Review with photos: `POST /api/v1/verification/reviews`
1. The service pre-checks the claim and each photo, for clear errors.
2. Then one transaction, token first:
   - a conditional `UPDATE` redeems the token, enforces the claim and releases `@photoCount` slots;
   - the review is inserted;
   - each photo is attached by a conditional `UPDATE` (same token, same user, unattached, `Ready`).
3. A count mismatch rolls everything back, so the token stays unredeemed (400 `invalid_photo`).

### Serving: `GET /api/v1/review-photos/{id}`
- The photo must be `Ready` and attached, and its review must pass `ReviewVisibility.IsPublic` in an active restaurant. Otherwise the answer is 404.
- The file is streamed with `private, no-cache`, `ETag` and `nosniff`; `If-None-Match` gets 304.

### Removal: Admin, the uploader, or cleanup
1. Tombstone: lock the token, set `Deleting`, clear any lease. The photo is invisible at once.
2. `DeleteFolderAsync`.
3. Purge: lock the token, set `Purged`, and release the slot and claim if the photo was unattached.

If any step fails, the row stays `Deleting` and cleanup retries it.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| `ReserveAsync` conditional `UPDATE` | The 5-photo limit and the claim can't be bypassed by parallel requests |
| `CompleteAsync` (the fence) | Only the current lease holder can choose the served file; a stale writer can't |
| Attempt-named files | Nothing is ever overwritten, so there's no check-then-act race on files |
| `AbandonAttemptAsync` | A failed attempt releases its own lease and tombstones in one statement |
| `RedeemRelationalAsync` | Redemption, claim, slot release and attachment commit together, or not at all |
| `SqlDeadlockRetry` | Whole-transaction retry on SQL error 1205; storage is never inside it |
| `RepairTokenAsync` | Per-token lock, then a separate counting statement (safe under `READ_COMMITTED_SNAPSHOT`) |
| `SkiaImageProcessor` | Decodes, applies orientation, resizes, flattens, re-encodes; no metadata survives |
| `PhotoStorageOptions.EnsureOutsideContentRoot` | The API refuses a storage root inside the repository |

---

## 5. API contract

| Method | Route | Auth | Success |
|---|---|---|---|
| POST | `/api/v1/verification/photos` | User | 201 or 200 `{ id, width, height }`. Multipart: `token`, `clientUploadId`, `file` |
| DELETE | `/api/v1/verification/photos/{id}` | User (uploader; unattached only) | `{ photoId, removed: true, fileDeletion: "completed" \| "pending" }` |
| POST | `/api/v1/verification/reviews` | User | Existing body plus optional `photoIds: [guid]` (≤ 5, distinct, in display order) |
| GET | `/api/v1/review-photos/{id}` | Anonymous | `image/jpeg`; `Cache-Control: private, no-cache`; `ETag`; `X-Content-Type-Options: nosniff`; 304 on `If-None-Match`; 404 when not servable |
| DELETE | `/api/v1/admin/photos/{id}` | Admin | `{ photoId, removed: true, fileDeletion }`; audit `photo.remove` → `succeeded` / `removed_file_pending` / `not_found` |
| *(reads)* | `GET /reviews/{id}`, `/restaurants/{id}/reviews`, `/feed`, `/users/{id}/reviews` | User | Each item gains `photos: [{ id, url, width, height }]`, in order; `url` = `/api/v1/review-photos/{id}` |

**Errors:**
- 400 `validation_error`, `invalid_photo`.
- 403 `token_claimed`.
- 409 `photo_limit`, `upload_in_progress`.
- 410 `photo_removed`.
- 413 `file_too_large`.
- 415 `unsupported_media_type`.
- 500 `upload_failed`.
- 503 `try_again`.
- The existing token errors.

**Limits:**
- 6 MB request size, 5 MB file.
- Rate limit `verification-photo`: 30/min per user (`RateLimiting:VerificationPhotoPerMinute`).

**Storage configuration:**
- `Storage:ReviewPhotos:RootPath` (or `Storage__ReviewPhotos__RootPath`). The default is `%LOCALAPPDATA%\Insaaf\review-photos`.
- `Storage:ReviewPhotos:CleanupIntervalMinutes` (default 60; 0 disables).

---

## 6. Database

### Migration `20261108051926_AddReviewPhotos` (additive only)
- **`ReviewPhotos`:**
  - Columns per the plan (§4).
  - CHECKs: status 0–3, `SortOrder` 0–4, `Ready` needs a `StorageKey`, attached needs a `SortOrder`.
  - FKs to `Reviews`, `VisitTokens` and `AspNetUsers`, all **Restrict (NO ACTION)**.
  - Unique `(VisitTokenId, UploadedByUserId, ClientUploadId)`.
  - Indexes: `(ReviewId, SortOrder)`, `(VisitTokenId, Status)`, `(Status, ReviewId, UpdatedAt)`, `UploadedByUserId`.
- **`VisitTokens`:** `PhotoClaimedByUserId` (NULL, no FK), `PhotoSlotsUsed` (int, default 0, CHECK 0–5).
- **Existing data unchanged:** every existing token has no claim and 0 slots, so it behaves exactly as in Phase 5.

### Business rules
- **Uploads** need a token a review could still be created with. They never redeem it.
- **The claim:** the first upload claims the token. While the uploader has a live photo row, nobody else can upload or redeem with it.
- **Slots:** `PhotoSlotsUsed` = unattached live rows (`Pending`, `Ready`, `Deleting`). It changes only in the same transaction as those rows.
- **Ratings:** photos never change ratings.
- **Visibility:** a hidden review, or one in an inactive restaurant, serves no photos.
- **Tracking:** a photo's folder is tracked by its row until confirmed empty. `Purged` tombstones keep tracking it for 30 days.

---

## 7. Verification

### Run (2026-11-08)
| Check | Result |
|---|---|
| Baseline before changes | ✅ 307/307 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 249/249 |
| `Insaaf.API.Tests` | ✅ 91/91 |
| Layer check (grep) | ✅ 0 matches |
| Migration review + `SET PARSEONLY ON` check of its SQL | ✅ Additive; 0 syntax errors |
| Migration applied | ✅ By the founder; `migrations list` shows no Pending; table and columns confirmed |
| **SQL Server probes** | ✅ **7/7, run 4 times** |
| **SQL Server smoke (API)** | ✅ **16/16 PASS** |
| Invariant across the whole database after all runs | ✅ 0 tokens where `PhotoSlotsUsed` ≠ unattached live rows |

### The new tests (33)
**28 Application cases:**
- **Processing:**
  - GPS and EXIF removed (a JPEG built in the test with a GPS block);
  - orientation 6 applied to pixels;
  - PNG and WebP accepted; resize to 2048;
  - text renamed as `.jpg`, GIF and empty input refused.
- **Storage:**
  - each attempt gets its own file, never overwritten;
  - keys outside the layout refused; idempotent deletes;
  - the root guard;
  - the default root is a per-user folder.
- **Uploads and claim:**
  - a Ready photo; the claim taken; never redeemed;
  - a same-id repeat returns the same photo with no extra slot;
  - another user's upload and review refused;
  - the 6th upload refused, and a delete frees its slot;
  - bad content refused before reservation.
- **Attachment:**
  - in order; slots released; photos on the detail; rating unchanged;
  - cross-token, cross-user and unknown ids refused with the token kept;
  - duplicates and more than 5 refused.
- **Failures:**
  - a save failure ends `Purged` and releases the slot and claim;
  - a save failure plus a delete failure stays `Deleting` until cleanup finishes.
- **Leases and the fence:**
  - a live lease gives 409; after expiry a retry completes;
  - a stale lease can't complete or change `StorageKey`;
  - an abandoned attempt blocks takeover and completion.
- **Removal:**
  - Admin removal reports `pending` vs `completed` honestly, with matching audit results;
  - a hidden review stops serving, and unhide restores it;
  - the uploader can't delete an attached photo.
- **Cleanup:**
  - 24 h expiry;
  - late-writer sweep in a `Purged` folder;
  - superseded-file sweep that keeps the served file;
  - prune after 30 days.
- **Model:** no cascading FK on `ReviewPhotos`.

**5 API tests:**
- 401/403/404 matrix.
- Full multipart flow on a QR token:
  - 201 then 200;
  - attach;
  - detail URL, with no path or `storageKey` in JSON;
  - image headers and 304;
  - Admin removal → 404.
- Refusals: 415, 413, 403 `token_claimed` for upload and review, with the token still resolvable by its claimant.
- Own delete versus someone else's.
- Photos on feed and restaurant reviews.

### SQL Server probes (direct repositories; only probe tokens on the inactive Smoke9 test restaurant)
| Probe | Result (each of 4 runs) |
|---|---|
| 10 parallel uploads on one token | Exactly 5 reserved, 5 limit refusals, `PhotoSlotsUsed = 5` |
| 5 parallel repeats of one `clientUploadId` | 1 row, 4 duplicates, 1 slot |
| 3 parallel lease takes on one `Pending` row | Exactly 1 winner |
| 100 mixed parallel operations on one token (uploads, tombstone+purge, repair) | Invariant held; 0 operations still busy after retries |
| Redemption by A racing an upload by B on an unclaimed token | One clean winner each time (the upload won all 4 runs: redeem → `TokenClaimed`) |
| Reads with photos | List 2 commands, feed 1, user reviews 1 |
| End of run | 0 live probe photos, 0 held slots |

### Defect found and fixed during this track
- **What the first probe run found:** a counter repair running alongside uploads set `PhotoSlotsUsed` too low, which later let a token reach 6 live photos and violate the CHECK.
- **Cause:** the database uses `READ_COMMITTED_SNAPSHOT`, so the single repair statement counted photos from a snapshot taken before it locked the token.
- **Fix:** repair now runs per token: one statement locks the token, and a separate statement counts. Every writer for that token holds the token lock until it commits, so nothing is missed. A failing token no longer stops the others.
- **Cleanup:** the earlier probe rows (25) were purged through the repository.
- **Now:** 4 later runs passed, and the whole database holds the invariant.

### SQL Server smoke results (16/16)
- **Setup:** Admin login; a dedicated test restaurant made a partner, with 3 tokens.
- **Uploads:**
  - 201, then 200 with the same id;
  - fake image 415;
  - another user on the claimed token 403 `token_claimed`;
  - 6th upload 409 `photo_limit`;
  - own delete `completed`.
- **Storage:** files under `C:\Users\Lenovo\AppData\Local\Insaaf\review-photos\…`, outside the repository.
- **Review:**
  - another token's photo gives 400 `invalid_photo`, and the token still resolves;
  - a review with 2 photos keeps their order;
  - no storage path in JSON.
- **Image:** 200 `image/jpeg`, `private, no-cache`, 304 on `ETag`; an unattached photo isn't served.
- **Moderation:**
  - Admin removal: `completed`, the image 404, the detail down to 1 photo;
  - hiding the review gives 404 for its photo, and unhiding gives 200.
- **End state:** the existing restaurant's rating is unchanged (4.0, 2 reviews).

### Not verified
- **Redemption winning the race against another user's upload on SQL Server:** in all 4 probe runs the upload won. The other branch is checked by reading the code, plus the InMemory path.
- **The hosted cleanup timer on the running API:** cleanup logic is tested, but no hourly run was observed.
- **The audit log line on the running API:** tests use `RecordingAuditLog`.
- **Linux hosting:** SkiaSharp's Linux native assets are a Phase 11 item.
- **Large volumes:** pilot data only.

---

## 8. Mobile handoff
- **Upload:**
  - Each picked photo, compressed on the device, is sent as multipart `token`, `clientUploadId` (one UUID per logical photo, reused on retry) and `file`.
  - 201 or 200 gives `{ id }`.
  - Retry with the same id on network errors, 409 `upload_in_progress` and 503 `try_again`. Use a new id after 410.
- **Remove before submitting:** `DELETE /verification/photos/{id}` (best effort).
- **Submit:** add `photoIds` (only uploaded ones, in thumbnail order) to the existing review body. On 400 `invalid_photo` the token is **not** used; re-upload or submit without the photos.
- **Display:** `photos[].url` is relative, so prefix it with `API_BASE_URL`. No auth header is needed. On a load error, keep the sand placeholder.
- **Errors to map in Arabic:** `token_claimed`, `photo_limit`, `upload_in_progress`, `photo_removed`, `file_too_large`, `unsupported_media_type`, `upload_failed`, `invalid_photo`, `try_again`.
- **Refresh:** removals reach the app only through data refetches (plan §10). Native image caches must not be relied on.
- **Native build:** `expo-image-picker` and `expo-image-manipulator` need a **new EAS development build**.

---

## 9. Known limitations (Backend)
- **Storage:** local disk only; cloud storage and Linux native assets come in Phase 11.
- **ETag:** contains the photo id and attempt id (no path).
- **Cleanup:** hourly; unattached photos live up to about 25 h.
- **Tombstones:** `Purged` rows are kept 30 days.
- **Test data in the development database:**
  - `smoke9a-…` accounts.
  - The inactive "Smoke9A …" test restaurant (`0d9e5d00-6351-450f-85d2-46ecb3a86dcd`), with one review and its photos.
  - Probe tokens on the inactive Smoke9 restaurant.
  - Photo files for the live test photos under `%LOCALAPPDATA%\Insaaf\review-photos`.

### Open release items
1. The Phase 8, 8A and 9 device checklists are still not run.
2. iOS is untested (Phase 10).

---

## 10. Educational summary (Backend)
- **Why reserve a slot with one UPDATE?** "Count, then insert" lets two requests both see 4 and both insert. A single conditional UPDATE on the token row is serialized by SQL Server's row lock, so exactly five succeed.
- **Why does every attempt write its own file?** If attempts shared a file name, a slow, cancelled attempt could overwrite the good one later. With unique names, the only decision is a database statement: the fence.
- **Why tombstones instead of deleting rows?** A row is what proves a file belongs to someone. Keeping a tombstone for 30 days means even a very late write is found and removed.
- **Why lock the token first, everywhere?** Deadlocks come from two transactions taking the same locks in opposite orders. One order everywhere removes that cause, and a whole-transaction retry handles the rest.
- **Why did the first repair fail?** With `READ_COMMITTED_SNAPSHOT`, a statement reads data as of its own start. Locking first and counting in the next statement makes the count current. Only a real SQL Server probe could show this.
- **Why re-encode every photo?** Decoding and re-encoding keeps only pixels. Anything hidden in the file, from GPS to malformed data, is gone.

---

## 11. Mobile Track

**Date:** 2026-11-08.

### 11.1 Overview
| ID | Task | Status | Evidence |
|---|---|---|---|
| 9A.5 | "إضافة صور" in the review form: pick, compress, preview, upload, retry, remove; send only uploaded ids | ✅ Verified on Android | typecheck, lint, bundles; founder's device run (§11.7) |
| 9A.6 | Photos on `ReviewCard` (up to 3, "+N") and Review Detail; refresh triggers; refresh-failure notice | ✅ Verified on Android | Same |
| 9A.7 (Mobile) | Checks and device checklist | ✅ | Automated checks pass; **all 13 device cases reported passed**; iOS not tested |

**Applied decisions:**
| # | Implementation |
|---|---|
| D-4 | `expo-image-picker` ~57.0.21 (library only) and `expo-image-manipulator` ~57.0.22. A new EAS development build was required |
| D-5 | 72×72 thumbnails on sand: spinner, retry overlay, ×. No Figma design for them |
| D-8 | Tapping a photo does nothing (no gallery) |
| D-12 | With failed photos, a confirmation "بعض الصور لم تُرفع. إرسال التقييم بدونها؟"; only uploaded ids are sent |
| D-14 | Refetch on screen focus (if stale), on app resume (`focusManager` + `AppState`), and every 5 minutes on a focused photo screen |
| D-16 | "تعذر تحديث المحتوى. اسحب للتحديث." while older data is shown after a failed refetch |

**Deviations from the plan's wording (same behaviour):**
1. **`clientUploadId`** comes from a small `Math.random` UUID v4 helper (`utils/uuid.ts`) instead of a native crypto module. It only needs to be unique per user and token, not secret.
2. **Retry policy:** 409, 503 and network errors are retried automatically up to 3 times, 2 s apart, with the same id. 410 retries with a new id.
3. **Compression failure** marks the photo failed with no retry; the user removes it.

### 11.2 Application structure (new/modified)
| File | Responsibility |
|---|---|
| `features/verification/hooks/useReviewPhotos.ts` | Per-photo state, compression, upload with retries, remove, `markFailed` |
| `features/verification/components/ReviewPhotoPicker.tsx` | "إضافة صور" button (Figma icon) and thumbnails |
| `features/reviews/components/ReviewPhotoStrip.tsx` | Card (3 + "+N") and detail rows, with a sand placeholder on error |
| `features/reviews/hooks/useRefreshOnFocus.ts` | Focus refetch when stale; 5-minute interval while focused and active |
| `features/reviews/components/RefreshFailedNotice.tsx` | The refresh-failure notice |
| `utils/uuid.ts` | `clientUploadId` |

**Modified:**
- `VerifiedReviewForm.tsx`, `ScanScreen.tsx`.
- `verificationApi.ts` (`uploadPhoto` multipart, `deletePhoto`), `types/verification.ts`, `types/review.ts` (`photos`).
- `ReviewCard.tsx`, `ReviewDetailScreen.tsx`, `FeedScreen.tsx`, `HomeScreen.tsx`, `RestaurantDetailScreen.tsx`, `ProfileScreens.tsx`.
- `app/_layout.tsx` (`focusManager`), `app.json` (image-picker plugin), `package.json`.

**Also changed during the device run:**
- `useVerificationMutations.ts` (taste refresh), `BottomNav.tsx` (unread dot), `FeedScreen.tsx` (chip count and colours) — see the Phase 8A report §13.
- `AuthFooterLink.tsx` (baseline fix, §11.7).

### 11.3 User journeys and states
- **Scan → write:**
  1. "إضافة صور" opens the library (up to the remaining slots).
  2. Each photo compresses (≤ 2048 px, JPEG 0.8) and uploads on its own.
  3. Spinner → done, or failed with retry and ×.
  4. Submit is disabled while anything is uploading. With failures it asks first, then sends only uploaded ids, in order.
- **Server says `invalid_photo`:** the token is kept; the sent photos turn failed, with "تعذّر إرفاق بعض الصور. أعد رفعها أو أرسل بدونها.".
- **Photo errors in Arabic:** too large, unsupported, limit, token claimed, rate limited, generic failure.
- **Viewing:**
  - Cards show up to 3 photos, with "+N" on the third. Review Detail shows them all in one row.
  - A photo that fails to load keeps the sand placeholder.
  - After removal or hiding, the photo leaves on the next successful refresh.

### 11.4 State management
- **Server state:** TanStack Query only.
- **Selected photos:** local state in `useReviewPhotos`; no global store.
- **New:** an app-wide `focusManager` wired to `AppState`, so active stale queries refetch on resume. Photo screens also refetch on focus and every 5 minutes while focused.
- **After a review is created:** `['users']`, `['reviews']` and `['home']` are invalidated (device fix).

### 11.5 API data flow
- **Upload:** `useReviewPhotos` → `verificationApi.uploadPhoto` → `apiClient` (multipart, Bearer, refresh interceptor) → `POST /api/v1/verification/photos` → `{ id }`.
- **Submit:** `ScanScreen` → `createVerifiedReview({ …, photoIds })`.
- **Display:** `photos[].url` (relative) is prefixed with `API_BASE_URL` → anonymous `GET /api/v1/review-photos/{id}`.

### 11.6 RTL and Figma
- **From Figma:**
  - The "إضافة صور" button and icon, in the text card's bottom row.
  - Card photos: up to 3, 4:3, rounded, with "+N".
  - Detail photos: every photo in one row, rounded 16.
- **No Figma design:** the thumbnails and their states, the failure confirmation, the photo error line, the refresh-failure notice, the النشاط unread dot.
- **RTL:** the × uses `end`, rows follow the layout direction. Checked by the founder on Android.

### 11.7 Testing and integration
| Check | Result |
|---|---|
| `tsc --noEmit` | ✅ |
| `expo lint` | ✅ 0 errors; 1 older warning in `services/api/client.ts` |
| `expo export` Android (1584 modules) and iOS (1460 modules) | ✅ |
| Backend APIs on SQL Server | ✅ Smoke 16/16, probes 7/7 (§7) |
| **Device checklist (13 cases), Android** | ✅ **All reported passed by the founder** |
| iOS | ❌ Not tested (Phase 10) |

> The device results were run by the founder on an Android phone with the new development build, against the local API, and reported in chat ("all done, including the photos"). They were not observed by Claude. Item-by-item timings (e.g. how long a removed photo took to disappear on an open screen) weren't reported.

**Device cases run:**
1. 3 photos upload.
2. Partial failure with airplane mode, then retry.
3. Confirmation, then sending without the failed photo.
4. Card and detail display.
5. Removal on an already-open screen.
6. Removal with pull-to-refresh.
7. The 5-photo limit.
8. Another account on a claimed token.
9. Return to the tab, and resume.
10. Failed refetch shows the notice, then a successful one.
11. Remove before sending.
12. Hide and unhide.
13. Account switch.

**Defects found on the device and fixed:**
1. **The camera wouldn't open or ask permission on Android.**
   - **Cause:** `"cameraPermission": false` in the `expo-image-picker` plugin config blocks `android.permission.CAMERA` for the whole app (`tools:node="remove"`), which broke QR scanning.
   - **Fix:** the value is now the same Arabic camera text as `expo-camera`. `expo config --type introspect` confirms CAMERA is requested; only `RECORD_AUDIO` is removed.
   - Needed a new EAS build; re-tested by the founder.
2. **Login footer:** "إنشاء حساب" sat below the baseline of "ليس لديك حساب؟".
   - **Cause:** a `Pressable` view nested inside `Text`.
   - **Fix:** the link is a nested `Text` with `onPress`. Applies to Login, Register and Forgot Password.
   - Typecheck, lint and bundles pass. **Not yet re-checked on the device.**
3. Taste refresh, chip colours and the النشاط dot: Phase 8A report §13.

### 11.8 Educational summary (Mobile)
- **One upload per photo, keyed by `clientUploadId`:** a retry after a lost response returns the same photo instead of a duplicate. That's why the id is created once per picked photo, not per attempt.
- **Uploads never touch the token:** the review submission alone redeems it. So a failed photo can always be retried or dropped.
- **Draw from data, not from caches:** native image caches can't be told to forget a photo. The app shows only the photos in the latest successful response and refetches at well-defined moments.
- **Config plugins change the native app:** one wrong permission flag removed the camera for the whole app. It's invisible to typecheck and bundles, which is why device builds matter.

---

## 12. Consolidated Phase 9A status
| ID | Track | Status |
|---|---|---|
| 9A.1–9A.4 | Backend | ✅ Verified (340/340 tests, probes 7/7 ×4, smoke 16/16 on SQL Server) |
| 9A.5–9A.6 | Mobile | ✅ Verified on Android (founder-reported device run) |
| 9A.7 | Both | ✅ Backend and Mobile checks done |

**The Phase 9A gate** ("A verified review can include photos, shown on review cards and Review Detail, and Admin can remove any photo") is proven at API level on SQL Server, and on an Android device as reported by the founder.

### Open items and founder actions
1. **iOS** is untested (Phase 10), including photo-library permission text and caching.
2. **Check the login footer fix** on the device: reload Metro.
3. **Production storage** (cloud, Linux native assets for SkiaSharp) comes in Phase 11.
4. **Test data** in the development database and `%LOCALAPPDATA%\Insaaf\review-photos`:
   - smoke and probe accounts;
   - the "Smoke9A" restaurant;
   - two "مطعم تجربة الصور" test restaurants (active), with their reviews and photos;
   - probe tokens.

### Files changed in Phase 9A (summary)
- **Backend:** the files in §2.
- **Mobile:** the files in §11.2.
- **Docs:** `phase9a-report.md`, and §13 addenda in `phase8-report.md`, `phase8a-report.md` and `phase9-report.md`; also `project-progress.md`, `decisions.md` (ADR-018), `implementation-plan.md` and `README.md`.
- **One migration:** `AddReviewPhotos`.
- **New packages:** SkiaSharp (backend), `expo-image-picker` and `expo-image-manipulator` (mobile).
