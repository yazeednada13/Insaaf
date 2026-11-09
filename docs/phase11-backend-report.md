# Phase 11 — Completion (Figma parity): Backend report

**Date:** 2026-11-09
**Branch:** `feature/phase11-completion` (the founder manages Git)
**References:**
- The approved Phase 11 plan (tasks 11.1–11.21, decisions D-1…D-11).
- [decisions.md](decisions.md) ADR-021.
- [phase10-report.md](phase10-report.md), whose §7.5 is the Figma inventory that started this phase.

**Report status:** ✅ **Backend Track complete** (11.1–11.10) · ⏳ the Mobile Track starts only on "Phase 11 — Mobile"

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 11 is the last development phase. It builds every Figma screen and feature that was still missing and needs no external account. The founder decided:
- The official restaurant reply is written by an Admin in the restaurant's name. There is no new role.
- Password reset is built in full; Google sign-in stays deferred.
- Release becomes Phase 12 (deferred).

This Backend track gives the Mobile track every API it needs.

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 11.1 | Schema: one migration | ✅ Verified | `20261109063424_AddCompletionFeatures`:<br>• reviewed (additive only)<br>• SQL parsed with `SET PARSEONLY`<br>• `has-pending-model-changes`: none<br>• **applied by the founder** to `Insaaf` and `Insaaf_IntegrationTests` (confirmed with `migrations list` and `sys.tables`) |
| 11.2 | Saved restaurants and reviews (G-5) | ✅ Verified | 3 API tests; SQL Server race test (5 parallel saves → 1 row each) |
| 11.3 | Comment likes (G-10) | ✅ Verified | 1 API test (repeat-safe, per user, scoped to the review, cascade on comment delete); SQL Server race |
| 11.4 | Review reports (G-9) | ✅ Verified | 1 API test (validation, one open per user, Admin queue, resolve, re-report after resolve); SQL Server race on the filtered unique index |
| 11.5 | Official reply (G-8) | ✅ Verified | 1 API test (Admin-only, validation, replace, shown on reads, one notification, ratings unchanged, delete removes the notification); SQL Server parallel upserts |
| 11.6 | Recent reviews and community photos (G-7, G-4) | ✅ Verified | 2 API tests; both added to the SQL Server N+1 test |
| 11.7 | Profile photos (G-12) | ✅ Verified | 2 API tests (415, square crop, `no-cache`, replace deletes the old file, remove, Admin never served); SQL Server round trip |
| 11.8 | Taste Setup (G-3) | ✅ Verified | 2 API tests (validation, case-insensitive dedupe, `hasCompletedTasteSetup`, suggested people rules); SQL Server test |
| 11.9 | Password reset and change (G-14, G-13) | ✅ Verified | 3 unit tests; 2 API tests (code from the outbox, wrong and reused code, every session revoked, new password only); SQL Server reset |
| 11.10 | Notification settings; tests; report | ✅ Verified | 1 API test (a turned-off type creates nothing; turning it back on works); cleanup tools extended; this report |

### Applied decisions
| # | Decision | Implementation |
|---|---|---|
| D-1 | Phase 11 — Completion; Release becomes Phase 12 | ✅ ADR-021 |
| D-2 | One migration | ✅ Applied by the founder to both databases |
| D-3 | 6-digit reset code | ✅ Identity's email token provider |
| D-5 | Taste Setup uses the real categories | ✅ Slugs validated against `Categories` |
| D-6 | Settings sub-pages | Backend: notification settings and change password ✅. The rest are Mobile screens (privacy text and support email come from the founder) |
| D-7 | Report reasons | ✅ `offensive`, `not_genuine`, `wrong_information`, `other` (+ optional note, up to 500 characters) |
| D-9 | Official reply through the Admin API only | ✅ |
| D-10 | Avatars visible to everyone; never for Admin accounts | ✅ |
| D-11 | Google, skeleton loaders, «صور المطعم» not built | ✅ Recorded |

