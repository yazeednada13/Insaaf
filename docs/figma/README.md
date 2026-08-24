# Insaaf — Figma Design Source

**Status:** Figma file link recorded (2026-08-24)

## Purpose

Figma is the **primary visual source of truth** for Insaaf mobile UI. This folder holds design references, exported tokens, and implementation notes.

## Figma file

| Field | Value |
|-------|-------|
| **File URL** | https://www.figma.com/make/DfKJchmPebiHH9dRYaYMuS/File-Management?t=Evgc4tFE4wwzhPu1-1 |
| **Access** | Founder-provided link — confirm view/edit for implementation team |
| **Recorded** | 2026-08-24 |

## Token export workflow (before UI phases)

1. Confirm view/edit access to the Figma file.
2. Export or document design tokens (colors, typography, spacing, radius, shadows).
3. Save exports under `docs/figma/` (e.g. `tokens.json`, `colors.md`, component screenshots).
4. Map tokens to `src/mobile/constants/` during Phase 1+ mobile work.

## Screen implementation checklist

For each screen (per `docs/architecture.md` and `.cursor/rules/ui-ux.mdc`):

1. Inspect Figma layout, typography, spacing, states.
2. Identify reusable components.
3. Implement in Expo Router + feature folders.
4. Compare implementation to Figma.
5. Document trade-offs in `docs/decisions.md` if fidelity cannot be exact.

## Blockers

- Pixel-accurate UI sign-off requires Figma file access and token extraction.
- Do not redesign screens for implementation convenience.
