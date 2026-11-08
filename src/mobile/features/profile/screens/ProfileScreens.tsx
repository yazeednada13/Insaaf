import { router, useLocalSearchParams, type Href } from 'expo-router';
import type { UseQueryResult } from '@tanstack/react-query';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Circle, Path } from 'react-native-svg';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { openUserList, useCurrentStack } from '@/features/navigation/stackRoutes';
import {
  useMyProfileQuery,
  useUserProfileQuery,
  useUserReviewsQuery,
} from '@/features/profile/hooks/useUserQueries';
import { ChevronLeftIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListErrorState,
  SkeletonBlock,
} from '@/features/restaurants/components/RestaurantListStates';
import { ReviewCard } from '@/features/reviews/components/ReviewCard';
import { TasteMatchButton, TasteProfileCard } from '@/features/profile/components/TasteViews';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { FollowButton } from '@/features/social/components/FollowButton';
import type { SocialApiError } from '@/services/social/socialApi';
import type { UserProfileDto } from '@/types/social';
import { RefreshFailedNotice } from '@/features/reviews/components/RefreshFailedNotice';
import { useRefreshOnFocus } from '@/features/reviews/hooks/useRefreshOnFocus';

function goBack() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/restaurants');
  }
}

function Stat({ value, label, onPress }: { value: number; label: string; onPress?: () => void }) {
  return (
    <Pressable accessibilityRole={onPress ? 'button' : undefined} disabled={!onPress} onPress={onPress} style={styles.stat}>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </Pressable>
  );
}

function ProfileHeader({ profile }: { profile: UserProfileDto }) {
  const stack = useCurrentStack();
  const name = profile.fullName ?? profile.userName ?? '';

  return (
    <View>
      <View style={styles.card}>
        <View style={styles.identity}>
          <UserAvatar name={name} size={80} />
          <View style={styles.identityText}>
            <Text style={styles.name}>{name}</Text>
            {profile.userName ? <Text style={styles.userName}>@{profile.userName}</Text> : null}
            {profile.bio ? <Text style={styles.bio}>{profile.bio}</Text> : null}
          </View>
        </View>

        <View style={styles.stats}>
          <Stat label="تقييم" value={profile.reviewCount} />
          <Stat
            label="متابع"
            onPress={() => openUserList(stack, profile.id, 'followers')}
            value={profile.followerCount}
          />
          <Stat
            label="يتابع"
            onPress={() => openUserList(stack, profile.id, 'following')}
            value={profile.followingCount}
          />
        </View>

        {profile.isMe ? (
          <Pressable
            accessibilityRole="button"
            onPress={() => router.push('/profile/edit' as Href)}
            style={styles.editButton}
          >
            <Text style={styles.editLabel}>تعديل الملف</Text>
          </Pressable>
        ) : (
          <View style={styles.actions}>
            <TasteMatchButton name={name} userId={profile.id} />
            <FollowButton isFollowing={profile.isFollowedByMe} size="full" userId={profile.id} />
          </View>
        )}
      </View>

      {profile.isMe ? <TasteProfileCard userId={profile.id} /> : null}

      <View style={styles.tab}>
        <Text style={styles.tabLabel}>{profile.isMe ? 'تقييماتي' : 'التقييمات'}</Text>
      </View>
    </View>
  );
}

function ProfileSkeleton() {
  return (
    <View style={styles.skeleton}>
      <SkeletonBlock style={styles.skeletonAvatar} />
      <SkeletonBlock style={styles.skeletonLine} />
      <SkeletonBlock style={styles.skeletonCard} />
    </View>
  );
}

interface ProfileViewProps {
  title?: string;
  showBack: boolean;
  showSettings: boolean;
  query: UseQueryResult<UserProfileDto, SocialApiError>;
}

