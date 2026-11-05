import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts } from '@/constants/theme';

interface StarRatingProps {
  rating: number | null;
  size?: 'sm' | 'md' | 'lg';
}

// Figma StarRating.tsx: accent stars (`leading-none`) + Inter number, always LTR.
// Phase 3 has no reviews: a null rating renders five empty stars without a number.
export function StarRating({ rating, size = 'sm' }: StarRatingProps) {
  const value = rating ?? 0;
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  const stars = `${'★'.repeat(full)}${half ? '½' : ''}${'☆'.repeat(5 - full - (half ? 1 : 0))}`;

  return (
    <View style={styles.row}>
      <Text style={[styles.stars, STAR_STYLES[size]]}>{stars}</Text>
      {rating != null ? (
        <Text style={[styles.number, NUMBER_STYLES[size]]}>
          {rating.toFixed(1)}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  number: {
    color: colors.accent,
  },
  numberLg: {
    fontFamily: fonts.numericBold,
    fontSize: 20,
    lineHeight: 28,
  },
  numberMd: {
    fontFamily: fonts.numericSemiBold,
    fontSize: 14,
    lineHeight: 20,
  },
  numberSm: {
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: 16,
  },
  row: {
    alignItems: 'center',
    direction: 'ltr',
    flexDirection: 'row',
    gap: 4,
  },
  stars: {
    color: colors.accent,
  },
  starsLg: {
    fontSize: 18,
    lineHeight: 18,
  },
  starsMd: {
    fontSize: 14,
    lineHeight: 14,
  },
  starsSm: {
    fontSize: 12,
    lineHeight: 12,
  },
});

const STAR_STYLES = { sm: styles.starsSm, md: styles.starsMd, lg: styles.starsLg };
const NUMBER_STYLES = { sm: styles.numberSm, md: styles.numberMd, lg: styles.numberLg };
