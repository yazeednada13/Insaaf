import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';

interface VerifiedBadgeProps {
  size?: 'sm' | 'md';
}

// Figma VerifiedBadge.tsx. Not pressable: the explanatory sheet (spec §16) has no design in the
// Make file (decision D9). Review cards use sm; the QR result and success views use md.
export function VerifiedBadge({ size = 'sm' }: VerifiedBadgeProps) {
  return (
    <View style={[styles.badge, size === 'md' && styles.badgeMd]}>
      <Text style={styles.check}>✓</Text>
      <Text style={styles.label}>زيارة موثقة</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignItems: 'center',
    backgroundColor: colors.verifiedBackground,
    borderColor: colors.verifiedBorder,
    borderRadius: 999,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  badgeMd: {
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  check: {
    color: colors.primary,
    fontSize: 10,
  },
  label: {
    color: colors.primary,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
});
