# Phase 8 — Profiles: consolidated report

**Date:** 2026-11-05
**Branch:** `feature/phase8-profiles` (from `develop` after the Phase 7 merge; the founder manages Git)
**References:** the approved Master Plan (Phase 8), the approved Phase 8 plan (decisions D-1…D-14), [decisions.md](decisions.md) ADR-003/ADR-015, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`ProfileScreen.tsx`, `EditProfileScreen.tsx`, `SettingsScreen.tsx`, `SearchScreen.tsx`)
**Report status:** ✅ **Backend Track complete** · ⏳ Mobile Track not started (waiting for `Phase 8 — Mobile`)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 8 gives every reviewer a public identity: a profile with stats from verified reviews, followers and following lists, and people search. Users can edit their profile and sign out. Admin accounts have no public presence.

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 8.1 | `Bio`, `AddUserBio`; profile read and edit; username through Identity; logout | ✅ Verified | Build; migration review; the founder applied it; tests; SQL Server smoke |
| 8.2 | User reviews, followers and following, search, `hasPublicProfile` | ✅ Verified | Tests; smoke; SQL command counts |
| 8.4 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 232/232 tests; smoke 24/24 PASS |

### Applied decisions (approved with the plan)
| # | Decision | Implementation |
|---|---|---|
| D-1 | Task IDs | ✅ 8.1–8.4 |
| D-2 | Username management | ✅ `UserManager` only; case-insensitive uniqueness; sign-in stays by email |
| D-3 | Bio | ✅ Optional, 0–150 |
| D-4 | Admin accounts | ✅ 404 for others; out of search and lists; `/users/me` works |
| D-10 | Search rate limit | ✅ `social-search`, 60/minute per user |
| D-12 | Sign-out scope | ✅ `POST /auth/logout` revokes all the user's refresh tokens |
| D-13 | List paging | ✅ Page-based, `CreatedAt DESC, UserId DESC` |
| D-14 | Admin names in content | ✅ Names visible; `hasPublicProfile` is false |

D-5, D-6, D-7, D-8 and D-9 apply to the Mobile Track. D-11: password change isn't in this phase.

### Deviations from the plan's wording (same contract, recorded honestly)
1. **`SocialQueries.cs` is new:** the public-review rule and the Admin rule are shared there, so `ReviewRepository` and `UserProfileRepository` use one definition.
2. **`FeedCursorCodec` is new:** the cursor moved out of `FeedService` so the feed and a user's reviews share it.
3. **`UserExistsAsync` was removed** from `IUserFollowRepository`; following now uses `IUserProfileRepository.IsPublicAsync`, which also applies the Admin rule.
4. **Your own data:** a user can always read their own lists and reviews, including an Admin account. Only other callers get 404.
5. **Unfollowing an Admin:** returns 404 like following, so a follow created before this phase can't be removed through the API. Such a row doesn't count in any list or stat.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, EF Core or ASP.NET Core in Domain and Application. The Admin check is a query in Infrastructure.

### Phase 8 Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Repositories/IUserProfileRepository.cs`, `Projections/UserProjections.cs` | Profile, list and search contracts |
| Domain | `IReviewRepository.cs` and review projections (modified) | `ListByUserAsync`; `hasPublicProfile` data |
| Application | `Social/UserProfileService.cs` | Profile rules, validation, Admin behaviour |
| Application | `Social/FeedCursorCodec.cs`, `Social/Dtos/UserProfileDtos.cs` | Cursor; contracts |
| Application | `Auth/IAuthService.cs`, `AuthService.cs`, `IUserAccountService.cs` (modified) | `LogoutAsync`, `UpdateProfileAsync` |
| Infrastructure | `Identity/ApplicationUser.cs`, `ApplicationUserConfiguration.cs` (modified) | `Bio` |
| Infrastructure | `Auth/IdentityUserAccountService.cs` (modified) | Profile update through `UserManager` |
| Infrastructure | `Repositories/UserProfileRepository.cs`, `SocialQueries.cs` | Queries and shared rules |
| Infrastructure | `Migrations/20261105083246_AddUserBio*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/UsersController.cs` (rewritten), `AuthController.cs` (modified) | The new endpoints |
| API | `RateLimitingExtensions.cs`, `appsettings.json` (modified) | `social-search` policy |

