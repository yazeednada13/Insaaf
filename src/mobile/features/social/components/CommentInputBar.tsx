import { useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import { SendIcon } from '@/features/social/components/ReviewActionIcons';

/** Matches the backend ReviewCommentValidator (decision D-5). */
const COMMENT_MAX_LENGTH = 500;

interface CommentInputBarProps {
  /** The signed-in user's name or email, for the avatar initial. */
  userLabel: string;
  submitting: boolean;
  error: string | null;
  /** Resolves true when the comment was saved, so the input can clear. */
  onSubmit: (text: string) => Promise<boolean>;
}

// Figma ReviewDetailScreen.tsx comment input. It grows for longer comments, up to four lines.
export function CommentInputBar({ userLabel, submitting, error, onSubmit }: CommentInputBarProps) {
  const [text, setText] = useState('');
  const trimmed = text.trim();
  const canSend = trimmed.length > 0 && !submitting;

  const send = async () => {
    if (canSend && (await onSubmit(trimmed))) {
      setText('');
    }
  };

  return (
    <View style={styles.bar}>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.row}>
        <UserAvatar name={userLabel} size={36} />
        <View style={styles.field}>
          <TextInput
            maxLength={COMMENT_MAX_LENGTH}
            multiline
            onChangeText={setText}
            placeholder="اكتب تعليقًا..."
            placeholderTextColor={colors.textPlaceholder}
            style={styles.input}
            textAlign={textAlignStart}
            textAlignVertical="center"
            value={text}
          />
        </View>
        <Pressable
          accessibilityLabel="إرسال التعليق"
          accessibilityRole="button"
          accessibilityState={{ disabled: !canSend, busy: submitting }}
          disabled={!canSend}
          onPress={() => void send()}
          style={[styles.send, !canSend && styles.sendDisabled]}
        >
          {submitting ? (
            <ActivityIndicator color={colors.primaryForeground} size="small" />
          ) : (
            <SendIcon size={14} />
          )}
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    backgroundColor: colors.surface,
    borderTopColor: 'rgba(0,0,0,0.08)',
    borderTopWidth: 1,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  error: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginBottom: 8,
    textAlign: 'center',
  },
  field: {
    backgroundColor: colors.background,
    borderColor: colors.border,
    borderRadius: 20,
    borderWidth: 1.5,
    flex: 1,
    justifyContent: 'center',
    minHeight: 42,
    paddingHorizontal: 16,
    paddingVertical: 8,
  },
  input: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    maxHeight: 80,
    padding: 0,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
  },
  send: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 18,
    height: 36,
    justifyContent: 'center',
    width: 36,
  },
  sendDisabled: {
    opacity: 0.4,
  },
});
