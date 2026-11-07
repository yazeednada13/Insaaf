import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';
import { openReview, openUser } from '@/features/navigation/stackRoutes';
import {
  RestaurantListErrorState,
  SkeletonBlock,
} from '@/features/restaurants/components/RestaurantListStates';
import { NotificationItem } from '@/features/social/components/NotificationItem';
import {
  useMarkAllReadMutation,
  useNotificationsQuery,
  useUnreadCountQuery,
} from '@/features/social/hooks/useNotifications';
import type { NotificationDto } from '@/types/social';

function open(notification: NotificationDto) {
  if (notification.type === 'follow') {
    if (notification.actor.hasPublicProfile) {
      openUser('activity', notification.actor.id);
    }
  } else if (notification.reviewId) {
    openReview('activity', notification.reviewId);
  }
}

// The "الإشعارات" chip of Figma ActivityScreen.tsx.
export function NotificationsList() {
  const listQuery = useNotificationsQuery(true);
  const countQuery = useUnreadCountQuery();
  const markAllRead = useMarkAllReadMutation();
  const [refreshing, setRefreshing] = useState(false);
  const markedRead = useRef(false);

  const notifications = listQuery.data?.pages.flatMap((page) => page.items) ?? [];

  // Opening the chip marks everything read, once the list has loaded (decision D-8).
  const { isSuccess } = listQuery;
  const { mutate } = markAllRead;
  useEffect(() => {
    if (isSuccess && !markedRead.current) {
      markedRead.current = true;
      mutate();
    }
  }, [isSuccess, mutate]);

  const refresh = async () => {
    setRefreshing(true);
    await Promise.all([listQuery.refetch(), countQuery.refetch()]);
    mutate();
    setRefreshing(false);
  };

  const renderEmpty = () => {
    if (listQuery.isPending) {
      return (
        <View style={styles.skeletons}>
          <SkeletonBlock style={styles.skeleton} />
          <SkeletonBlock style={styles.skeleton} />
        </View>
      );
    }

    if (listQuery.isError) {
      return (
        <RestaurantListErrorState message={listQuery.error.message} onRetry={() => void listQuery.refetch()} />
      );
    }

    return <Text style={styles.empty}>لا توجد إشعارات بعد</Text>;
  };

  return (
    <FlatList
      ItemSeparatorComponent={() => <View style={styles.separator} />}
      ListEmptyComponent={renderEmpty()}
      ListFooterComponent={
        listQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} style={styles.footer} /> : null
      }
      contentContainerStyle={styles.content}
      data={notifications}
      keyExtractor={(notification) => notification.id}
      onEndReached={() => {
        if (listQuery.hasNextPage && !listQuery.isFetchingNextPage) {
          void listQuery.fetchNextPage();
        }
      }}
      refreshControl={
        <RefreshControl colors={[colors.primary]} onRefresh={() => void refresh()} refreshing={refreshing} />
      }
      renderItem={({ item }) => <NotificationItem notification={item} onPress={open} />}
    />
  );
}

const styles = StyleSheet.create({
  content: {
    flexGrow: 1,
    paddingBottom: 24,
    paddingHorizontal: 16,
  },
  empty: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginVertical: 48,
    textAlign: 'center',
  },
  footer: {
    marginTop: 16,
  },
  separator: {
    height: 8,
  },
  skeleton: {
    borderRadius: 16,
    height: 76,
  },
  skeletons: {
    gap: 8,
  },
});