**Not modified:** verification, ratings, aggregates, and the feed's content rule.

---

## 3. End-to-end data flow

### `PUT /api/v1/users/me`
1. `[Authorize]`; the JSON deserializer rejects extra fields.
2. `UserProfileService` trims and validates the name and bio.
3. `IdentityUserAccountService.UpdateProfileAsync` loads the user. A changed username goes through `SetUserNameAsync`, which validates it, checks uniqueness and sets `NormalizedUserName`. Otherwise `UpdateAsync` saves.
4. `DuplicateUserName`, or a race on the unique index (2601/2627), becomes 409 `username_taken`.
5. The fresh profile is returned.

### `GET /api/v1/users/{id}`
`GetPublicAsync` filters out Admin accounts, then one statement returns the profile, the review count from the shared visibility rule, follow counts and the follow status. A null result is 404.

### `POST /api/v1/auth/logout`
`[Authorize]` → `AuthService.LogoutAsync` → the existing `RevokeAllForUserAsync`. Any later refresh with an old token fails.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| `SocialQueries.AdminUserIds` | One definition of "not public"; used by profiles, lists, search and `hasPublicProfile` |
| `SetUserNameAsync` | Keeps normalization and uniqueness inside Identity, per D-2 |
| `FollowRow` (a class, not a record) | EF can translate member access on it, so sorting and paging work after the join |
| `CreatedAt DESC, UserId DESC` | Two follows can share a time; the user id settles the order, so pages don't repeat or skip |
| `ReadStreamAsync` | One projection for the feed and a user's reviews |

---

## 5. API contract

All routes need `[Authorize]` and use the envelope `{ success, data, errors: [{code, message}], meta: { requestId } }`.

| Method | Route | Success |
|---|---|---|
| GET | `/api/v1/users/{id}` | 200 `{ id, fullName, userName, bio, reviewCount, followerCount, followingCount, isFollowedByMe, isMe }` |
| GET | `/api/v1/users/me` | 200 the same shape |
| PUT | `/api/v1/users/me` | Body `{ fullName, userName, bio }`; 200 the updated profile |
| GET | `/api/v1/users/{id}/reviews?cursor=&limit=20` | 200 `{ items, nextCursor }`, same as the feed |
| GET | `/api/v1/users/{id}/followers?page=&pageSize=` | 200 `{ items: [{ id, fullName, userName, isFollowedByMe, isMe }], page, pageSize, totalCount }` |
| GET | `/api/v1/users/{id}/following?page=&pageSize=` | Same |
| GET | `/api/v1/users/search?q=&page=&pageSize=` | 200 `{ items: [{ id, fullName, userName, reviewCount, followerCount, isMe }], page, pageSize, totalCount }` |
| POST | `/api/v1/auth/logout` | 200 `{ signedOut: true }` |

- `q` is 2–100 characters. `pageSize` is 1–50.
- Email and roles never appear in these responses, except in the existing auth responses.

### Changes to existing contracts
- `reviewer` and comment `author` gain `hasPublicProfile` (additive), on review lists, detail, feed and comments.
- `PUT`/`DELETE /users/{id}/follow` return 404 for an Admin account.

### Admin accounts
Others get 404 `not_found` on an Admin's profile, reviews, lists and follow, the same as an unknown user. Admin accounts never appear in search or lists. Their names stay in content with `hasPublicProfile: false`.

### Errors
| Status | code | When |
|---|---|---|
| 400 | `validation_error` | Bad name, username, bio, paging or cursor; `q` too short or long; extra body field |
| 401 | `unauthorized` | No token |
| 404 | `not_found` | Unknown or Admin user |
| 409 | `username_taken` | The username belongs to someone else |
| 429 | `rate_limited` | Limit exceeded |

---

## 6. Database

### Migration `20261105083246_AddUserBio` (additive only)
`AspNetUsers` gains `Bio nvarchar(150) NULL`.

