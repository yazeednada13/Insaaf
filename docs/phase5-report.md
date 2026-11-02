# Phase 5 — QR Verification + Verified Review Creation: التقرير الموحّد

**التاريخ:** 2026-11-02
**الفرع:** `feature/phase5-qr-verification` (من `develop` @ `92689f0`؛ المؤسس بيدير Git)
**المراجع:** الخطة الرئيسية المعتمدة (Phase 5)، خطة Phase 5 المعتمدة (القرارات D-Q1…D-X)، [decisions.md](decisions.md) ADR-004، Figma Make `QRScreen.tsx`
**حالة التقرير:** ✅ **Backend Track مكتمل** · ⏳ Mobile Track لم يبدأ (بانتظار `Phase 5 — Mobile`)

> التقرير بيفصل بين ثلاث أشياء: شو **تحقّقنا منه بالتشغيل**، شو **فحصناه بقراءة الكود**، وشو **مش متحقَّق منه**.

---

## 1. نظرة عامة

### الهدف التجاري
Phase 5 بيبني **المسار الوحيد لإنشاء تقييم في الـ MVP**: QR على الفاتورة → VisitToken → المطعم بيتحدّد على السيرفر → تقييم موثّق. الهدف إنه ما حدا يقدر يكتب تقييم "موثّق" بدون ما يكون معه رمز حقيقي من فاتورة مطعم شريك، وإنه الرمز يشتغل **مرة وحدة بس** وقبل ما تنتهي صلاحيته، وإنه **الـ client ما بيختار المطعم أبداً**.

### حالة المهام (Backend)
| ID | المهمة | الحالة | الدليل |
|---|---|---|---|
| 5.1 | `VisitToken` + القواعد + الشراكة + migration `AddVisitTokens` | ✅ Verified | Build؛ مراجعة الـ migration (إضافات فقط)؛ المؤسس طبّقها (`migrations list` بدون Pending)؛ smoke على SQL Server؛ 8 حالات اختبار للقواعد والأهلية |
| 5.2 | `IVisitTokenRepository` + خدمة الإصدار + الإلغاء | ✅ Verified | اختبارات codec/payload + اختبارات `VisitTokenIssuer` مباشرة (Count 1 و 25، principal مقيّد، رفض غير الشريك) + smoke |
| 5.3 | Redeem + create بـ transaction واحد | ✅ Verified | اختبار تزامن بـ `DbContext` اثنين (InMemory) + **smoke على SQL Server: 5 طلبات متزامنة → 201 واحد و 409 أربعة** |
| 5.4 | API الإنشاء الموثّق | ✅ Verified | 7 اختبارات API + اختبار 429 + smoke |
| 5.5 | Admin API: إصدار، إلغاء، ملخّص، شراكة | ✅ Verified | اختبارات API (401/403/409/201) + smoke |
| 5.9 (Backend) | الاختبارات والتحقق | ✅ | Release: 0 أخطاء، 0 تحذيرات؛ 130/130 اختبار؛ smoke 16/16 PASS |

### القرارات المطبّقة
| # | القرار | التطبيق |
|---|---|---|
| D-Q1 | رمز لكل فاتورة، استخدام واحد، مربوط بالمطعم على السيرفر | ✅ |
| D-I1 | إصدار مستقل عن القناة | ✅ `IVisitTokenIssuer` واحد + `IssuanceChannel` (`Manual = 1` فقط مستخدم؛ `Pos = 2` محجوز بدون تطبيق) + `IssuedByUserId` nullable |
| D-I2 | صلاحية الإصدار لكل مطعم | ✅ `IssuancePrincipal` + فحص النطاق `issuance_forbidden` (403) داخل الخدمة؛ factory وحيد `ForAdmin` |
| D-PL | صيغة الـ payload | ✅ `insaaf://visit/<43 حرف base64url>` مبنيّة ومقروءة من `VisitPayload` فقط |
| D-P1 | الشراكة | ✅ `IsPartner` + `PartnerSince` + `PUT …/partnership` |
| D-E1 | الصلاحية والحجم | ✅ `expiresInDays` افتراضي 90 (1–365)، `count` 1–500 |
| D-T1 | الـ client بيبعت الرمز نفسه | ✅ `token` بالـ resolve والإنشاء |
| D-F2 | النص 0–500 | ✅ `VerifiedReviewValidator.MaxTextLength = 500` |
| D-R | Rate limiting | ✅ مدمج بـ ASP.NET Core: resolve 20/دقيقة، create 5/دقيقة لكل مستخدم، من الإعدادات |
| D-A1 | حساب Admin | ✅ المؤسس فعّل `SeedAdmin` بـ User Secrets؛ السجل: `Development admin user seeded for admin@insaaf.local` |
| D-X | حد تقييمات لكل مستخدم/مطعم | ❌ غير مطبّق (مش بالـ roadmap) |

