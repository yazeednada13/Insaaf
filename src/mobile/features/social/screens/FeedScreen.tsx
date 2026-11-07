import { router } from 'expo-router';
import { useState } from 'react';
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
import { useFeedQuery } from '@/features/social/hooks/useFeedQuery';

// Figma ActivityScreen.tsx "following" tab. The notifications chip is out of scope (decision D-9).
export function FeedScreen() {
  const insets = useSafeAreaInsets();
  const feedQuery = useFeedQuery();
  const [refreshing, setRefreshing] = useState(false);

  const reviews = feedQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const refresh = async () => {
    setRefreshing(true);
    await feedQuery.refetch();
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
        <View style={styles.chip}>
          <Text style={styles.chipLabel}>المتابعة</Text>
        </View>
      </View>

      <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        ListFooterComponent={
          feedQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} style={styles.footer} /> : null
        }
        ListHeaderComponent={
          reviews.length > 0 ? <Text style={styles.hint}>تقييمات الأشخاص الذين تتابعهم</Text> : null
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
      />
    </View>
  );
}

const styles = StyleSheet.create({
  chip: {
    alignSelf: 'flex-start',
    backgroundColor: colors.primary,
    borderColor: colors.border,
    borderRadius: 999,
    borderWidth: 1.5,
    paddingHorizontal: 20,
    paddingVertical: 8,
  },
  chipLabel: {
    color: colors.primaryForeground,
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
