import { router, useLocalSearchParams } from 'expo-router';
import { useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  KeyboardAvoidingView,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { openUser, useCurrentStack } from '@/features/navigation/stackRoutes';
import { ChevronLeftIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListErrorState,
  SkeletonBlock,
} from '@/features/restaurants/components/RestaurantListStates';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import { CommentInputBar } from '@/features/social/components/CommentInputBar';
import { FollowButton } from '@/features/social/components/FollowButton';
import { CommentItem } from '@/features/social/components/CommentItem';
import { CommentIcon, HeartIcon } from '@/features/social/components/ReviewActionIcons';
import {
  useCreateCommentMutation,
  useDeleteCommentMutation,
  useReviewCommentsQuery,
} from '@/features/social/hooks/useReviewComments';
import { useReviewDetailQuery } from '@/features/social/hooks/useReviewDetailQuery';
import { useReviewLike } from '@/features/social/hooks/useReviewLike';
import type { ReviewDetailDto } from '@/types/review';
import { formatLocation } from '@/types/restaurant';
import type { ReviewCommentDto } from '@/types/social';
import { formatRelativeTime } from '@/utils/relativeTime';
import { RefreshFailedNotice } from '@/features/reviews/components/RefreshFailedNotice';
import { ReviewPhotoStrip } from '@/features/reviews/components/ReviewPhotoStrip';
import { useRefreshOnFocus } from '@/features/reviews/hooks/useRefreshOnFocus';

const STARS = [1, 2, 3, 4, 5] as const;

function goBack() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/restaurants');
  }
}

function DetailedRating({ label, value }: { label: string; value: number }) {
  return (
    <View style={styles.detailRow}>
      <Text style={styles.detailLabel}>{label}</Text>
      <View style={styles.detailStars}>
        {STARS.map((star) => (
          <Text key={star} style={[styles.detailStar, { color: star <= value ? colors.accent : colors.border }]}>
            ★
          </Text>
        ))}
      </View>
    </View>
  );
}

function ReviewContent({ review }: { review: ReviewDetailDto }) {
  const { user } = useAuth();
  const stack = useCurrentStack();
  const likeMutation = useReviewLike();
  const openReviewer = () => openUser(stack, review.reviewer.id);
  const reviewerName = review.reviewer.fullName ?? review.reviewer.userName ?? '';

  return (
    <View>
      <View style={styles.reviewer}>
        <Pressable disabled={!review.reviewer.hasPublicProfile} onPress={openReviewer}>
          <UserAvatar name={reviewerName} size={52} />
        </Pressable>
        <View style={styles.reviewerText}>
          <View style={styles.nameRow}>
            <Pressable disabled={!review.reviewer.hasPublicProfile} onPress={openReviewer}>
              <Text style={styles.reviewerName}>{reviewerName}</Text>
            </Pressable>
            {review.isVerified ? <VerifiedBadge /> : null}
          </View>
          <Pressable
            accessibilityRole="link"
            onPress={() =>
              router.navigate({ pathname: '/restaurants/[id]', params: { id: review.restaurant.id } })
            }
          >
            <Text style={styles.restaurantLine}>
              {review.restaurant.name} · {formatLocation(review.restaurant.city, review.restaurant.address)}
            </Text>
          </Pressable>
        </View>
        <View style={styles.reviewerSide}>
          <Text style={styles.reviewerTime}>{formatRelativeTime(review.createdAt)}</Text>
          {user?.id === review.reviewer.id || !review.reviewer.hasPublicProfile ? null : (
            <FollowButton isFollowing={review.isReviewerFollowedByMe} userId={review.reviewer.id} />
          )}
        </View>
      </View>

      <View style={styles.ratingsCard}>
        <View style={styles.overallRow}>
          <Text style={styles.overallLabel}>التقييم الكلي</Text>
          <StarRating rating={review.rating} size="lg" />
        </View>
        <View style={styles.ratingsDivider} />
        <DetailedRating label="الطعام" value={review.foodRating} />
        <DetailedRating label="الخدمة" value={review.serviceRating} />
        <DetailedRating label="القيمة مقابل السعر" value={review.valueRating} />
        <DetailedRating label="الأجواء" value={review.atmosphereRating} />
      </View>

      {review.text ? <Text style={styles.reviewText}>{review.text}</Text> : null}

      <ReviewPhotoStrip photos={review.photos} variant="detail" />

      <View style={styles.actions}>
        <Pressable
          accessibilityLabel={review.isLikedByMe ? 'إلغاء الإعجاب' : 'إعجاب'}
          accessibilityRole="button"
          accessibilityState={{ selected: review.isLikedByMe }}
          disabled={likeMutation.isPending}
          hitSlop={8}
          onPress={() => likeMutation.mutate({ reviewId: review.id, like: !review.isLikedByMe })}
          style={styles.action}
        >
          <HeartIcon filled={review.isLikedByMe} size={18} />
          <Text style={[styles.count, review.isLikedByMe && styles.countLiked]}>{review.likeCount}</Text>
        </Pressable>
        <View style={styles.action}>
          <CommentIcon size={18} />
          <Text style={styles.count}>{review.commentCount}</Text>
        </View>
      </View>

      <Text style={styles.commentsTitle}>التعليقات</Text>
    </View>
  );
}

