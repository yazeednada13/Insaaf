# Insaaf — Product Specification (MVP)

**Version:** 1.0  
**Last updated:** 2026-08-24

## Product

**Insaaf** (إنصاف) — mobile application for trustworthy restaurant reviews and social food discovery.

**Hero positioning:** Discover restaurants as they are—not as they're marketed.

## Core concepts

### 1. Verified restaurant reviews (MVP differentiator)

- User scans **QR code** on restaurant invoice
- System validates visit token and resolves **restaurant server-side**
- User completes **verified review** — cannot change restaurant
- **MVP:** this is the **only** path to create a review
- Verified status visible per Figma

### 2. Social discovery

- Following-oriented feed of reviewer content
- Like and comment on reviews
- Follow reviewers
- Discover trusted reviewers
- Social engagement does **not** affect star ratings

### 3. Restaurant discovery

- Browse, search, filter restaurants
- View detail, ratings, **read** reviews
- Filters (product-driven): rating, price, location, cuisine/category

## Users and roles (MVP)

| Role | MVP capabilities |
|------|------------------|
| **User** | Discover, scan QR, write verified reviews, social, profile |
| **Admin** | Seed restaurants, issue visit tokens, moderate (API-only) |

**Not in MVP:** `RestaurantOwner` role, owner self-service app, web dashboard.

## MVP scope summary

| In MVP | Not in MVP |
|--------|------------|
| Verified review via QR | Unverified review create path/UX |
| Read review lists + aggregates | Photo upload (unless Figma requires — pending) |
| Restaurant discovery + filters | Restaurant owner app |
| Likes, comments, follows, feed | Push notifications (post-MVP default) |
| Public profiles | Cloud production hosting (Phase 11) |
| Admin-seeded Amman pilot data | Social login (email/password first) |

## Pilot geography

- **Initial seed:** Amman, Jordan partner venues and catalog data
- **Model:** generic city/location fields — expandable to other cities

## UI/UX

- **Figma** is the visual source of truth ([figma/README.md](figma/README.md))
- **RTL-first** Arabic layout
- All data-driven screens: loading, empty, error, retry per Figma

## Verified review rules (non-negotiable)

1. QR → token validation → server sets restaurant
2. Client cannot override `RestaurantId` on create
3. No standalone "Write Review" on restaurant detail in MVP
4. Future unverified reviews: founder approval + ADR required

## Success criteria (MVP beta)

- Diners can discover Amman restaurants in app
- Partner venues: scan QR → verified review with locked restaurant
- Social interactions on verified reviews
- Following feed works
- Ops can seed data and issue tokens without owner app

## Related documents

- [architecture.md](architecture.md)
- [implementation-plan.md](implementation-plan.md)
- [decisions.md](decisions.md)
