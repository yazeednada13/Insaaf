import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts } from '@/constants/theme';

interface StarRatingProps {
  rating: number | null;
  size?: 'sm' | 'md';
}

// Figma StarRating.tsx: accent stars (`leading-none`) + Inter number, always LTR.
// Phase 3 has no reviews: a null rating renders five empty stars without a number.
export function StarRating({ rating, size = 'sm' }: StarRatingProps) {
  const isMedium = size === 'md';
  const value = rating ?? 0;
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  const stars = `${'★'.repeat(full)}${half ? '½' : ''}${'☆'.repeat(5 - full - (half ? 1 : 0))}`;

  return (
    <View style={styles.row}>
      <Text style={[styles.stars, isMedium ? styles.starsMd : styles.starsSm]}>{stars}</Text>
      {rating != null ? (
        <Text style={[styles.number, isMedium ? styles.numberMd : styles.numberSm]}>
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
  starsMd: {
    fontSize: 14,
    lineHeight: 14,
  },
  starsSm: {
    fontSize: 12,
    lineHeight: 12,
  },
});
