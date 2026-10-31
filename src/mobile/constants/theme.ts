/**
 * Design tokens from Figma Make (Insaaf mobile app spec).
 * Source: figma.com/make/DfKJchmPebiHH9dRYaYMuS — AuthScreen.tsx + index.css
 */
export const spacing = {
  xs: 4,
  sm: 6,
  md: 16,
  lg: 24,
  xl: 48,
} as const;

export const colors = {
  background: '#FAF9F5',
  surface: '#FFFFFF',
  textPrimary: '#202522',
  textSecondary: '#999999',
  textMuted: '#888888',
  textBody: '#555555',
  textPlaceholder: '#AAAAAA',
  textLabel: '#444444',
  textDivider: '#BBBBBB',
  textDescription: '#666666',
  textFaint: '#CCCCCC',
  primary: '#24352A',
  primaryForeground: '#FAF9F5',
  accent: '#B77945',
  border: '#E9DFC9',
  error: '#C62828',
  overlay: 'rgba(32, 37, 34, 0.5)',
  divider: 'rgba(0,0,0,0.06)',
  skeletonHighlight: '#F0E8D5',
  heroButton: 'rgba(255,255,255,0.9)',
  heroGradient: 'rgba(0,0,0,0.5)',
  openStatusBackground: 'rgba(36,53,42,0.08)',
  closedStatusBackground: 'rgba(0,0,0,0.05)',
} as const;

/**
 * Restaurant discovery tokens from Figma Make
 * (ExploreScreen.tsx, SearchScreen.tsx, RestaurantScreen.tsx, RestaurantCard.tsx, index.css).
 */
export const restaurantLayout = {
  horizontalPadding: 20,
  searchHorizontalPadding: 16,
  headerPaddingTop: 48,
  heroHeight: 220,
  heroButtonSize: 36,
  cardImage: 72,
  cardImageRadius: 12,
  chipRadius: 999,
  cardRadius: 16,
  controlRadius: 12,
  sheetRadius: 20,
  inputBorderWidth: 1.5,
} as const;

export const radius = {
  input: 12,
  button: 16,
} as const;

export const typography = {
  title: 30,
  subtitle: 14,
  body: 14,
  label: 14,
  button: 16,
  divider: 12,
  lineHeightTitle: 36,
  lineHeightBody: 20,
} as const;

export const fonts = {
  arabic: 'IBMPlexSansArabic',
  arabicRegular: 'IBMPlexSansArabic_400Regular',
  arabicMedium: 'IBMPlexSansArabic_500Medium',
  arabicSemiBold: 'IBMPlexSansArabic_600SemiBold',
  arabicBold: 'IBMPlexSansArabic_700Bold',
  // Figma `font-inter`: ratings, price symbols and counts.
  numericRegular: 'Inter_400Regular',
  numericMedium: 'Inter_500Medium',
  numericSemiBold: 'Inter_600SemiBold',
  numericBold: 'Inter_700Bold',
} as const;

/**
 * Start-edge text alignment for the RTL app.
 * React Native swaps 'left' and 'right' when RTL layout is active (Android TextAttributeProps,
 * iOS RCTTextAttributes), so 'left' renders at the start edge: the right side in Arabic.
 * Never use textAlign: 'right' for Arabic text; it renders on the left in RTL.
 */
export const textAlignStart = 'left' as const;

/** Figma (Tailwind) line heights: text-xs, text-sm, text-base, text-2xl, leading-snug/relaxed at 14px. */
export const lineHeights = {
  xs: 16,
  sm: 20,
  base: 24,
  xl2: 32,
  snugSm: 19.25,
  relaxedSm: 22.75,
} as const;

export const authLayout = {
  horizontalPadding: 24,
  headerPaddingTop: 48,
  headerPaddingBottom: 24,
  formGap: 16,
  labelInputGap: 6,
  primaryButtonMarginTop: 8,
  footerPaddingBottom: 32,
  inputBorderWidth: 1.5,
  inputPaddingHorizontal: 16,
  inputPaddingVertical: 14,
  primaryButtonPaddingVertical: 16,
  secondaryButtonPaddingVertical: 14,
} as const;
