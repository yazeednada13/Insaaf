import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import type { RatingBreakdownDto } from '@/types/restaurant';

const ROWS: { key: keyof RatingBreakdownDto; label: string }[] = [
  { key: 'food', label: 'الطعام' },
  { key: 'service', label: 'الخدمة' },
  { key: 'value', label: 'القيمة مقابل السعر' },
  { key: 'atmosphere', label: 'الأجواء' },
];

// Figma RestaurantScreen.tsx "متوسط التقييمات": label (w-28), sand track with copper fill at
// value/5, and the Inter value (w-6). Shown only when the restaurant has public reviews.
export function RatingBreakdownCard({ breakdown }: { breakdown: RatingBreakdownDto }) {
  return (
    <View style={styles.card}>
      <Text style={styles.title}>متوسط التقييمات</Text>
      {ROWS.map(({ key, label }) => (
        <View key={key} style={styles.row}>
          <Text style={styles.label}>{label}</Text>
          <View style={styles.track}>
            <View style={[styles.fill, { width: `${(breakdown[key] / 5) * 100}%` }]} />
          </View>
          <Text style={styles.value}>{breakdown[key].toFixed(1)}</Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    padding: 16,
  },
  fill: {
    backgroundColor: colors.accent,
    borderRadius: 4,
    height: '100%',
  },
  label: {
    color: colors.textBody,
    flexShrink: 0,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
    width: 112,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    marginBottom: 8,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 12,
    textAlign: textAlignStart,
  },
  track: {
    backgroundColor: colors.border,
    borderRadius: 4,
    flex: 1,
    height: 8,
    overflow: 'hidden',
  },
  value: {
    color: colors.accent,
    fontFamily: fonts.numericSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    width: 24,
  },
});
