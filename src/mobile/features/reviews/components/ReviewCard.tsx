import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { openReview, openUser, useCurrentStack } from '@/features/navigation/stackRoutes';
import { FadeInView } from '@/features/restaurants/components/FadeInView';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { ReviewPhotoStrip } from '@/features/reviews/components/ReviewPhotoStrip';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import { CommentIcon, HeartIcon } from '@/features/social/components/ReviewActionIcons';
import { useReviewLike } from '@/features/social/hooks/useReviewLike';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { formatLocation } from '@/types/restaurant';
import type { ReviewListItemDto } from '@/types/review';
import type { FeedItemDto } from '@/types/social';
import { formatRelativeTime } from '@/utils/relativeTime';

// Figma ReviewCard.tsx: text longer than 120 characters is truncated behind "اقرأ المزيد".
const PREVIEW_LENGTH = 120;

interface ReviewCardProps {
  review: ReviewListItemDto & Partial<Pick<FeedItemDto, 'restaurant'>>;
}

/**
 * Figma ReviewCard.tsx: reviewer, verified badge, rating, time, text, like and comment actions. The
 * restaurant line shows only when the review carries a restaurant (the feed); it's redundant on the
 * restaurant's own page. Photos follow Figma (up to 3, Phase 9A); official reply and save are not rendered.
 */
export function ReviewCard({ review }: ReviewCardProps) {
  const [expanded, setExpanded] = useState(false);
  const likeMutation = useReviewLike();
  const reviewerName = review.reviewer.fullName ?? review.reviewer.userName ?? '';
  const isLong = review.text.length > PREVIEW_LENGTH;
  const { restaurant } = review;
  const stack = useCurrentStack();
  const openDetail = () => openReview(stack, review.id);
  const openReviewer = () => openUser(stack, review.reviewer.id);

  return (
    <FadeInView>
      {/* Founder decision: the whole card opens Review Detail; the inner buttons keep their own action. */}
      <Pressable accessible={false} onPress={openDetail} style={styles.card}>
        <View style={styles.header}>
          <Pressable disabled={!review.reviewer.hasPublicProfile} onPress={openReviewer}>
            <UserAvatar name={reviewerName} size={44} />
          </Pressable>
          <View style={styles.identity}>
            <View style={styles.nameRow}>
              <Pressable disabled={!review.reviewer.hasPublicProfile} onPress={openReviewer}>
                <Text style={styles.name}>{reviewerName}</Text>
              </Pressable>
              {review.isVerified ? <VerifiedBadge /> : null}
            </View>
            {restaurant ? (
              <Pressable
                accessibilityRole="link"
                hitSlop={4}
                onPress={() =>
                  router.navigate({ pathname: '/restaurants/[id]', params: { id: restaurant.id } })
                }
              >
                <Text style={styles.restaurant}>
                  {restaurant.name} · {formatLocation(restaurant.city, restaurant.address)}
                </Text>
              </Pressable>
            ) : null}
          </View>
          <View style={styles.meta}>
            <StarRating rating={review.rating} />
            <Text style={styles.time}>{formatRelativeTime(review.createdAt)}</Text>
          </View>
        </View>

        <Text style={styles.text}>
          {isLong && !expanded ? `${review.text.slice(0, PREVIEW_LENGTH)}...` : review.text}
        </Text>
        {isLong ? (
          <Pressable accessibilityRole="button" hitSlop={8} onPress={() => setExpanded((value) => !value)}>
            <Text style={styles.toggle}>{expanded ? 'عرض أقل' : 'اقرأ المزيد'}</Text>
          </Pressable>
        ) : null}

        <ReviewPhotoStrip photos={review.photos} variant="card" />

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
            <HeartIcon filled={review.isLikedByMe} size={16} />
            <Text style={[styles.count, review.isLikedByMe && styles.countLiked]}>{review.likeCount}</Text>
          </Pressable>
          <Pressable
            accessibilityLabel="التعليقات"
            accessibilityRole="button"
            hitSlop={8}
            onPress={openDetail}
            style={styles.action}
          >
            <CommentIcon size={16} />
            <Text style={styles.count}>{review.commentCount}</Text>
          </Pressable>
        </View>
      </Pressable>
    </FadeInView>
  );
}

const styles = StyleSheet.create({
  action: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 6,
  },
  actions: {
    alignItems: 'center',
    borderTopColor: colors.divider,
    borderTopWidth: 1,
    flexDirection: 'row',
    gap: 16,
    marginTop: 12,
    paddingTop: 8,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    boxShadow: '0px 1px 8px rgba(0, 0, 0, 0.04)',
    padding: 16,
  },
  count: {
    color: colors.textMuted,
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  countLiked: {
    color: colors.accent,
  },
  header: {
    alignItems: 'flex-start',
    flexDirection: 'row',
    gap: 12,
    marginBottom: 12,
  },
  identity: {
    flex: 1,
    minWidth: 0,
  },
  meta: {
    alignItems: 'flex-end',
    gap: 4,
  },
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  nameRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  restaurant: {
    color: colors.accent,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 2,
    textAlign: textAlignStart,
  },
  text: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.relaxedSm,
    textAlign: textAlignStart,
  },
  time: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 10,
    lineHeight: 15,
  },
  toggle: {
    color: colors.primary,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 4,
    textAlign: textAlignStart,
  },
});
