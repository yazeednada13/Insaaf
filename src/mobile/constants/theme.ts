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
  textLabel: '#444444',
  textDivider: '#BBBBBB',
  primary: '#24352A',
  primaryForeground: '#FAF9F5',
  accent: '#B77945',
  border: '#E9DFC9',
  error: '#C62828',
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
