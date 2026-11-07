import { Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { openUser, useCurrentStack } from '@/features/navigation/stackRoutes';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import type { ReviewCommentDto } from '@/types/social';
import { formatRelativeTime } from '@/utils/relativeTime';

interface CommentItemProps {
  comment: ReviewCommentDto;
  deleting: boolean;
  onDelete: (comment: ReviewCommentDto) => void;
}

// Figma ReviewDetailScreen.tsx comment. "إعجاب" and "رد" are out of scope; "حذف" has no Figma design
// (decision D-7) and shows only on the user's own comments.
export function CommentItem({ comment, deleting, onDelete }: CommentItemProps) {
  const stack = useCurrentStack();
  const authorName = comment.author.fullName ?? comment.author.userName ?? '';
  const openAuthor = () => openUser(stack, comment.author.id);

  return (
    <View style={styles.row}>
      <Pressable disabled={!comment.author.hasPublicProfile} onPress={openAuthor}>
        <UserAvatar name={authorName} size={36} />
      </Pressable>
      <View style={styles.body}>
        <View style={styles.bubble}>
          <Pressable disabled={!comment.author.hasPublicProfile} onPress={openAuthor}>
            <Text style={styles.author}>{authorName}</Text>
          </Pressable>
          <Text style={styles.text}>{comment.text}</Text>
        </View>
        <View style={styles.meta}>
          <Text style={styles.time}>{formatRelativeTime(comment.createdAt)}</Text>
          {comment.isMine ? (
            <Pressable
              accessibilityRole="button"
              disabled={deleting}
              hitSlop={8}
              onPress={() => onDelete(comment)}
            >
              <Text style={[styles.delete, deleting && styles.deleteDisabled]}>حذف</Text>
            </Pressable>
          ) : null}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  author: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginBottom: 4,
    textAlign: textAlignStart,
  },
  body: {
    flex: 1,
    minWidth: 0,
  },
  bubble: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    padding: 12,
  },
  delete: {
    color: colors.textMuted,
    fontFamily: fonts.arabicMedium,
    fontSize: 10,
    lineHeight: 15,
  },
  deleteDisabled: {
    opacity: 0.4,
  },
  meta: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    marginTop: 4,
    paddingHorizontal: 4,
  },
  row: {
    flexDirection: 'row',
    gap: 12,
    marginBottom: 16,
  },
  text: {
    color: colors.textLabel,
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
});