// Figma ProfileScreen.tsx. Not built: the المحفوظات tab and "مشاركة الملف" (Phase 8 decision D-8).
function ProfileView({ title, showBack, showSettings, query }: ProfileViewProps) {
  const insets = useSafeAreaInsets();
  const profile = query.data;
  const reviewsQuery = useUserReviewsQuery(profile?.id);
  useRefreshOnFocus(reviewsQuery);
  const reviews = reviewsQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const renderEmpty = () => {
    if (reviewsQuery.isPending) {
      return <ActivityIndicator color={colors.primary} style={styles.loading} />;
    }

    if (reviewsQuery.isError) {
      return (
        <RestaurantListErrorState
          message={reviewsQuery.error.message}
          onRetry={() => void reviewsQuery.refetch()}
        />
      );
    }

    return <Text style={styles.empty}>لا توجد تقييمات بعد</Text>;
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        {showBack ? (
          <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={goBack} style={styles.backButton}>
            <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
          </Pressable>
        ) : null}
        <Text numberOfLines={1} style={styles.title}>
          {title ?? profile?.fullName ?? ''}
        </Text>
        {showSettings ? (
          <Pressable
            accessibilityLabel="الإعدادات"
            accessibilityRole="button"
            hitSlop={8}
            onPress={() => router.push('/profile/settings' as Href)}
          >
            <Svg fill="none" height={22} stroke="#888888" strokeWidth={1.8} viewBox="0 0 24 24" width={22}>
              <Circle cx={12} cy={12} r={3} />
              <Path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-2 2 2 2 0 01-2-2v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 01-2-2 2 2 0 012-2h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 010-2.83 2 2 0 012.83 0l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 012-2 2 2 0 012 2v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 012 2 2 2 0 01-2 2h-.09a1.65 1.65 0 00-1.51 1z" />
            </Svg>
          </Pressable>
        ) : null}
      </View>

      {query.isPending ? <ProfileSkeleton /> : null}

      {query.isError ? (
        <RestaurantListErrorState
          message={query.error.code === 'not_found' ? 'هذا الحساب غير متاح.' : query.error.message}
          onRetry={query.error.code === 'not_found' ? undefined : () => void query.refetch()}
        />
      ) : null}

      {profile ? (
        <FlatList
          ItemSeparatorComponent={() => <View style={styles.separator} />}
          ListEmptyComponent={renderEmpty()}
          ListFooterComponent={
            reviewsQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} style={styles.loading} /> : null
          }
          ListHeaderComponent={
            <>
              <ProfileHeader profile={profile} />
              <RefreshFailedNotice visible={reviewsQuery.isRefetchError} />
            </>
          }
          contentContainerStyle={styles.content}
          data={reviews}
          keyExtractor={(review) => review.id}
          onEndReached={() => {
            if (reviewsQuery.hasNextPage && !reviewsQuery.isFetchingNextPage) {
              void reviewsQuery.fetchNextPage();
            }
          }}
          renderItem={({ item }) => <ReviewCard review={item} />}
        />
      ) : null}
    </View>
  );
}

export function MyProfileScreen() {
  const query = useMyProfileQuery();
  return <ProfileView query={query} showBack={false} showSettings title="ملفي الشخصي" />;
}

export function UserProfileScreen() {
  const { userId } = useLocalSearchParams<{ userId: string }>();
  const query = useUserProfileQuery(userId);
  return <ProfileView query={query} showBack showSettings={false} />;
}

const styles = StyleSheet.create({
  actions: {
    flexDirection: 'row',
    gap: 8,
  },
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
  bio: {
    color: colors.textDescription,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.snugSm,
    marginTop: 6,
    textAlign: textAlignStart,
  },
  card: {
    paddingBottom: 16,
  },
  content: {
    flexGrow: 1,
    paddingBottom: 24,
    paddingHorizontal: 20,
  },
  editButton: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 12,
    borderWidth: 1.5,
    paddingVertical: 10,
  },
  editLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  empty: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginVertical: 32,
    textAlign: 'center',
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  identity: {
    alignItems: 'flex-start',
    flexDirection: 'row',
    gap: 16,
    marginBottom: 16,
  },
  identityText: {
    flex: 1,
    minWidth: 0,
  },
  loading: {
    marginVertical: 24,
  },
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 20,
    lineHeight: 28,
    textAlign: textAlignStart,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  separator: {
    height: 12,
  },
  skeleton: {
    gap: 16,
    paddingHorizontal: 20,
  },
  skeletonAvatar: {
    borderRadius: 40,
    height: 80,
    width: 80,
  },
  skeletonCard: {
    borderRadius: 16,
    height: 120,
  },
  skeletonLine: {
    borderRadius: 8,
    height: 16,
  },
  stat: {
    alignItems: 'center',
  },
  statLabel: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  statValue: {
    color: colors.textPrimary,
    fontFamily: fonts.numericBold,
    fontSize: 18,
    lineHeight: 28,
  },
  stats: {
    flexDirection: 'row',
    gap: 16,
    marginBottom: 16,
  },
  tab: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 12,
    marginBottom: 16,
    paddingVertical: 8,
  },
  tabLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  title: {
    color: colors.textPrimary,
    flex: 1,
    fontFamily: fonts.arabicBold,
    fontSize: 18,
    lineHeight: 28,
    textAlign: textAlignStart,
  },
  userName: {
    color: colors.textPlaceholder,
    fontFamily: fonts.numericRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
});
