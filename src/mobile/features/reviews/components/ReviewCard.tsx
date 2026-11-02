import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { FadeInView } from '@/features/restaurants/components/FadeInView';
import { StarRating } from '@/features/restaurants/components/StarRating';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import type { ReviewListItemDto } from '@/types/review';
import { formatRelativeTime } from '@/utils/relativeTime';

// Figma ReviewCard.tsx: text longer than 120 characters is truncated behind "اقرأ المزيد".
const PREVIEW_LENGTH = 120;

/**
 * Figma ReviewCard.tsx, Phase 4 read-only subset (decision D5): reviewer, verified badge, rating,
 * time and text. The restaurant line (redundant on its own page), photos, official reply and the
 * like/comment/save row belong to later phases and are not rendered.
 */
export function ReviewCard({ review }: { review: ReviewListItemDto }) {
  const [expanded, setExpanded] = useState(false);
  const reviewerName = review.reviewer.fullName ?? review.reviewer.userName ?? '';
  const isLong = review.text.length > PREVIEW_LENGTH;

  return (
    <FadeInView>
      <View style={styles.card}>
        <View style={styles.header}>
          <UserAvatar name={reviewerName} size={44} />
          <View style={styles.identity}>
            <View style={styles.nameRow}>
              <Text style={styles.name}>{reviewerName}</Text>
              {review.isVerified ? <VerifiedBadge /> : null}
            </View>
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
      </View>
    </FadeInView>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    boxShadow: '0px 1px 8px rgba(0, 0, 0, 0.04)',
    padding: 16,
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
