# Insaaf — Restaurant / Discovery design tokens (from Figma Make)

**Source:** `figma.com/make/DfKJchmPebiHH9dRYaYMuS`  
**Screens:** `ExploreScreen.tsx`, `SearchScreen.tsx`, `RestaurantScreen.tsx`, `HomeScreen.tsx` (restaurant sections), `RestaurantCard.tsx`, `StarRating.tsx`, `index.css`, `types.ts`, `data/mock.ts`  
**Extracted:** 2026-09-13 (Phase 3 — Task 3.1)

## Shared palette (extends auth tokens)

| Token | Value | Usage |
|-------|-------|-------|
| Screen background | `#FAF9F5` | Explore, search, detail body |
| Surface / card | `#FFFFFF` | Cards, info bar, filter sheet inputs |
| Text primary | `#202522` | Titles, restaurant names |
| Text secondary | `#555555` | Metadata, descriptions |
| Text muted | `#888888` / `#AAAAAA` | Placeholders, counts, empty hints |
| Text faint | `#CCCCCC` | Inline separators in metadata |
| Primary | `#24352A` | Selected chips, CTAs, open badge |
| Primary foreground | `#FAF9F5` | Text on primary chips/buttons |
| Accent / rating | `#B77945` | Star rating, links (“عرض الكل”), recommended label |
| Border / sand | `#E9DFC9` | Search bar, chips, skeleton shimmer |
| Image placeholder | `#E9DFC9` | Card/hero fallback background |
| Divider | `rgba(0,0,0,0.06)` | Section borders |
| Card shadow (horizontal) | `0 1px 6px rgba(0,0,0,0.04)` | List cards |
| Card shadow (vertical) | `0 2px 10px rgba(0,0,0,0.06)` | Carousel cards |
| Overlay scrim | `rgba(32,37,34,0.5)` | Bottom sheet backdrop |
| Hero gradient | `linear-gradient(to top, rgba(0,0,0,0.5) 0%, transparent 60%)` | Detail hero |
| Open badge bg | `rgba(36,53,42,0.08)` / `rgba(36,53,42,0.85)` | Status pills |
| Closed badge bg | `rgba(0,0,0,0.05)` / `rgba(0,0,0,0.5)` | Status pills |
| Rating bar track | `#E9DFC9` | Sub-rating progress (detail — Phase 4+) |
| Reset button bg | `#E9DFC9` | Filter sheet “إعادة تعيين” |

**Typography:** IBM Plex Sans Arabic (primary), Inter (numeric: ratings, price symbols, counts in parentheses).

## Layout tokens

| Token | Value |
|-------|-------|
| Screen horizontal padding | 20px (`px-5`) list/detail; 16px (`px-4`) search |
| Header padding top | 48px (`pt-12`) |
| Section title size | 14px semibold |
| Explore page title | 24px bold (`text-2xl`) |
| Detail hero height | 220px |
| Card border radius | 16px (`rounded-2xl`) |
| Card image radius (horizontal) | 12px (`rounded-xl`) |
| Chip / pill radius | full (`rounded-full`) or 12px (`rounded-xl`) |
| Filter sheet panel radius | 20px top corners |
| Vertical card width | 208px (`w-52`) |
| Horizontal card image | 72×72px |
| Vertical card image height | 120px |
| List item gap | 8px (`gap-2`) |
| Carousel gap | 12px (`gap-3`) |
| Bottom tab safe area | `pb-24` on scroll content |

## Search bar

| State | Border | Background | Placeholder |
|-------|--------|------------|-------------|
| Default (Explore/Home) | `1.5px solid #E9DFC9` | `#FFFFFF` | `#AAAAAA` — “ابحث عن مطعم أو شخص...” |
| Active (Search screen) | `1.5px solid #24352A` | `#FFFFFF` | same |

Padding: 16px horizontal, 12px vertical (`px-4 py-3`). Icon: 16×16, stroke `#AAAAAA` / `#888888`.

## Category chips (Explore)

| State | Background | Text | Border |
|-------|------------|------|--------|
| Selected | `#24352A` | `#FAF9F5` | none |
| Default | `#FFFFFF` | `#555555` | `1.5px solid #E9DFC9` |

Padding: 16px × 8px (`px-4 py-2`). Horizontal scroll row.

**Figma category labels (display):** الكل، برجر، بيتزا، سوشي، قهوة، دجاج، حلويات، إيطالي، لبناني.

## Filter bottom sheet

**Title:** فلترة المطاعم

| Section | Control | Options |
|---------|---------|---------|
| التقييم (الحد الأدنى) | Single-select chips | `3★`, `3.5★`, `4★`, `4.5★` (default `4` in prototype) |
| السعر | Multi-select chips | `$`, `$$`, `$$$`, `$$$$` (Inter font) |
| مفتوح الآن فقط | Toggle | on/off |

**Actions:** إعادة تعيين (secondary, `#E9DFC9` bg) · عرض النتائج (primary, `#24352A` bg).

Chip style matches category chips above.

## Restaurant card — horizontal (list / search)

- White card, 12px padding, 16px radius, light border + shadow.
- **Name:** 14px semibold `#202522`.
- **Open status:** 12px — مفتوح (`#24352A`) / مغلق (`#AAAAAA`), top-right.
- **Rating:** `StarRating` sm — accent stars + numeric (LTR).
- **Meta row:** `{reviewCount} تقييم · {price} · {cuisine}` — 12px `#888888`.
- **Distance row:** map-pin icon 10×10 + `{distance}` — 12px `#AAAAAA`.
- Tap → restaurant detail.

