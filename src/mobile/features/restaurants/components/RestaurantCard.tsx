import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { StarRating } from '@/features/restaurants/components/StarRating';
import { colors, fonts, restaurantLayout } from '@/constants/theme';
import {
  formatCuisine,
  formatPriceLevel,
  type RestaurantListItemDto,
} from '@/types/restaurant';

interface RestaurantCardProps {
  restaurant: RestaurantListItemDto;
  onPress: () => void;
}

export function RestaurantCard({ restaurant, onPress }: RestaurantCardProps) {
  const cuisine = formatCuisine(restaurant.categories);
  const meta = [restaurant.reviewCount > 0 ? `${restaurant.reviewCount} تقييم` : null, formatPriceLevel(restaurant.priceLevel), cuisine]
    .filter(Boolean)
    .join(' · ');

  return (
    <Pressable onPress={onPress} style={styles.card}>
      {restaurant.imageUrl ? (
        <Image source={{ uri: restaurant.imageUrl }} style={styles.image} />
      ) : (
        <View style={[styles.image, styles.placeholder]} />
      )}
      <View style={styles.body}>
        <Text style={styles.name}>{restaurant.name}</Text>
        <StarRating rating={restaurant.averageRating} />
        {meta ? <Text style={styles.meta}>{meta}</Text> : null}
        <Text style={styles.location}>
          {restaurant.address ? `${restaurant.address}، ${restaurant.city}` : restaurant.city}
        </Text>
      </View>
    </Pressable>
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
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  image: {
    backgroundColor: colors.border,
    borderRadius: 12,
    height: restaurantLayout.cardImage,
    width: restaurantLayout.cardImage,
  },
  location: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    marginTop: 4,
    textAlign: 'right',
  },
  meta: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    marginTop: 4,
    textAlign: 'right',
  },
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    textAlign: 'right',
  },
  placeholder: {
    backgroundColor: colors.border,
  },
});