### انحرافات عن صياغة الخطة (نفس العقد، مسجّلة بأمانة)
1. **اسم خدمة الإلغاء:** الخطة سمّتها `VisitTokenRevoker`؛ المطبّق `VisitTokenLifecycleService` (`IVisitTokenLifecycleService`) لأنها بتجمع الإلغاء **والملخّص**، وكلاهما بنفس فحص النطاق. السلوك نفسه.
2. **ترتيب الحفظ داخل الـ transaction:** الخطة وصفت `SaveChanges` واحد (UPDATE + INSERT). المطبّق **حفظين داخل transaction واحد**: أولاً "حجز" الرمز (UPDATE مع فحص `ConcurrencyStamp`)، بعدين INSERT التقييم، بعدين Commit. **السبب:** هيك الطلب اللي خسر السباق بيفشل قبل ما يحاول يضيف تقييم، وهاد بيشتغل صح حتى على InMemory (اللي ما فيه transactions) — اكتشفناه لما فشل اختبار التزامن بالنسخة الأولى. على SQL Server الاثنين بيـcommit أو بيرجعوا مع بعض.
3. **`IVisitTokenCodec`** فيه `GenerateToken` و `Hash` بس؛ قراءة الـ payload موجودة بـ `VisitPayload.TryExtractToken` (static بالـ Application) بدل `TryParsePayload` على الـ codec، عشان صيغة الـ payload تكون بمكان واحد مستقل عن التشفير.
4. **ملف اختبار:** `CustomWebApplicationFactory` صار مش `sealed` وفيه `ConfigureTestSettings` افتراضي، عشان نعمل host بحدود rate limit منخفضة.

---

## 2. المعمارية

```text
Insaaf.API ──► Insaaf.Application ──► Insaaf.Domain
    │                  ▲                   ▲
    └──► Insaaf.Infrastructure ────────────┘
```
**متحقَّق (grep):** صفر مراجع لـ `Roles.Admin` أو EF Core أو ASP.NET Core داخل Domain و Application. قيد الـ Admin موجود **بالـ controller فقط**.

