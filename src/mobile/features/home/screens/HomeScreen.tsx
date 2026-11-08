import { router, type Href } from 'expo-router';
import { useState, type ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import {
  useSuggestedRestaurantsQuery,
  useTrendingRestaurantsQuery,
} from '@/features/home/hooks/useHomeQueries';
import { useMyProfileQuery } from '@/features/profile/hooks/useUserQueries';
import { RestaurantCard } from '@/features/restaurants/components/RestaurantCard';
import { SearchIcon } from '@/features/restaurants/components/RestaurantIcons';
import { RestaurantTile } from '@/features/restaurants/components/RestaurantTile';
import { useRestaurantDistance } from '@/features/restaurants/hooks/useRestaurantDistance';
import { ReviewCard } from '@/features/reviews/components/ReviewCard';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { useFeedQuery } from '@/features/social/hooks/useFeedQuery';
import type { RestaurantListItemDto } from '@/types/restaurant';
import { RefreshFailedNotice } from '@/features/reviews/components/RefreshFailedNotice';
import { useRefreshOnFocus } from '@/features/reviews/hooks/useRefreshOnFocus';

/** Decision D-2: the following section shows the first feed items. */
const FOLLOWING_PREVIEW = 2;

// Figma HomeScreen.tsx greeting.
function greeting(date: Date): string {
  const hour = date.getHours();
  if (hour < 12) {
    return 'صباح الخير';
  }

  return hour < 17 ? 'مساء الخير' : 'مساء النور';
}

function openRestaurant(restaurant: RestaurantListItemDto) {
  router.navigate({ pathname: '/restaurants/[id]', params: { id: restaurant.id } });
}

function Section({ title, onSeeAll, children }: { title: string; onSeeAll?: () => void; children: ReactNode }) {
  return (
    <View style={styles.section}>
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>{title}</Text>
        {onSeeAll ? (
          <Pressable accessibilityRole="button" hitSlop={8} onPress={onSeeAll}>
            <Text style={styles.seeAll}>عرض الكل</Text>
          </Pressable>
        ) : null}
      </View>
      {children}
    </View>
  );
}

function SectionState({ loading, error, onRetry, empty }: {
  loading: boolean;
  error?: string;
  onRetry: () => void;
  empty?: string;
}) {
  if (loading) {
    return <ActivityIndicator color={colors.primary} style={styles.sectionLoading} />;
  }

  if (error) {
    return (
      <Pressable accessibilityRole="button" onPress={onRetry} style={styles.sectionMessage}>
        <Text style={styles.sectionMessageText}>{error}</Text>
        <Text style={styles.retry}>إعادة المحاولة</Text>
      </Pressable>
    );
  }

  return empty ? <Text style={[styles.sectionMessageText, styles.sectionMessage]}>{empty}</Text> : null;
}

// Figma HomeScreen.tsx. Not built: "تقييمات حديثة" (D-3) and the verified-visit sheet (D-9).
export function HomeScreen() {
  const insets = useSafeAreaInsets();
  const formatDistance = useRestaurantDistance();
  const profileQuery = useMyProfileQuery();
  const feedQuery = useFeedQuery();
  useRefreshOnFocus(feedQuery);
  const suggestedQuery = useSuggestedRestaurantsQuery();
  const trendingQuery = useTrendingRestaurantsQuery();
  const [refreshing, setRefreshing] = useState(false);

  const following = (feedQuery.data?.pages.flatMap((page) => page.items) ?? []).slice(0, FOLLOWING_PREVIEW);
  const suggested = suggestedQuery.data?.items ?? [];
  const trending = trendingQuery.data?.items ?? [];
  const name = profileQuery.data?.fullName ?? profileQuery.data?.userName ?? '';

  const refresh = async () => {
    setRefreshing(true);
    await Promise.all([feedQuery.refetch(), suggestedQuery.refetch(), trendingQuery.refetch()]);
    setRefreshing(false);
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <View style={styles.greetingRow}>
          <View>
            <Text style={styles.brand}>إنصاف</Text>
            <Text style={styles.greeting}>{greeting(new Date())} 👋</Text>
          </View>
          <Pressable accessibilityLabel="ملفي" onPress={() => router.navigate('/profile' as Href)}>
            <UserAvatar name={name} size={40} />
          </Pressable>
        </View>

        <Pressable
          accessibilityRole="search"
          onPress={() => router.push('/restaurants/search')}
          style={styles.searchBar}
        >
          <SearchIcon color={colors.textPlaceholder} size={16} strokeWidth={2} />
          <Text style={styles.searchPlaceholder}>ابحث عن مطعم أو شخص...</Text>
        </Pressable>
      </View>

      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl colors={[colors.primary]} onRefresh={() => void refresh()} refreshing={refreshing} />
        }
      >
        <RefreshFailedNotice visible={feedQuery.isRefetchError} />
        <Section onSeeAll={() => router.navigate('/activity' as Href)} title="من الأشخاص الذين تتابعهم">
          {following.length > 0 ? (
            <View style={styles.reviews}>
              {following.map((review) => (
                <ReviewCard key={review.id} review={review} />
              ))}
            </View>
          ) : (
            <SectionState
              empty="تابع أشخاصًا لتظهر تقييماتهم هنا"
              error={feedQuery.error?.message}
              loading={feedQuery.isPending}
              onRetry={() => void feedQuery.refetch()}
            />
          )}
        </Section>

        <Section title="مقترح لك">
          {suggested.length > 0 ? (
            <ScrollView contentContainerStyle={styles.tiles} horizontal showsHorizontalScrollIndicator={false}>
              {suggested.map((restaurant) => (
                <RestaurantTile
                  distance={formatDistance(restaurant)}
                  key={restaurant.id}
                  label="موصى به"
                  onPress={() => openRestaurant(restaurant)}
                  restaurant={restaurant}
                />
              ))}
            </ScrollView>
          ) : (
            <SectionState
              empty="لا توجد اقتراحات بعد"
              error={suggestedQuery.error?.message}
              loading={suggestedQuery.isPending}
              onRetry={() => void suggestedQuery.refetch()}
            />
          )}
        </Section>

        <Section onSeeAll={() => router.navigate('/restaurants')} title="رائج بالقرب منك">
          {trending.length > 0 ? (
            <View style={styles.list}>
              {trending.map((restaurant) => (
                <RestaurantCard
                  distance={formatDistance(restaurant)}
                  key={restaurant.id}
                  onPress={() => openRestaurant(restaurant)}
                  restaurant={restaurant}
                />
              ))}
            </View>
          ) : (
            <SectionState
              empty="لا توجد مطاعم بعد"
              error={trendingQuery.error?.message}
              loading={trendingQuery.isPending}
              onRetry={() => void trendingQuery.refetch()}
            />
          )}
        </Section>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  brand: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: textAlignStart,
  },
  content: {
    paddingBottom: 24,
    paddingHorizontal: 16,
  },
  greeting: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 20,
    lineHeight: 28,
    textAlign: textAlignStart,
  },
  greetingRow: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  header: {
    paddingBottom: 12,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  list: {
    gap: 8,
  },
  retry: {
    color: colors.primary,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 4,
  },
  reviews: {
    gap: 12,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  searchBar: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: restaurantLayout.controlRadius,
    borderWidth: restaurantLayout.inputBorderWidth,
    flexDirection: 'row',
    gap: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  searchPlaceholder: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  section: {
    marginBottom: 20,
  },
  sectionHeader: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  sectionLoading: {
    marginVertical: 16,
  },
  sectionMessage: {
    alignItems: 'center',
    paddingVertical: 16,
  },
  sectionMessageText: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 13,
    lineHeight: lineHeights.sm,
    textAlign: 'center',
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  seeAll: {
    color: colors.accent,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  tiles: {
    gap: 12,
    paddingBottom: 4,
    paddingHorizontal: 4,
  },
});