### Deviations from the plan's wording (same contract, recorded honestly)
1. **Avatars use their own storage port, `IAvatarStorage`, not `IPhotoStorage` (plan (A)).**
   - Review-photo storage only accepts `{photoId}/{leaseId}.jpg` keys.
   - Its cleanup sweep deletes any folder that has no photo row.
   - `LocalAvatarStorage` writes to `{photo root}/avatars/{key}.jpg`, a folder the sweep ignores, because the sweep only reads folders named by a photo id.
2. **Avatar URL:** `/api/v1/users/{userId}/avatar?v={key}`. A new upload is a new URL for every image cache. The server looks the user up by id and ignores `v`.
3. **Reply notifications:** only creating a reply notifies. Replacing its text doesn't notify again, and deleting it removes the notification.
4. **The notification settings cover likes, comments and follows (D-6).** The official reply always notifies.
5. **Suggested people:** active (not suspended), non-Admin users with **at least one** public review, not already followed, most reviews first.
6. **Saved reviews are listed newest review first** (the feed cursor), not in saving order. Saved restaurants are listed most recently saved first.
7. **Changing the password returns fresh tokens for the current device**, after revoking every refresh token. A reset doesn't sign in: the app goes to login.
8. **A wrong reset code counts as a failed sign-in attempt** (Identity lockout: 5 tries, 5 minutes), so a 6-digit code can't be guessed in time.
9. **Reporting your own review is not refused.** Nothing in Figma or the spec says it should be.

---

## 2. Architecture

The dependency direction is unchanged: Domain ← Application ← Infrastructure ← API. No CQRS, MediatR or generic repository, and no new role.

| Layer | New | Changed |
|---|---|---|
| Domain | Entities `SavedRestaurant`, `SavedReview`, `ReviewCommentLike`, `ReviewReport`, `ReviewReply`, `UserFoodPreference`; enums `ReviewReportReason`, `TastePriority`; `NotificationType.Reply = 4`; ports `ISavedRepository`, `IReviewCommentLikeRepository`, `IReviewReportRepository`, `IReviewReplyRepository`; `Phase11Projections.cs`, `ReviewReplyProjection` | `IReviewRepository` (saved and recent streams), `IReviewCommentRepository` (viewer for likes), `IUserProfileRepository` (avatar, taste, settings, suggestions), `IReviewPhotoRepository` (restaurant photos), `INotificationRepository`; review, comment, user and notification projections (optional trailing fields) |
| Application | `SavedService`, `ReviewCommentLikeService`, `ReviewReportService`, `Admin/ReportAndReplyService`, `RestaurantPhotoService`, `UserAvatarService`, `TasteSetupService`, `UserSettingsService`, `UserAvatars` (URL helper), ports `IEmailSender`, `IAvatarStorage` | `AuthService` (forgot, reset, change), `FeedService` (recent), `NotificationPublisher` (reply), `ReviewService`, `UserProfileService`, `NotificationService` (new fields), `IImageProcessor.ProcessAvatar`, DTOs |
| Infrastructure | 4 repositories, `LocalAvatarStorage`, `Email/` (`EmailOptions`, `FileEmailSender`, `SmtpEmailSender`), 6 configurations | `ApplicationUser` (+6 columns), `AppDbContext`, `ReviewRepository`, `ReviewCommentRepository`, `UserProfileRepository`, `NotificationRepository`, `ReviewPhotoRepository`, `RestaurantRepository` (suggestions), `IdentityUserAccountService`, `SkiaImageProcessor`, `DependencyInjection` |
| API | — | `ReviewsController`, `RestaurantsController`, `UsersController`, `HomeController`, `AuthController`, `AdminModerationController`, `RateLimitingExtensions` (+2 policies), `appsettings.json` |

---

## 3. End-to-end data flow