## Restaurant card — vertical (carousel)

- Width 208px; image 120px tall with optional label badge (e.g. موصى به — `#B77945` on white).
- Open/closed pill on image corner.
- Name, rating, `price · cuisine`, distance — same typography as horizontal.

## Star rating component

| Size | Text class | Number style |
|------|------------|--------------|
| sm | `text-xs` | `text-xs font-inter` |
| md | `text-sm` | `text-sm font-semibold font-inter` |
| lg | `text-lg` | `text-xl font-bold font-inter` |

Color: `#B77945`. Direction: `ltr` for stars + number.

**Phase 3 empty state:** when `averageRating` is null, show empty stars or “—” per implementation (no reviews yet).

## Explore screen copy

| Element | Arabic |
|---------|--------|
| Page title | استكشاف |
| Section (all categories) | الأعلى تقييمًا بالقرب منك |
| Filter button | فلترة |
| Empty results title | لا توجد نتائج |
| Empty results hint | جرّب تغيير الفلاتر أو الفئة |

## Search screen copy

| Element | Arabic |
|---------|--------|
| Placeholder | ابحث عن مطعم أو شخص... |
| Cancel | إلغاء |
| Tabs | الكل · المطاعم · الأشخاص |
| Section: restaurants | المطاعم |
| Section: people | الأشخاص |
| Idle hint | ابحث عن مطعم أو شخص |
| No results title | لا نتائج لـ "{query}" |
| No results hint | جرّب كلمة بحث مختلفة |

**Phase 3 scope:** restaurant tab/section only; people search is out of scope (later phase).

## Restaurant detail screen

### Hero (220px)

- Full-bleed image + bottom gradient.
- **Back** (leading): 36×36 circle, `rgba(255,255,255,0.9)` — chevron `#202522`.
- **Actions** (trailing): save + share circles (same style) — **Phase 3: UI optional/deferred** (no save API).
- **Name overlay:** 24px bold white, bottom of hero.

### Info bar (white, bottom border)

- Star rating (md) + `({reviewCount} تقييم)`.
- Meta: `{price} · {cuisine} · {location}` — 12px `#555555`, sand dot separators.
- Hours pill: `مفتوح الآن` / `مغلق` · `{hours}` — **Phase 3: hours/open deferred** (placeholder or hidden until ops data exists).
- **Directions CTA:** الاتجاهات — primary pill, opens maps from lat/lng.

### Content tabs

| Tab | Label | Phase 3 |
|-----|-------|---------|
| details | التفاصيل | **In scope** — description + contact |
| reviews | التقييمات | **Read-only placeholder** — empty state until Phase 4 |
| photos | الصور | **Out of scope** — hide tab or empty state (ADR: media deferred) |

### Details tab sections

- **عن المطعم** — description in white card, 16px radius.
- **متوسط التقييمات** — food / service / value / atmosphere bars — **Phase 4+** (hide or empty in Phase 3).
- **Phone row** — icon + number.

### Reviews tab (Figma reference — Phase 4)

Sub-filters: الكل · زيارات موثقة. Uses `ReviewCard`. **No write-review CTA** on this screen (ADR-004).

## Home screen restaurant sections (reference — not Phase 3 mobile scope)

| Section | Title | Card variant |
|---------|-------|--------------|
| Suggested | مقترح لك | vertical + label “موصى به” |
| Trending | رائج بالقرب منك | horizontal |

“عرض الكل” links use accent `#B77945`. Full home feed is post–Phase 3.

## Loading / empty / error (product requirement)

Per `docs/product-spec.md` and shared `index.css`:

| State | Pattern |
|-------|---------|
| Loading | `.skeleton` shimmer (`#E9DFC9` → `#F0E8D5`) |
| Empty (filters) | Centered icon circle `#E9DFC9`, title + hint (see copy above) |
| Error | Arabic message + retry — mirror auth patterns |
| Animation | `animate-fade-in` on list items |

## Data fields shown in Figma vs Phase 3 backend

| Figma field | Phase 3 API | Notes |
|-------------|-------------|-------|
| `name` | `name` | Arabic |
| `description` | `description` | Detail |
| `location` | `address` + `city` | Display: `{address}، {city}` |
| `price` (`$`…`$$$$`) | `priceLevel` (1–4) | Map in mobile |
| `cuisine` | `categories[]` | Join display with ` · ` |
| `rating` | `averageRating` | `null` in Phase 3 |
| `reviewCount` | `reviewCount` | `0` in Phase 3 |
| `image` | `imageUrl` | Optional; placeholder `#E9DFC9` if null |
| `phone` | `phone` | Optional |
| `latitude` / `longitude` | same | Directions + distance |
| `distance` | — | **Client-computed** from device location |
| `isOpen` / `hours` | — | **Deferred** — filter UI may be disabled in Phase 3 |
| Save / share | — | **Deferred** |
| Reviews / photos tabs | — | Phase 4 / deferred |

## Mobile routing (Phase 3 target)

```
app/restaurants/index.tsx     → Explore / discovery list (Figma ExploreScreen)
app/restaurants/[id].tsx      → Restaurant detail (Figma RestaurantScreen)
```

Search: modal/stack route or dedicated screen — mirror Figma `SearchScreen` restaurant sections. People tab excluded in Phase 3.

Authenticated entry: post-login redirect to `/restaurants` (replaces Phase 2 `app/index.tsx` placeholder in Task 3.12+).
