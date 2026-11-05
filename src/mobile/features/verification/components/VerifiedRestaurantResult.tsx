import { Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import { formatLocation } from '@/types/restaurant';
import type { VerifiedRestaurantDto } from '@/types/verification';

interface VerifiedRestaurantResultProps {
  restaurant: VerifiedRestaurantDto;
  onContinue: () => void;
}

// Figma QRScreen.tsx "result" view. The restaurant comes from the server-resolved token and is
// display-only: there is no way to pick or change it.
export function VerifiedRestaurantResult({ restaurant, onContinue }: VerifiedRestaurantResultProps) {
  return (
    <View style={styles.screen}>
      <View style={styles.content}>
        <View style={styles.checkCircle}>
          <Text style={styles.check}>✓</Text>
        </View>

        <View style={styles.heading}>
          <Text style={styles.eyebrow}>أنت تقيّم الآن</Text>
          <Text style={styles.name}>{restaurant.name}</Text>
          <VerifiedBadge size="md" />
        </View>

        <View style={styles.card}>
          <Text style={styles.cardLabel}>المطعم</Text>
          <Text style={styles.cardName}>{restaurant.name}</Text>
          <Text style={styles.cardLocation}>{formatLocation(restaurant.city, restaurant.address)}</Text>
          <View style={styles.cardRating}>
            <StarRating rating={restaurant.averageRating} size="sm" />
          </View>
        </View>

        <Text style={styles.lockNote}>لا يمكنك تغيير المطعم — يتم تحديده تلقائيًا من رمز الفاتورة</Text>
      </View>

      <View style={styles.footer}>
        <Pressable accessibilityRole="button" onPress={onContinue} style={styles.button}>
          <Text style={styles.buttonLabel}>متابعة التقييم</Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 16,
    paddingVertical: 16,
  },
  buttonLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  card: {
    alignSelf: 'stretch',
    backgroundColor: colors.surface,
    borderColor: 'rgba(36,53,42,0.1)',
    borderRadius: 16,
    borderWidth: 1.5,
    padding: 16,
  },
  cardLabel: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginBottom: 4,
    textAlign: textAlignStart,
  },
  cardLocation: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginTop: 2,
    textAlign: textAlignStart,
  },
  cardName: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
    textAlign: textAlignStart,
  },
  cardRating: {
    alignItems: 'flex-start',
    marginTop: 8,
  },
  check: {
    color: colors.primary,
    fontSize: 24,
    lineHeight: 32,
  },
  checkCircle: {
    alignItems: 'center',
    backgroundColor: 'rgba(36,53,42,0.08)',
    borderRadius: 32,
    height: 64,
    justifyContent: 'center',
    width: 64,
  },
  content: {
    alignItems: 'center',
    flex: 1,
    gap: 20,
    justifyContent: 'center',
  },
  eyebrow: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 8,
    textAlign: 'center',
  },
  footer: {
    paddingBottom: 16,
  },
  heading: {
    alignItems: 'center',
  },
  lockNote: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: 'center',
  },
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 24,
    lineHeight: lineHeights.xl2,
    marginBottom: 12,
    textAlign: 'center',
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
    paddingHorizontal: 24,
  },
});
