import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';

interface UserRowProps {
  fullName: string | null;
  userName: string | null;
  subtitle?: string;
  onPress: () => void;
  trailing?: ReactNode;
}

// Figma SearchScreen.tsx people row, reused for the followers and following lists (decision D-9).
export function UserRow({ fullName, userName, subtitle, onPress, trailing }: UserRowProps) {
  const name = fullName ?? userName ?? '';

  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={styles.row}>
      <UserAvatar name={name} size={48} />
      <View style={styles.text}>
        <Text style={styles.name}>{name}</Text>
        {userName ? <Text style={styles.userName}>@{userName}</Text> : null}
        {subtitle ? <Text style={styles.subtitle}>{subtitle}</Text> : null}
      </View>
      {trailing}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  name: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  row: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  subtitle: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 2,
    textAlign: textAlignStart,
  },
  text: {
    flex: 1,
    minWidth: 0,
  },
  userName: {
    color: colors.textPlaceholder,
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: textAlignStart,
  },
});