### Saving a review: `PUT /api/v1/reviews/{id}/save`
1. `ReviewsController.Save` takes the user id from the JWT.
2. `SavedService.SaveReviewAsync`:
   - `IsPublicAsync` returns false → 404;
   - otherwise `SavedRepository.AddReviewIfMissingAsync`.
3. Repeats and concurrent requests are safe. The repository checks first, and the composite key `(UserId, ReviewId)` turns a racing duplicate into a caught 2601/2627 error.
4. Every later review read projects `isSavedByMe` in the same SQL statement as an `EXISTS` (the N+1 test shows no extra commands).

### The official reply: `PUT /api/v1/admin/reviews/{id}/reply`
1. `[Authorize(Roles = Admin)]` on the controller; `actorId` comes from the JWT.
2. `ReportAndReplyService.SetReplyAsync`:
   - validates the text (1–1000 characters, trimmed);
   - the review must be public;
   - `ReviewReplyRepository.UpsertAsync` creates or replaces the reply. A racing create retries as a replace.
3. Only a **create** calls `NotificationPublisher.RepliedAsync`. This adds a `Reply` notification (actor = the Admin; unique per review).
4. An audit line records the ids and the result only.
5. **On reads:** `restaurantReply: { text, createdAt }` comes from an `OUTER APPLY`-style subquery. The notification DTO carries `restaurant: { id, name }`, so the device shows the restaurant and never the Admin.

### A profile photo: `PUT /api/v1/users/me/avatar` → `GET /api/v1/users/{id}/avatar`
1. **Upload:**
   - the multipart file is limited to 5 MB;
   - `SkiaImageProcessor.ProcessAvatar` runs the full review-photo pipeline (format checks, orientation, no metadata), then a centred square crop of at most 512 px;
   - `LocalAvatarStorage.SaveAsync` writes a new file with a new key;
   - `SetAvatarKeyAsync` swaps the key and returns the old one;
   - the old file is deleted. A failed delete only leaves an unreferenced file.
2. **Serving:** the route is anonymous, like review photos. `GetPublicAvatarKeyAsync` returns no key for Admin accounts, and the response uses `private, no-cache` with an `ETag`.

### Password reset
1. `POST /auth/forgot-password { email }`:
   - rate limited per IP;
   - an email is sent only if an account exists, through `IEmailSender`. In Development the file sender writes it to the outbox.
   - The response is identical either way.
2. `POST /auth/reset-password { email, code, newPassword }`:
   - **Lockout check:** if the account is locked out, the request is refused.
   - **Code check:** the code is verified with Identity's email token provider. A wrong code calls `AccessFailedAsync`.
   - **Password update:** on success the password is set through `ResetPasswordAsync`. This also changes the security stamp, so the code becomes single-use.
   - **Sessions:** `RevokeAllForUserAsync` signs out every device.

---

## 4. Important code

| Code | Why it exists |
|---|---|
| `SavedRepository`, `ReviewCommentLikeRepository` | Check-then-insert, with the composite key as the real guard. The same pattern as review likes (Phase 6) |
| `ReviewReportRepository` | One open report per user per review, enforced by the filtered unique index `IX_ReviewReports_Open_PerReporter` (`WHERE [ResolvedAt] IS NULL`, a single condition, valid on SQL Server). After resolving, a new report is allowed |
| `ReportAndReplyService` | All Phase 11 Admin moderation in one Application service; the role is checked only on the controller, as in Phase 9 |
| `UserAvatars` | One place for avatar URLs and the "no photo without a public profile" rule |
| `TasteSetupService` | Validates slugs against `Categories` (D-5) and stores priorities as flags. «مقترح لك» uses chosen and reviewed categories together (`RestaurantRepository.ListSuggestedIdsAsync`) |
| `NotificationRepository.RecipientWantsAsync` | The settings check before every add; removing a notification is never blocked |
| `FileEmailSender` / `SmtpEmailSender` | Selected by `Email:Provider` (`file` by default, `smtp` for hosting) |

