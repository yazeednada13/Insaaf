import { router, useLocalSearchParams } from 'expo-router';
import { useMemo } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout } from '@/constants/theme';
import { openUser, useCurrentStack } from '@/features/navigation/stackRoutes';
import { UserRow } from '@/features/profile/components/UserRow';
import { useUserListQuery } from '@/features/profile/hooks/useUserQueries';
import { ChevronLeftIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListErrorState,
  SkeletonBlock,
} from '@/features/restaurants/components/RestaurantListStates';
import { FollowButton } from '@/features/social/components/FollowButton';
import type { UserListItemDto } from '@/types/social';

const COPY = {
  followers: { title: 'المتابعون', empty: 'لا يوجد متابعون بعد' },
  following: { title: 'يتابع', empty: 'لا يتابع أحدًا بعد' },
};

function goBack() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/restaurants');
  }
}

// Followers and following have no screen in the Figma Make file; rows reuse its people row (D-9).
function UserListScreen({ kind }: { kind: 'followers' | 'following' }) {
  const insets = useSafeAreaInsets();
  const stack = useCurrentStack();
  const { userId } = useLocalSearchParams<{ userId: string }>();
  const listQuery = useUserListQuery(userId, kind);

  // A follow between two page loads shifts the list, so a row could arrive twice.
  const users = useMemo(() => {
    const seen = new Map<string, UserListItemDto>();
    listQuery.data?.pages.flatMap((page) => page.items).forEach((item) => seen.set(item.id, item));
    return [...seen.values()];
  }, [listQuery.data]);

  const renderEmpty = () => {
    if (listQuery.isPending) {
      return (
        <View style={styles.skeletons}>
          <SkeletonBlock style={styles.skeletonRow} />
          <SkeletonBlock style={styles.skeletonRow} />
          <SkeletonBlock style={styles.skeletonRow} />
        </View>
      );
    }

    if (listQuery.isError) {
      return (
        <RestaurantListErrorState
          message={listQuery.error.message}
          onRetry={() => void listQuery.refetch()}
        />
      );
    }

    return <Text style={styles.empty}>{COPY[kind].empty}</Text>;
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={goBack} style={styles.backButton}>
          <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
        </Pressable>
        <Text style={styles.title}>{COPY[kind].title}</Text>
      </View>

      <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        ListFooterComponent={
          listQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} style={styles.footer} /> : null
        }
        contentContainerStyle={styles.content}
        data={users}
        keyExtractor={(user) => user.id}
        onEndReached={() => {
          if (listQuery.hasNextPage && !listQuery.isFetchingNextPage) {
            void listQuery.fetchNextPage();
          }
        }}
        renderItem={({ item }) => (
          <UserRow
            fullName={item.fullName}
            onPress={() => openUser(stack, item.id)}
            trailing={item.isMe ? null : <FollowButton isFollowing={item.isFollowedByMe} userId={item.id} />}
            userName={item.userName}
          />
        )}
      />
    </View>
  );
}

export function FollowersScreen() {
  return <UserListScreen kind="followers" />;
}

export function FollowingScreen() {
  return <UserListScreen kind="following" />;
}

const styles = StyleSheet.create({
  backButton: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 18,
    borderWidth: 1.5,
    height: 36,
    justifyContent: 'center',
    width: 36,
  },
  content: {
    flexGrow: 1,
    paddingBottom: 24,
    paddingHorizontal: 16,
    paddingTop: 8,
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
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  separator: {
    height: 8,
  },
  skeletonRow: {
    borderRadius: 16,
    height: 72,
  },
  skeletons: {
    gap: 8,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 18,
    lineHeight: 28,
  },
});