### ملفات Phase 5 (Backend)
| الطبقة | الملف | المسؤولية |
|---|---|---|
| Domain | `Enums/VisitTokenIssuanceChannel.cs` | `Manual = 1`، `Pos = 2` (محجوز) |
| Domain | `Entities/VisitToken.cs` | الرمز: المطعم، الإصدار، القناة، الـ hash، التواريخ، `ConcurrencyStamp` |
| Domain | `Entities/Restaurant.cs` (معدّل) | `IsPartner`، `PartnerSince`، `VisitTokens` |
| Domain | `Rules/VisitTokenRules.cs` | **المصدر الوحيد** لحالة الرمز (`Valid/Expired/Used/Revoked`) والأهلية (`IsActive && IsPartner`) |
| Domain | `Repositories/IVisitTokenRepository.cs` | العقد: بحث بالـ hash، إضافة، إلغاء، ملخّص، **`RedeemWithReviewAsync`** |
| Domain | `Repositories/IRestaurantRepository.cs` (معدّل) + `Projections/RestaurantPartnershipProjection.cs` | قراءة/تعديل الشراكة |
| Application | `Verification/Abstractions/IVisitTokenCodec.cs` | توليد الرمز وحساب الـ hash (التطبيق بالـ Infrastructure) |
| Application | `Verification/VisitPayload.cs` | بناء وقراءة `insaaf://visit/<token>` |
| Application | `Verification/IssuancePrincipal.cs` | مين بيصدر، كما حدّده السيرفر |
| Application | `Verification/VisitTokenIssuer.cs` | **خدمة الإصدار الوحيدة**، مستقلة عن القناة |
| Application | `Verification/VisitTokenLifecycleService.cs` | الإلغاء والملخّص |
| Application | `Verification/VerificationService.cs` | Resolve و Create |
| Application | `Verification/Validation/VerifiedReviewValidator.cs` | التقييمات 1–5، النص ≤ 500 |
| Application | `Verification/VerificationException.cs` + `Dtos/*` | الأخطاء والعقود |
| Application | `Admin/RestaurantPartnershipService.cs` | الشراكة |
| Infrastructure | `Verification/VisitTokenCodec.cs` | `RandomNumberGenerator` (32 بايت) + SHA-256 |
| Infrastructure | `Persistence/Configurations/VisitTokenConfiguration.cs` | الجدول، CHECKs، الـ indexes، الـ FKs |
| Infrastructure | `ReviewConfiguration.cs`، `RestaurantConfiguration.cs`، `AppDbContext.cs` (معدّلة) | FK + filtered unique، `IsPartner` |
| Infrastructure | `Persistence/Repositories/VisitTokenRepository.cs` | **حدود الـ transaction** |
| Infrastructure | `Persistence/Migrations/20261102022855_AddVisitTokens*.cs` | الـ migration (ولّدها وطبّقها المؤسس) |
| API | `Controllers/VerificationController.cs` | `visits/resolve`، `reviews` |
| API | `Controllers/AdminRestaurantsController.cs` | شراكة، إصدار، إلغاء، ملخّص |
| API | `Extensions/RateLimitingExtensions.cs` | سياسات الـ rate limit + 429 envelope |
| API | `Program.cs`، `ExceptionHandlingMiddleware.cs`، `ApiResponseExtensions.cs`، `appsettings.json` (معدّلة) | التسجيل، معالجة `VerificationException`، `CreatedEnvelope`، الإعدادات |

---

## 3. مسار البيانات من البداية للنهاية

### إصدار رموز: `POST /api/v1/admin/restaurants/{id}/visit-tokens` `{count, expiresInDays}`
1. JwtBearer بيتحقق من التوكن؛ `[Authorize(Roles = "Admin")]` بيرفض غير الـ Admin (401/403).
2. الـ controller بيحسب `ExpiresAt = now + expiresInDays` وبيبني `IssuancePrincipal.ForAdmin(sub)` — **من الـ JWT، مش من الـ body**.
3. `VisitTokenIssuer.IssueAsync`: فحص النطاق → `count` 1–500 → الصلاحية بالمستقبل → المطعم موجود → `IsActive && IsPartner` (وإلا 409 `restaurant_not_partner`).
4. لكل رمز: `codec.GenerateToken()` (256 بت عشوائية) → `codec.Hash()` → `VisitToken` بالـ hash فقط، و `IssuanceChannel`/`IssuedByUserId` من الـ principal، و `IssuanceId` واحد للدفعة.
5. `AddRangeAsync` → EF → `INSERT VisitTokens`.
6. الرد 201: `payloads` = `VisitPayload.Build(token)`. **هاي المرة الوحيدة اللي الرموز الخام بتطلع فيها؛** ما في أي endpoint بيرجّعها بعدين.

### المعاينة: `POST /api/v1/verification/visits/resolve` `{token}`
1. `[Authorize]` + rate limit `verification-resolve`.
2. `VerificationService.ResolveAsync` → `GetRedeemableTokenAsync`: `TryExtractToken` → hash → `FindByHashAsync` (مع المطعم) → `VisitTokenRules.Evaluate` → الأهلية.
3. تفاصيل المطعم من `IRestaurantRepository.GetByIdAsync` (المعدّل والعدد). **ما في استهلاك للرمز.**

