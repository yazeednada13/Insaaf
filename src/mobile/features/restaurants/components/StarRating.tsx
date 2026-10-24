import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts } from '@/constants/theme';

interface StarRatingProps {
  rating: number | null;
  size?: 'sm' | 'md';
}

export function StarRating({ rating, size = 'sm' }: StarRatingProps) {
  if (rating == null) {
    return (
      <Text style={[styles.empty, size === 'md' ? styles.md : styles.sm]}>
        لا توجد تقييمات بعد
      </Text>
    );
  }

  const full = Math.floor(rating);
  const half = rating - full >= 0.5;
  const stars = `${'★'.repeat(full)}${half ? '½' : ''}${'☆'.repeat(5 - full - (half ? 1 : 0))}`;

  return (
    <View style={styles.row}>
      <Text style={[styles.stars, size === 'md' ? styles.md : styles.sm]}>{stars}</Text>
      <Text style={[styles.number, size === 'md' ? styles.numberMd : styles.sm]}>
        {rating.toFixed(1)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  empty: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
  },
  md: {
    fontSize: 14,
  },
  number: {
    color: colors.accent,
    fontFamily: fonts.arabicSemiBold,
  },
  numberMd: {
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 4,
  },
  sm: {
    fontSize: 12,
  },
  stars: {
    color: colors.accent,
  },
});
