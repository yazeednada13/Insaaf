import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import {
  RestaurantListEmptyState,
  RestaurantListErrorState,
} from '@/features/restaurants/components/RestaurantListStates';
import { ReviewCard } from '@/features/reviews/components/ReviewCard';
import { ReviewCardSkeleton } from '@/features/reviews/components/ReviewCardSkeleton';
import { NotificationsList } from '@/features/social/components/NotificationsList';
import { useFeedQuery } from '@/features/social/hooks/useFeedQuery';
import { useUnreadCountQuery } from '@/features/social/hooks/useNotifications';
import { RefreshFailedNotice } from '@/features/reviews/components/RefreshFailedNotice';
import { useRefreshOnFocus } from '@/features/reviews/hooks/useRefreshOnFocus';

type ActivityTab = 'following' | 'notifications';

// Figma ActivityScreen.tsx: "المتابعة" (the feed) and "الإشعارات" chips.
export function FeedScreen() {
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<ActivityTab>('following');
  const feedQuery = useFeedQuery();
  useRefreshOnFocus(feedQuery);
  const countQuery = useUnreadCountQuery();
  const [refreshing, setRefreshing] = useState(false);
  const unread = countQuery.data?.count ?? 0;

  // The unread count refreshes whenever النشاط opens (decision D-10).
  const { refetch: refetchCount } = countQuery;
  useFocusEffect(
    useCallback(() => {
      void refetchCount();
    }, [refetchCount]),
  );

  const reviews = feedQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const refresh = async () => {
    setRefreshing(true);
    await Promise.all([feedQuery.refetch(), countQuery.refetch()]);
    setRefreshing(false);
  };

  const renderEmpty = () => {
    if (feedQuery.isPending) {
      return (
        <View style={styles.skeletons}>
          <ReviewCardSkeleton />
          <ReviewCardSkeleton />
        </View>
      );
    }

    if (feedQuery.isError) {
      return (
        <RestaurantListErrorState
          message={feedQuery.error.message}
          onRetry={() => void feedQuery.refetch()}
        />
      );
    }

    // Decision D-5: the copy and the button have no Figma design.
    return (
      <View>
        <RestaurantListEmptyState
          hint="تابع أشخاصًا من صفحة التقييم لتظهر تقييماتهم هنا"
          title="لا توجد تقييمات بعد"
        />
        <Pressable
          accessibilityRole="button"
          onPress={() => router.navigate('/restaurants')}
          style={styles.exploreButton}
        >
          <Text style={styles.exploreLabel}>استكشف المطاعم</Text>
        </Pressable>
      </View>
    );
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Text style={styles.title}>النشاط</Text>
        <View style={styles.chips}>
          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected: tab === 'following' }}
            onPress={() => setTab('following')}
            style={[styles.chip, tab === 'following' && styles.chipSelected]}
          >
            <Text style={[styles.chipLabel, tab === 'following' && styles.chipLabelSelected]}>المتابعة</Text>
          </Pressable>
          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected: tab === 'notifications' }}
            onPress={() => setTab('notifications')}
            style={[styles.chip, tab === 'notifications' && styles.chipSelected]}
          >
            <Text style={[styles.chipLabel, tab === 'notifications' && styles.chipLabelSelected]}>الإشعارات</Text>
            {unread > 0 ? (
              <View style={styles.count}>
                <Text style={styles.countLabel}>{unread > 99 ? '99+' : unread}</Text>
              </View>
            ) : null}
          </Pressable>
        </View>
      </View>

      {tab === 'notifications' ? <NotificationsList /> : null}

      {tab === 'following' ? <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        ListFooterComponent={
          feedQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} style={styles.footer} /> : null
        }
        ListHeaderComponent={
          <>
            <RefreshFailedNotice visible={feedQuery.isRefetchError} />
            {reviews.length > 0 ? <Text style={styles.hint}>تقييمات الأشخاص الذين تتابعهم</Text> : null}
          </>
        }
        contentContainerStyle={styles.content}
        data={reviews}
        keyExtractor={(review) => review.id}
        onEndReached={() => {
          if (feedQuery.hasNextPage && !feedQuery.isFetchingNextPage) {
            void feedQuery.fetchNextPage();
          }
        }}
        refreshControl={
          <RefreshControl colors={[colors.primary]} onRefresh={() => void refresh()} refreshing={refreshing} />
        }
        renderItem={({ item }) => <ReviewCard review={item} />}
      /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  chips: {
    flexDirection: 'row',
    gap: 8,
  },
  chipLabelSelected: {
    color: colors.primaryForeground,
  },
  chipSelected: {
    backgroundColor: colors.primary,
  },
  count: {
    alignItems: 'center',
    backgroundColor: colors.accent,
    borderColor: colors.background,
    borderRadius: 10,
    borderWidth: 1.5,
    end: -6,
    height: 20,
    justifyContent: 'center',
    minWidth: 20,
    paddingHorizontal: 4,
    position: 'absolute',
    top: -6,
  },
  countLabel: {
    color: '#FFFFFF',
    fontFamily: fonts.numericMedium,
    fontSize: 10,
    lineHeight: 14,
  },
  chip: {
    alignSelf: 'flex-start',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 999,
    borderWidth: 1.5,
    paddingHorizontal: 20,
    paddingVertical: 8,
  },
  chipLabel: {
    color: colors.textBody,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  content: {
    flexGrow: 1,
    paddingBottom: 24,
    paddingHorizontal: 16,
  },
  exploreButton: {
    alignSelf: 'center',
    backgroundColor: colors.primary,
    borderRadius: 12,
    marginTop: -40,
    paddingHorizontal: 20,
    paddingVertical: 10,
  },
  exploreLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  footer: {
    marginTop: 16,
  },
  header: {
    paddingBottom: 12,
    paddingHorizontal: 20,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  hint: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginBottom: 16,
    textAlign: textAlignStart,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  separator: {
    height: 12,
  },
  skeletons: {
    gap: 12,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 24,
    lineHeight: lineHeights.xl2,
    marginBottom: 16,
    textAlign: textAlignStart,
  },
});