function DetailSkeleton() {
  return (
    <View style={styles.skeleton}>
      <SkeletonBlock style={styles.skeletonAvatar} />
      <SkeletonBlock style={styles.skeletonCard} />
      <SkeletonBlock style={styles.skeletonLine} />
    </View>
  );
}

// Figma ReviewDetailScreen.tsx. Not built: the `⋮` menu, save, photos and restaurant reply. Reviewer
// profile links arrive with profiles (Phase 8).
export function ReviewDetailScreen() {
  const insets = useSafeAreaInsets();
  const { reviewId } = useLocalSearchParams<{ reviewId: string }>();
  const { user } = useAuth();
  const listRef = useRef<FlatList<ReviewCommentDto>>(null);
  const [commentError, setCommentError] = useState<string | null>(null);

  const reviewQuery = useReviewDetailQuery(reviewId);
  useRefreshOnFocus(reviewQuery);
  const commentsQuery = useReviewCommentsQuery(reviewId);
  const createMutation = useCreateCommentMutation(reviewId ?? '');
  const deleteMutation = useDeleteCommentMutation(reviewId ?? '');

  const comments = commentsQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const submitComment = async (text: string) => {
    setCommentError(null);
    try {
      await createMutation.mutateAsync(text);
      // Comments are oldest first: the new one is at the end of the list.
      listRef.current?.scrollToEnd({ animated: true });
      return true;
    } catch (error) {
      setCommentError((error as Error).message);
      return false;
    }
  };

  const confirmDelete = (comment: ReviewCommentDto) => {
    Alert.alert('حذف التعليق', 'هل تريد حذف هذا التعليق؟ لا يمكن التراجع عن ذلك.', [
      { text: 'إلغاء', style: 'cancel' },
      {
        text: 'حذف',
        style: 'destructive',
        onPress: () =>
          deleteMutation.mutate(comment.id, {
            onError: (error) => Alert.alert('تعذر حذف التعليق', error.message),
          }),
      },
    ]);
  };

  const renderEmptyComments = () => {
    if (commentsQuery.isPending) {
      return <ActivityIndicator color={colors.primary} style={styles.commentsLoading} />;
    }

    if (commentsQuery.isError) {
      return (
        <RestaurantListErrorState
          message={commentsQuery.error.message}
          onRetry={() => void commentsQuery.refetch()}
        />
      );
    }

    return <Text style={styles.noComments}>لا توجد تعليقات بعد</Text>;
  };

  return (
    // Android runs edge-to-edge, so the window doesn't shrink for the keyboard (see VerifiedReviewForm).
    <KeyboardAvoidingView behavior="padding" style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={goBack} style={styles.backButton}>
          <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
        </Pressable>
        <Text style={styles.headerTitle}>التقييم</Text>
      </View>

      {reviewQuery.isPending ? <DetailSkeleton /> : null}

      {reviewQuery.isError ? (
        <RestaurantListErrorState
          message={reviewQuery.error.message}
          onRetry={reviewQuery.error.code === 'not_found' ? undefined : () => void reviewQuery.refetch()}
        />
      ) : null}

      {reviewQuery.isSuccess ? (
        <>
          <FlatList
            ListEmptyComponent={renderEmptyComments()}
            ListFooterComponent={
              commentsQuery.isFetchingNextPage ? <ActivityIndicator color={colors.primary} /> : null
            }
            ListHeaderComponent={
              <>
                <RefreshFailedNotice visible={reviewQuery.isRefetchError} />
                <ReviewContent review={reviewQuery.data} />
              </>
            }
            contentContainerStyle={styles.content}
            data={comments}
            keyExtractor={(comment) => comment.id}
            keyboardShouldPersistTaps="handled"
            onEndReached={() => {
              if (commentsQuery.hasNextPage && !commentsQuery.isFetchingNextPage) {
                void commentsQuery.fetchNextPage();
              }
            }}
            ref={listRef}
            renderItem={({ item }) => (
              <CommentItem
                comment={item}
                deleting={deleteMutation.isPending && deleteMutation.variables === item.id}
                onDelete={confirmDelete}
              />
            )}
          />
          <CommentInputBar
            error={commentError}
            onSubmit={submitComment}
            submitting={createMutation.isPending}
            userLabel={user?.email ?? ''}
          />
        </>
      ) : null}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  action: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 8,
  },
  actions: {
    alignItems: 'center',
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    borderTopColor: colors.divider,
    borderTopWidth: 1,
    flexDirection: 'row',
    gap: 24,
    paddingVertical: 12,
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
  commentsLoading: {
    marginVertical: 24,
  },
  commentsTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 12,
    marginTop: 16,
    textAlign: textAlignStart,
  },
  content: {
    paddingHorizontal: 20,
    paddingVertical: 16,
  },
  count: {
    color: colors.textMuted,
    fontFamily: fonts.numericRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  countLiked: {
    color: colors.accent,
  },
  detailLabel: {
    color: colors.textDescription,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  detailRow: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 6,
  },
  detailStar: {
    fontSize: 14,
    lineHeight: 20,
  },
  detailStars: {
    direction: 'ltr',
    flexDirection: 'row',
    gap: 4,
  },
  header: {
    alignItems: 'center',
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    flexDirection: 'row',
    gap: 12,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  headerTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  nameRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  noComments: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginVertical: 24,
    textAlign: 'center',
  },
  overallLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  overallRow: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  ratingsCard: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    marginBottom: 16,
    padding: 16,
  },
  ratingsDivider: {
    backgroundColor: '#F0F0EE',
    height: 1,
    marginBottom: 12,
  },
  restaurantLine: {
    color: colors.accent,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  reviewText: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.relaxedSm,
    marginBottom: 16,
    textAlign: textAlignStart,
  },
  reviewer: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    marginBottom: 16,
  },
  reviewerName: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
    textAlign: textAlignStart,
  },
  reviewerSide: {
    alignItems: 'flex-end',
    gap: 6,
  },
  reviewerText: {
    flex: 1,
    minWidth: 0,
  },
  reviewerTime: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  skeleton: {
    gap: 16,
    paddingHorizontal: 20,
    paddingVertical: 16,
  },
  skeletonAvatar: {
    borderRadius: 26,
    height: 52,
    width: 52,
  },
  skeletonCard: {
    borderRadius: 16,
    height: 180,
  },
  skeletonLine: {
    borderRadius: 8,
    height: 16,
  },
});
