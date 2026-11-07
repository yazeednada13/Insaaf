import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { MapPinIcon } from '@/features/restaurants/components/RestaurantIcons';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { formatCuisine, formatPriceLevel, type RestaurantListItemDto } from '@/types/restaurant';

interface RestaurantTileProps {
  restaurant: RestaurantListItemDto;
  distance: string | null;
  label?: string;
  onPress: () => void;
}

// Figma RestaurantCard.tsx, vertical variant (Home "مقترح لك"). The label sits at the start corner
// and the open status at the end corner, as Figma places them in RTL.
export function RestaurantTile({ restaurant, distance, label, onPress }: RestaurantTileProps) {
  const cuisine = formatCuisine(restaurant.categories);

  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={styles.card}>
      <View style={styles.image}>
        {restaurant.imageUrl ? <Image source={{ uri: restaurant.imageUrl }} style={StyleSheet.absoluteFill} /> : null}
        {label ? (
          <View style={[styles.badge, styles.labelBadge]}>
            <Text style={styles.badgeText}>{label}</Text>
          </View>
        ) : null}
        {restaurant.isOpen === null ? null : (
          <View style={[styles.badge, styles.statusBadge, !restaurant.isOpen && styles.closedBadge]}>
            <Text style={styles.badgeText}>{restaurant.isOpen ? 'مفتوح' : 'مغلق'}</Text>
          </View>
        )}
      </View>
      <View style={styles.body}>
        <Text numberOfLines={1} style={styles.name}>{restaurant.name}</Text>
        <StarRating rating={restaurant.averageRating} size="sm" />
        <Text numberOfLines={1} style={styles.meta}>
          {formatPriceLevel(restaurant.priceLevel)}
          {cuisine ? ` · ${cuisine}` : ''}
        </Text>
        {distance ? (
          <View style={styles.distance}>
            <MapPinIcon color={colors.textPlaceholder} size={10} strokeWidth={2} />
            <Text style={styles.meta}>{distance}</Text>
          </View>
        ) : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  badge: {
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 2,
    position: 'absolute',
    top: 8,
  },
  badgeText: {
    color: '#FFFFFF',
    fontFamily: fonts.arabicMedium,
    fontSize: 10,
    lineHeight: 15,
  },
  body: {
    alignItems: 'flex-start',
    gap: 4,
    padding: 12,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    boxShadow: '0px 2px 10px rgba(0, 0, 0, 0.06)',
    overflow: 'hidden',
    width: 208,
  },
  closedBadge: {
    backgroundColor: 'rgba(0,0,0,0.5)',
  },
  distance: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 4,
  },
  image: {
    backgroundColor: colors.border,
    height: 120,
  },
  labelBadge: {
    backgroundColor: colors.accent,
    start: 8,
  },
  meta: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: textAlignStart,
  },
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.snugSm,
    textAlign: textAlignStart,
  },
  statusBadge: {
    backgroundColor: 'rgba(36,53,42,0.85)',
    end: 8,
  },
});