### الإنشاء: `POST /api/v1/verification/reviews` `{token, rating, …, text}`
1. `[Authorize]` + rate limit `verification-create`. أي حقل زيادة (مثل `restaurantId`) → 400 من الـ JSON deserializer (`JsonUnmappedMemberHandling.Disallow`).
2. `VerifiedReviewValidator` **قبل** لمس الرمز.
3. نفس `GetRedeemableTokenAsync` (الرمز tracked).
4. بناء `Review`: `RestaurantId = token.RestaurantId`، `IsVerified = true`، `VisitTokenId = token.Id`، `UserId` من الـ JWT.
5. `RedeemWithReviewAsync`:
   ```text
   BEGIN TRAN
     UPDATE VisitTokens SET RedeemedAt, RedeemedByUserId, ConcurrencyStamp=@new
       WHERE Id=@id AND ConcurrencyStamp=@old      ← 0 صفوف = حدا سبقنا → DbUpdateConcurrencyException
     INSERT Reviews (…, VisitTokenId)              ← filtered UNIQUE index = خط حماية أخير (2601/2627)
   COMMIT
   ```
   أي فشل من الاثنين → rollback → `AlreadyRedeemed` → 409 `token_used`.
6. 201 `{reviewId, restaurantId}`. القراءة العلنية لسا بتمرّ من `ReviewVisibility.IsPublic`، فالتقييم بيطلع فوراً بالقائمة وبالمعدّلات.

---

## 4. الكود المهم وليش موجود
- **`VisitTokenRules`** — قاعدة وحدة للحالة بترتيب أولوية ثابت: مستخدَم → ملغى → منتهي → صالح. كل الخدمات بتستعملها، فما في مكان ثاني بيقرّر "هل الرمز صالح".
- **`IssuancePrincipal` + `VisitTokenIssuer`** — الإصدار ما بيعرف شي اسمه "Admin". بيعرف بس principal فيه قناة ومستخدم اختياري ونطاق مطعم اختياري. اليوم الـ Admin غير مقيّد؛ بالمستقبل قناة POS بتبني principal مقيّد بمطعمها من الـ credential، والخدمة نفسها بترفض أي مطعم ثاني. مختبَر بـ principal مقيّد مبني بالاختبار فقط.
- **`VisitTokenCodec`** — نفس نمط `RefreshTokenService`: عشوائية تشفيرية، وتخزين hash فقط. لو تسرّبت الداتابيس، ما في رموز قابلة للاستعمال.
- **`VisitPayload`** — مكان واحد للصيغة؛ تغييرها لاحقاً لرابط `https` ما بيلمس الـ API.
- **`VisitTokenRepository.RedeemWithReviewAsync`** — قلب الضمان "تقييم واحد لكل رمز".
- **`RevokeUnusedAsync`** — بيغيّر `ConcurrencyStamp` كمان، فالإلغاء ما بيقدر يكتب فوق استهلاك صار بنفس اللحظة؛ عند التعارض بيعيد المحاولة (حتى 3 مرات).
- **`RateLimitingExtensions`** — fixed window مقسوم حسب `sub` (أو IP بدون مستخدم)، ورد 429 بنفس الـ envelope.

---

## 5. عقود الـ API

كل الردود بالـ envelope `{ success, data, errors: [{code, message}], meta: { requestId } }`.

### العميل (`[Authorize]` — User أو Admin)
| Method | Route | Body | النجاح |
|---|---|---|---|
| POST | `/api/v1/verification/visits/resolve` | `{ "token": "<الرمز أو الـ payload كامل>" }` | 200 `{ restaurant: { id, name, city, address, imageUrl, averageRating, reviewCount } }` |
| POST | `/api/v1/verification/reviews` | `{ token, rating, foodRating, serviceRating, valueRating, atmosphereRating, text }` | 201 `{ reviewId, restaurantId }` |

| Status | code | متى |
|---|---|---|
| 400 | `token_invalid` | مش معروف، صيغة غلط، ملغى، أو المطعم مش فعّال/مش شريك (ما بنكشف الفرق) |
| 410 | `token_expired` | `ExpiresAt <= now` |
| 409 | `token_used` | مستخدَم قبل، أو خسر السباق |
| 400 | `validation_error` | تقييم برّا 1–5، نص > 500، حقل زيادة (مثل `restaurantId`) |
| 401 | `unauthorized` | بدون توكن |
| 429 | `rate_limited` | تجاوز الحد |

