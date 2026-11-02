import { router, useLocalSearchParams } from 'expo-router';
import { Fragment, useState } from 'react';
import {
  Image,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ChevronLeftIcon, PhoneIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListErrorState,
  SkeletonBlock,
} from '@/features/restaurants/components/RestaurantListStates';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { useRestaurantDetailQuery } from '@/features/restaurants/hooks/useRestaurantQueries';
import { RatingBreakdownCard } from '@/features/reviews/components/RatingBreakdownCard';
import { RestaurantReviewsList } from '@/features/reviews/components/RestaurantReviewsList';
import { useRestaurantReviewsQuery } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import { RestaurantApiError } from '@/services/restaurants/restaurantErrors';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import {
  formatCuisine,
  formatLocation,
  formatPriceLevel,
  type RestaurantDetailDto,
} from '@/types/restaurant';

type DetailTab = 'details' | 'reviews';

// Figma RestaurantScreen.tsx tabs. The photos tab is out of scope (media deferred).
const TABS: { id: DetailTab; label: string }[] = [
  { id: 'details', label: 'التفاصيل' },
  { id: 'reviews', label: 'التقييمات' },
];

function goBack() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/restaurants');
  }
}

function openDirections(restaurant: RestaurantDetailDto) {
  const destination = `${restaurant.latitude},${restaurant.longitude}`;
  void Linking.openURL(`https://www.google.com/maps/dir/?api=1&destination=${destination}`);
}

// Load the next reviews page when the scroll position is this close to the bottom.
const LOAD_MORE_THRESHOLD = 200;

function isNearBottom({ nativeEvent }: NativeSyntheticEvent<NativeScrollEvent>) {
  const { contentOffset, contentSize, layoutMeasurement } = nativeEvent;
  return layoutMeasurement.height + contentOffset.y >= contentSize.height - LOAD_MORE_THRESHOLD;
}

function BackButton() {
  return (
    <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={goBack} style={styles.heroButton}>
      <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
    </Pressable>
  );
}

