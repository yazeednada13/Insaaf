# Phase 8A — Home, Notifications and Taste Match: consolidated report

**Date:** 2026-11-07
**Branch:** `feature/phase8a-home-notifications` (from `develop` after the Phase 8 merge; the founder manages Git)
**References:** the roadmap (Phase 8A), the approved Phase 8A plan (decisions D-1…D-11), [decisions.md](decisions.md) ADR-014/ADR-016, Figma Make `DfKJchmPebiHH9dRYaYMuS` (`HomeScreen.tsx`, `ActivityScreen.tsx`, `ProfileScreen.tsx`)
**Report status:** ✅ **Backend Track complete** · ⏳ Mobile Track not started (waiting for `Phase 8A — Mobile`)

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
⏳ **Not started.** Waiting for the founder's instruction: `Phase 8A — Mobile`.