**ما في** `POST /api/v1/reviews` (404) ولا `POST /api/v1/restaurants/{id}/reviews` (405).

### Admin (`[Authorize(Roles = "Admin")]`)
| Method | Route | Body | النجاح |
|---|---|---|---|
| PUT | `/api/v1/admin/restaurants/{id}/partnership` | `{ "isPartner": true }` | 200 `{ restaurantId, isActive, isPartner, partnerSince }` |
| POST | `/api/v1/admin/restaurants/{id}/visit-tokens` | `{ "count": 1–500, "expiresInDays": 1–365 (افتراضي 90) }` | 201 `{ issuanceId, restaurantId, expiresAt, payloads[] }` |
| POST | `/api/v1/admin/restaurants/{id}/visit-tokens/revoke` | `{ "issuanceId": "…" }` أو `{}` | 200 `{ revokedCount }` |
| GET | `/api/v1/admin/restaurants/{id}/visit-tokens/summary` | — | 200 `{ active, redeemed, expired, revoked }` |

الأخطاء: 401، 403، 404 `not_found`، 409 `restaurant_not_partner`، 400 `validation_error`، (403 `issuance_forbidden` موجود بالخدمة لكن ما بيصير عملياً للـ Admin لأنه غير مقيّد).

**القيود:** ما في طريقة لاسترجاع payload بعد الإصدار؛ ما في endpoint لعرض الرموز فردياً؛ إلغاء رمز واحد بعينه غير ممكن (بالإصدار أو بالمطعم كامل).

---

## 6. الداتابيس وقواعد العمل

### Migration `20261102022855_AddVisitTokens` (إضافات فقط)
- **`Restaurants`:** `IsPartner bit NOT NULL DEFAULT 0`، `PartnerSince datetime2 NULL`. كل المطاعم الحالية بتبلّش مش شريكة.
- **`VisitTokens`:** الأعمدة حسب الخطة. CHECKs:
  - `CK_VisitTokens_ExpiresAfterIssued`: `[ExpiresAt] > [IssuedAt]`
  - `CK_VisitTokens_RedeemerRequiresRedemption`: `[RedeemedByUserId] IS NULL OR [RedeemedAt] IS NOT NULL`
  - `CK_VisitTokens_ManualRequiresIssuer`: `[IssuanceChannel] <> 1 OR [IssuedByUserId] IS NOT NULL`
  - FKs: `Restaurants` (Cascade)؛ `AspNetUsers` ×2 (Restrict).
  - Indexes: `TokenHash` UNIQUE، `IssuanceId`، `(RestaurantId, RedeemedAt, RevokedAt)`، + indexes الـ FKs.
- **`Reviews`:** FK `VisitTokenId → VisitTokens` (Restrict) + `IX_Reviews_VisitTokenId` UNIQUE `WHERE [VisitTokenId] IS NOT NULL`. قيد Phase 4 `IsVerified = 0 OR VisitTokenId IS NOT NULL` ما تغيّر.

### قواعد العمل
- الأهلية `IsActive && IsPartner` بتنفحص عند **الإصدار والمعاينة والإنشاء**؛ إنهاء الشراكة بيبطل كل الرموز الموجودة فوراً.
- الحالة: مستخدَم ← ملغى ← منتهي ← صالح؛ عند `ExpiresAt == now` الرمز منتهي.
- الرمز ما بيتغيّر مطعمه أبداً؛ التقييم بياخد المطعم من الرمز فقط.
- ما في seed لرموز أو شراكات.

---

## 7. التحقق

