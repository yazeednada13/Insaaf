import { Fragment } from 'react';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { FadeInView } from '@/features/restaurants/components/FadeInView';
import { MapPinIcon } from '@/features/restaurants/components/RestaurantIcons';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import {
  formatCuisine,
  formatPriceLevel,
  type RestaurantListItemDto,
} from '@/types/restaurant';

interface RestaurantCardProps {
  restaurant: RestaurantListItemDto;
  // Client-computed from device location; null hides the row (permission denied or unknown).
  distance: string | null;
  onPress: () => void;
}

// Figma RestaurantCard.tsx (horizontal variant).
export function RestaurantCard({ restaurant, distance, onPress }: RestaurantCardProps) {
  const meta = [
    `${restaurant.reviewCount} تقييم`,
    formatPriceLevel(restaurant.priceLevel),
    formatCuisine(restaurant.categories) || null,
  ].filter((part): part is string => Boolean(part));

  return (
    <FadeInView>
      <Pressable accessibilityRole="button" onPress={onPress} style={styles.card}>
        <View style={styles.imageFrame}>
          {restaurant.imageUrl ? (
            <Image source={{ uri: restaurant.imageUrl }} style={styles.image} />
          ) : null}
        </View>
        <View style={styles.body}>
          <View style={styles.titleRow}>
            <Text style={styles.name}>{restaurant.name}</Text>
            {restaurant.isOpen != null ? (
              <Text style={[styles.status, restaurant.isOpen ? styles.statusOpen : styles.statusClosed]}>
                {restaurant.isOpen ? 'مفتوح' : 'مغلق'}
              </Text>
            ) : null}
          </View>
          <StarRating rating={restaurant.averageRating} />
          <View style={styles.metaRow}>
            {meta.map((part, index) => (
              <Fragment key={part}>
                {index > 0 ? <Text style={styles.separator}>·</Text> : null}
                <Text style={styles.meta}>{part}</Text>
              </Fragment>
            ))}
          </View>
          {distance ? (
            <View style={styles.distanceRow}>
              <MapPinIcon color={colors.textPlaceholder} size={10} strokeWidth={2} />
              <Text style={styles.distance}>{distance}</Text>
            </View>
          ) : null}
        </View>
      </Pressable>
    </FadeInView>
  );
}

const styles = StyleSheet.create({
  body: {
    flex: 1,
    minWidth: 0,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    boxShadow: '0px 1px 6px rgba(0, 0, 0, 0.04)',
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  distance: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  distanceRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 4,
    marginTop: 4,
  },
  image: {
    height: '100%',
    width: '100%',
  },
  imageFrame: {
    backgroundColor: colors.border,
    borderRadius: restaurantLayout.cardImageRadius,
    height: restaurantLayout.cardImage,
    overflow: 'hidden',
    width: restaurantLayout.cardImage,
  },
  meta: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  metaRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 4,
  },
  name: {
    color: colors.textPrimary,
    flexShrink: 1,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.snugSm,
    textAlign: textAlignStart,
  },
  status: {
    flexShrink: 0,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  statusClosed: {
    color: colors.textPlaceholder,
  },
  statusOpen: {
    color: colors.primary,
  },
  // Figma: name and open status share the first row (`justify-between items-start gap-2`).
  titleRow: {
    alignItems: 'flex-start',
    flexDirection: 'row',
    gap: 8,
    justifyContent: 'space-between',
  },
  separator: {
    color: colors.textFaint,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
});
