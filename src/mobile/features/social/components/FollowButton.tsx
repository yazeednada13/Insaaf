import { Pressable, StyleSheet, Text } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';
import { useFollowUser } from '@/features/social/hooks/useFollowUser';

interface FollowButtonProps {
  userId: string;
  isFollowing: boolean;
  size?: 'compact' | 'full';
}

// Figma ProfileScreen.tsx: "متابعة" (solid) / "تتابع" (sand). Compact on rows and Review Detail.
export function FollowButton({ userId, isFollowing, size = 'compact' }: FollowButtonProps) {
  const followMutation = useFollowUser();
  const isFull = size === 'full';

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: isFollowing, disabled: followMutation.isPending }}
      disabled={followMutation.isPending}
      hitSlop={6}
      onPress={() => followMutation.mutate({ userId, follow: !isFollowing })}
      style={[styles.button, isFull && styles.buttonFull, isFollowing && styles.following]}
    >
      <Text style={[styles.label, isFull && styles.labelFull, isFollowing && styles.followingLabel]}>
        {isFollowing ? 'تتابع' : 'متابعة'}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 6,
  },
  buttonFull: {
    flex: 1,
    paddingVertical: 10,
  },
  following: {
    backgroundColor: colors.border,
  },
  followingLabel: {
    color: colors.textPrimary,
  },
  label: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  labelFull: {
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
});