export function RestaurantDetailScreen() {
  const insets = useSafeAreaInsets();
  const { id } = useLocalSearchParams<{ id: string }>();
  const detailQuery = useRestaurantDetailQuery(id);
  const [tab, setTab] = useState<DetailTab>('details');
  // Fetched when the reviews tab is first opened; cached afterwards.
  const reviewsQuery = useRestaurantReviewsQuery(id, tab === 'reviews');

  const onContentScroll = (event: NativeSyntheticEvent<NativeScrollEvent>) => {
    if (
      tab === 'reviews'
      && reviewsQuery.hasNextPage
      && !reviewsQuery.isFetchingNextPage
      && isNearBottom(event)
    ) {
      void reviewsQuery.fetchNextPage();
    }
  };

  // Figma renders screens below the 44px status bar, so the hero starts under the safe area.
  const screenStyle = [styles.screen, { paddingTop: insets.top }];

  if (detailQuery.isPending) {
    return (
      <View style={screenStyle}>
        <SkeletonBlock style={styles.hero} />
        <View style={styles.infoBar}>
          <SkeletonBlock style={styles.skeletonLineWide} />
          <SkeletonBlock style={styles.skeletonLine} />
        </View>
      </View>
    );
  }

  if (detailQuery.isError) {
    const isNotFound =
      detailQuery.error instanceof RestaurantApiError && detailQuery.error.code === 'not_found';

    return (
      <View style={screenStyle}>
        <View style={styles.errorHeader}>
          <BackButton />
        </View>
        <RestaurantListErrorState
          message={detailQuery.error.message}
          onRetry={isNotFound ? undefined : () => void detailQuery.refetch()}
        />
      </View>
    );
  }

  const restaurant = detailQuery.data;
  const textMeta = [
    formatCuisine(restaurant.categories) || null,
    formatLocation(restaurant.city, restaurant.address),
  ].filter((part): part is string => Boolean(part));

  return (
    <View style={screenStyle}>
      <View style={styles.hero}>
        {restaurant.imageUrl ? (
          <Image source={{ uri: restaurant.imageUrl }} style={StyleSheet.absoluteFill} />
        ) : null}
        <View pointerEvents="none" style={styles.heroGradient} />
        {/* Save and share actions are deferred in Phase 3 (no saved-restaurants API). */}
        <View style={styles.heroActions}>
          <BackButton />
        </View>
        <Text style={styles.heroName}>{restaurant.name}</Text>
      </View>

      <View style={styles.infoBar}>
        <View style={styles.infoRow}>
          <View style={styles.infoText}>
            <View style={styles.ratingRow}>
              <StarRating rating={restaurant.averageRating} size="md" />
              <Text style={styles.reviewCount}>({restaurant.reviewCount} تقييم)</Text>
            </View>
            <View style={styles.metaRow}>
              <Text style={styles.price}>{formatPriceLevel(restaurant.priceLevel)}</Text>
              {textMeta.map((part) => (
                <Fragment key={part}>
                  <Text style={styles.metaSeparator}>·</Text>
                  <Text style={styles.meta}>{part}</Text>
                </Fragment>
              ))}
            </View>
            {restaurant.isOpen != null ? (
              <View style={styles.statusRow}>
                <Text
                  style={[
                    styles.statusPill,
                    restaurant.isOpen ? styles.statusPillOpen : styles.statusPillClosed,
                  ]}
                >
                  {restaurant.isOpen ? 'مفتوح الآن' : 'مغلق'}
                  {restaurant.todayHours ? ` · ${restaurant.todayHours}` : ''}
                </Text>
              </View>
            ) : null}
          </View>
          <Pressable
            accessibilityRole="button"
            onPress={() => openDirections(restaurant)}
            style={styles.directions}
          >
            <Text style={styles.directionsLabel}>الاتجاهات</Text>
          </Pressable>
        </View>
      </View>

      <View style={styles.tabs}>
        {TABS.map((item) => (
          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected: tab === item.id }}
            key={item.id}
            onPress={() => setTab(item.id)}
            style={[styles.tab, tab === item.id && styles.tabSelected]}
          >
            <Text style={[styles.tabLabel, tab === item.id && styles.tabLabelSelected]}>
              {item.label}
            </Text>
          </Pressable>
        ))}
      </View>

      <ScrollView contentContainerStyle={styles.content} onScroll={onContentScroll} scrollEventThrottle={100}>
        {tab === 'details' ? (
          <View style={styles.sections}>
            <View style={styles.card}>
              <Text style={styles.cardTitle}>عن المطعم</Text>
              <Text style={styles.description}>{restaurant.description}</Text>
            </View>
            {restaurant.ratingBreakdown ? (
              <RatingBreakdownCard breakdown={restaurant.ratingBreakdown} />
            ) : null}
            {restaurant.phone ? (
              <View style={styles.card}>
                <View style={styles.phoneRow}>
                  <PhoneIcon color={colors.textMuted} size={16} strokeWidth={1.8} />
                  <Text style={styles.phone}>{restaurant.phone}</Text>
                </View>
              </View>
            ) : null}
          </View>
        ) : (
          // Read-only list (Phase 4). No write-review action in MVP (ADR-004).
          <RestaurantReviewsList query={reviewsQuery} />
        )}
      </ScrollView>
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
  cardTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 8,
    textAlign: textAlignStart,
  },
  content: {
    paddingBottom: 32,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingTop: 16,
  },
  description: {
    color: colors.textDescription,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.relaxedSm,
    textAlign: textAlignStart,
  },
  directions: {
    backgroundColor: colors.primary,
    borderRadius: restaurantLayout.controlRadius,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  directionsLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  errorHeader: {
    alignItems: 'flex-start',
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  hero: {
    backgroundColor: colors.border,
    height: restaurantLayout.heroHeight,
    overflow: 'hidden',
  },
  heroActions: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    left: 0,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
    position: 'absolute',
    right: 0,
    top: 0,
  },
  heroButton: {
    alignItems: 'center',
    backgroundColor: colors.heroButton,
    borderRadius: restaurantLayout.heroButtonSize / 2,
    height: restaurantLayout.heroButtonSize,
    justifyContent: 'center',
    width: restaurantLayout.heroButtonSize,
  },
  heroGradient: {
    bottom: 0,
    left: 0,
    position: 'absolute',
    right: 0,
    top: 0,
    experimental_backgroundImage: `linear-gradient(to top, ${colors.heroGradient} 0%, transparent 60%)`,
  },
  heroName: {
    bottom: 16,
    color: '#FFFFFF',
    fontFamily: fonts.arabicBold,
    fontSize: 24,
    left: 16,
    lineHeight: lineHeights.xl2,
    position: 'absolute',
    right: 16,
    textAlign: textAlignStart,
  },
  infoBar: {
    backgroundColor: colors.surface,
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    gap: 8,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingVertical: 16,
  },
  infoRow: {
    alignItems: 'flex-start',
    flexDirection: 'row',
    gap: 16,
    justifyContent: 'space-between',
  },
  infoText: {
    flex: 1,
  },
  meta: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  metaRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 6,
  },
  metaSeparator: {
    color: colors.border,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  phone: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  phoneRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
  },
  price: {
    color: colors.textBody,
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  ratingRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  reviewCount: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  sections: {
    gap: 16,
  },
  skeletonLine: {
    borderRadius: 6,
    height: 10,
    width: '45%',
  },
  skeletonLineWide: {
    borderRadius: 6,
    height: 14,
    width: '60%',
  },
  // Figma hours pill: `text-xs font-medium px-2 py-0.5 rounded-full`, mt-1.
  statusPill: {
    borderRadius: restaurantLayout.chipRadius,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    overflow: 'hidden',
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  statusPillClosed: {
    backgroundColor: colors.closedStatusBackground,
    color: colors.textPlaceholder,
  },
  statusPillOpen: {
    backgroundColor: colors.openStatusBackground,
    color: colors.primary,
  },
  statusRow: {
    alignItems: 'flex-start',
    marginTop: 4,
  },
  tab: {
    alignItems: 'center',
    borderRadius: restaurantLayout.controlRadius,
    flex: 1,
    paddingVertical: 8,
  },
  tabLabel: {
    color: colors.textMuted,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  tabLabelSelected: {
    color: colors.primaryForeground,
  },
  tabSelected: {
    backgroundColor: colors.primary,
  },
  tabs: {
    backgroundColor: colors.background,
    flexDirection: 'row',
    gap: 4,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingTop: 12,
  },
});