### تم تشغيله (2026-11-02)
| الفحص | النتيجة |
|---|---|
| `dotnet build -c Release` | ✅ 0 أخطاء، 0 تحذيرات |
| `Insaaf.Application.Tests` | ✅ 83/83 (منها 27 حالة Phase 5: 17 primitives + 10 flow، شاملة حالات الـ theories) |
| `Insaaf.API.Tests` | ✅ 47/47 (منها 8 Phase 5) |
| فحص الطبقات (grep) | ✅ صفر نتائج |
| مراجعة الـ migration | ✅ إضافات فقط، `Down` صحيح |
| تطبيق الـ migration | ✅ نفّذه المؤسس؛ `migrations list` بدون Pending |
| Seed الـ Admin | ✅ سجل الـ API |
| **Smoke على SQL Server** (سكربت شغّله المؤسس، الـ API على 5232) | ✅ **16/16 PASS** |

**نتائج الـ smoke:**
- تسجيل دخول Admin 200؛ إصدار كـ User 403؛ بدون توكن 401.
- إصدار لمطعم مش شريك 409 `restaurant_not_partner`؛ تفعيل الشراكة 200 مع `partnerSince`؛ إصدار رمزين 201.
- Resolve 200 بمطعم الرمز (Burger Makers).
- `restaurantId` بالـ body → 400 `validation_error`؛ `POST /reviews` → 404.
- **5 طلبات إنشاء متزامنة بنفس الرمز → `409,409,409,409,201`** — هاد الإثبات الحقيقي إنه الـ transaction + `ConcurrencyStamp` شغّالين على SQL Server.
- تقييم واحد علني وموثّق بالقائمة؛ resolve بعدها 409 `token_used`.
- الإلغاء 200 (`revoked=1`)؛ resolve لرمز ملغى 400 `token_invalid`.
- الملخّص: `active=0 redeemed=1 expired=0 revoked=1`.

### مش متحقَّق منه
- **410 على SQL Server:** مختبَر بالـ API tests (InMemory) فقط؛ ما قدرنا نعمل رمز منتهي بالـ smoke بدون تعديل مباشر بالداتابيس.
- **429 على SQL Server:** مختبَر بالـ API tests فقط.
- **وجود الـ CHECK constraints فعلياً بالداتابيس:** متحقَّق من ملف الـ migration، مش باستعلام على `sys.check_constraints`. استعلام اختياري للمؤسس:
  ```sql
  SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('VisitTokens');
  SELECT name, has_filter, filter_definition FROM sys.indexes WHERE name = 'IX_Reviews_VisitTokenId';
  ```
- **مسار الـ unique violation (2601/2627):** ما انضرب بالـ smoke لأن `ConcurrencyStamp` منع التكرار قبله؛ موجود كخط حماية أخير بالكود.
- **الحالة `issuance_forbidden`** مختبَرة على مستوى الخدمة فقط (ما في قناة مقيّدة حالياً).

### آثار الـ smoke على داتابيس التطوير
- مطعم `Burger Makers` (`e7146302-9d94-45dc-aa19-7bed4b44c48e`) صار شريك.
- تقييم موثّق واحد بنص "Phase 5 smoke test".
- حسابين `smoke-…@example.com`، ورمزين (واحد مستخدم وواحد ملغى).

---

## 8. تسليم للـ Mobile
- **Base URL:** نفس Phase 3/4 (`EXPO_PUBLIC_API_BASE_URL` + `/api/v1`). كل الطلبات بـ Bearer عبر الـ Axios interceptor الموجود (refresh تلقائي على 401).
- **قراءة الـ QR:** اقبل بس `insaaf://visit/` + 43 حرف `[A-Za-z0-9_-]`؛ أي شي ثاني → شاشة "رمز QR غير صالح لإنصاف" بدون ما تتصل بالسيرفر. القاعدة لازم تطابق `VisitPayload` (ابعت الـ payload كامل أو الرمز؛ السيرفر بيقبل الاثنين).
- **الربط بشاشات Figma:** `token_invalid` → "غير صالح"؛ `token_expired` (410) → "انتهت صلاحية رمز الفاتورة"؛ `token_used` (409) → "تم استخدام هذا الرمز مسبقًا"؛ `rate_limited` و شبكة → خطأ مع "إعادة المحاولة".
- **الإنشاء:** **ممنوع** إرسال `restaurantId` أو أي حقل زيادة (400). الحقول: `token` + 5 تقييمات (1–5، D-F1 كلها مطلوبة بالـ UI) + `text` (0–500).
- **بعد النجاح:** invalidate لـ `['restaurants','detail',id]`، `['restaurants','list']`، `['reviews','restaurant',id]` (`id` = `restaurantId` من الرد).
- **مخاطر:** `expo-camera` بيحتاج EAS build جديد للـ development app؛ لازم زر إرسال واحد (disabled أثناء الإرسال) عشان ما نستهلك الرمز وما نعرض 409 مربك؛ حد 5 إنشاءات/دقيقة.
- **للاختبار على الجهاز:** المؤسس بيصدر رموز من Admin API (Postman/السكربت) ويحوّلها لـ QR بأي مولّد.

