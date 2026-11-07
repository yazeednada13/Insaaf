# Phase 8A — Home, Notifications and Taste Match: consolidated report

**Date:** 2026-11-07
**Branch:** `feature/phase8a-home-notifications` (from `develop` after the Phase 8 merge; the founder manages Git)
**References:** the roadmap (Phase 8A), the approved Phase 8A plan (decisions D-1…D-11), [decisions.md](decisions.md) ADR-014/ADR-016, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`HomeScreen.tsx`, `ActivityScreen.tsx`, `ProfileScreen.tsx`)
**Report status:** ✅ **Backend Track complete** · ⚠️ **Mobile Track implemented; not device-tested** (§11.7) · ✅ Phase 8A merged into `develop` (PR #9, `b9ebbcc`)

> This report separates three things: what was **verified by running it**, what was **checked by reading the code**, and what is **not verified**.

---

## 1. Overview

### Business purpose
Phase 8A gives the app a home and a voice: a الرئيسية tab with restaurant suggestions and trends, in-app notifications when people like, comment on or follow, and Taste Match built from real verified reviews.

### Task status (Backend)
| ID | Task | Status | Evidence |
|---|---|---|---|
| 8A.1 | Notifications: entity, `AddNotifications`, triggers, API | ✅ Verified | Migration review; the founder applied it; tests; SQL Server smoke |
| 8A.2 | Suggested and trending restaurants | ✅ Verified | Ranking tests; smoke; command counts |
| 8A.5 | Taste and Taste Match | ✅ Verified | Formula and rule tests; smoke; command counts |
| 8A.7 (Backend) | Tests and verification | ✅ | Release: 0 errors, 0 warnings; 255/255 tests; smoke 16/16 PASS |

### Applied decisions
| # | Decision | Implementation |
|---|---|---|
| D-4 | Ranking | ✅ Suggested: reviewed categories, not yet reviewed, best rated, fallback to best rated. Trending: public reviews in 30 days, then rating, then id |
| D-5 | Retention | ✅ Everything kept for the pilot |
| D-6 | Taste minimum | ✅ 3 verified reviews, for taste and match |
| D-7 | Levels and formula | ✅ ≥ 40% very high, ≥ 20% high, else medium; `100 − 25 × |difference|`, weighted by the smaller count |
| D-8 | Mark as read | ✅ `POST /notifications/read-all` (the chip calls it in Mobile) |

D-1, D-2, D-3, D-9, D-10 and D-11 apply to the Mobile Track.

### Deviations from the plan's wording (same contract, recorded honestly)
1. **Where failures are logged:** Application has no logging package, so `NotificationRepository` (Infrastructure) catches and logs write failures. The rules of who gets notified stay in Application's `NotificationPublisher`, so no new dependency was added.
2. **Ranking lives in `IRestaurantRepository`**, next to the restaurant projection it reuses; `RestaurantService.ToListItemDto` became `internal` so Home maps items the same way.
3. **Taste is on `IUserProfileRepository`** as `GetTasteAsync`, beside the other user reads.
4. **Hidden-review notifications:** a notification about a review that became hidden, or whose restaurant is inactive, is left out of the list and the count. The plan didn't say.
5. **Taste counts one review in each of a restaurant's categories**, so shares can add up to more than 100%.

---

## 2. Architecture

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**Verified (grep):** 0 references to `Roles.Admin`, EF Core, ASP.NET Core or logging in Domain and Application.

### Phase 8A Backend files
| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Notification.cs`, `Enums/NotificationType.cs` | The notification |
| Domain | `Repositories/INotificationRepository.cs`, `Projections/Phase8AProjections.cs` | Contracts and read models |
| Domain | `IReviewRepository`, `IRestaurantRepository`, `IUserProfileRepository` (modified) | Author lookup; ranking; taste |
| Application | `Social/NotificationPublisher.cs` | Who is notified, and when it's removed |
| Application | `Social/NotificationService.cs`, `HomeService.cs`, `UserTasteService.cs` | Reads, ranking and taste rules |
| Application | `Social/ReviewLikeService.cs`, `ReviewCommentService.cs`, `UserFollowService.cs` (modified) | Call the publisher after each action |
| Application | `Social/Dtos/Phase8ADtos.cs` | Contracts |
| Infrastructure | `Configurations/NotificationConfiguration.cs`, `Repositories/NotificationRepository.cs` | Storage, visibility filter, safe writes |
| Infrastructure | `RestaurantRepository.cs`, `UserProfileRepository.cs`, `ReviewRepository.cs` (modified) | Ranking, taste, author |
| Infrastructure | `Migrations/20261107131634_AddNotifications*.cs` | The migration (generated and applied by the founder) |
| API | `Controllers/NotificationsController.cs`, `HomeController.cs`; `UsersController.cs` (modified) | The new endpoints |

---

## 3. End-to-end data flow

### A like creates a notification
`PUT /reviews/{id}/like` → `ReviewLikeService` saves the like → `NotificationPublisher.LikedAsync` looks up the review's author → if it isn't the liker, `NotificationRepository.AddAsync(unique: true)` adds one unless an equal one exists. A failure there is logged and the like stands.

### `GET /api/v1/notifications`
`[Authorize]` → recipient from the JWT → `NotificationService` validates the limit and cursor → one statement returns the recipient's visible notifications with the actor's name and `hasPublicProfile` → `limit + 1` rows decide `nextCursor`.

### `GET /api/v1/home/suggested`
`HomeService` → `ListSuggestedIdsAsync` ranks ids → `ListByIdsAsync` loads the items with the same projection as Explore → the service keeps the ranking order.

### `GET /api/v1/users/{id}/taste-match`
Admin rule → both users' category counts and averages → availability rule → `UserTasteService.Match`.

---

## 4. Important code
| Code | Why it exists |
|---|---|
| `NotificationPublisher` | One place for "who is notified"; your own actions never notify you |
| `AddAsync(unique: true)` | Liking and unliking repeatedly leaves at most one like notification |
| `RemoveForCommentAsync` | Replaces the foreign key SQL Server wouldn't allow (a second cascade path from `Reviews`) |
| `Visible(recipientId)` | Notifications never point at content the user can't open |
| `UserTasteService.Match` | A pure function, so the formula is tested exactly |

---

## 5. API contract

All routes need `[Authorize]` and use the standard envelope.

| Method | Route | Success |
|---|---|---|
| GET | `/api/v1/notifications?cursor=&limit=20` | `{ items: [{ id, type, actor: { id, fullName, userName, hasPublicProfile }, reviewId, createdAt, isRead }], nextCursor }` |
| GET | `/api/v1/notifications/unread-count` | `{ count }` |
| POST | `/api/v1/notifications/read-all` | `{ count: 0 }` |
| GET | `/api/v1/home/suggested?limit=10` | `{ items: [restaurant list item] }` |
| GET | `/api/v1/home/trending?limit=10` | `{ items: [restaurant list item] }` |
| GET | `/api/v1/users/{id}/taste` | `{ reviewCount, hasEnoughReviews, categories: [{ name, slug, share, level }] }` |
| GET | `/api/v1/users/{id}/taste-match` | `{ available, percentage, categories: [{ name, slug, percentage }] }` |

- `type` is `like`, `comment` or `follow`. The device builds the text.
- `level` is `very_high`, `high` or `medium`. The device shows the Arabic label.
- Notification `limit` is 1–50; Home `limit` is 1–20.
- **Errors:** 400 `validation_error`, 401 `unauthorized`, 404 `not_found` (taste of an unknown or Admin user).

---

## 6. Database

### Migration `20261107131634_AddNotifications` (additive only)
`Notifications`: `Id`, `RecipientId`, `ActorId`, `Type`, `ReviewId`, `CommentId`, `CreatedAt`, `ReadAt`.
- CHECK `CK_Notifications_NotSelf`.
- FKs: users (Restrict ×2), `Reviews` (Cascade). `CommentId` has no FK.
- Indexes: `(RecipientId, CreatedAt DESC, Id DESC)`, filtered unread `(RecipientId) WHERE ReadAt IS NULL`, `ActorId`, `ReviewId`.

### Business rules
- No notification for your own action.
- One like notification and one follow notification per actor; a new comment notification per comment.
- Undoing an action removes its notification.
- Read state belongs to the recipient only.
- Taste and match use verified public reviews of active restaurants only, with at least 3 reviews.

---

## 7. Verification

### Run (2026-11-07)
| Check | Result |
|---|---|
| Baseline before changes (Release) | ✅ 232/232 |
| `dotnet build -c Release` | ✅ 0 errors, 0 warnings |
| `Insaaf.Application.Tests` | ✅ 175/175 |
| `Insaaf.API.Tests` | ✅ 80/80 |
| Layer check (grep) | ✅ 0 matches |
| Migration review | ✅ Additive only |
| Migration applied | ✅ By the founder; `migrations list` shows no Pending |
| **SQL Server smoke** | ✅ **16/16 PASS** |
| **SQL commands on SQL Server** | ✅ Notifications 1; unread count 1; suggested 2; trending 1; items 1; taste 2 |

**Command counts:** suggested runs its fallback query only when the first returns nothing; taste runs one count and one grouped query. None grow with the number of rows.

### The new tests (23)
18 Application cases and 5 API tests. They cover every trigger and its removal, no self-notification, like dedupe, paging, read state per recipient, hidden reviews, ranking and fallback, the 30-day window, taste shares and levels, the match formula (one case computes 83%), the availability rules and the Admin rule.

### SQL Server smoke results
- A follow notifies the followee once, with no email in the response; the actor gets nothing.
- The liker and commenter get nothing; read-all brings the count to 0; unfollow removes the notification; a bad cursor gives 400.
- Suggested returned 10 active restaurants; trending returned up to 5, led by Burger Makers; a bad limit gives 400.
- Taste for a user with 1 review: `hasEnoughReviews: false`; match unavailable; Admin taste 404.
- Restaurant ratings unchanged.

### Not verified
- **That the author of a seeded review receives like and comment notifications on SQL Server:** covered by tests and the API test only. The smoke test has no login for that author.
- **Notification list size:** the command count ran on an empty list; the query shape doesn't depend on the size.
- **Taste with 3 or more reviews on SQL Server:** tests only; the development data has no such user.
- **Rankings with realistic data:** the pilot has 2 reviews in total.

---

## 8. Mobile handoff
- **Home:** `suggested` → vertical cards with "موصى به"; `trending` → horizontal cards with distance from the device. The following section uses the feed.
- **Notifications:** call `unread-count` when النشاط opens and on pull-to-refresh; call `read-all` when the "الإشعارات" chip opens.
- **Text from `type`:** like → "أعجب {name} بتقييمك."; comment → "علّق {name} على تقييمك."; follow → "بدأ {name} بمتابعتك.".
- **Tap:** like and comment open `reviewId`; follow opens the actor's profile when `hasPublicProfile` is true.
- **Taste:** show "ذوقي في الطعام" when `hasEnoughReviews`, otherwise a hint. Show "توافق في الذوق" only when `available`.
- **Risks:** five tabs now; the landing tab changes to الرئيسية; the Phase 8 device checklist is still open.

---

## 9. Known limitations (Backend)
- No push notifications (post-MVP).
- Notifications are kept forever for now (D-5).
- Ranking and Taste Match are simple rules, adjustable after the pilot.
- **Test data:** `smoke8a-…@example.com` accounts remain in the development database.

---

## 10. Educational summary (Backend)
- **Why build text on the device?** The server sends facts (`type`, actor); each client words them in its own language, so English later needs no backend change.
- **Why "publisher" and "repository" are separate:** the publisher holds business rules (who, when); the repository holds storage and error handling. Each can change without the other.
- **Why never fail an action for a notification?** The like is what the user asked for; a notification is a side effect. Losing one is better than refusing the like.
- **Why a filtered index for unread?** Counting unread rows is frequent; an index only over unread rows keeps it small and fast.
- **Why a minimum of 3 reviews?** With one or two reviews, a percentage looks precise but means nothing.

---

## 11. Mobile Track

**Date:** 2026-11-07.

### 11.1 Overview
| ID | Task | Status | Evidence |
|---|---|---|---|
| 8A.3 | الرئيسية tab | ⚠️ Implemented, not device-tested | typecheck, lint, bundles |
| 8A.4 | "الإشعارات" chip with unread count | ⚠️ Implemented, not device-tested | Same; the backend side is verified on SQL Server |
| 8A.6 | "ذوقي في الطعام" and "توافق في الذوق" | ⚠️ Implemented, not device-tested | Same |
| 8A.7 (Mobile) | Tests and verification | ⚠️ Partial | Automated checks only; **device checklist not run** |

**Applied decisions:**
| # | Implementation |
|---|---|
| D-1 | ✅ `/` redirects to `/home`; Scan's "العودة للرئيسية" goes to `/home` |
| D-2 | ✅ The first 2 feed items; "عرض الكل" opens النشاط |
| D-3 | ✅ "تقييمات حديثة" not built |
| D-8 | ✅ Opening the chip loads the list, then calls `read-all` once; pull-to-refresh calls it again |
| D-9 | ✅ Verified-visit sheet not built |
| D-10 | ✅ The count refreshes when النشاط gains focus and on pull-to-refresh; no polling |
| D-11 | ✅ The count shows only on the chip, not on the bottom tab |

**Deviations from the plan's wording (same behaviour):**
1. Figma's vertical `RestaurantCard` variant is a separate component, `RestaurantTile`, so the existing horizontal card stays unchanged.
2. The unread-count query key is `['notifications','unread-count']`.
3. After `read-all`, only the count updates. The list keeps its unread styling until it's refreshed, so the user can still see what was new.

### 11.2 Application structure (new/modified)
| File | Responsibility |
|---|---|
| `app/(tabs)/home/_layout.tsx`, `index.tsx` | الرئيسية tab and its stack |
| `app/(tabs)/home/review/[reviewId].tsx`, `users/[userId]/{index,followers,following}.tsx` | Review and profiles inside the Home stack, so Back stays in the tab |
| `features/home/screens/HomeScreen.tsx` | Greeting, search bar, three sections |
| `features/home/hooks/useHomeQueries.ts` | `suggested`, `trending` |
| `features/restaurants/components/RestaurantTile.tsx` | Figma vertical card ("موصى به", open status) |
| `features/social/components/NotificationsList.tsx`, `NotificationItem.tsx` | Chip content and Figma notification card |
| `features/social/hooks/useNotifications.ts` | List (cursor), unread count, mark all read |
| `features/profile/components/TasteViews.tsx` | `TasteProfileCard`, `TasteMatchButton` and its sheet |
| `features/profile/hooks/useTasteQueries.ts` | `taste`, `taste-match` |

Modified: `app/(tabs)/_layout.tsx` (home tab first), `app/index.tsx`, `BottomNav.tsx` (five tabs, Figma home icon), `stackRoutes.ts` (`home` stack), `FeedScreen.tsx` (two chips), `ProfileScreens.tsx`, `ScanScreen.tsx`, `socialApi.ts`, `types/social.ts`.

### 11.3 User journeys and states
- **Open the app →** الرئيسية. The greeting follows the hour: صباح الخير before 12, مساء الخير before 17, then مساء النور.
- **Home →** search bar opens Search; the avatar opens ملفي; review cards open Review Detail in the Home stack; restaurant cards open the restaurant page in استكشاف.
- **Home states:** each section has its own loading spinner, error with "إعادة المحاولة", and empty text. Pull-to-refresh reloads all three.
- **النشاط → الإشعارات:**
  - Skeletons, then the list, or "لا توجد إشعارات بعد" when empty.
  - Unread items get a tinted background and a dot.
  - A like or comment notification opens the review; a follow opens the profile when `hasPublicProfile` is true.
  - More pages load at the end of the list.
- **My Profile:** the "ذوقي في الطعام" card, or a hint when there are fewer than 3 reviews.
- **Another profile:** the "{x}% توافق في الذوق" button next to Follow, shown only when `available`. It opens a sheet with category bars.

### 11.4 State management
- **TanStack Query only.** New keys: `['home','suggested']`, `['home','trending']`, `['notifications','list']`, `['notifications','unread-count']`, `['users','taste',id]`, `['users','taste-match',id]`.
- `read-all` writes its `{ count: 0 }` straight into the count cache.
- Account isolation from Phase 8 covers the new keys: the whole cache is cleared when the account changes.

### 11.5 API data flow
Screens → `features/{home,social,profile}/hooks` → `socialApi` → `apiClient` (Axios + Bearer + refresh) → `/api/v1/home/*`, `/api/v1/notifications*`, `/api/v1/users/{id}/taste*`. Notification text is built on the device from `type` and the actor's name.

### 11.6 RTL and Figma
- **RTL:** the same patterns as earlier screens: `textAlignStart`, row layouts, and `start`/`end` for badges. **Not checked on a device.**
- **Built from Figma:** Home header and sections, the vertical card, the النشاط chips, the notification card, the taste card, and the match button and sheet.
- **Not built:** "تقييمات حديثة" (D-3) and the verified-visit sheet (D-9).
- **No Figma design:** the per-section empty and error texts, and the hint for fewer than 3 reviews.

### 11.7 Testing and integration
| Check | Result |
|---|---|
| `tsc --noEmit` | ✅ |
| `expo lint` | ✅ 0 errors; 1 older warning in `services/api/client.ts` |
| `expo export` Android (1569 modules) and iOS (1445 modules) | ✅ |
| Backend APIs on SQL Server | ✅ Smoke 16/16 (§7) |
| **Device checklist** | ❌ **Not run** |
| iOS | ❌ Not tested (Phase 10) |

**Device checklist (to run; nothing here has been verified):**
1. The app opens on الرئيسية; five tabs in Figma's order; the home icon fills when active.
2. Greeting, the avatar initial, and the search bar opening Search.
3. The following section shows 2 reviews; "عرض الكل" opens النشاط.
4. "مقترح لك" scrolls horizontally with "موصى به"; "رائج" shows distance; cards open the restaurant.
5. A review from Home opens in the Home stack, and Back returns to Home.
6. Scan's "العودة للرئيسية" goes to Home.
7. With a second account: like, comment and follow, then check that the first account's chip shows the count.
8. Opening "الإشعارات" shows unread styling, and the count disappears.
9. Tapping each notification type opens the right review or profile.
10. Unlike, unfollow and deleting the comment remove the notification after a refresh.
11. "ذوقي في الطعام" with 3 or more reviews, and the hint with fewer.
12. "توافق في الذوق" appears only when both users qualify; the sheet opens and closes.
13. Sign out and sign in with another account: no notifications, taste or Home data from the first account.
14. RTL on every new screen.

### 11.8 Educational summary (Mobile)
- **One tab, its own stack:** Home has its own review and profile routes, so opening a review from Home doesn't jump to another tab, and Back works as expected.
- **Sections fail independently:** each section has its own query, so a failed "رائج" doesn't hide "مقترح لك".
- **Read state is a server fact:** the app asks the server to mark everything read and trusts the count it returns. It doesn't guess locally.
- **Facts from the server, words on the device:** notifications arrive as `type` plus actor; the app writes the Arabic sentence.
- **Hide rather than mislead:** Taste Match is hidden when the server says it isn't `available`, instead of showing a weak percentage.

---

## 12. Consolidated Phase 8A status
| ID | Track | Status |
|---|---|---|
| 8A.1, 8A.2, 8A.5 | Backend | ✅ Verified (255/255, smoke 16/16 on SQL Server) |
| 8A.3, 8A.4, 8A.6 | Mobile | ⚠️ Implemented; automated checks pass; **not tested on a device** |
| 8A.7 | Both | Backend ✅; Mobile ⚠️ device checklist not run |

**The Phase 8A gate:** proven at API level (notifications for the right person only, rankings, and taste from verified history). The mobile side is **not proven on a device**.

### Limitations and founder actions
1. **Run the 14-item checklist in §11.7**, together with the open Phase 8 checklist, before beta. Item 13 (account isolation) comes first.
2. No push notifications; the count refreshes only when النشاط opens.
3. Rankings and Taste Match are simple rules, to tune after the pilot.
4. **Test data:** `smoke8a-…` accounts remain in the development database.

### Files changed in Phase 8A (summary)
Backend files in §2, Mobile files in §11.2, plus `docs/phase8a-report.md`, `project-progress.md`, `decisions.md`. One migration: `AddNotifications`. No new dependencies.