### Business rules
- Stats count verified public reviews of active restaurants only.
- Follower and following counts leave Admin accounts out, so they match the lists.
- Usernames are unique ignoring case. Changing only your own letter case is allowed.
- Username characters follow Identity's defaults, which don't allow spaces or Arabic letters, on registration and on edit.

---

## 7. Verification

### Run (2026-11-05)
| Check | Result |
|---|---|
| Baseline before changes (Release) | ✅ 193/193 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 157/157 |
| `Insaaf.API.Tests` | ✅ 75/75 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only |
| Migration applied | ✅ By the founder |
| **SQL Server smoke** | ✅ **24/24 PASS** |
| **SQL commands on SQL Server** | ✅ Profile 1; user reviews 1; followers 2; following 2; search 2 for 15 rows |

**Lists and search use 2 commands** by design: one count and one page. The number doesn't grow with the rows, so there's no N+1.

**During development:** the first test run failed 4 tests, because EF couldn't translate sorting on a record created by constructor. `FollowRow` became a class, and all tests passed.

### The new tests (39)
27 Application cases and 12 API tests. They cover stats, Admin privacy, paging ties, search in Arabic and English, edit validation, username case rules through real Identity, sign-in after a change, logout, no private fields in public JSON, and rate limits.

### SQL Server smoke results
- `/users/me` 200 with no email or roles; anonymous 401; unknown user 404.
- Edit 200; case-only conflict with another user 409; own case change 200; invalid username 400; extra field 400.
- Sign-in by email works after a username change.
- Profile stats, followers paging without duplicates, following list, user reviews, search.
- Admin hidden on profile, reviews, followers, following and follow (404), and in search.
- **Logout:** 200 twice; the old refresh token is rejected with 401.
- Restaurant ratings unchanged (`avg=4.0 count=2`).

### Not verified
- **A username race on SQL Server:** the 2601/2627 path wasn't triggered; it's covered only by reading the code.
- **Access token after logout:** it stays valid until it expires, by design.
- **Search performance with large data:** `LIKE '%x%'` wasn't measured; re-measure in Phase 10.
- **Rate limit on SQL Server:** API tests only.

---

## 8. Mobile handoff
- **Sign-out:** call `POST /auth/logout`, then clear everything locally even if that call fails. Say in the confirmation that it signs out of all devices.
- **Names in content:** only navigate when `hasPublicProfile` is true.
- **Follow state:** comes from `isFollowedByMe` on profiles and rows.
- **Own profile:** `GET /users/me`; hide the follow button when `isMe` is true.
- **Edit:** show `username_taken`. Usernames allow Latin letters, digits and `-._@+`.
- **Lists:** page-based; search starts at 2 characters.
- **Errors:** `validation_error`, `not_found`, `username_taken`, `rate_limited`, `unauthorized`.
- **Risks:** the Query cache must clear whenever the session user changes, including forced sign-out.

---

## 9. Known limitations (Backend)
- Sign-out signs out every device (founder decision).
- There's no password change (D-11).
- No avatars.
- Usernames can't be in Arabic, because of Identity's default character set.
- A follow of an Admin created before this phase can't be removed through the API; it isn't counted anywhere.
- **Test data in the development database:** `smoke8-…@example.com` accounts and their follows.

---

## 10. Educational summary (Backend)
- **Why `UserManager`?** Identity keeps `UserName` and a normalized copy in step, and checks uniqueness against the normalized copy. That's why `Ali` and `ali` conflict.
- **Why stable ordering?** Without a unique tie-breaker, SQL can return tied rows in any order, so a row can appear on two pages or none.
- **Why 404 for Admin accounts?** A 403 would confirm that the account exists and is special. 404 reveals nothing.
- **Why `hasPublicProfile`?** The client shouldn't guess. The server says whether a name leads anywhere, without exposing a role.
- **Why revoke on sign-out?** Deleting the token on the phone isn't enough if a copy leaked. Revoking on the server makes it useless.

---

## 11. Mobile Track
⏳ **Not started.** Waiting for the founder's instruction: `Phase 8 — Mobile`.