---

## 5. API contract (new and changed)

All routes require a JWT unless marked anonymous. Errors use the usual envelope.

### Saved (private to the caller)
| Method and route | Response | Errors |
|---|---|---|
| `PUT` / `DELETE /api/v1/restaurants/{id}/save` | `{ id, isSavedByMe }` | 404 `not_found` (saving only: an inactive or unknown restaurant) |
| `PUT` / `DELETE /api/v1/reviews/{id}/save` | `{ id, isSavedByMe }` | 404 (saving only: a review that isn't public) |
| `GET /api/v1/users/me/saved/restaurants?page&pageSize` | `{ items: RestaurantListItem[], page, pageSize, totalCount }` (active only) | 400 when `pageSize` > 50 |
| `GET /api/v1/users/me/saved/reviews?cursor&limit` | The feed shape `{ items, nextCursor }` (public reviews only) | 400 bad cursor or limit |

New fields: `isSavedByMe` on restaurant detail, review list items, review detail and feed items.

### Comments, reports, recent, photos
| Method and route | Body | Response | Errors |
|---|---|---|---|
| `PUT` / `DELETE /api/v1/reviews/{id}/comments/{commentId}/like` | — | `{ commentId, likeCount, isLikedByMe }` | 404 (review not public, or the comment belongs to another review) |
| `POST /api/v1/reviews/{id}/report` | `{ reason, note? }` (extra fields → 400) | `{ reviewId, reported: true }` | 400 `validation_error`, 404 |
| `GET /api/v1/home/recent?cursor&limit` | — | Feed shape: everyone's public reviews, newest first | 400 |
| `GET /api/v1/restaurants/{id}/photos?cursor&limit` | — | `{ items: [{ id, reviewId, url, thumbnailUrl, width, height }], nextCursor }` | 400, 404 |

New fields:
- comments: `likeCount`, `isLikedByMe`;
- reviews: `restaurantReply: { text, createdAt } | null`;
- user summaries (`reviewer`, `author`, `actor`, profile, lists, search): `avatarUrl | null`;
- notifications: `type` can be `reply`, with `restaurant: { id, name }`.

### Profile, Taste Setup, settings
| Method and route | Body | Response |
|---|---|---|
| `PUT /api/v1/users/me/avatar` | multipart `file` | `{ avatarUrl }`. Errors: 400 no file, 413 too large, 415 not an image |
| `DELETE /api/v1/users/me/avatar` | — | `{ avatarUrl: null }` |
| `GET /api/v1/users/{id}/avatar?v=` | — | **Anonymous.** JPEG; 404 when there is none or the user is an Admin |
| `GET` / `PUT /api/v1/users/me/taste-preferences` | `{ categorySlugs: string[1..20], priorities: (food_quality\|service\|price\|atmosphere\|location)[] }` | `{ categorySlugs, priorities, hasCompletedTasteSetup }` |
| `GET /api/v1/users/suggested?limit` (1–20) | — | `{ items: UserSearchItem[] }` |
| `GET` / `PUT /api/v1/users/me/settings` | `{ notifyLikes, notifyComments, notifyFollows }` (extra fields → 400) | The same |

`GET /api/v1/users/me` now has `hasCompletedTasteSetup`. It's `null` on other users' profiles.

### Auth
| Method and route | Body | Response | Errors |
|---|---|---|---|
| `POST /api/v1/auth/forgot-password` | `{ email }` | `{ codeSent: true }` (always) | 400 invalid email; 429 (5 per minute per IP) |
| `POST /api/v1/auth/reset-password` | `{ email, code, newPassword }` | `{ passwordReset: true }` | 400 `invalid_code`, `too_many_attempts`, `validation_error`; 429 (10 per minute per IP) |
| `POST /api/v1/auth/change-password` (JWT) | `{ currentPassword, newPassword }` | `AuthResponseDto` (new tokens) | 400 `wrong_password`, `validation_error` |

### Admin (`[Authorize(Roles = Admin)]`)
| Method and route | Body | Response |
|---|---|---|
| `GET /api/v1/admin/reports?status=open\|resolved\|all&cursor&limit` | — | `{ items: [{ id, reviewId, restaurantId, restaurantName, reporterId, reporterUserName, reason, note, createdAt, resolvedAt, isReviewHidden }], nextCursor }` |
| `PUT /api/v1/admin/reports/{id}/resolve` | — | The report; repeating is safe; 404 when unknown |
| `PUT /api/v1/admin/reviews/{id}/reply` | `{ text }` | `{ reviewId, text, createdAt, updatedAt }`; 400, 404 |
| `DELETE /api/v1/admin/reviews/{id}/reply` | — | `{ reviewId, deleted: true }`; 404 when there is none |

---

## 6. Database

### Migration `20261109063424_AddCompletionFeatures` (additive only)
- **New tables:** `SavedRestaurants`, `SavedReviews`, `ReviewCommentLikes`, `ReviewReports`, `ReviewReplies`, `UserFoodPreferences`.
- **New columns on `AspNetUsers`:**
  - `AvatarKey` (`nvarchar(100)`, null);
  - `TastePriorities` (int, default 0);
  - `TasteSetupCompletedAt` (null);
  - `NotifyLikes`, `NotifyComments` and `NotifyFollows` (bit, default 1). Existing accounts kept every notification on, which was checked on both databases.
- **Delete rules:**
  - Every foreign key to `AspNetUsers`, `Restaurants` and `Categories` is `Restrict`.
  - `Cascade` is used only from a review (to saved reviews, reports and the reply) and from a comment (to its likes). Each table has a single cascade path.
- **Indexes:**
  - owner lists `(UserId, CreatedAt DESC)`;
  - reverse lookups;
  - the filtered unique report index;
  - the Admin queue `(ResolvedAt, CreatedAt)`.

### Business rules
- Saving, comment likes, reports, replies and community photos all start from the public-review rule (`PublicReviewsOfActiveRestaurants`), so moderation applies everywhere automatically.
- A reply never touches the review or the rating aggregates; the API test checks that the average and count are unchanged.
- **Cleanup tools updated:** both `docs/scripts/cleanup-test-data.ps1` and the integration-test cleanup now delete the six new tables' rows first, in foreign-key order. The integration runs end with **0 leftover rows**.

---

## 7. Verification

### Run (2026-11-09)
| Check | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 warnings, 0 errors |
| `Insaaf.Application.Tests` | ✅ 256/256 (+3 password tests) |
| `Insaaf.API.Tests` | ✅ 115/115 (+15 in `Phase11EndpointsTests`) |
| `Insaaf.IntegrationTests` on SQL Server | ✅ 26/26 (+4 in `Phase11Tests`; the N+1 test extended); 0 leftover rows; the 12 seeded restaurants kept |
| `Insaaf.IntegrationTests` without a connection string | ✅ 26 skipped |
| Migration | ✅ Parsed; no pending model changes; applied by the founder to both databases |

### Command counts on SQL Server (1 reviewer → 4 reviewers)
All 21 reads are constant. The new or changed ones:
| Read | Commands |
|---|---|
| `GET /restaurants/{id}` | 2 → 2 (was 1 before Phase 11; +1 for `isSavedByMe`) |
| `GET /home/recent` | 1 → 1 |
| `GET /restaurants/{id}/photos` | 2 → 2 |
| `GET /users/me/saved/reviews` | 1 → 1 |
| `GET /users/me/saved/restaurants` | 2 → 2 |
| `GET /users/suggested` | 1 → 1 |
| `GET /admin/reports` | 1 → 1 |

Review reads stay at their Phase 10 counts, even though they now also carry the saved state, the reply and avatars.

### Defects found and fixed during this track
- **Corrupted test file.** An edit with PowerShell (`Get-Content` without an encoding) turned the Arabic in `Phase11EndpointsTests.cs` into mojibake. The tests still passed, because both sides of each comparison were corrupted the same way. The file was restored by reversing the decoding, and no corrupted characters remain.
- **Logging dependency.** The avatar service first used `ILogger`, which the Application project doesn't reference. It was removed instead of adding a dependency.

### Not verified
- **Real email delivery.** The SMTP sender compiles but has never sent anything; that waits for Phase 12.
- **The exact life of a reset code.** It comes from Identity's time-based provider: 3-minute steps, accepted for a few minutes. It wasn't measured.
- **Avatars of very large images** (12,000 px input); only the shared pipeline's limits are tested.
- **Mobile use of the new APIs.** Not built until the Mobile track.

---

## 8. Mobile handoff
- **All new routes** are listed in §5. Every field is additive, so older app builds keep working.
- **Images:**
  - Avatars: use `avatarUrl` as given, prefixed with `API_BASE_URL`; `null` means show initials.
  - Cards: use `thumbnailUrl`.
  - Community photos come with a `reviewId`, to open the review.
- **Notifications:** for `reply`, show `restaurant.name`, not the actor.
- **Taste Setup:**
  - Show it when `GET /users/me` returns `hasCompletedTasteSetup: false`.
  - Foods: the app's category slugs. Saving requires at least one.
  - Step 3: `GET /users/suggested`, using the existing follow buttons.
- **Password reset:** email screen → code screen → new password, then go to login. Change password returns new tokens; the session must store them.
- **Errors to map to Arabic messages:**

  | Code | When |
  |---|---|
  | `invalid_code` | Wrong or expired reset code |
  | `too_many_attempts` | Too many wrong codes (account locked for a while) |
  | `wrong_password` | Wrong current password when changing it |
  | `unsupported_media_type` (415) | The photo isn't a JPEG, PNG or WebP image |
  | `file_too_large` (413) | The photo is over 5 MB |
  | `rate_limited` (429) | Too many requests in a minute |

- **Local testing:** reset codes are written to `%LOCALAPPDATA%\Insaaf\outbox`. The founder reads the code there.
- **Risks:** none in the contract. The only new native need is the image picker for avatars, which already exists (Phase 9A permissions), so **no new EAS build is expected**.

---

## 9. Educational summary (Backend)

**One read, many facts.** Saved state, the official reply and avatars were added as extra columns of the same review query, not as new queries per review. EF Core turns each one into a correlated subquery (`EXISTS`, `OUTER APPLY`) inside one SQL statement. The command-count test proves it: the counts didn't change when four reviewers' data was added.

**Keys as the real guard.** Every "only once" rule is enforced by the database:
- composite primary keys for saved items and likes;
- a filtered unique index for open reports;
- the review id as the reply's key.

The services check first for a clean answer, and the database settles the races. The SQL Server tests fire five parallel requests at each rule to prove it.

**Moderation by construction.** Every new read starts from the same public-review rule. Hiding a review, deactivating a restaurant or removing a photo therefore hides the matching saved items, community photos, replies and reports' context, with no new code.

**Ports for what changes later.** Email and avatar storage are interfaces in Application, with simple local implementations in Infrastructure. Phase 12 swaps in SMTP and cloud storage by configuration, without touching the services.

**Security in small steps.** The password-reset flow is built from small pieces:
- the same answer whether or not an account exists;
- a per-IP rate limit;
- a per-account lockout on wrong codes;
- a single-use code (the security stamp changes);
- every session revoked after a change.

---

## 10. Status and next step
- **Backend Track: complete.** Nothing was committed (the founder manages Git), and the migration is applied.
- **Next:** the founder writes **"Phase 11 — Mobile"** (11.11–11.21). Before then, the founder supplies the **privacy text** (or approves my draft) and the **support email** (D-6).