### عملية التوزيع المؤقتة للـ pilot (برّا الكود، مش جزء من المعمارية)
1. Admin بيفعّل شراكة المطعم بعد الاتفاق.
2. Admin بيصدر دفعة (`count`، `expiresInDays`).
3. العمليات بيحوّلوا كل payload لـ QR منفصل (ستيكر).
4. بيتسلّموا للمطعم بشكل آمن.
5. الموظفين بيلزقوا رمز **مختلف** على كل فاتورة.
6. لو ضاعت دفعة → إلغاء بالـ `IssuanceId` وإصدار جديد.

### اتجاه POS المستقبلي (غير مطبّق)
قناة POS لاحقاً = caller جديد (endpoint تكامل بـ credential مربوط بمطعم واحد، و adapters لكل مزوّد) بينادي نفس `IVisitTokenIssuer` بـ `Count = 1` و principal مقيّد. **ما بيتغيّر** الـ resolve ولا الإنشاء ولا الاستهلاك ولا جدول الرموز. ما في دور restaurant-owner ولا modules خاصة بـ POS بـ Phase 5.

---

## 9. ملخّص تعليمي (Backend)
- **ليش رمز لكل فاتورة مش QR ثابت للمطعم؟** الـ QR الثابت لو انصوّر مرة بيصير "تصريح دائم" لتقييمات وهمية. رمز لمرة وحدة بصلاحية محدودة بيربط التقييم بزيارة حقيقية.
- **ليش hash بس؟** نفس منطق كلمات السر: الداتابيس بتعرف تتحقق من الرمز بدون ما تقدر ترجّعه. الرمز الخام بيعيش على الفاتورة وبجيب العميل بس.
- **ليش السيرفر بيحدد المطعم؟** الرمز هو الدليل؛ المطعم خاصية للرمز مش اختيار للمستخدم. لهيك الـ DTO بيرفض `restaurantId` نهائياً بدل ما يتجاهله، عشان أي محاولة تلاعب تبين بوضوح.
- **optimistic concurrency:** بدل ما نقفل الصف، كل رمز معه `ConcurrencyStamp`. الـ UPDATE بيشترط إنه الختم ما تغيّر من لحظة القراءة؛ أول طلب بينجح وبيغيّر الختم، والباقي بيلاقوا 0 صفوف ← 409. الـ transaction بيضمن إنه الرمز والتقييم مع بعض أو ولا واحد، والـ filtered unique index ضمانة أخيرة على مستوى الداتابيس حتى لو الكود تغيّر.
- **الفصل بين "مين بيصدر" و "كيف بنصدر":** الـ controller بيقرّر الهوية (Admin من الـ JWT)، والخدمة بتطبّق القواعد على أي هوية. هاد اللي بيخلّي إضافة POS لاحقاً إضافة مش إعادة كتابة.
- **Clean Architecture بالممارسة:** القواعد (Domain) ما بتعرف EF؛ الخدمات (Application) ما بتعرف HTTP ولا الأدوار؛ التشفير والـ transaction (Infrastructure) وراء interfaces؛ الـ API رفيعة: مصادقة، rate limit، وتحويل الأخطاء.

---

## 10. Mobile Track
⏳ **لم يبدأ.** بانتظار تعليمات المؤسس `Phase 5 — Mobile` (المهام 5.6–5.9 Mobile حسب الخطة المعتمدة).
