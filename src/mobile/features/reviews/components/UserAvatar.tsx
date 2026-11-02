import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';

// Figma UserAvatar.tsx: sand circle; without a photo it shows the first letter of the name.
// The API has no avatar images yet, so the initial is always shown.
export function UserAvatar({ name, size }: { name: string; size: number }) {
  return (
    <View style={[styles.circle, { borderRadius: size / 2, height: size, width: size }]}>
      <Text style={styles.initial}>{name.charAt(0)}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  circle: {
    alignItems: 'center',
    backgroundColor: colors.border,
    justifyContent: 'center',
    overflow: 'hidden',
  },
  initial: {
    color: colors.primary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
});
